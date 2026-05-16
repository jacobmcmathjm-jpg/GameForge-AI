# GameForge AI — Template Install Guide

## Overview

GameForge generates playable Unreal Engine 5 game prototypes using a **template copy system**.

GameForge is designed to require **minimal human interaction**. You provide a game idea. GameForge automatically:
- Checks for a local template
- Copies the best matching template
- Falls back safely to a Project Shell if no template is available
- Classifies the output stage automatically using honest, stage-based detection
- Prepares the next upgrade recommendation

**Manual action is only required when:**
- A real Unreal template must be installed for the first time
- An API key must be entered
- Packaging requires a user-controlled export step in Unreal Editor

---

## Asset Files Alone Do Not Prove Active Gameplay

This is a core design rule in GameForge's classification system.

**Example:** The Unreal Engine First Person template includes animation assets with names like `Fire_Montage`, `Death_Anim`, `Aim_Animation`. These are legitimate Unreal content — they are animation sequences that play during weapon use. However, their filenames contain words like "Fire" and "Death".

A naive asset scanner could detect `Fire_Montage.uasset` and conclude "weapon firing system detected." This would be **wrong**. The presence of an animation asset named "Fire" does not prove:
- That a weapon Blueprint exists and is wired to the player
- That the weapon actually fires when the player presses the shoot button
- That there is any functional weapon gameplay in Play mode

**GameForge separates two different concepts:**

| Concept | What it means | Evidence required |
|---------|---------------|-------------------|
| **Asset detection** | Asset files with relevant names exist in Content/ | File scan of Content/ only |
| **Active confirmation** | Gameplay systems are active and connected in Play mode | Strict BP asset names (`BP_Weapon`, `BP_Enemy`, etc.) + custom GameMode in Config/DefaultGame.ini |

**Only confirmed stages should be labelled as confirmed.** If GameForge cannot verify that a system is active, it says so clearly — and tells you to open Unreal Editor and press Play to verify manually.

---

## Output Stages

GameForge classifies output automatically based on honest, stage-based detection. Eight stages exist:

| # | Stage | Label | Evidence required |
|---|-------|-------|-------------------|
| 0 | No template installed | **Project Shell / Environment Walkthrough** | Safe fallback — auto |
| 1 | Player + map | **Playable Movement Template** (movement_base) | Player BP + .umap found in Content/ |
| 2 | Weapon assets only | **FPS Asset Base** (fps_asset_base) | BP_Weapon/weapon folder detected — no config evidence |
| 3 | Weapon + config | **FPS Weapon Template** (fps_weapon_base) | Weapon assets + custom GameMode in DefaultGame.ini |
| 4 | Enemy assets only | **Zombie Asset Base** (zombie_asset_base) | BP_Enemy/enemy folder detected — no config evidence |
| 5 | Enemy + weapon | **Zombie Shooter Base** (zombie_shooter_base) | Enemy + weapon assets both detected |
| 6 | All systems + config | **Playable Template Project** (full_playable_template) | All strict asset groups + custom GameMode confirmed |
| 7 | User-exported | **Packaged Build** | User exports via Unreal Editor — manual step |

**Stages 2 and 4 (fps_asset_base, zombie_asset_base)** mean: relevant asset files were found in Content/, but GameForge cannot confirm those systems are active in Play mode without config evidence. Open Unreal Editor and press Play to verify.

---

## What GameForge Scans

GameForge scans **only the Content/ folder** of the template. It does NOT scan:
- `Docs/` — GameForge's own documentation
- `Output/` — GameForge's own reports
- `Scripts/` — GameForge's own scripts
- `README.md` or any text files

This prevents false positives from documentation files containing words like "weapon" or "enemy".

**Strict asset detection rules (Content/ scan):**

GameForge uses `^`-anchored regex patterns that match the **start of filenames only**:

