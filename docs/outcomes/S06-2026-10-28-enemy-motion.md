# S06 — flat-lab enemy-motion refinement

User-authorized next pass after the S05/S06 weapon/contact checkpoint. **In progress; no motion/art/gameplay acceptance is inferred from source changes.**

## Implemented scope under validation

- Bounded acceleration/deceleration and turn-rate control; guards wait/slow while facing away from their intended route instead of instantly translating sideways at full speed.
- Hit reactions stop locomotion and heading changes. Death remains terminal and collision-disabled until explicit reset; the existing original death clip is retained, not replaced with a claimed ragdoll.
- Original post-Animator foot placement for the existing flat static training lab: distance/turn-driven alternating steps, world-space stance anchors, swing clearance, timed descent on stopping, and small visual pelvis compensation for leg reach. Controller/root movement remains authoritative.
- Corpse grounding samples the actual baked skin, adjusts the visual root only against the flat floor, then stops sampling once the terminal clip has settled. It is not per-limb physics, arbitrary-terrain support or a performance acceptance.
- Existing hand contacts and evaluated-barrel firing run after the leg/visual pass; action exclusion, pause, death and reset protections must still pass.

Fresh lab target is v7, template kit 0.16.46, bridge unchanged at 0.17.18. v4/v5/v6 scenes, source bundles and previous Windows deliveries are retained. Exact known kit upgrades require protected backups. No paid provider, package/weight download, secret extraction, GitHub upload or unattended work is part of this pass.

## Verification plan and open quality gates

Seven new PlayMode cases cover real controller travel/turning/acceleration, actual ankle displacement against planted world anchors, repeated-solve idempotence, hit/root/heading freeze, independently baked corpse-floor contact, pause and reset at 30/60/120 Hz simulation steps where applicable. These are not measured rendered frame rates or human animation approval.

The initial Release solution and helper builds have no warnings/errors. Host and actual Unity tests are being run; tests added to source are not passes until executed. Human motion review, rough foot/hand style, transitions, material/lighting work, arbitrary terrain, crowd tactics, representative performance and full playable-level acceptance remain open.

## Revision journal

- Initial host suite: **323/323**, no failures/skips (`work/enemy-motion-verification/host`).
- First kit 0.16.46 upgrade retained backup `8b116ab0fa074aa5aa652bb5c51cacf9`; compile succeeded, log `c234a2f0cae6499a8ad8ed4f9756c3cb.log`. Fresh v7 generation/evidence `f9d7be4353ae411eb9aa5449e2677046.json`; its Unity tests are running against the exact installed first revision.
- A separate staged refinement increases turn-step cadence and completes an alternating recovery shuffle when stopped feet are displaced/rotated. An additional test requires feet to finish a large turn on their own sides, with aligned planted orientations. This is not relabelled as part of the first installed revision; it will receive its own exact kit/fixture and test results.
- First v7 / kit 0.16.46 results: **18/18 EditMode and 40/40 PlayMode**, no failures/skips. Logs `4c5e6ca5aa59477392f744398d07afd4.log` / `de0cb97e44f4481993974bb96bf2ffe1.log`. This proves the first contact/root/grounding revision, not the staged recovery-shuffle refinement.
- Final candidate **v8 / kit 0.16.47** adds the stop/turn recovery behaviour and an eighth motion test. It will be run separately; v7 remains retained. Bridge stays 0.17.18; the kit now has 33 exact embedded source files.
- Candidate Release build: 0 warnings/errors; host **323/323**, zero failed/skipped (`work/enemy-motion-verification/final-host`). Matching Windows app publishes and offline dependency probe exits 0; cloud/online flags remain disabled, with no nonempty actual credential fields.
- Kit 0.16.47 backup `7f9bd4b1a7c140c79889a5049947c413`; compile log `f5c46bbbb6de438a85a852b2fbc265d2.log`. Fresh v8 scene/34-artifact evidence `ca17efc490e047938f8e16c5005eae29.json`. The final Unity tests are running.
- Final v8 / kit 0.16.47 results: **18/18 EditMode and 41/41 PlayMode**, none failed/skipped. Includes the large-turn own-side/aligned/planted-foot check, real routed travel/ankle-slip/idempotence and independent corpse skin-floor checks. Logs `f430f65214b945208b7c40301f53d54a.log` / `658ccf65dad0411380fb1b3a4681bb9b.log`. Host candidate still passes **323/323**. These are technical regression results, not a completed visual/rights/play-feel/performance stage.
- Actual runtime camera capture succeeded after the bridge's 15-second wall-clock interval: 1280×720 / 116,280 bytes / SHA-256 `80e7e5e9051cb73357378b67a2303a3f80574cde7c1e6c3e12225c9acb0a91bd`; log `ec5fa0971ea049feb4a4cf0a1a1296c5.log`. Viewed image shows the front-facing guard and another guard near the divider, but remains basic grey-lab/procedural art. A single image is not a motion recording, HUD/standalone control check, measured simulation duration, human acceptance or performance benchmark.

## Technical delivery and next safe action

- Fresh Windows development build succeeded, log `3feb090c134f45af9f380feb69439363.log`; build-provenance SHA-256 `361c829915486888a4a99406e4b4255eb43f397ca932d3999f14bb281c9dfbef`.
- Game: `outputs/ForgeCore-TestGame-20261028-EnemyMotionV1/S06-SecurityCombat-v8.exe`, with the entire folder. All **335 files / 227,390,314 bytes** match delivery hashes/lengths/aggregate; manifest SHA-256 `6dbe4be1f2f6b35055f19601061162b2527cd2232eb7a0322377d41717992be8`.
- Matching ForgeCore app: `outputs/ForgeCore-Windows-x64-20261028-EnemyMotionV1/ForgeCore.Desktop.exe`. All **481 files / 264,854,124 bytes** verified; manifest SHA-256 `07d4d533fcf16c481b246f36ff6fd4e4a4886c035cb346c73220399136657301`. Copied app's offline `--verify-package` returned 0. Close an older ForgeCore copy before starting it against the same store.
- Preview copy: `outputs/ForgeCore-S06-SecurityCombat-v8.png`. Source GLB remains `ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0`; previous v6 scene remains `ac2591fa40d62234ec9c5a6ee873136865400fe25eccb573e1cf9eb970d4a448`; new v8 scene `622d37b838646ce0817168cbe7c2d141e4a296d390892cf1024e5b78ed97e4d3`; kit manifest `cc4321a5557d376aca8be958dd86389144cd38603451e349745ba9053a758263`.
- Sanitized source checkpoint target: `backups/20261028-s06-enemy-motion-v1`; final separate verification receipt records archive/restore results. External Unity project, binary assets/builds, live configuration, weights, stores/logs and full Git history are excluded. Local checkpoint only; no GitHub upload or automation.
- No interactive player launch or GUI approval retry was attempted in this pass. Standalone controls/play-feel, rendered motion sequence, human visual/rights review and representative frame/hitch/crowd performance remain pending. **S06 remains in progress.** The next bounded pass is character materials/lighting and weapon presentation, alongside a human motion/playtest review; not an immediate jump to a complete designed game.
