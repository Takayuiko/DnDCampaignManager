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

function compile(file) {
    const source = fs.readFileSync(path.join(__dirname, '../src/api', file), 'utf8')
        .replace('import.meta.env.VITE_API_BASE_URL', 'undefined');
    return ts.transpileModule(source, {
        compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
    }).outputText;
}

function client() {
    let rejectResponse;
    const refreshes = [], retries = [], tokens = new Map([['token', 'expired']]);
    const location = { href: '' };
    const api = async config => { retries.push(config); return { data: config.url }; };
    api.defaults = { baseURL: '/api', headers: { common: { Authorization: 'Bearer expired' } } };
    api.interceptors = { request: { use() {} }, response: { use(_, reject) { rejectResponse = reject; } } };
    api.post = () => new Promise((resolve, reject) => refreshes.push({ resolve, reject }));
    const localStorage = { getItem: key => tokens.get(key), setItem: (key, value) => tokens.set(key, value), removeItem: key => tokens.delete(key) };
    const exports = {};
    vm.runInNewContext(compile('axios.ts'), { exports, require: () => ({ default: { create: () => api } }), localStorage, window: { location } });
    const unauthorized = url => ({ config: { url, headers: {} }, response: { status: 401 } });
    return { exports, api, localStorage, location, tokens, refreshes, retries, unauthorized, fail: error => rejectResponse(error) };
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
    vm.runInNewContext(compile('aiApi.ts'), { exports: stream, require: name => name === "../Utils/apiError" ? errorExports : c.exports,
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
    vm.runInNewContext(compile('aiApi.ts'), { exports: stream, require: name => name === "../Utils/apiError" ? errorExports : c.exports,
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
