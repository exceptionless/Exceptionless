// Merge the collector's native data, never percentages or lossy Cobertura conditions.
import { execFile, execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, join, posix, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { isDeepStrictEqual, parseArgs, promisify } from 'node:util';
import { SaxesParser } from 'saxes';

const root = resolve(import.meta.dirname, '../..');
const execute = promisify(execFile);
const collectorVersion = '18.11.2';
const readJson = (path) => JSON.parse(readFileSync(path, 'utf8'));
const hash = (data) => createHash('sha256').update(data).digest('hex');
const settingsHash = () => hash(readFileSync(join(root, 'tests/CodeCoverage.config')));
const commit = () => execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim();
const identity = () => ({
    commit: commit(),
    run: process.env.GITHUB_RUN_ID ?? 'local',
    attempt: process.env.GITHUB_RUN_ATTEMPT ?? '1',
    collector: collectorVersion,
    configuration: 'Release',
    settings: settingsHash()
});

function integer(value) {
    if (!/^\d+$/.test(value ?? '') || !Number.isSafeInteger(Number(value))) throw new Error(`Invalid coverage integer: ${value}`);
    return Number(value);
}

export function sourcePath(path, sourceRoot) {
    const normalized = path.replaceAll('\\', '/');
    const prefix = sourceRoot.replaceAll('\\', '/').replace(/\/$/, '') + '/';
    const relative = normalized.startsWith(prefix) ? normalized.slice(prefix.length) : normalized;
    if (!relative.startsWith('src/') || posix.normalize(relative) !== relative || relative.includes('/obj/') || relative.includes('/bin/')) {
        throw new Error(`Unexpected coverage source path: ${path}`);
    }
    return relative;
}

export function readNative(xml, sourceRoot) {
    const modules = [];
    const lines = new Map();
    const sources = new Map();
    let current;
    let rootSeen = false;
    const parser = new SaxesParser();
    parser.on('doctype', () => {
        throw new Error('Coverage reports must not contain a DTD');
    });
    parser.on('opentag', ({ name, attributes: a }) => {
        if (name === 'results') rootSeen = true;
        if (name === 'module') {
            if (current || !a.id || !a.name) throw new Error('Invalid coverage module');
            current = {
                id: a.id,
                name: a.name,
                covered: integer(a.blocks_covered),
                total: integer(a.blocks_covered) + integer(a.blocks_not_covered),
                sources: new Map(),
                ranges: []
            };
        }
        if (name === 'source_file') {
            if (!current || current.sources.has(a.id) || a.checksum_type !== 'SHA256' || !/^[a-f\d]{64}$/i.test(a.checksum ?? '')) {
                throw new Error('Missing, duplicate or unsupported source checksum');
            }
            current.sources.set(a.id, { path: sourcePath(a.path, sourceRoot), checksum: a.checksum.toLowerCase() });
        }
        if (name === 'range') {
            if (!current || !['yes', 'partial', 'no'].includes(a.covered)) throw new Error('Invalid coverage range');
            const start = integer(a.start_line);
            const end = integer(a.end_line);
            if (start < 1 || end < start || end - start > 100_000) throw new Error('Invalid source range');
            current.ranges.push({ source: a.source_id, start, end, covered: a.covered !== 'no' });
        }
    });
    parser.on('closetag', ({ name }) => {
        if (name !== 'module') return;
        if (!current || modules.some((m) => m.name === current.name)) throw new Error('Duplicate coverage module');
        for (const source of current.sources.values()) {
            if (sources.has(source.path) && sources.get(source.path) !== source.checksum) throw new Error('Incompatible source revisions');
            sources.set(source.path, source.checksum);
        }
        for (const range of current.ranges) {
            const source = current.sources.get(range.source);
            if (!source) throw new Error('Coverage range references a missing source');
            for (let line = range.start; line <= range.end; line++) {
                const key = `${source.path}:${line}`;
                lines.set(key, (lines.get(key) ?? false) || range.covered);
            }
        }
        modules.push({ id: current.id, name: current.name, covered: current.covered, total: current.total });
        current = undefined;
    });
    parser.write(xml).close();
    if (!rootSeen || !modules.length || !lines.size || current) throw new Error('Empty or malformed coverage report');
    return { modules, lines, sources };
}

export function unionLines(reports) {
    const lines = new Map();
    for (const report of reports) {
        for (const [key, covered] of report.lines) lines.set(key, (lines.get(key) ?? false) || covered);
    }
    return lines;
}

export function assertUnion(reports, merged) {
    if (!isDeepStrictEqual(unionLines(reports), merged.lines)) throw new Error('Merged coverage is not the union of source locations');
}

export function validateManifests(manifests, expected, kind, count) {
    if (
        !Number.isInteger(count) ||
        count < 1 ||
        manifests.length !== count ||
        !isDeepStrictEqual(
            manifests.map((m) => m.index).sort((a, b) => a - b),
            Array.from({ length: count }, (_, i) => i + 1)
        )
    ) {
        throw new Error('Missing or duplicate coverage shards');
    }
    const sessions = new Set();
    for (const manifest of manifests) {
        if (
            manifest.schema !== 1 ||
            manifest.kind !== kind ||
            manifest.count !== count ||
            manifest.complete !== true ||
            typeof manifest.session !== 'string' ||
            !manifest.session ||
            sessions.has(manifest.session) ||
            !isDeepStrictEqual(manifest.identity, expected) ||
            typeof manifest.sha256 !== 'string' ||
            !/^[a-f\d]{64}$/.test(manifest.sha256) ||
            typeof manifest.source_root !== 'string'
        ) {
            throw new Error('Malformed, stale, incompatible or incomplete coverage manifest');
        }
        const measurements =
            kind === 'e2e'
                ? ['startup_seconds', 'test_seconds', 'shutdown_seconds', 'total_seconds', 'peak_process_tree_rss_kib', 'peak_host_used_kib']
                : ['test_seconds'];
        if (measurements.some((name) => !Number.isFinite(manifest.timings?.[name]) || manifest.timings[name] < 0)) {
            throw new Error('Missing or malformed coverage measurements');
        }
        sessions.add(manifest.session);
    }
}

function collector(args) {
    execFileSync('dotnet', ['tool', 'run', 'dotnet-coverage', '--', ...args], { cwd: root, stdio: 'inherit', timeout: 120_000 });
}

export function validateSources(reports, repositoryRoot = root) {
    const sources = new Map();
    const modules = new Map();
    for (const report of reports) {
        for (const module of report.modules) {
            const key = `${module.id}:${module.total}`;
            if (modules.has(module.name) && modules.get(module.name) !== key) throw new Error(`Incompatible module build: ${module.name}`);
            modules.set(module.name, key);
        }
        for (const [path, checksum] of report.sources) {
            if (sources.has(path) && sources.get(path) !== checksum) throw new Error(`Incompatible source revision: ${path}`);
            if (!sources.has(path) && hash(readFileSync(join(repositoryRoot, path))) !== checksum) throw new Error(`Source checksum mismatch: ${path}`);
            sources.set(path, checksum);
        }
    }
}

function convert(file, output, sourceRoot) {
    collector(['merge', file, '--output-format', 'xml', '--output', output]);
    return readNative(readFileSync(output, 'utf8'), sourceRoot);
}

export function seal(directory, { kind, index, count, session, complete, timings }) {
    const file = join(directory, 'backend.coverage');
    const report = convert(file, join(directory, 'backend.xml'), root);
    validateSources([report]);
    if (kind === 'e2e') {
        for (const name of ['Exceptionless.Web.dll', 'Exceptionless.Job.dll', 'Exceptionless.Core.dll', 'Exceptionless.Insulation.dll']) {
            if (!report.modules.some((m) => m.name === name && m.covered > 0)) throw new Error(`Missing application coverage: ${name}`);
        }
    }
    const manifest = {
        schema: 1,
        identity: identity(),
        kind,
        index,
        count,
        session,
        complete,
        source_root: root,
        sha256: hash(readFileSync(file)),
        timings
    };
    writeFileSync(join(directory, 'coverage-manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
    return report;
}

function findManifests(directory) {
    return readdirSync(directory, { recursive: true, withFileTypes: true })
        .filter((entry) => entry.isFile() && entry.name === 'coverage-manifest.json')
        .map((entry) => join(entry.parentPath, entry.name));
}

async function loadShards(directory, kind, count, expected) {
    const paths = findManifests(directory);
    const manifests = paths.map(readJson);
    validateManifests(manifests, expected, kind, count);
    const shards = paths.map((path, index) => {
        const manifest = manifests[index];
        const file = join(dirname(path), 'backend.coverage');
        if (hash(readFileSync(file)) !== manifest.sha256) throw new Error(`Coverage artifact checksum mismatch: ${file}`);
        return { manifest, file, report: undefined };
    });
    // Bound collector processes to limit CPU and memory contention. Re-convert every
    // checked native input; parallelism never replaces source or union validation.
    for (let offset = 0; offset < shards.length; offset += 2) {
        const results = await Promise.allSettled(
            shards.slice(offset, offset + 2).map(async (shard) => {
                const output = join(dirname(shard.file), 'verified.xml');
                const result = await execute(
                    'dotnet',
                    ['tool', 'run', 'dotnet-coverage', '--', 'merge', shard.file, '--output-format', 'xml', '--output', output],
                    {
                        cwd: root,
                        timeout: 120_000,
                        maxBuffer: 8 * 1024 * 1024
                    }
                );
                process.stdout.write(result.stdout);
                process.stderr.write(result.stderr);
                shard.report = readNative(readFileSync(output, 'utf8'), shard.manifest.source_root);
            })
        );
        const errors = results.filter((result) => result.status === 'rejected').map((result) => result.reason);
        if (errors.length) throw new AggregateError(errors, `Native coverage verification failed:\n${errors.map((error) => error.message).join('\n')}`);
    }
    return shards;
}

const escapeXml = (value) => String(value).replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');

function statistics(report) {
    return {
        lines_covered: [...report.lines.values()].filter(Boolean).length,
        lines_total: report.lines.size,
        blocks_covered: report.modules.reduce((sum, m) => sum + m.covered, 0),
        blocks_total: report.modules.reduce((sum, m) => sum + m.total, 0)
    };
}

// One class per canonical source file avoids counting linked files twice across modules.
// Branch attributes are deliberately absent: native Microsoft reports carry blocks, not branches.
export function writeCobertura(report, output, packageName = 'Backend') {
    const files = new Map();
    for (const [key, covered] of [...report.lines].sort(([a], [b]) => a.localeCompare(b))) {
        const separator = key.lastIndexOf(':');
        const file = key.slice(0, separator);
        if (!files.has(file)) files.set(file, []);
        files.get(file).push({ line: Number(key.slice(separator + 1)), covered });
    }
    const stats = statistics(report);
    const xml = [
        '<?xml version="1.0" encoding="utf-8"?>',
        `<coverage line-rate="${stats.lines_covered / stats.lines_total}" lines-covered="${stats.lines_covered}" lines-valid="${stats.lines_total}" version="1.0">`,
        `<sources><source>.</source></sources><packages><package name="${escapeXml(packageName)}"><classes>`
    ];
    const classNames = new Set();
    for (const [file, lines] of files) {
        // Preserve the complete path: e.g. button.ts and button.svelte must not
        // collide, nor may a dotted filename collide with nested directories.
        const className = file.replaceAll('/', '.');
        if (classNames.has(className)) throw new Error(`Ambiguous coverage class name: ${file}`);
        classNames.add(className);
        xml.push(`<class name="${escapeXml(className)}" filename="${escapeXml(file)}"><methods/><lines>`);
        for (const { line, covered } of lines.sort((a, b) => a.line - b.line)) xml.push(`<line number="${line}" hits="${Number(covered)}"/>`);
        xml.push('</lines></class>');
    }
    xml.push('</classes></package></packages></coverage>');
    writeFileSync(output, xml.join('\n') + '\n');
    return stats;
}

function merge(shards, directory) {
    mkdirSync(directory, { recursive: true });
    const output = join(directory, 'backend.coverage');
    collector(['merge', ...shards.map((s) => s.file), '--output-format', 'coverage', '--output', output]);
    const report = convert(output, join(directory, 'backend.xml'), shards[0].manifest.source_root);
    validateSources([...shards.map((s) => s.report), report]);
    assertUnion(
        shards.map((s) => s.report),
        report
    );
    const stats = writeCobertura(report, join(directory, 'Cobertura.xml'));
    writeFileSync(join(directory, 'summary.json'), JSON.stringify(stats, null, 2) + '\n');
    return { report, stats };
}

async function aggregate(directory, output, apiCount, e2eCount) {
    if (existsSync(output) && readdirSync(output).length) throw new Error('Coverage report output directory must be empty');
    const started = performance.now();
    const expected = identity();
    const api = await loadShards(join(directory, 'api'), 'api', apiCount, expected);
    const e2e = e2eCount ? await loadShards(join(directory, 'e2e'), 'e2e', e2eCount, expected) : [];
    validateSources([...api, ...e2e].map((s) => s.report));
    const components = { '.NET tests': merge(api, join(output, 'dotnet')) };
    if (e2e.length) {
        components['E2E backend'] = merge(e2e, join(output, 'e2e'));
        components['Combined backend'] = merge([...api, ...e2e], join(output, 'combined'));
    } else {
        // Preserve the existing .NET-only artifact's Cobertura entry point.
        copyFileSync(join(output, 'dotnet/Cobertura.xml'), join(output, 'Cobertura.xml'));
    }
    const percentage = (covered, total) => `${((100 * covered) / total).toFixed(2)}% (${covered.toLocaleString('en-US')}/${total.toLocaleString('en-US')})`;
    const headline = (components['Combined backend'] ?? components['.NET tests']).stats;
    const summary = [
        '### Backend code coverage',
        '',
        `**${e2e.length ? 'Combined backend' : '.NET tests'}: ${percentage(headline.lines_covered, headline.lines_total)} source lines.**`,
        '',
        `Commit \`${expected.commit}\`; run \`${expected.run}\`, attempt \`${expected.attempt}\`.`,
        '',
        '| Execution | Source lines | IL blocks |',
        '| --- | ---: | ---: |'
    ];
    for (const [name, { stats }] of Object.entries(components)) {
        summary.push(`| ${name} | ${percentage(stats.lines_covered, stats.lines_total)} | ${percentage(stats.blocks_covered, stats.blocks_total)} |`);
    }
    summary.push(
        '',
        'Combined line and block coverage uses the native collector union. Exact cross-shard branch coverage is unavailable; IL blocks are not branches.',
        ''
    );
    if (e2e.length) {
        const added = [...components['E2E backend'].report.lines]
            .filter(([key, hit]) => hit && !components['.NET tests'].report.lines.get(key))
            .map(([key]) => key);
        writeFileSync(join(output, 'e2e-added-lines.json'), JSON.stringify(added.sort(), null, 2) + '\n');
        summary.push(
            `E2E adds **${added.length.toLocaleString('en-US')} source lines** beyond the .NET suite. Full source locations are in \`e2e-added-lines.json\`.`,
            ''
        );
        summary.push(
            '| E2E shard | Startup seconds | Tests seconds | Shutdown seconds | Process tree RSS MiB | Host used MiB |',
            '| --- | ---: | ---: | ---: | ---: | ---: |'
        );
        for (const { manifest } of e2e.sort((a, b) => a.manifest.index - b.manifest.index)) {
            const t = manifest.timings;
            summary.push(
                `| ${manifest.index} | ${t.startup_seconds.toFixed(1)} | ${t.test_seconds.toFixed(1)} | ${t.shutdown_seconds.toFixed(1)} | ${(t.peak_process_tree_rss_kib / 1024).toFixed(0)} | ${(t.peak_host_used_kib / 1024).toFixed(0)} |`
            );
        }
    }
    const elapsed = (performance.now() - started) / 1000;
    const peakRss = process.resourceUsage().maxRSS;
    summary.push('', `Coverage validation and aggregation: ${elapsed.toFixed(1)} seconds; Node peak RSS ${(peakRss / 1024).toFixed(0)} MiB.`, '');
    writeFileSync(join(output, 'summary.md'), summary.join('\n'));
    writeFileSync(
        join(output, 'provenance.json'),
        JSON.stringify({ identity: expected, api_shards: apiCount, e2e_shards: e2eCount, aggregation_seconds: elapsed, node_peak_rss_kib: peakRss }, null, 2) +
            '\n'
    );
    console.log(summary.join('\n'));
}

async function main() {
    const { values, positionals } = parseArgs({
        allowPositionals: true,
        options: { output: { type: 'string' }, api: { type: 'string' }, e2e: { type: 'string' } }
    });
    if (positionals[0] !== 'aggregate' || positionals.length !== 2 || !values.output)
        throw new Error('Usage: backend-coverage.mjs aggregate DIR --output DIR --api N [--e2e N]');
    await aggregate(resolve(positionals[1]), resolve(values.output), Number(values.api), Number(values.e2e ?? 0));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
    try {
        await main();
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}
