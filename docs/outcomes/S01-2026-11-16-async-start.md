# S01 — queued new-game checkpoint and protected quit, 16 November 2026

## Scope and implementation

Continue from the exact 343-file .94 source checkpoint (ZIP b53ef22f71e93090d925dfb8165df0e5f489fd8c291356d460bafa358b1c0514) in work/restored-checkpoints/20261116-s01-support-map-v1-async-start-v1. The user requested nonblocking initial checkpoint creation with ordered saves, visible state, recovery and safe shutdown. No model/API/provider/normal desktop-store session or human approval is used.

Kit 0.16.95 / unchanged bridge 0.17.22 upgrades only the exact .94 hash set, with protected backup .forgecore/unity/template-kit-backups/7f72ba10e9f94fe6907dd2a1cc2776b2. Unknown/modified sources are not overwritten. Source edits are mirrored into the primary checkout only after baseline checks; unrelated changes/local main are not reset or staged wholesale.

StartNewGame now captures the same immutable validated snapshot and submits it to the existing bounded serialized worker without waiting. Durable Flush(true), atomic replace, retained previous state, occupied .new protection, the 16-request cap and sticky failure/no automatic replay remain. This moves a caller wait, not physical storage latency or snapshot capture, off the gameplay thread. Saved checkpoint/settings formats and profile remain unchanged.

A saving/completion/failure notice is visible during gameplay. Pending state is not labelled durable completion. Explicit manual save/load and settings persistence keep their original synchronous boundaries; this increment does not claim to remove all disk waits.

The shared C#9 DemoCheckpointExit state machine polls rather than blocking rendering on normal quit. The game pauses while a pending write drains; after two seconds or failure, the UI offers continued waiting/cancellation or an explicit choice to exit without the latest checkpoint. Escape/restart input cannot resume gameplay behind this prompt. The previous bounded two-second OnApplicationQuit wait remains an OS/forced-event fallback. This is not power-loss or arbitrary third-party quit-handler certification.

Diagnostic schema 3 identifies queued versus blocking-control dispatch. The latter exists only in a development player with the timing flag; normal launches always use the queued path. The same executable is used for comparison. The two-second warmup, boundary interval ownership and provisional 100 ms gameplay maximum are unchanged. All eighteen measured windows exactly match original sampler counts/maxima.

## Tests and measured result

- Final host suite: 929 passed (575 Core, 40 Agent, 314 Security), zero failures/skips. Ten new exit-state cases cover completion, pending/timeout/failure, cancellation/continued waiting, explicit force and invalid clocks. Existing queue ordering, immutable captures, backlog limits, sticky failure and atomic-state tests remain.
- Generated Runtime/Editor C#9 semantic compilation, actual Unity compilation and Windows build pass.
- Actual Unity suite: 19 EditMode and 109 graphics-enabled PlayMode checks pass, zero failures/skips. New integration cases hold a writer while frames advance, verify new-game return/snapshot contents, cancel a pending quit, and preserve the prior file plus occupied .new on failure. Editor-only writer injection is absent from delivered players. Existing tests which deliberately read a committed snapshot now explicitly wait for that durability boundary.
- PlayMode XML SHA-256: 954e4c310ef88075c00911c56ea261f179bc5568c69834b524ed8695adbd61c2.
- Verify-CheckpointInterruption kills ONLY its own synthetic helper after temporary-file durable flush and before replacement. Old durable state and the interrupted candidate remain; an attempted replay is rejected. Local receipt: work/checkpoint-interruption-66127ed6816641c99d8f6cf9d5283ec4/verification.json. It does not kill Unity, touch normal saves, or simulate an OS/power failure.
- Four actual standalone rendering/state modes pass: zero magenta, actual player/guard shots, physical power-box reset/persistence and independent corpse contact limits.
- Final audit: the same 594 scene/asset/waveform/shader/review-record hashes remain exact. No rights/art/listening acceptance is fabricated.

