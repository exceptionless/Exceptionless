"""Run complete xUnit classes on balanced shards, using discovery as the source of truth."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
TIMINGS = Path(__file__).with_name("backend-durations.json")


def partition(classes, durations, count):
    if count < 1 or len(classes) < count or len(set(classes)) != len(classes):
        raise ValueError("Shards must be nonempty and discovered classes must be unique")
    shards = [[] for _ in range(count)]
    totals = [0.0] * count
    # Unknown/new classes are always included. Timings influence balance, never membership.
    for name in sorted(classes, key=lambda name: (-durations.get(name, 1.0), name)):
        index = min(range(count), key=lambda i: (totals[i], len(shards[i]), i))
        shards[index].append(name)
        totals[index] += max(durations.get(name, 1.0), 0.01)
    return [sorted(shard) for shard in shards]


def read_results(path):
    root = ET.parse(path).getroot()
    methods = {
        test.attrib["id"]: test.find("{*}TestMethod").attrib["className"]
        for test in root.findall(".//{*}TestDefinitions/{*}UnitTest")
    }
    results = []
    for result in root.findall(".//{*}UnitTestResult"):
        hours, minutes, seconds = result.get("duration", "0:0:0").split(":")
        results.append({
            "class": methods[result.attrib["testId"]],
            "name": result.attrib["testName"],
            "seconds": int(hours) * 3600 + int(minutes) * 60 + float(seconds),
            "outcome": result.attrib["outcome"],
        })
    if not results:
        raise ValueError(f"No test results in {path}")
    return results


def summarize(results):
    durations = {}
    for result in results:
        name = result["class"]
        durations[name] = durations.get(name, 0) + result["seconds"]
    return {name: round(seconds, 3) for name, seconds in sorted(durations.items())}


def run(args):
    assembly = Path(args.assembly).resolve()
    discovery = subprocess.run(
        ["dotnet", str(assembly), "--list-tests", "json", "--no-ansi"],
        check=True, capture_output=True, text=True,
    )
    discovered = json.loads(discovery.stdout)
    if discovered["schemaVersion"] != 1:
        raise ValueError("Unsupported Microsoft Testing Platform discovery schema")
    classes = sorted({f"{test['type']['namespace']}.{test['type']['typeName']}" for test in discovered["tests"]})
    if not classes:
        raise ValueError("No test classes discovered")
    durations = json.loads(TIMINGS.read_text())
    shards = partition(classes, durations, args.count)
    if not 1 <= args.index <= args.count:
        raise ValueError("Shard index must be between 1 and shard count")
    selected = shards[args.index - 1]
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    manifest = {
        "index": args.index,
        "count": args.count,
        "discovery_hash": hashlib.sha256("\n".join(classes).encode()).hexdigest(),
        "discovered_count": len(classes),
        "classes": selected,
    }
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"Shard {args.index}/{args.count}: {len(selected)} of {len(classes)} classes", flush=True)
    command = [
        "dotnet", str(assembly), "--results-directory", str(output),
        "--report-xunit-trx", "--report-xunit-trx-filename", "test-results.trx",
        "--minimum-expected-tests", "1", "--filter-class", *selected,
    ]
    if args.coverage:
        command += [
            "--coverage", "--coverage-settings", str(ROOT / "tests/CodeCoverage.config"),
            "--coverage-output", "coverage.cobertura.xml", "--coverage-output-format", "cobertura",
        ]
    if os.environ.get("GITHUB_ACTIONS"):
        command.append("--report-github")
    result = subprocess.run(command, env={**os.environ, "ASPNETCORE_ENVIRONMENT": "Development"})
    return result.returncode


def report(args):
    directory = Path(args.directory)
    paths = sorted(directory.glob("**/test-results.trx"))
    if args.count:
        manifests = [json.loads(path.read_text()) for path in sorted(directory.glob("**/manifest.json"))]
        if len(paths) != args.count or sorted(m["index"] for m in manifests) != list(range(1, args.count + 1)):
            raise ValueError("Missing or duplicate shard results/manifests")
        if len({m["discovery_hash"] for m in manifests}) != 1 or any(m["count"] != args.count for m in manifests):
            raise ValueError("Shards used different test discovery or shard counts")
        selected = [name for m in manifests for name in m["classes"]]
        if len(selected) != len(set(selected)) or len(selected) != manifests[0]["discovered_count"]:
            raise ValueError("Shard plan duplicated or omitted discovered classes")
        for path in paths:
            manifest = json.loads(path.with_name("manifest.json").read_text())
            actual = {result["class"] for result in read_results(path)}
            if actual != set(manifest["classes"]):
                raise ValueError(f"Executed classes differ from shard plan: {path}")
            if args.coverage and not path.with_name("coverage.cobertura.xml").is_file():
                raise ValueError(f"Missing shard coverage: {path}")
    results = [result for path in paths for result in read_results(path)]
    if not results:
        raise ValueError("No test reports found")
    durations = summarize(results)
    if args.write_timings:
        Path(args.write_timings).write_text(json.dumps(durations, indent=2) + "\n")
    lines = ["### .NET test timings", "", f"{len(results)} results across {len(durations)} classes.", "",
             "| Slowest class | Total test seconds |", "| --- | ---: |"]
    for name, seconds in sorted(durations.items(), key=lambda item: item[1], reverse=True)[:15]:
        lines.append(f"| {name} | {seconds:.2f} |")
    lines += ["", "| Slowest test | Seconds |", "| --- | ---: |"]
    for result in sorted(results, key=lambda result: result["seconds"], reverse=True)[:15]:
        name = result["name"].replace("|", "\\|").replace("\n", " ")
        lines.append(f"| {name} | {result['seconds']:.2f} |")
    summary = "\n".join(lines) + "\n"
    print(summary)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as stream:
            stream.write(summary)
    return int(any(result["outcome"] not in ("Passed", "NotExecuted") for result in results))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    runner = commands.add_parser("run")
    runner.add_argument("--assembly", default="tests/Exceptionless.Tests/bin/Release/net10.0/Exceptionless.Tests.dll")
    runner.add_argument("--index", type=int, required=True)
    runner.add_argument("--count", type=int, required=True)
    runner.add_argument("--output", required=True)
    runner.add_argument("--coverage", action="store_true")
    runner.set_defaults(func=run)
    reporter = commands.add_parser("report")
    reporter.add_argument("directory")
    reporter.add_argument("--count", type=int)
    reporter.add_argument("--coverage", action="store_true")
    reporter.add_argument("--write-timings")
    reporter.set_defaults(func=report)
    args = parser.parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
