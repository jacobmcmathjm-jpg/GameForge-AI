# ForgeCore staged outcomes

Plan version: 1 — 26 October 2026. This is the active checklist; the initial agreed baseline is retained in [the dated archive](docs/archive/2026-10-26-outcomes-v1.md). Existing implementation history remains in [ROADMAP.md](ROADMAP.md).

Current visual examples: [28 October actual capability showcase](docs/outcomes/SHOWCASE-2026-10-28.md), with four prototype game families, three fixed Blender recipes and reused labs/map. These are real generated outputs, not concept images or evidence that any open stage is complete.

Latest bounded playtest follow-up: [S04/S05 sprint, jump and physical down-sight alignment](docs/outcomes/S04-S05-2026-10-28-controls-ads.md). This updates the reusable security-combat kit; older delivered EXEs stay unchanged and stage acceptance remains open.

Latest implementation: [S08–S10 industrial kit and greybox mission](docs/outcomes/S08-S10-2026-10-28.md), continued 29 October. Five original Blender props, exact reviewed-source manifest/import gates and an ordered mission prototype pass 495 host / 19 editor / 71 graphics-enabled play tests. Approved target placement remains unimplemented; art, full combat playthrough and balance acceptance are still open. [29 October user playtest](docs/outcomes/S10-2026-10-29-user-playtest.md): the user reports guards and objective collection worked; remaining checks and stage acceptance are separate.

## Target and working rules

Latest source-only candidate: [29 October guard-motion refinement](docs/outcomes/S03-S06-2026-10-29-motion-refinement.md). Candidate kit 0.16.58 passes 519 host tests and static Unity-reference compilation, but is not installed or runtime/visually verified. The external project and delivered game remain unchanged on kit 0.16.57. Unity-project write access was not granted; S09 placement, S10 final acceptance and S11 remain unfinished.

Build a standalone Windows ForgeCore app that can plan, assemble, test and deliver an original playable game using local tools. Start with **one single-player first-person action demo and one convincing enemy archetype**. Expand only after that benchmark works. Different genres require their own implemented systems and tests; “any genre” is a long-term target, not a present capability or guarantee.

- Default route: local/offline, no paid generation or API keys. Installed Unity/Blender, compatible hardware and any legally usable model weights/assets are external dependencies. “No API cost” does not mean no compute, storage or licence constraints.
- Keep separate: prototype success, technical verification, human visual/rights acceptance, gameplay quality and release readiness. None implies the others.
- Use one stage or a small named subset per request. Do not start purchases, downloads of new model weights, publishing or rights decisions without the required authorization.
- Every completed stage needs evidence, regression tests, a dated outcome note and a recoverable source checkpoint. Keep old accepted artifacts; never overwrite unknown project content.
- Human review and approvals stay explicit. An approval waiting in one queue must not justify bypassing a gate or replaying an ambiguous write.
- Estimates mean focused implementation and testing time, not a promise of unattended calendar progress. Running the existing app longer does not implement missing features.

## Verified starting point — S00 complete, prototype scope only

As of this baseline: four executable prototype families (first-person action, platformer, top-down RPG, racing); linked-level production/build workflows; procedural industrial scenery; basic UI/audio; placeholder combat animation and visible swept projectiles; armed enemies; and one bounded local Blender barrel recipe with exact-source reuse/import/preview. The app's barrel request does not yet automatically choose an existing scene or approve its art.

Evidence: **259/259 host tests; 16/16 Unity EditMode and 8/8 PlayMode**. A fresh barrel scene passed movement, respawn and 6/6 deterministic objectives, Windows build and startup checks. The separate ten-second rendered profile exercised combat but completed 0/6 objectives. It passed current p95 budgets **while recording a 1,727.84 ms maximum frame interval**; smoothness is not accepted. Template kit 0.16.33 preserves imported PBR material slots. No human visual/rights reviews have been manufactured.

Detailed baseline: [docs/archive/2026-10-26-baseline-evidence.md](docs/archive/2026-10-26-baseline-evidence.md).

## Stage index

Statuses: **Complete** = evidence-backed within the stated scope; **In progress** = started, with acceptance still open; **Next** = recommended next bounded task, not already running; **Planned** = not accepted; **R&D** = feasibility/quality uncertain.

