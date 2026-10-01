import assert from 'node:assert/strict';
import { test } from 'node:test';
import { assertUnion, readNative, sourcePath, unionLines, validateManifests, validateSources } from './backend-coverage.mjs';

const expected = { commit: 'revision', run: '12', attempt: '2', collector: '18.11.2', configuration: 'Release', settings: 'settings' };
const manifest = (index) => ({
    schema: 1,
    kind: 'e2e',
    index,
    count: 2,
    complete: true,
    session: `session-${index}`,
    identity: expected,
    source_root: '/checkout',
    sha256: 'a'.repeat(64)
});

test('coverage completeness rejects stale, missing, duplicate and unfinished artifacts', () => {
    for (const defect of [
        null,
        'missing',
        'duplicate',
        'session',
        'commit',
        'run',
        'attempt',
        'settings',
        'configuration',
        'collector',
        'count',
        'incomplete',
        'checksum'
    ]) {
        const manifests = structuredClone([manifest(1), manifest(2)]);
        if (defect === 'missing') manifests.pop();
        else if (defect === 'duplicate') manifests[1].index = 1;
        else if (defect === 'session') manifests[1].session = manifests[0].session;
        else if (Object.hasOwn(expected, defect)) manifests[1].identity[defect] = 'stale';
        else if (defect === 'count') manifests[1].count = 3;
        else if (defect === 'incomplete') manifests[1].complete = false;
        else if (defect === 'checksum') manifests[1].sha256 = 'invalid';
        if (defect === null) validateManifests(manifests, expected, 'e2e', 2);
        else assert.throws(() => validateManifests(manifests, expected, 'e2e', 2), undefined, defect);
    }
});

const xml = (first, second) => `<results><modules>
<module id="id" name="Example.dll" blocks_covered="1" blocks_not_covered="1">
<functions><function><ranges>
<range source_id="1" start_line="10" end_line="11" covered="${first}" />
<range source_id="1" start_line="12" end_line="12" covered="${second}" />
</ranges></function></functions>
<source_files><source_file id="1" path="/checkout/src/Example.cs" checksum_type="SHA256" checksum="${'a'.repeat(64)}" /></source_files>
</module></modules></results>`;

test('native source union preserves complementary hits, uncovered lines and linked-file identity', () => {
    const left = readNative(xml('yes', 'no'), '/checkout');
    const right = readNative(xml('no', 'yes'), '/checkout');
    assert.deepEqual(
        [...unionLines([left, right])],
        [
            ['src/Example.cs:10', true],
            ['src/Example.cs:11', true],
            ['src/Example.cs:12', true]
        ]
    );
    assertUnion([left, right], readNative(xml('yes', 'yes'), '/checkout'));
    assert.throws(() => assertUnion([left, right], left), /not the union/);
    const uncovered = readNative(xml('no', 'no'), '/checkout');
    assert.equal(unionLines([uncovered]).size, 3);
    assert.equal([...unionLines([uncovered]).values()].filter(Boolean).length, 0);
    assert.deepEqual(unionLines([left, left]), left.lines);
    assert.equal(sourcePath('C:\\checkout\\src\\Example.cs', 'C:\\checkout'), 'src/Example.cs');
    for (const path of ['src/../secret.cs', '/other/src/Example.cs', 'src/obj/file.cs']) {
        assert.throws(() => sourcePath(path, '/checkout'));
    }
});

test('native coverage rejects malformed XML, missing source identities and incompatible builds', () => {
    for (const text of [
        '<results/>',
        xml('yes', 'no').replace('</module>', ''),
        xml('invalid', 'no'),
        xml('yes', 'no').replace('source_id="1"', 'source_id="2"'),
        xml('yes', 'no').replace('checksum_type="SHA256"', 'checksum_type="unknown"')
    ])
        assert.throws(() => readNative(text, '/checkout'));
    const left = { modules: [{ name: 'Example.dll', id: 'one', total: 2 }], sources: new Map() };
    const right = { modules: [{ name: 'Example.dll', id: 'two', total: 2 }], sources: new Map() };
    assert.throws(() => validateSources([left, right]), /Incompatible module/);
});
