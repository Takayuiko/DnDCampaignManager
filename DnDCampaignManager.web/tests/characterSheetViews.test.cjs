const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const compiled = ts.transpileModule(fs.readFileSync(path.join(__dirname,
    '../src/components/Character/CharacterSheetForm.tsx'), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
}).outputText;
function nodes(tree, visibleOnly = false) {
    if (!tree || typeof tree !== 'object' || (visibleOnly && tree.props?.hidden)) return [];
    return [tree, ...[tree.props?.children].flat(Infinity).flatMap(child => nodes(child, visibleOnly))];
}
function harness(characterId = 7) {
    const states = [], exports = {};
    let cursor = 0;
    let form = {
        campaignId: 12, name: 'Freya', class: 'Fighter', race: 'Human', level: 1,
        background: 'Soldier', alignment: 'Neutral', experiencePoints: 0,
        strength: 10, dexterity: 10, constitution: 10, intelligence: 10, wisdom: 10, charisma: 10,
        armorClass: 10, initiative: 0, speed: 30, hitPointMax: 10, hitPointCurrent: 10,
        hitPointTemporary: 0, inspiration: false, skills: [], attacks: [],
        hitDice: { die: 'd10', total: 1, remaining: 1 }, savingThrows: Object.fromEntries(
            ['strength', 'dexterity', 'constitution', 'intelligence', 'wisdom', 'charisma']
                .map(key => [key, { isProficient: false, miscBonus: 0 }]))
    };
    class Input {}
    vm.runInNewContext(compiled, { exports, HTMLInputElement: Input, require(name) {
        if (name === 'react') return {
            useState(initial) { const index = cursor++; if (!(index in states)) states[index] = initial;
                return [states[index], value => { states[index] = typeof value === 'function' ? value(states[index]) : value; }]; },
            useEffect() {}, useMemo(factory) { return factory(); }
        };
        if (name === '../../api/campaignApi') return {};
        if (name === '../../Utils/dnd') return { abilityModUtil: score => Math.floor((score - 10) / 2), formatModUtil: value => value >= 0 ? `+${value}` : String(value) };
        if (name === '../UI/FormCard') return { default: 'form-card' };
        if (name === '../UI/Button') return { default: 'button' };
        if (name === '../UI/ErrorPanel') return { default: 'error-panel' };
        if (name === './CharacterInventory') return { default: 'inventory' };
        return require(name);
    } });
    return { render() { cursor = 0; return exports.default({ title: 'Character Sheet', form, characterId,
        setForm(value) { form = typeof value === 'function' ? value(form) : value; },
        onCancel() {}, onSubmit() {}, submitLabel: 'Save Character' }); } };
}
test('Character and Inventory tabs switch visible panels and retain edited character fields', () => {
    const h = harness();
    nodes(h.render(), true).find(n => n.type === 'input' && n.props.name === 'name')
        .props.onChange({ target: { name: 'name', value: 'Freya Updated' } });
    nodes(h.render()).find(n => n.props.role === 'tab' && n.props.children === 'Inventory').props.onClick();
    const inventoryTree = nodes(h.render(), true);
    assert.ok(inventoryTree.some(n => n.type === 'inventory' && n.props.readOnly === false));
    assert.equal(inventoryTree.some(n => n.type === 'form'), false);
    nodes(h.render()).find(n => n.props.role === 'tab' && n.props.children === 'Character').props.onClick();
    assert.equal(nodes(h.render(), true).find(n => n.props.name === 'name').props.value, 'Freya Updated');
});
test('Read View includes the character summary and read-only inventory with a blue mode toggle', () => {
    const h = harness();
    const toggle = nodes(h.render()).find(n => n.props.children === 'Read View');
    assert.ok(toggle.props.className.includes('bg-sky-700'));
    toggle.props.onClick();
    const tree = nodes(h.render(), true);
    assert.ok(tree.some(n => n.type === 'h2' && n.props.children === 'Freya'));
    assert.ok(tree.some(n => n.type === 'inventory' && n.props.readOnly === true));
    assert.equal(tree.some(n => n.type === 'form' || n.props.role === 'tab'), false);
    tree.find(n => n.props.children === 'Edit View').props.onClick();
    assert.ok(nodes(h.render(), true).some(n => n.props.role === 'tab' && n.props.children === 'Character'));
});
test('new unsaved character sheets cannot open an inventory tab', () => {
    const h = harness(null);
    assert.equal(nodes(h.render()).some(n => n.type === 'inventory' || n.props.role === 'tab'), false);
});
