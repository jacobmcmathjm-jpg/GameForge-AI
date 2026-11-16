# S01 — current-game diagnostic integration, 16 November 2026

## Scope and preserved baseline

The user authorized diagnostic integration into the newer working game and a separate public current-source backup branch, leaving draft PR #3 and main untouched. This is not a continuation of the obsolete primitive timing window as a delivered game. The historical PR outcome remains in S01-2026-11-16.md, including its failing raw attempts and explicit legacy-launch guard.

The immutable November 5 source checkpoint (329 source files, ZIP SHA-256 f98c7fb78b9e1b1936bae5f69865c1edaa9b48c661b57afd9f20754e44c972bb) was restored into work/restored-checkpoints/20261105-retro64-v2-timing-20261116. Its source matched the local current source before work (excluding live configuration). The primary checkout's unrelated changes and legacy main branch were not reset, merged or staged en masse. Selected integration files were mirrored back with apply_patch only after baseline-hash checks and then matched exactly to the tested isolated source.

Kit 0.16.93 / unchanged bridge 0.17.22 recognizes the exact .92 source map and uses the production protected upgrade. Original .92 backup: .forgecore/unity/template-kit-backups/cefbc74105f147ae996744b6903a4f28. No scene, character, animation, sound bank or model was regenerated. No human approval/review decision, library promotion, model service, external generation API or live SQLite session was used.

## Reusable diagnostic change

DemoTimingTrace is shared Core/Unity-compatible C#9 source, embedded in the generated kit. It is enabled in players only by -forgecore-timing-probe. Without that flag the new collector/context snapshots are not created; controls, gameplay and existing telemetry continue unchanged. There is no normal-frame JSON/file work.

The primary clock is consecutive telemetry Update Stopwatch observations. Unity unscaled delta is explicitly supplementary and can describe a different interval. Phase maxima/counts and GC/memory/focus/work context distinguish warmup, gameplay, idle and paused observations. Awake/Ready values are Unity initialization latencies; native process startup before Awake is unobserved, and startup has zero invented rendered frames. The first warmup observation can span initialization after Awake. GC/work overlap is correlation, not causal attribution.

The existing two-second warmup is unchanged, including the interval crossing its boundary in the measured gameplay/idle window. All twelve real windows across both launches have exact frame-count/maximum equality to the old sampler. No hitch was removed by extending warmup. Existing player-profile p95/GPU/memory/maximum policies are unchanged.

Caps are 64 first long-event details, a separate worst wall-clock event per phase, 16 windows, 1,000,000 observations and 8 overlapping-work entries per retained event. Lost detail is counted; observation/window overflow prevents provisional acceptance. A large supplementary delta cannot hide a later worse wall-clock interval. The provisional gameplay maximum is 100 ms; wholeSessionSmoothnessAccepted remains false even when measured gameplay passes. Instrumentation overhead is not independently benchmarked.

## Actual current-game evidence

Scene: Assets/Scenes/Retro/Industrial-Retro64-v2.unity, unchanged SHA-256 1c5d16f360cd0e0f5979cf20ef1817a702cf389dd76ac15b9522eac11512cb9a.

Fresh build: Builds/S01-CurrentGame-20261116/ForgeCore-S01Current.exe. Level0 SHA-256 79e22c76c826b68d3aa34b8745a28e03a02751750a81c7cd37791c200ede6a5a. Kit manifest SHA-256 a6179b5c8b174199cb9f4f2a391a177f92323c912e516c652379785eadad4778. The generic native launcher hash alone is not a game identity; the complete clean delivery manifest binds the actual payload.

Unity 6000.5.5f1, Direct3D12, RTX 3070, Ryzen 7 5700X3D, actual 1920 x 1080. Each launch completed three preset missions through controller/projectile code, checkpoint roundtrip and three thirty-second idle soaks. Each mission killed three guards, fired fourteen player shots and travelled approximately 54.8 m; no direct damage or teleport helper was used. Audio remains forgecore_layered_surface_audio_v2, 34 clips and gbox_subway_hardfloor_v1 recorded footsteps. Probes mute audio and use isolated diagnostic storage; the previous listening approval is preserved, not newly fabricated.

| Ordered launch | Unity Awake / Ready latency | Warmup max wall / supplementary Unity | Gameplay p95 range | Gameplay maximum range | Idle maximum |
| --- | --- | --- | --- | --- | --- |
| First | 2,298.10 / 2,820.50 ms | 571.55 / 2,806.14 ms | 16.743–16.747 ms | 17.056–17.183 ms | 17.296 ms |
| Repeat | 2,298.10 / 2,819.65 ms | 568.81 / 2,806.03 ms | 16.740–16.777 ms | 17.070–17.624 ms | 17.082 ms |

