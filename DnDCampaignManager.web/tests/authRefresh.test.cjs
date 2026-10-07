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
).outputText, { AbortController, DOMException, setTimeout, clearTimeout, exports: errorExports, require });

function compile(file) {
    const source = fs.readFileSync(path.join(__dirname, '../src/api', file), 'utf8')
        .replace('import.meta.env.VITE_API_BASE_URL', 'undefined');
    return ts.transpileModule(source, {
        compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
    }).outputText;
}

function lockManager() {
    let tail = Promise.resolve();
    return { request(_, callback) {
        const result = tail.then(callback);
        tail = result.catch(() => {});
        return result;
    } };
}

function client(tokens = new Map([['token', 'expired']]), locks = { request: (_, callback) => callback() }) {
    let rejectResponse;
    const refreshes = [], retries = [];
    const location = { href: '' };
    const api = async config => { retries.push(config); return { data: config.url }; };
    api.defaults = { baseURL: '/api', headers: { common: { Authorization: 'Bearer expired' } } };
    api.interceptors = { request: { use() {} }, response: { use(_, reject) { rejectResponse = reject; } } };
    api.post = () => new Promise((resolve, reject) => refreshes.push({ resolve, reject }));
    const localStorage = { getItem: key => tokens.get(key), setItem: (key, value) => tokens.set(key, value), removeItem: key => tokens.delete(key) };
    const exports = {};
    vm.runInNewContext(compile('axios.ts'), { AbortController, DOMException, setTimeout, clearTimeout, exports, require: name => name === "../Utils/requestCancellation" ? require("./helpers/requestCancellation.cjs") : ({ default: { create: () => api } }), localStorage, navigator: { locks }, window: { location } });
    const unauthorized = url => ({ config: { url, headers: { Authorization: 'Bearer expired' } }, response: { status: 401 } });
    return { AbortController, DOMException, setTimeout, clearTimeout, exports, api, localStorage, location, tokens, refreshes, retries, unauthorized, fail: error => rejectResponse(error) };
}

test('failed refresh rejects all concurrent requests and permits a later fresh attempt', { timeout: 2000 }, async () => {
    const c = client();
    const errors = [c.unauthorized('/campaigns'), c.unauthorized('/maps'), c.unauthorized('/characters')];
    const settled = Promise.allSettled(errors.map(c.fail));
    assert.equal(c.refreshes.length, 1);
    c.refreshes[0].reject(new Error('Session expired'));
    const results = await settled;
    results.forEach((result, i) => { assert.equal(result.status, 'rejected'); assert.equal(result.reason, errors[i]); });
    assert.equal(c.tokens.has('token'), false);
    assert.equal(c.api.defaults.headers.common.Authorization, undefined);
    assert.equal(c.location.href, '/login');
    assert.equal(c.retries.length, 0);
    c.tokens.set('token', 'expired'); // A subsequent login establishes a fresh session.
    const next = c.fail(c.unauthorized('/campaigns'));
    assert.equal(c.refreshes.length, 2);
    c.refreshes[1].resolve({ data: { accessToken: 'new' } });
    await next;
    assert.equal(c.retries.length, 1);
});

test('successful refresh retries all waiting requests once using the new token', async () => {
    const c = client();
    const errors = [c.unauthorized('/campaigns'), c.unauthorized('/maps')];
    const requests = errors.map(c.fail);
    assert.equal(c.refreshes.length, 1);
    c.refreshes[0].resolve({ data: { accessToken: 'new' } });
    await Promise.all(requests);
    assert.equal(c.tokens.get('token'), 'new');
    assert.equal(c.retries.length, 2);
    for (const error of errors) {
        assert.equal(error.config.headers.Authorization, 'Bearer new');
        assert.equal(error.config._retry, true);
        await assert.rejects(c.fail(error), reason => reason === error);
    }
    assert.equal(c.refreshes.length, 1);
});

test('auth endpoint failures and errors without request configuration do not refresh', async () => {
    const c = client();
    for (const error of [c.unauthorized('/auth/login'), c.unauthorized('/auth/register'), c.unauthorized('/auth/refresh'), { response: { status: 401 } }]) {
        await assert.rejects(c.fail(error), reason => reason === error);
    }
    assert.equal(c.refreshes.length, 0);
});

test('AI streaming and Axios requests share refresh failure without hanging', { timeout: 2000 }, async () => {
    const c = client();
    const stream = {};
    vm.runInNewContext(compile('aiApi.ts'), { AbortController, DOMException, setTimeout, clearTimeout, exports: stream, require: name => name === "../Utils/apiError" ? errorExports : name === "../Utils/requestCancellation" ? require("./helpers/requestCancellation.cjs") : c.exports,
        localStorage: c.localStorage, fetch: async () => ({ status: 401 }) });
    const request = c.fail(c.unauthorized('/campaigns'));
    const streaming = stream.streamAIMessage('conversation', 'Hello', () => {});
    const settled = Promise.allSettled([request, streaming]);
    await Promise.resolve();
    assert.equal(c.refreshes.length, 1);
    c.refreshes[0].reject(new Error('Session expired'));
    const results = await settled;
    assert.equal(results[0].status, 'rejected');
    assert.equal(results[1].status, 'rejected');
    assert.equal(results[1].reason.message, 'Your session has expired.');
});

