"""Generate, test and optionally export a complete, bounded indie game.

The supported first genre is a 2D action platformer. Generation uses reusable
game systems, seeded level layouts and original procedural art/audio. Optional
Ollama changes the validated brief; it never bypasses the engine quality gate.
"""
from __future__ import annotations

import hashlib
import json
import math
import os
import random
import shutil
import struct
import subprocess
import time
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parent
TEMPLATES = ROOT / "templates" / "indie"


def load_goal() -> dict:
    return json.loads((ROOT / "indie_goal.json").read_text(encoding="utf-8"))


def find_godot(explicit: str | None = None) -> str | None:
    if explicit:
        candidate = Path(explicit).expanduser()
        if candidate.is_file():
            return str(candidate.resolve())
        return shutil.which(explicit)
    for command in ("godot", "godot4", "godot.exe"):
        found = shutil.which(command)
        if found:
            candidate = Path(found).resolve()
            console = candidate.with_name(candidate.stem + "_console.exe")
            return str(console if console.is_file() else candidate)
    local = Path(os.environ.get("LOCALAPPDATA", ""))
    packages = local / "Microsoft" / "WinGet" / "Packages"
    if packages.is_dir():
        found = sorted(packages.glob("GodotEngine.GodotEngine_*/Godot*_console.exe"))
        if found:
            return str(found[-1])
    return None


def make_levels(seed: int) -> list[dict]:
    rng = random.Random(seed)
    levels = []
    names = ["01  /  Mosslight Hollow", "02  /  The Quiet Crossing", "03  /  The Last Beacon"]
    for chapter in range(3):
        platforms = []
        x, y = 0, 590
        for i in range(8):
            width = 380 if i == 0 else rng.randrange(300, 401, 20)
            platforms.append([x, y, width])
            x += width + rng.randrange(60, 91, 10)
            y = max(510, min(610, y + rng.choice([-40, 0, 40])))
        shards = [{"x": p[0] + p[2] * 0.52, "y": p[1] - 48, "taken": False} for p in platforms[1:7]]
        enemies = []
        for i in [2, 4, 6]:
            p = platforms[i]
            enemies.append({"x": p[0] + p[2] * 0.7, "origin": p[0] + p[2] * 0.7, "y": p[1] - 23,
                            "range": 42 + chapter * 5, "speed": 1.0 + chapter * 0.15, "alive": True})
        checkpoints = [[platforms[i][0] + 45, platforms[i][1] - 20] for i in [3, 5]]
        end = platforms[-1]
        levels.append({"name": names[chapter], "platforms": platforms, "shards": shards, "enemies": enemies,
                       "checkpoints": checkpoints, "portal": [end[0] + end[2] - 85, end[1] - 20],
                       "width": end[0] + end[2]})
    return levels


def validate_layout(levels: list[dict]) -> None:
    if len(levels) != 3:
        raise ValueError("The indie contract requires three chapters")
    for level in levels:
        if len(level["shards"]) != 6 or not level["enemies"] or len(level["checkpoints"]) != 2:
            raise ValueError("Chapter objective, enemies or checkpoints missing")
        for current, following in zip(level["platforms"], level["platforms"][1:]):
            gap = following[0] - current[0] - current[2]
            rise = current[1] - following[1]
            # Conservative bounds under the player's 265px/s, 535 jump /1250 gravity arc.
            if not 0 <= gap <= 100 or abs(rise) > 45:
                raise ValueError("Unreachable platform transition")
        for shard in level["shards"]:
            if not any(p[0] + 20 < shard["x"] < p[0] + p[2] - 20 and 20 <= p[1] - shard["y"] <= 75 for p in level["platforms"]):
                raise ValueError("Shard is not reachable above a platform")