| ID | Outcome | Status | Main dependency |
| --- | --- | --- | --- |
| S00 | Preserve the working prototype and first Blender-prop baseline | Complete | Existing evidence |
| S01 | Measure and remove visible startup/runtime hitches | In progress | S00 |
| S02 | Import one properly skinned, rigged enemy character | In progress — technical checks passed | S00; approved asset source |
| S03 | Give that enemy a complete blended animation set | In progress — animation lab foundation | S02 |
| S04 | Bring one character to a defined detailed visual benchmark | In progress — original security uniform foundation | S02–S03 |
| S05 | Integrate proper first-person arms and weapon animation | In progress — isolated skinned-arm combat lab | S03; compatible weapon/arms |
| S06 | Integrate enemy navigation, combat and animation coherently | In progress — bounded three-guard combat lab | S03, S05 |
| S07 | Make local source assets a searchable, reviewed reusable library | In progress — source review/promotion and bounded suitability checks | Existing bundle/vault foundations |
| S08 | Expand the bounded Blender prop recipes into an industrial kit | In progress — five original sources and Unity/runtime checks; art review open | S07 |
| S09 | Let ForgeCore select, import and place suitable approved assets | In progress — exact manifest/source-import gate; target placement pending | S07–S08 |
| S10 | Build a purposeful playable level, not only an object layout | In progress — user reports guards/objectives work; integration/final acceptance open | S06, S09 |
| S11 | Complete the demo's UI, settings, saves and accessibility basics | Planned | S10 |
| S12 | Add a local, rights-aware audio production/integration workflow | Planned | S07, S10 |
| S13 | Finish the demo's lighting, effects and graphics/performance pass | Planned | S04–S06, S10, S12 |
| S14 | Run reproducible playthrough, regression and soak acceptance | Planned | S10–S13 |
| S15 | Produce a reliably autonomous, restartable single-demo workflow | Planned | S09–S14 |
| S16 | Deliver and reproduce the complete Windows EXE folder | Planned | S14–S15 |
| S17 | Extend one genre at a time with its own quality benchmark | Planned | S16 |
| S18 | Generate original detailed characters/rigs/animation locally on demand | R&D | S02–S04, S07; feasible local approach |

Stages are ordered for clarity, not an assertion that all are strictly serial. The dependency column determines what can safely be worked on independently. No implementation starts merely because a stage is listed.

## Acceptance checklist by stage

### S01 — smoothness and honest performance evidence

Progress/evidence: [S01 diagnostic work note](docs/outcomes/S01-2026-10-26.md) and [archived first diagnostic milestone](docs/archive/2026-10-26-s01-diagnostics-v1.md). The original intermittent event remains unattributed; diagnostic and acceptance improvements are not themselves proof of a performance fix.

- Reproduce the long frame with explicit startup/loading versus interactive-play timings; determine its cause rather than assuming shader compilation.
- Separate cold/warm runs and document machine, resolution, quality preset and concurrent workload.
- Add maximum/long-frame reporting alongside p95; define acceptable startup and interactive hitch budgets before declaring a fix.
- Prove improvement with repeated rendered runs of the same exact build/scene and preserve before/after evidence. Current low-complexity p95 results are not production-load certification.

### S02 — real character rig and skinning foundation

Progress: [S02 rig foundation and evidence](docs/outcomes/S02-2026-10-27.md) and [dated source/status archive](docs/archive/2026-10-27-s02-rig-foundation-v1.md). An original local 22-bone technical mannequin now passes actual Blender/Unity skin and diagnostic-pose checks, and the ForgeCore offline prompt route works end-to-end. This is not a detailed production character, an animation set, Humanoid retargeting or a gameplay enemy replacement. Human visual/rights review remains unrecorded; the conservative stage status stays open for final acceptance.

- Select one original or appropriately licensed humanoid base with provenance; no purchase or licence attestation is automatic.
- Import an actual skinned mesh with skeletal hierarchy, valid bind pose, weights, scale, orientation and grounded feet. Check the chosen Unity rig/retargeting convention.
- Verify shoulders, elbows, hips and knees through test poses without detached clothing, extreme stretching or missing materials.
- Retain source, export settings, measurements and hashes; integrate in a new test scene without replacing accepted content.

### S03 — proper enemy animation

