# GameForge AI Engine v3.3.1 — Startup Hotfix v3.3.1

This build adds approved rigged character and animation-pack gathering to the full unified pipeline.

## Included from previous unified build

- Full Auto Pipeline
- Game Intelligence
- Visual Target
- Free 3D Generator
- Internal Mesh Generator
- PBR Materials
- Photoreal Mode
- Auto Model Gatherer
- Auto GLB/GLTF Importer
- Complete Game EXE Builder
- In-game display settings
- External Playtest / Export
- Rigged Character + Animation Importer

## New in v3.3.1

- Startup Hotfix v3.3.1 panel
- Approved animation manifest
- Create manifest from game plan
- Download enabled rigged character GLB/GLTF assets
- Download enabled animation-pack GLB/GLTF assets
- Store licence metadata
- Run animation importer after gathering
- Missing animation report
- Integrated into Forge Autonomous Game

## Important

GameForge still does not randomly scrape copyrighted assets. It downloads only enabled direct GLB/GLTF URLs from approved manifests with licence metadata. If animations are missing, GameForge uses procedural fallback animation.

## Recommended workflow

1. Paste your game prompt.
2. Click Forge Autonomous Game.
3. GameForge creates model/animation manifests and uses enabled approved assets when available.
4. If no approved rigged assets exist, it uses procedural fallback.
5. Open Animation Gatherer / Animations if you want to inspect reports.
6. Open Studio Editor and Play.
7. Export Complete Game EXE when ready.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Full Unified Build + Rigged Animation Importer

This is the full unified latest build. It includes all previous staged systems plus the new Rigged Character + Animation Importer framework.

## Included in this unified build

- Full Auto Pipeline
- Game Intelligence
- Visual Target
- Free 3D Generator
- Internal Mesh Generator
- PBR Materials
- Photoreal Mode
- Auto Model Gatherer
- Auto GLB/GLTF Importer
- Complete Game EXE Builder
- In-game display settings
- External Playtest / Export
- One-click autonomous forge
- Rigged Character + Animation Importer

## New in v3.3.1

- Animation Importer panel
- Character model profile planning
- Rig/skeleton/animation detection framework
- Animation clip mapping:
  - idle
  - walk
  - run
  - attack
  - hit
  - death
- Zombie/enemy controller assignment
- NPC controller assignment
- Hitbox metadata
- Scale/role metadata
- Procedural fallback animation when clips are missing
- Integrated into Forge Autonomous Game

## Important

If a GLB/GLTF character includes animations, GameForge prepares to map them into gameplay states. If the model has no rig/animations, GameForge uses procedural fallback movement so enemies do not feel completely static.

This is still a prototype framework, but it makes imported zombies/characters much more useful for generated games.

## Recommended workflow

1. Paste your game prompt.
2. Click Forge Autonomous Game.
3. GameForge runs the full unified pipeline.
4. Open Animations if you want to inspect controllers.
5. Open Studio Editor and Play.
6. Export Complete Game EXE when ready.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Startup Hotfix v3.3.1

This update incorporates the model gatherer, auto asset importer, photoreal pass and complete-game preparation into the one-click generation pipeline.

## New in v3.3.1

When you click **Forge Autonomous Game**, GameForge now attempts to run:

1. Game Intelligence
2. Visual Target
3. Downstream asset jobs
4. Model Gatherer manifest creation
5. Approved GLB/GLTF model gathering, if enabled URLs/licences exist
6. Auto GLB/GLTF Importer assignment
7. Base playable scene generation
8. Free 3D fallback asset placement
9. Internal Mesh asset pack
10. Photoreal Mode:
    - lighting/fog
    - PBR material pack
    - scene detail pass
11. Runtime reset / validation
12. Complete Game plan / EXE preparation
13. Studio Editor ready to play

## Important

The model gatherer does not randomly scrape copyrighted assets. It uses approved manifests with direct GLB/GLTF URLs and licence metadata. If no approved model is available, GameForge automatically uses Internal Mesh / Free 3D fallback assets.

## Simple workflow

1. Paste your game prompt.
2. Click **Forge Autonomous Game**.
3. Wait until the checklist completes.
4. Click Play in Studio Editor.
5. Open Complete Game EXE to export/build the game package.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Startup Hotfix v3.3.1

This update adds an approved GLB/GLTF model gathering system.

## New in v3.3.1

- Model Gatherer panel
- Create Approved Model Manifest
- Create Manifest From Game Plan
- Run Model Gatherer
- Gather + Auto Import Pass
- Downloads direct GLB/GLTF URLs only when enabled
- Stores licence metadata
- Copies gathered models into the Asset Library
- Runs Auto GLB/GLTF Importer afterwards
- Uses procedural fallback assets when no model exists

