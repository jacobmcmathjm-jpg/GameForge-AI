# Indie rifle — quieter-workstation validation

16 November 2026. Follow-up to [the retained candidate and failed attempts](2026-11-16-indie-rifle.md), after the user said the other game/browser tabs were closed and testing could resume. **Technical validation now passes for the unchanged .97 candidate. Human art acceptance and the original intermittent freeze remain open.** This is validation, not a new graphics implementation or a new source of detailed hands/guards.

## Actual Windows/Unity results

| Check | Fresh attempt | Result |
| --- | --- | --- |
| Four exported-player modes | `quiet-v1` | Courtyard, power-box, firing/render and corpse checks pass, exit 0; zero sampled magenta |
| Full rendered mission, three presets | `quiet-first` | Three full 1080p missions and three 30-second idle soaks pass |
| Repeat, three presets | `quiet-repeat` | Three full 1080p missions and three 8-second idle soaks pass |
| Complete graphics-enabled PlayMode suite | `indie-hero-quiet-v1`, scope All | **116/116**, zero failures/skips, exit 0 |
| Complete EditMode suite | `edit-tests-v97-quiet-v1` | **19/19**, zero failures/skips |
| Production runtime-camera capture | `capture-hero-quiet-v1` | Successful bridge operation, actual 1280x720 PNG and successful provenance; zero sampled magenta |
| Older protected files | `preservation-quiet-v1` | **877/877 exact hashes** |

The four targeted rifle tests are included in the full 116, not four additional unique tests. The earlier 962 passing host tests remain applicable: only verification harness/export/docs change in this follow-up; runtime, kit, generated scene and binaries do not. Do not describe this as another new run of those 962 host cases.

Full missions exercise actual controller movement, projectile kills, reloads, objectives, recorded footsteps and checkpoint roundtrip. No direct damage or player teleport substitutes for mission completion. The independent physical power-box and corpse-floor thresholds are unchanged. The camera is a real runtime capture, not an image-generation concept.

The PlayMode process uses the same 600-second allowance as the second retained failed attempt; default remains 240. Editor per-test 180 seconds, production preview 122 seconds, player-mode 45 seconds, two-second gameplay warmup and 100 ms measured gameplay maximum gate are unchanged. No assertion, startup allowance inside the game or measurement window is loosened.

## Timing is still qualified

| Attempt | Maximum measured gameplay interval | Maximum warmup interval |
| --- | ---: | ---: |
| quiet-first | **68.1289 ms** | **772.1541 ms** |
| quiet-repeat | **17.1160 ms** | **569.2975 ms** |

Both pass the existing provisional measured-gameplay maximum gate; their six gameplay/idle windows exactly match the original sampler's windows. The first-run 68 ms spike and both long warmup intervals remain evidence. Startup has no Update samples in these receipts, not proof of zero startup cost. Unity supplementary deltas are not substituted for the primary wall-clock intervals.

Neither receipt accepts whole-session smoothness or marks the freeze fixed. Quieter process state correlates with successful retries but **does not prove memory contention caused the previous failures or that runtime performance improved**. No unrelated user process was closed or killed by this work.

## Preservation and handoff

Kit remains **0.16.97**, bridge **0.17.22**. Same 286 generated artifacts, same 43 exact inherited audio/motion/hand files and same scene/runtime identities as the original candidate. No existing human approval, protected backup, old scene, failed attempt, stable export or legacy review is replaced. No package install, cloud generation, SQLite access or normal app/store/model startup occurs in this validation.

The successful runtime image is `Previews/Indie/Runtime-Hero-Quiet-v1.png`, SHA-256 **fc7b67651b6f571984159eb4088ec34e7de5a1296e9b8c9b9d8c822868fec44b** (726,760 bytes). It is byte-identical to the image written by the earlier timed-out camera attempt, but **only the fresh successful operation has successful provenance**. The earlier diagnostic remains accurately labelled failed.

Separate review folder: `outputs/ForgeCore-IndieHeroReview-20261116-V1`; four payloads / 728,476 bytes; manifest **79a4f06113c41f9a4ea3c81a427364df78908b8db2044b8a3a5a7840f9cc5db4**. The exporter requires full quiet evidence and labels human acceptance false. Existing app/game folders `20261116-IndieHeroV1` remain byte-identical; no .98 bump or duplicate binary release is needed.

New source-only checkpoint: **20261116-indie-rifle-quiet-v1**. Publish follow-through only on **codex/indie-rifle-candidate-20261116**, preserving the tested environment branch, S01 branches, draft PR #3 and main. Raw local diagnostics, Unity project and game binaries are excluded from public source backups. Independent offline exact-ZIP restore/build is a separate recovery check, not another Unity run.

A nullable-string compile error in the verification helper stopped before the Editor operation launched; it was corrected to `string?`. A later approval-service usage-limit rejection did not execute the requested command. After explicit user continuation, the same approval route accepted the remaining bounded checks. Neither incident is misreported as a Unity test failure or bypassed.

## Evidence identities

Local receipts retained under the verification workspace, not uploaded as raw logs:

- Full PlayMode XML SHA-256: **e0e34fd970e8f467d22f4e18358f28fba03e0bacc9103fe4d392dac2e343f164**.
- `work/indie-hero/edit-tests-v97-quiet-v1.json`: **16120a5a73a941004448bc1aae7e184ab77ec584729513b37257d12e1929f47e**.
- `work/indie-hero/capture-hero-quiet-v1.json`: **2f67d928bcd5c63c61014d99a76900365ab0aa343ec98cb484ede03cb2bcd44e**.
- `work/indie-hero/preservation-quiet-v1.json`: **3441305d336025024b7a598f1dd12218e98354c0772e0573e8444f3575aac4fa**.
- Four-mode receipt: **faf271ae6d6dc0a9fab177f627315a53fa84dbdbf954853d0bc87c332c053d5d**.
- First/repeat raw mission receipts: **a8ec499ebb4d8c00bb43c94e172b8d27b8cad65e5cfdcaadaf18c7a75e31cb52** / **4b935db2c11e31d75cea870753880d75bfdc427e7326ccbe52a3b85aafbfd77f**.
- First/repeat validated timing: **add191e5170ace9f9025ed4db9423e21d2edeea6b0b6c0f6025e664675ab4862** / **498b44c7d93614bfa67aa30234ab23f22cbd74bf8440a809c9c571fadc80926f**.

Next bounded visual task, after reviewing this candidate: original rig-compatible hands/guard anatomy, with grip/contact, ADS opening, reload/deformation and motion-preservation tests. This does not certify all genres, realistic/AA graphics, a larger level or unattended full-game creation.

