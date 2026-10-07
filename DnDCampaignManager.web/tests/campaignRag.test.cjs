const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');

function compile(file) {
    return ts.transpileModule(fs.readFileSync(path.join(__dirname, '../src', file), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
    }).outputText;
}
function elements(tree) {
    if (!tree || typeof tree !== 'object') return [];
    return [tree, ...[tree.props?.children].flat(Infinity).flatMap(elements)];
}

test('campaign dropdown opens the corresponding personal chat and removes general/new chat controls', async () => {
    const calls = [];
    const exportsObject = {};
    vm.runInNewContext(compile('api/aiApi.ts'), {
        AbortController, DOMException, setTimeout, clearTimeout, exports: exportsObject, require: () => ({ default: { post: async (url, body) => { calls.push({ url, body }); return { data: {} }; } } })
    });
    await exportsObject.createConversation(undefined, 12);
    await exportsObject.createConversation();
    assert.equal(calls[0].body.campaignId, 12);
    assert.equal(calls[1].body.campaignId, null);

    const states = []; let cursor = 0; let createdScope;
    const chat = {};
    vm.runInNewContext(compile('pages/AIChat.tsx'), {
        AbortController, DOMException, setTimeout, clearTimeout, exports: chat,
        require(name) {
            if (name === 'react') return {
                useState(initial) {
                    const index = cursor++;
                    if (!(index in states)) states[index] = index === 1 ? { id: 'old', title: 'Old', campaignId: 5, messages: [] }
                        : index === 5 ? false : index === 8 ? [{ id: 5, name: 'First' }, { id: 12, name: 'Second' }] : initial;
                    return [states[index], value => { states[index] = typeof value === 'function' ? value(states[index]) : value; }];
                }, useRef: initial => ({ current: initial }), useEffect() {}, useMemo: f => f()
            };
            if (name === '../api/campaignApi') return {};
            if (name === '../auth/AuthContext') return { useAuth: () => ({ user: { role: 'DM' } }) };
            if (name === '../api/aiApi') return { async createConversation(title, campaignId) {
                createdScope = campaignId;
                return { data: { id: 'new', title: 'New', campaignId, messageCount: 0 } };
            }, async getConversation(id) { return { data: { id, title: 'Selected campaign', campaignId: 12, messages: [] } }; } };
            return require(name);
        }
    });
    function render() { cursor = 0; return chat.default(); }
    elements(render()).find(n => n.type === 'select').props.onChange({ target: { value: '12' } });
    assert.equal(states[1], null);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(createdScope, 12);
    assert.equal(states[1].campaignId, 12);
    assert.equal(elements(render()).find(n => n.type === 'select').props.value, '12');
    assert.equal(elements(render()).some(n => n.props?.children === '+ New'), false);
});

function sessionPage(owner = true, failTranscription = false, deferredTranscription = null, retryStatus = 'ready') {
    const states = []; let cursor = 0;
    const exportsObject = {};
    const calls = [];
    vm.runInNewContext(compile('pages/SessionNotes.tsx'), {
        AbortController, DOMException, setTimeout, clearTimeout, exports: exportsObject, confirm: () => true,
        require(name) {
            if (name === 'react') return {
                useState(initial) {
                    const index = cursor++;
                    if (!(index in states)) states[index] = index === 0 ? { id: 12, name: 'Campaign', ownerId: 1 }
                        : index === 2 ? false : typeof initial === 'function' ? initial() : initial;
                    return [states[index], value => { states[index] = typeof value === 'function' ? value(states[index]) : value; }];
                }, useRef(initial) {
                    const index = cursor++;
                    if (!(index in states)) states[index] = { current: initial };
                    return states[index];
                }, useEffect() {}
            };
            if (name === 'react-router-dom') return { useParams: () => ({ campaignId: '12' }), Link: 'a' };
            if (name === '../auth/AuthContext') return { useAuth: () => ({ user: { id: owner ? 1 : 2, role: owner ? 'DM' : 'Player' } }) };
            if (name === '../api/campaignApi') return {};
            if (name === '../Utils/apiError') return { extractApiError: (_, fallback) => fallback };
            if (name === '../api/sessionNotesApi') return {
                async transcribeSessionAudio(_, audio, signal) {
                    if (deferredTranscription) {
                        deferredTranscription.signal = signal;
                        return new Promise(resolve => { deferredTranscription.resolve = resolve; });
                    }
                    if (failTranscription) throw new Error('Provider diagnostics');
                    return { data: { text: 'Freya found a silver key in the recorded session.', model: 'test-transcription' } };
                },
                async createSessionNote(campaignId, body) {
                    calls.push({ campaignId, body });
                    return { data: { ...body, id: 1, campaignId, indexStatus: 'failed', chunkCount: 0, embeddingInputTokens: 0 } };
                },
                async indexSessionNote() { return { data: { ...states[1][0], indexStatus: retryStatus, chunkCount: 1, embeddingInputTokens: 15 } }; }
            };
            return require(name);
        }
    });
    return { states, calls, render() { cursor = 0; return exportsObject.default(); } };
}

