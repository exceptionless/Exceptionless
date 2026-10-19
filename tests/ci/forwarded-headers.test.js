import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const repositoryRoot = fileURLToPath(new URL('../../', import.meta.url));
const discoveryScript = join(repositoryRoot, 'k8s/scripts/discover-forwarded-headers.js');
const subnetId = '/subscriptions/subscription/resourceGroups/gateway-group/providers/Microsoft.Network/virtualNetworks/network/subnets/gateway';

function discover(directory, association = subnetId) {
    const output = join(directory, 'forwarding.json');
    const azureFixture = join(directory, 'azure-fixture.mjs');
    writeFileSync(azureFixture, `
        import assert from 'node:assert/strict';
        assert.deepEqual(process.argv.slice(2), [
            'network', 'vnet', 'subnet', 'show', '--ids', process.env.TEST_SUBNET,
            '--query', '{id:id,addressPrefix:addressPrefix,addressPrefixes:addressPrefixes,delegations:delegations[].serviceName}',
            '--output', 'json', '--only-show-errors'
        ]);
        console.log(JSON.stringify({
            id: process.env.TEST_SUBNET,
            addressPrefix: '10.60.64.0/24',
            delegations: ['Microsoft.ServiceNetworking/trafficControllers']
        }));
    `);
    // Exercise the real command interpreter and argument forwarding on Windows.
    writeFileSync(join(directory, 'az.cmd'), `@"${process.execPath}" "${azureFixture}" %*\r\n`);

    const runner = join(directory, 'runner.mjs');
    writeFileSync(runner, `
        import assert from 'node:assert/strict';
        import childProcess from 'node:child_process';
        import { syncBuiltinESMExports } from 'node:module';
        import { pathToFileURL } from 'node:url';
        const execute = childProcess.execFileSync;
        childProcess.execFileSync = (command, args, options) => {
            if (command === 'helm') {
                return '  alb.networking.azure.io/alb-name: public-alb\\n  alb.networking.azure.io/alb-namespace: kube-system\\n';
            }
            if (command === 'kubectl') {
                assert.deepEqual(args, [
                    'get', 'applicationloadbalancer.alb.networking.azure.io', 'public-alb',
                    '--namespace', 'kube-system', '--output', 'json'
                ]);
                return JSON.stringify({
                    metadata: { generation: 1 },
                    spec: { associations: [process.env.TEST_SUBNET] },
                    status: { conditions: ['Accepted', 'Deployment'].map(type => ({ type, status: 'True', observedGeneration: 1 })) }
                });
            }
            if (command === 'az') {
                assert.notEqual(process.platform, 'win32', 'Windows must invoke az.cmd through its interpreter');
                return execute(process.execPath, [process.env.TEST_AZURE_FIXTURE, ...args], options);
            }
            return execute(command, args, options);
        };
        syncBuiltinESMExports();
        process.argv = [process.execPath, process.env.TEST_DISCOVERY_SCRIPT, 'ex-test', 'values.yaml', process.env.TEST_OUTPUT];
        await import(pathToFileURL(process.env.TEST_DISCOVERY_SCRIPT));
    `);

    const result = spawnSync(process.execPath, [runner], {
        cwd: directory,
        encoding: 'utf8',
        env: {
            ...process.env,
            PATH: `${directory}${process.platform === 'win32' ? ';' : ':'}${process.env.PATH}`,
            TEST_AZURE_FIXTURE: azureFixture,
            TEST_DISCOVERY_SCRIPT: discoveryScript,
            TEST_OUTPUT: output,
            TEST_SUBNET: association
        },
        timeout: 15_000,
        windowsHide: true
    });

    assert.ifError(result.error);
    return { ...result, output };
}

test('discovery uses the registered gateway resource and preserves Azure CLI arguments', () => {
    const directory = mkdtempSync(join(tmpdir(), 'forwarding test '));
    try {
        const result = discover(directory);
        assert.equal(result.status, 0, result.stderr);
        assert.deepEqual(JSON.parse(readFileSync(result.output, 'utf8')), {
            forwardedHeaders: { knownNetworks: ['10.60.64.0/24'] }
        });
    } finally {
        rmSync(directory, { force: true, recursive: true });
    }
});

test('subnet data cannot expand Windows shell syntax and failed discovery removes stale output', () => {
    const directory = mkdtempSync(join(tmpdir(), 'forwarding test '));
    try {
        const output = join(directory, 'forwarding.json');
        writeFileSync(output, 'stale forwarding values');
        const result = discover(directory, `${subnetId}%PATH%`);
        if (process.platform === 'win32') {
            assert.equal(result.status, 1);
            assert.match(result.stderr, /unsupported Windows command-shell characters/);
            assert.equal(existsSync(output), false);
        } else {
            // Direct execution must pass the percent signs literally, without shell expansion.
            assert.equal(result.status, 0, result.stderr);
        }
    } finally {
        rmSync(directory, { force: true, recursive: true });
    }
});
