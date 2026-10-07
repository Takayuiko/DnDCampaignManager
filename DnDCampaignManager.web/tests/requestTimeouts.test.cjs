const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const cancellation = require('./helpers/requestCancellation.cjs');
const compile = file => ts.transpileModule(fs.readFileSync(path.join(__dirname, '../src', file), 'utf8')
    .replace('import.meta.env.VITE_API_BASE_URL', 'undefined'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
}).outputText;
const flush = () => new Promise(resolve => setImmediate(resolve));

function stream(fetch, refreshAccessToken) {
    const exports = {}, timers = new Map(); let next = 0;
    vm.runInNewContext(compile('api/aiApi.ts'), { exports, AbortController, DOMException, TextDecoder,
        localStorage: { getItem: () => 'expired' }, fetch,
        setTimeout: (callback, duration) => { timers.set(++next, { callback, duration }); return next; },
        clearTimeout: id => timers.delete(id),
        require: name => name === '../Utils/requestCancellation' ? cancellation
            : name === '../Utils/apiError' ? { extractErrorMessage: (_, fallback) => fallback }
                : { default: { defaults: { baseURL: '/api' } }, refreshAccessToken } });
    return { exports, timers };
}

test('stream deadline aborts a stalled header request and clears its timer', async () => {
    let signal;
    const c = stream((_, options) => { signal = options.signal; return new Promise(() => {}); });
    const request = c.exports.streamAIMessage('chat', 'Hello', () => {});
    const rejected = assert.rejects(request, error => error.name === 'TimeoutError' && /timed out/.test(error.message));
    const timer = [...c.timers.values()][0];
    assert.equal(timer.duration, 150000); timer.callback();
    await rejected;
    assert.equal(signal.aborted, true); assert.equal(c.timers.size, 0);
});

test('cancellation and deadline release a stalled response reader without claiming completion', async () => {
    for (const timedOut of [false, true]) {
        let cancelled = 0;
        const body = new ReadableStream({ cancel() { cancelled++; } });
        const c = stream(async () => ({ ok: true, status: 200, body }));
        const controller = new AbortController();
        const request = c.exports.streamAIMessage('chat', 'Hello', () => {}, controller.signal);
        const rejected = assert.rejects(request, error => error.name === (timedOut ? 'TimeoutError' : 'AbortError'));
        await flush(); assert.equal(body.locked, true);
        if (timedOut) [...c.timers.values()][0].callback(); else controller.abort();
        await rejected;
        assert.equal(cancelled, 1); assert.equal(body.locked, false); assert.equal(c.timers.size, 0);
    }
});

test('pre-cancelled streams never send and cancellation during refresh does not send a retry', async () => {
    let calls = 0, finishRefresh;
    const refresh = new Promise(resolve => { finishRefresh = resolve; });
    const c = stream(async () => { calls++; return { status: 401 }; }, () => refresh);
    const pre = new AbortController(); pre.abort();
    await assert.rejects(c.exports.streamAIMessage('chat', 'Hello', () => {}, pre.signal), error => error.name === 'AbortError');
    assert.equal(calls, 0);
    const controller = new AbortController();
    const request = c.exports.streamAIMessage('chat', 'Hello', () => {}, controller.signal);
    const rejected = assert.rejects(request, error => error.name === 'AbortError');
    await flush(); controller.abort(); await rejected;
    finishRefresh('new'); await flush();
    assert.equal(calls, 1); assert.equal(c.timers.size, 0);
});

test('Axios default and long-operation overrides are bounded and forward cancellation signals', async () => {
    let settings; const calls = [];
    const api = { defaults: { headers: { common: {} } },
        interceptors: { request: { use() {} }, response: { use() {} } },
        post: async (...args) => { calls.push(args); return {}; }, get: async (...args) => { calls.push(args); return {}; } };
    const exports = {};
    vm.runInNewContext(compile('api/axios.ts'), { exports,
        require: name => name === '../Utils/requestCancellation' ? cancellation
            : { default: { create: options => { settings = options; return api; } } } });
    assert.equal(settings.timeout, 30000);
    const notes = {}, maps = {}, controller = new AbortController();
    const context = exports => ({ exports, FormData, require: () => ({ default: api }) });
    vm.runInNewContext(compile('api/sessionNotesApi.ts'), context(notes));
    vm.runInNewContext(compile('api/mapsApi.ts'), context(maps));
    await notes.transcribeSessionAudio(1, new Blob(['audio']), controller.signal);
    await notes.createSessionNote(1, {}, controller.signal);
    await notes.indexSessionNote(1, 2, controller.signal);
    await maps.saveMap(1, null, { title: 'Map', description: '', locations: [] }, null, controller.signal);
    await maps.getMapImage(1, 2, controller.signal);
    calls.forEach(call => {
        const options = call.at(-1);
        assert.equal(options.timeout, 180000); assert.equal(options.signal, controller.signal);
    });
});

test('cancelling one shared-operation waiter rejects promptly and leaves other waiters running', async () => {
    let resolve; const operation = new Promise(done => { resolve = done; });
    const controller = new AbortController();
    const cancelled = cancellation.waitWithSignal(operation, controller.signal);
    const rejected = assert.rejects(cancelled, error => error.name === 'AbortError');
    const remaining = cancellation.waitWithSignal(operation);
    controller.abort(); await rejected;
    resolve('new-token'); assert.equal(await remaining, 'new-token');
});
