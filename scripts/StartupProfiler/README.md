# Current-game startup CPU diagnostics

These are owned, opt-in developer diagnostics, not a new game-generation feature or a replacement game. Use the retained .95 game, never PR #3's older purple-flash fixture. No scene, SDK/runtime source, normal save, audio, rights review or human approval is changed.

## Capture and read

`Invoke-StartupCpuCapture.ps1 -Attempt <fresh-label>` copies the sealed .95 delivery to its own `work/startup-cpu/<label>/Player` and launches its existing real mission/timing probes at 1920 x 1080. Add `-Variant On` or `-Variant Off` only after the explicit diagnostic export pair exists. `-NoProfiler` is required for the ordinary timing comparison; profiled numbers are attribution evidence only. The process owns only its diagnostic saves and has a 260-second timeout. Existing attempts are never overwritten. Do not run alongside another Unity/player/performance test.

The profiler uses the documented startup flags, a requested 240-frame limit and a 64 MiB profiler working-memory limit. Unity exports frame **0 through 240**, including native initialization: **241 records** in these captures. The memory flag does **not** cap raw-file disk usage: the splash-enabled trace reached **1.37 GB**. Keep raw traces local, allow sufficient disk space, and inspect one capture before making more.

Create a separate scratch Unity project under ignored `work`, set its installed Editor version, and copy `StartupProfileReader.cs` into its `Assets/Editor`. Run Unity with `-batchmode -nographics -noUpm -projectPath <scratch> -executeMethod StartupProfileReader.Export -forgecore-profile-source <absolute-existing.raw> -forgecore-profile-json <absolute-fresh.json> -logFile <fresh.log>`. Use the normal configured Windows licensing context; do not alter licensing. Never import the raw trace into the game project.

The reader exports at most 1,001 frame records / 20,000 samples of at least 1 ms. It separately aggregates all samples starting within the last 650 ms of the first frame, limited to 512 names / top 80 returned names. This catches repeated sub-millisecond splash/preload calls which a long-sample-only view misses. Truncation/drop counts are explicit. Thread index zero is checked in results to be `Main Thread`. These are **inclusive, nested timings**: never add parent and child totals as exclusive CPU time. Native profiler time, Unity time and the game's Awake-to-Update Stopwatch interval are different clocks/regions.

## Explicit splash-only experiment

`StartupSplashComparisonBuild.cs` is a temporary Editor helper, not part of the installed SDK. Its exact project, scene hash, original settings hash and fresh output folders are guarded. It builds the same scene/runtime with `PlayerSettings.SplashScreen.show` on then off, using Development options in both cases. A `finally` restores the original setting and exact settings-file bytes; verify their hash again after Editor exit. An OS/process crash can interrupt `finally`: inspect/recover settings before continuing after any timeout. Never replay an ambiguous build over an occupied output.

The helper was temporarily copied to `Assets/ForgeCoreStartupDiagnostics/Editor`, then moved out with its generated metadata into the local ignored `work/startup-cpu/retired-project-helper` recovery folder. It is absent from the live project. Both diagnostic exports remain in separate `Builds/S01-StartupFade-20261116/SplashOn` and `SplashOff` folders. Normal app/game deliveries and normal project splash settings are unchanged.

The evidence sequence is `splash-on-plain`, `splash-off-plain`, `splash-on-repeat`, all unprofiled, plus `splash-off-profile` for native attribution. `Test-StartupSplashComparison.ps1` requires exact .95 runtime/level identities, successful real mission/audio/save evidence, unchanged phase gates, an off first interval below 200 ms and a reduction of at least 400 ms from both controls. It seals comparison output before reporting failure. The extra `StartupSplash-Off` candidate in `Invoke-CourtyardPlayerChecks.ps1` runs the four existing graphics/state/firing/corpse checks without weakening their gates.

`Test-StartupSplashComparisonHarness.ps1` covers ten synthetic comparator success/rejection cases. Its fixtures are explicitly synthetic and are never counted as mission or performance evidence.

## Interpretation

The on/off/on experiment attributes roughly 500 ms of the initial interval to Unity's splash fade. It does **not** fix or explain the original intermittent gameplay freeze. Do not remove the first interval, extend warmup, change the 100 ms provisional gameplay gate, or mark whole-session smoothness accepted. No normal game update was shipped by this diagnostic increment.

Primary references: [Unity startup-profiler arguments](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/profiler/command-line-arguments), [RawFrameDataView](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/profiling/rawframedataview), [Unity 6000.5 splash settings and fade](https://docs.unity.com/en-us/engine/6000.5/manual/unity-editor/editor-settings-reference/comp-manager-group/class-player-settings-splash-screen), [supported splash show property](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityeditor/playersettings/splashscreen/show).
