# Jarvis Autonomous Game Generation Framework

State-persistent Python daemon that plans, harvests assets, and iteratively builds a Godot 4 project — designed to run unattended for hours on a local Windows 11 machine with Ollama and Playwright.

## Modules

| File | Role |
|------|------|
| `main.py` | Daemon orchestrator + CLI |
| `state_engine.py` | Strict `project_state.json` persistence |
| `installer_manager.py` | Pre-Flight Approval Queue + silent installs |
| `crawler_agent.py` | Playwright asset harvesting (itch.io, OpenGameArt) |
| `project_builder.py` | Godot 4 `.gd` / `.tscn` generation + validation |
| `ollama_client.py` | Local Ollama LLM planning & code gen |

## Quick start (Windows 11)

```bat
cd jarvis
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
playwright install chromium

:: Ensure Ollama is running locally (optional but recommended)
:: ollama serve
:: ollama pull llama3.2

python main.py --goal "Build a 2D pixel platformer with enemies and collectibles"
```

At startup you will see:

```
Jarvis requires the following third-party installations to run unattended...
```

Type `Y` once for blanket master approval. After that, downloads and silent installs proceed without further prompts.

## Resume after crash / reboot

State is flushed to `project_state.json` after every task. Simply run:

```bat
python main.py
```

Interrupted `running` tasks are recovered as `pending`. Completed work is never duplicated.

## CLI flags

```
python main.py --status          # print backlog summary
python main.py --once            # process one task then exit
python main.py --yes             # auto-approve installers (CI / unattended)
python main.py --skip-install    # skip pre-flight installs
python main.py --skip-harvest    # skip Playwright crawls
python main.py --reset-tasks     # clear backlog and replan
python main.py --model llama3.2  # Ollama model
```

## State schema (`project_state.json`)

- `Master_Goal` — long-term objective
- `Current_Milestone_Index` — active milestone
- `Task_Backlog[]` — `{task_id, description, status, logs}`
  - statuses: `pending` | `running` | `completed` | `pending_fix` | `failed` | `skipped`
- `MASTER_APPROVAL` — blanket approval for installs + autonomous downloads

## Outputs

- `godot_project/` — generated Godot 4 project (`project.godot`, `scripts/`, `scenes/`)
- `res/assets/` — unpacked harvested assets
- `res/downloads/` — raw downloaded archives
- `installers/` — cached third-party installer packages

## Validation / crash policy

If a generated `.gd` or `.tscn` fails syntax checks, the failure is logged to `project_state.json`, the task is marked `pending_fix`, and the daemon moves to the next item — it never aborts the whole run.