## Important safety rule

GameForge does not randomly scrape copyrighted assets. The gatherer uses approved manifests with direct model URLs and licence metadata. This is safer for commercial use and avoids pulling copyrighted/franchise/famous-likeness assets.

## Workflow

1. Paste prompt.
2. Click Forge Autonomous Game.
3. Open Model Gatherer.
4. Click Create Manifest From Game Plan.
5. Open the manifest file and add direct GLB/GLTF URLs plus licence text.
6. Set enabled=true for approved models.
7. Click Gather + Auto Import Pass.
8. Open Studio Editor and Play.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Startup Hotfix v3.3.1

This update adds an automatic GLB/GLTF asset import and assignment pass.

## New in v3.3.1

- Auto Assets panel
- Scan local GLB/GLTF models
- Create automatic assignment plan
- Run Auto Import Pass
- Integrated into One-Click Autonomous Forge
- Uses imported models first
- Falls back to Internal Mesh / Free 3D when no model exists
- Matches assets by names and requirements:
  - zombie / infected / creature → character/enemy
  - house / building / gas station → building
  - tree / rock / grass → environment
  - weapon / gun / rifle / pistol → weapon
  - supply / medkit / ammo / radio / key / fuse → prop/pickup

## Important

This prepares and attempts GLB/GLTF placement. Rigged character animation support is still a later update. Static models and environment props are the safest first use.

## Recommended workflow

1. Put GLB/GLTF models in your GameForge Asset Library models folder.
2. Paste your game prompt.
3. Click Forge Autonomous Game.
4. GameForge automatically scans and assigns matching models.
5. If no model exists, it uses procedural fallback assets.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Startup Hotfix v3.3.1

This update adds a free photorealism foundation.

## New in v3.3.1

- Startup Hotfix v3.3.1 panel
- Photoreal plan generator
- Cinematic horror lighting/fog
- Bloom/colour grading where runtime supports it
- PBR surface pack
- Wet asphalt / weathered concrete / rusty metal / old wood / zombie skin materials
- Scene detail pass
- CC0/public-domain realistic asset checklist
- Photoreal asset target list
- Exported game graphics option: Photoreal Target

## Important

This makes the generated scene more realistic than the blocky prototype stage. It does not magically create true photoreal people/zombies for free. For that, GameForge still needs high-quality GLB/GLTF assets, scans, or a real 3D generation provider.

## Best workflow

1. Forge Autonomous Game.
2. Open Startup Hotfix v3.3.1.
3. Create Photoreal Plan.
4. Apply Cinematic Lighting/Fog.
5. Create PBR Surface Pack.
6. Apply Scene Detail Pass.
7. Open Studio Editor and Play.
8. Export Complete Game EXE.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — In-Game Display Settings

This update adds proper display settings to the exported full-game runtime.

## New in v3.3.1

- Settings menu inside exported games
- Resolution selector
- PC-aware resolution list based on detected screen size
- Windowed mode
- Fullscreen request
- Borderless-style option
- Graphics quality presets:
  - Low
  - Medium
  - High
  - Ultra
- Apply Settings button
- Settings saved locally
- Back/return button

## Exported game flow

When the exported game opens:

1. It shows the intro:
   Developed by GameForge AI

2. It opens the main menu.

3. Click Settings.

4. Choose resolution/display/quality.

5. Click Apply Settings.

6. Start Single Player / Host Co-op / Join Co-op.

## Note

The runtime can request fullscreen and resize the game viewport. Exact native Electron window resizing can be strengthened later with direct IPC window controls.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Startup Hotfix v3.3.1

This build adds a full-game prototype export system.

## New in v3.3.1

- Complete Game EXE panel
- Create Complete Game Plan
- Export Full Game EXE Package
- Multi-level runtime plan
- Main menu:
  - Start Single Player
  - Host Co-op
  - Join Co-op
  - Settings
  - Quit
- Co-op framework placeholders
- Full-game runtime template
- BUILD_GAME_EXE.bat
- RUN_GAME_WITHOUT_BUILD.bat
- Startup intro screen:
  - “Developed by GameForge AI”

## Workflow

1. Paste your game prompt.
2. Use Forge Autonomous Game.
3. Open Complete Game EXE.
4. Click Create Complete Game Plan.
5. Click Export Full Game EXE Package.
6. Open the exported folder.
7. Double-click RUN_GAME_WITHOUT_BUILD.bat to test.
8. Double-click BUILD_GAME_EXE.bat to create the Windows .exe.

