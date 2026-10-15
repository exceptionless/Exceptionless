import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { discoverForwardedHeaders } from '../../k8s/scripts/discover-forwarded-headers.mjs';

const subnetId = '/subscriptions/test/resourceGroups/gateway/providers/Microsoft.Network/virtualNetworks/ingress/subnets/association';
const manifest = '    alb.networking.azure.io/alb-name: public-alb\n    alb.networking.azure.io/alb-namespace: kube-system\n';
const gateway = { spec: { associations: [subnetId] } };
const subnet = { id: subnetId, addressPrefix: '192.0.2.0/24', delegations: [{ serviceName: 'Microsoft.ServiceNetworking/trafficControllers' }] };

function mock({ rendered = manifest, resource = gateway, network = subnet, fail } = {}) {
    const calls = [];
    return {
        calls,
        execute(command, args) {
            calls.push([command, args]);
            if (command === fail) throw new Error('Access denied');
            if (command === 'helm') return rendered;
            if (command === 'kubectl') return JSON.stringify(resource);
            if (command === 'az') return JSON.stringify(network);
            throw new Error(`Unexpected command: ${command}`);
        }
    };
}

for (const environment of ['dev', 'prod']) {
    test(`${environment}: discovers the rendered gateway and passes prefixes through the real chart`, () => {
        // Helm renders locally; Azure and Kubernetes commands are always mocked.
        const valuesFile = `k8s/ex-${environment}-values.yaml`;
        const release = `ex-${environment}`;
        const rendered = execFileSync('helm', ['template', release, 'k8s/exceptionless', '--values', valuesFile, '--show-only', 'templates/api.yaml'], { encoding: 'utf8' });
        const stub = mock({ rendered });
        const values = discoverForwardedHeaders(release, valuesFile, stub.execute);
        assert.deepEqual(values, { forwardedHeaders: { knownNetworks: ['192.0.2.0/24'] } });
        assert.deepEqual(stub.calls[1], ['kubectl', ['get', 'applicationloadbalancers.alb.networking.azure.io', 'public-alb', '--namespace', 'kube-system', '--output', 'json']]);
        assert.deepEqual(stub.calls[2], ['az', ['network', 'vnet', 'subnet', 'show', '--ids', subnetId, '--output', 'json', '--only-show-errors']]);

        const directory = mkdtempSync(join(tmpdir(), 'forwarded-headers-'));
        try {
            const previous = join(directory, 'previous.json');
            const discovered = join(directory, 'discovered.json');
            writeFileSync(previous, JSON.stringify({ forwardedHeaders: { knownNetworks: ['198.51.100.0/24', '203.0.113.0/24'] } }));
            writeFileSync(discovered, JSON.stringify(values));
            const args = ['template', release, 'k8s/exceptionless', '--values', valuesFile, '--values', previous, '--values', discovered, '--show-only', 'templates/config.yaml'];
            const config = execFileSync('helm', args, { encoding: 'utf8' });
            assert.match(config, /EX_ForwardedHeaders__ForwardLimit: "1"/);
            assert.match(config, /EX_ForwardedHeaders__KnownNetworks__0: "192\.0\.2\.0\/24"/);
            assert.doesNotMatch(config, /KnownNetworks__1|198\.51\.100|203\.0\.113/);
            for (const key of ['EX_ForwardedHeaders__KnownNetworks__2', 'ForwardedHeaders__KnownProxies__0', 'EX_ForwardedHeaders__ForwardLimit']) {
                assert.throws(() => execFileSync('helm', [...args, '--set-string', `config.${key}=1`], { stdio: 'pipe' }), /Do not combine discovered/);
            }
        } finally {
            rmSync(directory, { recursive: true, force: true });
        }
    });
}

test('deduplicates association IDs and IPv4/IPv6 prefixes', () => {
    const stub = mock({ resource: { spec: { associations: [subnetId, subnetId] } }, network: { ...subnet, addressPrefixes: ['192.0.2.0/24', '2001:db8::/64'] } });
    assert.deepEqual(discoverForwardedHeaders('ex-test', 'values.yaml', stub.execute).forwardedHeaders.knownNetworks, ['192.0.2.0/24', '2001:db8::/64']);
    assert.equal(stub.calls.filter(([command]) => command === 'az').length, 1);
});

for (const [name, options, message] of [
    ['missing ingress', { rendered: '' }, /annotation/],
    ['conflicting gateway', { rendered: `${manifest}    alb.networking.azure.io/alb-name: other-alb\n` }, /annotation/],
    ['missing association', { resource: { spec: {} } }, /no association/],
    ['invalid association', { resource: { spec: { associations: ['--help'] } } }, /resource ID/],
    ['wrong subnet', { network: { ...subnet, id: `${subnetId}-other` } }, /identity/],
    ['missing delegation', { network: { ...subnet, delegations: [] } }, /delegation/],
    ['missing prefix', { network: { ...subnet, addressPrefix: undefined } }, /no address prefixes/],
    ...['0.0.0.0/0', '::/0', '192.0.2.0/33', 'bad/24', '192.0.2.0/24/extra'].map(prefix => [prefix, { network: { ...subnet, addressPrefix: prefix } }, /Invalid or unrestricted/]),
    ['Kubernetes denied', { fail: 'kubectl' }, /Access denied/],
    ['Azure denied', { fail: 'az' }, /Access denied/]
]) {
    test(`refuses discovery: ${name}`, () => {
        const stub = mock(options);
        assert.throws(() => discoverForwardedHeaders('ex-test', 'values.yaml', stub.execute), message);
    });
}

test('both deployment paths require discovery and consume its output', () => {
    const workflow = readFileSync('.github/workflows/build.yaml', 'utf8');
    for (const environment of ['dev', 'prod']) {
        assert.ok(workflow.includes(`node k8s/scripts/discover-forwarded-headers.mjs ex-${environment} k8s/ex-${environment}-values.yaml "$RUNNER_TEMP/forwarded-headers.json"`));
        assert.ok(workflow.includes(`--values ./k8s/ex-${environment}-values.yaml --values "$RUNNER_TEMP/forwarded-headers.json"`));
    }
});

test('CLI failure exits nonzero and removes stale generated trust', () => {
    const directory = mkdtempSync(join(tmpdir(), 'forwarded-headers-cli-'));
    try {
        // Restrict PATH to a failing fake Helm; no cluster or Azure tools can run.
        writeFileSync(join(directory, 'helm'), '#!/bin/sh\nexit 7\n', { mode: 0o700 });
        const output = join(directory, 'values.json');
        writeFileSync(output, JSON.stringify({ forwardedHeaders: { knownNetworks: ['198.51.100.0/24'] } }));
        const result = spawnSync(process.execPath, ['k8s/scripts/discover-forwarded-headers.mjs', 'ex-test', 'unused.yaml', output], {
            encoding: 'utf8', env: { ...process.env, PATH: directory }
        });
        assert.equal(result.status, 1);
        assert.match(result.stderr, /Forwarded-header discovery failed/);
        assert.equal(existsSync(output), false);
    } finally {
        rmSync(directory, { recursive: true, force: true });
    }
});
