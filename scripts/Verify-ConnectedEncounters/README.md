# Connected-room return-fire increment

This opt-in development diagnostic addresses a coverage gap: the fast actual-mission bot can clear all three guards without receiving return fire. That is not evidence of human combat balance or proof that the enemies cannot shoot.

Kit 0.16.108 adds fifteen graphics PlayMode cases to the generated connected profile: all three indoor guards at 30/60/120 Hz, pause/resume, and a deliberately inserted opaque-cover marker recheck. Existing gameplay/range/damage, audio, flash shader, motion clips, assets and geometry are unchanged. The synthetic cover is a fixture, not a claim about authored cover placement.

The standalone `-forgecore-connected-encounters-probe` only exists in development non-Editor players. It restores controlled mission stages and repositions a stationary, non-firing player six metres from each unchanged authored guard. The actual enemy Update, evaluated shot marker, muzzle recheck and swept projectile must reduce health after pause/resume. Each case is bounded to six seconds; the owner process has a 45-second limit. It uses a fresh adjacent diagnostic save root, not normal user saves. It is explicitly **not** traversal, balance, performance, art or stage acceptance. The ordinary mission probe remains separate and must still traverse the three-zone map without player teleport/direct damage.

`Program.cs` uses the production policy/executor and exact-hash protected kit upgrade. `audit-before` must precede `install`. Generate/build/capture use fresh destinations; successful receipts are not replayed, failed receipts are preserved. `audit-after` and `audit-final` compare the historical artifacts and review-record hashes. They do not parse or grant reviews. No model, API, download or live database is involved.

Run serially on the workstation, with Unity idle between operations:

1. Build this harness offline; `audit-before`, `install`, `compile`, `generate`.
2. `Invoke-VisualPassTests.ps1 -Attempt <fresh> -ExpectedPassed 144 -ProcessLimitSeconds 600`.
3. `build` and `edit-tests`; seal any delivery before player probes create diagnostic folders.
4. `Invoke-ConnectedEncounterChecks.ps1 -Attempt <fresh>`, then `Invoke-CourtyardPlayerChecks.ps1 -Candidate ConnectedEncounters-v1 -Attempt <fresh>`.
5. `Invoke-DemoAcceptance.ps1 -Candidate connected-encounters-v1 -Attempt <fresh> -Viewport 1080p`; validate unchanged timing gates with `Test-CurrentGameTiming.ps1`.
6. Run full host suites and final preservation audit. Record actual failures and results in the dated outcome, not in mutable historical receipts.

Prior v8 remains the human testing baseline until this new candidate's verification is complete. No source/target Blender art or rights decisions are invented by these tests.