def generate_audio(directory: Path) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    notes = {"jump": (310, 640, .16), "dash": (440, 95, .14), "pickup": (660, 1320, .27),
             "hurt": (170, 55, .24), "win": (523, 1046, .65), "checkpoint": (392, 784, .5)}
    rate = 22050
    for name, (first, last, duration) in notes.items():
        data = bytearray()
        phase = 0.0
        for i in range(int(rate * duration)):
            t = i / rate
            progress = t / duration
            phase += math.tau * (first + (last - first) * progress) / rate
            envelope = min(1, t * 90) * (1 - progress) ** 2
            sample = (math.sin(phase) + .2 * math.sin(phase * 2)) * .3 * envelope
            data.extend(struct.pack("<h", int(sample * 32767)))
        _wave(directory / f"{name}.wav", data, rate)
    data = bytearray()
    for i in range(rate * 8):
        t = i / rate
        # Integer cycles over eight seconds give a seamless original ambient loop.
        sample = sum(math.sin(math.tau * f * t) * (0.7 + .3 * math.sin(math.tau * t / 8 + k))
                     for k, f in enumerate([130.75, 196.0, 261.625, 329.625])) * .07
        data.extend(struct.pack("<h", int(sample * 32767)))
    _wave(directory / "ambience.wav", data, rate)


def _wave(path: Path, data: bytes, rate: int) -> None:
    with wave.open(str(path), "wb") as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(rate)
        audio.writeframes(data)


def run_engine(godot: str, project: Path, args: list[str], label: str, timeout: int = 90, standalone: bool = False) -> dict:
    log = project / f"{label}.log"
    command = [godot, "--headless", "--log-file", str(log)]
    if not standalone:
        command += ["--path", str(project)]
    command += args
    started = time.monotonic()
    try:
        result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace",
                                timeout=timeout, cwd=str(project / "build") if standalone else None,
                                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        output = result.stdout + result.stderr
        if log.exists():
            output += "\n" + log.read_text(encoding="utf-8", errors="replace")
        passed = result.returncode == 0 and not any(token in output for token in ["SCRIPT ERROR:", "ERROR:", "Parse Error"])
        return {"name": label, "passed": passed, "exit_code": result.returncode,
                "seconds": round(time.monotonic() - started, 2), "log": str(log), "details": output[-8000:]}
    except (OSError, subprocess.TimeoutExpired) as error:
        return {"name": label, "passed": False, "details": str(error)}


