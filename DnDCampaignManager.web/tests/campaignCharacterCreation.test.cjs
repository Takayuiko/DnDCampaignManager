const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const React = require('react');
const { renderToStaticMarkup } = require('react-dom/server');

const source = fs.readFileSync(path.join(__dirname, '../src/components/CampaignItem.tsx'), 'utf8');
const compiled = ts.transpileModule(source, {
    compilerOptions: {
        module: ts.ModuleKind.CommonJS,
        target: ts.ScriptTarget.ES2022,
        jsx: ts.JsxEmit.ReactJSX
    }
}).outputText;

function renderCampaign({ isDM = false, characters = [], user = { id: 1, role: 'Player' } } = {}) {
    const exportsObject = {};
    vm.runInNewContext(compiled, {
        exports: exportsObject,
        require(name) {
            if (name === '../auth/AuthContext') return { useAuth: () => ({ user }) };
            if (name === 'react-router-dom') return { useNavigate: () => () => {} };
            if (name === './UI/Button') return {
                default: ({ children, onClick }) => React.createElement('button', { onClick }, children)
            };
            return require(name);
        }
    });
    return renderToStaticMarkup(React.createElement(exportsObject.default, {
        campaign: { id: 10, name: 'Test Campaign', description: 'A campaign', characters },
        isDM,
        onEdit() {}, onDelete() {}, onAddCharacter() {}
    }));
}

test('campaign DM can modify the campaign without a character creation action', () => {
    const html = renderCampaign({
        isDM: true, user: { id: 1, role: 'DM' },
        characters: [{ id: 20, userId: 2, name: 'Another player' }]
    });
    assert.doesNotMatch(html, /Add Character/);
    assert.match(html, /Edit Campaign/);
});

test('players can add their first character', () => {
    assert.match(renderCampaign(), /Add Character/);
});

test('an existing character hides creation for both DM and player', () => {
    for (const isDM of [false, true]) {
        assert.doesNotMatch(renderCampaign({
            isDM, characters: [{ id: 20, userId: 1, name: 'My character' }]
        }), /Add Character/);
    }
});

test('a character in one campaign does not hide creation in another campaign', () => {
    assert.doesNotMatch(renderCampaign({ characters: [{ id: 20, userId: 1, name: 'Freya' }] }), /Add Character/);
    assert.match(renderCampaign({ characters: [{ id: 21, userId: 2, name: 'Another player' }] }), /Add Character/);
});

test('unauthenticated users do not get a creation action', () => {
    assert.doesNotMatch(renderCampaign({ user: null }), /Add Character/);
});
test('a full campaign displays all six characters and hides character creation', () => {
    const characters = Array.from({ length: 6 }, (_, i) => ({ id: i + 20, userId: i + 2, name: `Hero ${i + 1}` }));
    const html = renderCampaign({ characters });
    for (const character of characters) assert.ok(html.includes(character.name));
    assert.doesNotMatch(html, /Add Character|Available character space/);
    assert.match(html, /6 \/ 6.*Full/);
});
test('campaign cards reserve six spaces and retain existing characters above the new limit', () => {
    const html = renderCampaign({ characters: [{ id: 20, userId: 2, name: 'Freya' }] });
    assert.equal((html.match(/Available character space/g) ?? []).length, 5);
    const legacy = renderCampaign({ characters: Array.from({ length: 7 }, (_, i) => ({ id: i + 20, userId: i + 2, name: `Hero ${i + 1}` })) });
    assert.match(legacy, /Hero 7/);
    assert.doesNotMatch(legacy, /Add Character/);
});
