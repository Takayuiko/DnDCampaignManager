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
function nodes(tree) {
    if (!tree || typeof tree !== 'object') return [];
    return [tree, ...[tree.props?.children].flat(Infinity).flatMap(nodes)];
}
test('NPC writes carry map location, party status, portrait removal and edit version through authenticated API', async () => {
    const calls = []; const exports = {};
    vm.runInNewContext(compile('api/npcsApi.ts'), { exports, FormData, require: () => ({ default: {
        post: async (...args) => calls.push(['post', ...args]), put: async (...args) => calls.push(['put', ...args]),
        get: async (...args) => calls.push(['get', ...args])
    } }) });
    const draft = { name: 'Guide', description: 'Knows the town', mapId: 4, locationName: 'Inn', isPartyMember: true };
    await exports.saveNpc(12, null, draft, null, false);
    await exports.saveNpc(12, { id: 7, version: 'revision' }, draft, null, true);
    await exports.getNpcImage(12, 7);
    assert.equal(calls[0][1], '/campaigns/12/npcs');
    assert.equal(calls[0][2].get('mapId'), '4');
    assert.equal(calls[0][2].get('locationName'), 'Inn');
    assert.equal(calls[0][2].get('isPartyMember'), 'true');
    assert.equal(calls[1][1], '/campaigns/12/npcs/7');
    assert.equal(calls[1][2].get('version'), 'revision');
    assert.equal(calls[1][2].get('removeImage'), 'true');
    assert.equal(calls[2][2].responseType, 'blob');
});
function page(canManage) {
    const states = []; let cursor = 0; const exports = {};
    const npcs = [{ id: 1, name: 'Guide', description: 'Friendly', mapId: 4, locationName: 'Inn', isPartyMember: true },
        { id: 2, name: 'Merchant', description: 'Sells goods', mapId: null, locationName: 'Old town', isPartyMember: false }];
    vm.runInNewContext(compile('pages/Npcs.tsx'), { exports, require(name) {
        if (name === 'react') return { useEffect() {}, useRef: () => ({ current: null }), useState(initial) {
            const i = cursor++; if (!(i in states)) states[i] = i === 0 ? { canManage, npcs } : i === 1 ? [{ id: 4, title: 'Town', locations: [{ name: 'Inn' }] }] : i === 2 ? false : typeof initial === 'function' ? initial() : initial;
            return [states[i], value => { states[i] = typeof value === 'function' ? value(states[i]) : value; }];
        } };
        if (name === 'react-router-dom') return { Link: 'a', useParams: () => ({ campaignId: '12' }) };
        if (name === '../components/UI/Button') return { default: 'button' };
        if (name.startsWith('../api/')) return {};
        if (name === '../Utils/apiError') return {};
        return require(name);
    } });
    return { render() { cursor = 0; return exports.default(); } };
}
test('campaign readers see all NPCs and can filter party members without authoring controls', () => {
    const p = page(false); let tree = nodes(p.render());
    assert.equal(tree.filter(n => n.type === 'li').length, 2);
    assert.equal(tree.filter(n => n.type === 'button').length, 0);
    tree.find(n => n.type === 'input' && n.props.type === 'checkbox').props.onChange({ target: { checked: true } });
    assert.equal(nodes(p.render()).filter(n => n.type === 'li').length, 1);
});
test('DM can open an editor with required map location and optional portrait', () => {
    const p = page(true);
    nodes(p.render()).find(n => n.type === 'button' && n.props.children === 'Add NPC').props.onClick();
    const tree = nodes(p.render());
    assert.equal(tree.filter(n => n.type === 'select' && n.props.required).length, 2);
    assert.equal(tree.filter(n => n.type === 'textarea' && n.props.required).length, 1);
    assert.equal(tree.filter(n => n.type === 'input' && n.props.type === 'file').length, 1);
});
