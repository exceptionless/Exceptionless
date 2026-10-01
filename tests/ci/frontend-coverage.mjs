import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join, posix, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { isDeepStrictEqual, parseArgs } from 'node:util';
import { browserSource, eligibleSource } from '../../src/Exceptionless.Web/ClientApp/scripts/test-coverage/policy.ts';

const root = resolve(import.meta.dirname, '../..');
const client = join(root, 'src/Exceptionless.Web/ClientApp');
const require = createRequire(join(client, 'package.json'));
const { createCoverageMap } = require('@vitest/istanbul-lib-coverage');
const { createSourceMapStore } = require('@vitest/istanbul-lib-source-maps');
const { create, createContext } = require('@vitest/istanbul-lib-report');
const readJson = (file) => JSON.parse(readFileSync(file, 'utf8'));
const hash = (value) => createHash('sha256').update(value).digest('hex');
const writeJson = (file, value) => writeFileSync(file, JSON.stringify(value) + '\n');

export function identity() {
    return {
        commit: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
        run: process.env.GITHUB_RUN_ID ?? 'local',
        attempt: process.env.GITHUB_RUN_ATTEMPT ?? '1',
        collector: 'istanbul-5.0.3/1.0.2',
        settings: hash(
            ['package-lock.json', 'vite.config.ts', 'scripts/test-coverage/policy.ts', 'scripts/test-coverage/vite.ts']
                .map((path) => readFileSync(join(client, path), 'utf8'))
                .join('\n')
        )
    };
}

export function sourcePath(path, sourceRoot) {
    const normalized = path.replaceAll('\\', '/');
    const prefix = sourceRoot.replaceAll('\\', '/').replace(/\/$/, '') + '/';
    const relative = normalized.startsWith(prefix) ? normalized.slice(prefix.length) : normalized;
    if (posix.normalize(relative) !== relative || !eligibleSource(relative)) {
        throw new Error(`Unexpected frontend coverage source: ${path}`);
    }
    return relative;
}

export function inventory() {
    const paths = execFileSync('git', ['ls-files', '-z', '--', 'src'], { cwd: client, encoding: 'utf8' }).split('\0').filter(Boolean);
    return Object.fromEntries(paths.filter(eligibleSource).sort().map((path) => [path, hash(readFileSync(join(client, path)))]));
}

export function begin(directory, session) {
    writeJson(join(directory, 'collection-start.json'), { identity: identity(), sources: inventory(), session });
}

function counter(value) {
    if (!Number.isSafeInteger(value) || value < 0) throw new Error('Malformed frontend coverage counter');
    return Number(value > 0);
}

