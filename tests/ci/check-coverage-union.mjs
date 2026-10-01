// Real collector contract check: two complementary executions must merge to the union.
// Keep this outside product tests; run once in the final CI aggregate.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { assertUnion, readNative } from './backend-coverage.mjs';

const root = resolve(import.meta.dirname, '../..');
const directory = mkdtempSync(join(tmpdir(), 'coverage-union-'));
const run = (args) => execFileSync('dotnet', args, { cwd: root, stdio: 'pipe', timeout: 60_000 });
const coverage = (args) => run(['tool', 'run', 'dotnet-coverage', '--', ...args]);
try {
    mkdirSync(join(directory, 'src'));
    writeFileSync(
        join(directory, 'src/Probe.csproj'),
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>'
    );
    writeFileSync(
        join(directory, 'src/Program.cs'),
        'if (args.Length == 0)\n    System.Console.WriteLine("left");\nelse\n    System.Console.WriteLine("right");\n'
    );
    run(['build', join(directory, 'src/Probe.csproj'), '--configuration', 'Release']);
    for (const [name, args] of [
        ['left', []],
        ['right', ['right']]
    ]) {
        coverage([
            'collect',
            '--output-format',
            'coverage',
            '--output',
            join(directory, `${name}.coverage`),
            'dotnet',
            join(directory, 'src/bin/Release/net10.0/Probe.dll'),
            ...args
        ]);
    }
    coverage([
        'merge',
        join(directory, 'left.coverage'),
        join(directory, 'right.coverage'),
        '--output-format',
        'coverage',
        '--output',
        join(directory, 'union.coverage')
    ]);
    const reports = ['left', 'right', 'union'].map((name) => {
        const xml = join(directory, `${name}.xml`);
        coverage(['merge', join(directory, `${name}.coverage`), '--output-format', 'xml', '--output', xml]);
        return readNative(readFileSync(xml, 'utf8'), directory);
    });
    assertUnion(reports.slice(0, 2), reports[2]);
    assert.equal([...reports[0].lines.values()].filter(Boolean).length, 2);
    assert.equal([...reports[1].lines.values()].filter(Boolean).length, 2);
    assert.equal([...reports[2].lines.values()].filter(Boolean).length, 3);
    const merged = reports[2].modules[0];
    assert.equal(merged.covered, merged.total);
    for (const input of reports.slice(0, 2)) assert.ok(input.modules[0].covered < merged.covered);
    console.log('Native collector union verified: complementary line and IL block hits preserved.');
} finally {
    rmSync(directory, { recursive: true, force: true });
}
