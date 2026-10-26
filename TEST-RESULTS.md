# Test Results

## 26 October 2026 — first local Blender barrel pipeline

- Added bounded original local Blender creation, exact-source library reuse, separate non-overwriting import and a deterministic Desktop prompt route through Unity structural inspection/review preview. No model inference, API key, paid provider, download or package install is needed.
- Real Blender 5.2.1 LTS produced a 6,540-triangle, two-material PBR barrel with editable source and a grounded pivot. Unity imported it with canonical transforms and 5,715 split vertices; identical source was reused into fresh destinations.
- Real in-game rendering exposed a material overwrite bug. Kit 0.16.33 now limits procedural finishes to explicitly Yard-tagged geometry, retaining imported PBR slots/textures; a Unity regression test was added.
- Release host suites pass 259/259 (Core 119, Agent 37, Security/Tools 103). Real Unity compile, structural evidence, review sheet, fresh scene placement with box collision, gameplay (movement/respawn/6-of-6 objectives) and in-game render passed. Further player/test/delivery results are recorded in [the milestone report](outputs/ForgeCore-v037-local-blender-barrel.md).
- Original source, bundle hashes, evidence paths, snapshots and explicit unfinished art/review requirements are recorded in that report. Existing scenes/builds and both package files are preserved.
- Final Unity tests pass 16 EditMode / 8 PlayMode. Real Blender cancellation cleanup passed. The exact development player passes startup and the bounded rendered profile, but a 1,727.84 ms maximum frame interval was recorded despite passing p95 gates: smoothness remains unresolved, not certified. New app/game deliveries have fully verified SHA-256 manifests; the copied app passes `--verify-package` (exit 0).

## 26 October 2026 — offline animated-combat level generation

- Kit 0.16.31 introduced original hip/knee joint walk clips, first-person weapon kick/reload/magazine poses, visible swept projectiles and armed enemies with cover, magazine/reload and pause checks. Kit 0.16.32 adds camera-to-muzzle cover checks, a standalone-safe bullet shader fallback and a rendered-profile combat-exercise requirement.
- Current Release host suites pass 229 tests (Core 113, Agent 37, Security/Tools 79), none failed/skipped. Release Desktop publish has zero warnings/errors; offline package dependency probe passed.
- Two fresh real production-service requests each passed all 20 queue steps. Current kit 0.16.32 queue: `78289439-8591-4a33-9d21-d055decb3da9`; scene `Assets/Scenes/Productions/Game-9c2948725dc44812806fa9e179f75a72-Level01.unity`; build `Builds/Game-9c2948725dc44812806fa9e179f75a72/Game-9c2948725dc44812806fa9e179f75a72.exe`.
- Unity 6000.5.5f1: compile passed, EditMode 16/16, PlayMode 7/7. Four new runtime tests verify joint/weapon pose sampling and recovery, travel-before-damage/thin-cover collision and hostile damage, real player fire/reload inputs tied to ammo/animation, and enemy cover/fire/reload/pause behavior.
- Deterministic gameplay verification passed movement 2.50 m, respawn and objectives 6/6. Exact source build startup smoke passed ten seconds. New 1280x720 runtime preview passed its bounded error-material/luminance sample and was visually inspected; SHA-256 `02595681cfce7035721f1893beefd892834958e2da394a1d156aca809c72be53`.
- Ten-second rendered profile passed: 18,583 frames, p95 frame 0.7491 ms, maximum frame 2.7252 ms, p95 GPU 0.335872 ms, peak allocated memory 176,033,336 bytes, 59.1815 m travel, 20 action events, minimum health 88, 657 renderers. These are short low-complexity workstation results, not production-art or long-soak certification. The profile completed 0/6 objectives; separate deterministic verification completed six.
- Actual standalone log gate confirmed `player_bullets=16, enemy_shots=41, player_reload_animations=7, enemy_reloads=9`. The gate rejects a new animated first-person profile that fails to exercise player bullets, enemy firing or player reload.
- Current evidence `.forgecore/unity/player-profile/evidence/53c13a3603f74daaaee7c0c210bb5c0d.json`, SHA-256 `5c8b0ce2efb5fd9afd706b30177dc6512e544d0405ad05c7f9b27b002b202977`. Gameplay evidence `.forgecore/unity/gameplay-evidence/2c59f64c96f842d785a02562492749e9.json`.
- Exact upgrades retained protected backups: 0.16.30→0.16.31 `db7d38f33a2b4dc38baf9f5aa5e38327`; 0.16.31→0.16.32 `41d9593fef4c47178e90a87a586c015e`. Protected package manifest/lock stayed byte-identical. Earlier scenes/builds and deliveries were not overwritten.
- Current development and delivered config disable Meshy, Seele and online catalogue search. No paid generation or provider API key was used. All eight vault searches were empty; no human rights/visual review or approved production asset was fabricated.
- Final copied app 481 payload files / 264,081,123 bytes and game 335 files / 205,748,150 bytes passed every SHA-256/length/count/aggregate check. Manifests exclude themselves. Final app `--verify-package` returned 0; copied game survived a five-second headless startup probe and only that probe process was stopped. This does not claim a normal GUI/model-inference test or copied-game full playthrough.
- Remaining: finished Blender assets/rigging, production-quality character animation, complete visible player-body animation, audio/music pipeline, navigation and game-feel iteration, long playtests, accessibility/settings and general any-genre autonomous production.

