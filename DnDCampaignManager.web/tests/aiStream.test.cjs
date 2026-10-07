const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');

const errorExports = {};
vm.runInNewContext(ts.transpileModule(
    fs.readFileSync(path.join(__dirname, '../src/Utils/apiError.ts'), 'utf8'),
    { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }
).outputText, { exports: errorExports, require });

const compiled = ts.transpileModule(
    fs.readFileSync(path.join(__dirname, '../src/api/aiApi.ts'), 'utf8'),
    { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }
).outputText;
const done = {
    conversationId: 'conversation', messageId: 2, model: 'test', durationMs: 25, reply: 'Hello 🐉',
    usage: { inputTokens: 100, outputTokens: 20, totalTokens: 120, estimatedCostUsd: 0.001 }
};

function client(streamText, chunkSize = 7, cancel = () => {}, keepOpen = false) {
    const exportsObject = {};
    const bytes = new TextEncoder().encode(streamText);
    let offset = 0;
    const body = new ReadableStream({
        cancel,
        pull(controller) {
            if (offset >= bytes.length) { if (!keepOpen) controller.close(); return; }
            controller.enqueue(bytes.slice(offset, offset + chunkSize));
            offset += chunkSize;
        }
    });
    vm.runInNewContext(compiled, {
        exports: exportsObject, TextDecoder,
        localStorage: { getItem: () => 'test-token' },
        require: name => name === '../Utils/apiError' ? errorExports : ({ default: { defaults: { baseURL: '/api' } } }),
        fetch: async () => ({ ok: true, status: 200, body })
    });
    return exportsObject;
}

for (const newline of ['\n', '\r\n']) {
    test(`fragmented ${JSON.stringify(newline)} stream completes with exact usage and UTF-8 text`, async () => {
        const stream = `event: token${newline}data: ${JSON.stringify({ text: 'Hello 🐉' })}${newline}${newline}` +
            `event: done${newline}data: ${JSON.stringify(done)}${newline}${newline}`;
        let text = '';
        const result = await client(stream, 1).streamAIMessage('conversation', 'Hi', token => { text += token; });
        assert.equal(text, 'Hello 🐉');
        assert.equal(result.usage.inputTokens, 100);
        assert.equal(result.usage.outputTokens, 20);
        assert.equal(result.usage.totalTokens, 120);
        assert.equal(result.durationMs, 25);
        assert.equal(result.reply, 'Hello 🐉');
    });
}

test('legacy PascalCase usage fails safely instead of returning undefined counts', async () => {
    const legacy = { ...done, usage: { InputTokens: 100, OutputTokens: 20, TotalTokens: 120 } };
    await assert.rejects(client(`event: done\ndata: ${JSON.stringify(legacy)}\n\n`).streamAIMessage('conversation', 'Hi', () => {}),
        /Please refresh the conversation/);
});

test('an interrupted stream does not claim completion', async () => {
    await assert.rejects(client('event: token\ndata: {"text":"Partial"}\n\n').streamAIMessage('conversation', 'Hi', () => {}),
        /ended before a completion/);
});

test('an explicit stream error is returned to the chat', async () => {
    await assert.rejects(client('event: error\ndata: {"error":"Please try again."}\n\n').streamAIMessage('conversation', 'Hi', () => {}),
        /Please try again/);
});

test('completion resolves even when stream cleanup never finishes', async () => {
    const api = client(`event: done\ndata: ${JSON.stringify(done)}\n\n`, 4096, () => new Promise(() => {}), true);
    let timer;
    try {
        const result = await Promise.race([
            api.streamAIMessage('conversation', 'Hi', () => {}),
            new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('Completion is waiting for cleanup')), 200); })
        ]);
        assert.equal(result.messageId, 2);
    } finally { clearTimeout(timer); }
});

test('chat re-enables its input and Send for two consecutive replies', async () => {
    const states = [];
    let cursor = 0;
    let requests = 0;
    const chatExports = {};
    const chatCompiled = ts.transpileModule(
        fs.readFileSync(path.join(__dirname, '../src/pages/AIChat.tsx'), 'utf8'),
        { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX } }
    ).outputText;
    const initialConversation = { id: 'conversation', title: 'Chat', messages: [] };
    vm.runInNewContext(chatCompiled, {
        exports: chatExports,
        require(name) {
            if (name === 'react') return {
                useState(initial) {
                    const index = cursor++;
                    if (!(index in states)) states[index] = index === 1 ? initialConversation : index === 5 ? false : initial;
                    return [states[index], value => { states[index] = typeof value === 'function' ? value(states[index]) : value; }];
                },
                useMemo: factory => factory(), useRef: initial => ({ current: initial }), useEffect() {}
            };
            if (name === '../api/aiApi') return {
                async streamAIMessage(...args) {
                    requests++;
                    const completion = { ...done, messageId: requests };
                    return client(`event: done\ndata: ${JSON.stringify(completion)}\n\n`, 4096,
                        () => new Promise(() => {}), true).streamAIMessage(...args);
                }
            };
            if (name === '../api/campaignApi') return {};
            if (name === '../auth/AuthContext') return { useAuth: () => ({ user: { role: 'DM' } }) };
            return require(name);
        }
    });
    function render() { cursor = 0; return chatExports.default(); }
    function elements(tree) {
        if (!tree || typeof tree !== 'object') return [];
        return [tree, ...[tree.props?.children].flat(Infinity).flatMap(elements)];
    }
    for (const message of ['First message', 'Second message']) {
        const input = elements(render()).find(node => node.type === 'input');
        assert.equal(input.props.disabled, false);
        input.props.onChange({ target: { value: message } });
        const nodes = elements(render());
        assert.equal(nodes.find(node => node.type === 'button' && node.props.type === 'submit').props.disabled, false);
        const pending = nodes.find(node => node.type === 'form').props.onSubmit({ preventDefault() {} });
        assert.equal(elements(render()).find(node => node.type === 'input').props.disabled, true);
        let timer;
        try {
            await Promise.race([pending, new Promise((_, reject) => {
                timer = setTimeout(() => reject(new Error('Chat stayed loading after completion')), 200);
            })]);
        } finally { clearTimeout(timer); }
        assert.equal(elements(render()).find(node => node.type === 'input').props.disabled, false);
    }
    assert.equal(requests, 2);
    assert.equal(states[1].messages.filter(message => message.role === 'assistant' && message.status === 'completed').length, 2);
});