## Important

This creates a full-game prototype runtime and EXE build package. The actual .exe is built on your PC because Electron packaging needs local dependencies.

Full online co-op networking is still a framework placeholder in this version.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Startup Hotfix v3.3.1

This build adds the next staged upgrade: testing the generated game outside the editor and exporting a playable demo package.

## New in v3.3.1

- Playtest / Export panel
- Launch Playtest Window
- Validate Demo
- Export Playable Demo Folder
- Game-only playtest view with HUD/objective/restart/quit
- Demo package export into Documents/GameForgeExportedDemos
- PLAY_DEMO.bat launcher generated for each exported demo
- Demo manifest, scene JSON and runtime JSON
- Validation checklist

## Recommended workflow

1. Paste your game prompt.
2. Click Forge Autonomous Game.
3. Open Playtest / Export.
4. Click Validate Demo.
5. Click Launch Playtest Window.
6. Click Export Playable Demo Folder.

## Important

This is a prototype export framework, not a final standalone game .exe yet. It creates a playable demo folder and launcher. A true standalone .exe build comes after this system is validated.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Startup Hotfix v3.3.1

This build adds the all-in-one button.

## New in v3.3.1

- One-Click Forge panel
- Dashboard quick button: Forge Autonomous Game
- Dashboard quick button: Forge + Auto Play
- Runs the full pipeline automatically:
  - Game Intelligence
  - Visual Target
  - Free 3D Jobs
  - Internal Mesh Recipes
  - PBR Materials
  - Base Playable Scene
  - Free 3D Assets Into Scene
  - Internal Mesh Pack
  - Runtime Reset / Validation
  - Studio Editor Ready

## Recommended workflow

1. Paste your full game prompt into the main Game Description box.
2. Open Dashboard or One-Click Forge.
3. Click Forge Autonomous Game.
4. When it finishes, click Play in Studio Editor.

You can also try Forge + Auto Play.

Important: this is still a prototype automation pipeline. It will make a playable prototype, not a guaranteed AAA photoreal finished game.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Startup Hotfix v3.3.1

This build adds a pre-generation intelligence step so GameForge gathers the necessary information before building the game.

New:
- Game Intelligence panel
- Game brief / scene requirements
- Asset list
- Material list
- Audio list
- Jump scare/combat trigger list
- HUD requirements
- Copyright-safe prompts
- Validation checklist
- One-click downstream job creation for Visual Target, Free 3D, Internal Mesh and PBR material systems

Recommended workflow:
1. Paste game prompt in the main description box.
2. Open Game Intelligence.
3. Click Create Game Intelligence Plan.
4. Click Create All Downstream Jobs.
5. Click Generate Free Assets Into Scene.
6. Open Studio Editor and Play.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Internal Mesh + PBR + GLB Exporter

This build combines the next three upgrades:

## v1.3 Internal Mesh Generator
- Procedural mesh recipes
- Better zombie/humanoid structures
- Boss zombie recipes
- Buildings, gas station, trees, rocks, props and weapons
- Random seed metadata
- Scene placement

## v1.4 PBR Material + Texture Generator
- Wet asphalt
- Weathered concrete
- Rusty metal
- Zombie skin
- Damaged clothing
- Weathered wood
- Mossy rock
- Material metadata/provenance

## v3.3.1 GLB/GLTF Exporter Framework
- Export plan records
- Provenance metadata
- Asset library metadata
- Ready for Babylon GLTF2 exporter integration

Important: this makes the free internal generator more detailed and less blocky, but it is still procedural prototype generation, not full Meshy-level photoreal AI generation.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Visual Target Generator

This version adds the visual target and asset-matching workflow.

New:
- Visual Target panel
- Creates a visual target/concept prompt from the game prompt
- Extracts an asset list from the target
- Creates Free 3D jobs from the target
- Creates AI 3D provider job records from the target
- Generates queued free procedural assets into the scene
- Adds visual target context to Hybrid AI

This lets GameForge work toward a specific look, like a first-person zombie directly in front of the player, abandoned gas station, horror rural town, trees, grass, rocks, buildings, HUD, pistol and survival loop.

Important: this creates the target prompt and asset plan. The actual concept image can be generated externally or later through an image API connector.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Free 3D Generator

This version adds the free version of the online-style 3D generation workflow.

## New in v3.3.1

- Free 3D Generator panel
- Local procedural asset job queue
- Create Free 3D Job
- Create Asset Pack From Game Prompt
- Generate Queue Into Scene
- Generates original procedural prototype assets:
  - zombies/humanoids
  - boss placeholders
  - buildings
  - trees
  - rocks
  - grass
  - props
  - weapon pickups
