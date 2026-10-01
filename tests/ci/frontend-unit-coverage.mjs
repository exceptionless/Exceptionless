import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { parseArgs } from 'node:util';
import { begin, seal } from './frontend-coverage.mjs';

const root = resolve(import.meta.dirname, '../..');
const { values, positionals } = parseArgs({ allowPositionals: true, options: { output: { type: 'string' } } });
if (!values.output) throw new Error('Usage: frontend-unit-coverage.mjs --output DIR [-- Vitest arguments]');
const output = resolve(values.output);
mkdirSync(output, { recursive: false });
const session = `unit-${process.env.GITHUB_RUN_ID ?? 'local'}-${process.env.GITHUB_RUN_ATTEMPT ?? '1'}-${randomUUID()}`;
begin(output, session);
const started = performance.now();
const child = spawn('npm', ['run', 'test:unit', '--', '--coverage', `--coverage.reportsDirectory=${output}`, '--coverage.clean=false', ...positionals], {
    cwd: join(root, 'src/Exceptionless.Web/ClientApp'), stdio: 'inherit'
});
const code = await new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('exit', (code) => resolve(code ?? 1));
});
const seconds = (performance.now() - started) / 1000;
try {
    await seal(output, { kind: 'unit', index: 1, count: 1, session, complete: code === 0, seconds });
    process.exitCode = code;
} catch (error) {
    writeFileSync(join(output, 'collection-error.txt'), String(error) + '\n');
    console.error(error);
    process.exitCode = code || 1;
}