Work started 27 October 2026: original parameterized Unity skeletal clips, a Generic Animator blend tree/action driver and isolated lab are being implemented against the unchanged S02 Blender rig. This is not detailed art or tactical enemy integration. Stage acceptance stays open until rendered motion, contact/transition quality and the regression evidence are reviewed. See [S03 work note](docs/outcomes/S03-2026-10-27.md).

- Provide idle, walk, run, aim, fire, reload, hit reaction and death for the first enemy archetype, with documented clip sources/rights.
- Use a real animation state machine/blending workflow; synchronize speed, turning, weapon action and movement. Make the root-motion policy explicit.
- Test transitions, looping, reload/fire exclusion, pause, damage/death interruption, muzzle alignment and unacceptable foot sliding.
- Demonstrate a rendered gameplay sequence and regression tests. Existing rigid-part placeholder clips do not satisfy this stage.

### S04 — one detailed character

Work note: [S04 industrial-security foundation](docs/outcomes/S04-2026-10-27.md). The user selected the industrial-security direction. A separate original Blender recipe and Unity pose checks are technical progress, not realistic-character or human visual acceptance.

Follow-up: [S04/S05 industrial visual finish](docs/outcomes/S04-S05-2026-10-28-visual-finish.md). Separate lab material variants, lighting and weapon presentation have an actual Unity camera render. A post-test emission-material integrity failure in the first v9 revision drove a fresh v10 correction and validator regression; final kit 0.16.49 passes **331 host / 18 EditMode / 47 PlayMode** tests. The original character source is unchanged; realistic art, human acceptance and full stage completion are not claimed.

- Agree a reference-quality sheet for silhouette, face/hands, clothing, material detail and in-game camera distance before acceptance; no imitation of protected characters is required.
- Add suitable topology, UVs, compatible PBR textures and skin/clothing detail, with measured texture/memory/triangle budgets and LOD where needed.
- Inspect deformation and clothing across the S03 set, then compare under consistent Unity lighting—not only a flattering Blender render.
- Record human visual acceptance separately from technical success. One polished archetype is not arbitrary character generation.

### S05 — first-person arms and weapon

Work note: [S05 skinned-arm integration](docs/outcomes/S05-2026-10-27.md). Isolated lab work is not the completed first-person weapon/animation acceptance.

Follow-up: [S05–S06 weapon/contact refinement](docs/outcomes/S05-S06-2026-10-27-polish.md). Smoother aiming/recoil, staged magazine movement and evaluated guard weapon contacts are a bounded lab improvement, not human play-feel or realistic-art acceptance.

- Add compatible skinned arms and a detailed weapon with idle, locomotion, aim, fire, reload and appropriate interaction poses.
- Keep grip, magazine, muzzle, recoil, sound, visible projectiles and ammo timing aligned; prevent reload/fire conflicts and camera/cover clipping.
- Test the supported frame rates, pause, interrupted reload/death and scene transitions. Full-body first person is a separate optional expansion.

### S06 — animated enemy gameplay

Work note: [S06 bounded combat integration](docs/outcomes/S06-2026-10-27.md). Static-grid lab navigation is not unrestricted tactical AI or arbitrary-level navigation.

Follow-up: [S06 flat-lab enemy-motion refinement](docs/outcomes/S06-2026-10-28-enemy-motion.md). Distance/turn-driven foot placement, acceleration, hit freeze and corpse grounding are being verified separately from human motion quality and terrain/crowd acceptance.

- Integrate navigation/pathfinding, obstacles, attack range, line of sight, aiming, damage and death with the animation state machine.
- Exercise multiple enemies without broken targeting, firing through cover or animation/physics ownership conflicts; document remaining tactical limitations.
- Validate wave/spawn/reset behaviour and representative CPU/animation load. Existing basic approach/line-of-sight logic is not full tactical AI.

### S07 — reusable reviewed local asset library

Progress: [S07 local candidate discovery and exact reuse](docs/outcomes/S07-2026-10-28.md). The offline app route searches immutable editable Blender bundles using bounded suitability filters; actual barrel/security/mannequin queries and exact candidate reuse into two fresh project folders passed with all 99 source files unchanged. This does not promote unreviewed local recipes into the approved vault. Local-source promotion, animation-compatibility metadata and accepted target-project Unity placement remain open; the follow-up below adds the human review interface.

