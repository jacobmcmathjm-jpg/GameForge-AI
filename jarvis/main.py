#!/usr/bin/env python3
"""
main.py — Jarvis autonomous game-generation daemon & CLI.

Orchestrates:
  - Persistent long-running loop (one sub-task at a time)
  - Instant state flush after every task
  - Pre-flight installer approval queue
  - Playwright asset harvesting
  - Godot 4 project building with pending_fix recovery

Resume: running `python main.py` again after crash/reboot continues exactly
where project_state.json left off — no duplicate work.
"""

from __future__ import annotations

import argparse
import signal
import sys
import time
import traceback
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, Optional

# Ensure jarvis/ is on sys.path when launched as a script
ROOT = Path(__file__).resolve().parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from state_engine import StateEngine, get_engine, _console  # noqa: E402
from installer_manager import InstallerManager, run_startup_preflight  # noqa: E402
from crawler_agent import CrawlerAgent  # noqa: E402
from project_builder import ProjectBuilder  # noqa: E402
from ollama_client import OllamaClient  # noqa: E402


BANNER = r"""
     ██╗ █████╗ ██████╗ ██╗   ██╗██╗███████╗
     ██║██╔══██╗██╔══██╗██║   ██║██║██╔════╝
     ██║███████║██████╔╝██║   ██║██║███████╗
██   ██║██╔══██║██╔══██╗╚██╗ ██╔╝██║╚════██║
╚█████╔╝██║  ██║██║  ██║ ╚████╔╝ ██║███████║
 ╚════╝ ╚═╝  ╚═╝╚═╝  ╚═╝  ╚═══╝  ╚═╝╚══════╝
  Autonomous Godot 4 Game Generation Framework
"""

_SHUTDOWN = False


def _handle_signal(signum: int, _frame: Any) -> None:
    global _SHUTDOWN
    _console("DAEMON", f"Signal {signum} received — graceful shutdown after current task…")
    _SHUTDOWN = True


