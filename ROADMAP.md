# ForgeCore Roadmap

For the current user-facing stages, priorities, completion criteria and estimates, see [OUTCOMES.md](OUTCOMES.md). This file retains implementation history; it is not a declaration that the remaining full-game outcomes are complete.

## v0.1 — safe local foundation

- WPF conversation shell and status panels
- Mock local model and configurable LLamaSharp seam
- Bounded agent loop with approval and cancellation
- Workspace-scoped text tools and allow-listed process execution
- SQLite memory and audit trail
- Security and orchestration unit tests

## v0.2 foundation - implemented increment

- Browse recent persisted conversations and reload their messages
- Show recent persisted audit activity
- Derive useful conversation titles from the first request
- Validate local `.gguf` model readiness without downloading or loading files
- Show live ForgeCore process CPU and RAM metrics

## v0.3 local inference foundation - implemented increment

- LLamaSharp 0.27 with matching Windows Vulkan backend
- Lazy local GGUF loading and asynchronous resource disposal
- Streaming, cancellable inference with bounded request, response, context, and token limits
- GBNF-constrained JSON plan generation plus strict parsing
- Fail-closed model configuration and model-load status
- Model-free tests for the inference orchestration and plan parser

## v0.4 conversational developer foundation - implemented increment

- Natural assistant response envelope with bounded conversation history
- Deterministic .NET console, class-library, and Unity starter scaffolds
- Strictly typed `scaffold_project` and `stage_web_asset` model actions
- Exact host allow-list, passive-type quarantine, byte limit, optional checksum verification, and licence manifests for staged assets
- Non-blocking approval window that active-task cancellation can close
- Detailed supervised program and game generation design in `GAME-GENERATION.md`

## v0.5 dependable coding loop - implemented foundation

- Exact unique text patches with optional optimistic SHA-256 checks
- Protected workspace-local snapshots and hash-guarded rollback with pre-restore safety copies
- Readable patch diff in the approval window
- Dedicated, typed `dotnet_build` and `dotnet_test` actions
- Local-feed-only restore, no shell, fixed arguments, hard timeout, bounded output, and process-tree cancellation
- Reserved `.forgecore` internal namespace protected from general mutation tools
- Bounded multi-pass inspect/patch/build/repair planning with typed tool observations
- Aggregate action limits, planning-pass limits, duplicate suppression, and deterministic-failure handoff to the model

## v0.6 project intelligence and visual refresh - implemented increment

- Metadata-only SQLite project index with bounded workspace scanning
- Path, symbol, and identifier retrieval through typed `index_project` and `search_project` actions
- Sensitive-file, binary, cache, reparse-point, per-file, aggregate-size, and file-count exclusions
- Transactional replacement that preserves the prior index on cancellation
- Professional three-pane dark WPF shell with conversation bubbles, task/audit cards, runtime/model status, and a focused composer
- Redesigned approval dialog and native Windows dark title bar

Still required to complete v0.6: independently streamed chat display, automatic stale-index detection/refresh, richer language-specific symbol extraction, and an interactive project dashboard.

## v0.7 supervised online asset foundation - implemented increment

- Typed `search_asset_catalog` action with derived `Network` permission and explicit approval
- Anonymous Openverse image/audio metadata adapter with a fixed HTTPS endpoint
- CC0/public-domain and commercial/derivative-compatible licence filters
- Mature/non-HTTPS/result-count/response-size/private-network exclusions
- Source, creator, licence, attribution, preview, and direct-media metadata returned as untrusted data
- Process-memory-only bounded cache; raw results redacted from SQLite and durable conversation output
- D-drive runtime layout for workspace projects/assets, SQLite memory/indexes, and local model weights
- Updated professional shell badges for local-model plus approval-gated online capability

Still required beyond v0.7: general cited web research, interactive preview cards, attribution-file aggregation, and automatic retention controls for conversation history.

## v0.8 guarded asset import - implemented increment

- Typed `inspect_staged_asset` read action with strict provenance-manifest validation and fresh SHA-256
- Bounded structural validation for PNG, JPEG, WebP, WAV, Ogg, MP3, OBJ, and GLB
- Image/geometry/resource limits plus rejection of OBJ material-library and GLB URI references
- Typed `import_staged_asset` write action bound to project, unchanged filename, destination, and exact inspected hash
- Separate visible approval, re-inspection, streaming rehash, atomic no-overwrite import, and provenance sidecar
- Quarantine retention and adversarial coverage for tampering, stale hashes, malformed manifests, traversal, internal targets, unsafe references, cancellation, and approval sequencing

