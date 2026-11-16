# Fallen guard support — 29 October 2026

The user's screenshot shows a horizontal corpse visibly above the floor; they confirm that waiting does not bring it down. This is a specific failed grounding check, not acceptance of the earlier death animation. At investigation time, the responding user player was still the delivered v6 process; no user player/editor is closed or overwritten. The new candidate must combine the retained warm-flash fix with corrected body support.

## Actual baseline, not a cached error value

The new opt-in development-player `-forgecore-corpse-probe` activates the authored power gate through `TryInteract`, inflicts lethal damage through the actual enemy component, and lets the normal Animator/post-animation pipeline run. At about .15, .4, .8, 1.2, 1.8, 2.2, 3 and 5 seconds it independently bakes the rendered skin and measures floor and torso vertices. It writes a fresh unique PNG/report directory next to its own executable and exits; ordinary launches have no diagnostic flag. The launcher has a timeout and only terminates its own process if necessary.

Baseline kit 0.16.67 / v12 retains old grounding code: lowest vertex **5 mm** above the floor, cached grounding error **zero**, but the torso stays **231.2 mm** above the floor at 1.8–5 seconds. Thus the existing lowest-vertex test does not establish body contact. The first probe's exit 0 used that old narrow criterion; its raw data fails the subsequently added torso criterion and is not a valid full corpse-support pass. Its initial observer position was behind the divider, so those early PNGs are not useful visual evidence; later probes use an unobstructed side view.

## Candidate refinement

- v13 / kit 0.16.68 adds a death-only post-animation support solve: orient the torso along the floor and flex legs rather than supporting the body on downward-extending limbs. Actual torso clearance improves to **47.1 mm**, stable through five seconds. Despite passing the first 50 mm limit, its side image still shows a gap, so this diagnostic candidate is retained but not delivered as the final fix.
- v14 / kit 0.16.69 also relaxes the downward arm brace outwards and tightens required settled torso clearance to **15 mm**, independently of the existing lowest-vertex check. Settling blends over .65–1.3 seconds; it does not teleport the gameplay/controller root. This is deterministic support for the existing flat lab/industrial guard, not ragdoll physics or arbitrary-terrain/cover-body collision acceptance.
- Living foot placement, gait, movement/control code and all authored motion curves are unchanged. The support solve runs only on the dead branch after Animator evaluation. The earlier soft white/amber flash and explicit bundled shader are retained.
- Add three 30/60/120 Hz torso-support cases and an automatic-frame death/pause/reset case. The standalone report now records dominant lowest bone as well as core-body clearance. Host contracts check that the correction is death-only and the real mission gate is used.

No wider stage completion, human animation/visual acceptance, source/rights review, model/API call, download or GitHub upload is implied. Protected exact-version kit backups and old scenes/builds remain intact. Final measured candidate, regression and delivery/checkpoint results are appended only after execution.

## Final v16 / kit 0.16.71

The stricter v14 diagnostic correctly fails: the lowest point is now `Hand.R` but the torso is still **76.8 mm** high. v15 measures hand/core-body vertex groups and relaxes hand support relative to torso geometry before the existing whole-body grounding pass. That produces **5 mm torso and whole-mesh clearance**, with `Spine` as the lowest supporting bone, stable through five seconds. v16 additionally ensures that a hand needing no correction does not skip processing of the other hand. Earlier candidates are retained, not labelled as final passes.

The final actual D3D12 standalone corpse diagnostic exits **0**; body contact is reached around **1.2 seconds**, remains at **0.005 m** through **5.0 seconds**, and the side-view image was inspected. Floor height is independently raycast as 0; whole-skin and majority Hips/Spine/Chest vertex groups are baked from the actual animated renderer. The geometric tolerance test requires core clearance <=15 mm, not merely a touching toe/hand. This still needs the user's visual check and is not ragdoll, full collision, realism or arbitrary-terrain acceptance.

