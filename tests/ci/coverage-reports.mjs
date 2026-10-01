import { spawn } from 'node:child_process';
import { appendFileSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const repository = resolve(import.meta.dirname, '../..');
const workspace = resolve(process.argv[2] ?? '.');
const output = (name) => join(workspace, name);

function run(command, args) {
    return new Promise((resolve, reject) => {
        const child = spawn(command, args, { cwd: repository, stdio: 'inherit' });
        child.once('error', reject);
        child.once('exit', (code, signal) => {
            if (code === 0) resolve();
            else reject(new Error(`${command} ${args.join(' ')} failed (${signal ?? code})`));
        });
    });
}

async function backend() {
    await run(process.execPath, ['tests/ci/backend-coverage.mjs', 'aggregate', output('coverage-shards'), '--output', output('backend-coverage'), '--api', '4', '--e2e', '6']);
    for (const component of ['dotnet', 'e2e', 'combined']) {
        await run('dotnet', ['tool', 'run', 'reportgenerator', '--',
            `-reports:${output('backend-coverage')}/${component}/Cobertura.xml`,
            `-targetdir:${output('backend-coverage')}/${component}/html`, '-reporttypes:Html']);
    }
}

async function frontend() {
    await run(process.execPath, ['tests/ci/frontend-coverage.mjs', 'aggregate', output('frontend-shards'), output('frontend-coverage'), '--count', '6']);
    for (const component of ['unit', 'e2e', 'combined']) {
        await run('dotnet', ['tool', 'run', 'reportgenerator', '--',
            `-reports:${output('frontend-coverage')}/${component}/Cobertura.xml`,
            `-targetdir:${output('frontend-coverage')}/${component}/html`, '-reporttypes:Html;JsonSummary']);
    }
    await run(process.execPath, ['tests/ci/frontend-coverage.mjs', 'verify-html', output('frontend-coverage')]);
}

// Independent source languages can aggregate concurrently. Wait for both so one
// collector failure cannot interrupt the other's diagnostics or appear successful.
const results = await Promise.allSettled([backend(), frontend()]);
const errors = results.filter((result) => result.status === 'rejected').map((result) => result.reason);
if (errors.length) throw new AggregateError(errors, 'Coverage reporting failed');

const summary = ['backend-coverage', 'frontend-coverage'].map((name) => readFileSync(join(output(name), 'summary.md'), 'utf8')).join('\n');
writeFileSync(output('coverage-summary.md'), summary);
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary);
