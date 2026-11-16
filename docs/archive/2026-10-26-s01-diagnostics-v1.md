# S01 checkpoint — 26 October 2026

Archived status: **In progress**, not complete. This captures the first S01 diagnostic milestone; retain the initial [outcomes-v1 plan](2026-10-26-outcomes-v1.md) and [baseline evidence](2026-10-26-baseline-evidence.md). Subsequent work updates the [active checklist](../../OUTCOMES.md) and adds later evidence rather than rewriting this status snapshot.

- Implemented: kit 0.16.34 startup/warmup/interactive phase reporting, independent monotonic frame intervals, bounded long-frame focus/GC context, profile schema 3, provisional 100 ms maximum interactive interval and 15 s engine-start-to-measurement gates. Bounded raw results remain local; p95 alone cannot pass a rare-freeze result.
- Verified: 264/264 Release host tests; real Unity compile; 18/18 EditMode, 8/8 PlayMode; fresh Windows diagnostic build using the unchanged barrel scene. Original scene, packages, earlier builds and delivery hashes are preserved.
- Three unchanged-build repeats recorded maxima 2.5769 / 2.6697 / 2.6711 ms. Three instrumented profiles (10/10/30 s) recorded worst Unity/Stopwatch interactive intervals 3.2122 / 3.0691 / 3.0992 ms and passed provisional gates. The loading-spanning initial interval remains separately visible at about 2.78 s; engine-to-measurement was about 3.88 s. End-to-end process-launch latency, cold caches and production load are not certified.
- **Unresolved:** the historical 1,727.84 ms event did not recur and has no established cause. Phase-accounting/acceptance improvements are not a proven hitch-removal fix. Next is capture/attribution of a real outlier and a targeted, reproducible before/after test.
- Full local identities, results, limitations and delivery manifests are in the [dated S01 work note](../outcomes/S01-2026-10-26.md).
- Recovery: sanitized local source checkpoint `backups/20261026-s01-diagnostics-v1`, independently of the unchanged previously accepted GitHub outcomes-v1 commit. It does not include external Unity project, binary assets, model weights, runtime data/logs or live configuration. New app/game folders use `20261026-S01DiagnosticsV1`; new app dependency probe and copied game startup check passed.

No later stage is completed by this milestone. No paid generation, package install, public S01 upload or unattended monitor is implied.
