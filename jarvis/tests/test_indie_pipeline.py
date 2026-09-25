import contextlib
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from indie_builder import generate, make_levels, validate_layout, verify_project, export_windows
from main import JarvisDaemon
from state_engine import StateEngine


class PipelineTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.quiet = contextlib.redirect_stdout(io.StringIO())
        self.quiet.__enter__()
        self.addCleanup(self.quiet.__exit__, None, None, None)

    def daemon(self):
        state = StateEngine(self.root / "state.json")
        daemon = JarvisDaemon.__new__(JarvisDaemon)
        daemon.state = state
        return daemon

    def test_failure_does_not_starve_healthy_task_and_retries_are_bounded(self):
        daemon = self.daemon()
        daemon.state.add_task("fails", task_id="bad")
        daemon.state.add_task("works", task_id="good")
        seen = []
        def dispatch(task):
            seen.append(task["task_id"])
            return {"ok": task["task_id"] == "good", "error": "test failure"}
        daemon._dispatch = dispatch
        for _ in range(8):
            daemon.process_one_task()
        self.assertEqual(seen, ["bad", "good", "bad", "bad"])
        self.assertEqual(daemon.state.task_counts()["failed"], 1)
        self.assertEqual(daemon.state.task_counts()["completed"], 1)

    def test_failure_without_error_message_is_not_completed(self):
        daemon = self.daemon()
        daemon.state.add_task("fails", task_id="bad")
        daemon._dispatch = lambda _: {"ok": False}
        daemon.process_one_task()
        self.assertEqual(daemon.state.task_counts()["completed"], 0)
        self.assertEqual(daemon.state.task_counts()["pending_fix"], 1)

    def test_skipped_task_is_not_claimed_as_completed(self):
        daemon = self.daemon()
        daemon.state.add_task("skip", task_id="skip")
        daemon._dispatch = lambda _: {"ok": True, "skipped": True}
        daemon.process_one_task()
        self.assertEqual(daemon.state.task_counts()["skipped"], 1)
        self.assertEqual(daemon.state.task_counts()["completed"], 0)

    def test_restart_preserves_completed_work_and_retry_count(self):
        daemon = self.daemon()
        daemon.state.add_task("done", task_id="done")
        daemon.state.mark_completed("done")
        daemon.state.add_task("interrupted", task_id="running")
        daemon.state.mark_running("running")
        resumed = StateEngine(self.root / "state.json")
        self.assertEqual(resumed.task_counts()["completed"], 1)
        self.assertEqual(resumed.get_next_pending_task()["task_id"], "running")
        self.assertEqual(resumed.get_next_pending_task()["attempts"], 1)

    def test_seeded_layouts_are_reproducible_and_conservatively_reachable(self):
        self.assertEqual(make_levels(417), make_levels(417))
        self.assertNotEqual(make_levels(417), make_levels(418))
        for seed in range(100):
            validate_layout(make_levels(seed))

    def test_layout_checker_rejects_unreachable_gap(self):
        levels = make_levels(417)
        levels[0]["platforms"][1][0] += 500
        with self.assertRaises(ValueError):
            validate_layout(levels)

    def test_generation_preserves_existing_output(self):
        output = self.root / "existing"
        output.mkdir()
        (output / "important.txt").write_text("keep")
        with self.assertRaises(ValueError):
            generate(output)
        self.assertEqual((output / "important.txt").read_text(), "keep")

    def test_missing_engine_blocks_readiness(self):
        report = verify_project(self.root, None)
        self.assertFalse(report["passed"])
        self.assertEqual(report["status"], "blocked")
        with self.assertRaises(ValueError):
            export_windows(self.root, "missing", report)

    def test_zero_exit_without_gameplay_report_is_rejected(self):
        with patch("indie_builder.run_engine", return_value={"name":"mock", "passed":True,"details":""}):
            report = verify_project(self.root, "mock")
        self.assertFalse(report["passed"])

    def test_real_engine_error_is_not_hidden_by_zero_exit(self):
        from indie_builder import run_engine
        from types import SimpleNamespace
        with patch("indie_builder.subprocess.run", return_value=SimpleNamespace(returncode=0, stdout="SCRIPT ERROR: failed", stderr="")):
            result = run_engine("mock", self.root, [], "probe")
        self.assertFalse(result["passed"])

    def test_export_that_cannot_start_does_not_enable_play(self):
        (self.root / "game_data.json").write_text('{"title":"Test"}')
        build = self.root / "build"
        build.mkdir()
        with (build / "Test.exe").open("wb") as executable:
            executable.truncate(1000001)
        report = {"passed": True}
        with patch("indie_builder.run_engine", side_effect=[{"passed":True}, {"passed":False}]) as engine:
            result = export_windows(self.root, "godot", report)
        self.assertFalse(result["passed"])
        self.assertNotIn("executable", result)
        self.assertTrue(engine.call_args.kwargs["standalone"])

    def test_standalone_smoke_uses_embedded_game_without_source_path(self):
        from indie_builder import run_engine
        from types import SimpleNamespace
        with patch("indie_builder.subprocess.run", return_value=SimpleNamespace(returncode=0, stdout="", stderr="")) as process:
            result = run_engine("game.exe", self.root, ["--quit-after","30"], "export_startup", standalone=True)
        self.assertTrue(result["passed"])
        self.assertNotIn("--path", process.call_args.args[0])
        self.assertEqual(process.call_args.kwargs["cwd"], str(self.root / "build"))


if __name__ == "__main__":
    unittest.main()
