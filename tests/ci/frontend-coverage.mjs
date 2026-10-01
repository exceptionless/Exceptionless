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
            ['package-lock.json', 'vite.config.ts', 'svelte.config.js', 'scripts/test-coverage/policy.ts', 'scripts/test-coverage/vite.ts']
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

export async function browserPlan(sources = inventory()) {
    const { createServer } = require('vite');
    const { frontendCoverage } = await import('../../src/Exceptionless.Web/ClientApp/scripts/test-coverage/vite.ts');
    const raw = {};
    const previousDirectory = process.cwd();
    process.chdir(client);
    let server;
    try {
        server = await createServer({
            configFile: join(client, 'vite.config.ts'),
            root: client,
            optimizeDeps: { noDiscovery: true, include: [] },
            server: { middlewareMode: true, watch: null },
            plugins: [
                frontendCoverage((file) => { raw[file.path] = structuredClone(file); }),
                {
                    name: 'coverage-plan-without-warmup',
                    configResolved(config) { config.server.warmup = { clientFiles: [], ssrFiles: [] }; }
                }
            ]
        });
        for (const path of Object.keys(sources).filter(browserSource)) {
            await server.environments.client.transformRequest('/' + path);
        }
        const mapped = (await createSourceMapStore().transformCoverage(createCoverageMap(raw))).toJSON();
        return includeUntouched(normalize(mapped, client, sources), Object.fromEntries(Object.entries(sources).filter(([path]) => browserSource(path))));
    } finally {
        await server?.close();
        process.chdir(previousDirectory);
    }
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
            !['seconds', 'finalize_seconds', 'peak_node_rss_kib'].every((key) => Number.isFinite(manifest[key]) && manifest[key] >= 0) ||
            typeof manifest.source_root !== 'string') {
            throw new Error('Malformed, stale, incompatible or incomplete frontend coverage manifest');
        }
        sessions.add(manifest.session);
    }
}