- Saves generated procedural asset metadata
- No paid API required
- No internet required
- No external copyrighted asset source used

## Important

This is the free/prototype 3D generator. It does not create AAA photoreal GLB people yet. It creates in-engine procedural assets so GameForge can automate playable prototypes without paid 3D generation APIs.

## Best use

Use Free 3D Generator for:
- playable prototypes
- legal/no-cost assets
- fast generation
- internal GameForge demos

Use AI 3D Assets provider pipeline later for:
- real GLB/GLTF generated models
- higher-quality characters/buildings
- provider-based 3D generation



--- Previous notes ---

# GameForge AI Engine v3.3.1 — AI 3D Asset Pipeline

This build starts incorporating the realistic 3D asset workflow into GameForge.

## New in v3.3.1

- AI 3D Assets panel
- 3D provider settings
- Provider API key storage
- Safe asset prompt rewriter
- Prompt-to-asset planning
- Provider job record creation
- GLB/GLTF model URL importer
- Metadata/licence storage for generated assets
- Auto-copy generated models into GameForge Asset Library
- Character Model Importer compatibility
- Hybrid AI context integration
- Copyright-safety policy tools

## Important status

This is the connector framework. To generate real 3D models automatically, you still need an API key/account from a 3D asset generation provider such as Meshy, Tripo, Scenario, or another GLB-capable provider.

Provider-specific automatic submission is prepared as a job-record framework. Exact endpoint submission can be added once the chosen provider/API account details are confirmed.

## How to use the new 3D pipeline

1. Open AI 3D Assets.
2. Choose provider.
3. Save provider settings/API key.
4. Click Create Asset Plan.
5. Click Create Provider Jobs.
6. Use the safe prompts with your 3D provider.
7. Import the returned GLB/GLTF model URL.
8. Scan Asset Library / Character Models.
9. Generate or update the game.

## Copyright safety

The prompt rewriter avoids:
- copyrighted franchises
- famous actors/celebrity likenesses
- exact replicas
- branded products

Use original descriptions and verify commercial-use rights before release.



--- Previous notes ---

# GameForge AI Engine v3.3.1 — Stable Prototype Cleanup

This build focuses on stability and demo-readiness rather than adding more features.

## What was cleaned up

- Stable GPU-safe startup
- Smaller default window size
- Reduced UI effects
- Scrollable sidebar
- Dashboard stable prototype controls
- Play Mode reset helpers
- Reliable WASD movement fallback
- `generateRealisticEnvironment()` stability fallback
- Cleaner installer script
- One-click stable start script
- Updated docs and clear prototype status

## Recommended way to run

Extract the zip, then double-click:

```text
START_GAMEFORGE_STABLE.bat
```

Or install it by double-clicking:

```text
INSTALL_GAMEFORGE.bat
```

## First test flow

1. Open Dashboard.
2. Click **Generate Stable Playable Prototype**.
3. It should open Studio Editor.
4. Click **Play**.
5. Click inside the viewport.
6. Test WASD movement.

## Honest status

This is a stable prototype cleanup build, not a finished commercial AI game engine yet. It is suitable for testing the concept, showing the workflow, and preparing a demo.



--- Previous notes ---

## v3.3.1 Installer Copy Fix

Fixes:
- Replaces robocopy installer copy with a PowerShell copy method.
- Checks package.json copied correctly.
- Adds install log output if copy fails.
- Adds CREATE_DESKTOP_SHORTCUT_ONLY.bat.
- Creates Desktop and Start Menu shortcuts after install.

Use:
1. Extract zip.
2. Double-click INSTALL_GAMEFORGE.bat.
3. If shortcut fails, run CREATE_DESKTOP_SHORTCUT_ONLY.bat.

## v3.3.1 Windows Installer Launcher

New:
- INSTALL_GAMEFORGE.bat
- RUN_INSTALLER_FIRST.txt
- Desktop shortcut creation
- Start Menu shortcut creation
- Installed launcher in Local AppData
- Uninstall script
- Optional NSIS installer script
- BUILD_REAL_INSTALLER_EXE.bat

How to install:
1. Extract the zip.
2. Double-click INSTALL_GAMEFORGE.bat.
3. Follow the prompts.
4. Use the Desktop shortcut: GameForge AI Engine.

Note:
This is a Windows installer launcher script. A fully compiled signed .exe installer needs to be built on Windows with NSIS or Electron Builder.

## v3.3.1 Windows Installer Launcher

New:
- START_GAMEFORGE.bat
- BUILD_GAMEFORGE_PORTABLE_EXE.bat
- BUILD_GAMEFORGE_INSTALLER.bat
- README_START_HERE.txt