| System | Detects | Does NOT detect |
|--------|---------|-----------------|
| Player | `BP_FirstPerson`, `BP_Player`, `BP_Character`, `PlayerCharacter`, `Mannequin` | Generic animation names |
| Weapon | `BP_Weapon`, `BP_Gun`, `BP_Rifle`, `BP_Pistol`, `weapon/` folder | `Fire_Montage`, `Aim_Animation` |
| Shooting | `BP_Projectile`, `BP_Bullet`, `BP_Tracer`, `projectile/` folder | `FireRate`, `MuzzleFlash_Particle` |
| HUD | `WBP_` prefix, `BP_HUD`, `hud/` folder, `crosshair`, `reticle` | `AmmoPickup`, `HealthPotion` |
| Damage | `BP_Damageable`, `BP_Destructible`, `BP_Target`, `DamageTarget` | Generic health values |
| Enemy | `BP_Enemy`, `BP_Zombie`, `BP_AI`, `BP_NPC`, `enemy/` folder, `zombie/` folder | `Death_Animation`, `EnemyHit_Sound` |

**Config evidence for active confirmation:**

GameForge reads `Config/DefaultGame.ini` and `Config/DefaultEngine.ini` looking for:
- `GlobalDefaultGameMode=` — a custom GameMode Blueprint reference
- `DefaultPawnClass=` — a custom Pawn Blueprint reference

A custom GameMode in config is structural evidence that the project has been set up to run actual gameplay — not just that some asset files exist.

---

## Autonomous Pipeline

```
Game Idea
→ Auto-select safest generation mode
→ Check local templates
→ Copy template if available, fall back if not
→ Rename Unreal project
→ Validate project structure
→ Scan Content/ only (strict asset detection)
→ Check Config/ for active gameplay evidence
→ Classify template level honestly (8 stages)
→ Detect mismatch between manifest and scan
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

If all checks pass → GameForge copies the template.
If any check fails → GameForge generates a project shell (safe fallback).

---

## template_manifest.json Standard

Every template folder must include a `template_manifest.json` file.

**movement_base manifest example:**

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

**fps_weapon_base manifest example:**

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
| `projectMode` | `"BlueprintOnly"` | Must be BlueprintOnly |
| `requiresCpp` | `false` | Must be false |
| `requiresPlugins` | `[]` | List any non-standard plugins |
| `templateLevel` | See stages table above | Stage hint — GameForge also auto-detects from scan |
| `hasPlayableMap` | `true` | Set once a .umap file is present |
| `hasPlayer` | `true` | Set once a player Blueprint is installed |
| `hasWeapons` | `true` | Set once weapon Blueprints are installed |
| `hasEnemies` | `true` | Set once enemy AI Blueprints are installed |
| `hasHUD` | `true` | Set once a HUD Widget Blueprint is installed |
| `status` | `"stable"` or `"ready"` | **Set this last** — this activates the template |

**Important:** GameForge only uses the template if `status` is `"stable"` or `"ready"`. While building or testing, keep it as `"not-installed"`.

**Note on templateLevel:** The `templateLevel` field in the manifest is informational. GameForge runs a content scan and config check to auto-classify the actual level. GameForge will warn in the logs if there is a mismatch between what the manifest claims and what the scan detects.

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
    DefaultGame.ini            ← Add GlobalDefaultGameMode here for active confirmation
    DefaultInput.ini
  Content/
    Maps/
      StarterMap.umap
    Blueprints/
      Player/
        BP_FirstPersonCharacter.uasset   ← detected as player
      Enemies/
        BP_Enemy.uasset                  ← detected as enemy
      Weapons/
        BP_Weapon.uasset                 ← detected as weapon
      UI/
        WBP_HUD.uasset                   ← detected as HUD
```

### Step 4: Update template_manifest.json

Open `app/templates/unreal/fps_blueprint/template_manifest.json` and:

