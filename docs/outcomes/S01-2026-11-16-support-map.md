# S01 — support-map optimization and reset attribution, 16 November 2026

## Objective and scope

Continue current-content S01 investigation without another graphics/audio downgrade, warmup extension or discarded boundary frame. Work begins from the exact 338-file 0.16.93 source checkpoint (ZIP 8534d536559a869f3b7e178731e2f022ec88764b2e148885f77e7e3696c5c7dc) in a fresh isolated restored source. Primary-source edits are mirrored only after baseline hash checks; unrelated dirty changes and local main are not reset/staged wholesale.

The initial hypothesis was that repeated skeleton-property access in support-map construction explained the half-second first observation. Actual scoped measurements reject that as the main cause: this region accounts for about 12 ms. The optimization below is useful but deliberately not presented as the original freeze fix.

Kit 0.16.94 / bridge 0.17.22 uses the production exact-hash .93 upgrade and protected .93 backup .forgecore/unity/template-kit-backups/229afcce3bc24c7abf4c7b9df541ffe5. No scene, model, animation, waveform, shader or material was regenerated; no human review/rights decision or live database was changed.

## Reusable source changes

DemoSkinSupportMap is shared Core/Unity C#9 code. It classifies each bone name once per character and evaluates four influences with scalar operations. The original loop allocated two temporary arrays per vertex and fetched skin.bones for each influence; those repeated calls/arrays are removed from the default path. Per-character mesh/role/output arrays still exist: this is not allocation-free whole-character startup.

Strict torso > 0.5 and hand > 0.5 decisions, influence order and exact bone-name matching are preserved. Thirteen new host cases cover names, thresholds/order, 10,000 randomized reference comparisons, malformed indices and zero managed allocations in warmed scalar evaluation. A new actual Unity test compares the applied private torso/hand support masks to the original loop on every guard vertex. It is not a new death/animation artistic acceptance.

Diagnostic schema 2 adds a persistent bounded work journal and variant identity. The normal per-window work journal remains unchanged; the extra journal is timing-opt-in only and survives ResetMeasurement. This fixes the diagnostic blind spot where work immediately before a reset was discarded. Both journals retain at most 256 detailed entries each, with fixed work-kind aggregate slots; existing frame/event/window caps remain. Added scopes cover support initialization/mapping, actor reset, Animator rebind and new-game checkpoint wait. Background callbacks capture managed journals/clock/frame only, never Unity APIs on the worker.

The development-only -forgecore-footwork-legacy-probe switch selects the exact original support mapper only when timing is also enabled. It cannot enable that path in the Editor or a normal player launch. This permits same-executable control measurements, rather than comparing unrelated games. No old primitive fixture is launched. Neither snapshots nor file/JSON writes are added to normal frame collection.

Existing two-second warmup, boundary ownership, 100 ms provisional gameplay maximum and player-profile p95/GPU/memory policies remain unchanged. All 24 measured gameplay/idle window counts and maxima exactly match the existing sampler. Whole-session smoothness remains explicitly unaccepted.

## Same-build measurements

Unity 6000.5.5f1, Direct3D12, RTX 3070, Ryzen 7 5700X3D, actual 1920 x 1080. Ordered original/cached pairs use the same build/scene/quality presets; first pair has three 30-second idle soaks, repeat pair has matched three 8-second soaks. OS/driver caches and ordinary workstation activity are not controlled. These are not certified cold-cache or independent overhead benchmarks.

| Launch | Three support maps, total | Warmup max wall | Gameplay max | New-game checkpoint wait max | Synchronous settings flush max |
| --- | ---: | ---: | ---: | ---: | ---: |
| Original first | 12.251 ms | 583.850 ms | 83.599 ms | 140.597 ms | 159.000 ms |
| Cached first | 0.560 ms | 561.909 ms | 17.048 ms | 63.790 ms | 58.144 ms |
| Original repeat | 12.573 ms | 576.192 ms | 17.039 ms | 62.631 ms | 54.199 ms |
| Cached repeat | 0.481 ms | 560.671 ms | 17.173 ms | 67.750 ms | 53.281 ms |

The named mapping region improves by over 95% in both pairs. Do not project that percentage onto total startup/gameplay. It executes during setup, not the later combat hitch. The retained 83.599 ms original-first gameplay sample overlaps background I/O, which does not establish its cause or prove the cached mapper fixed it.

