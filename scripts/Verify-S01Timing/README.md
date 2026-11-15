# S01 isolated timing verification

This branch comes from the older October source checkpoint. **Its prototype is not the later approved game.** It does not contain the subsequent warm muzzle shader, layered rifle audio, recorded footsteps, detailed guards or retro-profile work present on the workstation. Do not distribute this fixture as a game update or overwrite a later installation with it.

Use only a clean branch checkout. The helper creates `work/s01-live-v1/TestGame`, imports existing local test-framework packages, installs the candidate kit there, creates one primitive FPS scene and builds its player. It never targets `D:\ForgeCore\Workspace\Games\ForgeCore`. No rights/visual approvals, normal ForgeCore store or model/provider are opened. Unity's normal licensing/package setup still applies; network activity is not traced.

Arguments are `mode editor.exe editor-version existing-package-cache`. Modes: `prepare`, `bootstrap`, `generate`; `generate-retry1` is the explicitly recorded correction after the retained .34 compile failure, not an automatic write retry. Existing destinations/receipts stop execution. Keep the original failure and its protected kit backup.

`profile-first` and `profile-repeat` additionally require the fifth argument `--allow-legacy-visual-fixture`. The player may display the older, unaccepted effects/audio, including the purple-flash issue. This acknowledgement is only permission to launch the diagnostic fixture, **not visual/rights acceptance or permission to replace the newer game**. The initial two launches happened before this opt-in was added; the user identified the visible older fixture and no more players were launched afterward.

Measurements are two ordered launches of one exact new build, not controlled cold-cache testing. Requests/results, run observations, logs and budget failures are retained locally under the fixture's `.forgecore/unity/player-profile`. Receipts live in `work/s01-evidence`. Do not upload these raw runtime folders or present a good p95 as a freeze fix.

`Verify-S01Source` separately compiles every generated Runtime/Editor source against installed Unity references in C# 9, without launching Unity. This catches embedded-source errors that a host .NET build does not compile. It parses generated test sources but does not execute Unity tests.