def generate(output: Path, seed: int | None = None, title: str | None = None, ollama=None) -> dict:
    goal = load_goal()
    goal["seed"] = goal["seed"] if seed is None else seed
    if title:
        goal["title"] = title.strip()[:40] or goal["title"]
    design_source = "seeded_indie_template"
    if ollama is not None and ollama.available():
        try:
            response = ollama.chat("Suggest an original title (max 40 chars), tagline (max 70 chars) and integer seed for this brief. Reply only with a JSON object: " + goal["master_goal"])
            data = json.loads(response[response.index("{"):response.rindex("}") + 1])
            if not title and isinstance(data.get("title"), str):
                goal["title"] = data["title"].strip()[:40] or goal["title"]
            if isinstance(data.get("tagline"), str):
                goal["tagline"] = data["tagline"].strip()[:70]
            if seed is None and type(data.get("seed")) is int:
                goal["seed"] = data["seed"]
            design_source = "ollama_brief_with_verified_template"
        except (ValueError, KeyError, TypeError, RuntimeError):
            design_source = "seeded_indie_template_after_ollama_fallback"
    output = output.resolve()
    if output.exists() and any(output.iterdir()):
        raise ValueError("Output folder must be empty. Choose a new project folder to preserve existing work.")
    output.mkdir(parents=True, exist_ok=True)
    (output / "scripts").mkdir()
    levels = make_levels(goal["seed"])
    validate_layout(levels)
    data = {**goal, "levels": levels, "design_source": design_source}
    (output / "game_data.json").write_text(json.dumps(data, indent=2), encoding="utf-8")
    (output / "indie_goal.json").write_text(json.dumps(goal, indent=2), encoding="utf-8")
    for name in ["game.gd", "player.gd", "interface.gd", "gameplay_test.gd", "traversal_test.gd"]:
        shutil.copy2(TEMPLATES / name, output / "scripts" / name)
    # JSON quoting is compatible with the string values used in this Godot config.
    (output / "project.godot").write_text('config_version=5\n\n[application]\nconfig/name=' + json.dumps(goal["title"]) + '\nrun/main_scene="res://Main.tscn"\nconfig/features=PackedStringArray("4.3", "GL Compatibility")\n\n[display]\nwindow/size/viewport_width=1280\nwindow/size/viewport_height=720\nwindow/stretch/mode="canvas_items"\n\n[rendering]\nrenderer/rendering_method="gl_compatibility"\nrenderer/rendering_method.mobile="gl_compatibility"\ntextures/default_filters/use_nearest_mipmap_filter=false\n', encoding="utf-8")
    (output / "Main.tscn").write_text('[gd_scene load_steps=2 format=3]\n\n[ext_resource type="Script" path="res://scripts/game.gd" id="1"]\n\n[node name="Lanternfall" type="Node2D"]\nscript = ExtResource("1")\n', encoding="utf-8")
    generate_audio(output / "audio")
    (output / "ASSET_CREDITS.txt").write_text("Original geometric art, character designs and synthesized sound generated by GameForge. No downloaded asset packs or external asset licences are required. Godot engine: MIT licence, https://godotengine.org/license/\n", encoding="utf-8")
    (output / "PLAYTEST_CHECKLIST.md").write_text("# Human playtest still required\n\n- [ ] Readability at 1280x720 and smaller windows\n- [ ] Controls feel responsive with a keyboard\n- [ ] Every chapter can be completed through normal play\n- [ ] Checkpoint placement and enemy difficulty feel fair\n- [ ] Audio is balanced and mute works\n- [ ] Win, lose, pause and restart screens are clear\n\nAutomated checks do not establish commercial release readiness.\n", encoding="utf-8")
    (output / "README.txt").write_text(f"{goal['title']} — {goal['tagline']}\n\nCollect all six light shards in each chapter, then enter the stone beacon arch.\nA/D or arrows: move. Space/W/Up: jump. Shift/X: dash through enemies.\nEscape: pause. R: restart the whole run. M: mute. Enter: begin/continue.\nLight posts save your location and restore health. Three hits end the run.\n\nOpen project.godot in Godot 4, or launch the exported Windows game.\nSee quality_report.json for actual test results and PLAYTEST_CHECKLIST.md for remaining review.\n", encoding="utf-8")
    return data


