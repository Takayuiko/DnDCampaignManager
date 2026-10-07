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
test('map API sends typed locations in multipart requests and protects image requests through Axios', async () => {
    const calls = [];
    const exports = {};
    vm.runInNewContext(compile('api/mapsApi.ts'), { exports, FormData, require: () => ({ default: {
        post: async (...args) => calls.push(['post', ...args]), put: async (...args) => calls.push(['put', ...args]),
        get: async (...args) => calls.push(['get', ...args])
    } }) });
    const draft = { title: 'Tower', description: 'Ruins', locations: [{ name: 'Gate', description: 'Locked', x: 20, y: 40 }] };
    await exports.saveMap(12, null, draft, null);
    await exports.saveMap(12, 7, draft, null);
    await exports.getMapImage(12, 7);
    assert.equal(calls[0][1], '/campaigns/12/maps');
    assert.equal(calls[1][0], 'put');
    assert.equal(calls[1][1], '/campaigns/12/maps/7');
    assert.deepEqual(JSON.parse(calls[0][2].get('locationsJson')), draft.locations);
    assert.equal(calls[2][1], '/campaigns/12/maps/7/image');
    assert.equal(calls[2][2].responseType, 'blob');
});
function page(owner) {
    const states = []; let cursor = 0;
    const exports = {};
    const saved = [];
    vm.runInNewContext(compile('pages/Maps.tsx'), { exports, confirm: () => true, window: { scrollTo() {} },
        require(name) {
            if (name === 'react') return { useEffect() {}, useRef: () => ({ current: null }), useState(initial) {
                const i = cursor++;
                if (!(i in states)) states[i] = i === 0 ? { id: 12, ownerId: 1, name: 'Campaign' } : i === 12 && owner ? 'manage' : i === 2 ? false
                    : typeof initial === 'function' ? initial() : initial;
                return [states[i], value => { states[i] = typeof value === 'function' ? value(states[i]) : value; }];
            } };
            if (name === '../components/UI/Button') return { default: 'button' };
            if (name === 'react-router-dom') return { Link: 'a', useParams: () => ({ campaignId: '12' }) };
            if (name === '../auth/AuthContext') return { useAuth: () => ({ user: { id: owner ? 1 : 2, role: owner ? 'DM' : 'Player' } }) };
            if (name === '../api/campaignApi') return {};
            if (name === '../Utils/apiError') return { extractApiError: (_, fallback) => fallback };
            if (name === '../api/mapsApi') return {
                saveMap: async (campaignId, id, draft) => { saved.push(draft); return { data: { ...draft, id: 7, campaignId, indexStatus: 'failed' } }; },
                indexMap: async () => ({ data: { ...states[1][0], indexStatus: 'ready' } })
            };
            return require(name);
        }
    });
    return { states, saved, render() { cursor = 0; return exports.default(); } };
}
test('DM can add multiple pins and retry a saved map after indexing fails', async () => {
    const p = page(true);
    nodes(p.render()).find(n => n.type === 'button' && n.props.children === 'Add map').props.onClick();
    let tree = nodes(p.render());
    tree.find(n => n.type === 'input' && n.props.maxLength === 160).props.onChange({ target: { value: 'Tower' } });
    tree = nodes(p.render());
    tree.find(n => n.type === 'input' && n.props.maxLength === 120).props.onChange({ target: { value: 'Gate' } });
    tree = nodes(p.render());
    tree.find(n => n.type === 'input' && n.props.type === 'file').props.onChange({ target: { files: [{ size: 10, type: 'image/png' }] } });
    tree.find(n => n.type === 'button' && n.props.children === 'Add location').props.onClick();
    nodes(p.render()).find(n => n.type === 'button' && n.props.children === 'New location').props.onClick();
    tree = nodes(p.render());
    tree.find(n => n.type === 'input' && n.props.maxLength === 120).props.onChange({ target: { value: 'Tower' } });
    tree = nodes(p.render());
    tree.find(n => typeof n.type === 'function' && n.props.onPin).props.onPin(25, 75);
    nodes(p.render()).find(n => n.type === 'button' && n.props.children === 'Add location').props.onClick();
    await nodes(p.render()).find(n => n.type === 'form').props.onSubmit({ preventDefault() {} });
    assert.equal(p.saved[0].locations.length, 2);
    assert.equal(p.saved[0].locations[1].x, 25);
    assert.equal(p.states[1][0].indexStatus, 'failed');
    nodes(p.render()).find(n => n.type === 'button' && n.props.children === 'Retry indexing').props.onClick();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(p.states[1][0].indexStatus, 'ready');
});
test('players can view map locations without management controls', () => {
    const p = page(false);
    p.render();
    p.states[1] = [{ id: 7, title: 'Tower', description: '', locations: [{ name: 'Gate', description: 'Locked', x: 20, y: 40 }], indexStatus: 'ready' }];
    const tree = nodes(p.render());
    assert.equal(tree.some(n => n.type === 'form'), false);
    assert.equal(tree.some(n => n.type === 'button' && n.props.role !== 'tab'), false);
    assert.equal(tree.some(n => n.type === 'strong' && n.props.children === 'Gate'), true);
});

