# Blender creation, Unity integration and cross-game reuse

Requested target: ForgeCore should create the game's 3D asset set in Blender, integrate it into Unity and reuse suitable work in subsequent games. The first bounded local adapter is implemented: one original weathered-barrel recipe. The whole-game pipeline below remains a roadmap, not a claim of general autonomous art production.

## Current evidence

- Installed `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` executed the bundled original recipe; the actual reported version is 5.2.1 LTS. No installation/download or provider request was needed.
- ForgeCore already has exact-hash reviewed-vault storage/search/import tools, Unity generated-GLB preparation/inspection/preview, and game-production role/style searches.
- The existing vault requires current technical evidence plus separate live human visual/rights records. No such records have been fabricated for the watchtower or any new content.
- `CreateBlenderAsset` and `ImportBlenderAsset` now provide typed creation/exact-match reuse and separately approved non-overwriting imports. The Desktop prompt `Create a weathered barrel in Blender. Name it yard-barrel.` deterministically routes creation, import, bridge installation, compilation, structural inspection and four-view preview without loading a local language model. It stops short of human acceptance and scene placement.
- The IndustrialYardV1 environment remains original Unity procedural geometry. One local Blender barrel has been imported and placed in a separate test scene; this is not a whole Blender-generated environment.

## Implemented local contract

- Configure existing absolute `BlenderPath`, `BlenderVersion: "5.2"` and `BlenderTimeoutSeconds` (30–1200). A fresh install's example path is empty: execution fails closed until configured. This workstation is configured to its installed executable.
- Only `weathered_barrel_v1` is supported. Inputs are project, bounded lowercase name, seed 0–999999, height 1000–1500 mm and radius 250–500 mm. No arbitrary script, file, URL, output path, plugin or command argument is accepted. Explicit unsupported Blender creation requests stop instead of falling through to cloud.
- The recipe is embedded in the Tools assembly and copied into a fresh run directory. Blender starts background/factory/disable-autoexec/offline with two CPU threads, a time limit, bounded output and only an allow-listed process environment. No provider credentials are inherited. Blender/Python is still not an OS sandbox. Unity and Blender must be closed at the external-process check; ForgeCore never kills user-owned editors.
- Runs are serialized within the adapter and by a cross-process library lease. A stale lease stops work rather than being removed silently. Project operations also use the executor's existing per-project lease; interactive editors launched concurrently by another application cannot be prevented by this check.
- The library is workspace `.forgecore/blender-library`: isolated runs/logs, recipe references, and content-addressed objects. Objects retain `.blend`, `.glb`, studio preview, three packed source PBR PNGs, measured metrics, exact request and recipe. Entry hash, every file hash/length, binary/recipe/request identity, GLB structure, PNG envelopes, geometry budget, dimensions and bottom pivot are checked. The recipe uses image-based albedo/roughness/metallic textures, UVs, rolled rims, ribs, recessed lid and bung details. No normal-map bake or LOD is provided yet.
- An exact request (including name, dimensions, seed, recipe hash, executable hash/version) reuses its verified bundle without rerunning Blender. Import by exact bundle and GLB hashes can copy that source into another fresh project destination; neither existing files nor the source bundle are overwritten. The local provenance uses an explicit `forgecore://local-blender/` origin, not a fabricated download URL or licence.
- All bundles remain `unreviewed_local_recipe`. This is separate from the existing human-reviewed GLB vault; it neither manufactures visual/rights attestations nor makes the local source automatically eligible for approved production-vault selection. Cross-game semantic suitability search, source-aware reviewed-vault migration, variations and approval UI remain future work.
- Cancellation stops the owned Blender process tree. Before atomic publication it leaves only an isolated partial run for diagnosis; once publication commits, the complete immutable bundle is retained and its tiny reference write is finished. A cancelled task does not proceed to a dependent import. Startup, geometry, appearance and runtime checks are distinct. The app prompt does not choose an arbitrary existing scene or silently replace a placeholder.
- Kit 0.16.33 limits the procedural material pass to explicitly `Yard.<Finish>.*` renderers, preserving imported multi-material PBR slots/textures. Known 0.16.32 installations upgrade only by exact hashes with a protected prior-version backup.

## Intended production lifecycle

1. Split the game brief into an asset manifest: environment modules, props, gameplay items, characters, rigs, animation, weapons, vehicles, effects meshes and relevant interface models. Keep audio, music, dialogue and 2D UI work in their own pipelines.
2. Search the reviewed library first using role, style, dimensions, intended use, pivot/orientation, material/texture limits, collision, LOD and animation requirements. No unsuitable asset should be chosen merely because it exists.
3. Reuse an exact approved version, derive a new separately identified variation, or create a missing asset using a bounded reviewed Blender recipe. The model should choose typed parameters, not supply arbitrary executable Python or load an untrusted blend file.
4. Run the installed Blender serially with Unity-heavy jobs, with a time limit and normal-user privileges. A trusted versioned bridge starts from factory settings, disables automatic script execution and permits only bounded workspace input/output. Blender/Python is not inherently a security sandbox; typed input validation is not a substitute for OS isolation.
5. Retain the editable `.blend` source, exported self-contained `.glb`, preview and provenance. Bake/prepare compatible image-based PBR textures where required; do not assume every Blender shader node transfers to Unity. Validate mesh bounds, transforms, topology/material limits, external URI absence and file hashes before import.
6. Import into a fresh Unity destination, verify materials, scale, collision, LOD/rig/animation conventions and rendered appearance. Create game-specific prefabs and placements without altering the immutable source library entry.
7. Store accepted assets and their evidence as versioned content-addressed bundles. Existing vault schema must be extended to retain Blender source/recipe identity and additional suitability metadata; the current GLB-only vault does not already provide those extensions.
8. Validate the placed scene and exact game build. Keep dependencies and visual/rights review states explicit; failed or unreviewed work cannot be labelled approved library content. Previously accepted versions remain recoverable.

## First bounded implementation milestone

Create an original industrial prop kit: barrel, wooden crate, concrete barrier, fence module and corrugated cargo container. Use deterministic dimensions/material parameters, UVs, bevelled geometry, collision/pivot metadata and GLB export. Start with one prop end-to-end before expanding the kit.

Acceptance must demonstrate:

- repeatable recipe execution with bounded mesh/material counts and grounded canonical transforms;
- correct Blender version/bridge/recipe/output hashes and source preservation;
- schema rejection for unknown operations, arbitrary paths/scripts, modified bridges and occupied outputs;
- cancellation/timeouts without leaving a running process or silently continuing a dependent import;
- real Unity import, collision/scale/material inspection and a rendered in-game preview;
- reviewed library storage and suitability search;
- import of that same content hash into a second fresh project/game destination, without remodelling or changing the source bundle;
- a playable Windows delivery with unresolved quality requirements stated honestly.

Characters, rigging/skinning, locomotion/combat animation, organic terrain, detailed hero weapons and art-direction iteration are later separate milestones. Blender can produce these classes of content, but the quality of autonomous output depends on the implemented recipes/models, training/reference assets, integration and verification—not the presence of Blender alone.

## Primary references

- Blender command-line operation: https://docs.blender.org/manual/en/5.0/advanced/command_line/arguments.html
- Blender glTF export, materials and animation: https://docs.blender.org/manual/en/5.1/addons/import_export/scene_gltf2.html
- Blender scripting security: https://docs.staging.blender.org/manual/en/latest/advanced/scripting/security.html
