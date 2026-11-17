# Connected-room return-fire diagnostics — 17 November 2026

Continues the 418-file `20261117-approved-preparation-v1` source checkpoint on an isolated restored source root. Prior v8, its real preparation proof, evidence and review decisions are preserved. This increment is reusable template/test infrastructure, not a new graphics, balance or original-freeze fix.

## Why and scope

The previous twelve actual v8 mission records had zero hostile shots and full player health. The accurate bot can kill guards before entering their 14-metre firing range; the older combat-lab return-fire test did not exercise the two new indoor areas. Neither observation establishes broken enemy AI or accepted combat balance.

Kit **0.16.108**, bridge unchanged **0.17.23**, adds:

- Nine connected-room cases: all three authored indoor guards at 30/60/120 Hz must use actual AI, evaluated shot markers, muzzle/cover rechecks and swept projectile arrival to damage a stationary player.
- Three pause/resume cases and three deliberate inserted-cover marker-recheck cases. The inserted cover is synthetic fixture geometry, not a claim about authored cover placement.
- An opt-in development-only Windows encounter fixture with controlled stage restoration and player repositioning six metres from each unchanged guard. Each case has a six-second bound, the owning script a 45-second process bound, fresh adjacent diagnostic saves and create-new receipts. Actual Update/animation/projectiles must produce damage after pause/resume. This is explicitly **not** an ordinary mission traversal, human combat-balance, art or frame-performance acceptance test.
- Separate fresh production-policy/executor generation/build paths and normal-player regression/timing candidate selection. Existing evidence is not replayed or overwritten. No source/target review or rights approval is granted.

No existing combat-range, damage, controls, movement, flash, audio, rig, clip, prop, collision, lighting or screen recipe is changed. Of 85 original installed source files, 83 remain byte-identical; the connected tests change and `PrototypeGame.cs` differs only in its generated kit-version identity constant. One new opt-in diagnostic file makes 86. Unknown kit modifications remain protected; the verified .107 predecessor was backed up before upgrade.

## Verification so far

- The parent source ZIP independently extracted, restored offline and built Release before new edits.
- All 85 installed .107 source hashes verified before installing; historical audit records **2,982** protected hashes, including v8 and the later one-shot preparation proof.
- Production scaffold safely upgraded .107 to .108 and Unity compilation passed.
- Fresh preparation through the real host reader/writer succeeded: scene `Assets/Scenes/Indie/Industrial-Connected-Encounters-v1.unity`, asset set `connected-encounters-v1`; evidence `b5f9c1c945fa4c9cb777ae4f96a632c4.json`.
- **144 graphics PlayMode checks passed**, zero failures/skips. XML SHA-256 `905a9b0c5e25adb080ae8f48ea4443325ebbe10b806098a873ec346d985f9f7f`.
- Four changed/new PowerShell verification scripts parse successfully.

Further completed verification:

- Windows development export succeeds; **19 Editor checks** pass with zero failures/skips.
- **1,058 host tests** pass (696 Core / 40 Agent / 322 Security), zero failures/skips, using disposable fixtures. The new host check verifies development-only/opt-in/bounded fixture scope and explicit non-acceptance declarations; it is not a replacement for live Unity results.
- The exported Direct3D12 room fixture passes for all three guards: each launches one real projectile/marker, reducing player health **100 → 93** after pause/resume in approximately **1.767 seconds**, with no guard displacement. Raw report SHA-256 `ab88f006bf4e6b3d1534b0109f3b0550de1bedc95c1be0bb361a40f8f9654400`. Controlled player repositioning is declared, not misreported as ordinary traversal.
- All four separate exported modes pass: courtyard render, power box, actual player/guard warm flash and independently measured corpse grounding. Sampled magenta pixels are zero. These are technical visual checks, not final art acceptance.
- All **306 fresh artifacts** verify by byte length and SHA-256; **43 inherited audio/clip/original-arm payloads** match v8. The post-test preservation audit verifies all **2,982 protected hashes** unchanged.
- Self-contained app dependency and six Release module checks plus three **synthetic-observation** planner routes pass. No normal app host, model/provider, live database or real Unity tools are opened by the package probes. An initial smoke-check assertion used the earlier delivery's configuration hash and refused before starting the app; it was corrected to the independently restored parent's exact sanitized example hash `f2bb167a526a47f4348d9a4d16bce9f9e6899bbb7f426ff8948af7eaab32bd54`. Configuration files were not rewritten to bypass the check.
- Separate sealed app delivery: **481 files / 267,500,404 bytes**, manifest SHA-256 `f432b9dd048abf8399f7ad3290fe189f860e2d009abf77dcd33c388fa155c651`; Windows game: **336 files / 241,033,589 bytes**, manifest SHA-256 `c5c9fb07ea4d66fa0ec6358f6196f161e26189e6be8136b71efe35995447fb05`. Every payload verifies. Player diagnostic folders are generated only after sealing; they are not part of the delivery. Existing v8 delivery is not replaced.

Nine actual **1920×1080** mission passes (first, repeat and independently hash-verified relocated Windows copy; three presets each) pass with all three zones, gates, objectives, far-room checkpoint restores, about **118 metres** of real controller traversal and the recorded footstep bank. No player teleport or direct damage is used by this separate mission driver. Every run remains at 21 player shots, zero hostile shots and health 100: the new stationary room fixture demonstrates return fire exists, **not** that the fast mission bot now tests human combat pressure.

| Ordered batch | Gameplay maximum | Warmup stopwatch maximum | Raw receipt SHA-256 |
| --- | ---: | ---: | --- |
| First | 28.1997 ms | 611.6652 ms | `06e11ab9047c1627a816bea575bb19f2d76fec1bcac1793b153c36c14224ff65` |
| Repeat | 17.1945 ms | 571.3224 ms | `1c2b60556786f18ccd5597211ec465b88ae5d4e928a9a5a40ee09e9c682375dc` |
| Relocated copy | 17.9652 ms | 571.4392 ms | `0fe14c5418d7fa8a6f1e7a6e459b3dd33757f552e40a06844aed9da153c6f504` |

The unchanged 100 ms measured-gameplay gate passes and original windows match; **whole-session smoothness and the original freeze are not accepted**. Supplementary Unity unscaled warmup maxima are approximately 2,790 / 2,804 / 2,794 ms and are retained separately from the primary stopwatch intervals, not silently averaged or attributed to a cause. OS/driver caches are uncontrolled. This is not a smooth-startup or hardware certification claim. The relocated executable receives no Unity project argument; external file reads are not traced, so it is not proof of total dependency isolation.

The final room-fixture repeat also passes with 100 → 93 health for each guard (raw report SHA-256 `1c34e0acddd70ab7a6eec1c6e67b2c56354ac6a4d07691ba851c58a435ff0db8`), and the final preservation audit again verifies **2,982** exact hashes. Both sealed delivery folders were copied to fresh primary-workspace output paths without replacing v8. Later sanitized source checkpoint/recovery/publication identities require their external create-new receipts. Do not mutate earlier sealed archives or historical receipts to add later proof.

## Remaining acceptance

The actual-mission bot remains separate and must still walk the three-zone route without player teleport/direct damage. Human return-fire pressure and cover usefulness need playtesting; a controlled stationary target fixture cannot approve balance. Recorded audio and warm flash must retain their standalone checks. Human Blender source/rights and target reviews remain required before approved-library placement; these tests do not authorize them. Current visuals are provisional, and startup/warmup stutters and the original freeze remain open.