def verify_project(output: Path, godot: str | None) -> dict:
    output = output.resolve()
    checks = []
    checks.append({"name": "godot_available", "passed": bool(godot)})
    if godot:
        checks.append(run_engine(godot, output, ["--import"], "engine_import"))
        if checks[-1]["passed"]:
            result_file = output / "gameplay_test_results.json"
            if result_file.exists():
                result_file.unlink()
            test = run_engine(godot, output, ["--fixed-fps", "60", "--script", "res://scripts/gameplay_test.gd"], "gameplay_tests")
            if result_file.exists():
                results = json.loads(result_file.read_text(encoding="utf-8"))
                test["assertions"] = results["checks"]
                test["passed"] = test["passed"] and results.get("passed") is True and len(results["checks"]) >= 17
            else:
                test["passed"] = False
                test["details"] += "\nGameplay test report was not produced."
            checks.append(test)
            traversal_file = output / "traversal_test_results.json"
            if traversal_file.exists():
                traversal_file.unlink()
            traversal = run_engine(godot, output, ["--fixed-fps", "60", "--script", "res://scripts/traversal_test.gd"], "full_game_traversal", 180)
            if traversal_file.exists():
                traversal["result"] = json.loads(traversal_file.read_text(encoding="utf-8"))
                traversal["passed"] = traversal["passed"] and traversal["result"].get("passed") is True
            else:
                traversal["passed"] = False
            checks.append(traversal)
            checks.append(run_engine(godot, output, ["--quit-after", "30"], "startup_smoke"))
    passed = len(checks) == 5 and all(c["passed"] for c in checks)
    report = {"schema_version": 1, "status": "engine_verified_prototype" if passed else "blocked",
              "passed": passed, "quality_target": "polished_indie_prototype", "checks": checks,
              "human_playtest": "pending", "commercial_release_ready": False,
              "source_sha256": {str(p.relative_to(output)): hashlib.sha256(p.read_bytes()).hexdigest()
                                for p in sorted(output.rglob("*.gd")) if ".godot" not in p.parts}}
    (output / "quality_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    return report


def export_windows(output: Path, godot: str, report: dict) -> dict:
    if not report["passed"]:
        raise ValueError("Export blocked: the game has not passed its engine quality gate")
    build = output / "build"
    build.mkdir(exist_ok=True)
    name = "".join(c for c in json.loads((output / "game_data.json").read_text(encoding="utf-8"))["title"] if c.isalnum() or c in " _-").strip() or "GameForgeGame"
    executable = build / (name + ".exe")
    preset = '[preset.0]\nname="Windows Desktop"\nplatform="Windows Desktop"\nrunnable=true\nexport_filter="all_resources"\ninclude_filter="game_data.json,audio/*.wav"\nexclude_filter="scripts/*_test.gd,*.log,*test_results.json,quality_report.json"\nexport_path=' + json.dumps(str(executable).replace('\\', '/')) + '\n\n[preset.0.options]\nbinary_format/embed_pck=true\nbinary_format/architecture="x86_64"\ntexture_format/bptc=true\ntexture_format/s3tc=true\ncodesign/enable=false\napplication/modify_resources=false\n'
    (output / "export_presets.cfg").write_text(preset, encoding="utf-8")
    result = run_engine(godot, output, ["--export-release", "Windows Desktop", str(executable)], "windows_export", 180)
    result["passed"] = result["passed"] and executable.is_file() and executable.stat().st_size > 1000000
    if result["passed"]:
        # Run the embedded game, never the editable source project, before enabling Play.
        smoke = run_engine(str(executable), output, ["--quit-after", "30"], "export_startup", standalone=True)
        result["startup"] = smoke
        result["passed"] = smoke["passed"]
    if result["passed"]:
        result["executable"] = str(executable)
        result["sha256"] = hashlib.sha256(executable.read_bytes()).hexdigest()
        for file in ["README.txt", "ASSET_CREDITS.txt", "PLAYTEST_CHECKLIST.md"]:
            shutil.copy2(output / file, build / file)
    report["export"] = result
    (output / "quality_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    return result


def build_indie(output: Path, godot: str | None = None, seed: int | None = None, title: str | None = None,
                export: bool = False, ollama=None, state=None) -> dict:
    goal = load_goal()
    if state:
        state.set_master_goal(goal["master_goal"])
        state.state["indie_build"] = {"status": "generating", "output": str(output.resolve())}
        state.save()
    print("Generating complete indie game...", flush=True)
    data = generate(output, seed, title, ollama)
    print("Running Godot import, gameplay assertions and startup checks...", flush=True)
    report = verify_project(output, find_godot(godot))
    if export and report["passed"]:
        print("Exporting verified Windows game...", flush=True)
        export_windows(output.resolve(), find_godot(godot), report)
    result = {"ok": report["passed"] and (not export or report.get("export", {}).get("passed", False)),
              "status": report["status"], "title": data["title"], "output": str(output.resolve()),
              "report": str(output.resolve() / "quality_report.json"),
              "executable": report.get("export", {}).get("executable"), "human_playtest": "pending"}
    if state:
        state.state["indie_build"] = result
        state.save()
    print("GAMEFORGE_RESULT=" + json.dumps(result), flush=True)
    return result