Still required beyond v0.8: interactive asset preview/selection UI, licence attribution aggregation, engine-native import settings, and quarantine retention controls.

## v0.9 supervised Unity bridge - implemented foundation

- Deterministic version/hash-bound Editor bridge installer included in Unity scaffolds
- Fail-closed `UnityEditorPath`, project-shape, bridge, permission, path, timeout, and result validation
- Typed capability, compile, scene, primitive, component, EditMode/PlayMode test, bounded Play Mode, and Windows build operations
- SHA-bound one-use JSON protocol, no shell, `-batchmode`, `-noUpm`, bounded logs, cancellation, and process-tree termination
- Pre-change scene snapshots and no-overwrite bridge, scene, result, executable, and `_Data` targets
- Local-model grammar/prompt, visible approval details, agent verification semantics, and adversarial tests
- Real Unity `6000.5.5f1` acceptance verified for capability handshake, scene registration, primitive creation, compilation, and a Windows development executable

Still required beyond v0.9: richer serialized component properties, Build Settings editing beyond automatic new-scene registration, interactive scene preview, engine-native asset import settings, and signed distribution of the bridge as a reviewed Unity package.

## v0.10 supervised Unity AI collaboration - implemented foundation

- Optional official Unity MCP stdio adapter, disabled by default and non-blocking when absent
- Typed `discover` and `call` actions with strict schemas and derived `Execute` permission
- Exact relay, workspace-project, Assistant-package, Editor-version, and case-sensitive tool allow-list checks
- MCP lifecycle negotiation, bounded JSON-RPC arguments/results, timeout, cancellation, and process-tree termination
- ForgeCore approval details plus Unity first-connection approval; Unity credentials remain outside ForgeCore
- UI readiness badge, configuration example, setup guide, architecture/security documentation, and adversarial parser/policy tests
- Live official-relay acceptance against the linked `Games/ForgeCore` project: discovery, console diagnostics, ForgeCore approval UI, and the read-only asset-model query completed successfully
- Deterministic low-latency routes for Unity model and Console diagnostics; all routed calls still require the ordinary policy and one-action approval

Still required beyond v0.10: enable eligible Unity asset-generation models for the linked account/project, add a separately typed and reviewed policy for each selected mutating generator, display discovered schemas as interactive cards, and add before/after Unity scene snapshots. The broad `Unity_RunCommand` tool remains intentionally excluded because it exposes arbitrary C# execution. Unity Generators remain Unity-managed and are never invoked without explicit informed consent for a specific generation.

## v0.11 playable Unity map workflow - implemented foundation

- Fixed/hash-verified Frontier Valley map-kit installation with a typed `create_map_demo` action
- Seeded serialization-safe mesh landscape, trails, river, forest, five objectives, player controller, minimap, lighting, and day/night cycle
- Scene/build no-overwrite enforcement plus protected Build Settings, Graphics Settings, and Quality Settings snapshots
- Optional locked-local Unity package mode with pinned-cache verification and byte-exact manifest/lock restoration
- In-memory one-project session authorization bounded by duration and action count; commands, administrator actions, other projects, and non-CC0 downloads remain excluded
- Real Unity `6000.5.5f1` compile, generation, 20-second Play Mode, Windows build, and standalone player-log acceptance on the RTX 3070 workstation

Still required beyond v0.11: original art and animation, engine-native imported-asset settings, navmesh/enemies/gameplay depth, save/load, menus/settings, audio production, performance/quality presets, automated screenshot comparison, release signing/installer, and user-selected Unity AI generator policies. Frontier Valley is a playable technical demo foundation, not a finished commercial game.

## v0.12 supervised 3D generation - implemented foundation

- Typed `generate_3d_asset` plan contract and modular provider interface
- Opt-in fixed Seele API v2 provider using an environment-only key and exact prompt approval
- Bounded asynchronous job polling, cancellation, passive-artifact download, signed-query redaction, transactional quarantine, SHA-256, and immediate structural inspection
- Separate inspection/import approvals; session authorization cannot approve credit-consuming cloud generation
- Desktop readiness badge and detailed cloud-boundary permission dialog

