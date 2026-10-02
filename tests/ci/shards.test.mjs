import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { partition, report } from './backend-shards.mjs';
import { validate } from './browser-report.mjs';
import { inventory, plan, verifySelection } from './browser-shards.mjs';

test('partition covers new classes once and is deterministic', () => {
    const classes = ['slow', 'medium', 'fast', 'new', 'another-new'];
    const timings = { slow: 90, medium: 60, fast: 30, deleted: 999 };
    const shards = partition(classes, timings, 3);
    assert.deepEqual(shards.flat().sort(), [...classes].sort());
    assert.deepEqual(shards, partition([...classes].reverse(), timings, 3));
    assert.equal(new Set(['slow', 'medium', 'fast'].map((name) => shards.findIndex((shard) => shard.includes(name)))).size, 3);
    for (const count of [0, 6, 1.5]) {
        assert.throws(() => partition(classes, timings, count));
    }
});

test('backend gate rejects incomplete or inconsistent shards and reads xUnit JSON timings', () => {
    for (const defect of [
        null,
        'missing',
        'duplicate',
        'discovery',
        'omitted',
        'execution',
        'coverage',
        'trx',
        'failed',
        'skipped',
        'pending',
        'global-error',
        'empty',
        'invalid-duration'
    ]) {
        const directory = mkdtempSync(join(tmpdir(), 'exceptionless-ci-'));
        try {
            for (const index of [1, 2]) {
                const shard = join(directory, String(index));
                mkdirSync(shard);
                let name = `Example.Test${index}`;
                const manifest = { index, count: 2, discovery_hash: 'same', discovered_count: 2, classes: [name] };
                if (index === 2) {
                    if (defect === 'missing') continue;
                    if (defect === 'duplicate') manifest.index = 1;
                    if (defect === 'discovery') manifest.discovery_hash = 'different';
                    if (defect === 'omitted') manifest.classes = [];
                    if (defect === 'execution') name = 'Unexpected.Test';
                }
                writeFileSync(join(shard, 'manifest.json'), JSON.stringify(manifest));
                if (defect !== 'coverage' || index !== 2) writeFileSync(join(shard, 'backend.coverage'), 'coverage fixture');
                if (defect !== 'trx' || index !== 2) writeFileSync(join(shard, 'test-results.trx'), '<TestRun />');
                const status = index === 2 && ['failed', 'skipped', 'pending'].includes(defect) ? defect : 'passed';
                const result = {
                    reportFormat: 'CTRF',
                    results: {
                        tests: [{ name: `${name}.Case("<value> & \\"quoted\\"")`, status, duration: 1500, extra: { type: name } }],
                        extra: { suites: [{ errors: [] }] }
                    }
                };
                if (index === 2) {
                    if (defect === 'global-error') result.results.extra.suites[0].errors.push({ message: 'Fixture failed' });
                    if (defect === 'empty') result.results.tests = [];
                    if (defect === 'invalid-duration') result.results.tests[0].duration = 'invalid';
                }
                writeFileSync(join(shard, 'test-results.json'), JSON.stringify(result));
            }
            const args = { directory, count: 2, coverage: true };
            if ([null, 'failed', 'skipped', 'pending'].includes(defect)) {
                const result = report(args);
                assert.equal(result.exitCode, Number(['failed', 'pending'].includes(defect)), defect);
                assert.deepEqual(result.durations, { 'Example.Test1': 1.5, 'Example.Test2': 1.5 });
            } else {
                assert.throws(() => report(args), undefined, defect);
            }
        } finally {
            rmSync(directory, { recursive: true, force: true });
        }
    }
});

test('browser gate checks discovery, results, and retries', () => {
    const browserTest = { projectName: 'chromium', status: 'expected', results: [{ duration: 500 }] };
    const expected = { suites: [{ specs: [{ id: 'case', title: 'browser behavior', tests: [browserTest] }] }] };
    for (const defect of [null, 'missing', 'duplicate', 'skipped', 'unexpected', 'no-result', 'global-error', 'flaky']) {
        const actual = structuredClone(expected);
        const spec = actual.suites[0].specs[0];
        if (defect === 'missing') actual.suites = [];
        else if (defect === 'duplicate') actual.suites.push(structuredClone(actual.suites[0]));
        else if (['skipped', 'unexpected', 'flaky'].includes(defect)) spec.tests[0].status = defect;
        else if (defect === 'no-result') spec.tests[0].results = [];
        else if (defect === 'global-error') actual.errors = [{ message: 'worker crashed' }];
        if (defect === null) validate(expected, actual);
        else assert.throws(() => validate(expected, actual), undefined, defect);
    }
});

test('browser duration balancing includes new tests exactly once and validates native selection', () => {
    const spec = (id) => ({ id, title: id, file: 'tests/example.e2e.ts', tests: [{ projectName: 'chromium' }] });
    const discovered = { suites: [{ title: 'tests/example.e2e.ts', suites: [{ title: 'journey', specs: ['slow', 'medium', 'fast', 'new'].map(spec) }] }] };
    const key = (id) => JSON.stringify([id, 'chromium']);
    const durations = { [key('slow')]: 60, [key('medium')]: 40, [key('fast')]: 20, deleted: 900 };
    const shards = plan(discovered, durations, 2);
    assert.deepEqual(
        shards.flatMap((shard) => shard.tests.map((test) => test.key)).sort(),
        inventory(discovered)
            .map((test) => test.key)
            .sort()
    );
    assert.ok(shards.every((shard) => shard.tests.length > 0));
    assert.equal(inventory(discovered)[0].selector, '[chromium] › tests/example.e2e.ts › journey › slow');
    const reversed = structuredClone(discovered);
    reversed.suites[0].suites[0].specs.reverse();
    assert.deepEqual(plan(reversed, durations, 2), shards);
    const selected = shards[0].tests;
    const report = { suites: [{ specs: selected.map((test) => spec(JSON.parse(test.key)[0])) }] };
    verifySelection(selected, report);
    for (const ids of [[], ['slow', 'slow'], ['unexpected']]) {
        assert.throws(() => verifySelection(selected, { suites: [{ specs: ids.map(spec) }] }));
    }
    assert.throws(() => inventory({ ...discovered, errors: [{ message: 'load failed' }] }));
    assert.throws(() => plan(discovered, durations, 5));
});