Follow-up: [S07 exact local-source review interface](docs/outcomes/S07-2026-10-28-source-review.md). A verified preview/measurement/hash dialog now requires an explicit live source-only visual or rights decision, with default acceptance disabled. Actual read-only preparation stops at the human gate with no records created; offscreen rendering is not live human acceptance. Vault promotion, richer compatibility and approved target-project Unity/runtime proof remain open.

Next technical pass: [S07 source-only promotion and suitability checks](docs/outcomes/S07-2026-10-28-source-promotion.md). The app can check exact source role/recipe/rig/height/triangle constraints and separately request a content-addressed promotion bound to both current live human review records. New decisions invalidate old promotion versions; source and prior versions are retained. This is deliberately not the Unity-reviewed asset vault and never implies target animation/material/performance or placement acceptance. All 99 real source files remain unchanged; no real reviews or promotions were created. Actual human review and full target/persisted workflow acceptance remain open before S07 can close.

- Extend the local bundle/vault contract for editable Blender source, recipes, textures, rigs and animation—not only a reviewed GLB.
- Search by role, style, dimensions, pivot, rig/animation requirements, budget and licence. Reuse only when suitable, not merely present.
- Provide exact visual/rights review UI and current technical evidence without fabricating attestations.
- Reuse an approved version in two fresh project/game destinations by exact hashes; keep source immutable and retain rollback/evidence.

### S08 — industrial Blender prop kit

Work note: [S08–S10 industrial work](docs/outcomes/S08-S10-2026-10-28.md). Five bounded original Blender sources have been generated through ForgeCore with editable source, 1K PBR and three LOD exports. Actual target validation and human art acceptance are distinct; no realistic-art completion is claimed.

- Improve the first barrel finish and add bounded original wooden-crate, concrete-barrier, fence and container recipes.
- Preserve editable source, UV/PBR compatibility, dimensions, canonical transforms, collision convention and appropriate LOD/geometry budgets.
- Re-test hostile/unsupported input rejection, cancellation, occupied outputs, modified sources and exact reuse for every recipe.
- Prove each prop's Unity appearance and runtime placement; more recipes do not imply unrestricted Blender modelling.

### S09 — automatic suitable asset integration

The [S08–S10 work note](docs/outcomes/S08-S10-2026-10-28.md) adds a fixed-profile read-only manifest and exact current-human-reviewed/promoted source-only import with a no-replay journal. Automatic target placement is still unimplemented; unmet human and target gates remain visible.

- Convert the game brief into an explicit asset manifest with unmet requirements visible.
- Select approved compatible library versions or identify a missing supported recipe; never silently substitute a failed request or invent a licence.
- Import and place assets in fresh level/prefab destinations with correct scale, lighting/materials, collision, LOD and navigation clearance.
- Validate every placement and retain deterministic identities/backups. This closes the current gap between a generated barrel and automatic level population.

### S10 — one designed playable level

The [S08–S10 work note](docs/outcomes/S08-S10-2026-10-28.md) records an authored infiltration greybox foundation. Its industrial slots are explicitly placeholders, not silently substituted accepted Blender art. Full combat playthrough, target integration and human balance acceptance remain open.

- Build a reproducible start-to-finish route with encounters, cover, navigation, spawn safety, objectives, pacing, win/lose/reset and meaningful boundaries.
- Validate collisions, unreachable areas, objective ordering and transitions; include a human play-feel/balance pass.
- Capture a representative full playthrough and make remaining defects explicit. Seeded object placement alone does not satisfy level design.

### S11 — demo interface and persistence

- Complete start/pause/restart/quit, HUD/readability, graphics/audio/input settings and supported save/load behaviour.
- Add the agreed accessibility basics (subtitles where relevant, readable text, sensitivity and suitable control options).
- Verify controller/keyboard/mouse paths actually supported, bad saves, setting persistence, focus loss and resolution changes.

### S12 — local audio workflow

- Specify weapon, impact, footsteps, ambience, UI and music needs; distinguish synthesis, licensed reuse and optional local generative models.
- Gate new downloads/hardware/licence requirements; retain original source, provenance and any attribution. Voice/dialogue is a separate explicit scope.
- Implement Unity spatial playback, animation events, levels/mixing, looping and memory budgets; test actual gameplay rather than exported clips only.

### S13 — graphics/effects and representative performance

