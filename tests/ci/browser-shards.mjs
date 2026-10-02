// Timings affect placement only. Live Playwright discovery owns the test inventory.
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { isDeepStrictEqual } from 'node:util';
import { partition } from './backend-shards.mjs';
import { cases } from './browser-report.mjs';

const client = resolve(import.meta.dirname, '../../src/Exceptionless.Web/ClientApp');
const readJson = (path) => JSON.parse(readFileSync(path, 'utf8'));

export function inventory(report) {
    const tests = [];
    function walk(suites, parents = [], depth = 0) {
        for (const suite of suites) {
            const titles = depth ? [...parents, suite.title] : parents;
            for (const spec of suite.specs ?? []) {
                for (const test of spec.tests) {
                    const parts = [test.projectName, spec.file, ...titles, spec.title];
                    if (parts.some((part) => typeof part !== 'string' || !part.trim() || /[\r\n›]/.test(part))) {
                        throw new Error('Browser test cannot be represented unambiguously in a Playwright test list');
                    }
                    tests.push({
                        key: JSON.stringify([spec.id, test.projectName]),
                        selector: `[${test.projectName}] › ${spec.file} › ${[...titles, spec.title].join(' › ')}`
                    });
                }
            }
            walk(suite.suites ?? [], titles, depth + 1);
        }
    }
    if (report.errors?.length) throw new Error('Browser discovery failed');
    walk(report.suites);
    if (!tests.length || new Set(tests.map((test) => test.key)).size !== tests.length || new Set(tests.map((test) => test.selector)).size !== tests.length) {
        throw new Error('Browser discovery must contain unique tests and selectors');
    }
    return tests;
}

export function plan(report, durations, count) {
    const tests = inventory(report);
    const byKey = new Map(tests.map((test) => [test.key, test]));
    // Give new tests a typical duration rather than packing them into a short shard.
    const known = Object.values(durations)
        .filter((seconds) => Number.isFinite(seconds) && seconds > 0)
        .sort((a, b) => a - b);
    const fallback = known[Math.floor(known.length / 2)] ?? 10;
    const weights = Object.fromEntries(tests.map(({ key }) => [key, Number.isFinite(durations[key]) && durations[key] > 0 ? durations[key] : fallback]));
    return partition(
        tests.map((test) => test.key),
        weights,
        count
    ).map((keys) => ({
        seconds: keys.reduce((sum, key) => sum + weights[key], 0),
        tests: keys.map((key) => byKey.get(key))
    }));
}

export function verifySelection(selected, report) {
    const actual = cases(report).map(([key]) => key);
    if (report.errors?.length || actual.length !== new Set(actual).size || !isDeepStrictEqual(new Set(actual), new Set(selected.map((test) => test.key)))) {
        throw new Error('Playwright test-list selection differs from the shard plan');
    }
}

export function prepare(directory, index, count, args = []) {
    if (!Number.isInteger(index) || index < 1 || index > count) throw new Error('Invalid browser shard index');
    const discover = (name, selection = []) => {
        const output = join(directory, name);
        execFileSync(process.execPath, [join(client, 'node_modules/playwright/cli.js'), 'test', ...args, ...selection, '--list', '--reporter=json'], {
            cwd: client,
            env: { ...process.env, E2E_SHARD: String(index), PLAYWRIGHT_JSON_OUTPUT_FILE: output },
            stdio: ['ignore', 'ignore', 'inherit'],
            timeout: 60_000
        });
        return readJson(output);
    };
    const discovered = discover('browser-discovery.json');
    const shards = plan(discovered, readJson(join(import.meta.dirname, 'browser-durations.json')), count);
    const selected = shards[index - 1];
    const file = join(directory, 'browser-test-list.txt');
    writeFileSync(file, selected.tests.map((test) => test.selector).join('\n') + '\n');
    verifySelection(selected.tests, discover('browser-selected.json', ['--test-list', file]));
    writeFileSync(join(directory, 'browser-shard-plan.json'), JSON.stringify({ index, count, shards }, null, 2) + '\n');
    console.log(`Browser shard ${index}/${count}: ${selected.tests.length} discovered tests, ${selected.seconds.toFixed(1)} estimated test seconds`);
    return file;
}
