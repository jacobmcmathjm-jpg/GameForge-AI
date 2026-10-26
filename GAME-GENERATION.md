# ForgeCore Program and Game Generation Plan

## Product target

ForgeCore should become a supervised local development partner, not an unsupervised code-and-download bot. The useful target is a workflow where the user can describe a small program or game, review ForgeCore's plan, approve consequential actions, watch progress, and run the result locally.

A representative request is:

> Create a Unity third-person prototype called ForestRun with movement, collectible items, one enemy type, a simple HUD, and properly licensed placeholder assets.

ForgeCore should respond conversationally, ask only for missing decisions, break the work into bounded tasks, create the project inside the configured workspace, stage approved assets with licence records, write code, run approved validation commands, and report exactly what succeeded or failed.

## Current implemented foundation

- Natural assistant responses and typed actions share a strict `{Response, Actions}` envelope. Ordinary conversation uses an empty action list.
- Up to 12 recent conversation messages and 12,000 history characters are included by default. Both limits are configurable.
- `scaffold_project` creates one of three deterministic templates after write approval:
  - `.NET 10` console application
  - `.NET 10` class library
  - minimal Unity project skeleton for an explicitly supplied editor version
- A scaffold is assembled in a temporary workspace-confined directory, moved into place only when complete, cannot overwrite any existing destination, and cannot leave the approved workspace.
- `stage_web_asset` can download passive image, audio, OBJ, or GLB files only when both the direct-download host and attribution-page host are explicitly allow-listed.
- Staged assets go to `<project>/.forgecore/staging`, never directly into production asset folders.
- Every staged asset receives a sidecar manifest containing its source page, direct URL, declared licence, byte count, SHA-256, and UTC timestamp.
- `inspect_staged_asset` validates that manifest and the passive file's bounded structure without changing the project.
- `import_staged_asset` requires a separate write approval bound to the inspected SHA-256 and performs an atomic, no-overwrite copy with a project-side provenance record.
- Redirects, proxies, embedded URL credentials, non-HTTPS URLs, nonstandard ports, active file types, oversized responses, path escapes, host mismatches, local/private/link-local DNS results, and optional checksum mismatches are rejected. The download connection uses the public address that passed validation to reduce DNS-rebinding exposure.
- Network staging always requires a visible `Network` permission approval.
- The approval window is non-blocking and closes when the active request is cancelled.
- Schema-3 game blueprints derive and persist a coherent art-set plan without persisting the user's prompt: a bounded visual style family and genre-dependent slots for environment modules, props, gameplay objects, characters, animations, weapons/tools, vehicles, effects, and UI. Each slot carries minimum counts and technical gates for PBR materials, rigs, clips, LODs, and collision where applicable.
- Art-set readiness remains advisory and fail-closed. It requires style consistency, immutable provenance, rights review, visual review, scale/pivot/material conventions, and combined performance evidence; a single attractive model cannot satisfy a complete realistic-game art set, and the plan grants no network, import, Unity, purchase, or legal permission.
- `inspect_game_art_asset` is a separate approval-gated, hash-bound Unity operation for an existing imported GLB. It writes local structural evidence for meshes, materials/PBR, textures, rigs, animation clips, LODs, collision, bounds, and missing resources; protects the GLB/provenance/`.meta`; never saves a scene; and always leaves rights and visual review incomplete.
- `capture_game_art_preview` is a second approval-gated, exact-hash Unity operation. It renders a non-overwriting 1280x720 four-view local PNG from a temporary in-memory clone, rejects blank/near-uniform and likely error-magenta output, protects the GLB/provenance/`.meta`, and writes an atomic source/image-hash sidecar. The PNG makes human review possible but cannot grant rights or visual acceptance.
- `review_game_art_asset` then presents either a visual-quality statement or a rights/source statement in a full live dialog bound to the exact GLB, PNG, and preview-sidecar hashes. The model cannot provide the decision, native tool parsing cannot emit it, trusted-project mode cannot authorize it, compact inbox controls cannot approve it, and closing the window records nothing. Approved and rejected outcomes are non-overwriting, separately recorded, and become stale after any source or preview change.
- The supervised generated-asset flow now stops before scene placement unless compile/import, structural inspection, four-view preview capture, human visual approval, and human rights approval all succeed for the same exact evidence. Rejection, cancellation, tampering, stale evidence, or a missing decision leaves the scene untouched.
- Current schema-2 3D art evidence carries measured vertex, triangle, material, texture count/pixels/dimensions/estimated memory, grounded-pivot offsets, root scale, and root rotation. Readiness fails when measurements or required conventions are absent, a typed role exceeds its per-asset ingestion cap, or the coherent pack exceeds its style-family total cap. Valid schema-1 history remains retained but is never current. These deterministic limits screen content before placement but do not claim runtime frame-time, draw-call, resident-memory, or target-device acceptance.

