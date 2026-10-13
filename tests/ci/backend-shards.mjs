// Run complete xUnit classes on balanced shards, using discovery as the source of truth.
import { execFileSync, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { appendFileSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { isDeepStrictEqual, parseArgs } from 'node:util';

const root = resolve(import.meta.dirname, '../..');
const timingsPath = join(import.meta.dirname, 'backend-durations.json');
const readJson = (path) => JSON.parse(readFileSync(path, 'utf8'));

export function partition(classes, durations, count) {
    if (!Number.isInteger(count) || count < 1 || classes.length < count || new Set(classes).size !== classes.length) {
        throw new Error('Shards must be nonempty and discovered classes must be unique');
    }
    const shards = Array.from({ length: count }, () => []);
    const totals = Array(count).fill(0);
    // Stable sorting keeps equal-duration classes in name order. New classes are always included.
    for (const name of [...classes].sort().sort((a, b) => (durations[b] ?? 1) - (durations[a] ?? 1))) {
        const index = totals.reduce(
            (best, total, candidate) => (total < totals[best] || (total === totals[best] && shards[candidate].length < shards[best].length) ? candidate : best),
            0
        );
        shards[index].push(name);
        totals[index] += Math.max(durations[name] ?? 1, 0.01);
    }
    return shards.map((shard) => shard.sort());
}

export function readResults(path) {
    const report = readJson(path);
    if (report.reportFormat !== 'CTRF' || !Array.isArray(report.results?.tests) || !report.results.tests.length) {
        throw new Error(`No CTRF test results in ${path}`);
    }
    if (report.results.extra?.suites?.some((suite) => suite.errors?.length)) {
        throw new Error(`xUnit reported errors outside individual tests: ${path}`);
    }
    return report.results.tests.map((test) => {
        if (!test.extra?.type || typeof test.name !== 'string' || typeof test.status !== 'string' || !Number.isFinite(test.duration) || test.duration < 0) {
            throw new Error(`Invalid xUnit CTRF test result in ${path}`);
        }
        return { class: test.extra.type, name: test.name, seconds: test.duration / 1000, outcome: test.status };
    });
}

export function summarize(results) {
    const durations = new Map();
    for (const result of results) {
        durations.set(result.class, (durations.get(result.class) ?? 0) + result.seconds);
    }
    return Object.fromEntries([...durations.keys()].sort().map((name) => [name, Number(durations.get(name).toFixed(3))]));
}

function run(args) {
    const assembly = resolve(args.assembly ?? join(root, 'tests/Exceptionless.Tests/bin/Release/net10.0/Exceptionless.Tests.dll'));
    const discovered = JSON.parse(
        execFileSync('dotnet', [assembly, '--list-tests', 'json', '--no-ansi'], {
            encoding: 'utf8',
            maxBuffer: 64 * 1024 * 1024
        })
    );
    if (discovered.schemaVersion !== 1) {
        throw new Error('Unsupported Microsoft Testing Platform discovery schema');
    }
    const classes = [...new Set(discovered.tests.map((test) => `${test.type.namespace}.${test.type.typeName}`))].sort();
    const count = Number(args.count);
    const index = Number(args.index);
    const shards = partition(classes, readJson(timingsPath), count);
    if (!Number.isInteger(index) || index < 1 || index > count) {
        throw new Error('Shard index must be between 1 and shard count');
    }
    if (!args.output) {
        throw new Error('The run command requires --output');
    }
    const selected = shards[index - 1];
    const output = resolve(args.output);
    mkdirSync(output, { recursive: true });
    const manifest = {
        index,
        count,
        discovery_hash: createHash('sha256').update(classes.join('\n')).digest('hex'),
        discovered_count: classes.length,
        classes: selected
    };
    writeFileSync(join(output, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
    console.log(`Shard ${index}/${count}: ${selected.length} of ${classes.length} classes`);
    const command = [
        assembly,
        '--results-directory',
        output,
        '--report-xunit-trx',
        '--report-xunit-trx-filename',
        'test-results.trx',
        '--report-xunit-ctrf',
        '--report-xunit-ctrf-filename',
        'test-results.json',
        '--minimum-expected-tests',
        '1',
        '--filter-class',
        ...selected
    ];
    if (args.coverage) {
        command.push(
            '--coverage',
            '--coverage-settings',
            join(root, 'tests/CodeCoverage.config'),
            '--coverage-output',
            'coverage.cobertura.xml',
            '--coverage-output-format',
            'cobertura'
        );
    }
    if (process.env.GITHUB_ACTIONS) {
        command.push('--report-github');
    }
    const result = spawnSync('dotnet', command, {
        stdio: 'inherit',
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' }
    });
    if (result.error) throw result.error;
    return result.status ?? 1;
}

function findFiles(directory, name) {
    return readdirSync(directory, { recursive: true, withFileTypes: true })
        .filter((entry) => entry.isFile() && entry.name === name)
        .map((entry) => join(entry.parentPath, entry.name))
        .sort();
}

export function report({ directory, count, coverage }) {
    const reports = findFiles(directory, 'test-results.json').map((path) => ({ path, results: readResults(path) }));
    if (count !== undefined) {
        const manifests = findFiles(directory, 'manifest.json').map(readJson);
        if (
            !Number.isInteger(count) ||
            count < 1 ||
            reports.length !== count ||
            !isDeepStrictEqual(
                manifests.map((m) => m.index).sort((a, b) => a - b),
                Array.from({ length: count }, (_, i) => i + 1)
            )
        ) {
            throw new Error('Missing or duplicate shard results/manifests');
        }
        if (new Set(manifests.map((m) => m.discovery_hash)).size !== 1 || manifests.some((m) => m.count !== count)) {
            throw new Error('Shards used different test discovery or shard counts');
        }
        const selected = manifests.flatMap((m) => m.classes);
        if (new Set(selected).size !== selected.length || manifests.some((m) => m.discovered_count !== selected.length)) {
            throw new Error('Shard plan duplicated or omitted discovered classes');
        }
        for (const { path, results } of reports) {
            const manifest = readJson(join(dirname(path), 'manifest.json'));
            if (!isDeepStrictEqual(new Set(results.map((result) => result.class)), new Set(manifest.classes))) {
                throw new Error(`Executed classes differ from shard plan: ${path}`);
            }
            if (!existsSync(join(dirname(path), 'test-results.trx'))) {
                throw new Error(`Missing shard TRX report: ${path}`);
            }
            if (coverage && !existsSync(join(dirname(path), 'coverage.cobertura.xml'))) {
                throw new Error(`Missing shard coverage: ${path}`);
            }
        }
    }
    const results = reports.flatMap((report) => report.results);
    if (!results.length) throw new Error('No test reports found');
    const durations = summarize(results);
    const lines = [
        '### .NET test timings',
        '',
        `${results.length} results across ${Object.keys(durations).length} classes.`,
        '',
        '| Slowest class | Total test seconds |',
        '| --- | ---: |'
    ];
    for (const [name, seconds] of Object.entries(durations)
        .sort((a, b) => b[1] - a[1])
        .slice(0, 15)) {
        lines.push(`| ${name} | ${seconds.toFixed(2)} |`);
    }
    lines.push('', '| Slowest test | Seconds |', '| --- | ---: |');
    for (const result of [...results].sort((a, b) => b.seconds - a.seconds).slice(0, 15)) {
        const name = result.name.replaceAll('|', '\\|').replaceAll('\n', ' ');
        lines.push(`| ${name} | ${result.seconds.toFixed(2)} |`);
    }
    return {
        durations,
        summary: lines.join('\n') + '\n',
        exitCode: Number(results.some((result) => !['passed', 'skipped'].includes(result.outcome)))
    };
}

function main() {
    const { values, positionals } = parseArgs({
        allowPositionals: true,
        options: {
            assembly: { type: 'string' },
            index: { type: 'string' },
            count: { type: 'string' },
            output: { type: 'string' },
            coverage: { type: 'boolean' },
            'write-timings': { type: 'string' }
        }
    });
    if (positionals[0] === 'run' && positionals.length === 1) return run(values);
    if (positionals[0] !== 'report' || positionals.length !== 2) {
        throw new Error(
            'Usage: backend-shards.mjs run --index N --count N --output DIR [--coverage] | report DIR [--count N] [--coverage] [--write-timings FILE]'
        );
    }
    const result = report({ directory: positionals[1], count: values.count === undefined ? undefined : Number(values.count), coverage: values.coverage });
    if (values['write-timings']) {
        writeFileSync(values['write-timings'], JSON.stringify(result.durations, null, 2) + '\n');
    }
    console.log(result.summary);
    if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, result.summary);
    return result.exitCode;
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
    try {
        process.exitCode = main();
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}