// Generated anonymous names and declaration spans can differ across Svelte's
// environments. Source body spans identify functions; ordered source arm spans
// identify branches. Never infer an arm from a percentage.
export function normalizeFile(file, path, source) {
    const lines = source.split(/\r?\n/);
    const point = (value, end = false) => {
        if (!Number.isInteger(value?.line) || value.line < 1 || value.line > lines.length) {
            throw new Error(`Coverage location outside source: ${path}`);
        }
        const column = end && (value.column === null || value.column === Infinity) ? lines[value.line - 1].length : value.column;
        if (!Number.isInteger(column) || column < 0 || column > lines[value.line - 1].length) {
            throw new Error(`Invalid coverage column: ${path}:${value.line}`);
        }
        return { line: value.line, column };
    };
    const range = (value) => {
        const start = point(value?.start);
        const end = point(value?.end, true);
        // Svelte reorders template expressions in generated JavaScript. Retain
        // both mapped endpoints (and the statement's original starting line);
        // sorting them would invent a different source location.
        if (!path.endsWith('.svelte') && (end.line < start.line || (end.line === start.line && end.column < start.column))) {
            throw new Error(`Reversed coverage location: ${path}`);
        }
        return { start, end };
    };
    const entries = (map, counts, convert) => {
        if (!map || !counts || Array.isArray(map) || Array.isArray(counts) ||
            !isDeepStrictEqual(Object.keys(map).sort(), Object.keys(counts).sort())) {
            throw new Error(`Missing frontend coverage counters: ${path}`);
        }
        return Object.entries(map).map(([id, value]) => convert(value, counts[id]));
    };
    const statements = new Map();
    for (const [span, hit] of entries(file.statementMap, file.s, (span, hit) => [range(span), counter(hit)])) {
        const key = JSON.stringify(span);
        statements.set(key, { span, hit: Number(Boolean(hit || statements.get(key)?.hit)) });
    }
    const functions = new Map();
    for (const [span, hit] of entries(file.fnMap, file.f, (fn, hit) => [range(fn.loc), counter(hit)])) {
        const key = JSON.stringify(span);
        functions.set(key, { span, hit: Number(Boolean(hit || functions.get(key)?.hit)) });
    }
    const branches = new Map();
    for (const [branch, hits] of entries(file.branchMap, file.b, (branch, hits) => {
        if (typeof branch.type !== 'string' || !Array.isArray(branch.locations) || !Array.isArray(hits) || hits.length !== branch.locations.length) {
            throw new Error(`Malformed frontend branch: ${path}`);
        }
        const locations = branch.locations.map((location, index) => {
            // An if without an else has an implicit false arm. Istanbul retains
            // its separate counter but intentionally supplies no source span.
            if (branch.type === 'if' && index === 1 && location.start && location.end &&
                [location.start.line, location.start.column, location.end.line, location.end.column].every((value) => value === undefined)) {
                return { start: {}, end: {} };
            }
            return range(location);
        });
        return [{ type: branch.type, loc: range(branch.loc), locations }, hits.map(counter)];
    })) {
        const key = JSON.stringify(branch);
        const previous = branches.get(key);
        branches.set(key, { branch, hits: hits.map((hit, i) => Number(Boolean(hit || previous?.hits[i]))) });
    }
    const output = { path, statementMap: {}, fnMap: {}, branchMap: {}, s: {}, f: {}, b: {} };
    [...statements].sort(([a], [b]) => a.localeCompare(b)).forEach(([, { span, hit }], id) => {
        output.statementMap[id] = span;
        output.s[id] = hit;
    });
    [...functions].sort(([a], [b]) => a.localeCompare(b)).forEach(([, { span, hit }], id) => {
        output.fnMap[id] = { name: `source_${id}`, decl: span, loc: span, line: span.start.line };
        output.f[id] = hit;
    });
    [...branches].sort(([a], [b]) => a.localeCompare(b)).forEach(([, { branch, hits }], id) => {
        output.branchMap[id] = { ...branch, line: branch.loc.start.line };
        output.b[id] = hits;
    });
    return output;
}

export function normalize(raw, sourceRoot = client, sources = inventory()) {
    const output = {};
    for (const [path, file] of Object.entries(raw)) {
        const relative = sourcePath(path, sourceRoot);
        if (output[relative] || !sources[relative] || sourcePath(file.path, sourceRoot) !== relative) {
            throw new Error(`Duplicate or untracked frontend source: ${path}`);
        }
        output[relative] = normalizeFile(file, relative, readFileSync(join(client, relative), 'utf8'));
    }
    if (!Object.keys(output).length) throw new Error('Empty frontend coverage');
    return output;
}

function includeUntouched(coverage, sources) {
    const ts = require('typescript');
    for (const path of Object.keys(sources)) {
        if (coverage[path] && ['s', 'f', 'b'].some((kind) => Object.keys(coverage[path][kind]).length)) continue;
        const source = readFileSync(join(client, path), 'utf8');
        const parsed = ts.createSourceFile(path, source, ts.ScriptTarget.Latest, true);
        // Istanbul correctly omits imports, re-exports and erased types. Every
        // other eligible file must have actual counters, even if never imported.
        const onlyDeclarations = !path.endsWith('.svelte') && parsed.statements.every((statement) =>
            ts.isImportDeclaration(statement) || ts.isExportDeclaration(statement) ||
            ts.isInterfaceDeclaration(statement) || ts.isTypeAliasDeclaration(statement) || ts.isEmptyStatement(statement)
        );
        if (!onlyDeclarations) throw new Error(`Untouched executable source missing from coverage: ${path}`);
        coverage[path] = { path, statementMap: {}, fnMap: {}, branchMap: {}, s: {}, f: {}, b: {} };
    }
    return coverage;
}