These features establish a supervised Unity prototype foundation. The optional Unity MCP adapter can hand explicitly approved polish work to an official Unity Editor session, but it does not make the pipeline autonomous and does not configure Unity's cloud/generative services.

## Intended user workflow

### 1. Discovery

ForgeCore discusses the idea without modifying anything. It should establish:

- target platform and engine;
- 2D or 3D presentation;
- input method and camera style;
- smallest playable loop;
- art and audio direction;
- licence requirements and whether commercial use is intended;
- performance target and supported hardware;
- repository and backup expectations.

The result is a short design brief stored only after approval.

### 2. Project planning

ForgeCore converts the brief into milestones and tasks with explicit dependencies. A small vertical slice should normally contain:

1. project scaffold;
2. input and player movement;
3. camera;
4. one interaction mechanic;
5. one failure or opposition mechanic;
6. minimal HUD;
7. placeholder assets;
8. automated code checks;
9. play-mode verification;
10. a local build.

Each task must have a bounded action count, success criteria, rollback notes, and an audit trail.

### 3. Project creation

ForgeCore proposes a typed `scaffold_project` action. The security layer validates its template, name, editor version, destination, permission, and workspace boundary before displaying the approval dialog.

After approval, deterministic code creates the skeleton. The language model never emits a shell command to create the base project.

### 4. Code generation and repair

ForgeCore reads relevant files, proposes complete file writes, requests approval, and then invokes only allow-listed build or test commands without a shell. Compiler output is recorded as a tool result and may inform a new bounded repair plan.

Future code tools should add patch-based edits, file version snapshots, diff previews, and rollback. Large projects should use retrieval over a project index rather than placing the entire repository into the model context.

### 5. Asset acquisition

Asset search and download must be separate actions:

1. A catalogue adapter searches configured sources without downloading.
2. ForgeCore presents previews, source pages, licence identifiers, attribution text, file types, sizes, and hashes when available.
3. The user selects assets.
4. `stage_web_asset` downloads only the approved direct URL from an allow-listed host.
5. ForgeCore verifies size and optional SHA-256 while streaming.
6. The asset and licence manifest remain quarantined under `.forgecore/staging`.
7. `inspect_staged_asset` validates supported dimensions, container headers, mesh counts, embedded references, manifest data, and SHA-256. Archive and engine-native compatibility inspection remain deferred.
8. `import_staged_asset` separately requests write approval, re-inspects/re-hashes, and atomically copies the validated asset plus provenance into its final project location without overwrite.

ForgeCore must never infer that “free” means unrestricted. Unknown, missing, or incompatible licences block import. Paid-store authentication and purchases remain manual.

### 6. Engine control

Unity control uses a deterministic version/hash-bound Editor source and a narrow command protocol rather than mouse automation. Signed package distribution remains a later hardening step. Supported commands include:

- open a known project;
- query the installed editor and package versions;
- compile scripts;
- read structured Console errors;
- create scenes and GameObjects through reviewed data files;
- generate the fixed Frontier Valley mesh-map foundation from a bounded seed;
- run EditMode and PlayMode tests;
- enter Play Mode for a bounded interval;
- make a development build to an approved output directory.

The bridge must reject arbitrary C# evaluation, package installation, external paths, editor preference changes, credential access, and build post-processors unless separately implemented and approved.

Blender should follow the same pattern: a versioned local add-on, a typed scene-operation protocol, explicit input/output files, timeouts, and no unrestricted Python console.

## Planned typed tools