You no longer need to manually type npm install and npm start.
Just double-click START_GAMEFORGE.bat.

For a true standalone .exe, double-click BUILD_GAMEFORGE_PORTABLE_EXE.bat and then open the generated .exe in the dist folder.

## v3.3.1 Windows Installer Launcher

This is a stronger fix for the app freezing when dragging or relocating the window.

Changes:
- Disables Electron hardware acceleration by default.
- Disables GPU compositing/rasterization by default.
- Disables accelerated 2D canvas by default.
- Disables Windows occlusion/compositor features that can stall Electron windows.
- Starts at a smaller 1280x800 window instead of a huge window.
- Uses a normal native Windows frame.
- Removes heavier mock viewport visuals.
- Adds safe launch batch files:
  - START_GAMEFORGE_SAFE_MODE.bat
  - START_GAMEFORGE_CPU_MODE.bat
  - RESET_AND_START_GAMEFORGE.bat

Recommended:
Run `npm install`, then either:
- `npm start`
or double-click:
- START_GAMEFORGE_CPU_MODE.bat

## v3.3.1 Always-On Smooth Window Mode

Change:
- Smooth Window Mode is now built into the app by default.
- You no longer need to click Enable Smooth Window Mode.
- Heavy animations/shadows/filters are reduced automatically.
- Electron performance flags from v3.3.1 remain enabled.
- Optional Restore Visual Effects button is still available if wanted.

This should make moving, dragging and resizing the app smoother by default.

## v3.3.1 Window Drag Performance Hotfix

Fixes/improvements:
- Adds Electron renderer responsiveness switches.
- Disables renderer background throttling.
- Adds BrowserWindow backgroundThrottling: false.
- Adds Smooth Window Mode on Dashboard.
- Smooth Window Mode reduces animations, shadows and heavy visual effects.
- Helps when the app feels frozen while dragging/resizing the window.
- Keeps v3.3.1 sidebar hotfix and v3.3.1 Hybrid AI Connector.

If dragging the whole app feels laggy:
1. Open Dashboard.
2. Click Enable Smooth Window Mode.
3. Try dragging/resizing again.

## v3.3.1 Sidebar Navigation Hotfix

Fixes:
- Makes the left sidebar scrollable.
- Adds visible scrollbar styling.
- Keeps menu items from shrinking off-screen.
- Adds Dashboard Quick Access buttons for:
  - AI Cost Estimator
  - Hybrid AI
  - Forge Wizard
  - Studio Editor
- Active menu item auto-scrolls into view.

Use this build if you cannot scroll the left side menu.

## v3.3.1 Hybrid AI Connector

New features:
- Hybrid AI panel
- OpenAI API key settings
- Local AI / OpenAI / Hybrid mode selector
- Model selector
- API connection test
- Cost estimate approval gate
- Generate With Hybrid AI button
- Structured JSON prompt-to-game request
- Converts Hybrid AI response into GameForge scene data
- Displays provider usage/cost estimate when returned
- Falls back/recommends Local AI if blocked or missing API key

Important:
API usage is billed through the user's OpenAI Platform/API account, not through ChatGPT Plus. GameForge stores the API key locally for prototype use.

## v3.3.1 AI Cost Estimator

New features:
- AI Cost Estimator panel
- Small draft / playable demo / polished prototype / ambitious game profiles
- Cheap / Balanced / High Quality modes
- Repair/test pass option
- Release-readiness review option
- Asset/licence planning option
- Overnight/batch estimate option
- Approval note for future Hybrid AI runs
- Cost policy screen

Important:
This version estimates future AI/API usage but does not call a paid AI API yet. It prepares GameForge for v3.3.1 Hybrid AI.

## v3.3.1 Curated CC0 Asset Downloader

New features:
- Curated CC0 Assets panel
- Approved source registry
- Asset need planning from game prompt
- Direct URL asset downloader with licence gate
- CC0/Public Domain only default
- Blocks unknown licence
- Blocks NonCommercial assets
- Blocks non-approved source domains
- Saves licence metadata/proof
- Copies approved assets into GameForge Asset Library
- Runs with Character Model Importer, Asset Pack Manager and Licence Auditor

Important:
This greatly reduces copyright/licence risk, but no app can guarantee legal clearance automatically. Before commercial release, verify each asset source/licence manually or with legal advice.

## v3.3.1 Local AI Character Model Importer