Still required beyond v0.12: live acceptance using the user's eligible Seele account, generated-asset preview cards, automatic Unity material/import presets, mesh optimisation/LOD/collider tools, Blender cleanup, rights-review records, scene placement, and screenshot-based visual QA. AI output is source material, not automatically production-ready art.

## v0.13 supervised Meshy generation - implemented foundation

- Fixed Meshy Text-to-3D v2 adapter with environment-only `MESHY_API_KEY` authentication
- Meshy 6 preview followed by PBR refinement, 2K textures, GLB target, bounded polling, timeout, and cancellation
- Provider/model pairing validation and a configurable deterministic preferred provider
- Visible credit/cloud warning, provider readiness badge, signed-download redaction, quarantine, structural inspection, and separate exact-hash import

Still required beyond v0.13: live acceptance after informed approval, Unity material/import presets, collider and LOD generation, scene placement, preview cards, visual QA, rights review, and performance validation. Meshy output remains untrusted source material until inspected and reviewed.

## v0.14 trusted project asset preparation - implemented foundation

- Exact-hash `prepare_generated_asset` Unity operation for generated GLBs and provenance sidecars
- Protected scene backup, unique anchor/object binding, bounded target-height scaling, static mesh/box collision, placeholder hiding, and mesh/material/performance report
- Safe bridge upgrades only from explicitly enumerated hash-verified prior releases, with protected backup and rollback
- Renamed trusted-project mode with explicit exclusions for cloud spending, packages, publishing, credentials, legal decisions, deletion, administrator access, and untyped PC control

Still required beyond v0.14: install an approved Unity GLB importer manually, live scene preparation acceptance, rendered preview capture and visual QA, material remediation, LOD mesh creation, navmesh validation, and human rights review.

## v0.15 genre-neutral game planning - implemented foundation

- Deterministic registry covering twelve genre families plus a bounded unfamiliar-genre fallback
- Thirty-five composable system packs with dependency resolution and validation gates
- Hybrid classification plus explicit multiplayer, procedural, narrative, inventory, crafting, stealth, vehicle, controller, touch, and XR signals
- Advisory local-model planning brief that grants no tools or permissions and does not widen the Unity bridge
- Versioned per-task game blueprints persisted through an optional store capability, with non-game requests excluded
- Schema 2 deterministic campaign plans with one to twelve level roles, seeds, mechanic focus, validation gates, and adjacent completion transitions
- Schema 3 coherent art-set plans with bounded style families, genre-dependent asset slots, minimum counts, technical gates, and deterministic migration from stored schema-2 blueprints
- Frontier Valley designated as the first certified third-person-action reference pack; other genres remain explicitly prototype-only

October target: accept any bounded game-genre prompt, produce a typed system graph and playable prototype path, and deliver polished output from several certified genre families. Certification requires a deterministic template, automated system tests, bounded Play Mode evidence, standalone build evidence, and documented performance/visual acceptance; equal commercial polish across every possible genre is not implied.

Continued in v0.16: expose the blueprint in the desktop UI, add reviewed prototype installers, certify first-person/platformer/top-down/racing packs, add screenshot-driven visual QA, and make generated asset/animation/audio requirements first-class plan nodes.

## v0.16 reviewed Unity prototype installer - implemented foundation

- Approval-gated `scaffold_unity_game_template` action with four exact prototype template identifiers
- Shared deterministic runtime and scene builder for first-person action, platformer, top-down RPG, and racing loops
- Hash-verified manifest, repeat-install verification, and refusal to overwrite modified template code
- Bridge 0.16.0 `create_game_template` operation with bounded scene path, template id, and seed
- Consistent locked-package verification and manifest/lock restoration for Unity test runs
- Template kit 0.16.24 splits every serializable `MonoBehaviour` into a same-named script asset, validates script-asset identity before scene save, safely upgrades enumerated exact prior installations with protected backups, adds render-pipeline-aware runtime visual themes plus cached deterministic procedural surfaces, and routes bounded typed control frames through normal player/actor updates
- Genre-specific creation assertions verify player/controller/camera wiring, objective type/count/order and physics, ordered racing checkpoints, and reachable completion state
- Reproduced and fixed the first-person standalone `level0` serialization crash caused by unresolved local script IDs
- Live acceptance for first-person action, platformer, top-down RPG, and racing: compilation passed, current EditMode tests 14/14, bounded Play Mode passed for 20 seconds, isolated Windows development builds succeeded, and each real player remained responsive with a clean startup log
- Product-owned `unity_player_smoke_test` with schema-2 ordered-scene/build hashes, bounded headless launch, startup-log failure scanning, and exact process-tree termination; live FirstPersonPrototypeV4 acceptance passed
- Ten-hour/120-action reviewed-project authorization option plus a persistent 24-hour manual approval inbox, live approve/reject controls, reject-only treatment for orphaned prior-session requests, and four bounded independent task lanes so unrelated work can continue while one task awaits a decision
- Same-project Unity execution lease plus hash-bound recovery from same-project Unity setting backups