## 26 October 2026 — industrial-yard visual pass

- Template kit 0.16.30 adds original industrial scenery, shared procedural material/normal maps, daylight/fog/sky, a multi-part weapon and humanoid-shaped targets with simple leg motion. Other genres and legacy scenes without the art-direction component retain their previous visual path. This is not Blender integration or photorealistic character acceptance.
- Release host suites: 229 passed (113 Core, 37 Agent, 79 Security/Tools). Unity 6000.5.5f1 compilation passed, EditMode 16/16 including the new yard serialization/collider contract, PlayMode 3/3, and deterministic gameplay movement/respawn/completion 6/6 passed.
- Protected exact kit upgrade 0.16.29 to 0.16.30 retained backup `abcd4cae03414b4ba36a80b12d69875d`. A new `IndustrialYardV1-Level01.unity` scene and `Builds/IndustrialYardV1/ForgeCoreIndustrialYardV1.exe` were created without replacing prior accepted scenes/builds.
- Actual 1280x720 runtime preview passed error-material/luminance checks and was visually inspected. PNG SHA-256: `9b5b01a836dcf636a82735942c5262bcef5273124f0878e55a0552cb122c8b12`. It remains visibly procedural; visual review is not a rights/library attestation.
- Exact player startup smoke passed ten seconds. Ten-second rendered scripted gameplay profile passed its existing workstation baseline: 19,092 frames, p95 frame approximately 0.70 ms, p95 GPU approximately 0.36 ms, peak allocation 173,883,589 bytes, travel 58.13 m and 15 actions. These short low-complexity measurements do not certify AA-quality content or a long soak. Deterministic gameplay and scripted profiling are distinct checks.
- Profile evidence: `.forgecore/unity/player-profile/evidence/6981b31e479c4b8583953fe723a5b085.json`. Protected package manifest/lock stayed byte-identical. No paid generation, external asset acquisition, fabricated approvals or package installation was used.
- Profile maximum frame was 59.80 ms, 595 renderers were counted, and the profile completed 0/6 objectives with minimum health 32. Thus the short baseline is not hitch-free/full-playthrough acceptance; separate deterministic verification completed 6/6. Evidence SHA-256 `c770c601876f1a6b2c56b1669ed68a06d77ead713cc593bea53094d07effe57b`.
- Self-contained updated Desktop publish succeeded and passed its offline `--verify-package` check, exit 0. Windows deliveries use the new `20261026-IndustrialYardV1` label; prior deliveries remain intact.

## 26 October 2026 — self-contained Windows delivery

- Release self-contained win-x64 publish succeeded with .NET/WPF 10.0.11 included. Final app folder passed its non-mutating `--verify-package` mode, exit 0; no normal GUI/store/model startup was attempted by that check.
- Release suites rerun: 229 passed (113 Core, 37 Agent, 79 Security/Tools), none failed/skipped.
- Delivery payload SHA-256/size verification passed for 481 app files (263,854,852 bytes) and 334 Unity game files (204,676,235 bytes). Manifests exclude themselves. The copied game executable matches the previously accepted source build.
- The copied player survived a five-second headless startup probe, then that probe process was stopped. Its log includes Unity cloud-config/telemetry connection failures under restricted networking. This is not rendered-gameplay, long-soak, model-inference or indie/AA quality acceptance.
- Publish profile and no-overwrite delivery script are reusable. The source script parses successfully; Windows-native dependency filtering is validated by the delivered app probe. Automatic future delivery, generated production art/audio, telemetry release configuration and visual/playability gates remain open.

## 26 October 2026 — production-workflow integration

- Debug and Release each pass 229 tests: Core 113, Agent 37, Security/Tools 79. Release Desktop build passes with zero warnings/errors.
- New regressions exercise all four executable genres at twelve levels within the persisted 32-step limit, matching creation/build scene order and runtime-derived seeds, role/style vault discovery, unique destinations, and explicit creation intent without intercepting asset/file/menu questions.
- Real queue/store integration tests prove plain creation prompts bypass model tool selection, persist their complete blueprint/workflow, execute beyond the general chat action limit within the independent queue bound, resume exact approved work after reopening the store, preserve open art/system requirements, and block dependent build steps on failure. Policy-denied work does not consume an active trusted-project grant.
- These host tests use injected tool results. Actual Unity runtime acceptance is tracked separately; passing the host suite does not certify visuals, complete assets, or the full requested game.
- The real production service subsequently passed all 20 stages of its fresh one-level first-person queue `ab628e8f-eef7-4308-b401-7a86b4072576`. It created `Assets/Scenes/Productions/Game-19faa8e4693c4f78a8d1dddfa8d04839-Level01.unity`, passed EditMode 15/15 and PlayMode 3/3, verified movement/respawn/objectives 6/6, captured a 1280x720 preview, built the exact Windows player, passed ten-second smoke, and passed ten-second rendered scripted-gameplay profiling.
- Production profile: 23,081 frames, 79.046 m travel, 19 actions, minimum health 3, frame p95 0.616 ms, maximum frame 5.185 ms, GPU p95 0.215 ms, peak allocated memory 167,376,529 bytes. Profile SHA-256 `64aa2cc0b3db61c0ab493c9df5de2903a21dad8f776e19dff9d52a233926e758` at `.forgecore/unity/player-profile/evidence/5851b72616514fa49772bfeaef8ab00c.json`. The profile completed 0/6 objectives; completion is proven by the separate gameplay stage.
- All eight vault-role searches returned no accepted candidates, so no production assets were imported. The captured preview was inspected and remains procedural blockout art. Package hashes stayed unchanged; Unity exited and temporary profile request/result files were removed.

