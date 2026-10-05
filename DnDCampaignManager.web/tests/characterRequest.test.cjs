const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');

const source = fs.readFileSync(path.join(__dirname, '../src/api/characterRequest.ts'), 'utf8');
const compiled = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
}).outputText;
const exportsObject = {};
vm.runInNewContext(compiled, { exports: exportsObject });
const { toCharacterRequest } = exportsObject;

function form(level = 5) {
    return {
        campaignId: 9, id: 42, userId: 7, proficiencyBonus: 99,
        name: 'Freya', class: 'Fighter', race: 'Human', level,
        background: 'Soldier', alignment: 'Neutral Good', experiencePoints: 6500,
        strength: 16, dexterity: 12, constitution: 14, intelligence: 10, wisdom: 11, charisma: 8,
        armorClass: 18, initiative: 1, speed: 30,
        hitPointMax: 44, hitPointCurrent: 19, hitPointTemporary: 3, inspiration: true,
        hitDice: { die: 'd10', total: 5, remaining: 2 },
        savingThrows: { strength: { isProficient: true, miscBonus: 1 } },
        skills: [{ skill: 'Athletics', ability: 'Strength', isProficient: true, isExpertise: false, miscBonus: 2 }],
        attacks: [{ id: 10, clientId: 'ui-only', name: 'Longsword', attackBonus: 7, damage: '1d8+4' }]
    };
}

test('save payload preserves character state without UI or ownership metadata', () => {
    const original = form();
    const payload = toCharacterRequest(original);
    for (const key of ['campaignId', 'id', 'userId']) assert.equal(key in payload, false);
    assert.equal('clientId' in payload.attacks[0], false);
    assert.equal('id' in payload.attacks[0], false);
    assert.equal(payload.attacks[0].damage, '1d8+4');
    assert.equal(payload.hitPointCurrent, 19);
    assert.equal(payload.hitDice.remaining, 2);
    assert.equal(payload.savingThrows, original.savingThrows);
    assert.equal(payload.skills, original.skills);
    assert.equal(original.attacks[0].clientId, 'ui-only');
});

test('proficiency follows the edited level rather than a stale loaded value', () => {
    for (const [level, expected] of [[1, 2], [4, 2], [5, 3], [9, 4], [13, 5], [17, 6], [20, 6]]) {
        assert.equal(toCharacterRequest(form(level)).proficiencyBonus, expected);
    }
});