- Establish a consistent art direction, lighting, shadows, post-processing, impacts/muzzle effects and scene/material quality presets.
- Profile representative characters, animation, AI, audio and effects, including cold/warm startup and interactive hitches.
- Agree target resolution, frame rate and hardware matrix; measure rather than asserting indie/AA quality from a style label or empty-map benchmark.

### S14 — acceptance tests and soak

- Run host/Unity tests, complete actual start-to-finish game objectives, and exercise failures, saves, transitions, pause and restart.
- Use reproducible rendered performance/long-frame evidence, longer soak runs and a recorded defect list.
- Distinguish automated scripted checks from human visual, controller-feel, balance, accessibility and rights acceptance.

### S15 — autonomous demo production

- From one supported brief, ForgeCore itself persists and executes the asset/animation/audio/map/UI/build/test dependency workflow without a coding assistant choosing each step.
- Demonstrate cancellation, restart, missing tools/assets, failed stage isolation and pending approvals. Only independent authorized work may continue.
- Never replay an ambiguous side effect or report a complete game when art/systems/reviews remain missing.
- Prove a fresh run from a documented clean baseline. A one-off helper acceptance is not autonomous app acceptance.

### S16 — reproducible Windows delivery

- Deliver the complete game EXE folder, exact source checkpoint, required-asset manifest, settings, rights/attribution inventory, controls and known limitations.
- Verify every shipped hash and test the copied folder from a fresh location; document engine/model/data dependencies of ForgeCore separately from playing the built game.
- Rebuild a known version and demonstrate recovery from an archived checkpoint. Installer/signing/updater work is separate unless explicitly selected.

### S17 — expand genre capability

- Pick the next genre explicitly. Define its systems, assets, animation, camera, input, map rules and measurable acceptance first.
- Complete one representative game loop before claiming the genre is supported at the demo's quality level.
- Treat multiplayer, open worlds, strategy/economy and hybrid genres as their own engineering scope—not a dropdown promise.

### S18 — original offline character generation research

- Compare feasible local procedural/modular/ML approaches with licence, hardware, storage, quality and runtime constraints; do not equate installing Blender with a character generator.
- Prove one original character through topology/UV/material generation, rigging/weights, animation and Unity acceptance before broadening the prompt space.
- Reuse approved bases only within their terms and label derived versus newly generated work honestly.
- Stop or narrow scope if the current hardware/approach cannot meet the benchmark. There is no guaranteed “any detailed character” date.

## Estimate register — planning only

- S02–S03 initial rigged/animated foundation: approximately **2–4 focused development weeks**, assuming a suitable licensed base and compatible clips are available.
- First convincing detailed character plus animation/integration: approximately **6–12 focused development weeks in total**, including refinement and testing. Not the estimate for the entire roadmap.
- Original detailed-character generation entirely offline: **several months, potentially 3–6+**, subject to feasibility; not a delivery commitment and not automatically additive to the above.
- Do not assign a full-game completion date until the first character and representative level have measured acceptance. Actual scope, assets, hardware and available implementation time can change these estimates.

## How to resume and archive work

Ask, for example: **“Work on S01 only, then show the evidence and checkpoint.”** Or: **“Start S02; first inspect what suitable licensed character assets are already available locally.”** This authorizes work on that stage, not new purchases or bypassing approvals.

On a stage change, update its status and append `docs/outcomes/Sxx-YYYY-MM-DD.md` with objective, changed files, test commands/results, evidence identities, unresolved defects and the next safe action. Create a new dated source checkpoint, verify its manifest and archive the then-current outcomes list. Do not edit an old snapshot to rewrite its history.

## Backup scope and visibility

The user initially requested private GitHub storage, then explicitly authorized **public** storage in `jacobmcmathjm-jpg/GameForge-AI` on 26 October 2026. That permission applies to the sanitized source/checklist checkpoint, not credentials, runtime stores, generated game binaries or licensed third-party asset redistribution.

Source checkpoints contain application source, tests, build definitions, original recipe code and documentation. They replace the live machine configuration with the safe example configuration. They deliberately exclude full local Git history, personal configuration, model weights, SQLite databases, runtime logs, generated builds, the external Unity project and the local Blender/vault asset binaries. Keep the existing local game/source bundles; back those external data up separately if requested. This is a reproducible source checkpoint, **not a complete workstation or asset-store backup**.