## v0.17 linked Unity campaign foundation - implemented increment

- Bridge 0.17.5 `create_game_campaign` with exact `-Level01.unity` naming and bounded one-to-twelve level count
- Deterministic per-level seed variation, serialized level position/count, objective-completion transitions, and manual next-level control
- Transactional partial-scene rollback and protected Build/Graphics/Quality setting snapshots
- Explicit campaign build contract that includes exactly the displayed linked scenes without changing the project's normal startup-scene invariant
- Live three-level first-person acceptance: fresh Campaign V4 generation, compile, current Unity tests 14/14, 20-second Play Mode, exact V9 three-scene Windows build, and 15-second provenance-bound player smoke test all passed; V4 includes start/pause/restart/quit presentation, saved highest-level unlocks, bounded unlocked-level selection, a multi-part magazine/reload/recoil weapon loop, runner/walker/brute combat profiles, articulated silhouettes, expanded arena dressing, procedural surface materials, and runtime audio/particle feedback

## v0.18 runtime visual evidence - implemented increment

- Typed, approval-gated `capture_scene_preview` with exact existing scene, local `Previews/.../*.png` destination, and one-to-thirty-second delay
- Bridge 0.17.5 synchronous 1280x720 active-camera capture that works in batch mode without external upload
- Automatic sampled-pixel rejection for likely shader-error magenta frames and blank/near-uniform images
- Executor-side PNG signature/dimension/size verification plus atomic schema-1 provenance bound to scene and image hashes, Editor/bridge versions, and capture duration
- Template kit 0.16.24 genre palettes, deterministic ground/structure/objective/weapon/actor surface textures, emissive objective accents, soft directional shadows, fog, level tint, genre-specific projection/framing, open racing checkpoint gates, start/pause/restart/quit flow, campaign-scoped highest-level persistence and unlocked-level selection, bounded FPS/top-down chase/contact combat, three target profiles, a multi-part first-person weapon/ammo/reload loop, articulated target silhouettes, cover/towers/point lights, locally synthesized feedback tones/particle bursts, deterministic gameplay checks, and multi-frame scripted control-path Play Mode coverage with built-in/URP-aware shader selection
- Live visual QA passed for the four standalone templates and linked first-person campaign; an initial asynchronous capture failure, a built-in/URP magenta defect, and a racing camera obstruction were reproduced, diagnosed, and fixed before acceptance

Still required beyond v0.18: expose blueprint/campaign detail, preview cards, and template certification in the desktop UI; add input-driven runtime gameplay tests beyond creation-time contracts (fulfilled in v0.26 for the bounded core control paths); add dependency-aware continuation/resume inside a blocked task; add camera-composition and reference-image comparison gates; replace primitive presentation with reviewed art, animation, audio, VFX, and performance budgets; extend level-unlock persistence with checkpoint/settings/level-select/corrupt-save handling; and promote templates from prototype to certified only after those gates pass.

## v0.19 deterministic runtime gameplay evidence - implemented increment

- Typed `verify_gameplay` operation with exact scene/duration arguments, approval gating, current-kit hash requirement, bounded resumable Play Mode execution, and no saved Play Mode mutation
- Development/editor-only template contract exercises actual serialized runtime components: player displacement, spawn restoration, objective interactions, ordered racing rejection, and final completion
- Dedicated-verification classification lets the agent treat successful checks as evidence rather than proposing them repeatedly
- Live acceptance passed independently for all four standalone genres and all nine levels across Campaigns V2, V3, and V4; reported movement ranged from 2.48-4.00 metres and every expected objective completed
- Schema-1 gameplay evidence is atomically persisted with exact scene and template-manifest hashes, Editor/bridge/kit identity, duration, movement, and objective counts; at v0.19 acceptance the 13 newest per-scene records were current with zero stale or invalid records. Kit 0.16.24 correctly reclassified those as stale historical evidence while Campaign V5 supplied three current records.