test('a failed embedding leaves saved notes visible and retry makes them searchable', async () => {
    const page = sessionPage();
    const nodes = elements(page.render());
    nodes.find(n => n.type === 'textarea').props.onChange({ target: { value: 'A silver key was found.' } });
    nodes.find(n => n.type === 'input' && n.props.maxLength === 160).props.onChange({ target: { value: 'The key' } });
    await elements(page.render()).find(n => n.type === 'form').props.onSubmit({ preventDefault() {} });
    assert.equal(page.calls[0].campaignId, 12);
    assert.equal(page.states[1][0].content, 'A silver key was found.');
    assert.match(page.states[5], /Session saved/);
    elements(page.render()).find(n => n.type === 'button' && n.props.children === 'Retry indexing').props.onClick();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(page.states[1][0].indexStatus, 'ready');
    assert.equal(page.states[3], false);
});

test('cancel transcription preserves the draft and ignores a late response', async () => {
    const deferred = {}, page = sessionPage(true, false, deferred);
    let nodes = elements(page.render());
    nodes.find(node => node.type === 'textarea').props.onChange({ target: { value: 'Unsaved notes' } });
    nodes.find(node => node.type === 'input' && node.props.type === 'file').props.onChange({ target: { files: [{ size: 1 }] } });
    nodes = elements(page.render());
    nodes.find(node => node.type === 'button' && node.props.children === 'Transcribe audio').props.onClick();
    nodes = elements(page.render());
    nodes.find(node => node.type === 'button' && node.props.children === 'Cancel transcription').props.onClick();
    assert.equal(deferred.signal.aborted, true);
    deferred.resolve({ data: { text: 'Late transcript' } });
    await new Promise(resolve => setImmediate(resolve));
    nodes = elements(page.render());
    assert.equal(nodes.find(node => node.type === 'textarea').props.value, 'Unsaved notes');
    assert.equal(nodes.find(node => node.type === 'button' && node.props.children === 'Transcribe audio').props.disabled, false);
    assert.ok(nodes.some(node => node.props.role === 'status' && /cancelled/.test(node.props.children)));
});

test('an active indexing lease is displayed as work in progress rather than a provider failure', async () => {
    const page = sessionPage(true, false, null, 'pending'); page.render();
    page.states[1] = [{ id: 1, title: 'Saved session', content: 'Notes', sessionNumber: 1, indexStatus: 'failed' }];
    elements(page.render()).find(node => node.type === 'button' && node.props.children === 'Retry indexing').props.onClick();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(page.states[1][0].indexStatus, 'pending');
    assert.ok(elements(page.render()).some(node => node.props.role === 'status' && /already in progress/.test(node.props.children)));
});

test('campaign players can read notes but do not see an authoring form', () => {
    const page = sessionPage(false);
    assert.equal(elements(page.render()).some(n => n.type === 'form'), false);
});

test('audio fills a reviewable draft and enters session knowledge only after Save', async () => {
    const page = sessionPage();
    elements(page.render()).find(n => n.type === 'input' && n.props.type === 'file').props.onChange({ target: { files: [{ size: 100, name: 'session.mp3' }] } });
    elements(page.render()).find(n => n.type === 'button' && n.props.children === 'Transcribe audio').props.onClick();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(page.states[8], 'Freya found a silver key in the recorded session.');
    assert.equal(page.calls.length, 0);
    assert.equal(page.states[3], false);
    await elements(page.render()).find(n => n.type === 'form').props.onSubmit({ preventDefault() {} });
    assert.equal(page.calls[0].body.content, 'Freya found a silver key in the recorded session.');
});

