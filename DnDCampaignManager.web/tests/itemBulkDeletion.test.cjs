const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const compiled = ts.transpileModule(fs.readFileSync(path.join(__dirname, '../src/pages/Items.tsx'), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
}).outputText;
function nodes(tree) {
    if (!tree || typeof tree !== 'object') return [];
    return [tree, ...[tree.props?.children].flat(Infinity).flatMap(nodes)];
}
function harness(canManage = true, failDelete = false) {
    const states = [], effects = [], calls = [], exports = {};
    let cursor = 0, initialized = false;
    let items = [{ id: 8, name: 'Torch', category: 'Gear', description: '', weightLb: 1, costGp: 0.01, source: null }];
    vm.runInNewContext(compiled, { exports, require(name) {
        if (name === 'react') return {
            useState(initial) { const i = cursor++; if (!(i in states)) states[i] = initial;
                return [states[i], value => { states[i] = typeof value === 'function' ? value(states[i]) : value; }]; },
            useEffect(effect) { if (!initialized) effects.push(effect); }
        };
        if (name === 'react-router-dom') return { useParams: () => ({ campaignId: '12' }), Link: 'a' };
        if (name === '../api/itemApi') return {
            async getItems() { return { data: { canManage, items } }; },
            async previewDeleteAllItems(id) { calls.push(['preview', id]); return { data: { itemCount: 1, inventoryEntryCount: 3 } }; },
            async deleteAllItems(id, preview) {
                calls.push(['delete', id, JSON.parse(JSON.stringify(preview))]);
                if (failDelete) throw new Error('stale preview');
                items = []; return { data: preview };
            }
        };
        if (name === '../components/UI/Button') return { default: 'button' };
        if (name === '../components/UI/ErrorPanel') return { default: 'error-panel' };
        if (name === '../components/SrdAttribution') return { default: 'attribution' };
        if (name === '../Utils/apiError') return { extractApiError: (_, fallback) => fallback };
        return require(name);
    } });
    return { calls, states, render() { cursor = 0; const tree = exports.default(); initialized = true; return tree; },
        async load() { this.render(); effects.forEach(effect => effect()); await new Promise(resolve => setImmediate(resolve)); } };
}
const click = (h, label) => nodes(h.render()).find(n => n.props.children === label).props.onClick();
test('bulk deletion requires count review and confirmation; cancellation deletes nothing', async () => {
    const h = harness(); await h.load();
    await click(h, 'Delete all items');
    assert.deepEqual(h.calls, [['preview', 12]]);
    assert.ok(nodes(h.render()).some(n => n.props.role === 'alertdialog'));
    click(h, 'Cancel deletion');
    assert.deepEqual(h.calls, [['preview', 12]]);
    await click(h, 'Delete all items');
    click(h, 'Delete all items permanently');
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(h.calls.at(-1), ['delete', 12, { itemCount: 1, inventoryEntryCount: 3 }]);
    assert.equal(h.states[0].items.length, 0);
    assert.ok(nodes(h.render()).some(n => n.props.role === 'status' && n.props.children.includes('3 inventory entries')));
});
test('failed bulk deletion preserves the catalog and clears confirmation for another review', async () => {
    const h = harness(true, true); await h.load();
    await click(h, 'Delete all items'); click(h, 'Delete all items permanently');
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(h.states[0].items.length, 1);
    assert.ok(nodes(h.render()).some(n => n.props.message === 'Unable to update items.'));
    assert.equal(nodes(h.render()).some(n => n.props.role === 'alertdialog' || n.props.role === 'status'), false);
});
test('players have no bulk deletion controls', async () => {
    const h = harness(false); await h.load();
    assert.equal(nodes(h.render()).some(n => n.props.children === 'Delete all items'), false);
    assert.equal(h.calls.length, 0);
});