1. Set `"hasPlayableMap": true`
2. Set `"hasPlayer": true`
3. Set `"hasWeapons": true` (if weapon Blueprints are installed)
4. Set `"hasEnemies": true` (if enemy AI Blueprints are installed)
5. Set `"hasHUD": true` (if HUD Widget Blueprints are installed)
6. Set `"status": "stable"` ← This activates the template

### Step 5: Add GameMode to Config (for active confirmation)

To reach `fps_weapon_base` or higher, add a custom GameMode reference to `Config/DefaultGame.ini`:

```ini
[/Script/EngineSettings.GameMapsSettings]
GlobalDefaultGameMode=/Game/Blueprints/GameMode/BP_GameMode.BP_GameMode_C
```

This tells GameForge that the project has been wired up to run actual gameplay — not just that asset files exist.

### Step 6: Test in GameForge

1. Open GameForge
2. Select the matching game type
3. Click **Generate Prototype**
4. Watch the logs — the stage classification will appear
5. Result card shows the honest stage name and what was confirmed vs. detected-only

---

## Required Systems Per Template Stage

### Stage 1 — Movement Base (movement_base)

**Minimum required:**
- First-person or third-person player pawn (capsule collision)
- First-person camera (spring arm + camera component)
- Player Controller with input mappings (WASD, mouse look, jump)
- At least one playable map (.umap file)
- Blueprint-only (no C++ modules)

**GameForge classifies as:** `movement_base`

**What pressing Play feels like:** Camera/movement only — you can walk and look around. No weapons, enemies, HUD, or gameplay systems.

---

### Stage 2 — FPS Asset Base (fps_asset_base)

**What this means:** GameForge found weapon/HUD asset files in Content/ that match strict naming rules (`BP_Weapon`, `BP_Gun`, `WBP_` HUD, etc.) but found no Config/DefaultGame.ini GameMode reference.

**GameForge classifies as:** `fps_asset_base`

**What to do:** Open the project in Unreal Editor and press Play. If weapons fire and HUD appears, the template is working. Add a custom GameMode to `Config/DefaultGame.ini` and GameForge will classify it as `fps_weapon_base` on the next generation.

**This is NOT a GameForge error** — it is an honest report that asset files were found but active gameplay could not be confirmed automatically.

---

### Stage 3 — FPS Weapon Template (fps_weapon_base)

**Required in addition to movement_base:**
- Weapon Blueprint (`BP_Weapon`, `BP_Gun`, `BP_Rifle`, or `BP_Pistol`) in Content/
- Shooting: `BP_Projectile` or similar, or line trace wired to weapon
- HUD Widget: `WBP_` prefixed Blueprint or `BP_HUD`
- Custom GameMode in `Config/DefaultGame.ini`

**GameForge classifies as:** `fps_weapon_base`

**What pressing Play feels like:** You can move, look, and shoot. Basic hit detection works. No enemies yet.

---

### Stage 4 — Zombie Asset Base (zombie_asset_base)

**What this means:** GameForge found enemy/AI asset files in Content/ that match strict naming rules (`BP_Enemy`, `BP_Zombie`, `enemy/` folder, etc.) but found no Config/DefaultGame.ini GameMode reference.

**GameForge classifies as:** `zombie_asset_base`

**What to do:** Open the project in Unreal Editor and press Play. If enemies spawn and act, the template is working. Add a custom GameMode to Config/DefaultGame.ini and GameForge will classify it as `zombie_shooter_base` on the next generation.

---

### Stage 5 — Zombie Shooter Base (zombie_shooter_base)

**Required in addition to fps_weapon_base:**
- Zombie/enemy AI Blueprint (`BP_Enemy`, `BP_Zombie`, or `enemy/` folder)
- Enemy spawner or wave manager
- Health/damage loop (player can be damaged, enemies can be killed)

**GameForge classifies as:** `zombie_shooter_base`

**What pressing Play feels like:** You can move, shoot, and enemies spawn and engage the player.

---

