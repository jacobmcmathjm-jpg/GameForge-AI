# Current-game timing integration

This is the November source, not the older PR #3 fixture. Kit .93 adds opt-in bounded diagnostics to the unchanged .92 game. Do not launch the legacy fixture to judge current graphics/audio.

Run from repository root. Each production-tool step writes an immutable receipt below work/current-s01. A failed/ambiguous receipt stops replay; inspect it and select a separately coded fresh attempt. Real Unity requires the licensed normal Windows context, no other Editor, and write authority for the named project.

1. dotnet run --project scripts/Verify-CurrentGameTiming -- audit-before
2. dotnet run --project scripts/Verify-CurrentGameTiming -- install
3. dotnet run --project scripts/Verify-CurrentGameTiming -- compile
4. dotnet run --project scripts/Verify-CurrentGameTiming -- build
5. Export the clean fresh folder before probes create diagnostic subdirectories.
6. scripts/Invoke-CourtyardPlayerChecks.ps1 -Candidate CurrentTiming-v1
7. scripts/Invoke-DemoAcceptance.ps1 -Candidate current-timing-v1 -Attempt first -Viewport 1080p -LongSoak
8. scripts/Test-CurrentGameTiming.ps1 -Attempt first
9. Repeat with a fresh attempt, without changing executable/scene/level0.
10. edit-tests, existing graphics PlayMode suite (106), then audit-after.

Build is the existing Retro64-v2 scene into a new S01-CurrentGame-20261116 folder. This harness has no scene/model/audio generation or review-decision mode. The install uses the exact protected production .92 upgrade; modified/unknown installed sources are not overwritten.

The full mission probe adds -forgecore-timing-probe. Without that flag no new timing collector/context snapshots/JSON are created. Gameplay controls, warm shader, recorded footsteps, weapon sounds, clips and the existing two-second measurement warmup are unchanged.

Primary intervals are consecutive telemetry Update Stopwatch observations. Unity unscaled delta is supplementary, may cover a different interval, and cannot hide a later worst wall-clock sample. Startup Awake/Ready values are Unity initialization latencies, not native process startup frames. Native pre-Awake startup is unobserved. Window maxima/counts must match the pre-existing gameplay sampler exactly; the boundary-crossing interval is not discarded.

Caps: 64 first detailed events, separate worst wall event per phase, 16 windows, 1,000,000 observations, 8 overlapping work entries per retained interval. Dropped details are counted; frame/window overflow prevents provisional acceptance. GC/memory/work overlaps indicate context, not causality. Normal-frame collection does no file/JSON work. Instrumentation overhead is not independently benchmarked.

The 100 ms gameplay maximum is provisional and reported separately from mission completion. It does not replace existing p95/GPU/memory contracts or establish startup/warmup/whole-session acceptance. OS/driver caches are not controlled by this harness. No freeze fix, human art/rights decision or new stage completion is implied.