## 26 October 2026 — V13 scripted gameplay accepted live

- The exact template kit 0.16.28 upgraded to 0.16.29 with protected backup `9b5a00934ee24deabb09abf23d0b6247`. Unity 6000.5.5f1 compilation, EditMode 15/15, PlayMode 3/3, the exact three-scene V13 development build, and ten-second startup smoke all passed.
- Ten-second 1280x720 Direct3D 12 gameplay profile on RTX 3070: 19,624 gameplay/rendered frames, 60.250 m travel, 13 action events, health minimum 61, frame p95 0.703 ms, GPU p95 0.208 ms, peak allocated memory 167,238,865 bytes, maximum hitch 1,494.696 ms; all configured baseline budgets passed.
- The profile completed 0/6 objectives; it verifies exercised movement/action/damage paths and performance, not a complete campaign playthrough. The scene still contains 44 renderers, four unique meshes, and 1,692 unique-mesh triangles.
- Evidence: `.forgecore/unity/player-profile/evidence/83ed3ad9abac497baaa6e3ed6cd01f1a.json`, SHA-256 `54002298a5263a0944ec48896ee27c71179c8d84bb70eb70d756dccc2b886c37`; V13 provenance SHA-256 `9cbf7a8ce5230afa61e7c0382b2d8c2f18379d3411047eefa2debf65a5f3bea5`; kit-manifest SHA-256 `4d8722b43fee22bb39beedaf19d4e9516fe603557b2be4607bf6a7e2d65d9f26`.
- Package hashes remained unchanged: manifest `c9fcb3ab2c704410ba18162adcf0f8921efe548ec0a31bd8679bb0e85f504712`, lock `615353b78c74667e25c3b792598a3dd5d89d52f80083586e978676db681e0bb5`. Transient profile request/result directories were empty afterward.

## 1 August 2026 - fixed scripted-gameplay standalone profile (host accepted; live pending)

- Source template kit `0.16.29` accepts only the hash-bound `scripted_gameplay_v1` scenario and uses the normal actor controllers to exercise bounded genre-specific movement/action during visible standalone profiling.
- Schema-2 host evidence requires positive player travel and action events plus bounded gameplay frames, objective state, minimum health, supported template identity, and the exact scenario; idle/mislabeled results fail closed.
- Planner grammar/prompt, dedicated-verification tracking, scoped policy, and permission-dialog detail are integrated.
- Debug and Release regressions each pass Agent 32/32, Core 98/98, Security/Tools 79/79 (209/209 total). Isolated Release Desktop build `work/desktop-validation-v032` passed with zero warnings and zero errors.
- The real protected 0.16.28-to-0.16.29 upgrade, Unity compile/tests, fresh Campaign V13 build, smoke, and gameplay profile are prepared. The approval service rejected the live command because the account usage limit was reached until 8 August 2026 06:33, so Unity was not launched and the real project remains on verified kit 0.16.28/V12 evidence below.

## 1 August 2026 - standalone rendered-player performance evidence

- Debug and Release regressions: Agent 32/32, Core 98/98, Security/Tools 79/79; total 209/209 in each configuration.
- Isolated Release desktop build `work/desktop-validation-v031` passed with zero warnings and zero errors; the running ForgeCore Desktop process remained responsive and was not stopped, replaced, or restarted.
- Template kit `0.16.28` upgraded through protected backups. The first live `0.16.27` compile exposed Unity 6.5's hard removal of `Object.GetInstanceID`; the exact failed installation was registered as a hash-verified predecessor and upgraded to a `HashSet<Mesh>` implementation rather than overwritten in place.
- Real Unity `6000.5.5f1`: compile passed, EditMode passed 15/15, PlayMode passed 3/3, and a fresh non-overwriting Campaign V12 development build contained exactly the three Campaign V5 scenes.
- Schema-3 build provenance binds Editor `6000.5.5f1`, kit `0.16.28`, template-manifest SHA-256 `a24e7636b4e004f3f4ccf55574a8d69748f9eca2fd3579d102017e5fa8287209`, executable SHA-256 `6456584d36a1ed8f5a4662a524f11bf1da4c580870aa6257e4cf086300f4f615`, `level0` SHA-256 `36e55c2a9ff87a00627598ceb5b2a5f38be84036bc875acc93e181a6a45ed3ae`, and all three source-scene hashes. The 10-second headless startup smoke also passed.
- Visible standalone profile: 1280x720 windowed Direct3D 12 on NVIDIA GeForce RTX 3070, quality level 0, 13,903 sampled frames, 0.799 ms average, 1.479 ms p95, 1,555.079 ms maximum single-frame hitch, 12,123 GPU samples, 0.245 ms GPU p95, and 166,431,889 bytes peak allocated memory. The 33.334 ms p95 frame/GPU and 4 GiB memory baseline passed.
- Scene-complexity context: 44 renderers, 4 unique meshes, 1,177 vertices, 1,692 triangles, and one camera. This is a low-complexity procedural blockout baseline; the strong p95 result is not representative production-art or shipping-quality evidence, and the maximum hitch remains visible in the retained record.
- Evidence: `D:\ForgeCore\Workspace\Games\ForgeCore\.forgecore\unity\player-profile\evidence\0630f4518371400fac42242f9fe9ea33.json` (file SHA-256 `73f4ffece7971e40691a4433bde8a943b1ff64f4475e04be0e6cf9f667862ee0`). Build provenance: `D:\ForgeCore\Workspace\Games\ForgeCore\Builds\FirstPersonCampaignV12\ForgeCoreFirstPersonCampaignV12.exe.forgecore-build.json` (SHA-256 `e632a31aa2578b1f52290b8e6abc127d508b3e59a73f95d2f72a7a7e92f546a0`).
- Package protection held byte-for-byte: `manifest.json` SHA-256 `c9fcb3ab2c704410ba18162adcf0f8921efe548ec0a31bd8679bb0e85f504712`; `packages-lock.json` SHA-256 `615353b78c74667e25c3b792598a3dd5d89d52f80083586e978676db681e0bb5`. No Unity process remained, and one-use profile request/result directories were empty after acceptance.

