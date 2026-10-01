"""Verify merged Playwright results against discovery and publish slow tests/retries."""

import argparse
import json
import os
from pathlib import Path


def cases(report):
    def walk(suites):
        for suite in suites:
            for spec in suite.get("specs", []):
                for test in spec["tests"]:
                    yield (spec["id"], test["projectName"]), spec, test
            yield from walk(suite.get("suites", []))
    return list(walk(report["suites"]))


def validate(expected, actual):
    planned = {key for key, _, _ in cases(expected)}
    executed = [key for key, _, _ in cases(actual)]
    if not planned or len(executed) != len(set(executed)) or set(executed) != planned:
        raise ValueError("Merged browser results omitted or duplicated discovered tests")
    if actual.get("errors"):
        raise ValueError("Playwright reported errors outside individual tests")
    for _, spec, test in cases(actual):
        if not test["results"] or test["status"] not in ("expected", "flaky"):
            raise ValueError(f"Browser test did not pass: {spec['title']} ({test['status']})")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("discovery")
    parser.add_argument("results")
    args = parser.parse_args()
    expected = json.loads(Path(args.discovery).read_text())
    actual = json.loads(Path(args.results).read_text())
    tests = cases(actual)
    rows = sorted(tests, key=lambda item: sum(r["duration"] for r in item[2]["results"]), reverse=True)
    retries = sum(max(len(test["results"]) - 1, 0) for _, _, test in tests)
    lines = ["### Browser test timings", "", f"{len(tests)} tests; {retries} retries.", "",
             "| Slowest test (including retries) | Seconds | Attempts |", "| --- | ---: | ---: |"]
    for _, spec, test in rows[:20]:
        seconds = sum(result["duration"] for result in test["results"]) / 1000
        name = f"{spec['file']}: {spec['title']}".replace("|", "\\|")
        lines.append(f"| {name} | {seconds:.2f} | {len(test['results'])} |")
    summary = "\n".join(lines) + "\n"
    print(summary)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as stream:
            stream.write(summary)
    validate(expected, actual)


if __name__ == "__main__":
    main()
