import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
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
