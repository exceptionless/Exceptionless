// Opt-in runner: ordinary Aspire development and production builds never load a profiler.
import { spawn, execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { closeSync, existsSync, mkdirSync, openSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { parseArgs } from 'node:util';
import { seal } from './backend-coverage.mjs';

const root = resolve(import.meta.dirname, '../..');
const apphost = join(root, 'src/Exceptionless.AppHost');
const client = join(root, 'src/Exceptionless.Web/ClientApp');
const { values, positionals } = parseArgs({
    allowPositionals: true,
    options: {
        output: { type: 'string' },
        index: { type: 'string' },
        count: { type: 'string' }
    }
});
const index = Number(values.index);
const count = Number(values.count);
if (!values.output || !Number.isInteger(index) || !Number.isInteger(count) || index < 1 || index > count) {
    throw new Error('Usage: e2e-backend-coverage.mjs --output DIR --index N --count N [-- Playwright arguments]');
}
const output = resolve(values.output);
// Reusing an output directory could accidentally publish a previous successful run.
mkdirSync(output, { recursive: false });
const session = `e2e-${process.env.GITHUB_RUN_ID ?? 'local'}-${process.env.GITHUB_RUN_ATTEMPT ?? '1'}-${index}-${randomUUID()}`;
const log = openSync(join(output, 'aspire.log'), 'w');
const collectorLog = openSync(join(output, 'collector.log'), 'w');
const started = performance.now();
const timings = {};
let server;
let app;
let testExitCode;
let interrupted = false;
let complete = false;
let appUrl;
let peakRssKiB = 0;
let peakHostUsedKiB = 0;
let lastResources = [];
const errors = [];

function run(command, args, options = {}) {
    const child = spawn(command, args, { cwd: root, stdio: 'inherit', ...options });
    child.done = new Promise((resolve, reject) => {
        child.once('error', reject);
        child.once('exit', (code, signal) => resolve({ code: code ?? 1, signal }));
    });
    // A process may fail while another operation is in progress.
    child.done.catch(() => {});
    return child;
}

async function command(command, args, options = {}) {
    const child = run(command, args, { timeout: 30_000, killSignal: 'SIGKILL', ...options });
    const result = await child.done;
    if (result.code !== 0) throw new Error(`${command} ${args[0]} failed (${result.code}, ${result.signal ?? 'exit'})`);
}

async function coverage(args) {
    await command('dotnet', ['tool', 'run', 'dotnet-coverage', '--', ...args]);
}

async function waitForExit(child, milliseconds) {
    let timer;
    try {
        return await Promise.race([
            child.done,
            new Promise((_, reject) => {
                timer = setTimeout(() => reject(new Error('Process did not exit')), milliseconds);
            })
        ]);
    } finally {
        clearTimeout(timer);
    }
}

const alive = (child) => child && child.exitCode === null && child.signalCode === null;
function terminate(child, signal) {
    if (!child?.pid) return;
    try {
        if (process.platform === 'win32') child.kill(signal);
        else process.kill(-child.pid, signal);
    } catch (error) {
        if (error.code !== 'ESRCH') throw error;
    }
}

function describe() {
    return JSON.parse(
        execFileSync('aspire', ['describe', '--apphost', apphost, '--format', 'Json', '--non-interactive'], {
            cwd: root,
            encoding: 'utf8',
            timeout: 15_000,
            stdio: ['ignore', 'pipe', 'ignore'],
            maxBuffer: 8 * 1024 * 1024
        })
    ).resources;
}

function assertLocal(url) {
    const parsed = new URL(url);
    if (parsed.hostname !== 'localhost' && !parsed.hostname.endsWith('.localhost')) throw new Error('E2E target must be localhost');
    return parsed.origin;
}

// Sample only this runner's process tree; other worktrees are not part of the measurement.
const memorySampler = setInterval(() => {
    if (process.platform !== 'linux') return;
    try {
        const rows = execFileSync('ps', ['-eo', 'pid=,ppid=,rss='], { encoding: 'utf8', timeout: 3000 })
            .trim()
            .split('\n')
            .map((line) => line.trim().split(/\s+/).map(Number));
        const ids = new Set([process.pid]);
        for (let previous = -1; previous !== ids.size;) {
            previous = ids.size;
            for (const [pid, parent] of rows) if (ids.has(parent)) ids.add(pid);
        }
        peakRssKiB = Math.max(
            peakRssKiB,
            rows.reduce((total, [pid, , rss]) => total + (ids.has(pid) ? rss : 0), 0)
        );
        const memory = readFileSync('/proc/meminfo', 'utf8');
        peakHostUsedKiB = Math.max(peakHostUsedKiB, Number(memory.match(/^MemTotal:\s+(\d+)/m)[1]) - Number(memory.match(/^MemAvailable:\s+(\d+)/m)[1]));
    } catch {
        // Measurements are diagnostic; collector and test failures are handled separately.
    }
}, 2000);
memorySampler.unref();
for (const signal of ['SIGINT', 'SIGTERM'])
    process.once(signal, () => {
        interrupted = true;
    });

try {
    let existing;
    try {
        existing = describe();
    } catch {
        /* No AppHost has been started in this checkout. */
    }
    if (existing?.some((r) => r.state === 'Running')) throw new Error('An AppHost is already running in this checkout; use an isolated worktree');
    // Direct Release startup bypasses the CLI's automatic certificate-trust step.
    await command('aspire', ['certs', 'trust', '--non-interactive'], { stdio: ['ignore', log, log] });
    server = run(
        'dotnet',
        [
            'tool',
            'run',
            'dotnet-coverage',
            '--',
            'collect',
            '--server-mode',
            '--session-id',
            session,
            '--settings',
            join(root, 'tests/CodeCoverage.config'),
            '--output',
            join(output, 'backend.coverage'),
            '--output-format',
            'coverage',
            '--log-file',
            join(output, 'coverage-diagnostics.log'),
            '--log-level',
            'Error'
        ],
        { detached: process.platform !== 'win32', stdio: ['ignore', 'pipe', collectorLog] }
    );
    await new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error('Collector did not become ready within 30 seconds')), 30_000);
        let text = '';
        server.stdout.on('data', (data) => {
            writeFileSync(collectorLog, data);
            text += data.toString();
            if (text.includes(`SessionId: ${session}`)) {
                clearTimeout(timer);
                resolve();
            }
        });
        server.done.then(() => {
            clearTimeout(timer);
            reject(new Error('Collector exited before readiness'));
        }, reject);
    });
    app = run(
        'dotnet',
        [
            'tool',
            'run',
            'dotnet-coverage',
            '--',
            'connect',
            session,
            '--timeout',
            '10000',
            'dotnet',
            'run',
            '--project',
            apphost,
            '--configuration',
            'Release',
            '--',
            '--ci-e2e'
        ],
        {
            // Aspire 13.6's dotnet run hook otherwise delegates back to CLI Debug startup.
            env: { ...process.env, ASPIRE_SUPPRESS_CLI_RUN_HOOK: 'true' },
            detached: process.platform !== 'win32',
            stdio: ['ignore', log, log]
        }
    );
    const deadline = performance.now() + 300_000;
    while (performance.now() < deadline && !interrupted) {
        if (!alive(app) || !alive(server)) throw new Error('AppHost or collector exited during startup; see diagnostics');
        let resources;
        try {
            resources = describe();
            lastResources = resources;
        } catch {
            await delay(1000);
            continue;
        }
        const healthy = ['Api', 'Jobs'].every((name) =>
            resources.some((r) => r.displayName === name && r.state === 'Running' && Object.values(r.healthReports).every((h) => h.status === 'Healthy'))
        );
        const frontend = resources.find((r) => r.displayName === 'App' && r.state === 'Running');
        const url = frontend?.urls?.find((u) => new URL(u.url).hostname === 'localhost')?.url ?? frontend?.urls?.[0]?.url;
        if (healthy && url) {
            appUrl = assertLocal(url);
            try {
                for (const path of ['/api/v2/about', '/next/']) {
                    execFileSync('curl', ['--fail', '--silent', '--show-error', '--insecure', '--max-time', '20', appUrl + path], {
                        timeout: 25_000,
                        stdio: 'ignore'
                    });
                }
                break;
            } catch {
                appUrl = undefined;
            }
        }
        await delay(1000);
    }
    if (!appUrl || interrupted) throw new Error('Aspire did not become healthy before the startup deadline');
    timings.startup_seconds = (performance.now() - started) / 1000;
    console.log(`Coverage session ready; API, Jobs and App healthy at ${appUrl}`);
    const testStart = performance.now();
    const tests = run('npm', ['run', 'test:e2e:ci', '--', `--shard=${index}/${count}`, ...positionals], {
        cwd: client,
        detached: process.platform !== 'win32',
        env: { ...process.env, E2E_URL: appUrl, E2E_SHARD: String(index), E2E_RUN_ID: session }
    });
    const cancelTest = setInterval(() => {
        if (interrupted) terminate(tests, 'SIGTERM');
    }, 1000);
    try {
        testExitCode = (await tests.done).code;
    } finally {
        clearInterval(cancelTest);
    }
    timings.test_seconds = (performance.now() - testStart) / 1000;
} catch (error) {
    errors.push(error.message);
} finally {
    const shutdownStart = performance.now();
    if (app && (testExitCode !== 0 || errors.length)) {
        writeFileSync(
            join(output, 'resource-state.json'),
            JSON.stringify(
                lastResources.map((r) => ({
                    name: r.displayName,
                    state: r.state,
                    health: Object.fromEntries(Object.entries(r.healthReports ?? {}).map(([name, report]) => [name, report.status ?? 'Pending']))
                })),
                null,
                2
            ) + '\n'
        );
        await Promise.allSettled(
            ['Api', 'Jobs', 'App', 'OldApp'].map(async (resource) => {
                const file = openSync(join(output, `${resource}.log`), 'w');
                try {
                    await command('aspire', ['logs', resource, '--apphost', apphost, '--tail', '200', '--timestamps', '--non-interactive'], {
                        timeout: 10_000,
                        stdio: ['ignore', file, file]
                    });
                } finally {
                    closeSync(file);
                }
            })
        );
    }
    // Snapshot while all clients are alive. Preserve it even if graceful shutdown fails.
    if (alive(server)) {
        try {
            await coverage(['snapshot', session, '--timeout', '10000', '--output', join(output, 'snapshot.coverage')]);
        } catch (error) {
            errors.push(`Snapshot: ${error.message}`);
        }
    }
    if (app) {
        try {
            await command('aspire', ['stop', '--apphost', apphost, '--non-interactive'], { stdio: ['ignore', log, log] });
            const result = await waitForExit(app, 15_000);
            if (result.code !== 0) throw new Error(`AppHost client exited with ${result.code}`);
        } catch (error) {
            errors.push(`AppHost shutdown: ${error.message}`);
            terminate(app, 'SIGTERM');
            try {
                await waitForExit(app, 5000);
            } catch {
                terminate(app, 'SIGKILL');
            }
        }
    }
    if (alive(server)) {
        try {
            await coverage(['shutdown', session, '--timeout', '10000']);
            const result = await waitForExit(server, 15_000);
            if (result.code !== 0) throw new Error(`Collector exited with ${result.code}`);
        } catch (error) {
            errors.push(`Collector shutdown: ${error.message}`);
            terminate(server, 'SIGKILL');
        }
    } else if (server) {
        errors.push('Collector exited before explicit finalization');
    }
    timings.shutdown_seconds = (performance.now() - shutdownStart) / 1000;
    timings.total_seconds = (performance.now() - started) / 1000;
    clearInterval(memorySampler);
    const diagnostics = join(output, 'coverage-diagnostics.log');
    if (existsSync(diagnostics) && readFileSync(diagnostics, 'utf8').trim()) errors.push('Collector reported errors; see coverage-diagnostics.log');
    complete = !interrupted && testExitCode !== undefined && errors.length === 0;
    if (complete) {
        try {
            seal(output, {
                kind: 'e2e',
                index,
                count,
                session,
                complete: testExitCode === 0,
                timings: { ...timings, peak_process_tree_rss_kib: peakRssKiB, peak_host_used_kib: peakHostUsedKiB }
            });
        } catch (error) {
            errors.push(`Coverage validation: ${error.message}`);
            complete = false;
        }
    }
    writeFileSync(
        join(output, 'lifecycle.json'),
        JSON.stringify(
            {
                session,
                index,
                count,
                complete,
                test_exit_code: testExitCode,
                timings,
                peak_process_tree_rss_kib: peakRssKiB,
                peak_host_used_kib: peakHostUsedKiB,
                errors
            },
            null,
            2
        ) + '\n'
    );
    closeSync(log);
    closeSync(collectorLog);
}
for (const error of errors) console.error(error);
process.exitCode = testExitCode || (complete ? 0 : 1);
