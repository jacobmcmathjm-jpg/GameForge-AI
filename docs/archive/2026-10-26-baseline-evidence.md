# ForgeCore baseline evidence — 26 October 2026

Archived starting point for outcomes plan v1. This is a summary of observed local acceptance, not a claim that every planned outcome is complete. No new game generation is run merely by writing this document.

## Accepted prototype evidence

- Host Release tests: Core 119, Agent 37, Security/Tools 103 — **259/259**.
- Unity 6000.5.5f1: **16/16 EditMode and 8/8 PlayMode**; bridge 0.17.15; template kit 0.16.33.
- Blender 5.2.1 LTS executed one original bounded weathered-barrel recipe: 3,376 source vertices, 6,540 triangles, two materials, packed PBR textures, 1.1 m height and grounded bottom pivot.
- Exact immutable source reuse and separate non-overwriting imports succeeded. Unity measured 5,715 split vertices, 6,540 triangles and approximately 5.68 MB texture memory.
- A separate new FPS test scene contains the barrel at uniform scale 1 and one box collider; gameplay checks passed movement 2.50 m, respawn and all 6 objectives.
- The in-game render retained the barrel's green/rust PBR materials after fixing a global procedural-material overwrite bug. Both review/game previews recorded 0% error-magenta samples; this does not establish artistic quality.
- The development Windows player survived a ten-second startup check. A copied delivery survived five seconds of headless startup; complete delivery manifests and the copied app's offline self-check passed.
- A real two-second cancellation test stopped its owned Blender process, left the committed bundle set unchanged and released its lease.

## Performance qualification and unresolved issue

The separate ten-second rendered scripted profile at 1280×720 recorded 16,509 frames, p95 frame interval 0.8122 ms, p95 GPU 0.3871 ms, peak allocated memory 179,019,143 bytes, 61.66 m travel and 12 input actions. Combat logs recorded 10 player bullets, 40 enemy shots, 7 player reload poses and 8 enemy reloads.

**Maximum frame interval: 1,727.84 ms. Cause not diagnosed.** Passing the current p95 workstation gate is not proof of smoothness or other-hardware/production-content acceptance. The scripted profile completed **0/6 objectives**; the separate deterministic completion check achieved 6/6.

## Exact retained identities

Paths below are relative to the configured workspace/project, not required public downloads.

- Local Blender bundle: `3a048beb98d8c19770ec71464daf87679561a195c095d1f60081804de2618336`.
- GLB SHA-256: `936fab54a5dcf80100dfa70ce24cbb0eed569c038c310e24e1ba049611482539`.
- Source under workspace: `.forgecore/blender-library/objects/<bundle>/model.blend`.
- Scene under project: `Assets/Scenes/BlenderBarrel-1caa9798e0e2.unity`.
- Art evidence: `.forgecore/unity/art-evidence/58850cfe0ac24d5eb340f638a984e334.json`.
- Gameplay evidence: `.forgecore/unity/gameplay-evidence/373504793a7c4b9cbdfe2d66819375c9.json`.
- Profile evidence: `.forgecore/unity/player-profile/evidence/c536a4de6775433aae14a71a712d54fb.json`.
- Profile evidence SHA-256: `21f3782bdce56ffeeaa6ce1fe113bb8a4d84df362041a2b73bc514b4190288bd6`.
- Executable SHA-256: `6456584d36a1ed8f5a4662a524f11bf1da4c580870aa6257e4cf086300f4f615`.
- Current kit manifest SHA-256: `811a906ca03c6f7bc76eba6d0cdd23f80ab8a0b6346664fad6261c5de6bd712f`.
- Packages manifest unchanged: `c9fcb3ab2c704410ba18162adcf0f8921efe548ec0a31bd8679bb0e85f504712`.
- Packages lock unchanged: `615353b78c74667e25c3b792598a3dd5d89d52f80083586e978676db681e0bb5`.

## Not accepted

Realistic detailed/skinned characters, proper character animation/retargeting, whole-game Blender asset production, source-aware human-reviewed library reuse, automatic suitable prop placement, complete audio/UI/saves, representative performance, long soaks, polished full playthrough and unrestricted genre/character generation remain open. No human visual/rights approvals were fabricated. No paid provider, credential extraction, download or package installation was used for the local barrel milestone.

Local delivery folders, previews, editable model and detailed v037 report remain available in the original workspace/output locations. They are excluded from the public source checkpoint; this archive records their evidence rather than publishing binary/runtime data.
