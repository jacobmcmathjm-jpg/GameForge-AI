# ForgeCore guarded Unity game-generation foundation

**Staged work and recovery:** [OUTCOMES.md](OUTCOMES.md) is the current outcome-by-outcome plan, with acceptance gates, stable stage IDs and a dated baseline archive. Ask to work on one stage at a time. [Source checkpoint instructions](docs/Source-checkpoints.md) describe safe local/GitHub backup scope and restoration.

ForgeCore is a private, local-first Windows assistant foundation built with C#, .NET 10, WPF, and MVVM. It intentionally uses a mock model by default and exposes only workspace-confined file tools plus explicitly allow-listed commands.

ForgeCore uses its deterministic Unity bridge for the project foundation, code, scenes, compilation, tests, and builds. It now includes a linked-level prototype kit, supervised Meshy generation, exact-hash generated-asset inspection and preview capture, and separate human-only visual and rights attestations. Cloud output is quarantined and structurally checked; generation, import, structural inspection, preview, visual acceptance, rights acceptance, and scene placement remain distinct trust decisions. ForgeCore still launches without a model, cloud account, or API key.

## Prerequisites

- Windows 10 or 11 x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build and run

```powershell
dotnet restore ForgeCore.sln
dotnet build ForgeCore.sln -c Release --no-restore
dotnet test ForgeCore.sln -c Release --no-build
dotnet run --project src/ForgeCore.Desktop/ForgeCore.Desktop.csproj
```

On this workstation ForgeCore stores runtime data under `D:\ForgeCore`: generated projects and assets in `Workspace`, SQLite memory/indexes in `Data`, and local weights in `Models`. Copy `appsettings.example.json` to `src\ForgeCore.Desktop\appsettings.json` to override defaults; the example remains model-free and disables cloud generation until explicitly enabled.

### Windows EXE folder delivery

Publish the Desktop project with `-p:PublishProfile=WindowsFolder -o <new-staging-folder>` for a self-contained Windows x64 folder. Its .NET/WPF runtime is included; Unity, local model weights and configured data remain external. `--verify-package` performs offline dependency validation without initializing the user store or starting work. Use [scripts/New-WindowsDelivery.ps1](scripts/New-WindowsDelivery.ps1) to copy a published app and an existing verified Unity build into fresh output folders with SHA-256 manifests. See [docs/Windows-delivery.md](docs/Windows-delivery.md) for prerequisites, validation limits and delivery instructions. This packaging is not automatic future-build distribution or release/visual acceptance.

The local [Blender asset pipeline](docs/Blender-asset-pipeline.md) now supports one original bounded recipe: `Create a weathered barrel in Blender. Name it yard-barrel.` It creates or exact-source reuses an immutable local bundle containing editable Blender source, GLB, PBR images, measurements, recipe and preview, then proposes a separate verified import, Unity inspection and review render. Installed Blender 5.2 and Unity remain external dependencies. No model inference, download, API key or paid provider is needed for this route. General asset sets, characters and Blender animations are not implemented; visual/rights acceptance is not automatic. Kit 0.16.33 preserves imported material slots instead of replacing them with the prototype's procedural finishes.

## Local model setup

No model is downloaded automatically. The default `Mock` provider makes the application usable without model files. To enable local inference, follow [MODEL-SETUP.md](MODEL-SETUP.md). ForgeCore validates an absolute local `.gguf` path, rejects UNC paths and unsafe limits, then loads weights lazily on the first request. Never place credentials in configuration.

## Safety

ForgeCore must not be run as administrator. File access is restricted to `WorkspaceRoot`; command execution has no shell and must match the configured allow-list. See [SECURITY.md](SECURITY.md) before extending tools.

Asset downloads are disabled until exact HTTPS catalogue and download hosts are configured. Supported PNG, JPEG, WebP, WAV, Ogg, MP3, OBJ, and GLB files are quarantined and structurally inspected; archives, packages, executables, external OBJ material libraries, and GLB URI references are rejected. Import always requires a separate approval and exact inspected hash. See [GAME-GENERATION.md](GAME-GENERATION.md) for the implemented program/game foundation and the route to Unity and Blender control.

Cloud generation is opt-in and never receives Unity files, credentials, or arbitrary local data. Only the reviewed text prompt and fixed generation parameters are sent. Keys are read from the current user's environment (`SEELE_API_KEY` or `MESHY_API_KEY`) and are never stored in ForgeCore configuration, memory, logs, or manifests. Every request requires visible one-action approval because it crosses the cloud boundary and may consume credits. Follow [SEELE-SETUP.md](SEELE-SETUP.md) or [MESHY-SETUP.md](MESHY-SETUP.md); never paste a key into ForgeCore chat.

Named 3D requests use a deterministic typed planner rather than placing a credit-consuming tool in the native LLamaSharp grammar. This keeps the workflow reliable and still enforces the same feature flag, project boundary, key check, visible approval, audit, cancellation, quarantine, structural inspection, and separate exact-hash import.

Before a named 3D request spends cloud credits, ForgeCore now searches a private offline reviewed-asset vault under the workspace. Only an exact asset with current Unity structural evidence plus separate live visual and rights approvals can be archived. Vault bundles are immutable and content-addressed, keep the source GLB, provenance, preview, Unity evidence, and both review records, and are searchable by name, role, style, and bounded tags. Reuse is a separate hash-bound, non-overwriting write approval; the destination project receives a new provenance sidecar and must pass fresh Unity technical inspection and review before scene placement.