Both launches retain eight detailed warmup events, six warmup wall intervals over 100 ms, no gameplay/idle intervals over 100 ms, no truncated frames/windows and all six expected windows. The first retained warmup interval has GC activity, which does not prove GC caused the interval. These are ordered launches after graphics checks, not controlled cold-cache runs. OS/driver caches and normal workstation activity were not isolated. Each mission-launch script required the Unity Editor to be idle.

Local raw first/repeat receipt SHA-256:
- 318b58a8220e97933c03824c52c759a7c1c13ca670ae2cb62d9ce1f07d0b745d
- da3ae9a4e57bd5ead42543ef527cae9b8bc7707965eb4fc6888d6596abd6c5ce

Raw diagnostic directories/logs/results stay local under the fresh build and isolated source work/s08-s10-verification. They are not uploaded as public source or confused with the historical PR fixture's failed performance attempt.

## Validation and preservation

- Release host suite: 906 passed (552 Core, including 17 new diagnostic cases; 40 Agent; 314 Security), zero failures/skips. A rerun used distinct TRX names after an initial shared filename warning; distinct final receipts are retained.
- Generated Runtime/Editor C#9 semantic compilation against installed Unity references passed. Generated tests are parsed here, not executed by this static check.
- Production protected .92 upgrade, actual Unity compilation and Windows build passed.
- All four actual standalone modes (courtyard, powerbox, render, corpse) passed. Every captured sample had zero magenta pixels; actual player and guard shots were observed. Hip warm-pixel delta +833, aimed +11 and guard +4,328; the actual hip-shot PNG was visually inspected and shows a warm white/orange flash. Corpse floor-contact limits and physical power-box persistence/reset checks remain unchanged.
- Both full three-preset mission/idle runs and exact old/new sampler comparisons passed; measured gameplay maximum passed separately from mission completion.
- Actual Unity regression suite: 19 EditMode and 106 graphics-enabled PlayMode checks passed, zero failures/skips. PlayMode XML SHA-256 427a0fb4a528bb1ff774758c3f6174e952409402729f7e5e656a7d9cf0f375ab.
- Preservation audit: BEFORE and AFTER the same 594 exact hashes matched original modern/retro evidence, warm shader and review/approval records. The first after-check falsely rejected culture-dependent dictionary ordering after JSON deserialization. An independent PowerShell hash/set check found no changed/new review files; the harness was corrected to compare exact key/value sets and count rather than enumeration order, and the after-check passed. No project content was changed to satisfy it.
- Original delivered November 4 and 5 game folders were independently verified every-file unchanged: manifest SHA-256 58529a9d3b8a530e2c3f13fbc788c52801aa2ef2a0943146228f13979c74ca14 and 9e513b233015bcb2324c83b684152d1b254a97cc9623a0ebdb739058bf3eb8c9.
- A fresh self-contained Windows app and separate clean game were exported before probes wrote diagnostic subdirectories. Safe offline app configuration SHA-256 b84302a01a8e7bd6e808747ef549d5d1c9b52c0fe033fda32cb5f6950641266d; no live configuration was published. Artifact verification and source checkpoint identities are retained in local manifests/receipts.
- Root delivery folders are outputs/ForgeCore-Windows-x64-20261116-CurrentTimingV1 (481 files; manifest 02940308349564e58890666f8ff97f5608b625ff06ea126c0c6603267c86dbea) and outputs/ForgeCore-TestGame-20261116-CurrentTimingV1 (336 files; manifest fb1de1d3942fdad26ef02eaafbf4538a30247e4736d537a6633c13a8b4362dfc). Every copied file/cardinality/hash verified. The clean copied folder was hash-verified; full mission launches above used the matching project build, not a separate portable-location run.
- Sanitized source checkpoint label: 20261116-s01-current-game-timing. Exact UTF-8/BOM/line endings are preserved; live configuration is substituted with the safe example, and the manifest excludes itself. Prior artifacts/raw attempts remain local and are never overwritten.

## Remaining work / recovery

S01 remains open. This prevents another graphics/audio downgrade and supplies bounded current-content evidence; it does not fix or attribute the original intermittent freeze. Native startup, controlled cold/warm conditions, startup/warmup budgets and representative causal profiling remain required. Do not label warmup stalls a gameplay fix, infer GPU/shader/GC causation from these counters, or call this AA graphics or autonomous all-genre production.

Keep PR #3 draft at 021cbe47a23639a93c9db0f0bdf755c2184fdc37 and main unchanged. Newer source is backed up on codex/s01-current-game-timing-20261116. Do not blindly install the old .35 fixture over the current kit. Recovery retains the original .92 protected source backup, immutable November source/game bundles and the pre-integration outcomes archive. Review records and pending art gates remain distinct from technical test success.