export async function seal(directory, { kind, index, count, session, complete, seconds }) {
    const started = performance.now();
    const sources = inventory();
    const start = readJson(join(directory, 'collection-start.json'));
    let coverage;
    let documents;
    let planSha;
    let planSeconds;
    if (kind === 'unit') {
        coverage = includeUntouched(normalize(readJson(join(directory, 'coverage-final.json')), client, sources), sources);
        const planStarted = performance.now();
        writeJson(join(directory, 'browser-plan.json'), await browserPlan(sources));
        planSeconds = (performance.now() - planStarted) / 1000;
        planSha = hash(readFileSync(join(directory, 'browser-plan.json')));
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
    const end = { identity: identity(), sources: inventory(), session };
    writeJson(join(directory, 'collection-end.json'), end);
    if (start.session !== session || !isDeepStrictEqual(start.identity, end.identity) || !isDeepStrictEqual(start.sources, end.sources)) {
        throw new Error('Frontend source or collection identity changed during execution');
    }
    writeJson(join(directory, 'frontend.json'), coverage);
    writeJson(join(directory, 'manifest.json'), {
        schema: 1, kind, index, count, session, complete, seconds, documents,
        finalize_seconds: (performance.now() - started) / 1000,
        peak_node_rss_kib: process.resourceUsage().maxRSS,
        identity: start.identity, source_root: client, sources,
        sha256: hash(readFileSync(join(directory, 'frontend.json'))),
        browser_plan_sha256: planSha,
        browser_plan_seconds: planSeconds
    });
}

export function validateBrowserPlan(plan, sources) {
    if (!isDeepStrictEqual(Object.keys(plan).sort(), Object.keys(sources).filter(browserSource).sort())) {
        throw new Error('Browser source plan has missing or unexpected files');
    }
    if (Object.values(plan).some((file) =>
        [...Object.values(file.s), ...Object.values(file.f), ...Object.values(file.b).flat()].some((value) => counter(value) !== 0))) {
        throw new Error('Browser source plan must contain only unexecuted source');
    }
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
        let plan;
        if (kind === 'unit') {
            if (!isDeepStrictEqual(Object.keys(coverage).sort(), Object.keys(sources).sort())) {
                throw new Error('Unit coverage has missing source files');
            }
            const rawPlan = readFileSync(join(directory, 'browser-plan.json'));
            if (hash(rawPlan) !== manifest.browser_plan_sha256 || !Number.isFinite(manifest.browser_plan_seconds) || manifest.browser_plan_seconds < 0) {
                throw new Error('Missing, malformed or corrupt browser source plan');
            }
            plan = normalize(JSON.parse(rawPlan), manifest.source_root, sources);
            validateBrowserPlan(plan, sources);
        } else if (Object.keys(coverage).some((path) => !browserSource(path))) {
            throw new Error('Browser collected server-only source');
        }
        return { manifest, coverage, plan };
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

export function sourceLines(reports) {
    const lines = new Map();
    for (const report of reports) {
        for (const [path, file] of Object.entries(report)) {
            for (const [id, span] of Object.entries(file.statementMap)) {
                const key = `${path}:${span.start.line}`;
                lines.set(key, (lines.get(key) ?? false) || counter(file.s[id]) > 0);
            }
        }
    }
    return lines;
}

export function verifyHtmlSummary(actual, expected) {
    if (actual.coveredlines !== expected.covered || actual.coverablelines !== expected.total ||
        actual.coveredbranches !== 0 || actual.totalbranches !== 0) {
        throw new Error('Frontend HTML coverage differs from the canonical line union');
    }
}

export async function aggregate(input, output, count) {
    const started = performance.now();
    const units = load(join(input, 'unit'), 'unit', 1);
    const browsers = load(join(input, 'e2e'), 'e2e', count);
    const unit = union(units.map((entry) => entry.coverage));
    const e2e = union([units[0].plan, ...browsers.map((entry) => entry.coverage)]);
    const unitLines = sourceLines([unit]);
    const e2eLines = sourceLines([e2e]);
    const combinedLines = sourceLines([unit, e2e]);
    // All line reports use the same eligible source locations, including the
    // client-only locations removed by Svelte's server-side unit compilation.
    for (const key of combinedLines.keys()) {
        if (!unitLines.has(key)) unitLines.set(key, false);
        const path = key.slice(0, key.lastIndexOf(':'));
        if (browserSource(path) && !e2eLines.has(key)) e2eLines.set(key, false);
    }
    mkdirSync(output, { recursive: false });
    const { writeCobertura } = await import('./backend-coverage.mjs');
    const summaries = {};
    for (const [name, lines, data] of [['unit', unitLines, unit], ['e2e', e2eLines, e2e], ['combined', combinedLines, undefined]]) {
        const directory = join(output, name);
        mkdirSync(directory);
        const collectorSummary = data ? report(join(directory, 'collector'), data) : undefined;
        const covered = [...lines.values()].filter(Boolean).length;
        summaries[name] = {
            lines: { covered, total: lines.size, pct: Number((100 * covered / lines.size).toFixed(2)) },
            branches: collectorSummary?.branches ?? null
        };
        writeCobertura({
            modules: [],
            lines: new Map([...lines].map(([key, covered]) => [`src/Exceptionless.Web/ClientApp/${key}`, covered]))
        }, join(directory, 'Cobertura.xml'), 'Frontend');
        writeJson(join(directory, 'lines.json'), Object.fromEntries([...lines].sort(([a], [b]) => a.localeCompare(b))));
        writeJson(join(directory, 'summary.json'), summaries[name]);
    }
    const added = {};
    for (const [key, covered] of combinedLines) {
        if (!covered || unitLines.get(key)) continue;
        const separator = key.lastIndexOf(':');
        const file = key.slice(0, separator);
        (added[file] ??= []).push(Number(key.slice(separator + 1)));
    }
    writeJson(join(output, 'e2e-added-lines.json'), added);
    writeJson(join(output, 'summary.json'), summaries);
    writeJson(join(output, 'provenance.json'), {
        identity: identity(), inputs: [...units, ...browsers].map((entry) => entry.manifest),
        seconds: (performance.now() - started) / 1000, peak_node_rss_kib: process.resourceUsage().maxRSS
    });
    const rows = Object.entries(summaries).map(([name, summary]) =>
        `| ${name === 'unit' ? 'Unit/component' : name === 'e2e' ? 'E2E browser' : '**Combined frontend**'} | ${summary.lines.pct}% (${summary.lines.covered}/${summary.lines.total}) | ${summary.branches ? `${summary.branches.pct}% (${summary.branches.covered}/${summary.branches.total})` : '**Unavailable**'} |`
    );
    writeFileSync(join(output, 'summary.md'), [
        '### Frontend source coverage', '', '| Execution | Canonical source lines | Collector-reported branches |', '| --- | ---: | ---: |', ...rows, '',
        `E2E adds **${Object.values(added).reduce((sum, lines) => sum + lines.length, 0)}** covered source lines beyond unit/component tests.`,
        '', 'Line coverage is an exact source-location union, not averaged percentages. Svelte server/unit and browser compilation emit incompatible branch maps; their branch figures remain separate and are not directly comparable. Combined branches are unavailable.',
        'Canonical HTML/Cobertura reports contain lines only. Component collector reports retain their own branch maps and compiler-specific line footprints; unmapped Svelte compiler control flow is not counted.',
        ''
    ].join('\n'));
    return summaries;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    const { values, positionals } = parseArgs({
        allowPositionals: true,
        options: { count: { type: 'string', default: '6' } }
    });
    if (positionals[0] === 'verify-html' && positionals.length === 2) {
        const directory = resolve(positionals[1]);
        const summaries = readJson(join(directory, 'summary.json'));
        for (const [name, summary] of Object.entries(summaries)) {
            verifyHtmlSummary(readJson(join(directory, name, 'html/Summary.json')).summary, summary.lines);
        }
    } else if (positionals[0] === 'aggregate' && positionals.length === 3) {
        await aggregate(resolve(positionals[1]), resolve(positionals[2]), Number(values.count));
    } else {
        throw new Error('Usage: frontend-coverage.mjs aggregate INPUT OUTPUT --count 6 | verify-html OUTPUT');
    }
}
