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
    if (typeof tree.type === 'function') return nodes(tree.type(tree.props), visibleOnly);
    return [tree, ...[tree.props?.children].flat(Infinity).flatMap(child => nodes(child, visibleOnly))];
}
function harness(characterId = 7) {
    const states = [], exports = {};
    let cursor = 0; let submissions = 0;
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
    const modules = new Map();
    function loadModule(filename) {
        filename = path.resolve(filename);
        if (modules.has(filename)) return modules.get(filename);
        const result = {}; modules.set(filename, result);
        const source = ts.transpileModule(fs.readFileSync(filename, 'utf8'), {
            compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
        }).outputText;
        vm.runInNewContext(source, { exports: result, HTMLInputElement: Input, crypto: { randomUUID: () => 'new-attack' }, confirm: () => true,
            require: name => resolve(name, filename) });
        return result;
    }
    function resolve(name, filename = path.join(__dirname, '../src/components/Character/CharacterSheetForm.tsx')) {
        if (name === 'react') return {
            useState(initial) { const index = cursor++; if (!(index in states)) states[index] = initial;
                return [states[index], value => { states[index] = typeof value === 'function' ? value(states[index]) : value; }]; },
            useEffect() {}, useMemo(factory) { return factory(); },
            useRef(initial) { const index = cursor++; if (!(index in states)) states[index] = { current: initial }; return states[index]; }
        };
        if (name === '../../api/campaignApi') return {};
        if (name === '../../Utils/dnd') return { abilityModUtil: score => Math.floor((score - 10) / 2), formatModUtil: value => value >= 0 ? `+${value}` : String(value) };
        if (name === '../UI/FormCard') return { default: 'form-card' };
        if (name === '../UI/Button') return { default: 'button' };
        if (name === '../UI/ErrorPanel') return { default: 'error-panel' };
        if (name === './CharacterInventory') return { default: 'inventory' };
        if (name.startsWith('.')) {
            const base = path.resolve(path.dirname(filename), name);
            return loadModule(fs.existsSync(base + '.tsx') ? base + '.tsx' : base + '.ts');
        }
        return require(name);
    }
    vm.runInNewContext(compiled, { exports, HTMLInputElement: Input, require: resolve });
    return { getForm: () => form, get submissions() { return submissions; }, Input, render() { cursor = 0; return exports.default({ title: 'Character Sheet', form, characterId,
        setForm(value) { form = typeof value === 'function' ? value(form) : value; },
        onCancel() {}, onSubmit() { submissions++; }, submitLabel: 'Save Character' }); } };
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


test('section edits preserve nested skill/save state and shared read calculations', () => {
    const h = harness();
    Object.assign(h.getForm(), { level: 5, wisdom: 17,
        skills: [{ skill: 'Perception', ability: 'Wisdom', isProficient: true, isExpertise: true, miscBonus: 2 }] });
    function skill() { return nodes(h.render()).find(n => n.key === 'Perception'); }
    nodes(skill()).find(n => n.type === 'input' && n.props.type === 'checkbox').props.onChange({ target: { checked: false } });
    assert.equal(h.getForm().skills[0].isExpertise, false);
    nodes(skill()).find(n => n.type === 'input' && n.props.type === 'checkbox').props.onChange({ target: { checked: true } });
    nodes(skill()).filter(n => n.type === 'input' && n.props.type === 'checkbox')[1].props.onChange({ target: { checked: true } });
    const savingRow = nodes(h.render()).find(n => n.key === 'strength' && nodes(n).some(child => child.props.children === 'Saving Throw'));
    nodes(savingRow).find(n => n.type === 'input' && n.props.type === 'checkbox').props.onChange({ target: { checked: true } });
    nodes(savingRow).find(n => n.type === 'input' && n.props.type === 'number').props.onChange({ target: { value: '4' } });
    assert.equal(h.getForm().savingThrows.strength.miscBonus, 4);
    assert.equal(h.getForm().savingThrows.dexterity.miscBonus, 0);
    assert.ok(nodes(h.render()).some(n => n.props.children === '+7'));
    nodes(h.render()).find(n => n.props.children === 'Read View').props.onClick();
    const summary = nodes(h.render(),true);
    assert.ok(summary.some(n => n.props.children === '+11'));
    assert.ok(summary.some(n => Array.isArray(n.props.children) && n.props.children.includes('Passive Perception ') && n.props.children.includes(21)));
});

test('numeric fields, hit-dice limits and attack edits remain controlled through the parent form', () => {
    const h = harness();
    const target = new h.Input(); Object.assign(target,{ name:'hitPointCurrent', type:'number',value:'7' });
    nodes(h.render()).find(n=>n.props.name==='hitPointCurrent').props.onChange({target});
    assert.equal(h.getForm().hitPointCurrent,7);
    h.getForm().hitDice = { die:'d10',total:3,remaining:3 };
    const section = nodes(h.render()).find(n=>n.type==='section' && nodes(n).some(child=>child.type==='h2' && child.props.children==='Hit Dice'));
    const diceInputs = nodes(section).filter(n=>n.type==='input');
    diceInputs[1].props.onChange({target:{value:'1'}});
    assert.equal(h.getForm().hitDice.remaining,1);
    diceInputs[2].props.onChange({target:{value:'5'}});
    assert.equal(h.getForm().hitDice.remaining,1);
    nodes(h.render()).find(n=>n.type==='button' && n.props.children==='New attack').props.onClick();
    nodes(h.render()).find(n=>n.type==='input' && n.props.placeholder==='Longsword').props.onChange({target:{value:'Fire Bolt'}});
    nodes(h.render()).find(n=>n.type==='input' && n.props.placeholder==='1d8+3 slashing').props.onChange({target:{value:'1d10 fire'}});
    nodes(h.render()).find(n=>n.type==='button' && n.props.children==='Add attack').props.onClick();
    assert.equal(h.getForm().attacks[0].clientId,'new-attack');
    assert.equal(h.getForm().attacks[0].damage,'1d10 fire');
    nodes(h.render()).find(n=>n.type==='button' && n.props.children==='Remove').props.onClick();
    assert.equal(h.getForm().attacks.length,0);
    assert.equal(h.getForm().hitPointCurrent,7);
});


test('added attacks collapse to read-only cards and modify/cancel preserves persisted identity', () => {
    const h = harness();
    h.getForm().attacks = [{ id:17,clientId:'existing',name:'Longsword',attackBonus:5,damage:'1d8 slashing' }];
    const click = text => nodes(h.render()).find(n=>n.type==='button' && n.props.children===text).props.onClick();
    const changeName = name => nodes(h.render()).find(n=>n.type==='input' && n.props.placeholder==='Longsword').props.onChange({target:{value:name}});
    assert.equal(nodes(h.render()).some(n=>n.props.placeholder==='Longsword'),false);
    click('Modify'); changeName('Cancelled change');
    assert.equal(h.getForm().attacks[0].name,'Longsword');
    click('Cancel attack');
    assert.equal(h.getForm().attacks[0].name,'Longsword');
    assert.equal(nodes(h.render()).some(n=>n.props.placeholder==='Longsword'),false);
    click('Modify'); changeName('Flame blade'); click('Save attack changes');
    assert.equal(h.getForm().attacks[0].name,'Flame blade');
    assert.equal(h.getForm().attacks[0].id,17);
    assert.equal(h.getForm().attacks[0].clientId,'existing');
    assert.equal(h.getForm().attacks.length,1);
    assert.equal(nodes(h.render()).some(n=>n.props.placeholder==='Longsword'),false);
    click('New attack'); changeName('Fire Bolt');
    nodes(h.render()).find(n=>n.props.placeholder==='1d8+3 slashing').props.onChange({target:{value:'1d10 fire'}});
    assert.equal(h.getForm().attacks.length,1);
    click('Add attack');
    assert.equal(h.getForm().attacks.length,2);
    assert.equal(h.getForm().attacks[1].name,'Fire Bolt');
    assert.equal(nodes(h.render()).filter(n=>n.type==='button' && n.props.children==='Modify').length,2);
    assert.equal(nodes(h.render()).some(n=>n.props.placeholder==='Longsword'),false);
});

test('unfinished attack drafts survive view switches and block character saving until confirmed or cancelled', () => {
    const h = harness();
    const click = text => nodes(h.render()).find(n=>n.type==='button' && n.props.children===text).props.onClick();
    click('New attack');
    nodes(h.render()).find(n=>n.props.placeholder==='Longsword').props.onChange({target:{value:'Draft attack'}});
    click('Read View'); click('Edit View');
    assert.equal(nodes(h.render()).find(n=>n.props.placeholder==='Longsword').props.value,'Draft attack');
    assert.equal(nodes(h.render()).find(n=>n.type==='button' && n.props.type==='submit').props.disabled,true);
    let prevented = false;
    nodes(h.render()).find(n=>n.type==='form').props.onSubmit({preventDefault(){prevented=true;}});
    assert.equal(prevented,true); assert.equal(h.submissions,0); assert.equal(h.getForm().attacks.length,0);
    click('Add attack');
    assert.equal(nodes(h.render()).find(n=>n.type==='button' && n.props.type==='submit').props.disabled,false);
    nodes(h.render()).find(n=>n.type==='form').props.onSubmit({preventDefault(){}});
    assert.equal(h.submissions,1);
    click('New attack'); click('Cancel attack');
    assert.equal(h.getForm().attacks.length,1);
    assert.equal(nodes(h.render()).find(n=>n.type==='button' && n.props.type==='submit').props.disabled,false);
});

test('attack limits block invalid drafts and a full collection still permits modification', () => {
    const h = harness();
    const button = text => nodes(h.render()).find(n => n.type === 'button' && n.props.children === text);
    button('New attack').props.onClick();
    nodes(h.render()).find(n => n.props.placeholder === 'Longsword').props.onChange({ target: { value: 'Sword' } });
    const bonus = () => nodes(nodes(h.render()).find(n => n.props['aria-label'] === 'Attack editor')).find(n => n.type === 'input' && n.props.min === -1000 && n.props.max === 1000);
    for (const invalid of ['1001', '-1001', '1.5']) {
        bonus().props.onChange({ target: { value: invalid } });
        assert.equal(button('Add attack').props.disabled, true);
        button('Add attack').props.onClick();
        assert.equal(h.getForm().attacks.length, 0);
    }
    bonus().props.onChange({ target: { value: '-1000' } });
    button('Add attack').props.onClick();
    assert.equal(h.getForm().attacks[0].attackBonus, -1000);
    h.getForm().attacks = Array.from({ length: 50 }, (_, index) => ({ clientId: String(index), name: `Attack ${index}`, attackBonus: 0, damage: '' }));
    assert.equal(button('New attack').props.disabled, true);
    button('Modify').props.onClick();
    nodes(h.render()).find(n => n.props.placeholder === 'Longsword').props.onChange({ target: { value: 'Modified' } });
    button('Save attack changes').props.onClick();
    assert.equal(h.getForm().attacks.length, 50);
    assert.equal(h.getForm().attacks[0].name, 'Modified');
});