| Tool | Purpose | Permission | State |
|---|---|---:|---|
| `list_files` | Inspect workspace directory | Read | Implemented |
| `read_text_file` | Read bounded project text | Read | Implemented |
| `write_text_file` | Create or replace project text | Write | Implemented |
| `apply_text_patch` | Replace one exact unique selection and snapshot the original | Write | Implemented |
| `restore_text_snapshot` | Restore a protected backup with current-hash verification | Write | Implemented |
| `create_directory` | Create workspace folder | Write | Implemented |
| `scaffold_project` | Create a known .NET or Unity skeleton | Write | Implemented |
| `stage_web_asset` | Quarantine an approved passive web asset with manifest | Network | Implemented, disabled until hosts are configured |
| `run_command` | Execute an exact configured command | Execute | Implemented |
| `dotnet_build` / `dotnet_test` | Local-feed-only, bounded SDK build or test | Execute | Implemented |
| `search_asset_catalog` | Search bounded Openverse image/audio metadata without downloading | Network | Implemented foundation |
| `generate_3d_asset` | Ask one configured provider for an original asset and quarantine passive output | Network | Implemented foundation; Seele or Meshy opt-in |
| `inspect_staged_asset` | Validate passive media/model structure, manifest, hash, and limits | Read | Implemented foundation |
| `import_staged_asset` | Re-inspect and atomically copy a hash-bound asset plus provenance | Write | Implemented foundation |
| `install_unity_bridge` | Install the deterministic versioned project bridge without overwrite | Write | Implemented foundation |
| `scaffold_unity_map_demo` | Install the hash-verified Frontier Valley runtime/builder without overwrite | Write | Implemented foundation |
| `unity_editor_action` | Capabilities, compile, scene/object/component, tests, bounded play, or Windows build | Execute | Implemented foundation |
| `unity_mcp_action` | Discover or invoke one exact allow-listed tool through Unity's official local MCP relay | Execute | Implemented foundation; disabled until manual Unity AI setup |
| `blender_scene_action` | Send a typed request to the Blender bridge | Execute | Planned |

The `prepare_generated_asset` Unity operation is an additional exact schema within `unity_editor_action`. It accepts only an imported GLB under `Assets/ForgeCore/Generated`, its expected provenance SHA-256, an existing scene, exact anchor/object names, a 1-100 metre target height, `mesh` or `box` collision, and an explicit placeholder policy. The scene is backed up before mutation. Unity must already have a GLB importer; ForgeCore does not install one.

## Asset source configuration

`AllowedAssetHosts` is empty by default. Enabling a source requires reviewing both its catalogue domain and the exact CDN domains used for downloads. Host entries are exact; allowing `example.com` does not implicitly allow every subdomain.

Example only:

```json
"ForgeCore": {
  "AllowedAssetHosts": [
    "catalogue.example",
    "downloads.example"
  ],
  "MaxAssetBytes": 104857600
}
```

Do not place cookies, store tokens, API keys, or signed purchase URLs in this file. ForgeCore v0.4 does not automate authenticated asset stores.

## Data and audit requirements

Future schema migrations should add:

- projects and engine versions;
- project milestones and dependency edges;
- asset catalogue results with expiry times;
- staged and imported asset records;
- licence identifiers, full attribution text, source URLs, hashes, and local paths;
- build, test, Unity, and Blender runs;
- file revisions and rollback points.

Conversation messages may refer to these records by identifier. Passwords, API keys, browser sessions, purchase receipts, and private store tokens must never be copied into assistant memory.

## Milestone definitions

### v0.4 - conversational developer foundation

- Natural responses with bounded conversation history.
- Deterministic .NET and Unity scaffolding.
- Passive asset staging with host allow-list, size limit, checksum, and licence manifest.
- Cancellable, explicit approval window.

### v0.5 - dependable coding loop (foundation implemented)

- Streaming conversational display independent of structured action generation.
- Patch previews, file snapshots, rollback, and output truncation. (Implemented foundation.)
- Dedicated `dotnet` build/test tools with structured arguments. (Implemented foundation.)
- Project-aware context retrieval and compiler-error repair loops.
- Real-model integration tests with response-time and cancellation budgets.

### v0.7 - supervised online asset foundation

- Read-only Openverse image/audio metadata adapter. (Implemented foundation.)
- Network approval, licence filters, source/attribution metadata, and ephemeral raw results. (Implemented foundation.)
- Search result preview UI with licence and attribution details. (Planned.)
- Interactive search preview UI and generated attribution aggregation. (Planned.)
- No archive extraction or executable packages until a separate hardened design is approved.

### v0.8 - guarded asset import

- Read-only quarantine inspection for supported passive image, audio, OBJ, and GLB files. (Implemented foundation.)
- Exact-hash-bound write approval, re-inspection, atomic no-overwrite import, and provenance sidecar. (Implemented foundation.)
- Engine-native compatibility validation and aggregate attribution file. (Planned.)

### v0.9 - Unity vertical slice