class JarvisDaemon:
    """Long-running, state-persistent execution daemon."""

    def __init__(
        self,
        state: StateEngine,
        ollama: OllamaClient,
        sleep_between_tasks: float = 1.0,
        idle_sleep: float = 15.0,
        skip_harvest: bool = False,
    ) -> None:
        self.state = state
        self.ollama = ollama
        self.sleep_between_tasks = sleep_between_tasks
        self.idle_sleep = idle_sleep
        self.skip_harvest = skip_harvest
        self.builder = ProjectBuilder(state, ollama_client=ollama)
        self.crawler = CrawlerAgent(state)
        self.installer = InstallerManager(state)

    # ------------------------------------------------------------------
    # Planning
    # ------------------------------------------------------------------
    def ensure_backlog(self) -> None:
        backlog = self.state.get("Task_Backlog") or []
        if backlog:
            _console(
                "DAEMON",
                f"Resuming existing backlog ({len(backlog)} tasks) — "
                f"{self.state.task_counts_summary()}",
            )
            return

        goal = self.state.get("Master_Goal", "")
        _console("DAEMON", f"Empty backlog — planning tasks for goal: {goal}")
        plan = self.ollama.plan_tasks(goal, existing_count=0)
        self.state.add_tasks_bulk(plan)
        _console("DAEMON", f"Queued {len(plan)} tasks.")

    # ------------------------------------------------------------------
    # Single-task dispatch
    # ------------------------------------------------------------------
    def process_one_task(self) -> bool:
        """
        Process exactly one sub-task. Returns True if a task was handled,
        False if the backlog is idle.
        """
        task = self.state.get_next_pending_task()
        if not task:
            return False

        task_id = task["task_id"]
        desc = task.get("description", "")
        ordinal, total = self.state.progress_index()
        task_type = task.get("type", "generic")

        _console(
            "SLEEP MODE ACTIVE",
            f"Processing task {ordinal} of {total}: {desc}",
        )
        _console("DAEMON", f"task_id={task_id} type={task_type}")

        # Mark running + flush immediately so a mid-task crash recovers cleanly
        self.state.mark_running(task_id)

        try:
            result = self._dispatch(task)
        except Exception as exc:
            tb = traceback.format_exc()
            _console("DAEMON", f"Task raised unexpectedly (non-fatal): {exc}")
            self.state.mark_pending_fix(task_id, log=f"Exception: {exc}\n{tb}")
            self.state.save()
            return True

        # Interpret result
        if result.get("pending_fix") or (result.get("ok") is False and result.get("error")):
            err = result.get("error") or "Validation / execution failure"
            self.state.mark_pending_fix(task_id, log=str(err))
            if result.get("traceback"):
                self.state.append_log(task_id, result["traceback"][:2000])
            _console(
                "DAEMON",
                f"Task {task_id} marked pending_fix — moving to next item (no crash).",
            )
        elif result.get("ok"):
            msg = result.get("message") or f"OK — files={result.get('files', result.get('count', ''))}"
            self.state.mark_completed(task_id, log=str(msg))
            _console("DAEMON", f"Task {task_id} completed.")
        else:
            self.state.mark_completed(task_id, log=f"Finished with result: {result}")

        # Instant disk flush already happens inside mark_*; call save for belt-and-suspenders
        self.state.save()
        self.state.advance_milestone_if_ready()
        return True

    def _dispatch(self, task: Dict[str, Any]) -> Dict[str, Any]:
        task_type = (task.get("type") or "generic").lower()
        payload = task.get("payload") or {}

        if task_type in ("harvest", "crawl", "assets"):
            if self.skip_harvest:
                _console("DAEMON", "Harvest skipped (--skip-harvest).")
                return {"ok": True, "message": "Harvest skipped by flag.", "count": 0}
            return self.crawler.execute_task(task)

        if task_type in ("install", "installer"):
            names = payload.get("tools") or []
            tools = self.installer.identify_from_llm_list(names) or self.installer.detect_required_tools()
            if not self.state.master_approval():
                return {"ok": False, "error": "MASTER_APPROVAL required for installs", "pending_fix": True}
            installed = self.installer.install_all_approved(tools)
            return {"ok": True, "message": f"Installed: {installed}"}

        # Everything else → project builder (bootstrap / scripts / scenes / systems)
        return self.builder.execute_task(task)

    # ------------------------------------------------------------------
    # Main loop
    # ------------------------------------------------------------------
    def run_forever(self) -> None:
        global _SHUTDOWN
        self.ensure_backlog()
        _console("DAEMON", "Entering unattended main loop (Ctrl+C to stop).")
        _console("DAEMON", "\n" + self.state.dump_summary())

        idle_rounds = 0
        while not _SHUTDOWN:
            cycle = self.state.bump_daemon_cycle()
            handled = self.process_one_task()

            if handled:
                idle_rounds = 0
                time.sleep(self.sleep_between_tasks)
                continue

            # No pending work
            idle_rounds += 1
            counts = self.state.task_counts()
            fixable = counts.get("pending_fix", 0)

            if fixable and idle_rounds == 1:
                _console(
                    "DAEMON",
                    f"{fixable} task(s) in pending_fix — re-queuing oldest for another attempt…",
                )
                self._requeue_one_fix()
                continue

            if counts.get("pending", 0) == 0 and counts.get("pending_fix", 0) == 0:
                _console(
                    "SLEEP MODE ACTIVE",
                    f"All tasks settled ({counts.get('completed', 0)} completed). "
                    f"Idle watch #{idle_rounds} — sleeping {self.idle_sleep:.0f}s. "
                    f"Daemon cycle={cycle}",
                )
                # Soft idle: allow operator to add tasks externally to project_state.json
                self.state.load()
                time.sleep(self.idle_sleep)
            else:
                time.sleep(self.sleep_between_tasks)

        _console("DAEMON", "Daemon stopped cleanly. State persisted.")
        try:
            self.crawler.stop_browser()
        except Exception:
            pass

    def _requeue_one_fix(self) -> None:
        with self.state._lock:
            for task in self.state._state.get("Task_Backlog", []):
                if task.get("status") == "pending_fix":
                    attempts = sum(1 for log in task.get("logs", []) if "Re-queued" in log)
                    if attempts >= 3:
                        self.state.mark_failed(
                            task["task_id"],
                            log="Exceeded pending_fix retry limit (3).",
                        )
                        return
                    self.state.mark_pending(
                        task["task_id"],
                        log=f"Re-queued from pending_fix (attempt {attempts + 1}).",
                    )
                    return

    def run_once(self) -> None:
        """Process a single task then exit (useful for testing)."""
        self.ensure_backlog()
        handled = self.process_one_task()
        if not handled:
            _console("DAEMON", "No pending tasks to process.")
        self.state.save()


