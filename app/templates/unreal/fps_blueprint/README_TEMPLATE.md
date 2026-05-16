# FPS Blueprint Template — GameForge AI

## Status: Template Not Installed

This folder is the expected location for the **FPS Blueprint** Unreal Engine template.

GameForge will detect whether a real template exists here by looking for a `.uproject` file.
If no `.uproject` is found, GameForge falls back to generating a Blueprint-only project shell.

---

## How to Install a Real FPS Template

1. Create or export a working Blueprint-only Unreal 5 project with the following systems:
   - First-person player pawn with capsule collision
   - First-person camera (spring arm + camera component)
   - First-person weapon mesh with fire and reload logic
   - Line trace or projectile damage system
   - Health component (player and enemy)
   - Basic AI enemy (patrol → chase → attack behavior tree)
   - HUD Widget Blueprint (health bar, ammo counter)
   - Game Mode Blueprint (wave logic, win/lose conditions)
   - Starter map (Content/Maps/StarterMap.umap)
   - Input mappings (WASD, mouse look, fire, reload, jump)
   - No C++ modules — Blueprint-only
   - No non-standard plugins

2. Copy the entire Unreal project folder contents into this directory:

```
app/templates/unreal/fps_blueprint/
  MyFPSTemplate.uproject       ← required for detection
  Config/
    DefaultEngine.ini
    DefaultGame.ini
    DefaultInput.ini
  Content/
    Maps/
      StarterMap.umap          ← required: at least one playable map
    Blueprints/
      Player/
        BP_PlayerCharacter.uasset
        BP_PlayerController.uasset
      Enemies/
        BP_EnemyCharacter.uasset
        BP_EnemyController.uasset
      Weapons/
        BP_WeaponBase.uasset
      UI/
        WBP_HUD.uasset
    Characters/
    Weapons/
    Audio/
    Materials/
    Meshes/
```

3. Once installed, GameForge will:
   - Copy this template folder to the user's output path
   - Rename the `.uproject` to match the user's chosen project name
   - Update Config files with the new project name
   - Add GameForge Docs, Output, Scripts, and Scenes folders
   - Report result type as "Playable Template"

---

## Recommended Sources for a Free FPS Blueprint Template

- Unreal Engine Marketplace (Free section) — search "First Person Blueprint"
- Unreal Engine built-in templates — create a "First Person" project in the Unreal Editor launcher
  and export the resulting folder here
- Epic Games "First Person Shooter" sample project (Blueprint only variant)

---

## Important Notes

- Do NOT include C++ source files or a `Modules` array in the `.uproject`
- Do NOT include plugins that are not part of a standard UE5 installation
- The template must open in Unreal Engine 5.4+ without errors or missing plugin warnings
- Test the template in Unreal before placing it here
