# GameForge AI — Indie Studio

One desktop app combining GameForge's interface and project settings with Jarvis's persisted generation pipeline and Godot's playable exports. Open **Indie Studio**, choose a title and world seed, then select **Build & test game**. A successful build enables **Play game** and **Open project**.

The first supported game is **Lanternfall**, a complete three-chapter 2D action platformer: collect six lights per chapter, jump and dash past moths, use checkpoints, and reach the final gate. It includes original procedural art and audio, pause/mute, damage and recovery, defeat, victory and restart.

## Run

- Windows: install Python 3.10+ and Godot 4 with matching Windows export templates.
- From `app`, run `npm ci` then `npm start` (Node.js required for development only).
- The desktop app detects local tools; custom executable paths are available under **Tool locations**. No API key, paid service or asset download is needed for Indie Studio.
- `npm run pack` builds the unpacked desktop app; `npm run build:portable` creates a portable Windows executable. Packaged builds include Jarvis and the game templates. Python and Godot remain local prerequisites for generation; exported games run independently.

## Quality contract

`jarvis/indie_goal.json` stores the explicit indie goal. Export is blocked unless layout checks, Godot import, 17 engine gameplay assertions, a complete keyboard-driven playthrough of all three chapters, and a startup smoke test pass. Each generated project includes `quality_report.json`, test results, source, asset credits and a human playtest checklist. A passing report means **engine-verified prototype**, not commercial release approval.

Generation currently varies a proven platformer template with a reproducible seed. Optional local Ollama can suggest the brief; it does not invent arbitrary games or bypass validation. The original Unreal tools remain available as a separate project-shell workflow. Bundled Unreal template manifests are not installed playable assets.

## Command line and tests

```sh
python jarvis/main.py --build-indie --output <new-empty-folder> --seed 417 --export
python -m unittest discover -s jarvis/tests -v
```

Add `--godot <executable>` if automatic detection fails. The desktop integration test runs the actual app, clicks Build, checks the exported result and captures screenshots:

```sh
cd app
npx electron tools/test-indie-desktop.cjs <test-output-folder>
```

The legacy Jarvis task queue now preserves completed work, allows other pending tasks before repairs, and stops repeatedly failing tasks after three attempts. Its older general-purpose daemon remains experimental; Indie Studio is the supported playable workflow.
