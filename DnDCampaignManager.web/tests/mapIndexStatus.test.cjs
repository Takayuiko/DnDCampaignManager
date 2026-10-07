const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const compiled = ts.transpileModule(fs.readFileSync(path.join(__dirname, '../src/pages/Maps.tsx'), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
}).outputText;

test('pending map polling preserves unchanged preview objects, updates status and cleans up', async () => {
    const original = { id: 7, campaignId: 1, title: 'Town map', description: '', locations: [], indexStatus: 'pending' };
    const states = [], exports = {};
    let cursor = 0, effect, tick, cleared = 0;
    let response = { ...original }, calls = 0;
    vm.runInNewContext(compiled, { exports,
        setInterval: fn => { tick = fn; return 1; }, clearInterval: () => { cleared++; },
        require(name) {
            if (name === 'react') return {
                useState(initial) { const i = cursor++; if (!(i in states)) states[i] = i === 0 ? { id: 1, ownerId: 2 }
                    : i === 1 ? [original] : i === 2 ? false : typeof initial === 'function' ? initial() : initial;
                    return [states[i], value => states[i] = typeof value === 'function' ? value(states[i]) : value]; },
                useEffect(fn, deps) { if (deps.length === 3) effect = fn; }
            };
            if (name === 'react-router-dom') return { Link: 'a', useParams: () => ({ campaignId: '1' }) };
            if (name === '../auth/AuthContext') return { useAuth: () => ({ user: { id: 2, role: 'DM' } }) };
            if (name === '../api/mapsApi') return { getMaps: async () => { calls++; return { data: [response] }; } };
            if (name === '../api/campaignApi') return {};
            if (name === '../components/UI/Button') return { default: 'button' };
            if (name === '../components/Maps/MapImage') return { default: function MapImage() {} };
            if (name === '../Utils/apiError') return {};
            return require(name);
        }
    });
    const render = () => { cursor = 0; return exports.default(); };
    render();
    const stop = effect();
    const priorList = states[1];
    tick();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(states[1], priorList);
    assert.equal(states[1][0], original);
    response = { ...original, indexStatus: 'ready' };
    tick();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(states[1][0].indexStatus, 'ready');
    stop();
    render();
    assert.equal(effect(), undefined);
    assert.equal(cleared, 1);
    assert.equal(calls, 2);
});
