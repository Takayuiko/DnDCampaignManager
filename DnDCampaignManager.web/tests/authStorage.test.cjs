const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');

function provider() {
    const values = [], listeners = new Map(), requests = [], exports = {};
    const stored = new Map([['token', 'initial']]);
    const localStorage = { getItem: key => stored.get(key) ?? null,
        setItem: (key, value) => stored.set(key, value), removeItem: key => stored.delete(key) };
    const api = { defaults: { headers: { common: {} } }, post: async () => {} };
    let cursor = 0, effect, cleanup;
    const react = { createContext: () => ({ Provider: 'provider' }), useContext() {},
        useState(initial) { const index = cursor++; if (!(index in values)) values[index] = initial;
            return [values[index], value => { values[index] = value; }]; },
        useEffect(callback) { effect = callback; } };
    const source = ts.transpileModule(fs.readFileSync(path.join(__dirname, '../src/auth/AuthContext.tsx'), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
    }).outputText;
    vm.runInNewContext(source, { AbortController, DOMException, setTimeout, clearTimeout, exports, localStorage,
        window: { addEventListener: (name, callback) => listeners.set(name, callback),
            removeEventListener: name => listeners.delete(name) },
        require: name => name === 'react' ? react : name === '../api/axios' ? { default: api }
            : name === '../api/authApi' ? { getMe: () => new Promise((resolve, reject) => requests.push({ resolve, reject })) }
                : require(name) });
    const render = () => { cursor = 0; return exports.AuthProvider({ children: null }).props.value; };
    render(); cleanup = effect();
    return { requests, stored, api, render, cleanup, listeners,
        event(key = 'token') { listeners.get('storage')({ key, storageArea: localStorage }); } };
}
const flush = () => new Promise(resolve => setImmediate(resolve));

test('provider reads rotated tokens and synchronizes refresh and logout from other tabs', async () => {
    const h = provider();
    const user = { id: 1, role: 'DM' };
    h.stored.set('token', 'bootstrap-refreshed');
    h.requests[0].resolve({ data: user });
    await flush();
    assert.equal(h.render().token, 'bootstrap-refreshed');
    h.stored.set('token', 'other-tab-refreshed'); h.event();
    h.requests[1].resolve({ data: user }); await flush();
    assert.equal(h.render().token, 'other-tab-refreshed');
    assert.equal(h.api.defaults.headers.common.Authorization, 'Bearer other-tab-refreshed');
    h.stored.delete('token'); h.event();
    assert.equal(h.render().user, null);
    assert.equal(h.render().token, null);
    assert.equal(h.api.defaults.headers.common.Authorization, undefined);
    h.cleanup(); assert.equal(h.listeners.size, 0);
});

test('late user responses cannot restore auth after another tab signs out or clears storage', async () => {
    const h = provider();
    h.stored.set('token', 'new-login'); h.event();
    h.stored.clear(); h.event(null);
    h.requests[0].resolve({ data: { id: 1 } });
    h.requests[1].resolve({ data: { id: 2 } });
    await flush();
    assert.equal(h.render().user, null);
    assert.equal(h.render().token, null);
    assert.equal(h.render().loading, false);
    h.cleanup();
});

test('older failed user loads cannot erase a newer tab login', async () => {
    const h = provider();
    h.stored.set('token', 'new-login'); h.event();
    h.requests[0].reject(new Error('Old request failed'));
    h.requests[1].resolve({ data: { id: 2 } }); await flush();
    assert.equal(h.stored.get('token'), 'new-login');
    assert.equal(h.render().user.id, 2);
    h.cleanup();
});
