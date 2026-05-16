# Zombie Shooter Blueprint Template — GameForge AI

## Status: Template Not Installed

This folder is the expected location for the **Zombie Shooter Blueprint** Unreal Engine template.

GameForge will detect whether a real template exists here by looking for a `.uproject` file.
If no `.uproject` is found, GameForge falls back to generating a Blueprint-only project shell.

---

## How to Install a Real Zombie Shooter Template

1. Create or export a working Blueprint-only Unreal 5 project with the following systems:
   - First-person player pawn with capsule collision
   - First-person camera (spring arm + camera component)
   - First-person weapon (pistol/rifle) with fire, reload, ammo logic
   - Line trace or projectile damage system
   - Health component for player and zombies
   - Zombie AI enemy with:
     - Sight and hearing perception (AIPerceptionComponent)
     - Behavior Tree: Patrol → Chase → Attack
     - Melee attack with damage on AnimNotify
     - Death ragdoll / destroy sequence
   - Zombie spawner with wave escalation timer
   - Wave Manager (wave count, enemies remaining, wave clear event)
   - HUD Widget Blueprint (health bar, ammo, wave number, enemies left)
   - Game Mode Blueprint (wave start, game over, score)
   - Starter map (Content/Maps/StarterMap.umap)
   - Input mappings (WASD, mouse look, fire, reload, jump, interact)
   - No C++ modules — Blueprint-only
   - No non-standard plugins

2. Copy the entire Unreal project folder contents into this directory:

```
app/templates/unreal/zombie_shooter_blueprint/
  MyZombieTemplate.uproject    ← required for detection
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
        BP_ZombieCharacter.uasset
        BP_ZombieController.uasset
        BT_ZombieBehavior.uasset
      Weapons/
        BP_WeaponBase.uasset
        BP_Pistol.uasset
      UI/
        WBP_HUD.uasset
        WBP_GameOver.uasset
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

## Recommended Sources

- Unreal Engine Marketplace (Free section) — search "Top Down Shooter Blueprint" or "Zombie Blueprint"
- Unreal Engine built-in Third Person template adapted for zombie gameplay
- Epic Games "Lyra Starter Game" (Blueprint portions — requires careful filtering)
- Community zombie shooter sample projects (verify they are Blueprint-only and plugin-free)

---

## Important Notes

- Do NOT include C++ source files or a `Modules` array in the `.uproject`
- Do NOT include plugins that are not part of a standard UE5 installation
- The template must open in Unreal Engine 5.4+ without errors or missing plugin warnings
- Test the template before placing it here — open it, press Play, and verify basic combat works
- All assets must be imported/baked into the project (no external dependencies)
