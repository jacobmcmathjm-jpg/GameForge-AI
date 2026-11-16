# Standalone muzzle-flash investigation — 29 October 2026

The user confirms the v6 effect is **purple** and does not resemble a muzzle flash. This is a failed user visual check, not approval of the earlier isolated render. The death animation also remains unaccepted; its exact remaining defect has not been specified. Preserve the living movement and do not claim S09/S10/S11 progress from fixing an effect.

## Reproduction and limits

The running v6 path and its EXE/gameplay assembly/level0 hashes match the delivered candidate. That rules out a simply misidentified old executable, not every possible rendering problem.

An opt-in development-player diagnostic now invokes the real `SecurityCombatPlayer.Simulate` fire input, waits for the post-animation shot marker and captures the actual first-person camera in the exported Windows player. It runs only with `-forgecore-render-probe` in a development build, not in the Editor or ordinary play. Its PNGs/report go into a fresh unique folder next to its own executable; it quits after bounded diagnostics. The launcher times out and terminates only the diagnostic process it created, never the user's player/editor. This is controlled rendering evidence, not a complete user-input playthrough or art acceptance.

The first diagnostic (kit 0.16.62) failed its Windows compile because the readonly input frame was mistakenly initialized with an object initializer. That failure is retained. The corrected 0.16.63 diagnostic uses the input constructor and builds. Its actual D3D12 captures show the existing effect as solid yellow triangles: 740 warm pixels in hip fire, 245 in ADS, zero magenta in those controlled shots. This does **not** reproduce or explain the user's purple symptom. It does confirm the poor hard-edged shape. The initial hit-feedback capture was too early to establish particle appearance; the next probe waits for particle simulation.

Sandbox-profile Unity initially exited 198 for lack of a usable license. The authorized normal workstation profile compiles/builds using its existing license; no activation, credentials or licensing settings were changed.

## New candidate — kit 0.16.64

- Replace opaque hard triangle prongs with soft additive crossed gas plumes and axial white/amber flare layers, short fade/shrink, small warm light and varied axial orientation. Still original procedural prototype VFX, not photographic fire or a finished audio/graphics pass.
- Bundle a small single-pass `ForgeCore/SoftWarmFeedback` shader under the kit's Resources directory and load that exact resource. It has no runtime Standard emission keyword/variant dependency, uses additive transparency with depth testing, and does not write depth. Missing/unsupported resources fail closed rather than intentionally drawing an error material.
- Use the same bundled shader for generic feedback particles and security projectile/trail materials, retaining lifecycle ownership, swept damage and bounded effect counts. No arbitrary imported shaders/packages are installed and no global project graphics settings are changed.
- Expand the standalone diagnostic to capture actual guard firing as well as hip fire, ADS and simulated hit particles, require real shot/projectile emission and visible warm shot pixels, and reject magenta in these captures. A passing probe still cannot certify every device/setting or override the user's next visual check.
- No death/living animation curves, rig, scene layout or movement math are changed in this candidate. Existing v6 scene SHA-256 remains `394e896d08a8f805858bddb5dfa2b9cb7d86b141fb5313390e7e6e4a2f4d136c`. Existing scenes/builds and exact-version protected kit backups are retained.

Build, regression, standalone capture, delivery and source-checkpoint results are recorded below only after they execute. Source-only checks and the previous 535-test/v6 results are not new v9 passes. No real source review/promotion, paid API, new asset download or GitHub upload is included.

## Final candidate: v11 / kit 0.16.66

The v9 capture showed a circular glow, so v10 adds uneven soft axial lobes while preserving the short fade and crossed gas plumes. v9/v10 did not pass the guard diagnostic: the first guard was still inactive behind the authored power-objective gate. v11 calls the real mission `TryInteract` power transition before observing the normal guard AI, rather than bypassing activation with `SetActive`. Failed diagnostic reports are retained; none was counted as a full pass. These revisions alter the diagnostic/flash only, not the living/death animation curves or mission logic.

