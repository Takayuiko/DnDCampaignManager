const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const compiled = ts.transpileModule(fs.readFileSync(path.join(__dirname,
    '../src/components/Character/CharacterInventory.tsx'), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
}).outputText;
const item = { id: 8, name: 'Torch', category: 'Gear', description: '', weightLb: null, costGp: null, source: null };
function nodes(tree) {
    if (!tree || typeof tree !== 'object') return [];
    return [tree, ...[tree.props?.children].flat(Infinity).flatMap(nodes)];
}
function harness(canAssign, failure = false) {
    const states = [], effects = [], calls = [], exports = {};
    let cursor = 0, initialized = false;
    vm.runInNewContext(compiled, { exports, require(name) {
        if (name === 'react') return {
            useState(initial) {
                const index = cursor++;
                if (!(index in states)) states[index] = initial;
                return [states[index], value => { states[index] = typeof value === 'function' ? value(states[index]) : value; }];
            },
            useEffect(effect) { if (!initialized) effects.push(effect); }
        };
        if (name === '../../api/itemApi') return {
            async getInventory(campaignId, characterId) { calls.push(['inventory', campaignId, characterId]); return { data: { canAssign, items: [] } }; },
            async getItems() { calls.push(['catalog']); return { data: { items: [item] } }; },
            async assignItem(campaignId, characterId, request) {
                calls.push(['assign', campaignId, characterId, JSON.parse(JSON.stringify(request))]);
                if (failure) throw new Error('failure');
                return { data: { id: 3, item, quantity: request.quantity, notes: request.notes, assignedAtUtc: '2026-10-05T00:00:00Z' } };
            }
        };
        if (name === '../../Utils/apiError') return { extractApiError: (_, fallback) => fallback };
        if (name === '../UI/Button') return { default: 'button' };
        if (name === '../UI/ErrorPanel') return { default: 'error-panel' };
        if (name === '../SrdAttribution') return { default: 'attribution' };
        return require(name);
    } });
    return {
        calls, states,
        render(readOnly = false) { cursor = 0; const tree = exports.default({ campaignId: 12, characterId: 7, readOnly }); initialized = true; return tree; },
        async load() { this.render(); effects.forEach(effect => effect()); await new Promise(resolve => setImmediate(resolve)); }
    };
}
test('players see an empty inventory without assignment controls or a catalog request', async () => {
    const h = harness(false); await h.load();
    const tree = nodes(h.render());
    assert.ok(tree.some(n => n.props.children === 'No items assigned yet.'));
    assert.equal(tree.some(n => n.type === 'form'), false);
    assert.deepEqual(h.calls, [['inventory', 12, 7]]);
});
test('DM assigns quantity and notes and sees the saved item', async () => {
    const h = harness(true); await h.load();
    nodes(h.render()).find(n => n.type === 'select').props.onChange({ target: { value: '8' } });
    nodes(h.render()).find(n => n.type === 'input').props.onChange({ target: { value: '4' } });
    nodes(h.render()).find(n => n.type === 'textarea').props.onChange({ target: { value: 'For the cave.' } });
    await nodes(h.render()).find(n => n.type === 'form').props.onSubmit({ preventDefault() {} });
    assert.deepEqual(h.calls.at(-1), ['assign', 12, 7, { itemId: 8, quantity: 4, notes: 'For the cave.' }]);
    assert.equal(h.states[0].items.length, 1);
    assert.equal(h.states[0].items[0].quantity, 4);
    assert.ok(nodes(h.render()).some(n => n.props.role === 'status'));
});
test('failed assignment retains input and inventory and exposes an error without success', async () => {
    const h = harness(true, true); await h.load();
    nodes(h.render()).find(n => n.type === 'select').props.onChange({ target: { value: '8' } });
    await nodes(h.render()).find(n => n.type === 'form').props.onSubmit({ preventDefault() {} });
    assert.equal(h.states[0].items.length, 0);
    assert.equal(h.states[2], '8');
    assert.ok(nodes(h.render()).some(n => n.props.message === 'Unable to assign item.'));
    assert.equal(nodes(h.render()).some(n => n.props.role === 'status'), false);
});
test('Read View hides DM assignment and management controls while showing inventory details', async () => {
    const h = harness(true); await h.load();
    h.states[0].items = [{ id: 3, item: { ...item, costGp: 0.01, weightLb: 1 }, quantity: 4, notes: 'For the cave.' }];
    const tree = nodes(h.render(true));
    assert.equal(tree.some(n => n.type === 'form' || n.type === 'select' || n.type === 'input' || n.type === 'textarea'), false);
    assert.equal(tree.some(n => n.props.children === 'Manage campaign items'), false);
    assert.ok(tree.some(n => n.type === 'h3' && n.props.children.includes('Torch')));
    assert.ok(tree.some(n => n.props.children?.includes?.('For the cave.')));
});
