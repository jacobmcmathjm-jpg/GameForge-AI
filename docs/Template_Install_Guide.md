# GameForge AI — Template Install Guide

## Overview

GameForge generates playable Unreal Engine 5 game prototypes using a **template copy system**.

There are three output levels:

| Level | Description | What it means |
|-------|-------------|---------------|
| **Project Shell / Environment Walkthrough** | Blueprint-only Unreal project with correct folder structure, config, and docs | Opens in Unreal with terrain and sky, but no player HUD, weapons, enemies, health, or gameplay systems |
| **Playable Template Project** | A real tested Unreal project copied and customised | Opens in Unreal and is immediately testable |
| **Packaged Build** | A compiled Windows .exe | A downloadable playable game |

GameForge currently generates **Project Shell / Environment Walkthrough** by default.
To generate **Playable Template Project**, you must install a real Unreal template.

---

## Template Detection

GameForge validates templates by checking **all** of the following:

1. `template_manifest.json` exists in the template folder
2. `template_manifest.json` is valid JSON with `projectMode: "BlueprintOnly"`, `requiresCpp: false`, and `status: "stable"` or `"ready"`
3. A `.uproject` file exists in the template folder root
4. A `Content/` folder exists
5. A `Config/` folder exists

If all checks pass → GameForge copies the template (**Playable Template Project** mode).
If any check fails → GameForge generates a project shell (**Project Shell / Environment Walkthrough** mode).

---

## template_manifest.json Standard

Every template folder must include a `template_manifest.json` file. This file tells GameForge whether the template is installed and valid.

**Required fields:**

```json
{
  "templateId": "fps_blueprint",
  "name": "First-Person Shooter Blueprint Template",
  "engine": "UnrealEngine",
  "compatibleVersions": ["5.4", "5.5", "5.6"],
  "projectMode": "BlueprintOnly",
  "requiresCpp": false,
  "requiresPlugins": [],
  "hasPlayableMap": true,
  "hasPlayer": true,
  "hasWeapons": true,
  "hasEnemies": true,
  "hasHUD": true,
  "status": "stable"
}
```

**Field reference:**

| Field | Required value to activate | Description |
|-------|---------------------------|-------------|
| `templateId` | Any string | Unique ID for this template |
| `name` | Any string | Human-readable name |
| `engine` | `"UnrealEngine"` | Engine type |
| `compatibleVersions` | Array of strings | UE versions tested |
| `projectMode` | `"BlueprintOnly"` | Must be BlueprintOnly — C++ templates not supported |
| `requiresCpp` | `false` | Must be false |
| `requiresPlugins` | `[]` | List any non-standard plugins (empty = no extra plugins) |
| `hasPlayableMap` | `true` | Set to true once a .umap file is in Content/Maps/ |
| `hasPlayer` | `true` | Set to true once a player Blueprint is installed |
| `hasWeapons` | `true` | Set to true once weapon Blueprints are installed |
| `hasEnemies` | `true` | Set to true once enemy AI Blueprints are installed |
| `hasHUD` | `true` | Set to true once a HUD Widget Blueprint is installed |
| `status` | `"stable"` or `"ready"` | **Set this last** — this is what activates the template |

**Important:** GameForge only uses the template if `status` is `"stable"` or `"ready"`. While you are building or testing, keep it as `"not-installed"` to avoid using an incomplete template.

---

## Template Folder Map

| Game Type | Template Folder |
|-----------|----------------|
| FPS | `app/templates/unreal/fps_blueprint/` |
| Zombie Shooter | `app/templates/unreal/zombie_shooter_blueprint/` |
| Horror | `app/templates/unreal/horror_blueprint/` |
| Survival | `app/templates/unreal/survival_blueprint/` |
| Racing | `app/templates/unreal/racing_blueprint/` |
| RPG | `app/templates/unreal/rpg_blueprint/` |
| Open World | `app/templates/unreal/open_world_blueprint/` |

---

## How to Create and Install a Template

### Step 1: Create the Unreal template

1. Open Epic Games Launcher → Unreal Engine 5.4+
2. Click **Launch** → New Project → Blueprint → First Person (or your target genre)
3. Disable "Starter Content" if you want a lean template
4. Set the project folder to a temp location
5. Build out the required gameplay systems (see below)
6. Test: press Play in Unreal Editor and verify the gameplay works

### Step 2: Verify template requirements

Your template MUST:

- [ ] Open in Unreal Engine 5.4+ without errors
- [ ] Show no "Missing Plugin" warnings
- [ ] Have NO `Modules` array in the `.uproject` file
- [ ] Have NO C++ source files in a `Source/` folder
- [ ] Have NO non-standard plugins (only standard UE5 plugins)
- [ ] Have at least one playable map in `Content/Maps/`
- [ ] Include working Blueprint assets in `Content/Blueprints/`

### Step 3: Copy the template

Copy the entire Unreal project folder into the matching GameForge template folder:

**Example for FPS:**
```
Copy from:  C:\Users\You\Documents\Unreal Projects\MyFPSTemplate\
Copy to:    GameForge\app\templates\unreal\fps_blueprint\
```