## 1 August 2026 - Editor performance evidence

- Debug and Release regressions: Agent 32/32, Core 97/97, Security/Tools 78/78; total 207/207 in each configuration.
- Isolated Release desktop build `work/desktop-validation-v030` passed with zero warnings and zero errors; the running ForgeCore process was not stopped or replaced.
- Real Unity `6000.5.5f1` compile passed through bridge `0.17.15` after live acceptance exposed and corrected two Unity 6 batch-mode assumptions (the removed `UnityStats.batches` API and non-advancing `Time.frameCount`). Each exact failed bridge was retained as an enumerated hash-verified predecessor and upgraded through the normal protected-backup path.
- Campaign V5 Level 2 ten-second profile: 9,848 Editor updates, average 0.309 ms, p95 0.342 ms, maximum 160.742 ms, peak allocated memory 619,377,655 bytes; the Editor update/memory baseline passed.
- Unity batch mode reported no render counters. Schema-2 evidence records `renderMetricsAvailable=false`, `renderBudgetPassed=false`, and `editorBaselineBudgetPassed=true`; it does not claim rendered-frame, GPU, or target-hardware acceptance.
- Evidence: `D:\ForgeCore\Workspace\Games\ForgeCore\.forgecore\unity\performance-evidence\955e7005758b41d2aca2b4733d1aa15a.json`.

## 1 August 2026 - persisted offline queues and reviewed-asset vault

- Debug and Release regressions: Agent 32/32, Core 96/96, Security/Tools 77/77; total 205/205 in each configuration.
- Isolated Release desktop build passed with zero warnings and zero errors. The ordinary Release output was locked by the running ForgeCore process, so validation used `work/desktop-validation-v029` and did not stop, replace, or restart the active app.
- Persisted-queue regressions cover dependency order, process/store restart, exact approval continuation, current-policy denial, concurrent claim at-most-once behavior, cancellation quarantine, foreign-owner `RequiresReview`, and explicit unattended-agent persistence.
- Live non-destructive queue acceptance used the real `D:\ForgeCore\Workspace` guard and production executor for two read-only calls. The first run stopped after one step, a new SQLite store/service instance resumed the dependency, and the queue moved from `Running` to `Completed` without launching Unity or changing the project.
- Asset-vault regressions prove typed offline search, current structural/visual/rights gate enforcement, canonical content addressing, idempotent exact storage, non-overwriting atomic target import, permission downgrade rejection, replay refusal, full-file re-verification, and tampered-bundle rejection without a partial destination.
- The live offline vault search for `frontier watchtower` returned zero entries. This is expected: the current real watchtower has valid schema-2 structural evidence but no live visual or rights review records, so ForgeCore did not archive it or create an authority-bearing vault entry.
- No external API, package installation, credential use, Unity launch, source-project write, or production-app deployment occurred during this acceptance.

## 1 August 2026 - campaign progression, combat, and restart-safe approval recovery

