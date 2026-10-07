const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const compiled = ts.transpileModule(fs.readFileSync(path.join(__dirname, '../src/components/Maps/MapImage.tsx'), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
}).outputText;

function preview(props, delayed = false) {
    let effect, callback, disconnected = false, cleanup;
    const states = [], requests = [], revoked = [], exports = {};
    vm.runInNewContext(compiled, { exports, AbortController,
        URL: { createObjectURL: () => 'blob:preview', revokeObjectURL: url => revoked.push(url) },
        IntersectionObserver: class {
            constructor(fn) { callback = fn; }
            observe() {}
            disconnect() { disconnected = true; }
        },
        require(name) {
            if (name === 'react') return {
                useRef: () => ({ current: {} }), useEffect: fn => { effect = fn; },
                useState: initial => { const i = states.length; states.push(initial); return [initial, value => states[i] = value]; }
            };
            if (name === '../../api/mapsApi') return Object.fromEntries(['getMapImage', 'getMapThumbnail'].map(method => [method, async (_, id, signal) => {
                requests.push({ method, id, signal });
                if (delayed) await new Promise(resolve => { cleanup = resolve; });
                return { data: {} };
            }]));
            return require(name);
        }
    });
    exports.default({ campaignId: 1, map: { id: 7, title: 'Map' }, locations: [], ...props });
    const dispose = effect();
    return { requests, states, revoked, dispose, visible: value => callback([{ isIntersecting: value }]),
        finish: () => cleanup?.(), disconnected: () => disconnected };
}
const flush = () => new Promise(resolve => setImmediate(resolve));

test('offscreen previews make no request; visibility fetches only one thumbnail', async () => {
    const p = preview({ thumbnail: true });
    await flush();
    assert.equal(p.requests.length, 0);
    p.visible(false);
    await flush();
    assert.equal(p.requests.length, 0);
    p.visible(true);
    p.visible(true);
    await flush();
    assert.equal(p.requests.length, 1);
    assert.equal(p.requests[0].method, 'getMapThumbnail');
    assert.equal(p.disconnected(), true);
    p.dispose();
    assert.equal(p.requests[0].signal.aborted, true);
    assert.deepEqual(p.revoked, ['blob:preview']);
});

test('full map view fetches the original immediately and local editor files make no request', async () => {
    const full = preview({ thumbnail: false });
    const local = preview({ file: {}, thumbnail: false });
    await flush();
    assert.equal(full.requests[0].method, 'getMapImage');
    assert.equal(local.requests.length, 0);
    full.dispose(); local.dispose();
});

test('unmount aborts a pending preview and ignores a late response', async () => {
    const p = preview({ thumbnail: true }, true);
    p.visible(true);
    await flush();
    p.dispose();
    assert.equal(p.requests[0].signal.aborted, true);
    p.finish();
    await flush();
    assert.equal(p.states[0], '');
    assert.equal(p.states[1], '');
});