Still required beyond v0.19: frame-driven input injection, multi-frame motion/physics traces, navigation/contact-damage assertions beyond the current deterministic multi-hit check, level-transition and save/load verification, performance telemetry, and human controller-feel testing.

## v0.20 coherent art-set readiness - implemented foundation

- Typed environment, prop, gameplay-object, character, animation, weapon/tool, vehicle, effects, and UI roles with bounded minimum-count and validation contracts
- Bounded style classification for coherent 3D, realistic PBR, stylized 3D, illustrated 2D, pixel-art 2D, and flexible prototypes without retaining prompt text
- Genre/system-aware requirements for PBR material coverage, compatible rigs and animation clips, LODs, collision, scale/pivots, provenance, rights review, visual review, and combined performance evidence
- Deterministic assessment rejects partial or mixed-style packs and cannot treat the existing single watchtower model as a complete realistic FPS art set
- Planning brief and task audit expose the derived art direction and slot count while explicitly granting no asset, network, Unity, purchase, or legal permission
- Stored schema-2 blueprints upgrade deterministically in memory to schema 3; migration is covered through a real SQLite legacy-row test

## v0.21 hash-bound Unity art evidence - implemented increment

- Bridge 0.17.6 read-only `inspect_game_art_asset` for one exact imported GLB, bounded asset-set id, typed art role, and style family
- Temporary preview-scene inspection of meshes/renderers, material and PBR coverage, textures and missing resources, rigs/avatars, animation clips, LODs, colliders, vertices/triangles, and bounds without saving project content
- Protected pre-inspection Unity metadata backup; any `.meta` change is restored and rejected, while GLB or provenance mutation also invalidates the result
- Atomic schema-1 evidence binds GLB, provenance, and `.meta` hashes and cannot mark rights or visual review complete
- Strict current/stale/invalid catalog maps only current structural facts into schema-3 readiness; the live watchtower is valid PBR geometry but correctly leaves a realistic FPS art plan incomplete at 0/8 slots

## v0.22 non-overwriting Unity art preview evidence - implemented increment

- Bridge 0.17.10 typed `capture_game_art_preview` for one exact imported GLB/hash/set identity and one non-existing local `Previews/.../*.png` destination
- Deterministic four-angle 1280x720 atlas rendered from a temporary clone in Unity's verified empty unsaved batch scene, with explicit temporary-object cleanup and no scene save
- Automated PNG/dimension, likely error-magenta, and blank/near-uniform rejection; atomic sidecar binds GLB, import provenance, `.meta`, preview, Editor, and bridge hashes/identity
- Supervised 3D generation now requires compile/import, current structural-ready evidence, and a valid preview before it may propose scene preparation; each failure stops placement
- Live watchtower acceptance passed at 239,333 bytes, SHA-256 `67655759ab46c14b03abe810966a481880b51e61e7e01cb53d9bfbf0f60424d1`, 0.00% sampled error-magenta, and luminance 0.065-0.955; rights and visual review remain false and the realistic FPS art plan remains 0/8

Still required beyond v0.22: add explicit reviewed rights and human visual-acceptance records separate from technical preview evidence; inspect texture density and pivots/orientation; enforce placed-scene runtime budgets; acquire a user-approved coherent licensed pack; then dress and re-accept one focused multi-level vertical slice.

## v0.23 deterministic art ingestion budgets - implemented foundation

- Current Unity art evidence maps measured vertex, triangle, material, and texture counts into schema-3 readiness instead of treating technical performance as an unimplemented text gate
- Every 3D role has bounded per-asset caps; realistic, coherent, and stylized 3D packs have separate whole-pack caps
- Missing measurements, per-role excess, and whole-pack excess each fail readiness with deterministic audit issues; style-mismatched evidence cannot consume or satisfy the target pack budget
- Live watchtower evidence is current and measured at 36,351 vertices, 30,069 triangles, one material, and four textures, inside the environment-prop ingestion caps; it remains unready because the pack, LOD/collision, rights, and visual gates are incomplete

## v0.24 texture-resource and transform-convention evidence - implemented increment

