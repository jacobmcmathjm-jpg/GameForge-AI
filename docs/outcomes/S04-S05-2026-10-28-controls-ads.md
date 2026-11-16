# S04/S05 controls and down-sight aiming — 28 October 2026

Scope: respond to the user's playtest report (no sprint; Space also fires) and request for right-click to look down the weapon's sights. This is a reusable trusted-kit change, not a hand-edited one-off scene or a completed game/stage.

## Implemented behaviour

- Security-combat FPS: either Shift + forward movement sprints at 6.5 m/s versus 4 m/s walking; ADS movement is 2.4 m/s. Aim, reload and firing inputs override sprint. Diagonal movement remains normalized.
- Space jumps using grounded CharacterController collision/gravity, with no second impulse in air or automatic held-key bounce. LMB alone fires in this lab. Other prototype genres retain their existing Space/action mapping.
- Hold RMB: smooth transition to the physical rifle's open reflex-style sight, with an explicit camera-aligned sight socket, 60-degree aimed FOV, no aimed bob, and recoil shared with the camera. Release returns to the original 72-degree hip view; reload temporarily unseats aim. This is a simple original physical amber sight bead/open frame, not a simulated optical collimator or new downloaded weapon.
- The existing world camera, wall occlusion and swept muzzle projectile collision remain authoritative. No through-wall weapon overlay or instant hitscan replacement.
- New scenes serialize the explicit socket. Older generated rifles have a bounded fallback to the existing named sight bead; older EXEs remain unchanged.

## Verification record

- Initial kit 0.16.50 compiled in actual Unity, with exact prior 0.16.49 protected backup. Final kit 0.16.51 also compiled and corrects the profiling version marker. Both exact development revisions remain recoverable; no unknown/modified source is overwritten.
- Initial existing host regression: 444/444. A subsequent expanded run overlapped with Unity and ten asset checks refused preparation under the editor-idle guard (435 passed / 10 failed). That diagnostic run is retained; no gate was weakened. An idle final rerun is recorded below after completion.
- New embedded PlayMode checks cover combat binding isolation, sprint/aim/reload/fire overrides, jump/landing/ammo isolation, no double-jump, pause/death/reset, ceiling collision, physical sight viewport alignment through turning/recoil/near-cover at 30/60/120 Hz, FOV restoration and actual hip/ADS camera captures.
- Final idle Release host rerun: **445/445**, none failed/skipped (Core 199, Agent 40, Security/Tools 206). The additional host regression binds the embedded profiling marker to the installed kit version and checks the combat-only input route. Evidence: `work/controls-ads-verification/host-idle`.
- Fresh lab generated through the production builder at `Assets/Scenes/S04-S05-ControlsAds-v1.unity`, kit 0.16.51 / bridge 0.17.18. Generation receipt: `002d52d5ba794dd1891c754ab1f49156.json`; scene SHA-256 `9ae584b65d1097faf6d3c5e13c088afa38aae9f3c7cde18c2e150a96d6a750ff`.
- Matching self-contained Windows app published; staged offline dependency probe returned 0. Actual Unity tests/build and visual inspection remain pending until recorded below. Human play-feel, optical realism, graphics quality, full-stage acceptance and representative performance are not inferred from these checks.
- Actual 0.16.51 EditMode suite passed 18/18. Its subsequent PlayMode run hit a native Unity crash when the new camera capture called Render on the Null graphics device. The crash log `9f20d21986e24fa995b1ae32acad790f.log` is retained; no complete PlayMode result is claimed for that run. After Unity exited, the stalled owned harness was interrupted (not a user editor or game).
- Corrected candidate: kit **0.16.52**, explicit Null-device ignore before any capture rendering; the supplemental graphics-enabled PlayMode run must execute that same test rather than ignore it. Final host rerun again passed **445/445**, none failed/skipped (`host-52-idle`). Fresh v2 scene, final Unity suites and matching v2 delivery are separate pending checks.
- Actual final 0.16.52 graphics-enabled PlayMode suite: **60/60**, none failed/skipped, Direct3D 12 / RTX 3070. Preserved XML/log: `work/controls-ads-verification/rendered-playmode.*`. Physical sight centring, sprint and grounded jump checks pass at 30/60/120 Hz. The actual ADS camera image was inspected: centre bead/open frame readable and receiver below the opening. Its all-tests capture also shows magenta feedback particles left by earlier firing tests. The shared feedback burst currently has no explicitly assigned particle material; this pre-existing cosmetic/VFX issue is not fixed or accepted by the controls work. An isolated capture is recorded separately so transient prior-test effects are not confused with the sight geometry.
- Final actual standard test route: **18/18 EditMode**, then **59 passed / 0 failed / 1 skipped** PlayMode, the sole skip being the guarded image capture on the Null device. Logs `e116b66a3d844dcfbed9642df0c8bf0c.log` and `5d709e11cef64011abe25da19d856081.log`. The same capture already ran in the 60/60 graphics suite. A further isolated graphics run passes 1/1 (`rendered-sight.xml`) with no leftover firing effects. Both actual hip and ADS images were inspected; the sight is centred and unobstructed, arms visible in hip view, no magenta object in this no-shot capture.
- Isolated camera evidence folder: `Previews/ControlsAds-tests/e5beb93d1785435988b75f54f2d55f51`. Copied actual ADS image: `outputs/ForgeCore-ControlsAds-20261028.png`, SHA-256 `16d6605bc5fd3be3c1be64488318e2c65aea2b977be888f375533ced2ef102ea`. This is a fixture camera still (no GUI), not live player video, a human aiming test or representative frame-time acceptance.

