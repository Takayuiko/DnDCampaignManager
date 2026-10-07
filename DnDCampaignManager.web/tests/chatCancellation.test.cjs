const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const source = ts.transpileModule(fs.readFileSync(path.join(__dirname, '../src/pages/AIChat.tsx'), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
}).outputText;
const nodes = tree => !tree || typeof tree !== 'object' ? [] :
    [tree, ...[tree.props?.children].flat(Infinity).flatMap(nodes)];

function page() {
    const states = [], effects = [], exports = {}, requests = []; let cursor = 0;
    vm.runInNewContext(source, { exports, AbortController, Error,
        require: name => name === 'react' ? {
            useState(initial) {
                const index = cursor++;
                if (!(index in states)) states[index] = index === 1 ? { id: 'chat', messages: [] } : index === 5 ? false : initial;
                return [states[index], value => states[index] = typeof value === 'function' ? value(states[index]) : value];
            }, useRef(initial) {
                const index = cursor++; if (!(index in states)) states[index] = { current: initial }; return states[index];
            }, useMemo: factory => factory(), useEffect: callback => effects.push(callback)
        } : name === '../api/aiApi' ? {
            streamAIMessage(id, text, onToken, signal) {
                requests.push({ signal }); onToken('Partial reply');
                return new Promise((_, reject) => signal.addEventListener('abort', () => reject(signal.reason), { once: true }));
            }
        } : name === '../api/campaignApi' ? {} : require(name) });
    const render = () => { cursor = 0; effects.length = 0; return nodes(exports.default()); };
    const send = () => {
        render().find(node => node.type === 'input').props.onChange({ target: { value: 'Hello' } });
        return render().find(node => node.type === 'form').props.onSubmit({ preventDefault() {} });
    };
    return { render, send, states, requests, unmount() { effects.at(-1)()(); } };
}

test('Stop aborts generation, retains a partial reply as interrupted, and re-enables Send', async () => {
    const p = page(); const request = p.send();
    p.render().find(node => node.type === 'button' && node.props.children === 'Stop').props.onClick();
    await request;
    assert.equal(p.requests[0].signal.aborted, true);
    assert.equal(p.states[1].messages.at(-1).content, 'Partial reply');
    assert.equal(p.states[1].messages.at(-1).status, 'failed');
    assert.match(p.states[6], /Generation stopped/);
    assert.equal(p.render().find(node => node.type === 'button' && node.props.type === 'submit').props.disabled, false);
});

test('leaving chat aborts generation and late rejection does not update page state', async () => {
    const p = page(); const request = p.send();
    p.unmount();
    const before = JSON.stringify(p.states.slice(0, 11));
    await request;
    assert.equal(p.requests[0].signal.aborted, true);
    assert.equal(JSON.stringify(p.states.slice(0, 11)), before);
});
