# Current-game checkpoint and timing verification

Kit .95 makes the automatic new-game checkpoint nonblocking and adds a rendered quit-wait/error/cancel state. It retains .94 support-map optimization and existing scene, sounds, muzzle shader, clips and saved-data schemas. Historical .93/.94 outcomes remain unchanged.

Use this repository root. Production receipts are immutable below work/async-start-s01; the fixed new build destination is Builds/S01-AsyncStart-20261116/ForgeCore-S01Async.exe. Never replay a failed or ambiguous mutating attempt; inspect its receipt and choose a deliberately fresh attempt. Real Unity needs the normal licensed Windows context, named-project write authority and no other Editor.

1. Restore/build/test host source from the exclusive local package cache.
2. Verify-CurrentGameTiming: audit-before, install, compile, edit-tests.
3. Invoke-VisualPassTests: fresh attempt, ExpectedPassed 109.
4. Verify-CurrentGameTiming: build. Export the clean app/game before probes create diagnostic directories.
5. Invoke-DemoAcceptance: Candidate current-timing-v3, Viewport 1080p, Attempt blocking-control, LongSoak and BlockingCheckpointControl. Validate with Test-CurrentGameTiming using the same candidate/attempt/control flag.
6. Repeat as async-first with LongSoak and no control flag; repeat as async-repeat with the matched default eight-second idle. Validate each exact report.
7. Compare-CheckpointDispatch.ps1 binds modes, windows, provenance and actual runtime DLL to the sealed clean export. It saves evidence before rejecting a failed enqueue-time criterion.
8. Invoke-CourtyardPlayerChecks: Candidate CurrentTiming-v3. Verify-CurrentGameTiming: audit-after.
9. Independently verify delivery/source manifests and restore-build-test the exact new source ZIP before publishing sanitized source.

The blocking-checkpoint flag is development-player/timing-only; normal launches always enqueue automatic new-game saves. The old support-map control remains scoped to the older v2 diagnostics. Do not use the obsolete PR #3 primitive fixture to assess current graphics/audio.

Snapshot capture and JSON serialization remain on the Unity thread. The existing bounded queue writes immutable strings serially on the pool, with durable flush, atomic replace, retained previous state and sticky failure/no ambiguous replay. The new-game caller no longer waits for it. Explicit manual save/load and settings persistence retain their previous synchronous durability boundaries.

A visible saving/error notice does not assert completion while pending. Normal quit waits by polling while rendering continues; after two seconds or failure it requires waiting/cancellation or an explicit choice to exit without the latest checkpoint. The old bounded two-second OnApplicationQuit drain remains an OS/forced-event fallback. This is not power-loss certification.

The generated Editor-only writer seam exercises a deliberately held write, continuing frames, immutable snapshot contents, quit cancellation and failure preservation. It does not exist in delivered players. Verify-CheckpointInterruption starts and kills ONLY its own synthetic helper after durable temporary-file flush and before replacement, verifies the old durable checkpoint and retained candidate, and rejects replay. It does not kill Unity, normal apps or user saves.

Timing schema 3 identifies queued versus blocking control. Stopwatch observations are primary, Unity delta supplementary; the two-second warmup and crossing-frame ownership are unchanged. First-event detail caps, separate worst-per-phase events, 16 windows, 1,000,000 frames and eight work overlaps remain. Extra scoped timing is opt-in; its detail ring is 256 with fixed aggregates. Nested/main/worker scopes overlap and must not be summed as exclusive CPU time.

The 100 ms gameplay maximum remains provisional. No startup or whole-session fix is inferred from a gameplay pass, a faster enqueue, or faster physical storage. OS/driver caches and normal workstation activity are not controlled; instrumentation overhead and human art/listening acceptance are separate.