test('AI streaming retries with the shared refreshed token and receives completion', async () => {
    const c = client();
    const stream = {}, calls = [];
    const done = { conversationId: 'conversation', messageId: 2, model: 'test', durationMs: 25,
        reply: 'Hello', usage: { inputTokens: 1, outputTokens: 1, totalTokens: 2, estimatedCostUsd: 0.001 } };
    vm.runInNewContext(compile('aiApi.ts'), { AbortController, DOMException, setTimeout, clearTimeout, exports: stream, require: name => name === "../Utils/apiError" ? errorExports : name === "../Utils/requestCancellation" ? require("./helpers/requestCancellation.cjs") : c.exports,
        localStorage: c.localStorage, TextDecoder,
        fetch: async (_, options) => {
            calls.push(options);
            if (calls.length === 1) return { status: 401 };
            return { status: 200, ok: true, body: new ReadableStream({ start(controller) {
                controller.enqueue(new TextEncoder().encode(`event: done\ndata: ${JSON.stringify(done)}\n\n`));
                controller.close();
            } }) };
        } });
    const request = c.fail(c.unauthorized('/campaigns'));
    const streaming = stream.streamAIMessage('conversation', 'Hello', () => {});
    await Promise.resolve();
    assert.equal(c.refreshes.length, 1);
    c.refreshes[0].resolve({ data: { accessToken: 'new' } });
    const [, completion] = await Promise.all([request, streaming]);
    assert.equal(completion.reply, 'Hello');
    assert.equal(calls.length, 2);
    assert.equal(calls[1].headers.Authorization, 'Bearer new');
});

const flush = () => new Promise(resolve => setImmediate(resolve));

test('three tabs rotate once and late unauthorized responses reuse the published token', async () => {
    const storage = new Map([['token', 'expired']]), locks = lockManager();
    const tabs = Array.from({ length: 3 }, () => client(storage, locks));
    const requests = tabs.map(tab => tab.fail(tab.unauthorized('/campaigns')));
    await flush();
    assert.equal(tabs.reduce((sum, tab) => sum + tab.refreshes.length, 0), 1);
    tabs[0].refreshes[0].resolve({ data: { accessToken: 'new' } });
    await Promise.all(requests);
    await tabs[2].fail(tabs[2].unauthorized('/maps'));
    assert.equal(tabs.reduce((sum, tab) => sum + tab.refreshes.length, 0), 1);
    tabs.forEach(tab => {
        assert.equal(tab.location.href, '');
        tab.retries.forEach(config => assert.equal(config.headers.Authorization, 'Bearer new'));
    });
});

test('refresh failure is published before queued tabs can rotate and all waiters settle', async () => {
    const storage = new Map([['token', 'expired']]), locks = lockManager();
    const tabs = Array.from({ length: 3 }, () => client(storage, locks));
    const settled = Promise.allSettled(tabs.map(tab => tab.fail(tab.unauthorized('/campaigns'))));
    await flush();
    tabs[0].refreshes[0].reject(new Error('Invalid cookie'));
    const results = await settled;
    assert.ok(results.every(result => result.status === 'rejected'));
    assert.equal(tabs.reduce((sum, tab) => sum + tab.refreshes.length, 0), 1);
    tabs.forEach(tab => assert.equal(tab.location.href, '/login'));
    assert.equal(storage.has('token'), false);
});

test('logout during a rotation does not restore the token or start a queued refresh', async () => {
    const storage = new Map([['token', 'expired']]), locks = lockManager();
    const first = client(storage, locks), second = client(storage, locks);
    const settled = Promise.allSettled([first.exports.refreshAccessToken(), second.exports.refreshAccessToken()]);
    await flush();
    storage.delete('token');
    first.refreshes[0].resolve({ data: { accessToken: 'obsolete' } });
    assert.ok((await settled).every(result => result.status === 'rejected'));
    assert.equal(storage.has('token'), false);
    assert.equal(second.refreshes.length, 0);
});

test('an obsolete refresh failure cannot clear a newer login', async () => {
    const c = client();
    const pending = c.exports.refreshAccessToken();
    c.tokens.set('token', 'different-session');
    c.refreshes[0].reject(new Error('Old session failed'));
    await assert.rejects(pending);
    assert.equal(c.tokens.get('token'), 'different-session');
    assert.equal(c.location.href, '');
});

test('unsupported browser fails safely without racing a refresh request', async () => {
    const c = client(new Map([['token', 'expired']]), null);
    await assert.rejects(c.exports.refreshAccessToken(), /Web Locks/);
    assert.equal(c.refreshes.length, 0);
    assert.equal(c.location.href, '/login');
});

test('cancelled Axios callers do not retry or cancel refresh for other requests', async () => {
    const c = client(), controller = new AbortController();
    const cancelledError = c.unauthorized('/maps'); cancelledError.config.signal = controller.signal;
    const pending = c.fail(cancelledError);
    const rejected = assert.rejects(pending, error => error === cancelledError);
    const other = c.fail(c.unauthorized('/campaigns'));
    controller.abort(); await rejected;
    assert.equal(c.location.href, ''); assert.equal(c.refreshes.length, 1);
    c.refreshes[0].resolve({ data: { accessToken: 'new' } }); await other;
    assert.equal(c.retries.length, 1); assert.equal(c.retries[0].url, '/campaigns');
    const preCancelled = c.unauthorized('/characters'); preCancelled.config.signal = controller.signal;
    await assert.rejects(c.fail(preCancelled), error => error === preCancelled);
    assert.equal(c.refreshes.length, 1);
});