New features:
- Adds Characters panel.
- Scans imported GLB/GLTF models for human/zombie/survivor/boss keywords.
- Passes imported character model references into Local AI generation.
- Allows Local AI to reference exact `ImportedAsset.relativePath` values.
- Adds auto-assign imported character models to current enemy placeholders.
- Keeps procedural humanoid fallback if no character models are imported.

Important: GameForge can now use realistic character models you import, but it does not legally download paid or restricted models automatically. Only import models you own or models licensed for your intended use.

## v3.3.1 Movement + Character Hotfix

Fixes:
- Adds forced WASD keyboard movement controller.
- Re-attaches camera controls on every Play start.
- Keeps pointer lock/mouse look.
- Adds safer camera/player reset.
- Adds less-blocky humanoid enemy placeholders.
- Keeps v0.7.2 polished UI and v0.7.2.1 playmode fixes.

Important: these are improved procedural prototype characters, not true photoreal humans. True realistic characters require imported GLB characters, Mixamo-style animations, or a future advanced asset pipeline.

## v3.3.1 PlayMode Hotfix

This hotfix adds the missing `generateRealisticEnvironment()` function and improves Play Mode reset behaviour.

Fixes:
- Missing `this.generateRealisticEnvironment is not a function` crash
- Playable prototype generation failing
- Player spawning into instant fail state
- Play Mode now resets health, stamina, ammo and camera
- Adds short spawn protection so enemies do not kill the player immediately
- Places enemies farther from the spawn point

## v3.3.1 UI Polish Update

This version updates GameForge's interface closer to the polished mockup style while keeping all v0.7.1 features.

New additions:
- Version updated to v3.3.1
- Polished dark navy/charcoal interface
- Blue/teal glowing accents
- Improved card styling
- Improved buttons, inputs and dropdowns
- Polished Forge Wizard styling
- Project health cards styling
- Pipeline progress styling
- Mock viewport preview styling

# GameForge AI Engine v3.3.1 PlayMode Hotfix

GameForge AI Engine v3.3.1 PlayMode Hotfix is a standalone desktop prototype for an AI-first game-generating engine.

It is **not** a plug-in for Unreal, Unity, Godot or Roblox. It uses Electron for the desktop shell and Babylon.js for the internal 3D viewport/runtime prototype.

## What this version adds over v0.4

- Offline Realism Pack
- Generate Realistic Environment
- Procedural grass patches
- Procedural rocks
- More natural tree placeholders
- Human scale placeholders
- Realistic daylight/fog atmosphere preset
- Realism asset plan for future licensed model packs

## What v0.4 added over v0.3

- Hypergame Mode
- Forge Mode staged offline generation
- Studio Editor improvements
- Scene/project save foundation
- Project load foundation
- Export JSON
- Export Playable Draft folder
- Transform controls for selected objects
- Duplicate/delete selected objects
- Component-style object data
- Terrain presets
- Material controls
- Gameplay system generator
- Asset Forge planner
- Test Agent
- Consolidated documentation and developer handoff

## Current reality

This version uses an offline template generator. It simulates the AI workflow so the app can be tested without paid API costs.

It does **not** yet create a finished hyper-realistic game automatically. That requires the future real AI API/cloud generation layer.

## How to run

1. Install Node.js
2. Unzip this folder
3. Open a terminal/Command Prompt inside the folder
4. Run:

```bash
npm install
npm start
```

## Controls

### Edit Mode
Use mouse to orbit, pan and zoom.

### Play Mode
Click **Play**.
Use:
- W A S D to move
- Mouse to look
- Left click to shoot
- R to reload
- E to interact/pick up loot
- Shift to move faster

Before testing audio, click **Enable Audio** in the Playable Prototype or Audio Pack panel.

Click **Stop** to return to edit mode.

## Recommended next build

### v0.5 — Real AI Connection
- OpenAI API service layer
- Prompt-to-scene JSON
- Prompt-to-component data
- Prompt-to-gameplay logic
- AI validation and repair
- Usage/cost tracking

### v0.6 — Real Runtime Systems
- Player controller
- Weapons
- Enemy AI
- Inventory
- Crafting
- Skill tree
- Quests
- Save/load gameplay

### v0.7 — Asset Generation Pipeline
- AI textures
- AI model generation integration
- Sound generation
- Asset importer
- Material editor

### v3.3.1
- Public standalone beta

## v3.3.1 Seamless Forge Wizard

This version makes the full generation workflow much more seamless.

New additions:
- Forge Wizard panel
- One-click full game pipeline
- Game genre presets
- Progress screen
- Auto-repair pass
- Project Health Dashboard
- Build Readiness Gate
- Make It Better button
- Automatic local media option
- Asset scan option
- Licence audit option
- Build validation option
- Optional native package export