The new persistent scopes expose actual blocking code paths: settings persistence calls durable FileStream.Flush on the main thread; StartNewGame calls SaveCheckpoint, which waits for the background checkpoint queue. The first scoped flush is 159.000 ms and a checkpoint wait reaches 140.597 ms. These are bounded evidence for next work, not an already implemented asynchronous-save change. Manual save/load durability and current queue semantics are preserved in this increment.

The first original support scope starts around 524 ms after telemetry Awake, so the earlier initial interval remains unattributed. Unity Awake/Ready latencies are about 2.29–2.85 s, not invented native process frames. No native CPU stack/shader/GC cause is assigned. Nested/main/worker scopes overlap: do not sum their durations as exclusive CPU time.

All twelve preset missions complete through actual controller/projectile code, three kills and fourteen player shots each, checkpoint roundtrip and grounded recorded footsteps; no teleport or direct-damage helper. All four runs pass the provisional measured gameplay maximum separately from mission completion. The full raw/comparison receipts remain local in the isolated source work/s08-s10-verification and work/support-map-s01.

## Identities and validation

Existing scene Assets/Scenes/Retro/Industrial-Retro64-v2.unity remains SHA-256 1c5d16f360cd0e0f5979cf20ef1817a702cf389dd76ac15b9522eac11512cb9a. Fresh build Builds/S01-SupportMap-20261116/ForgeCore-S01SupportMap.exe uses unchanged level0 79e22c76c826b68d3aa34b8745a28e03a02751750a81c7cd37791c200ede6a5a and new runtime DLL 47c23c71cfb9ae3c36f9f745d4206c35457a18832b0daff7b0351158bf646f41. Kit manifest 62177d618550af37705140871bf93e5d81141ede3ee9fc8a04ebeceaf1a90b32. The clean game delivery manifest is 8c6421345ed626878bfc66b20fcfe76c1e0845ecd36b4aaeef4b7b37e38e12a1. Native launcher hash alone is not build identity; comparison binds provenance, level0 and the actual generated runtime DLL to the sealed pre-probe export.

- Release host suite: 919 passed (565 Core, 40 Agent, 314 Security), zero failures/skips.
- Generated Runtime/Editor C#9 semantic compilation, actual Unity compilation and Windows build pass.
- Four rendered modes pass: courtyard, physical power-box state/reset, actual player/guard shots with zero magenta and independently grounded corpses.
- Actual Unity suite: 19 EditMode and 107 graphics-enabled PlayMode checks passed, zero failures/skips, including every-guard-vertex support-mask equivalence. PlayMode XML SHA-256 3ea239bbd7f9c843baf1f0c90bb15863ca23a8f183c41d85857a302e5afc04a3.
- Final preservation audit: the same 594 exact before/after artifact and review-record hashes match. No acceptance gate or artifact was changed to obtain that result.
- The accepted 34-clip audio recipe/recorded bank, muzzle shader, actual meshes/clips, controls and unchanged scene are preserved. Normal app/model/provider/SQLite sessions were not started.
- New self-contained app delivery: outputs/ForgeCore-Windows-x64-20261116-SupportMapV1, 481 payload files, manifest SHA-256 6fb716078da35e60a4528061fa82c9358795b5713b29b2cc7a9af8fd2cb301b9. Clean game: outputs/ForgeCore-TestGame-20261116-SupportMapV1, 336 payload files, manifest SHA-256 8c6421345ed626878bfc66b20fcfe76c1e0845ecd36b4aaeef4b7b37e38e12a1. Exports precede probe-generated directories, use safe offline configuration and are copied separately without replacing old deliveries. Every-file hash verification is separate from portable-location runtime acceptance; full measurements above use the matching project build.
- Sanitized source checkpoint label 20261116-s01-support-map-v1 preserves exact text bytes/BOM/line endings, substitutes safe example configuration and excludes its manifest from its payload. Old source/game/evidence bundles are retained.

## Next safe work / limits

S01 remains open. First remove the automatic new-game checkpoint's main-thread wait with explicit pending/error/exit/recovery semantics and regression coverage, preserving ordered atomic saves and explicit load/durability boundaries. Settings commits need an equally explicit persistence/UI policy; do not silently weaken durable writes to improve a benchmark. Native/script startup profiling is still needed for the initial half-second gap.

This is a tested reusable small setup optimization plus better attribution, not a full freeze fix, accepted frame pacing, new graphics/assets, bigger map or all-genre autonomous-game milestone. Use the new candidate/app folder when testing; old deliveries remain immutable. Back up sanitized progress on codex/s01-current-game-timing-20261116 without modifying draft PR #3 or main.
