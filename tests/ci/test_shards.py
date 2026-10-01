import argparse
from contextlib import redirect_stdout
import copy
import io
import json
from pathlib import Path
import tempfile
import unittest

from backend_shards import partition, report
from browser_report import validate


class ShardTests(unittest.TestCase):
    def test_partition_covers_new_classes_once_and_is_deterministic(self):
        classes = ["slow", "medium", "fast", "new", "another-new"]
        timings = {"slow": 90, "medium": 60, "fast": 30, "deleted": 999}
        shards = partition(classes, timings, 3)
        self.assertEqual(sorted(classes), sorted(name for shard in shards for name in shard))
        self.assertEqual(shards, partition(list(reversed(classes)), timings, 3))
        self.assertEqual(3, len({next(i for i, shard in enumerate(shards) if name in shard) for name in ("slow", "medium", "fast")}))
        for count in (0, 6):
            with self.subTest(count=count), self.assertRaises(ValueError):
                partition(classes, timings, count)

    def test_backend_gate_rejects_incomplete_or_inconsistent_shards(self):
        for defect in (None, "missing", "duplicate", "discovery", "omitted", "execution", "coverage", "failed"):
            with self.subTest(defect=defect), tempfile.TemporaryDirectory() as temp:
                directory = Path(temp)
                for index in (1, 2):
                    shard = directory / str(index)
                    shard.mkdir()
                    name = f"Example.Test{index}"
                    manifest = {"index": index, "count": 2, "discovery_hash": "same", "discovered_count": 2, "classes": [name]}
                    if index == 2:
                        if defect == "missing":
                            continue
                        if defect == "duplicate":
                            manifest["index"] = 1
                        if defect == "discovery":
                            manifest["discovery_hash"] = "different"
                        if defect == "omitted":
                            manifest["classes"] = []
                        if defect == "execution":
                            name = "Unexpected.Test"
                    (shard / "manifest.json").write_text(json.dumps(manifest))
                    if defect != "coverage" or index != 2:
                        (shard / "coverage.cobertura.xml").write_text("<coverage />")
                    outcome = "Failed" if defect == "failed" and index == 2 else "Passed"
                    (shard / "test-results.trx").write_text(f'''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                        <TestDefinitions><UnitTest id="test"><TestMethod className="{name}" /></UnitTest></TestDefinitions>
                        <Results><UnitTestResult testId="test" testName="{name}.Case" duration="00:00:01.5" outcome="{outcome}" /></Results>
                        </TestRun>''')
                args = argparse.Namespace(directory=temp, count=2, coverage=True, write_timings=None)
                with redirect_stdout(io.StringIO()):
                    if defect in (None, "failed"):
                        self.assertEqual(int(defect == "failed"), report(args))
                    else:
                        with self.assertRaises(ValueError):
                            report(args)

    def test_browser_gate_checks_discovery_results_and_retries(self):
        test = {"projectName": "chromium", "status": "expected", "results": [{"duration": 500}]}
        expected = {"suites": [{"specs": [{"id": "case", "title": "browser behavior", "tests": [test]}]}]}
        for defect in (None, "missing", "duplicate", "skipped", "unexpected", "no-result", "global-error", "flaky"):
            with self.subTest(defect=defect):
                actual = copy.deepcopy(expected)
                spec = actual["suites"][0]["specs"][0]
                if defect == "missing":
                    actual["suites"] = []
                elif defect == "duplicate":
                    actual["suites"].append(copy.deepcopy(actual["suites"][0]))
                elif defect in ("skipped", "unexpected", "flaky"):
                    spec["tests"][0]["status"] = defect
                elif defect == "no-result":
                    spec["tests"][0]["results"] = []
                elif defect == "global-error":
                    actual["errors"] = [{"message": "worker crashed"}]
                if defect in (None, "flaky"):
                    validate(expected, actual)
                else:
                    with self.assertRaises(ValueError):
                        validate(expected, actual)


if __name__ == "__main__":
    unittest.main()
