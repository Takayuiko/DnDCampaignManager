const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const flush = () => new Promise(resolve => setImmediate(resolve));
function harness() {
    const slots = []; let cursor = 0, effects = [], fail = false, confirm = false, loads = 0;
    const writes = [], alerts = [], navigate = () => {};
    const modules = {};
    const react = {
        useState(initial) { const i = cursor++; if (!(i in slots)) slots[i] = initial; return [slots[i], v => { slots[i] = typeof v === 'function' ? v(slots[i]) : v; }]; },
        useMemo(factory) { return factory(); },
        useEffect(effect, deps) { const i = cursor++; const previous = slots[i]; if (!previous || deps.some((v, j) => v !== previous.deps[j])) {
            slots[i] = { deps, cleanup: previous?.cleanup }; effects.push(() => { slots[i].cleanup?.(); slots[i].cleanup = effect(); });
        } }
    };
    function load(file) {
        const exports = {};
        vm.runInNewContext(ts.transpileModule(fs.readFileSync(path.join(__dirname, file), 'utf8'), {
            compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
        }).outputText, { AbortController, DOMException, setTimeout, clearTimeout, exports, crypto: { randomUUID: () => 'client' }, alert: message => alerts.push(message), window: { confirm: () => confirm }, require(name) {
            if (name === 'react') return react;
            if (name === 'react/jsx-runtime') return { jsx: (type, props) => ({ type, props }), jsxs: (type, props) => ({ type, props }) };
            if (name === 'axios') return { isAxiosError: err => err?.isAxiosError === true };
            if (name === 'react-router-dom') return { useParams: () => ({ campaignId: '1', characterId: '2' }), useNavigate: () => navigate };
            if (name === '../Utils/apiError') return modules.errors;
            if (name === '../api/campaignApi') return {
                async getCharacter() { loads++; return { data: { name: 'Server name', version: loads === 1 ? 'v1' : 'latest', skills: [], attacks: [] } }; },
                async updateCharacterByCampaign(cid, chid, form) { writes.push({ ...form }); if (fail) throw { isAxiosError: true, response: { status: 409, data: { code: 'character_version_conflict', error: 'Reload the latest character.' } } }; return { data: { version: 'v2' } }; }
            };
            return { default: name.includes('Button') ? 'button' : name };
        } }); return exports;
    }
    modules.errors = load('../src/Utils/apiError.ts');
    const Page = load('../src/pages/EditCharacter.tsx').default;
    return { writes, alerts, get loads() { return loads; }, conflict() { fail = true; }, confirmReload(value) { confirm = value; },
        render() { cursor = 0; const tree = Page(); const jobs = effects; effects = []; jobs.forEach(job => job()); return tree; } };
}
test('successful saves advance the form version for the next save', async () => {
    const h = harness(); h.render(); await flush(); let page = h.render();
    await page.props.onSubmit({ preventDefault() {} }); page = h.render();
    assert.equal(h.writes[0].version, 'v1'); assert.equal(page.props.form.version, 'v2');
    await page.props.onSubmit({ preventDefault() {} }); assert.equal(h.writes[1].version, 'v2');
});
test('conflicts retain the draft, block resaving, and require explicit reload confirmation', async () => {
    const h = harness(); h.render(); await flush(); let page = h.render();
    page.props.setForm(current => ({ ...current, name: 'My unsaved draft' })); page = h.render(); h.conflict();
    await page.props.onSubmit({ preventDefault() {} }); page = h.render();
    assert.equal(page.props.form.name, 'My unsaved draft'); assert.equal(page.props.saveDisabled, true); assert.match(page.props.error, /Reload/);
    await page.props.onSubmit({ preventDefault() {} }); assert.equal(h.writes.length, 1); assert.equal(h.alerts.length, 0);
    page.props.topRight.props.children[0].props.onClick(); h.render(); await flush(); assert.equal(h.loads, 1);
    h.confirmReload(true); page = h.render(); page.props.topRight.props.children[0].props.onClick(); h.render(); await flush(); page = h.render();
    assert.equal(h.loads, 2); assert.equal(page.props.form.name, 'Server name'); assert.equal(page.props.form.version, 'latest'); assert.equal(page.props.saveDisabled, false);
});