The combined build's separate actual hip/ADS/guard/hit diagnostic also exits **0** with both firing markers/projectiles, seven simulated hit particles and zero magenta in all seven firing/feedback captures. Warm shot pixels: hip **1,096**, ADS **395**, guard **4,136**. The new floor correction does not remove the soft white/amber flash.

Actual evidence folders under `D:/ForgeCore/Workspace/Games/ForgeCore/Builds/S10-CorpseGround-v16/`:

- `ForgeCoreRenderProbe-ce0b4eb21e354361ae7eb60d6514d4eb/`: corpse report, before image and eight time samples.
- `ForgeCoreRenderProbe-0af2d62678d34f3e8594f1e0ed7ff867/`: firing/feedback report and seven camera captures.

Receipts/logs: `work/s08-s10-verification/build-corpse-ground-v16.json`, `standalone-corpse-ground-v16.log`, `standalone-render-ground-v16.log`.

Final host **537/537** (250 Core, 40 Agent, 247 Security/Tools), zero failed/skipped; `work/corpse-ground-v16-host/`. Unity **19/19 EditMode** passes. Final PlayMode results are recorded after execution, not inferred from the standalone checks. All **45 installed kit files**, **76 unchanged v6 animation/presentation artifacts**, and unchanged v6 scene SHA-256 `394e896d08a8f805858bddb5dfa2b9cb7d86b141fb5313390e7e6e4a2f4d136c` were verified. All eight authored clips remain unchanged; the death change is the post-Animator support solve only. Human full-motion acceptance remains separate.

Matching app publishes with the previously verified safe offline workstation config substituted for live source config; the safe config matches by hash and offline `--verify-package` exits 0. No user database/model is opened by that probe. New delivery paths: `outputs/ForgeCore-Windows-x64-20261029-CorpseGroundV16/ForgeCore.Desktop.exe` and `outputs/ForgeCore-TestGame-20261029-CorpseGroundV16/S10-CorpseGround-v16.exe`. Keep the complete game folder; launch normally without either diagnostic flag. The old user v6 remains separate and will not update in place.

## Final regression, delivery and recovery

- **537/537 host**, **19/19 Unity EditMode**, **78/78 graphics-enabled PlayMode**, no failures/skips in those runs. Headless PlayMode: **76 passed / 0 failed / 2 intentional graphics skips**. New torso tests retain the stricter 15 mm limit and pass at 30/60/120 Hz plus automatic-frame death/pause/reset. Final Release solution build has **zero warnings/errors**. No representative performance/soak or human quality acceptance is inferred.
- Unity receipts: `edit-tests-corpse-ground-v16.json`, `play-tests-corpse-ground-v16.json`, `rendered-corpse-ground-v16-playmode.xml/.json/.log` under `work/s08-s10-verification/`.
- App: **481 payload files / 265,629,315 bytes**, manifest SHA-256 `04f8a178532d7022606f09bd2a943b1720782bb47d736cb10a83d6fc569df77b`. Game: **353 payload files / 220,108,715 bytes**, manifest SHA-256 `79fc0f6c8f01816b6380b31a43a6eb83ca004598db892abe87bf403d833514ff`. Every listed size/hash and aggregate was checked; six shipped app implementation assemblies match tested Release outputs. The game includes both probe PNG/report sets. Copied app offline probe exits 0, zero nonempty actual credential fields, cloud/catalogue disabled.
- Sanitized source checkpoint: `backups/20261029-corpse-ground-v16/`; a separate verification receipt records its archive hash/readback after creation. This does not claim independent restore/testing. Outcome-status snapshot: `docs/archive/2026-10-29-corpse-ground-v16-outcomes.md`.
- Failed/visually rejected diagnostic revisions and protected prior kits remain available; none replaces the user's old player. The final correction lives in ForgeCore's trusted reusable generator/runtime, not a one-off modification to the exported EXE. Full motion/art, arbitrary terrain/cover-body physics, S06/S09/S10/S11 and human acceptance remain open.