- Template kit `0.16.26`: the exact `0.16.24` upgrade created protected backup `c6f8f2e9a1aa406b83f4223498755272`. The first `0.16.25` compile exposed a missing namespace import; that exact failed installation was preserved and safely upgraded through backup `9344438ef2c943038dbe79b0b850898f` rather than overwritten silently.
- Unity compile passed through bridge `0.17.11` and Editor `6000.5.5f1`; EditMode passed 15/15 and PlayMode passed 3/3.
- New real-runtime tests prove exact next-scene requests, monotonic campaign unlock persistence across reload, non-regression after replay, target chase, cooldown-limited contact damage, control-frame attack cadence, target defeat, and objective completion.
- Campaign V5 current-kit revalidation: all three levels passed at 2.50 metres, respawn passed, and 6/6 objectives completed.
- Fresh non-overwriting visual evidence: `ForgeCoreFirstPersonCampaignV5-Level02-Runtime2.png`, 1280x720, 571,388 bytes, SHA-256 `156db9ecba3739cd1cec5063fdd319e56357b9010c659f8c4a392a998ba71a55`; sampled error magenta 0.00%, luminance 0.067-0.813. Human inspection still classifies it as a readable procedural blockout, not production art.
- Fresh non-overwriting Campaign V11 build contained exactly the three linked scenes and passed provenance validation plus a 15-second responsive clean-start player smoke test.
- Approval recovery regressions prove atomic one-time claim/execution, current-policy revalidation and denial, task completion, and live-only art attestation. Actions already marked executing are not blindly replayed after a crash.
- Debug and Release .NET regressions: Agent 25/25, Core 91/91, Security/Tools 77/77; total 193/193 in each configuration.
- Isolated Release desktop build passed with zero warnings and zero errors; the running installation was not replaced or restarted.
- No external API, network package install, credential use, publishing, broad computer control, or overwrite occurred.

## 1 August 2026 - frame-driven controls and Campaign V5 acceptance

- Template kit `0.16.24` exact upgrade: four enumerated predecessor upgrades each created a protected backup; modified or unknown installations remain non-overwritable.
- Unity compile: passed under bridge `0.17.11` and Editor `6000.5.5f1`.
- EditMode: 15/15 passed, including clamping of scripted analogue axes.
- PlayMode: 1/1 passed across first-person action, platformer, top-down RPG, and racing. The fixture used normal runtime `Update` paths over real elapsed frames to prove start, movement, genre action, pause/no-motion, resume, respawn, and a grounded platformer jump.
- Fresh non-overwriting Campaign V5: all three levels passed scene-specific runtime verification at 2.50 metres and 6/6 objectives; catalog status is 3 current, 13 stale historical, 0 invalid.
- Visual evidence: Level02 1280x720 PNG, 571,388 bytes, SHA-256 `156db9ecba3739cd1cec5063fdd319e56357b9010c659f8c4a392a998ba71a55`; 0.00% sampled error magenta and luminance 0.067-0.813. Human inspection found a coherent procedural prototype composition, not production art.
- Campaign V10 build: exact three-scene development build, executable SHA-256 `6456584d36a1ed8f5a4662a524f11bf1da4c580870aa6257e4cf086300f4f615`, `level0` SHA-256 `36e55c2a9ff87a00627598ceb5b2a5f38be84036bc875acc93e181a6a45ed3ae`; player remained responsive for 15 seconds with a clean startup log.
- Defects reproduced and fixed before acceptance: configuring an active particle system, batch frames advancing too quickly to prove meaningful motion, a platformer jump injected before grounding, and NUnit failures hidden by an opaque Editor result.
- Debug and Release .NET regressions: Agent 22/22, Core 91/91, Security/Tools 77/77; total 190/190 in each configuration.
- Isolated Release desktop build: passed with zero warnings and zero errors at `work/desktop-validation`; the running ForgeCore installation was not replaced or restarted.
- No external API, network package install, credentials, publishing, overwrite, or broad computer-control action occurred.

## 1 August 2026 - exact-hash human art-review gate

- Full Release solution regression: Agent 22/22, Core 91/91, Security/Tools 77/77; total 190/190.
- Isolated Release desktop build: passed with zero warnings and zero errors; the running ForgeCore Desktop installation was not replaced or restarted.
- Human-only boundary: native model parsing cannot emit an art review, trusted-project mode cannot pre-authorize one, compact inbox controls cannot approve one, and proposal arguments cannot contain a forged decision.
- Decision behavior: visual and rights approvals are separate; explicit rejection records a current false gate; cancellation records no art decision; a later exact-hash approval may supersede a rejection.
- Evidence integrity: post-dialog tampering, stale source/preview hashes, malformed records, duplicate call ids, and non-overwrite violations fail closed; retained legacy structural evidence cannot become a current review.
- Live Review2 watchtower preview: 1280x720, four views, 239,333 bytes, PNG SHA-256 `67655759ab46c14b03abe810966a481880b51e61e7e01cb53d9bfbf0f60424d1`, sidecar SHA-256 `38156bc6e846c05a49fed64f413a4eab26094b054a87f96bbc7efc8454083b8c`, bridge 0.17.11, Editor 6000.5.5f1.
- Live review state: zero art-review records; `visualReviewed=false` and `rightsReviewed=false` remain intentional until the user completes both live attestations.

Verified on 2026-07-30 with .NET 10 on Windows 11 x64.

## Release build

Command: `dotnet build ForgeCore.sln -c Release -p:NuGetAudit=false`

- Result: passed
- Warnings: 0
- Errors: 0

## Unit tests

Command: `dotnet test ForgeCore.sln -c Release --no-build --no-restore -p:NuGetAudit=false`

- ForgeCore.Core.Tests: 38 passed
- ForgeCore.Security.Tests: 59 passed
- ForgeCore.Agent.Tests: 16 passed
- Total: 113 passed, 0 failed, 0 skipped