The result should look like:
```
app/templates/unreal/fps_blueprint/
  template_manifest.json     ← GameForge validates this first
  MyFPSTemplate.uproject     ← GameForge detects and renames this
  Config/
    DefaultEngine.ini
    DefaultGame.ini
    DefaultInput.ini
  Content/
    Maps/
      StarterMap.umap
    Blueprints/
      Player/...
      Enemies/...
      Weapons/...
      UI/...
```

### Step 4: Update template_manifest.json

Open `app/templates/unreal/fps_blueprint/template_manifest.json` and:

1. Set `"hasPlayableMap": true`
2. Set `"hasPlayer": true`
3. Set `"hasWeapons": true`
4. Set `"hasEnemies": true`
5. Set `"hasHUD": true`
6. Set `"status": "stable"` ← This activates the template

### Step 5: Test in GameForge

1. Open GameForge
2. Select **FPS** (or matching game type)
3. Click **Generate Prototype**
4. Watch the logs for:
   - `Checking local template library...`
   - `Template manifest found: ...`
   - `Playable template copied successfully`
5. Result card shows: **Playable Template Project**

---

## Required Systems Per Template

### FPS Template
- First-person player pawn (capsule, camera, spring arm)
- First-person weapon (mesh, fire, reload, ammo)
- Line trace damage system
- Health component (player + enemies)
- Basic AI enemy (patrol/chase/attack)
- HUD (health bar, ammo counter)
- Game Mode (win/lose conditions)
- Starter map

### Zombie Shooter Template
Everything in FPS, plus:
- Zombie AI with sight/hearing perception
- Behavior Tree: Patrol → Chase → Melee Attack
- Zombie spawner with wave escalation
- Wave Manager (wave count, enemies remaining)
- Wave HUD (wave number, enemies left, timer)
- Game Over screen

### Horror Template
- Atmospheric first-person movement
- Flashlight (toggle on/off, battery drain)
- Stamina system (sprint drain, recover)
- Sanity meter (decay near enemies, triggers hallucinations)
- AI enemy: patrol, alert on sight/sound, chase, lose-sight
- Interactable items (pick up, examine, use)
- Hiding spots (trigger overlap, camera hide)
- Ambient audio triggers

### Survival Template
- Resource nodes (trees, rocks, plants — interact to gather)
- Inventory system (item array, weight, stacking)
- Needs component (hunger, thirst, warmth — decay over time)
- Crafting system (recipe data table, combine items)
- Base building (snap-to-grid placement)
- Day/night cycle (DirectionalLight rotation)
- Save game system

### Racing Template
- Chaos vehicle pawn with wheel blueprints
- Checkpoint sequence (ordered overlap triggers)
- Lap tracker (lap count, current time, best time)
- Countdown and race start system
- Race HUD (speed, lap, timer, position)
- Multiple race tracks or a demo track

### RPG Template
- Character stats (level, XP, STR/AGI/INT, on-level-up event)
- Inventory system (equipment slots, consumables)
- Dialogue system (NPC conversations, branching choices)
- Quest manager (active quests, objectives, completion)
- Loot drop component (enemy death → spawn loot actors)
- Save/load system

### Open World Template
- World Partition enabled
- HLODs for distant geometry
- Day/night cycle
- Exploration gameplay loop
- POI (point of interest) markers
- Streaming level volumes

---

## Important Notes

- GameForge renames the `.uproject` file to match the user's chosen project name
- GameForge updates `Config/DefaultEngine.ini` with the new name
- GameForge does NOT modify any Blueprint `.uasset` files (these are binary)
- If your template uses hardcoded project name references inside Blueprints, you may need to manually update them in Unreal Editor after generation
- GameForge adds `Docs/`, `Output/`, `Scripts/`, and `Scenes/` folders with its own documentation

---

## Getting Free Unreal Templates

- **Epic Games Launcher → Learn tab** — Many free sample projects
- **Unreal Engine Marketplace → Free section** — Blueprint-only game starters
- **Epic's built-in project templates** — First Person, Third Person, Top Down (all Blueprint)
  - Launch Unreal → New Project → Blueprint → First Person → Create
  - This gives you a working FPS shell immediately

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| GameForge shows "Project Shell" instead of "Playable Template Project" | Verify `template_manifest.json` exists and `status` is `"stable"` or `"ready"` |
| Manifest found but template not used | Check `projectMode` is `"BlueprintOnly"` and `requiresCpp` is `false` |
| GameForge shows "Template manifest not found" | Create `template_manifest.json` in the template folder (see standard above) |
| Unreal shows "Missing Plugin" on open | Check `.uproject` — remove any non-standard plugin entries |
| Unreal asks to rebuild (C++) | Check `.uproject` — remove the `Modules` array |
| Template opens but gameplay broken | Test and fix the template in Unreal Editor before placing it in GameForge |
| Blueprints reference wrong project name | Internal Blueprint references use content paths (e.g. `/Game/...`) — these do not break on project rename |
