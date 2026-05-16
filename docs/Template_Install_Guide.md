# GameForge AI — Template Install Guide

## Overview

GameForge generates playable Unreal Engine 5 game prototypes using a **template copy system**.

There are three output levels:

| Level | Description | What it means |
|-------|-------------|---------------|
| **Project Shell** | Blueprint-only Unreal project with correct folder structure, config, and docs | Opens in Unreal, but no gameplay assets yet |
| **Playable Template** | A real tested Unreal project copied and customised | Opens in Unreal and is immediately testable |
| **Packaged Build** | A compiled Windows .exe | A downloadable playable game |

GameForge currently generates **Project Shell** by default.
To generate **Playable Template**, you must install a real Unreal template.

---

## Template Detection

GameForge detects templates by looking for a `.uproject` file inside:

```
app/templates/unreal/<template_folder>/
```

If a `.uproject` file is found → GameForge copies the template (Playable Template mode).
If no `.uproject` file is found → GameForge generates a project shell (Project Shell mode).

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
  MyFPSTemplate.uproject     ← GameForge detects this
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

### Step 4: Test in GameForge

1. Open GameForge
2. Select **FPS** (or matching game type)
3. Click **Generate Prototype**
4. GameForge detects the template and copies it
5. Check logs for: `Playable FPS template copied successfully`
6. Result card shows: **Playable Template**

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
- GameForge updates `Config/DefaultEngine.ini` and `Config/DefaultGame.ini` with the new name
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
| GameForge shows "Project Shell" instead of "Playable Template" | Verify a `.uproject` file exists in the template folder |
| Unreal shows "Missing Plugin" on open | Check `.uproject` — remove any non-standard plugin entries |
| Unreal asks to rebuild (C++) | Check `.uproject` — remove the `Modules` array |
| Template opens but gameplay broken | Test and fix the template in Unreal Editor before placing it in GameForge |
| Blueprints reference wrong project name | Internal Blueprint references use content paths (e.g. `/Game/...`) — these do not break on project rename |