Final Windows build succeeds. Actual exported-player **D3D12** diagnostic exits **0**, confirms both player and guard shot markers/projectiles, and captures seven simulated hit particles. Warm pixel counts: hip baseline 8 → shot **1,096**; ADS baseline 16 → shot **396**; guard baseline 44 → shot **4,141**; hit-particle capture **25,976**. All seven captured before/shot/feedback images have **0 magenta pixels**. All captured transient materials use the bundled one-pass `ForgeCore/SoftWarmFeedback`, with shader supported. Hip and guard images were inspected. These are fixture-specific rendered facts; the precise cause of the user's purple v6 image remains unproven, and revised realism/visual acceptance is still pending.

Receipts/logs: `work/s08-s10-verification/build-soft-flash-v11.json`, `standalone-soft-flash-v11.log`. Actual PNG/report directory is the unique `ForgeCoreRenderProbe-*` subfolder of `D:/ForgeCore/Workspace/Games/ForgeCore/Builds/S10-SoftFlash-v11/`. Normal launch has no probe flag, so does not automate shooting or exit after the diagnostic.

Final host suite: **536/536** (250 Core, 40 Agent, 246 Security/Tools), zero failed/skipped; `work/soft-flash-v11-host/`. Matching self-contained Windows app publishes and passes offline `--verify-package` with exit 0; its safe workstation configuration matches the preceding verified offline delivery by hash. Cloud/catalogue flags and credentials are not newly enabled. Unity regression and final delivery integrity results follow separately.

Final Release solution build: **zero warnings/errors**. Unity **19/19 EditMode** passes. All **45 installed kit files** match the 0.16.66 manifest; all **76 existing v6 animation artifacts** and the v6 scene remain hash-identical. This includes unchanged living and death clips, not a new death fix.

## Delivery integrity

- App: `outputs/ForgeCore-Windows-x64-20261029-SoftFlashV11/ForgeCore.Desktop.exe`, **481 files / 265,576,204 bytes**, manifest SHA-256 `fb2f6d4c2e81a5ef5540f1a73f4e8e3c918fbd8f1c1c82c6e3a32600c8f85359`.
- Game: `outputs/ForgeCore-TestGame-20261029-SoftFlashV11/S10-SoftFlash-v11.exe`, **343 files / 215,701,224 bytes**, manifest SHA-256 `c2472a63818231ae1f9bad85a967b3c6199028355266c84700e9fce887ee9b28`. Includes the opt-in probe's actual PNG/report evidence. Keep the whole folder together; run normally without the probe flag to play.
- Every listed payload hash, size and aggregate verified. Six shipped Core/Tools/Agent/LocalAI/Security/Memory assemblies match final tested Release outputs. The user's currently running v6 player is not replaced, closed or remotely updated. Close it yourself before opening v11 to avoid confusing the two windows.
- Earlier backups/deliveries and diagnostic failures are retained. No source checkpoint is claimed until its independent receipt is written; a checkpoint's hash readback is not an independent restore/test pass.

Final Unity regressions: **19/19 EditMode**, **72 passed / 0 failed / 2 intentional graphics skips** in headless PlayMode, and **74/74 graphics-enabled PlayMode**, zero failed/skipped in the graphics run. Receipts: `edit-tests-soft-flash-v11.json`, `play-tests-soft-flash-v11.json`, `rendered-soft-flash-v11-playmode.xml/.json/.log` under `work/s08-s10-verification/`. The shipped app's offline probe also exits 0; zero nonempty actual credential fields, cloud generation/catalogue disabled. Prototype camera captures and test passes still do not supply the user's visual acceptance.

Sanitized source checkpoint location: `backups/20261029-soft-flash-v11/`; its separate `verification-receipt.json` records archive hash/readback and the evidence above after creation. Independent restoration and testing of that archive are not performed. Active outcome statuses are copied in `docs/archive/2026-10-29-soft-flash-v11-outcomes.md`. Existing user v6 and other players are not terminated; the only players automatically closed were owned opt-in diagnostic processes.