Coverage includes workspace boundary/traversal checks, permission downgrade and approval rules, exact command allow-listing, cancellation, emergency stop, task transitions, rejected-call non-execution, SQLite persistence, process metrics, streamed local-model orchestration, strict structured-plan parsing, fail-closed model/Unity/Seele/Meshy configuration, bounded conversation history and observations, deterministic project/map scaffolding, exact patching, snapshots, rollback, offline .NET builds, running-process cancellation, guarded asset discovery/generation/import, fixed API request/authentication contracts, signed-query redaction, archive rejection, deterministic Unity bridge installation, typed operation validation, SHA-bound Unity protocol/results, locked/no-package and no-shell arguments, session-authorization scope/budget/revocation, scene snapshots, no-overwrite build targets, Unity test XML parsing, Unity/cloud cancellation, compiler-error replanning, duplicate suppression, and aggregate limits.

## v0.13 Meshy provider checks

- Parser accepts only the `meshy` provider paired with `meshy-6`; cross-provider model substitution and briefs exceeding the guarded provider limit are rejected.
- Deterministic supervised planning selects Meshy without invoking native inference when it is the configured provider.
- Policy fails closed when Meshy is disabled or its environment key is missing/malformed, rejects permission downgrades, and requires visible credit-consuming network approval.
- Provider contract verifies Bearer authentication, guarded preview prompt, exact preview identity, PBR refine linked to the preview task, refined GLB selection, bounded credit reporting, and absence of the key from request bodies.
- Cancellation interrupts polling. Signed download parameters are removed from the Meshy manifest while the GLB is hashed, structurally checked, and quarantined with `MESHY_GENERATED_REVIEW_REQUIRED`.

## v0.14 generated-asset preparation checks

- Parser accepts only the exact generated GLB directory, 64-hex hash, existing scene schema, bounded target height, fixed collider modes, and explicit placeholder flag.
- Deterministic planning safely upgrades the bridge, reads local provenance when necessary, and proposes exact-hash preparation without native inference.
- Policy requires the imported GLB plus provenance and configured matching Unity Editor. Executor tests verify provenance/hash binding, request fields, no-package batch protocol, protected scene backup, and fail-before-Unity behavior on a mismatched hash.
- Trusted-project authorization accepts this typed operation only for its exact project while cloud generation and other excluded actions remain outside the grant.
- Embedded Unity bridge source parsed as C# 9 with zero syntax errors across 27,769 characters.

## v0.12 supervised 3D generation checks

- Strict parser accepts only the fixed `seele` provider, supported model names, safe asset names, bounded prompts, exact fields, and derived `Network` permission.
- Policy fails closed when disabled or when the environment key is missing/malformed, and requires explicit approval when correctly configured.
- The task-session grant cannot consume a cloud-generation approval.
- Provider contract test verifies the versioned jobs endpoint, Bearer header, Unity engine parameter, guarded original-asset prompt, and absence of the key from the JSON body.
- Valid embedded GLB output is streamed, hashed, quarantined, structurally checked, and recorded without a signed query. Archive output is rejected and rolled back. Cancellation interrupts a pending generation.
- Live external generation: not run; it requires the user's private API key, eligible plan/credits, and informed approval.
- Crash regression: a live LLamaSharp/Vulkan request exposed a native grammar-sampler access violation. The release grammar was restored to its previously accepted tool surface, and supervised 3D requests now use a deterministic typed state machine. Tests verify generation, quarantine inspection, and exact-hash import planning without invoking the native model.

## v0.11 Frontier Valley acceptance

- Map kit: `0.11.3` installed with seven hash-verified source files; an existing mismatched kit is never overwritten.
- Editor: Unity `6000.5.5f1` compiled the kit and generated `Assets/Scenes/FrontierValley.unity` with seed `240730`.
- Play Mode: the complete generated scene entered and exited a bounded 20-second Play Mode run successfully.
- Windows build: `D:\ForgeCore\Workspace\Games\ForgeCore\Builds\FrontierValley\FrontierValley.exe` and its `_Data` directory were produced without overwriting an existing target.
- Standalone player: launched with Direct3D 11 on the NVIDIA RTX 3070 and remained responsive. The fresh player log completed engine, physics, input, scene, and GPU initialization with no corruption, `Position out of bounds`, crash, missing-script, or exception markers.
- Regression isolation: a minimal diagnostic scene also built and launched. The earlier Frontier build failed with a corrupt `level0`; replacing Unity `TerrainData` with a generated mesh removed that failure. A first corrected package pass was deliberately cancelled after diagnostics showed the wrong URP shader would expand an excessive variant set; map kit `0.11.3` prefers the built-in Standard shader and the final build completed in 38 seconds.
- Recovery: failed and superseded map/scene/build artifacts were moved to named local `.forgecore/unity/backups` folders; no material project artifact was deleted.
- Package guard: all direct dependencies were pinned and already cached locally. Unity package mode completed without modifying the protected manifest or lock file.

## v0.10 Unity MCP collaboration checks