- Bridge 0.17.11 adds exact texture pixel counts, maximum dimensions, conservative estimated memory, normalized bottom/horizontal pivot offsets, root scale, and root rotation to read-only Unity inspection
- Schema-2 art evidence binds those metrics to the GLB/provenance/`.meta` hashes; valid schema-1 history remains retained but cannot become current or satisfy readiness
- 3D readiness now fails closed for missing convention measurements, non-uniform root scale, non-canonical root rotation, required non-grounded pivots, oversized textures, or per-asset/whole-pack texture-memory excess
- Live watchtower evidence passed at 16,777,216 texture pixels, about 117.4 MB conservative estimated texture memory, 2048x2048 maximum texture size, grounded pivot, uniform root scale, and canonical root rotation

Still required beyond v0.24: add explicit rights/human visual acceptance, placed-scene draw-call and resident-memory budgets, runtime frame-time telemetry on target hardware, semantic scale validation against role-specific real-world ranges, and a coherent licensed pack for one focused vertical slice.

## v0.25 exact-hash human art attestations - implemented increment

- New `review_game_art_asset` proposal supports separate `visual` and `rights` reviews while excluding the decision itself from model-controlled arguments
- Native model tool parsing and trusted-project authorization cannot emit or approve the review; approval is available only in the full live exact-hash dialog, while rejection is explicit and closing the dialog records nothing
- The executor re-hashes the imported GLB, provenance, `.meta`, preview PNG, and preview sidecar after the user decision, strictly revalidates current Editor/bridge provenance, and writes an atomic non-overwriting audit record
- The art catalog evaluates only the newest valid record for each asset-set/review-kind pair; approved records set their one flag, rejected records keep it false, and source/preview changes make prior decisions stale
- Tamper, forged-decision, cancellation, rejection, later-approval, non-overwrite, malformed, stale, and legacy-history behavior is covered by the regression suite
- Fresh live watchtower Review2 evidence uses bridge 0.17.11, is visually reviewable at 1280x720 with four views and 0.00% sampled error magenta, and deliberately remains `visualReviewed=false` and `rightsReviewed=false` pending the user's two decisions

Still required beyond v0.25: deploy the new desktop build, obtain the user's independent visual and Meshy-rights decisions for Review2, add LOD/collider remediation, measure placed-scene draw calls/resident memory/frame time on target hardware, acquire the remaining coherent art-set roles, and re-run multi-level gameplay and visual acceptance.

## v0.26 frame-driven runtime controls and Campaign V5 - implemented increment

- Template kit 0.16.24 adds a clamped typed control frame shared by normal keyboard/mouse input and development/editor-only scripted input; `PrototypeGame` and `PrototypeActor` no longer read Unity input directly.
- A bounded Play Mode NUnit fixture drives all four supported genres over real elapsed frames through start, meaningful movement, genre-specific action, pause with zero movement, resume, respawn, and a grounded platformer jump arc.
- Unity test failures now include at most five bounded NUnit case names, messages, and stacks instead of only an opaque Editor failure.
- Live Campaign V5 created three new non-overwriting scenes, passed compile, EditMode 15/15, PlayMode 1/1, and scene-specific runtime checks at 2.50 metres and 6/6 objectives per level.
- The exact ordered Campaign V10 Windows development build passed provenance verification and a 15-second responsive clean-start smoke test; its Level02 preview passed PNG, magenta, luminance, and human composition checks.
- The gameplay-evidence catalog reports 3 current, 13 stale historical, and 0 invalid records after the kit upgrade.

The cross-scene transition/save-load and bounded combat-path gaps above are fulfilled in v0.27. Target-hardware performance budgets, controller feel/balance, production art/animation/audio/VFX, and a multi-day scheduler remain open.

## v0.27 campaign persistence, combat traces, and exact approval recovery - implemented increment

- Template kit 0.16.26 adds real-runtime Play Mode coverage for exact next-level requests, save/load of monotonically increasing campaign unlocks, bounded chase/contact damage, control-frame attack cadence, target defeat, and objective completion.
- A pending non-art approval surviving an application restart can be atomically claimed, revalidated under the current policy, and executed once for its exact owning task. Current-policy denial does not execute it, and human art review remains live-dialog-only.
- Fresh Campaign V5 acceptance passed EditMode 15/15, PlayMode 3/3, all three gameplay checks at 2.50 metres and 6/6 objectives, a new non-overwriting runtime preview, exact three-scene V11 build, and 15-second responsive player smoke.