### Stage 6 — Playable Template Project (full_playable_template)

**Required:**
- All systems from zombie_shooter_base
- HUD Widget with gameplay info visible
- Custom GameMode in `Config/DefaultGame.ini`
- Objective loop (wave clear, score, or equivalent)

**GameForge classifies as:** `full_playable_template`

**What pressing Play feels like:** Complete playable game loop. Press Play, survive waves, see HUD, win/lose condition triggers.

---

### Other Genre Systems

### Horror Template
- Atmospheric first-person movement
- Flashlight (toggle on/off, battery drain)
- Stamina system (sprint drain, recover)
- Sanity meter (decay near enemies)
- AI enemy: patrol, alert on sight/sound, chase, lose-sight
- Interactable items (pick up, examine, use)
- Hiding spots
- Ambient audio triggers

### Survival Template
- Resource nodes (interact to gather)
- Inventory system (item array, weight, stacking)
- Needs component (hunger, thirst, warmth — decay over time)
- Crafting system (recipe data table)
- Base building (snap-to-grid placement)
- Day/night cycle
- Save game system

### Racing Template
- Chaos vehicle pawn with wheel blueprints
- Checkpoint sequence (ordered overlap triggers)
- Lap tracker (lap count, current time, best time)
- Race HUD (speed, lap, timer, position)

### RPG Template
- Character stats (level, XP, attributes, on-level-up event)
- Inventory system (equipment slots, consumables)
- Dialogue system (NPC conversations, branching choices)
- Quest manager (active quests, objectives, completion)
- Loot drop component
- Save/load system

### Open World Template
- World Partition enabled
- Day/night cycle
- Exploration gameplay loop
- POI (point of interest) markers
- Streaming level volumes

---

## Important Notes

- GameForge renames the `.uproject` file to match the user's chosen project name
- GameForge updates `Config/DefaultEngine.ini` with the new name
- GameForge does NOT modify any Blueprint `.uasset` files (these are binary)
- GameForge adds `Docs/`, `Output/`, `Scripts/`, and `Scenes/` folders with its own documentation
- If your template uses hardcoded project name references inside Blueprints, you may need to manually update them in Unreal Editor after generation (content paths like `/Game/...` do not break on rename)

---

## Getting Free Unreal Templates

- **Epic Games Launcher → Learn tab** — Many free sample projects
- **Unreal Engine Marketplace → Free section** — Blueprint-only game starters
- **Epic's built-in project templates** — First Person, Third Person, Top Down (all Blueprint)
  - Launch Unreal → New Project → Blueprint → First Person → Create
  - This gives you a working FPS shell immediately (movement_base or fps_asset_base stage)

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| GameForge shows "Project Shell" instead of a template stage | Verify `template_manifest.json` exists and `status` is `"stable"` or `"ready"` |
| Manifest found but template not used | Check `projectMode` is `"BlueprintOnly"` and `requiresCpp` is `false` |
| GameForge shows `fps_asset_base` instead of `fps_weapon_base` | Add `GlobalDefaultGameMode=` to `Config/DefaultGame.ini` and name weapon assets with `BP_Weapon` prefix |
| GameForge shows `zombie_asset_base` instead of `zombie_shooter_base` | Add `GlobalDefaultGameMode=` to `Config/DefaultGame.ini` and name enemy assets with `BP_Enemy` or `BP_Zombie` prefix |
| Manifest claims higher stage than scan detects | GameForge warns in logs — update `templateLevel` in manifest to match actual content |
| Unreal shows "Missing Plugin" on open | Check `.uproject` — remove any non-standard plugin entries |
| Unreal asks to rebuild (C++) | Check `.uproject` — remove the `Modules` array |
| Template opens but gameplay broken | Test and fix the template in Unreal Editor before placing it in GameForge |
| Blueprints reference wrong project name | Internal Blueprint references use content paths (e.g. `/Game/...`) — these do not break on project rename |