- Typed parser: passed. `discover` accepts only project/operation; `call` requires a whitespace-free tool name and one bounded JSON object. Extra fields, malformed/non-object JSON, and permission downgrades are rejected.
- Policy: passed. The adapter is denied by default and remains denied without the exact relay, matching Editor version, workspace Unity project, or declared official Assistant package.
- Allow-list and consent: passed. Discovery requires approval without invoking a tool; calls require a separate approval and an exact case-sensitive configured tool name.
- Release client: compiled with fixed `relay_win.exe --mcp` process startup, no shell, current MCP 2025-11-25 lifecycle negotiation with explicit compatible revisions, bounded stdio JSON-RPC, timeout/cancellation, and process-tree termination.
- Live relay: passed against the open linked `D:\ForgeCore\Workspace\Games\ForgeCore` project with official Assistant `2.17.0-pre.1`. Discovery exposed the Editor's current tools, Console diagnostics completed, and the allow-listed `Unity_AssetGeneration_GetModels` action completed through ForgeCore's visible one-action approval window.
- Service result: Unity returned a successful response with an empty `Models` array. This proves the local policy/relay/Editor path but does not prove asset generation; Unity must make eligible models available to the linked account/project before ForgeCore can propose a generation.
- Planner regression: passed. The constrained LLama grammar now includes the already-typed `unity_mcp_action` kind and call schema. Two deterministic read-only Unity diagnostic intents bypass model warm-up but do not bypass policy or approval.

## v0.9 Unity bridge checks

- Core/parser: passed. Only the eight documented operation schemas are accepted; traversal, unknown fields, arbitrary script/component text, excessive Play Mode duration, and external build targets are rejected.
- Bridge installation: passed. The deterministic source and version manifest install transactionally, identical re-installation is harmless, and mismatched existing source is never overwritten.
- Process protocol: passed with an injected process boundary. ForgeCore supplied `-batchmode`, `-noUpm`, fixed method/request/result arguments, verified request SHA-256, rejected mismatched results, cleaned one-use request/result files, and retained bounded local logs.
- Recovery/no-overwrite: passed. Scene mutations created exact pre-change snapshots; scene creation also snapshots existing Build Settings before registering the new scene; existing scene and Windows `.exe`/`_Data` targets were denied.
- Tests/cancellation: passed. Bounded DTD-disabled NUnit XML was parsed, and cancellation propagated through the Unity process boundary.
- Agent flow: passed. Install, capabilities, scene creation, compile, EditMode tests, and Windows build were observed across bounded planning passes with six separate approvals.
- Generated bridge syntax: passed under Roslyn C# 9 with 0 syntax errors across 18,382 characters.
- Actual Unity Editor: passed on manually installed Unity `6000.5.5f1` with Windows Standalone support. ForgeCore performed a real version/hash-bound capability handshake, created and registered `Assets/Scenes/Main.unity`, added `ForgeCore Cube`, compiled the project, and built a no-overwrite Windows development player.
- Real player artifact: `D:\ForgeCore\Workspace\Games\ForgeCoreAcceptance600055f1\Builds\Windows\ForgeCoreAcceptance.exe` exists at 667,648 bytes with its `ForgeCoreAcceptance_Data` directory and Unity runtime files.
- Real backup path: scene creation preserved the existing `EditorBuildSettings.asset` before registering the scene, and primitive creation preserved the scene before mutation.
- Unity Test Framework limitation: a fresh project could not resolve NUnit while every ForgeCore Editor launch enforced `-noUpm`. The failed compiler attempt was stopped and repaired through SHA-checked patches; package-manager access was not silently enabled. Real Unity Test Framework execution therefore remains unclaimed until its package is pre-resolved manually, while the bounded XML parser, failure handling, and cancellation paths remain unit-tested.

## v0.8 guarded asset checks

- Valid quarantined PNG and OBJ inspection: passed. Dimensions/geometry and exact staged SHA-256 were returned without changing project files.
- Import separation: passed. Inspection derives `Read`; import derives `Write`, requires a distinct approval, and is bound to the exact hash and destination.
- Atomic provenance import: passed. The asset and `.asset.json` provenance sidecar were created without overwrite while the staged original remained available.
- Adversarial cases: passed. Post-stage byte tampering, stale approved hashes, malformed/extra manifest fields, project traversal, `.forgecore` destinations, permission downgrades, malformed PNG data, and external OBJ material libraries were blocked.
- Cancellation: passed before structural inspection and throughout the existing staged download/agent pathways.

## Additional checks

- `dotnet list ForgeCore.sln package --vulnerable --include-transitive`: no known vulnerable packages.
- Release desktop smoke launch: the v0.10 application opened against the configured `D:\ForgeCore` workspace/database/model layout, remained responsive, and exposed the new Unity MCP status badge. Disabled/missing Unity MCP correctly leaves collaboration unavailable without preventing startup.

## Hardware-dependent local inference check

- Model: official Qwen3 8B `Q4_K_M` GGUF (5,027,783,488 bytes; publisher SHA-256 verified).
- Backend: LLamaSharp 0.27 Vulkan on an NVIDIA RTX 3070 with 33 configured GPU layers.
- The running process loaded `runtimes\win-x64\native\vulkan\ggml-vulkan.dll`.
- Observed GPU memory during inference: approximately 6,001 MiB; approximately 2,018 MiB remained free.
- Visible WPF result: the request completed, the task transitioned from Running to Completed, and ForgeCore displayed `Confirm real local Vulkan GPU inference is working`.
- After completion, the UI reported `LLamaSharp/Vulkan: Model loaded locally and ready.`

## v0.4 real-model developer workflow check

