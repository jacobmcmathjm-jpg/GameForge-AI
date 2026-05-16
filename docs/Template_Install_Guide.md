# GameForge AI — Template Install Guide

## Overview

GameForge generates playable Unreal Engine 5 game prototypes using a **template copy system**.

GameForge is designed to require **minimal human interaction**. You provide a game idea. GameForge automatically:
- Checks for a local template
- Copies the best matching template
- Falls back safely to a Project Shell if no template is available
- Classifies the output stage automatically
- Prepares the next upgrade recommendation

**Manual action is only required when:**
- A real Unreal template must be installed for the first time
- An API key must be entered
- Packaging requires a user-controlled export step in Unreal Editor

---

## Output Stages

GameForge classifies output automatically based on what content is detected:

| Stage | Label | What GameForge does |
|-------|-------|---------------------|
| No template installed | **Project Shell / Environment Walkthrough** | Safe fallback — used automatically |
| Player + map detected | **Playable Movement Template** (movement_base) | Auto-classified — not a full FPS game |
| Player + weapon/shooting/HUD | **FPS Weapon Template** (fps_weapon_base) | Auto-classified |
| Player + weapon + enemies | **Zombie Shooter Template** (zombie_shooter_base) | Auto-classified |
| All systems present | **Playable Template Project** (full_playable_template) | Auto-classified |
| User packages in Unreal | **Packaged Build** | Manual step — user-controlled |

---

## Autonomous Pipeline

```
Game Idea
→ Auto-select safest generation mode
→ Check local templates
→ Copy template if available, fall back if not
→ Rename Unreal project
→ Validate project structure
→ Classify template level automatically
→ Score stage readiness
→ Report missing systems for next stage
→ Prepare next upgrade recommendation
→ Future: automated packaging
```

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

**Required fields (movement_base example):**

```json
{
  "templateId": "fps_blueprint",
  "name": "First-Person Shooter Blueprint Template",
  "engine": "UnrealEngine",
  "compatibleVersions": ["5.4", "5.5", "5.6"],
  "projectMode": "BlueprintOnly",
  "requiresCpp": false,
  "requiresPlugins": [],
  "templateLevel": "movement_base",
  "hasPlayableMap": true,
  "hasPlayer": true,
  "hasWeapons": false,
  "hasEnemies": false,
  "hasHUD": false,
  "status": "stable"
}
```

**FPS Weapon Template manifest (fps_weapon_base):**

```json
{
  "templateId": "fps_blueprint",
  "name": "FPS Weapon Blueprint Template",
  "engine": "UnrealEngine",
  "compatibleVersions": ["5.4", "5.5", "5.6"],
  "projectMode": "BlueprintOnly",
  "requiresCpp": false,
  "requiresPlugins": [],
  "templateLevel": "fps_weapon_base",
  "hasPlayableMap": true,
  "hasPlayer": true,
  "hasWeapons": true,
  "hasEnemies": false,
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
| `templateLevel` | `"movement_base"` / `"fps_weapon_base"` / `"zombie_shooter_base"` | Stage hint — GameForge also auto-detects from content scan |
| `hasPlayableMap` | `true` | Set to true once a .umap file is present |
| `hasPlayer` | `true` | Set to true once a player Blueprint is installed |
| `hasWeapons` | `true` | Set to true once weapon Blueprints are installed |
| `hasEnemies` | `true` | Set to true once enemy AI Blueprints are installed |
| `hasHUD` | `true` | Set to true once a HUD Widget Blueprint is installed |
| `status` | `"stable"` or `"ready"` | **Set this last** — this activates the template |

**Important:** GameForge only uses the template if `status` is `"stable"` or `"ready"`. While building or testing, keep it as `"not-installed"`.

**Note on templateLevel:** GameForge also runs a recursive content scan to auto-classify the template level. The `templateLevel` field in the manifest is informational — GameForge will warn if there is a mismatch between what the manifest claims and what the content scan detects.

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

---

### Template v0.1 — Movement Base (movement_base)

**Minimum required — GameForge detects this automatically:**
- First-person or third-person player pawn (capsule collision)
- First-person camera (spring arm + camera component)
- Player Controller with input mappings (WASD, mouse look, jump)
- At least one playable map (.umap file)
- Blueprint-only (no C++ modules)
- No non-standard plugins

**GameForge will classify as:** `movement_base`

**What pressing Play feels like:** Camera/movement mode — you can walk and look around, but there are no weapons, enemies, HUD, or gameplay systems.

---

### Template v0.2 — FPS Weapon Template (fps_weapon_base)

**To upgrade from movement_base to fps_weapon_base, add:**
- Visible weapon or weapon placeholder (mesh on arms/hands, or weapon actor)
- Shooting mechanic (fire input, projectile or line trace)
- Projectile or line trace damage system
- Crosshair or basic HUD Widget Blueprint (reticle/crosshair overlay)
- Ammo placeholder or weapon status display
- Basic target/damage test (destructible actor or dummy target)
- No C++ modules
- No non-standard plugins

**Folder/file naming that GameForge detects** (any of these trigger weapon detection):
`weapon`, `gun`, `rifle`, `pistol`, `shoot`, `fire`, `projectile`, `bullet`, `ammo`, `crosshair`, `reticle`, `damage`, `target`, `muzzle`, `hud`, `widget`, `wbp_`

**GameForge will classify as:** `fps_weapon_base`

**What pressing Play feels like:** You can move, look, and shoot. Basic hit detection works. No enemies yet.

**Template manifest for fps_weapon_base:**
```json
{
  "templateId": "fps_blueprint",
  "name": "FPS Weapon Blueprint Template",
  "engine": "UnrealEngine",
  "compatibleVersions": ["5.4", "5.5", "5.6"],
  "projectMode": "BlueprintOnly",
  "requiresCpp": false,
  "requiresPlugins": [],
  "templateLevel": "fps_weapon_base",
  "hasPlayableMap": true,
  "hasPlayer": true,
  "hasWeapons": true,
  "hasEnemies": false,
  "hasHUD": true,
  "status": "stable"
}
```

**Install location:** `app/templates/unreal/fps_blueprint/`

---

### Template v0.3 — Zombie Shooter Base (zombie_shooter_base)

**To upgrade from fps_weapon_base to zombie_shooter_base, add these on top of fps_weapon_base:**
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