test('added locations are read-only, edit explicitly, and cancel restores the pin and name', () => {
    const p = page(true);
    nodes(p.render()).find(n => n.type === 'button' && n.props.children === 'Add map').props.onClick();
    let tree = nodes(p.render());
    tree.find(n => n.type === 'input' && n.props.maxLength === 120).props.onChange({ target: { value: 'Gate' } });
    nodes(p.render()).find(n => n.type === 'button' && n.props.children === 'Add location').props.onClick();
    tree = nodes(p.render());
    assert.equal(tree.some(n => n.type === 'input' && n.props.maxLength === 120), false);
    assert.equal(tree.some(n => typeof n.type === 'function' && n.props.onPin), false);
    tree.find(n => n.type === 'button' && n.props.children === 'Edit location').props.onClick();
    tree = nodes(p.render());
    tree.find(n => n.type === 'input' && n.props.maxLength === 120).props.onChange({ target: { value: 'Changed gate' } });
    nodes(p.render()).find(n => typeof n.type === 'function' && n.props.onPin).props.onPin(10, 20);
    nodes(p.render()).find(n => n.type === 'button' && n.props.children === 'Cancel location').props.onClick();
    tree = nodes(p.render());
    assert.equal(tree.some(n => n.type === 'input' && n.props.maxLength === 120), false);
    assert.equal(p.states[7].locations[0].name, 'Gate');
    assert.equal(p.states[7].locations[0].x, 50);
    tree.find(n => n.type === 'button' && n.props.children === 'Edit location').props.onClick();
    nodes(p.render()).find(n => n.type === 'input' && n.props.maxLength === 120).props.onChange({ target: { value: 'Updated gate' } });
    nodes(p.render()).find(n => n.type === 'button' && n.props.children === 'Save location changes').props.onClick();
    assert.equal(p.states[7].locations[0].name, 'Updated gate');
    assert.equal(nodes(p.render()).some(n => n.type === 'input' && n.props.maxLength === 120), false);
});

test('view and manage tabs separate full map display from the editor', () => {
    const p = page(true);
    p.render();
    p.states[1] = [{ id: 7, title: 'Tower', description: '', locations: [{ name: 'Gate', description: 'Locked', x: 20, y: 40 }], indexStatus: 'ready' }];
    p.states[12] = 'view';
    let tree = nodes(p.render());
    assert.equal(tree.some(n => n.type === 'form'), false);
    assert.equal(tree.filter(n => typeof n.type === 'function' && n.props.locations && !n.props.thumbnail).length, 1);
    tree.find(n => n.type === 'button' && n.props.children === 'Manage maps').props.onClick();
    tree = nodes(p.render());
    tree.find(n => n.type === 'button' && n.props.children === 'Edit').props.onClick();
    tree = nodes(p.render());
    assert.equal(tree.some(n => n.type === 'form'), true);
    assert.equal(tree.filter(n => typeof n.type === 'function' && n.props.locations && !n.props.thumbnail).length, 1);
    tree.find(n => n.type === 'button' && n.props.children === 'View maps').props.onClick();
    tree = nodes(p.render());
    assert.equal(tree.some(n => n.type === 'form'), false);
    assert.equal(tree.some(n => n.type === 'button' && n.props.children === 'Edit'), false);
    assert.equal(tree.filter(n => typeof n.type === 'function' && n.props.locations && !n.props.thumbnail).length, 1);
});

test('only the next available slot adds a map and add/edit replace the list until cancel', () => {
    const p = page(true);
    p.render();
    for (const count of [0, 3, 6, 8]) {
        p.states[1] = Array.from({ length: count }, (_, i) => ({ id: i + 1, title: `Map ${i + 1}`, description: '', locations: [{ name: 'Gate', description: '', x: 20, y: 40 }], indexStatus: 'ready' }));
        let tree = nodes(p.render());
        assert.equal(tree.filter(n => n.type === 'button' && n.props.children === 'Add map').length, 1);
        assert.equal(tree.some(n => n.type === 'form'), false);
        tree.find(n => n.type === 'button' && n.props.children === 'Add map').props.onClick();
        tree = nodes(p.render());
        assert.equal(tree.some(n => n.type === 'form'), true);
        assert.equal(tree.some(n => n.props['aria-label'] === 'Manage map list'), false);
        tree.find(n => n.type === 'button' && n.props.children === 'Cancel add').props.onClick();
        tree = nodes(p.render());
        assert.equal(tree.some(n => n.type === 'form'), false);
        assert.equal(tree.some(n => n.props['aria-label'] === 'Manage map list'), true);
        if (count) {
            tree.find(n => n.type === 'button' && n.props.children === 'Edit').props.onClick();
            tree = nodes(p.render());
            assert.equal(tree.some(n => n.props['aria-label'] === 'Manage map list'), false);
            assert.equal(tree.find(n => n.type === 'input' && n.props.maxLength === 160).props.value, 'Map 1');
            tree.find(n => n.type === 'button' && n.props.children === 'Cancel edit').props.onClick();
        }
    }
});