- Versioned Unity Editor bridge and capability handshake. (Implemented foundation.)
- Scene, object, component, compile, test, bounded play-mode, and Windows build actions. (Implemented foundation.)
- One generated simple 3D prototype rebuilt by an audited plan. (Frontier Valley foundation accepted on Unity `6000.5.5f1`.)

### v0.11 - playable map foundation (implemented)

- Deterministic seeded mesh landscape, trail, river, forest, objectives, controls, minimap, and lighting.
- Guarded compile, Play Mode, and Windows executable workflow with protected Unity project settings.
- Narrow in-memory project-session authorization for the approved workflow; no broad PC takeover.
- Reviewed template kit 0.16.29 supplies a start/pause/restart/quit flow, persistent highest-level unlocks, selection among unlocked levels, runner/walker/brute chase/contact enemies, first-person magazine/reload/recoil, procedural target silhouettes and arena scenery, tiled materials, and locally generated feedback. Typed control frames feed the real runtime update paths, with Play Mode tests covering input, pause/resume, respawn, progression, damage, combat, and jumping. Its fixed hash-bound `scripted_gameplay_v1` profiler records movement/action evidence with rendered frame/GPU/memory measurements. The live 0.16.29/V13 acceptance passed compilation, EditMode 15/15, PlayMode 3/3, exact three-level build, startup smoke, and rendered gameplay profiling on 26 October 2026. A full game still requires production art and skeletal animation, navigation/deeper tactics, state saves, settings, audio/VFX, representative performance, release packaging, and content-quality acceptance.

### v0.12 - supervised 3D generation (implemented foundation)

- Optional Seele API v2 provider with environment-only authentication and explicit prompt/credit approval.
- Original self-contained GLB requests, bounded polling/download, transactional quarantine, hash manifest, and structural inspection.
- Generated assets require rights/quality review and a separate exact-hash import; generation is never covered by session authorization.

### v0.13 - supervised Meshy generation (implemented foundation)

- Optional Meshy Text-to-3D v2 provider using Meshy 6 preview and PBR refinement.
- Fixed game-prop constraints, GLB output, 2K PBR textures, bounded credits reporting, polling, cancellation, quarantine, and signed-query redaction.
- Meshy generation remains a separately approved cloud action; inspection and exact-hash Unity import remain later trust decisions.

### v0.14 - generated asset preparation and trusted project mode

- Prepare an exact imported generated GLB under the Frontier Valley watchtower anchor with bounded scale and static collision.
- Back up the scene, preserve provenance, return mesh/material/performance counts, and keep visual/rights acceptance explicit.
- Reduce repetitive prompts only for typed actions in one project; never grant unrestricted PC control or pre-authorize spending, packages, publishing, credentials, deletion, or legal decisions.

### v0.25 - exact-hash human art attestations

- Require two separate live human decisions for generated art: visual acceptance and rights/source acceptance.
- Bind each decision to the exact imported GLB, provenance, `.meta`, preview PNG, preview sidecar, Editor version, and bridge version; stale or tampered evidence fails closed.
- Record explicit rejection without setting readiness, record cancellation as no decision, and prevent the model or trusted-project session from self-certifying either gate.
- Merge only the newest valid record per asset set and review kind into art readiness. Scene placement remains blocked until both current approvals exist.

### v0.10 - Blender-assisted generation

- Versioned Blender bridge with typed operations.
- Primitive meshes, materials, UVs, exports, and preview renders.
- Resource budgets and deterministic output paths.

## Definition of “usable for game creation”

ForgeCore is ready for supervised game-prototype work only when it can repeatedly pass an end-to-end acceptance test:

1. hold a coherent multi-turn design conversation;
2. create a project after one clear approval;
3. write and revise source with diff visibility;
4. stage a selected, licence-compatible passive asset;
5. import it through a separate approval;
6. compile without unrestricted commands;
7. run engine tests through a typed bridge;
8. create a local development build;
9. stop within a documented cancellation budget at every stage;
10. reconstruct what happened from SQLite audit records.

Until that acceptance test passes, ForgeCore should be described as a development foundation rather than an autonomous game generator.

## Hardware expectations for this machine

The RTX 3070 8 GB and 16 GB system RAM are suitable for the current Qwen3 8B `Q4_K_M` coding/planning model, Unity or Blender individually, and small prototypes. Running Unity, Blender, local inference, and heavy asset generation simultaneously will create memory pressure. ForgeCore should therefore serialize expensive work, unload or reduce model GPU layers when launching heavy tools, cap imported asset sizes, and prefer low-resolution placeholders during iteration.
