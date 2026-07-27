"""
ollama_client.py — Thin local Ollama LLM client for task planning & code gen.

Talks to the local Ollama HTTP API (default http://127.0.0.1:11434).
All network failures are soft — the daemon continues with templates.
"""

from __future__ import annotations

import json
import urllib.error
import urllib.request
from typing import Any, Dict, List, Optional

from state_engine import _console


class OllamaClient:
    def __init__(
        self,
        base_url: str = "http://127.0.0.1:11434",
        model: str = "llama3.2",
        timeout: int = 120,
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.model = model
        self.timeout = timeout
        self._available: Optional[bool] = None

    def available(self) -> bool:
        if self._available is not None:
            return self._available
        try:
            req = urllib.request.Request(f"{self.base_url}/api/tags", method="GET")
            with urllib.request.urlopen(req, timeout=5) as resp:
                self._available = resp.status == 200
        except Exception:
            self._available = False
        if self._available:
            _console("OLLAMA", f"Connected to {self.base_url} (model={self.model})")
        else:
            _console("OLLAMA", f"Ollama not reachable at {self.base_url} — using templates.")
        return self._available

    def chat(self, prompt: str, system: Optional[str] = None) -> str:
        if not self.available():
            raise RuntimeError("Ollama is not available")

        payload: Dict[str, Any] = {
            "model": self.model,
            "stream": False,
            "messages": [],
        }
        if system:
            payload["messages"].append({"role": "system", "content": system})
        payload["messages"].append({"role": "user", "content": prompt})

        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            f"{self.base_url}/api/chat",
            data=data,
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as resp:
                body = json.loads(resp.read().decode("utf-8"))
            return body.get("message", {}).get("content", "")
        except urllib.error.HTTPError as exc:
            raise RuntimeError(f"Ollama HTTP {exc.code}: {exc.read()[:200]}") from exc
        except Exception as exc:
            raise RuntimeError(f"Ollama request failed: {exc}") from exc

    def generate_gdscript(self, description: str, class_hint: str = "Generated") -> str:
        system = (
            "You are an expert Godot 4 GDScript engineer. "
            "Reply with ONLY valid GDScript code. No markdown fences, no commentary. "
            "Always start with an 'extends' line. Use Godot 4 syntax (typed vars, :=, etc)."
        )
        prompt = (
            f"Write a complete Godot 4 GDScript file for: {description}\n"
            f"Preferred class_name: {class_hint}\n"
            "Keep it self-contained and compile-clean."
        )
        return self.chat(prompt, system=system)

    def plan_tasks(self, master_goal: str, existing_count: int = 0) -> List[Dict[str, Any]]:
        """
        Ask the LLM to decompose Master_Goal into a backlog of sub-tasks.
        Falls back to a deterministic plan if Ollama is offline.
        """
        if not self.available():
            return self.default_plan(master_goal)

        system = (
            "You decompose game-development goals into ordered JSON tasks for an "
            "autonomous Godot 4 builder. Reply with ONLY a JSON array. Each item: "
            '{"task_id":"t01","description":"...","type":"bootstrap|harvest|player|enemy|ui|script|scene",'
            '"payload":{}}. Types: bootstrap, harvest, player, enemy, ui, script, scene, game_system.'
        )
        prompt = (
            f"Master goal: {master_goal}\n"
            f"Existing tasks already queued: {existing_count}\n"
            "Produce 8-15 concrete sub-tasks covering bootstrap, asset harvest, "
            "player, enemies, UI, and scene wiring. Use short task_ids like t01, t02…"
        )
        try:
            raw = self.chat(prompt, system=system)
            return self._parse_task_json(raw)
        except Exception as exc:
            _console("OLLAMA", f"Planning failed ({exc}) — using default plan.")
            return self.default_plan(master_goal)

    def identify_required_tools(self, master_goal: str) -> List[str]:
        if not self.available():
            return ["7zip", "git", "godot"]
        system = (
            "List third-party Windows tools needed for unattended Godot game generation. "
            "Reply with ONLY a JSON string array. Known names: 7zip, git, ffmpeg, godot."
        )
        try:
            raw = self.chat(
                f"Goal: {master_goal}\nWhich installers are required?",
                system=system,
            )
            start = raw.find("[")
            end = raw.rfind("]")
            if start >= 0 and end > start:
                data = json.loads(raw[start : end + 1])
                if isinstance(data, list):
                    return [str(x) for x in data]
        except Exception as exc:
            _console("OLLAMA", f"Tool identification failed: {exc}")
        return ["7zip", "git", "godot"]

    @staticmethod
    def _parse_task_json(raw: str) -> List[Dict[str, Any]]:
        start = raw.find("[")
        end = raw.rfind("]")
        if start < 0 or end <= start:
            raise ValueError("No JSON array in model response")
        data = json.loads(raw[start : end + 1])
        if not isinstance(data, list):
            raise ValueError("Expected JSON array")
        tasks: List[Dict[str, Any]] = []
        for i, item in enumerate(data):
            if not isinstance(item, dict):
                continue
            tasks.append(
                {
                    "task_id": str(item.get("task_id") or f"t{i+1:02d}"),
                    "description": str(item.get("description") or f"Task {i+1}"),
                    "type": str(item.get("type") or "script"),
                    "payload": item.get("payload") or {},
                }
            )
        return tasks

    @staticmethod
    def default_plan(master_goal: str) -> List[Dict[str, Any]]:
        """Deterministic backlog used when Ollama is offline."""
        return [
            {
                "task_id": "t01",
                "description": "Bootstrap Godot 4 project structure (project.godot, Main scene)",
                "type": "bootstrap",
                "payload": {},
            },
            {
                "task_id": "t02",
                "description": f"Harvest open-source 2D assets for: {master_goal}",
                "type": "harvest",
                "payload": {
                    "query": "2d pixel art sprites cc0",
                    "platforms": ["opengameart", "itchio"],
                    "max_pages": 2,
                    "max_assets": 3,
                },
            },
            {
                "task_id": "t03",
                "description": "Generate Player controller script and scene",
                "type": "player",
                "payload": {"class_name": "Player", "speed": 220},
            },
            {
                "task_id": "t04",
                "description": "Generating Enemy Script Logic",
                "type": "enemy",
                "payload": {"class_name": "Enemy", "health": 3},
            },
            {
                "task_id": "t05",
                "description": "Generate HUD / UI layer",
                "type": "ui",
                "payload": {"class_name": "HUD"},
            },
            {
                "task_id": "t06",
                "description": "Generate GameManager autoload-style system",
                "type": "game_system",
                "payload": {"class_name": "GameManager", "description": master_goal},
            },
            {
                "task_id": "t07",
                "description": "Generate Collectible pickup script",
                "type": "script",
                "payload": {"class_name": "Collectible"},
            },
            {
                "task_id": "t08",
                "description": "Generate Level01 scene tree wiring Player + Enemy + HUD",
                "type": "scene",
                "payload": {
                    "scene_name": "Level01",
                    "script": "res://scripts/Main.gd",
                    "tree": [
                        {"name": "PlayerSpawn", "type": "Marker2D", "parent": "."},
                        {"name": "EnemySpawn", "type": "Marker2D", "parent": "."},
                        {"name": "Tiles", "type": "Node2D", "parent": "."},
                    ],
                },
            },
            {
                "task_id": "t09",
                "description": "Harvest audio SFX packs (CC0)",
                "type": "harvest",
                "payload": {
                    "query": "cc0 sound effects",
                    "platforms": ["opengameart"],
                    "max_pages": 1,
                    "max_assets": 2,
                },
            },
            {
                "task_id": "t10",
                "description": "Generate CameraFollow script for player",
                "type": "script",
                "payload": {"class_name": "CameraFollow"},
            },
        ]