export function union(reports) {
    const merged = {};
    for (const report of reports) {
        for (const [path, file] of Object.entries(report)) {
            if (!merged[path]) {
                merged[path] = structuredClone(file);
                continue;
            }
            const previous = merged[path];
            for (const map of ['statementMap', 'fnMap', 'branchMap']) {
                if (!isDeepStrictEqual(previous[map], file[map])) throw new Error(`Incompatible frontend ${map}: ${path}`);
            }
            for (const kind of ['s', 'f']) {
                for (const key of Object.keys(file[kind])) previous[kind][key] = Number(counter(previous[kind][key]) || counter(file[kind][key]));
            }
            for (const key of Object.keys(file.b)) {
                previous.b[key] = file.b[key].map((hit, i) => Number(counter(hit) || counter(previous.b[key][i])));
            }
        }
    }
    return merged;
}

export function validateManifests(manifests, expected, kind, count) {
    const indexes = manifests.map((manifest) => manifest.index).sort((a, b) => a - b);
    if (!Number.isInteger(count) || count < 1 || !isDeepStrictEqual(indexes, Array.from({ length: count }, (_, i) => i + 1))) {
        throw new Error('Missing or duplicate frontend coverage shards');
    }
    const sessions = new Set();
    for (const manifest of manifests) {
        if (manifest.schema !== 1 || manifest.kind !== kind || manifest.count !== count || manifest.complete !== true ||
            !isDeepStrictEqual(manifest.identity, expected) || typeof manifest.session !== 'string' || !manifest.session ||
            sessions.has(manifest.session) || !/^[a-f\d]{64}$/.test(manifest.sha256 ?? '') ||
            !Number.isFinite(manifest.seconds) || manifest.seconds < 0 || typeof manifest.source_root !== 'string') {
            throw new Error('Malformed, stale, incompatible or incomplete frontend coverage manifest');
        }
        sessions.add(manifest.session);
    }
}

export async function seal(directory, { kind, index, count, session, complete, seconds }) {
    const sources = inventory();
    const start = readJson(join(directory, 'collection-start.json'));
    if (start.session !== session || !isDeepStrictEqual(start.identity, identity()) || !isDeepStrictEqual(start.sources, sources)) {
        throw new Error('Frontend source or collection identity changed during execution');
    }
    let coverage;
    let documents;
    if (kind === 'unit') {
        coverage = includeUntouched(normalize(readJson(join(directory, 'coverage-final.json')), client, sources), sources);
    } else {
        const workers = readdirSync(directory).filter((name) => /^worker-\d+-[a-f\d-]+\.json$/.test(name)).sort();
        if (!workers.length) throw new Error('No browser coverage workers finalized');
        const reports = [];
        documents = 0;
        for (const worker of workers) {
            const data = readJson(join(directory, worker));
            if (data.complete !== true || data.session !== session || !Number.isInteger(data.documents) || data.documents < 1) {
                throw new Error(`Incomplete browser coverage worker: ${worker}`);
            }
            documents += data.documents;
            const mapped = await createSourceMapStore().transformCoverage(createCoverageMap(data.coverage));
            const normalized = normalize(mapped.toJSON(), client, sources);
            if (Object.keys(normalized).some((path) => !browserSource(path))) throw new Error('Browser collected server-only source');
            reports.push(normalized);
        }
        coverage = union(reports);
    }
    writeJson(join(directory, 'frontend.json'), coverage);
    writeJson(join(directory, 'manifest.json'), {
        schema: 1, kind, index, count, session, complete, seconds, documents,
        identity: start.identity, source_root: client, sources,
        sha256: hash(readFileSync(join(directory, 'frontend.json')))
    });
}

function load(directory, kind, count) {
    const inputs = readdirSync(directory).map((name) => join(directory, name));
    const manifests = inputs.map((path) => readJson(join(path, 'manifest.json')));
    validateManifests(manifests, identity(), kind, count);
    const sources = inventory();
    return inputs.map((directory, i) => {
        const manifest = manifests[i];
        if (!isDeepStrictEqual(manifest.sources, sources)) throw new Error('Frontend source inventory or revision mismatch');
        const raw = readFileSync(join(directory, 'frontend.json'));
        if (hash(raw) !== manifest.sha256) throw new Error('Frontend coverage checksum mismatch');
        const coverage = normalize(JSON.parse(raw), manifest.source_root, sources);
        return { manifest, coverage };
    });
}