## Preservation

## Final technical delivery

- Fresh final v2 generation receipt `6dd7c29164e04351988b1be3a6643676.json`: scene SHA-256 `150d34ec25e36f298d2af6b58258194cb254fed6727bb7626aeb19f2e4a7b2d6`, kit manifest `013d8ee03fc1b71707de4d0edfb1a03d44daa996d82bd7effa38711b3202e4ee`. All **70 generated artifact/meta hashes** and the scene still match after tests and build; **349 historical non-kit baseline files** remain unchanged. Kit changes are separately hash-verified and protected by backups.
- Fresh Windows development build succeeded (`4f095fb3a8ac42fd9cea7f4142cdde76.log`), executable SHA-256 `6456584d36a1ed8f5a4662a524f11bf1da4c580870aa6257e4cf086300f4f615`. The production provenance-bound player smoke passed **5 seconds** without startup failure markers (`2969866875424d14b884ee79eed2ef4e.log`); not a foreground human playtest.
- Game: `outputs/ForgeCore-TestGame-20261028-ControlsAdsV2/S04-S05-ControlsAds-v2.exe`. All **335 payloads / 212,815,027 bytes** verified; manifest SHA-256 `1676b55eeaea775ebed8a509a6fe68bb3c355dea93517b60623dbe6fd2cc59a1`.
- Matching app: `outputs/ForgeCore-Windows-x64-20261028-ControlsAdsV2/ForgeCore.Desktop.exe`. All **481 payloads / 265,161,939 bytes** verified; manifest SHA-256 `9c20422efa79f92da306ca48cc419c6b877b9a5a9dc19703e44c851ac80944f3`. Copied offline dependency probe 0; 4 tested Release implementation assemblies match; all 34 embedded kit sources and bridge match the installed project. No nonempty actual credential fields (environment-variable *names* are not credentials; values were not read).
- Recoverable sanitized source checkpoint: `backups/20261028-controls-ads-v2`. Its separate `verification-receipt.json` records actual archive/restore/test checks after execution. This excludes external Unity project, game/art binary assets, models, live stores/config secrets and Git history; not a full workstation backup or public upload.
- The user's earlier “everything works” feedback applies to the preceding demo, with sprint/Space issues reported. These revised controls still need a new human playtest. Stage statuses are unchanged. No foreground game/app was launched for the user or popup approved.

## Preserved inputs and scope

Original imported guard GLB must remain SHA-256 `ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0`; prior v10 scene must remain `c5ae2da5972d4b97adb8a28e40edaa9ecad097372967b96963483090c07865c7`.

No new assets/providers/packages/models, source-library reviews, public upload, automatic popup approval, ongoing monitoring or unattended goal are part of this work. Existing game deliveries remain intact. S04/S05/S06 stay in progress.