Unity 6000.5.5f1, Direct3D12, RTX 3070, Ryzen 7 5700X3D, actual 1920 x 1080. Control/first queued run each contain three 30-second idle soaks; the queued repeat has three 8-second idle soaks. Caches/workstation activity are not controlled. All nine controller/projectile missions complete with three kills/fourteen shots each, checkpoint roundtrip and recorded footsteps, without direct-damage/teleport helpers. Each run explicitly checks all queued writes are durable by the final soak boundary.

| Run | New-game checkpoint max | Total over six calls | Worker durable flush max | Settings flush max | Warmup max wall | Gameplay max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Blocking control | 81.381 ms | 347.081 ms | 101.507 ms | 84.155 ms | 565.480 ms | 81.854 ms |
| Queued first | 5.099 ms | 5.458 ms | 99.637 ms | 61.576 ms | 562.299 ms | 17.099 ms |
| Queued repeat | 5.199 ms | 5.709 ms | 304.627 ms | 71.991 ms | 559.771 ms | 17.038 ms |

Both queued runs meet the predeclared caller criterion (at most 10 ms and at most 25% of the control maximum). Do not attribute later gameplay differences to a setup-only change. The control's 81.854 ms sample is retained, not hidden by p95 or called final smoothness acceptance. Worker/native/managed scopes may overlap; totals are not exclusive CPU time. The early ~560 ms interval and original intermittent freeze remain unattributed/unfixed.

Raw receipt hashes in order: f6d955d9d31ccc7d3abd18d51a3dbccff00a25f499b9988d3f5dbda70be4554c, 68140d27385496034d9c7ab64dc23bbdcc41cd6cbf031d92224dc3292bb075fb, 459ee66896db2faf634ed8b3200876488bd9c440a689288da27cc4ae131df347. Raw reports/logs/storage and comparison remain local below the isolated source's work/s08-s10-verification and work/async-start-s01, not public source.

## Identity, delivery and recovery

Scene Assets/Scenes/Retro/Industrial-Retro64-v2.unity is unchanged (1c5d16f360cd0e0f5979cf20ef1817a702cf389dd76ac15b9522eac11512cb9a). Fresh build: Builds/S01-AsyncStart-20261116/ForgeCore-S01Async.exe. Build level0: 3c2aa7d31f436e920e60ef3063f9393285f72d0b8887c9eabc9ab78c33bb5a0e; actual generated runtime DLL: e3a56176ab7625ed409ef142c62c6229f369c091a0bab22efc96231916d890e9; kit manifest: 363786d128fde36ee800c519ac3b38fdac70ec57214282f5e276178b18398dad. Comparison binds the sealed pre-probe export's provenance/level0/runtime DLL; generic native EXE hash alone is insufficient.

New app folder: outputs/ForgeCore-Windows-x64-20261116-AsyncStartV1 (481 files, manifest e4810a48921aa9539f6f0e95fbef510bb09184e2a65208ce074023df96a0762f). New game folder: outputs/ForgeCore-TestGame-20261116-AsyncStartV1 (336 files, manifest 934fdfa95239bce7786831451c5b8c3ecd1842199f48b9f88b1437902c4e12b4). Clean exports precede diagnostic directories and use safe offline configuration. Every copied payload is hash-verified; full runs above use the matching project build, not an independently launched portable-location copy.

Source checkpoint label 20261116-s01-async-start-v1 substitutes the safe example configuration and excludes its own manifest from the payload. Prior source/game bundles, .94 protected backup and human reviews remain. Publish sanitized source only on codex/s01-current-game-timing-20261116; keep PR #3 draft and main untouched.

S01 stays open. Next: characterize the initial startup gap with current-content/native/script profiling and separately define any settings-persistence UI/durability change. Do not silently weaken writes, extend warmup, replay ambiguous saves or call this a full stutter/graphics/genre milestone.