The recoverable dependency-graph and interrupted-execution gaps above are fulfilled in v0.28. Target-hardware frame-time/draw-call/memory budgets, controller feel and balance testing, and production art/animation/audio/VFX remain open. ForgeCore deliberately does not self-authorize new actions or auto-replay side effects with ambiguous completion.

## v0.28 bounded persisted offline work queues - implemented increment

- Explicit unattended requests can persist one acyclic graph of 1-32 exact offline typed calls before execution; network, administrator, cloud generation, catalogue staging, and human-attestation actions are excluded.
- SQLite claims only dependency-ready steps atomically. Every step is revalidated under current policy, ordinary write/execute approval pauses only that queue, denial blocks dependents, and completion is bound to the exact execution owner.
- Restart resumes bounded ready work, while a step left executing by another process becomes `RequiresReview` and is never replayed automatically. Cancellation likewise quarantines ambiguous in-flight work.
- Live read-only acceptance executed one project-settings listing, reopened the SQLite queue store/service, resumed the dependent project-version read, and completed without launching Unity or modifying the real workspace.

## v0.29 immutable reviewed-asset reuse - implemented foundation

- New typed `search_asset_vault`, `store_reviewed_asset`, and `import_vault_asset` actions provide offline search plus exact-hash, approval-gated storage and project reuse.
- Vault storage requires current schema-2 Unity evidence, performance/convention measurements, and separate current live visual and rights approvals. It preserves exact provenance, preview, evidence, and review records in a content-addressed non-overwriting bundle.
- Import re-verifies every bundle file and atomically creates a new Unity asset directory and provenance without copying source `.meta` authority. Fresh target-project Unity inspection/review remains mandatory.
- The supervised named-3D planner searches the vault before proposing cloud generation and can route an exact named candidate through safe project import and the existing preparation gates.
- Regression coverage proves premature storage rejection, idempotent exact storage, offline typed search, scoped approval, atomic import, replay refusal, and tamper rejection without a partial destination.

Still required beyond v0.29: deploy the isolated build, complete the real watchtower's live visual and rights reviews so it becomes the first vault object, add UI cards/preview selection and attribution export, support reviewed multi-file texture/audio/animation bundles, and connect role-based vault selection to campaign art-pack planning rather than only explicit named 3D requests.

## v0.30 hash-bound Editor performance baseline - implemented increment

- Typed `profile_scene` actions require the current template kit, an existing scene, a five-to-thirty-second duration, and a separate execute approval.
- Bridge 0.17.15 runs non-saving Play Mode and returns bounded Editor-update interval, allocated-memory, draw-call, SetPass, triangle, and vertex measurements when Unity exposes them.
- Atomic schema-2 evidence binds metrics and budgets to the exact scene/template-manifest hashes plus Editor/bridge/kit identity, and separates Editor-baseline success from render-metric availability and render-budget success.
- Campaign V5 Level 2 live acceptance sampled 9,848 Editor updates over ten seconds: 0.342 ms p95, 160.742 ms maximum, 619,377,655 bytes peak allocated memory, and `editorBaselineBudgetPassed=true`. Batch render counters were unavailable and are explicitly recorded as unevaluated rather than passed.
- Debug and Release regressions now pass 207/207; the isolated Release desktop build passes with zero warnings/errors while the running app remains untouched.

Still required beyond v0.30: visible-Editor or standalone-player rendered-frame/GPU evidence on target hardware, performance history/catalog UI, enforceable quality presets, and representative profiling after production art/animation/VFX replace prototype geometry.

## v0.31 hash-bound standalone rendered-player baseline - implemented increment