function report(directory, data) {
    mkdirSync(directory, { recursive: false });
    const absolute = Object.fromEntries(Object.entries(data).map(([path, file]) => [join(client, path), { ...file, path: join(client, path) }]));
    const coverageMap = createCoverageMap(absolute);
    const context = createContext({ dir: directory, coverageMap });
    for (const format of ['html', 'json', 'json-summary', 'cobertura']) create(format).execute(context);
    return coverageMap.getCoverageSummary().toJSON();
}

export function aggregate(input, output, count) {
    const started = performance.now();
    const units = load(join(input, 'unit'), 'unit', 1);
    const browsers = load(join(input, 'e2e'), 'e2e', count);
    const unit = union(units.map((entry) => entry.coverage));
    // Vitest's explicitly included, untouched sources supply the browser's zero
    // counters as well. Browser-only source can extend this map only if present
    // in the sealed source inventory.
    const emptyBrowser = Object.fromEntries(Object.entries(unit).filter(([path]) => browserSource(path)).map(([path, file]) => {
        const empty = structuredClone(file);
        for (const kind of ['s', 'f']) for (const key of Object.keys(empty[kind])) empty[kind][key] = 0;
        for (const key of Object.keys(empty.b)) empty.b[key].fill(0);
        return [path, empty];
    }));
    const e2e = union([emptyBrowser, ...browsers.map((entry) => entry.coverage)]);
    const combined = union([unit, e2e]);
    mkdirSync(output, { recursive: false });
    const summaries = {
        unit: report(join(output, 'unit'), unit),
        e2e: report(join(output, 'e2e'), e2e),
        combined: report(join(output, 'combined'), combined)
    };
    const unitMap = createCoverageMap(unit);
    const added = {};
    for (const file of createCoverageMap(combined).files()) {
        const lines = createCoverageMap(combined).fileCoverageFor(file).getLineCoverage();
        const before = unit[file] ? unitMap.fileCoverageFor(file).getLineCoverage() : {};
        const hits = Object.entries(lines).filter(([line, hits]) => hits > 0 && !(before[line] > 0)).map(([line]) => Number(line));
        if (hits.length) added[file] = hits;
    }
    writeJson(join(output, 'e2e-added-lines.json'), added);
    writeJson(join(output, 'summary.json'), summaries);
    writeJson(join(output, 'provenance.json'), {
        identity: identity(), inputs: [...units, ...browsers].map((entry) => entry.manifest),
        seconds: (performance.now() - started) / 1000, peak_node_rss_kib: process.resourceUsage().maxRSS
    });
    const rows = Object.entries(summaries).map(([name, summary]) =>
        `| ${name === 'unit' ? 'Unit/component' : name === 'e2e' ? 'E2E browser' : '**Combined frontend**'} | ${summary.lines.pct}% (${summary.lines.covered}/${summary.lines.total}) | ${summary.branches.pct}% (${summary.branches.covered}/${summary.branches.total}) |`
    );
    writeFileSync(join(output, 'summary.md'), [
        '### Frontend source coverage', '', '| Execution | Lines | Mapped branches |', '| --- | ---: | ---: |', ...rows, '',
        `E2E adds **${Object.values(added).reduce((sum, lines) => sum + lines.length, 0)}** covered source lines beyond unit/component tests.`,
        '', 'Counters are source-location unions, not averaged percentages. Svelte compiler control flow without an original-source mapping is not counted as a mapped branch.',
        ''
    ].join('\n'));
    return summaries;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    const { values, positionals } = parseArgs({
        allowPositionals: true,
        options: { count: { type: 'string', default: '6' } }
    });
    if (positionals[0] !== 'aggregate' || positionals.length !== 3) {
        throw new Error('Usage: frontend-coverage.mjs aggregate INPUT OUTPUT --count 6');
    }
    aggregate(resolve(positionals[1]), resolve(positionals[2]), Number(values.count));
}
