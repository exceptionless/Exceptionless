import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';
import { normalizeFile, sourcePath, union, validateManifests } from './frontend-coverage.mjs';

const source = 'export function choose(flag) {\n    if (flag) return 1;\n    return 2;\n}\n';
const span = (line, start = 0, end = 10) => ({ start: { line, column: start }, end: { line, column: end } });
const file = (left, right) => ({
    path: 'src/choose.ts',
    statementMap: { 0: span(2), 1: span(3) }, s: { 0: left, 1: right },
    fnMap: {}, f: {},
    branchMap: { 0: { loc: span(2), type: 'if', locations: [span(2), { start: {}, end: {} }] } },
    b: { 0: [left, right] }
});

test('frontend union distinguishes opposite arms and validates original source maps', () => {
    const left = { 'src/choose.ts': normalizeFile(file(1, 0), 'src/choose.ts', source) };
    const right = { 'src/choose.ts': normalizeFile(file(0, 1), 'src/choose.ts', source) };
    assert.deepEqual(union([left, right])['src/choose.ts'].b[0], [1, 1]);
    assert.deepEqual(union([left, left])['src/choose.ts'].b[0], [1, 0]);
    assert.deepEqual(union([left, right])['src/choose.ts'].s, { 0: 1, 1: 1 });
    const changed = structuredClone(right);
    changed['src/choose.ts'].branchMap[0].loc.end.column++;
    assert.throws(() => union([left, changed]), /Incompatible frontend branchMap/);
    const malformed = file(1, 0);
    malformed.s[0] = -1;
    assert.throws(() => normalizeFile(malformed, 'src/choose.ts', source), /counter/);
    assert.equal(sourcePath('C:\\checkout\\src\\choose.ts', 'C:\\checkout'), 'src/choose.ts');
    assert.throws(() => sourcePath('src/../choose.ts', '/checkout'), /Unexpected/);
});

test('frontend aggregation rejects missing, duplicate, stale and incomplete artifacts', () => {
    const identity = { commit: 'current', run: '1', attempt: '2' };
    const manifests = [1, 2].map((index) => ({
        schema: 1, kind: 'e2e', index, count: 2, complete: true, session: `session-${index}`,
        identity, sha256: 'a'.repeat(64), source_root: '/checkout', seconds: 1
    }));
    validateManifests(manifests, identity, 'e2e', 2);
    for (const invalid of [manifests.slice(1), [manifests[0], manifests[0]]]) {
        assert.throws(() => validateManifests(invalid, identity, 'e2e', 2), /Missing or duplicate/);
    }
    for (const change of [{ complete: false }, { identity: { ...identity, commit: 'stale' } }, { seconds: null }, { sha256: 'bad' }]) {
        assert.throws(() => validateManifests([{ ...manifests[0], ...change }, manifests[1]], identity, 'e2e', 2), /Malformed, stale/);
    }
});

test('every existing browser test uses the coverage-aware fixture', () => {
    const directory = resolve(import.meta.dirname, '../../src/Exceptionless.Web/ClientApp/e2e/tests');
    for (const name of readdirSync(directory).filter((name) => /\.e2e\.[jt]s$/.test(name))) {
        const text = readFileSync(resolve(directory, name), 'utf8');
        const imports = [...text.matchAll(/import\s+([^;]*?)\s+from\s+['"]@playwright\/test['"]/g)];
        for (const [, names] of imports) {
            assert.doesNotMatch(names, /\btest\b|\*\s+as/, `${name} bypasses the coverage fixture`);
        }
        assert.doesNotMatch(text, /\b(?:chromium|firefox|webkit)\s*\.\s*launch/, `${name} launches an untracked browser`);
    }
});
