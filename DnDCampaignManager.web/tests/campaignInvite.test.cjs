const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const errorExports = {};
vm.runInNewContext(ts.transpileModule(
    fs.readFileSync(path.join(__dirname, '../src/Utils/apiError.ts'), 'utf8'),
    { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }
).outputText, { AbortController, DOMException, setTimeout, clearTimeout, exports: errorExports, require });

const compiled = ts.transpileModule(
    fs.readFileSync(path.join(__dirname, '../src/pages/EditCampaign.tsx'), 'utf8'),
    { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX } }
).outputText;

function harness(inviteError, status = 404) {
    const states = [];
    let cursor = 0;
    let initialized = false;
    const effects = [];
    const calls = [];
    const exportsObject = {};
    vm.runInNewContext(compiled, {
        AbortController, DOMException, setTimeout, clearTimeout, exports: exportsObject,
        alert(message) { throw new Error(message); },
        require(name) {
            if (name === 'react') return {
                useState(initial) {
                    const index = cursor++;
                    if (!(index in states)) states[index] = initial;
                    return [states[index], value => { states[index] = typeof value === 'function' ? value(states[index]) : value; }];
                },
                useEffect(effect) { if (!initialized) effects.push(effect); },
                useMemo(factory) { return factory(); }
            };
            if (name === 'react-router-dom') return { useParams: () => ({ campaignId: '12' }), useNavigate: () => () => {} };
            if (name === '../auth/AuthContext') return { useAuth: () => ({ user: { id: 1, role: 'DM' } }) };
            if (name === '../Utils/apiError') return errorExports;
            if (name === '../api/campaignApi') return {
                async getCampaign(id) { calls.push(['load', id]); return { data: { id, name: 'Campaign', description: '', ownerId: 1, players: [] } }; },
                async addPlayerToCampaign(id, email) {
                    calls.push(['invite', id, email]);
                    if (inviteError) throw { isAxiosError: true, response: { status, data: inviteError } };
                    return { data: { id: 2, email } };
                }
            };
            return require(name);
        }
    });
    return {
        calls,
        render() { cursor = 0; const tree = exportsObject.default(); initialized = true; return tree; },
        async initialize() { this.render(); effects.forEach(effect => effect()); await new Promise(resolve => setImmediate(resolve)); }
    };
}

function elements(tree) {
    if (!tree || typeof tree !== 'object') return [];
    return [tree, ...[tree.props?.children].flat(Infinity).flatMap(elements)];
}

async function invite(h) {
    await h.initialize();
    elements(h.render()).find(node => node.type === 'button' && node.props.children === 'Players').props.onClick();
    elements(h.render()).find(node => node.type === 'input' && node.props.placeholder === 'player@email.com').props.onChange({ target: { value: ' PLAYER@Example.com ' } });
    await elements(h.render()).find(node => node.type === 'button' && node.props.children === 'Invite').props.onClick();
}

test('management loads the campaignId route and Invite sends a request and shows membership', async () => {
    const h = harness();
    await invite(h);
    assert.deepEqual(h.calls, [['load', 12], ['invite', 12, 'player@example.com']]);
    const tree = h.render();
    assert.ok(elements(tree).some(node => node.props?.role === 'status' && node.props.children.includes('was added')));
    assert.ok(elements(tree).some(node => node.props?.children === 'player@example.com'));
});

test('missing accounts display the API error with no success message', async () => {
    const h = harness('No registered user exists with that email.');
    await invite(h);
    const nodes = elements(h.render());
    assert.ok(nodes.some(node => node.props?.children === 'No registered user exists with that email.'));
    assert.equal(nodes.some(node => node.props?.role === 'status'), false);
});

test('unexpected server errors show a simple message instead of a stack trace', async () => {
    const h = harness('System.InvalidOperationException: internal failure\n at Some.Server.Method()', 500);
    await invite(h);
    const nodes = elements(h.render());
    assert.ok(nodes.some(node => node.props?.children === 'Unable to add the player right now. Please try again.'));
    assert.equal(nodes.some(node => typeof node.props?.children === 'string' && node.props.children.includes('System.')), false);
});

test('error helper preserves validation errors and hides network errors and diagnostic text', () => {
    const error = (status, data) => ({ isAxiosError: true, response: { status, data } });
    assert.equal(errorExports.extractApiError(error(400, { errors: { Email: ['Enter a valid email.'] } }), 'Try again'), 'Enter a valid email.');
    assert.equal(errorExports.extractApiError(error(400, { errors: { Password: ['Password must be between 12 and 128 characters.'] } }), 'Try again'),
        'Password must be between 12 and 128 characters.');
    assert.equal(errorExports.extractApiError(error(400, { message: 'Email already registered' }), 'Try again'), 'Email already registered');
    assert.equal(errorExports.extractApiError(error(500, { message: 'System.Exception: internal failure' }), 'Try again'), 'Try again');
    assert.equal(errorExports.extractApiError(error(400, 'System.InvalidOperationException: internal failure'), 'Try again'), 'Try again');
    assert.equal(errorExports.extractApiError({ isAxiosError: true }, 'Try again'), 'Try again');
});

test('timeouts and cancellation use safe messages without exposing transport errors', () => {
    assert.match(errorExports.extractApiError({ isAxiosError: true, code: 'ECONNABORTED', message: 'Internal URL' }, 'Fallback'),
        /timed out.*check whether your changes were saved/);
    assert.match(errorExports.extractApiError({ isAxiosError: true, code: 'ETIMEDOUT' }, 'Fallback'), /timed out/);
    assert.equal(errorExports.extractApiError({ isAxiosError: true, code: 'ERR_CANCELED' }, 'Fallback'), 'Request cancelled.');
});

test('shared errors support conflict and problem details while suppressing server diagnostics', () => {
    assert.equal(errorExports.extractApiResponseError(409, { error: 'This conversation is busy.' }, 'Failed'), 'This conversation is busy.');
    assert.equal(errorExports.extractApiResponseError(422, { detail: 'Choose a valid skill.' }, 'Failed'), 'Choose a valid skill.');
    assert.equal(errorExports.extractApiResponseError(500, { detail: 'Database unavailable' }, 'Failed'), 'Failed');
    assert.equal(errorExports.extractErrorMessage({ error: 'System.Exception: secret' }, 'Failed'), 'Failed');
    assert.equal(errorExports.extractLoginError({ isAxiosError: true, response: { status: 401, data: 'secret' } }), 'Invalid email or password.');
});