test('failed audio transcription keeps existing draft text and clears loading', async () => {
    const page = sessionPage(true, true);
    elements(page.render()).find(n => n.type === 'textarea').props.onChange({ target: { value: 'Existing notes' } });
    elements(page.render()).find(n => n.type === 'input' && n.props.type === 'file').props.onChange({ target: { files: [{ size: 100, name: 'session.mp3' }] } });
    elements(page.render()).find(n => n.type === 'button' && n.props.children === 'Transcribe audio').props.onClick();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(page.states[8], 'Existing notes');
    assert.equal(page.states[3], false);
    assert.match(page.states[4], /existing draft is unchanged/);
    assert.equal(page.states[4].includes('diagnostic'), false);
});

test('players cannot select general chat and need campaign membership to start a chat', () => {
    for (const campaigns of [[], [{ id: 12, name: 'Joined campaign' }]]) {
        let cursor = 0;
        const chat = {};
        vm.runInNewContext(compile('pages/AIChat.tsx'), {
            AbortController, DOMException, setTimeout, clearTimeout, exports: chat,
            require(name) {
                if (name === 'react') return {
                    useState(initial) {
                        const index = cursor++;
                        return [index === 5 ? false : index === 8 ? campaigns : initial, () => {}];
                    }, useRef: initial => ({ current: initial }), useEffect() {}, useMemo: f => f()
                };
                if (name === '../auth/AuthContext') return { useAuth: () => ({ user: { role: 'Player' } }) };
                if (name === '../api/campaignApi' || name === '../api/aiApi') return {};
                return require(name);
            }
        });
        cursor = 0;
        const nodes = elements(chat.default());
        assert.equal(nodes.some(n => n.type === 'option' && n.props.children === 'General D&D chat'), false);
        assert.equal(nodes.some(n => n.type === 'button' && n.props.children === '+ New'), false);
        assert.equal(nodes.find(n => n.type === 'select').props.value, '');
        if (campaigns.length === 0) assert.equal(nodes.some(n => n.props?.children === 'Join a campaign to use AI chat.'), true);
    }
});


test('unconfigured AI disables sending while saved conversations remain visible', async () => {
    const states = []; let cursor = 0; let requests = 0;
    const chat = {};
    vm.runInNewContext(compile('pages/AIChat.tsx'), {
        AbortController, DOMException, setTimeout, clearTimeout, exports: chat,
        require(name) {
            if (name === 'react') return {
                useState(initial) {
                    const index = cursor++;
                    if (!(index in states)) states[index] = index === 2 ? { configured: false, model: 'test', provider: 'OpenAI' }
                        : index === 1 ? { id: 'saved', title: 'Saved', campaignId: 1, messages: [] }
                        : index === 5 ? false : initial;
                    return [states[index], value => { states[index] = typeof value === 'function' ? value(states[index]) : value; }];
                }, useRef: initial => ({ current: initial }), useEffect() {}, useMemo: f => f()
            };
            if (name === '../api/aiApi') return { streamAIMessage() { requests++; } };
            if (name === '../api/campaignApi') return {};
            return require(name);
        }
    });
    function render() { cursor = 0; return chat.default(); }
    const nodes = elements(render());
    assert.equal(nodes.find(n => n.type === 'input').props.disabled, true);
    assert.equal(nodes.find(n => n.type === 'button' && n.props.type === 'submit').props.disabled, true);
    assert.equal(nodes.some(n => typeof n.props?.children === 'string' && n.props.children.includes('AI chat is unavailable')), true);
    assert.equal(nodes.find(n => n.type === 'button' && n.props.children === 'Clear chat').props.disabled, false);
    await nodes.find(n => n.type === 'form').props.onSubmit({ preventDefault() {} });
    assert.equal(requests, 0);
    assert.equal(states[1].id, 'saved');
    assert.equal(states[1].messages.length, 0);
});