The goal is to make the workflow feel like:

Describe game → choose preset → click Forge Full Game Package → test/export.

## v0.7 Native EXE Exporter

This version adds the native Windows .exe package workflow.

New additions:
- Native EXE Export panel
- Generate Full Game Package button
- Native package validation
- Export Native EXE Package
- Standalone game package folder
- Runtime Electron game shell
- build_windows.bat
- electron-builder config
- 1920x1080 runtime target
- Game data, scene data and component export
- Native build README

Important:
v0.7 prepares a Windows .exe package workflow. On your Windows PC, open the exported package folder and run build_windows.bat to create the .exe in the dist folder.

This still needs QA and polish before being treated as a finished commercial game.

## v0.6 Playable Game Build System

This version moves GameForge toward a more complete playable game draft.

New additions:
- Build System panel
- 1920x1080 target resolution setting
- Build quality target setting
- Complete playable draft generator
- Build validation checklist
- Playable build export folder
- Runtime pause overlay
- Menu plan generation
- Save/load plan generation
- Prototype runtime save/load buttons
- Build README and data export

Important:
v0.6 exports a playable draft data package and launch information page. It does not yet create a fully packaged native .exe game. Native executable packaging is a later stage.

## v0.5.5 Licence Auditor

This version adds a Licence Auditor and credits manager.

New additions:
- Licence Auditor panel
- Gather known licence/credit metadata
- Classify pasted licence text
- Audit current asset library and generated media
- CC0/Public Domain only audit mode
- Commercial-safe audit mode
- Attribution-allowed audit mode
- Block obvious NonCommercial/Unknown assets
- Generate LICENCE_AUDIT.md
- Generate CREDITS_ALL_ASSETS.md
- Store JSON audit reports

This is not legal advice. It is a safety workflow that helps GameForge gather the necessary licensing information and avoid obvious licence mistakes.

## v0.5.4 Legal Sound Finder

This version adds a legal-use sound sourcing layer.

New additions:
- Legal Sound Finder panel
- Licence mode selector
- CC0/Public Domain only mode
- Commercial-use allowed mode
- Attribution-required mode
- NonCommercial blocker
- Unknown licence blocker
- Prompt-based required sound query generation
- Auto-find approved sounds for common gameplay events
- Approved sound result list
- Download approved sounds
- Generate CREDITS_AUDIO.md

Default behaviour prioritises CC0/Public Domain sounds. Users still need to verify licences before commercial release.

## v0.5.3 Local Media + Web Sound

This version adds automatic local media generation and an optional web sound finder.

New additions:
- Generate local placeholder textures
- Generate local placeholder UI icons
- Generate local placeholder audio
- Auto-generate media during autonomous game generation
- Pass generated media context into Local AI prompts
- Web Sound Finder panel
- Freesound-style API token support
- Search web sounds by query
- Preview sound results
- Download selected sound previews with licence metadata

Important:
The built-in generator creates placeholder media directly on your PC. The Web Sound Finder is optional and requires your own API token/source compliance. Always check sound licences before commercial use.

## v0.5.2 GLB Model Reference

This version adds individual GLB/GLTF model referencing and placement.

New additions:
- Detect imported .glb / .gltf models
- Display a GLB/GLTF Model References list
- Place individual imported models into the scene
- Copy model reference snippets
- Pass individual GLB/GLTF references into Local AI prompts
- Allow Local AI scene JSON to use ImportedAsset.relativePath
- Attempt to load GLB/GLTF models visually through Babylon.js loaders
- Create a proxy placeholder if a model cannot be loaded

Important:
This is the first imported-model placement layer. Some GLB/GLTF files may need scale/rotation/material adjustments.

## v0.5.1 Asset Pack Manager

This version adds an Asset Pack Manager so GameForge can import, download, scan and use asset packs during autonomous local AI generation.

It supports:
- Downloading files/packs by URL
- Importing local asset files
- Scanning the GameForge asset library
- Saving asset manifests
- Passing asset library context into the local AI prompt
- Letting generated objects reference imported assets through component data

Recommended formats:
- 3D models: .glb / .gltf first
- textures: .png / .jpg / .webp
- audio: .wav / .mp3 / .ogg
- packs: .zip

Important:
Only download/import assets you own or assets with a licence that allows your intended use.

## v0.5 Autonomous Local AI

This version adds local AI as the main generator.

It is designed so you can type a prompt, click **Autonomously Generate Playable Game**, and the app will ask your local model to create:

- game title and summary
- objective
- 3D scene object list
- terrain/environment objects
- trees, grass, rocks, buildings and props
- enemies
- loot and weapon pickups
- crafting station
- gameplay logic
- audio plan
- test plan

