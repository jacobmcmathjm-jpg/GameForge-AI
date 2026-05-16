# GameForge AI — Unreal Blueprint Templates

This folder contains Blueprint-only Unreal Engine 5 template projects used by GameForge
to generate playable game prototypes.

## How Templates Work

When a user generates a game in GameForge:

1. GameForge checks this folder for a matching template (based on game type)
2. If a template `.uproject` file is found, GameForge copies the full template
3. GameForge renames the project and updates config/docs/reports
4. Result is reported as: **Playable Template**

If no template is installed, GameForge generates a Blueprint-only project shell
(correct folder structure, config files, docs — but no gameplay assets).
Result is reported as: **Project Shell**

## Template Folders

| Folder | Game Type | Status |
|--------|-----------|--------|
| fps_blueprint/ | First-Person Shooter | Not installed |
| zombie_shooter_blueprint/ | Zombie Shooter | Not installed |
| horror_blueprint/ | Horror | Not installed |
| survival_blueprint/ | Survival | Not installed |
| racing_blueprint/ | Racing | Not installed |
| rpg_blueprint/ | RPG | Not installed |
| open_world_blueprint/ | Open World | Not installed |

## Installing a Template

See the `README_TEMPLATE.md` file inside each folder for specific instructions.

General steps:
1. Create a working Blueprint-only Unreal 5 project with the required gameplay systems
2. Verify it opens without errors or missing plugin warnings
3. Copy all project files into the matching folder here
4. GameForge will detect the template on next generation

## Requirements for All Templates

- No C++ source files
- No `Modules` array in `.uproject`
- No non-standard or missing plugins
- Must include at least one playable map (Content/Maps/*.umap)
- Must open in Unreal Engine 5.4+ without warnings
- All Blueprint assets must be baked into the project (no external dependencies)