- Natural response: passed. Qwen3 returned `I can help you build console applications, class libraries, and Unity games.` with no tool actions.
- Conversation history: passed. A follow-up asking which engine had been mentioned returned `I mentioned Unity as the game engine.`
- Typed project planning: passed. The model produced a `ScaffoldProject` call for `Projects/LiveGameTools` using the `dotnet-console` template.
- Approval UI: passed. The window displayed the exact description, policy reason, tool, permission, path, template, and project name before execution.
- Project execution: passed. Approval created the three expected source files inside an isolated workspace and transitioned the task to Completed.
- Generated-project build: passed with 0 warnings and 0 errors under .NET 10 Release.
- Approval-stage cancellation: passed. Stop closed the approval window, transitioned the task to Cancelled, and did not create `CancellationProbe`.

## v0.5 real-model coding-loop check

- Cautious first turn: passed safely. The model proposed a read-only inspection and made no mutation.
- Exact second-turn plan: passed. Qwen3 produced `ApplyTextPatch` followed by `DotnetBuild` with no unrestricted command.
- Patch approval diff: passed. The window displayed `- Hello from LiveGameTools!` and `+ ForgeCore v0.5 coding loop works!` before approval.
- Patch execution: passed. ForgeCore changed only the unique target text, created a `.forgecore/backups` snapshot, and returned the updated SHA-256.
- Dedicated build: passed. A separate Execute approval ran local-feed-only restore plus Release build; the generated project built with 0 warnings and 0 errors.
- Hash-guarded rollback: passed. Qwen3 produced `RestoreTextSnapshot` using the returned backup path and current hash; ForgeCore restored the original file and created a pre-restore safety snapshot.

The original v0.5 smoke used separate requests because the runner was one-pass at that point.

## v0.5 real-model single-request multi-pass check

- One request: passed. The request named `Program.cs`, an exact greeting replacement, the `.csproj`, and a Release build.
- Pass 1: Qwen produced `ReadTextFile`; ForgeCore read the current source without approval.
- Pass 2: Qwen used the typed observation to produce `ApplyTextPatch`. The approval window showed the exact one-line diff before approval, and ForgeCore created its protected backup.
- Pass 3: Qwen used the patch result to produce `DotnetBuild`. After separate Execute approval, local-feed-only restore and Release build passed with 0 warnings and 0 errors in 0.71 seconds.
- Pass 4: Qwen proposed an unnecessary non-allow-listed command to inspect the compiled DLL. ForgeCore denied it, did not execute it, recorded the denial, and completed with an explicit planning-boundary notice because typed .NET verification had already succeeded.
- Persistence: the audit database recorded each planning pass, proposal, policy result, approval, execution, denial, and `CompletedAtPlanningLimit` event. The source and output DLL were verified directly after completion; the isolated source fixture was then restored.
- Hardware and latency: inference used the configured Qwen3 8B Vulkan model on the RTX 3070. Each grammar-constrained planning pass took roughly 50–100 seconds in this smoke, so latency optimization remains necessary before this feels conversational.

## v0.6 project-index and visual checks

- Metadata-only indexing: passed. A test project persisted one supported source record while excluding `bin`, app settings, and literal secret/password values.
- Retrieval: passed. Symbol/identifier search returned the correct workspace-relative source path and symbol without storing or returning source bodies.
- Limits and cancellation: passed. A one-file cap produced an explicitly truncated summary; cancellation during transactional replacement preserved the prior index.
- Boundary policy: passed. Traversal and UNC index/search requests were denied. Recursive scanning skips reparse points rather than following them.
- Desktop visual smoke: passed. The Release WPF executable launched in model-free mode with the three-pane shell, native dark title bar, empty-conversation welcome state, visible emergency stop, index/local badges, task queue, audit stream, and runtime status.
- Visual QA artifact: `outputs/ForgeCore-v0.6-professional-ui.png` was captured from the running Release application after correcting text inheritance and empty-state presentation.

## v0.7 online catalogue checks

- Typed parser and policy: passed. `search_asset_catalog` derives `Network`, rejects unknown fields/media/licence filters and results above ten, is denied when disabled, and requires approval when enabled.
- Executor isolation: passed. Tests verify the fixed `api.openverse.org` host, mature filtering, HTTPS source/media requirements, compatible licences, response limits, cancellation, and no workspace writes.
- Ephemeral cache: passed. Identical bounded searches reuse one process-memory result and create no files.
- Persistence privacy: passed. Raw catalogue metadata reaches the next planning pass, but SQLite stores an explicit redaction marker and the durable conversation omits the raw result set.
- Live Openverse smoke: passed. The production executor returned three HTTPS `forest` image records under the CC0/public-domain filter with creator, source page, licence URL, preview, direct media URL, and attribution; no asset was downloaded.
- D-drive migration: the 5,027,783,488-byte model was copied to `D:\ForgeCore\Models`, and its SHA-256 matched `D98CDCBD03E17CE47681435B5150E34C1417F50B5C0019DD560E4882C5745785`. The Release desktop opened its D: database, loaded the model through LLamaSharp/Vulkan, and completed a real no-tool response. Workspace and SQLite paths now point to `D:\ForgeCore\Workspace` and `D:\ForgeCore\Data`; the verified obsolete C: runtime copies and empty workspace were removed afterward.
