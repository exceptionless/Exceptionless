// Verify merged Playwright results against discovery and publish slow tests/retries.
import { appendFileSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { isDeepStrictEqual, parseArgs } from 'node:util';

export function cases(report) {
    function* walk(suites) {
        for (const suite of suites) {
            for (const spec of suite.specs ?? []) {
                for (const test of spec.tests) {
                    yield [JSON.stringify([spec.id, test.projectName]), spec, test];
                }
            }
            yield* walk(suite.suites ?? []);
        }
    }
    return [...walk(report.suites)];
}

export function validate(expected, actual) {
    const planned = new Set(cases(expected).map(([key]) => key));
    const executed = cases(actual).map(([key]) => key);
    if (!planned.size || executed.length !== new Set(executed).size || !isDeepStrictEqual(new Set(executed), planned)) {
        throw new Error('Merged browser results omitted or duplicated discovered tests');
    }
    if (actual.errors?.length) throw new Error('Playwright reported errors outside individual tests');
    for (const [, spec, test] of cases(actual)) {
        if (!test.results?.length || test.status !== 'expected') {
            throw new Error(`Browser test did not pass: ${spec.title} (${test.status})`);
        }
    }
}

function main() {
    const { positionals, values } = parseArgs({ allowPositionals: true, options: { 'write-timings': { type: 'string' } } });
    if (positionals.length !== 2) throw new Error('Usage: browser-report.mjs DISCOVERY_JSON RESULTS_JSON');
    const [expected, actual] = positionals.map((path) => JSON.parse(readFileSync(path, 'utf8')));
    const tests = cases(actual);
    const duration = (test) => test.results.reduce((total, result) => total + result.duration, 0);
    const rows = [...tests].sort((a, b) => duration(b[2]) - duration(a[2]));
    const retries = tests.reduce((total, [, , test]) => total + Math.max(test.results.length - 1, 0), 0);
    const lines = [
        '### Browser test timings',
        '',
        `${tests.length} tests; ${retries} retries.`,
        '',
        '| Slowest test (including retries) | Seconds | Attempts |',
        '| --- | ---: | ---: |'
    ];
    for (const [, spec, test] of rows.slice(0, 20)) {
        const name = `${spec.file}: ${spec.title}`.replaceAll('|', '\\|').replaceAll('\n', ' ');
        lines.push(`| ${name} | ${(duration(test) / 1000).toFixed(2)} | ${test.results.length} |`);
    }
    const summary = lines.join('\n') + '\n';
    console.log(summary);
    if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary);
    validate(expected, actual);
    if (values['write-timings']) {
        writeFileSync(
            values['write-timings'],
            JSON.stringify(
                Object.fromEntries([...tests].sort(([a], [b]) => a.localeCompare(b)).map(([key, , test]) => [key, Number((duration(test) / 1000).toFixed(3))])),
                null,
                2
            ) + '\n'
        );
    }
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
    try {
        main();
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}
