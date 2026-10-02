import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { runInNewContext } from 'node:vm';

test('runtime configuration preserves the SSL setting, escaped values, and secret boundary', () => {
    const script = fileURLToPath(new URL('./update-config.sh', import.meta.url));
    const message = "Quoted 'message' with \\ and a newline\n";
    for (const setting of [undefined, 'false', 'true']) {
        const directory = mkdtempSync(join(tmpdir(), 'exceptionless-runtime-config-'));
        try {
            writeFileSync(join(directory, 'index.html'), '<script src="/_app/env.js"></script>');
            execFileSync('bash', [script], {
                cwd: directory,
                env: {
                    PATH: process.env.PATH,
                    EX_NotificationMessage: message,
                    EX_ConnectionStrings__OAuth: 'GitHubId=public-client;GitHubSecret=private-marker',
                    ...(setting === undefined ? {} : { EX_EnableSsl: setting })
                }
            });
            const source = readFileSync(join(directory, '_app/env.js'), 'utf8');
            const context = { window: { location: { origin: 'http://localhost:8080' } } };
            runInNewContext(source.replace('export const env=', 'globalThis.env='), context);
            assert.equal(context.env.PUBLIC_ENABLE_SSL, setting ?? 'false');
            assert.equal(context.env.PUBLIC_BASE_URL, 'http://localhost:8080');
            assert.equal(context.env.PUBLIC_SYSTEM_NOTIFICATION_MESSAGE, message);
            assert.equal(context.env.PUBLIC_GITHUB_APPID, 'public-client');
            assert.ok(!source.includes('private-marker'));
            assert.match(readFileSync(join(directory, 'index.html'), 'utf8'), /\/_app\/env\.js\?v=[a-f0-9]{32}/);
        } finally {
            rmSync(directory, { recursive: true, force: true });
        }
    }
});

function withStartupFixture(run) {
    const directory = mkdtempSync(join(tmpdir(), 'exceptionless-entrypoint-'));
    try {
        const bin = join(directory, 'bin');
        mkdirSync(bin);
        mkdirSync(join(directory, 'app/wwwroot'), { recursive: true });
        writeFileSync(join(bin, 'update-config'), '#!/bin/bash\nexit "$CONFIG_EXIT"\n', { mode: 0o700 });
        writeFileSync(join(bin, 'dotnet'), '#!/usr/bin/env node\nconsole.log(JSON.stringify({ pid: process.pid, args: process.argv.slice(2) }));\n', { mode: 0o700 });
        run((script, configExit, args = []) => spawnSync('bash', [
            '-c',
            // Run the actual entrypoint with only external processes and container directories replaced.
            'cd() { builtin cd "$TEST_ROOT$1"; }; chown() { :; }; mkdir() { :; }; supervisord() { echo SUPERVISOR_STARTED; exit 0; }; export -f cd chown mkdir supervisord; exec bash "$1" "${@:2}"',
            'entrypoint-test',
            fileURLToPath(new URL(script, import.meta.url)),
            ...args
        ], {
            cwd: directory,
            encoding: 'utf8',
            timeout: 5000,
            env: { ...process.env, PATH: `${bin}:${process.env.PATH}`, TEST_ROOT: directory, CONFIG_EXIT: String(configExit) }
        }));
    } finally {
        rmSync(directory, { recursive: true, force: true });
    }
}

test('app entrypoint propagates config failure and replaces itself without splitting arguments', () => {
    withStartupFixture((run) => {
        const failure = run('./app-docker-entrypoint.sh', 42);
        assert.equal(failure.status, 42, failure.stderr);
        assert.equal(failure.stdout, '');

        const success = run('./app-docker-entrypoint.sh', 0, ['--Example=two words']);
        assert.equal(success.status, 0, success.stderr);
        const application = JSON.parse(success.stdout);
        assert.deepEqual(application.args, ['Exceptionless.Web.dll', '--Example=two words']);
        assert.equal(application.pid, success.pid);
    });
});

test('all-in-one entrypoint starts its supervisor only after successful config generation', () => {
    withStartupFixture((run) => {
        const failure = run('./docker-entrypoint.sh', 42);
        assert.equal(failure.status, 42, failure.stderr);
        assert.ok(!failure.stdout.includes('SUPERVISOR_STARTED'));

        const success = run('./docker-entrypoint.sh', 0);
        assert.equal(success.status, 0, success.stderr);
        assert.ok(success.stdout.includes('SUPERVISOR_STARTED'));
    });
});
