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
function page(failPromotion = false) {
    let cursor = 0;
    const states = [];
    const calls = [];
    const exportsObject = {};
    vm.runInNewContext(compile('pages/DungeonMasters.tsx'), {
        AbortController, DOMException, setTimeout, clearTimeout, exports: exportsObject,
        require(name) {
            if (name === 'react') return {
                useState(initial) {
                    const i = cursor++;
                    if (!(i in states)) states[i] = i === 0 ? [{ id: 1, email: 'admin@example.test', isAdmin: true, campaignCount: 0 },
                        { id: 2, email: 'dm@example.test', isAdmin: false, campaignCount: 2 }] : i === 2 ? false : initial;
                    return [states[i], value => { states[i] = typeof value === 'function' ? value(states[i]) : value; }];
                }, useEffect() {}
            };
            if (name === '../Utils/apiError') return { extractApiError: (_, fallback) => fallback };
            if (name === '../api/dungeonMastersApi') return {
                async promoteDungeonMaster(email) {
                    calls.push({ operation: 'promote', email });
                    if (failPromotion) throw new Error('Server diagnostic stack');
                    return { data: { id: 3, email, isAdmin: false, campaignCount: 0 } };
                },
                async previewDmRemoval(id) { return { data: { userId: id, email: 'dm@example.test', campaignCount: 2, characterCount: 3, preservedCharacterCount: 1 } }; },
                async removeDungeonMaster(id, count) { calls.push({ operation: 'remove', id, count }); }
            };
            return require(name);
        }
    });
    return { calls, states, render() { cursor = 0; return exportsObject.default(); } };
}
test('DM removal requires an explicit preview and confirmation, and admin has no removal action', async () => {
    const p = page();
    assert.equal(nodes(p.render()).filter(n => n.type === 'button' && n.props.children === 'Review removal').length, 1);
    nodes(p.render()).find(n => n.props.children === 'Review removal').props.onClick();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(p.calls.length, 0);
    nodes(p.render()).find(n => n.props.children === 'Cancel').props.onClick();
    assert.equal(p.states[6], null);
    assert.equal(p.calls.length, 0);
    nodes(p.render()).find(n => n.props.children === 'Review removal').props.onClick();
    await new Promise(resolve => setImmediate(resolve));
    nodes(p.render()).find(n => n.props.children === 'Confirm removal and delete owned campaign data').props.onClick();
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(p.calls, [{ operation: 'remove', id: 2, count: 2 }]);
    assert.equal(p.states[0].length, 1);
    assert.equal(p.states[0][0].isAdmin, true);
});
test('granting DM rights adds the registered account to the roster', async () => {
    const p = page();
    nodes(p.render()).find(n => n.type === 'input').props.onChange({ target: { value: 'new@example.test' } });
    await nodes(p.render()).find(n => n.type === 'form').props.onSubmit({ preventDefault() {} });
    assert.equal(p.states[0].length, 3);
    assert.equal(p.states[0].find(dm => dm.id === 3).isAdmin, false);
    assert.equal(p.states[1], '');
});
test('failed DM promotion leaves the roster intact and shows a safe error', async () => {
    const p = page(true);
    nodes(p.render()).find(n => n.type === 'input').props.onChange({ target: { value: 'missing@example.test' } });
    await nodes(p.render()).find(n => n.type === 'form').props.onSubmit({ preventDefault() {} });
    assert.equal(p.states[0].length, 2);
    assert.equal(p.states[3], false);
    assert.equal(p.states[4], 'Unable to grant DM rights.');
});
test('the admin route requires both DM role and the admin flag', () => {
    for (const user of [{ role: 'Player', isAdmin: false }, { role: 'DM', isAdmin: false }, { role: 'Player', isAdmin: true }, { role: 'DM', isAdmin: true }]) {
        const exportsObject = {};
        vm.runInNewContext(compile('auth/RoleRoute.tsx'), {
            AbortController, DOMException, setTimeout, clearTimeout, exports: exportsObject,
            require(name) {
                if (name === './AuthContext') return { useAuth: () => ({ user, loading: false }) };
                if (name === 'react-router-dom') return { Navigate: 'redirect' };
                return require(name);
            }
        });
        const result = exportsObject.default({ role: 'Admin', children: 'admin page' });
        assert.equal(result === 'admin page', user.role === 'DM' && user.isAdmin);
    }
});
