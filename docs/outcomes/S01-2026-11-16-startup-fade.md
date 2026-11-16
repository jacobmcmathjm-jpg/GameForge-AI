# S01 — startup CPU trace and splash-only attribution

16 November 2026. Continues the exact .95 checkpoint after queued new-game saves. **S01 stays open. The intermittent gameplay freeze is not fixed.** This is a developer-diagnostic increment, not a new production game, graphics upgrade, new stage acceptance or autonomous genre capability.

## Finding

The previously unexplained ~560 ms Awake-to-first-Update interval contains approximately **500 ms of Unity splash-screen fade**, not 500 ms inside the game's setup methods. This is supported by a native startup CPU trace and a separate splash-only on/off/on export comparison. The splash fade is documented in [Unity's 6000.5 splash settings](https://docs.unity.com/en-us/engine/6000.5/manual/unity-editor/editor-settings-reference/comp-manager-group/class-player-settings-splash-screen). Its deliberate startup presentation must not be conflated with an intermittent in-game freeze.

In the original .95 profiled capture, the final startup tail repeats `SplashScreenRender`, `UpdatePreloading` and background integration. There are 745,007 splash calls across approximately 500 ms. Every individual call is below 1 ms; the initial long-sample-only export missed them. The reader now retains bounded aggregated short-sample information as well. Nested totals overlap and are not exclusive CPU time. With splash off, no splash marker is retained and the lengthy repeated loop disappears.

## Actual comparison

Same .95 game runtime, scene, Development build options, Unity 6000.5.5f1, Direct3D12, RTX 3070, Ryzen 7 5700X3D, 1920 x 1080. Serial ordinary-player tests; three actual controller/projectile missions per run, with isolated saves and three eight-second idle soaks. Caches/workstation activity are uncontrolled. No direct damage or teleport shortcut.

| Unprofiled run | Awake-to-ready Unity clock | First wall interval | Maximum measured gameplay interval |
| --- | ---: | ---: | ---: |
| Splash on | 520.414 ms | 568.587 ms | 83.966 ms |
| Splash off | 19.776 ms | 66.848 ms | 85.982 ms |
| Splash on repeat | 519.074 ms | 566.582 ms | 24.923 ms |

The minimum first-interval reduction against both controls is **499.734 ms**. The bounded comparison passes its declared >=400 ms reduction / <200 ms off-interval criteria. All three measured gameplay maxima remain below the unchanged provisional 100 ms gate, but the **84–86 ms gameplay events still need work**. Removing startup presentation does not improve or explain those events. Do not infer whole-session acceptance from these samples or their p95.

Profiled attribution runs are separate: original .95 first wall interval 606.268 ms, splash-off 67.265 ms. Each raw trace contains frames 0 through 240 (241 records), including native engine initialization. Profiling changes overhead; raw first-frame native durations are not comparable to game timing or performance acceptance. The on trace is 1,369,512,433 bytes; off is 17,319,859 bytes. Raw reports/logs/captures/player copies remain local under the isolated source's ignored `work/startup-cpu`, not GitHub.

## Preservation and checks

- Generated runtime DLL remains **e3a56176ab7625ed409ef142c62c6229f369c091a0bab22efc96231916d890e9** in original/on/off players; no runtime/template source changed. Kit **0.16.95**, bridge **0.17.22**, diagnostic schema 3, original two-second warmup and provisional 100 ms gameplay gate remain.
- Level0 remains **3c2aa7d31f436e920e60ef3063f9393285f72d0b8887c9eabc9ab78c33bb5a0e**. Scene remains **1c5d16f360cd0e0f5979cf20ef1817a702cf389dd76ac15b9522eac11512cb9a**.
- Original ProjectSettings.asset restored and independently read back as **3b45899f92669925ca58f854db6f5aae4a8fb1125818d11a601adda32ee043dd** after Editor exit. Normal splash settings and delivered games are unchanged.
- All **594** protected scene/asset/waveform/shader/review hashes match before/after. The temporary Editor-only helper and generated metadata were moved out into a local recovery folder; no project source or review was deleted or replaced.
- **15 actual missions** complete across the initial profiled .95 run, three unprofiled comparison runs and off-profile run. All preserve the 34-clip bank, recorded footstep identity, actual shots/kills and checkpoint roundtrip. These are technical probes, not new human listening/art approval.
- The splash-off export separately passes the four existing courtyard/power-box/render/corpse modes: zero magenta in sampled captures, actual player/enemy shots, persistent/resettable physical power box and original floor-contact thresholds. No flash/audio/animation recipe is replaced.
- Ten synthetic comparator cases pass: valid, slow off, insufficient reduction, changed report hash, profiled input, legacy schema, escaped report path, different runtime, incomplete mission and altered warmup. These fixtures are not real-game evidence.
- Host regression: **929 passed** (575 Core, 40 Agent, 314 Security), zero failures/skips. No new Unity test-runner suite is claimed: runtime source did not change; actual compilation/export/standalone checks above are the live Unity verification for this increment.

## Local evidence identities

Comparison JSON SHA-256: **c0c8fce0f988a493eadd51c01a22f6c931598dcce11b84b5611e3dc1c94f50e6**. Original detailed CPU export: **257185e61c4073b83e61e11adca3d379c2f02109d03a7868a0eb20b09319a88d**. Off CPU export: **488c00b15492767a319b3411df11076bf6fc859cbd2f3f51c578389655044441**.

Raw CPU traces: on **4a47973a2b36803418e5c9ffb566e4bd0f7a2a9529f111b01a8ca21ecf153803**, off **15692bb38341038f93ee834cbcafffccf3f27cf51d461bb8c01fb88565d6c4e2**. Unprofiled raw mission-report hashes in order: **ab4b900761c1143248fa41ceab165e35bd3a5fff7fcba85be9e61ce785f9fd9c**, **7d8b7f7024e48cb44e3107659fdef34425d88db5f1c435a9450a6306e14d834e**, **4d0144a40e74166f38e9f45495a6acdc334e3ee270badaf493b81de2db3c7dd9**.

## Next

Keep the ordinary game and its accepted audio/flash assets intact. Use a bounded gameplay-targeted CPU capture to correlate the remaining 84–86 ms events with first-use firing, corpse/GC, settings or other actual samples; do not assign a cause without evidence. Decide separately whether the user-facing export should retain Unity's startup presentation. The no-splash folders are diagnostic exports, not automatically selected production replacements.

Continue source backup on **codex/s01-current-game-timing-20261116**. PR #3 stays draft/unchanged; main stays unchanged. Preserve prior evidence and approvals.

Source checkpoint label: **20261116-s01-startup-fade-v1**. The checkpoint factory preserves exact source bytes, substitutes the safe offline example configuration and excludes runtime data, raw traces and its own manifest. Independent restore/build receipts are local and separate from source. No new app/game release is shipped in this increment.