- New typed `unity_player_profile_test` actions require an exact ForgeCore development build, fixed 1280x720 windowed rendering, a five-to-thirty-second duration, and scoped execute approval.
- Schema-3 build provenance binds executable, `level0`, ordered scene-source hashes, Editor version, template-kit version, and template-manifest hash. Transient profile requests are SHA-bound; transient request/result files are removed; logs and atomic evidence remain local and non-overwriting.
- Template kit 0.16.28 performs a one-second warmup, then records standalone frame deltas, optional Unity GPU timings, allocated memory, graphics-device and quality identity, and active renderer/mesh/geometry/camera counts. Normal player launches remain unaffected when profile arguments are absent.
- Regression coverage rejects build tampering before launch, mismatched results, over-budget results, timeout, unexpected fields, unsafe parameters, and permission downgrade. Debug and Release each pass 209/209; the isolated Release desktop build has zero warnings/errors.
- Live Campaign V12 acceptance passed Unity compile, EditMode 15/15, PlayMode 3/3, a fresh exact three-scene development build, headless smoke, and visible 10-second profile on the RTX 3070. At 1280x720 it sampled 13,903 frames, 1.479 ms p95 frame time, 0.245 ms p95 GPU time, and 166,431,889 bytes peak allocated memory; all configured workstation-baseline budgets passed.

Still required beyond v0.31: profile representative reviewed production art, skeletal animation, lighting, VFX, audio, AI/navigation load, and higher quality/resolution presets; add history/trend UI, controller-feel/balance testing, and explicit target-machine matrices. The static-opening limitation is addressed by the v0.32 host implementation below, pending live acceptance. The current live result proves ample headroom for a low-complexity procedural blockout only.

## v0.32 fixed standalone gameplay-load profile - live accepted on 26 October 2026

- Template kit 0.16.29 makes scripted profiling a fixed, hash-bound `scripted_gameplay_v1` contract. It exercises movement and genre actions through the ordinary actor controllers during the visible standalone sample; normal launches remain unaffected.
- Schema-2 result/evidence adds template identity, gameplay-frame count, accumulated player travel, action-event count, objective state, and minimum health. The host rejects zero-movement, zero-action, unsupported-template, mismatched-scenario, malformed, tampered, over-budget, and timeout results.
- Planner grammar/prompt, dedicated-verification tracking, policy text, and the approval dialog all understand the typed profile action. Debug and Release each pass 209/209; isolated Release Desktop build `work/desktop-validation-v032` has zero warnings/errors.
- A protected 0.16.28-to-0.16.29 upgrade plus fresh Campaign V13 build/smoke/profile sequence is prepared in `work/live-player-profile`. The approval service refused the launch because the account usage limit was reached, so no Unity process was launched and the real project remains unchanged on verified 0.16.28.

The scoped live acceptance passed on 26 October: compilation, EditMode 15/15, PlayMode 3/3, three-scene V13 development build, smoke, and gameplay profile. The profile recorded 60.250 m travel, 13 actions, and health minimum 61 over ten seconds; frame p95 0.703 ms and GPU p95 0.208 ms passed the configured workstation baseline. The earlier usage-limit note above is historical.

Still required beyond v0.32: profile representative reviewed art/animation/VFX/AI loads and multiple quality/resolution/target-machine presets. Scripted input proves runtime paths execute; it is not a substitute for human controller-feel, combat-balance, or visual-quality review.

## v0.33 production-workflow integration — implemented build pipeline; full-game production remains active

- Explicit creation requests route directly to a persistent dependency workflow with the complete blueprint, new scene/build destinations, vault role/style discovery, Unity/runtime preparation, level creation, EditMode/PlayMode, every-level gameplay verification, preview, exact campaign build, smoke, and scripted gameplay profile.
- Scoped trusted-project grants now apply to queued actions after policy checks, fixing repeated approval stops in already-authorized offline work. Restart approval recovery continues the exact queue and reports the retained production requirements.
- Full-game requirements remain distinct from passed build steps. Asset acquisition/selection/placement/animation, requested systems beyond the existing templates, visual/play-feel iteration, and release gates still require implementation.
- Debug and Release each pass 229 tests; the Release Desktop build has zero warnings/errors. A real fresh one-level production queue passed all 20 stages: creation, EditMode 15/15, PlayMode 3/3, objectives 6/6, preview, Windows build, smoke, and scripted-gameplay profile. All eight vault searches were empty; production art remains unfulfilled.

## Later (intentionally deferred)

- Model picker UI, runtime GPU telemetry, and model compatibility diagnostics
- Windows UI Automation and browser tooling with dedicated permissions
- General web research and asset catalogue preview UI, plus Playwright, richer Unity Editor workflows, and a Blender adapter
- Semantic retrieval/embeddings, memory retention controls, encryption options, and export
- Signed tool plugins, richer command templates, and OS-level sandboxing
- Packaging, auto-update, accessibility hardening, and performance telemetry that remains opt-in/local
