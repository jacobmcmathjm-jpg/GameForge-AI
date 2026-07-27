"""
state_engine.py — Strict local JSON tracking and task persistence.

project_state.json is the system's long-term memory. Crash, reboot, or
network loss must never lose progress: every status change is flushed to
disk immediately so `python main.py` resumes exactly where it left off.
"""

from __future__ import annotations

import json
import os
import threading
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
FRAMEWORK_ROOT = Path(__file__).resolve().parent
STATE_FILE = FRAMEWORK_ROOT / "project_state.json"
STATE_BACKUP = FRAMEWORK_ROOT / "project_state.backup.json"

VALID_STATUSES = ("pending", "running", "completed", "pending_fix", "failed", "skipped")


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _console(tag: str, message: str) -> None:
    """Deep console printing for active system focus."""
    stamp = datetime.now().strftime("%H:%M:%S")
    print(f"[{stamp}] [{tag}] {message}", flush=True)


class StateEngine:
    """
    Thread-safe manager for project_state.json.

    Tracked fields:
      - Master_Goal
      - Current_Milestone_Index
      - Task_Backlog  [{task_id, description, status, logs}, ...]
      - MASTER_APPROVAL
      - milestones, assets, installers, meta
    """

    def __init__(self, state_path: Optional[Path] = None) -> None:
        self.state_path = Path(state_path) if state_path else STATE_FILE
        self.backup_path = self.state_path.with_suffix(".backup.json")
        self._lock = threading.RLock()
        self._state: Dict[str, Any] = {}
        self.load()

    # ------------------------------------------------------------------
    # Default / schema
    # ------------------------------------------------------------------
    @staticmethod
    def default_state(master_goal: str = "") -> Dict[str, Any]:
        return {
            "Master_Goal": master_goal or "Build a complete Godot 4 game autonomously",
            "Current_Milestone_Index": 0,
            "Task_Backlog": [],
            "MASTER_APPROVAL": False,
            "approved_installers": [],
            "installed_tools": [],
            "milestones": [
                "Bootstrap project structure",
                "Harvest open-source assets",
                "Generate core game scripts",
                "Build scene trees",
                "Validate and iterate",
            ],
            "harvested_assets": [],
            "godot_project_path": str(FRAMEWORK_ROOT / "godot_project"),
            "assets_path": str(FRAMEWORK_ROOT / "res" / "assets"),
            "meta": {
                "created_at": _utc_now(),
                "updated_at": _utc_now(),
                "last_resume_at": None,
                "daemon_cycles": 0,
                "framework_version": "1.0.0",
            },
        }

    # ------------------------------------------------------------------
    # Load / Save
    # ------------------------------------------------------------------
    def load(self) -> Dict[str, Any]:
        with self._lock:
            if self.state_path.exists():
                try:
                    with open(self.state_path, "r", encoding="utf-8") as fh:
                        self._state = json.load(fh)
                    self._normalize()
                    # Any task left 'running' after a crash becomes pending again
                    recovered = 0
                    for task in self._state.get("Task_Backlog", []):
                        if task.get("status") == "running":
                            task["status"] = "pending"
                            task.setdefault("logs", []).append(
                                f"[{_utc_now()}] Recovered from interrupted 'running' state after restart."
                            )
                            recovered += 1
                    if recovered:
                        _console(
                            "STATE",
                            f"Recovered {recovered} interrupted task(s) → pending (no duplicate work).",
                        )
                    self._state["meta"]["last_resume_at"] = _utc_now()
                    self._write_disk()
                    _console(
                        "STATE",
                        f"Loaded project_state.json — "
                        f"milestone {self._state['Current_Milestone_Index']}, "
                        f"{self.task_counts_summary()}",
                    )
                    return self._state
                except (json.JSONDecodeError, OSError) as exc:
                    _console("STATE", f"Corrupt state file ({exc}); attempting backup restore…")
                    if self.backup_path.exists():
                        try:
                            with open(self.backup_path, "r", encoding="utf-8") as fh:
                                self._state = json.load(fh)
                            self._normalize()
                            self._write_disk()
                            _console("STATE", "Restored from project_state.backup.json")
                            return self._state
                        except Exception as backup_exc:
                            _console("STATE", f"Backup restore failed: {backup_exc}")
                    _console("STATE", "Initializing fresh state.")
            self._state = self.default_state()
            self._write_disk()
            _console("STATE", f"Created new project_state.json at {self.state_path}")
            return self._state

    def _normalize(self) -> None:
        defaults = self.default_state()
        for key, value in defaults.items():
            if key not in self._state:
                self._state[key] = value
        if "meta" not in self._state or not isinstance(self._state["meta"], dict):
            self._state["meta"] = defaults["meta"]
        for mk, mv in defaults["meta"].items():
            self._state["meta"].setdefault(mk, mv)
        for task in self._state.get("Task_Backlog", []):
            task.setdefault("task_id", str(uuid.uuid4())[:8])
            task.setdefault("description", "")
            task.setdefault("status", "pending")
            task.setdefault("logs", [])
            task.setdefault("type", "generic")
            task.setdefault("payload", {})

    def save(self) -> None:
        """Flush state to disk instantly after every task transition."""
        with self._lock:
            self._state["meta"]["updated_at"] = _utc_now()
            self._write_disk()

    def _write_disk(self) -> None:
        self.state_path.parent.mkdir(parents=True, exist_ok=True)
        payload = json.dumps(self._state, indent=2, ensure_ascii=False)
        # Atomic-ish write: temp → replace, plus backup of previous good file
        tmp = self.state_path.with_suffix(".tmp")
        with open(tmp, "w", encoding="utf-8") as fh:
            fh.write(payload)
            fh.flush()
            os.fsync(fh.fileno())
        if self.state_path.exists():
            try:
                with open(self.state_path, "r", encoding="utf-8") as fh:
                    prev = fh.read()
                with open(self.backup_path, "w", encoding="utf-8") as fh:
                    fh.write(prev)
            except OSError:
                pass
        os.replace(tmp, self.state_path)

    # ------------------------------------------------------------------
    # Accessors
    # ------------------------------------------------------------------
    @property
    def state(self) -> Dict[str, Any]:
        with self._lock:
            return self._state

    def get(self, key: str, default: Any = None) -> Any:
        with self._lock:
            return self._state.get(key, default)

    def set_master_goal(self, goal: str) -> None:
        with self._lock:
            self._state["Master_Goal"] = goal
            self.save()
            _console("STATE", f"Master_Goal set → {goal}")

    def set_master_approval(self, approved: bool) -> None:
        with self._lock:
            self._state["MASTER_APPROVAL"] = bool(approved)
            self.save()
            _console(
                "STATE",
                f"MASTER_APPROVAL = {self._state['MASTER_APPROVAL']}",
            )

    def master_approval(self) -> bool:
        return bool(self.get("MASTER_APPROVAL", False))

    # ------------------------------------------------------------------
    # Task backlog
    # ------------------------------------------------------------------
    def add_task(
        self,
        description: str,
        task_type: str = "generic",
        payload: Optional[Dict[str, Any]] = None,
        task_id: Optional[str] = None,
        status: str = "pending",
    ) -> str:
        with self._lock:
            tid = task_id or str(uuid.uuid4())[:8]
            # Deduplicate by task_id
            for existing in self._state["Task_Backlog"]:
                if existing["task_id"] == tid:
                    _console("STATE", f"Task {tid} already exists — skipping duplicate.")
                    return tid
            task = {
                "task_id": tid,
                "description": description,
                "status": status if status in VALID_STATUSES else "pending",
                "type": task_type,
                "payload": payload or {},
                "logs": [f"[{_utc_now()}] Task enqueued."],
                "created_at": _utc_now(),
                "updated_at": _utc_now(),
            }
            self._state["Task_Backlog"].append(task)
            self.save()
            _console("STATE", f"Enqueued task {tid}: {description}")
            return tid

    def add_tasks_bulk(self, tasks: List[Dict[str, Any]]) -> List[str]:
        ids: List[str] = []
        for t in tasks:
            ids.append(
                self.add_task(
                    description=t.get("description", "Untitled task"),
                    task_type=t.get("type", "generic"),
                    payload=t.get("payload"),
                    task_id=t.get("task_id"),
                    status=t.get("status", "pending"),
                )
            )
        return ids

    def get_next_pending_task(self) -> Optional[Dict[str, Any]]:
        """
        Return exactly one next actionable task.
        Prefer 'pending_fix', then 'pending'. Never return completed/running/failed/skipped.
        """
        with self._lock:
            backlog = self._state.get("Task_Backlog", [])
            for status in ("pending_fix", "pending"):
                for task in backlog:
                    if task.get("status") == status:
                        return dict(task)
            return None

    def mark_running(self, task_id: str) -> None:
        self._set_status(task_id, "running", log=f"Started execution.")

    def mark_completed(self, task_id: str, log: str = "Completed successfully.") -> None:
        self._set_status(task_id, "completed", log=log)

    def mark_pending_fix(self, task_id: str, log: str) -> None:
        self._set_status(task_id, "pending_fix", log=log)

    def mark_failed(self, task_id: str, log: str) -> None:
        self._set_status(task_id, "failed", log=log)

    def mark_pending(self, task_id: str, log: str = "Re-queued as pending.") -> None:
        self._set_status(task_id, "pending", log=log)

    def append_log(self, task_id: str, message: str) -> None:
        with self._lock:
            for task in self._state.get("Task_Backlog", []):
                if task["task_id"] == task_id:
                    task.setdefault("logs", []).append(f"[{_utc_now()}] {message}")
                    task["updated_at"] = _utc_now()
                    self.save()
                    return

    def _set_status(self, task_id: str, status: str, log: str = "") -> None:
        with self._lock:
            for task in self._state.get("Task_Backlog", []):
                if task["task_id"] == task_id:
                    task["status"] = status
                    task["updated_at"] = _utc_now()
                    if log:
                        task.setdefault("logs", []).append(f"[{_utc_now()}] {log}")
                    self.save()
                    _console("STATE", f"Task {task_id} → {status}")
                    return
            _console("STATE", f"WARNING: task_id {task_id} not found when setting {status}")

    def task_counts(self) -> Dict[str, int]:
        with self._lock:
            counts: Dict[str, int] = {s: 0 for s in VALID_STATUSES}
            for task in self._state.get("Task_Backlog", []):
                st = task.get("status", "pending")
                counts[st] = counts.get(st, 0) + 1
            counts["total"] = len(self._state.get("Task_Backlog", []))
            return counts

    def task_counts_summary(self) -> str:
        c = self.task_counts()
        return (
            f"{c.get('completed', 0)}/{c.get('total', 0)} done | "
            f"pending={c.get('pending', 0)} "
            f"fix={c.get('pending_fix', 0)} "
            f"running={c.get('running', 0)} "
            f"failed={c.get('failed', 0)}"
        )

    def progress_index(self) -> tuple[int, int]:
        """Return (1-based current task ordinal among actionable, total backlog)."""
        with self._lock:
            backlog = self._state.get("Task_Backlog", [])
            total = len(backlog)
            done = sum(1 for t in backlog if t.get("status") in ("completed", "skipped", "failed"))
            return done + 1, total

    def advance_milestone_if_ready(self) -> None:
        with self._lock:
            backlog = self._state.get("Task_Backlog", [])
            actionable = [t for t in backlog if t.get("status") in ("pending", "pending_fix", "running")]
            if not actionable:
                milestones = self._state.get("milestones", [])
                idx = self._state.get("Current_Milestone_Index", 0)
                if idx + 1 < len(milestones):
                    self._state["Current_Milestone_Index"] = idx + 1
                    self.save()
                    _console(
                        "STATE",
                        f"Advanced to milestone {idx + 1}: {milestones[idx + 1]}",
                    )

    def record_asset(self, asset_info: Dict[str, Any]) -> None:
        with self._lock:
            self._state.setdefault("harvested_assets", []).append(asset_info)
            self.save()

    def record_installed_tool(self, tool_name: str) -> None:
        with self._lock:
            tools = self._state.setdefault("installed_tools", [])
            if tool_name not in tools:
                tools.append(tool_name)
            self.save()

    def bump_daemon_cycle(self) -> int:
        with self._lock:
            self._state["meta"]["daemon_cycles"] = int(self._state["meta"].get("daemon_cycles", 0)) + 1
            self.save()
            return self._state["meta"]["daemon_cycles"]

    def dump_summary(self) -> str:
        with self._lock:
            goal = self._state.get("Master_Goal", "")
            idx = self._state.get("Current_Milestone_Index", 0)
            milestones = self._state.get("milestones", [])
            ms = milestones[idx] if idx < len(milestones) else "(complete)"
            return (
                f"Goal: {goal}\n"
                f"Milestone [{idx}]: {ms}\n"
                f"Tasks: {self.task_counts_summary()}\n"
                f"MASTER_APPROVAL: {self._state.get('MASTER_APPROVAL')}\n"
                f"Assets harvested: {len(self._state.get('harvested_assets', []))}"
            )


# Module-level convenience singleton (lazy)
_engine: Optional[StateEngine] = None


def get_engine(state_path: Optional[Path] = None) -> StateEngine:
    global _engine
    if _engine is None:
        _engine = StateEngine(state_path=state_path)
    return _engine


if __name__ == "__main__":
    engine = StateEngine()
    print(engine.dump_summary())
