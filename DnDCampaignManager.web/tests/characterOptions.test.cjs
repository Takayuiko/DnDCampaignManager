const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const flush = () => new Promise(resolve => setImmediate(resolve));
function deferred() { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; }
const option = (name, isCustom = true) => ({ id: 1, name, isCustom });
function harness() {
    const slots = [], modules = new Map(), loads = [], additions = [];
    let cursor = 0, effects = [], campaignId = 1, initialOptions;
    let form = { campaignId, class: 'Fighter', race: 'Human', background: 'Soldier' };
    const api = {};
    for (const [kind, load, create] of [['class','getCharacterClass','addCharacterClass'],
        ['race','getCharacterRaces','addCharacterRace'],['background','getCharacterBackgrounds','addCharacterBackground']]) {
        api[load] = (id, signal) => { const job = { kind, id, signal, ...deferred() }; loads.push(job); return job.promise; };
        api[create] = (id, body) => { const job = { kind, id, body, ...deferred() }; additions.push(job); return job.promise; };
    }
    const react = {
        useState(initial) { const index = cursor++; slots[index] ??= { value: initial };
            return [slots[index].value, value => { slots[index].value = typeof value === 'function' ? value(slots[index].value) : value; }]; },
        useRef(initial) { const index = cursor++; slots[index] ??= { current: initial }; return slots[index]; },
        useMemo(factory) { return factory(); },
        useEffect(effect, deps) { const index = cursor++; const previous = slots[index];
            if (!previous || deps.some((value, i) => value !== previous.deps[i])) {
                slots[index] = { deps, cleanup: previous?.cleanup };
                effects.push(() => { slots[index].cleanup?.(); slots[index].cleanup = effect(); });
            }
        }
    };
    function load(filename) {
        if (modules.has(filename)) return modules.get(filename);
        const result = {}; modules.set(filename, result);
        vm.runInNewContext(ts.transpileModule(fs.readFileSync(filename,'utf8'), {
            compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
        }).outputText, { AbortController, DOMException, setTimeout, clearTimeout, exports: result, require(name) {
            if (name === 'react') return react;
            if (name === '../../api/campaignApi') return api;
            if (name === 'axios') return { isAxiosError: error => error?.isAxiosError === true };
            if (name.startsWith('.')) return load(path.resolve(path.dirname(filename),name+'.ts'));
            return require(name);
        } });
        return result;
    }
    const hook = load(path.join(__dirname,'../src/components/Character/useCharacterOptions.ts')).useCharacterOptions;
    return { loads, additions, get form() { return form; },
        render() { cursor = 0; const result = hook(campaignId, value => { form = typeof value === 'function' ? value(form) : value; },initialOptions);
            const pending = effects; effects = []; pending.forEach(run => run()); return result; },
        switchCampaign(id) { campaignId = id; form = { ...form,campaignId: id }; },
        preloaded(options) { initialOptions = options; },
        unmount() { slots.forEach(slot => slot?.cleanup?.()); }
    };
}

test('option groups load independently, sort and deduplicate names, and isolate failures', async () => {
    const h = harness(); h.render();
    assert.equal(h.loads.length,3);
    assert.equal(h.render().class.loading,true);
    h.loads.find(job=>job.kind==='class').resolve({data:[option(' wizard '),option('Runesmith'),option('runesmith')]});
    h.loads.find(job=>job.kind==='race').reject(new Error('Failure'));
    h.loads.find(job=>job.kind==='background').resolve({data:[option('Soldier',false)]});
    await flush(); const result = h.render();
    assert.equal(result.class.names.filter(name=>name.toLowerCase()==='wizard').length,1);
    assert.equal(result.class.names.filter(name=>name.toLowerCase()==='runesmith').length,1);
    assert.equal(result.race.loading,false);
    assert.match(result.race.error,/Could not load races/);
    assert.equal(result.background.names[0],'Soldier');
    assert.deepEqual(h.form,{campaignId:1,class:'Fighter',race:'Human',background:'Soldier'});
});

test('preloaded options skip requests and do not replace saved identity values', () => {
    const h = harness(); h.preloaded({classes:[option('Runesmith')],races:[option('Human',false)],backgrounds:[]});
    h.render(); const result = h.render();
    assert.equal(h.loads.length,0);
    assert.equal(result.class.loading,false);
    assert.ok(result.class.names.includes('Fighter') && result.class.names.includes('Runesmith'));
    assert.equal(result.race.names[0],'Human');
    assert.equal(h.form.class,'Fighter');
});

test('homebrew creation trims names, prevents overlapping submissions and selects each saved option', async () => {
    const h = harness(); h.preloaded({classes:[],races:[],backgrounds:[]}); h.render();
    for (const kind of ['class','race','background']) {
        h.render()[kind].setDraft('  Custom '+kind+'  ');
        h.render()[kind].setAdding(true);
        const pending = h.render()[kind].add();
        const repeated = h.render()[kind].add();
        assert.equal(h.additions.filter(job=>job.kind===kind).length,1);
        assert.equal(h.render()[kind].saving,true);
        const job = h.additions.at(-1); assert.equal(job.body.name,'Custom '+kind);
        job.resolve({data:option('Custom '+kind)}); await pending; await repeated;
        const result = h.render()[kind];
        assert.equal(result.saving,false); assert.equal(result.adding,false); assert.equal(result.draft,'');
        assert.ok(result.names.includes('Custom '+kind)); assert.equal(h.form[kind],'Custom '+kind);
    }
});

test('late loads and homebrew responses cannot change another campaign or an unmounted hook', async () => {
    const h = harness(); h.render();
    h.render().class.setDraft('Old campaign class'); const add = h.render().class.add();
    h.switchCampaign(2); h.render();
    assert.ok(h.loads.filter(job => job.id === 1).every(job => job.signal.aborted));
    const lateAdd = h.additions[0]; lateAdd.resolve({data:option('Old campaign class')}); await add;
    h.loads.filter(job=>job.id===1).forEach(job=>job.resolve({data:[option('Old result')]}));
    h.loads.filter(job=>job.id===2).forEach(job=>job.resolve({data:[option('New result')]}));
    await flush(); const result = h.render();
    assert.ok(result.class.names.includes('New result'));
    assert.equal(result.class.names.includes('Old result'),false);
    assert.equal(result.class.names.includes('Old campaign class'),false);
    assert.equal(h.form.class,'Fighter');
    h.render().class.setDraft('Unmounted'); const unmounted = h.render().class.add(); h.unmount();
    assert.ok(h.loads.every(job => job.signal.aborted));
    h.additions.at(-1).resolve({data:option('Unmounted')}); await unmounted;
    assert.equal(h.form.class,'Fighter');
});

test('failed creation keeps its draft, clears saving, and shows only safe API messages', async () => {
    const h = harness(); h.preloaded({classes:[],races:[],backgrounds:[]}); h.render();
    h.render().class.setDraft('Duplicate'); h.render().class.setAdding(true);
    let pending = h.render().class.add();
    h.additions.at(-1).reject({isAxiosError:true,response:{status:409,data:'That class already exists.'}});
    await pending; let result = h.render().class;
    assert.equal(result.error,'That class already exists.'); assert.equal(result.draft,'Duplicate');
    assert.equal(result.adding,true); assert.equal(result.saving,false);
    pending = result.add(); h.additions.at(-1).reject({isAxiosError:true,response:{status:500,data:'System.Exception: diagnostics'}});
    await pending; result = h.render().class; assert.equal(result.error,'Could not add class.');
});