# ----------------------------------------------------------------------
# CLI
# ----------------------------------------------------------------------
def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        prog="jarvis",
        description="Jarvis — autonomous, state-persistent Godot 4 game generation daemon.",
    )
    p.add_argument(
        "--goal",
        type=str,
        default=None,
        help="Set / override Master_Goal before running.",
    )
    p.add_argument(
        "--model",
        type=str,
        default="llama3.2",
        help="Ollama model name (default: llama3.2).",
    )
    p.add_argument(
        "--ollama-url",
        type=str,
        default="http://127.0.0.1:11434",
        help="Ollama HTTP base URL.",
    )
    p.add_argument(
        "--yes",
        "-y",
        action="store_true",
        help="Auto-approve Pre-Flight installer queue (non-interactive).",
    )
    p.add_argument(
        "--skip-install",
        action="store_true",
        help="Skip installer pre-flight / downloads.",
    )
    p.add_argument(
        "--skip-harvest",
        action="store_true",
        help="Skip Playwright asset harvesting tasks.",
    )
    p.add_argument(
        "--once",
        action="store_true",
        help="Process exactly one task then exit.",
    )
    p.add_argument(
        "--status",
        action="store_true",
        help="Print project_state.json summary and exit.",
    )
    p.add_argument(
        "--reset-tasks",
        action="store_true",
        help="Clear Task_Backlog (keeps Master_Goal / approval) then plan fresh.",
    )
    p.add_argument(
        "--idle-sleep",
        type=float,
        default=15.0,
        help="Seconds to sleep when backlog is empty (default: 15).",
    )
    p.add_argument(
        "--state-file",
        type=str,
        default=None,
        help="Override path to project_state.json.",
    )
    return p


def main(argv: Optional[list] = None) -> int:
    args = build_parser().parse_args(argv)

    print(BANNER)
    _console("BOOT", f"Jarvis starting @ {datetime.now().isoformat(timespec='seconds')}")
    _console("BOOT", f"Framework root: {ROOT}")
    _console("BOOT", f"Platform: {sys.platform} | Python {sys.version.split()[0]}")

    state_path = Path(args.state_file) if args.state_file else None
    state = StateEngine(state_path=state_path) if state_path else get_engine()

    if args.goal:
        state.set_master_goal(args.goal)

    if args.reset_tasks:
        with state._lock:
            state._state["Task_Backlog"] = []
            state._state["Current_Milestone_Index"] = 0
            state.save()
        _console("BOOT", "Task_Backlog cleared.")

    if args.status:
        print()
        print(state.dump_summary())
        print()
        for t in state.get("Task_Backlog", []):
            print(f"  [{t.get('status'):12}] {t.get('task_id')}: {t.get('description')}")
        return 0

    # ---- Pre-Flight Approval Queue ----
    ollama = OllamaClient(base_url=args.ollama_url, model=args.model)
    if not args.skip_install:
        _console("BOOT", "Running Pre-Flight Approval Queue…")
        # Optionally enrich required-tool list via LLM
        try:
            llm_tools = ollama.identify_required_tools(state.get("Master_Goal", ""))
        except Exception:
            llm_tools = []
        mgr = InstallerManager(state)
        required = mgr.detect_required_tools(extra_names=llm_tools)
        # Also merge any LLM-only identifications
        for t in mgr.identify_from_llm_list(llm_tools):
            if t.name not in {r.name for r in required}:
                required.append(t)
        approved = mgr.run_preflight_approval(required, auto_yes=args.yes)
        if approved and required:
            _console("INSTALLING", "Triggering approved system package installs…")
            mgr.install_all_approved(required)
        if not approved:
            _console(
                "BOOT",
                "Continuing without MASTER_APPROVAL — installs/downloads will be skipped.",
            )
    else:
        _console("BOOT", "Installer pre-flight skipped (--skip-install).")

    # Signal handlers for graceful stop
    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            signal.signal(sig, _handle_signal)
        except Exception:
            pass

    daemon = JarvisDaemon(
        state=state,
        ollama=ollama,
        skip_harvest=args.skip_harvest,
        idle_sleep=args.idle_sleep,
    )

    if args.once:
        daemon.run_once()
    else:
        daemon.run_forever()

    return 0


if __name__ == "__main__":
    sys.exit(main())