Unity is not downloaded or installed by ForgeCore. Configure an existing normal-user Unity Editor using [UNITY-SETUP.md](UNITY-SETUP.md). This workstation is configured for Unity `6000.5.5f1`; a real guarded run installed map kit `0.11.3`, generated and played Frontier Valley, and produced a Windows development `.exe`. When `EnableUnityProjectPackages` is enabled, every direct package must already be pinned and present in the local package cache; ForgeCore snapshots and byte-checks `manifest.json` and `packages-lock.json` around the launch and restores them if Unity changes either. Otherwise Editor launches continue to use `-noUpm`.

`TRUST PROJECT` is deliberately narrower than blanket computer control. Its in-memory grant uses either the standard one-hour window or an explicit ten-hour/120-action overnight window, applies to one exact project, and covers only typed Unity foundation/build/generated-asset preparation, typed .NET build/test, CC0 passive-asset search/staging/inspection/import, and exact reviewed-vault storage/import. It never pre-authorizes cloud spending, purchases, publishing, credentials, human visual attestations, legal/rights decisions, deletion, commands, arbitrary files, non-CC0 downloads, another project, administrator actions, registry changes, installers, packages, or unrestricted Windows control. Emergency Stop immediately revokes the grant.

Unity AI collaboration is also never installed or enabled automatically. The selected project must explicitly declare the official `com.unity.ai.assistant` package, the relay must exist locally, the Editor version must match, and the user must approve the pending connection in the intended Unity Editor. ForgeCore does not read or store Unity credentials. Generative textures/materials/audio remain Unity-managed services and may consume Unity credits; use the workflow in [UNITY-SETUP.md](UNITY-SETUP.md).

The coding loop defaults to twelve planning passes and twelve proposed actions across the entire task. Tool observations include their original typed arguments and bounded results so the local model can advance without rediscovering paths. A successful action is not silently repeated, deterministic failures are returned to the model for repair planning instead of being blindly retried, and reaching either limit fails the task honestly.

Project indexes are opt-in, workspace-confined, and local. They store relative paths, extensions, hashes, sizes, timestamps, extracted symbol names, and identifier terms—not source-file bodies. Build/cache/internal directories and sensitive configuration filenames are skipped. Search results are discovery hints; ForgeCore must use the secured file-read tool on the current file before proposing an edit.

Online catalogue search is optional and approval-gated. Requests go only to the fixed Openverse HTTPS API through a no-proxy, no-redirect client that rejects local/private network destinations. Query length, media type, licence filter, result count, response bytes, and process-memory cache lifetime are bounded. Remote text is labelled untrusted. Raw result payloads are available only to the current planning loop, replaced with a redaction marker in SQLite, and omitted from the persisted conversation transcript.

## Verified scope

The latest offline first-person acceptance uses template kit 0.16.32: original joint-based walk clips, firing/reload poses, visible swept bullets, armed enemies with bounded magazines/reload and cover checks, and an actual standalone combat-exercise gate. A fresh prompt-driven production run passed all 20 steps, Unity EditMode 16/16, PlayMode 7/7, deterministic objectives 6/6, Windows build, smoke and rendered gameplay profiling. The default workstation configuration disables Meshy, Seele and online catalogue search. See [docs/Offline-first-level.md](docs/Offline-first-level.md) for operation and limits. This is template-driven original procedural content, not finished Blender characters, automatic production art, or general any-genre game generation.

As of 26 October 2026, explicit creation prompts for the four executable genres route through a persistent game-production workflow before the local model's general planning loop. ForgeCore saves the blueprint and complete dependency chain, searches the reviewed vault by art role/style, prepares Unity, creates the campaign, verifies every level, captures a preview, builds Windows, smoke-tests the executable, and profiles scripted gameplay. Queue execution now honors an active scoped trusted-project grant after policy validation. Failed stages block dependent work, and approval recovery resumes exact saved steps. Build validation remains separate from full-game fulfillment: coherent production assets, their acquisition/placement/animation, extra systems, visual/play-feel review, and release gates remain unfinished. Debug and Release regressions pass 229/229; the Release Desktop build has zero warnings/errors.

The current source and live-installed Unity game-template kit is `0.16.29`. Its Play Mode tests drive normal runtime paths for start, movement, genre action, pause/resume, respawn, grounded jumping, next-level loading, persistent unlocks, enemy damage, and combat completion. The fixed `scripted_gameplay_v1` profile rejects evidence without controller movement and genre action. Live Campaign V5/V13 acceptance passed Unity compilation, EditMode 15/15, PlayMode 3/3, the exact three-scene Windows development build, startup smoke, and a ten-second 1280x720 RTX 3070 gameplay profile. That profile recorded 19,624 gameplay frames, 60.250 m travel, 13 actions, 0.703 ms frame p95, 0.208 ms GPU p95, and 167,238,865 bytes peak allocated memory. A 1,494.696 ms maximum hitch is retained in the evidence. These are measurements of the current procedural content; production art, representative loads, and additional machines still require acceptance. Restart recovery revalidates persisted work, pauses for outstanding approvals, and quarantines ambiguous in-flight work. The reviewed-asset vault is implemented and regression-tested, but the real watchtower still needs live visual and rights reviews before archival.

Build and test commands and their final results are recorded in `TEST-RESULTS.md`. Features listed in `ROADMAP.md` under Later are not implemented.

The production workflow was also accepted live on a fresh one-level first-person campaign: all 20 saved stages passed through Unity to an executable, with objectives 6/6 verified before building. All eight asset-role searches returned no accepted candidates. The generated preview remains procedural blockout art, so automatic production-art acquisition, placement, and animation are still required for the full game-generation goal.