The app then validates the model output and builds the scene inside the GameForge editor.

### Important visual note

The generated trees, rocks, grass and terrain are recognisable 3D/procedural placeholders, not pixel art. They are not yet full photoreal scanned assets. For true natural-looking trees and terrain, a future version should add either a licensed realistic asset library or a local/cloud 3D asset generator.

### Local AI setup

Recommended default:

```bash
ollama pull llama3.1:8b
ollama run llama3.1:8b
```

Default endpoint:

```text
http://localhost:11434/api/generate
```

## Audio Pack Additions

v0.4.3 adds offline placeholder sound files and gameplay audio triggers:

- gunshot
- reload
- pickup
- enemy hit
- player hurt
- footstep
- UI click
- objective complete
- enemy growl placeholder
- ambient wind loop

These are generated placeholder WAV sounds. They are not production-quality, but they make the prototype feel more alive and testable.

## Playable Prototype Additions

v0.4.2 adds a more game-like survival prototype loop:

- HUD
- health
- stamina
- ammo
- shooting
- reload
- enemy chase/attack
- loot pickup
- medkit/ammo pickup
- interaction with E
- score
- simple survival objective
- enemy wave spawning

This makes the editor test mode feel closer to a playable draft instead of only a 3D walkthrough.

## Realism Pack Notes

This version does not create full AAA graphics or true photoreal humans offline. Instead, it adds a practical offline realism foundation:
- grass
- trees
- rocks
- terrain presets
- lighting/fog
- everyday human placeholders
- material presets

For truly lifelike humans, trees, grass, rocks and terrain, future versions should include a licensed offline asset library or connect to a real AI asset-generation service.


## v3.3.1 Game Style + Rating Selector
This version adds an automatic photoreal / realistic asset planning pass tied into the autonomous generation button.


## v3.3.1 Game Style + Rating Selector
Photoreal realism prep now runs automatically for every generated game. No toggle or special graphics setting is required.


## v3.3.1 Game Style + Rating Selector
The main generation flow is now one-click autonomous with an estimated time, live progress bar and stage checklist.


## v3.3.1 Game Style + Rating Selector
GameForge now includes a licence-aware asset acquisition layer that can download approved direct asset URLs, store licence metadata, import assets, and fallback safely when no approved assets are available.


## v3.3.1 Game Style + Rating Selector
GameForge now includes a local self-asset generator that creates procedural models/descriptors, PBR-style textures, icons, audio placeholders and metadata when approved downloads are missing.


## v3.3.1 Game Style + Rating Selector
GameForge now includes a dedicated audio generator that creates procedural WAV files for ambience, footsteps, creaks, ghost sounds, enemy sounds, UI and jumpscare events when approved audio is unavailable.


## v3.3.1 Game Style + Rating Selector
GameForge now runs a quality gate that scores real models, PBR materials, lighting, scene dressing, animation, audio and optimization before labelling a build photoreal. Low-quality fallback visuals are labelled Prototype Only or AA Candidate instead of Photoreal Ready.


## v3.3.1 Game Style + Rating Selector
GameForge now detects and prepares automation for Blender, Godot, FFmpeg and Unreal handoff, creates legal asset policy files, Blender processing scripts, Godot export presets, Unreal handoff packages and build readiness reports.


## v3.3.1 Game Style + Rating Selector
GameForge now prepares Meshy-ready prompt packs, import folders and attribution templates so you can test Meshy-generated high-quality 3D assets before paying for a subscription/API workflow.


## v3.3.1 Game Style + Rating Selector
Adds Meshy API connector structure with free-test fallback and a Simple Mode UI that hides advanced panels behind a More button.


## v3.3.1 Game Style + Rating Selector
Ran a fine-toothed static sweep over JavaScript syntax, frontend/backend API wiring, Meshy API guards, Simple Mode startup, and toolchain helpers. See STABILITY_SWEEP_REPORT_v3_0_1.md.


## v3.3.1 Game Style + Rating Selector
Adds Unreal project/handoff package generation with Meshy-to-Unreal manifests, PBR material plans, Lumen/Nanite notes, post-process plans, scene dressing density and build readiness reports.


v6.8.2 combines the working GameForge UI/PC app with the uploaded v12.1 Intricate Gameplay Systems Architect package. Imported source files are preserved in Imported_v12_1_Intricate_Gameplay_Systems, and a bridge module integrates gameplay architecture into the main pipeline.


v6.8.2 test pass completed. See GameForge_App/README_TEST_PASS_v6_5_1.txt for checks and results.
