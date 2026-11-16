# Authorized guard-motion runtime follow-up — 29 October 2026

The user requested the project permission prompt. The tool granted write access to `D:\ForgeCore\Workspace\Games\ForgeCore` for this turn; the earlier access blocker no longer applied during this run. No Codex/Windows security setting was changed by the assistant.

## Result

Installed kit **0.16.59**, bridge **0.17.21**, motion recipe **generic_mannequin_motion_v2**. Fresh scene: `Assets/Scenes/S10-IndustrialMotion-v4.unity`; fresh prefab/report folder: `Assets/ForgeCore/CharacterAnimations/s10-industrial-motion-v4/`. The original Blender guard and older scenes/builds were preserved.

The first installed candidate (0.16.58/v3) passed editor tests but failed the strengthened reload check at 30/60/120 Hz. Inspection disproved the initial source-only diagnosis: its old support socket was already magazine-parented, not a stationary firing foregrip. The extra offset moved the hand only about 3 mm. The corrected builder creates a separate stationary firing foregrip, while the reload contact follows the actual withdrawing/reinserting magazine. The same >3 cm departure and <2.5 cm contact assertions now pass; the failed candidate and receipts remain retained. See [the source candidate and correction history](S03-S06-2026-10-29-motion-refinement.md).

This also installs the source candidate's revised idle/locomotion/aim/action body curves, recoil timing, soft foot lift/landing and staged hit/death motion. These are original procedural curves, not mocap or a production animation benchmark. Graphics/assets remain prototype-quality and require further refinement.

## Verification

- Final Release host regression: **519 passed**, none failed/skipped (236 Core, 40 Agent, 243 Security/Tools), `work/guard-motion-v4-final-host/`.
- Release solution build: **0 warnings/errors**.
- Actual Unity EditMode: **19/19**, no failures/skips. Standard headless PlayMode: **70 passed, 0 failed, 1 intentional camera skip**. Receipts: `work/s08-s10-verification/edit-tests-motion-grip-v3.json` and `play-tests-motion-grip-v3.json` (the receipt suffix names the third grip attempt; it tests the newly generated v4 prefab).
- Actual graphics-enabled PlayMode: **71/71**, no failures/skips. Full XML: `work/s08-s10-verification/rendered-motion-playmode.xml`. All three guard reload/contact/death cases passed at 30/60/120 Hz. This is not a new full human mission playthrough, balanced difficulty, realistic art, performance/soak or animation-quality acceptance.
- Fresh animation report: 8 clips, finite sampled geometry, maximum loop error **0 m**, stance residual approximately **0.000000251 m**, maximum sampled edge stretch approximately **2.2373**. These measurements retain their technical thresholds; a passed threshold is not human deformation-quality acceptance.
- Verified **76 new artifact hashes**, scene SHA-256 `8071270fc04487a7b77028513ab4f93b3c8d4ad33366acfb17c65a17aafcea50`, and all **41 installed kit source hashes** after validation. Evidence request: `c54e3e6d789740d4a2af009d5462d42a`.
- Original guard SHA-256 remains `ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0`. Older v2 mission SHA-256 remains `64cd1734cdc4b6a6cc41f6ff89911db32a7eb6f0458db57d7ef94ff6320b25ed`, matching its archived evidence. The previously delivered Windows game was not replaced.

## New test game

`D:\ForgeCore\Workspace\Games\ForgeCore\Builds\S10-IndustrialMotion-v4\S10-IndustrialMotion-v4.exe`

The production Windows build and five-second provenance-bound responsive startup smoke both passed. Receipts: `work/s08-s10-verification/build-motion-grip-v4.json` and `smoke-motion-grip-v4.json`. Keep its entire folder, including `_Data`, DLLs and build provenance. This is a new development test game, not a final release or a replaced ForgeCore app package.

Controls remain WASD/mouse, Shift sprint, Space jump, LMB fire, RMB sights, R reload, E objective interaction, Esc pause, F5 restart. The mission remains the explicit industrial greybox; no unreviewed props were silently placed. Human review should compare all guard actions and transitions, especially firing grip → magazine reach/withdrawal/insertion → firing grip, as well as walking/turns and hit/death motion.

## Preserved upgrades and remaining work

- Protected original 0.16.57 kit: `.forgecore/unity/template-kit-backups/8583ee354d0f4930a9be6a10fdb095b3` (40 file hashes verified).
- Protected rejected 0.16.58 candidate: `.forgecore/unity/template-kit-backups/6a706655bd7e44838645de59549ea8b9`.
- Local sanitized source checkpoint: `backups/20261029-motion-v4-runtime/`; its receipt records archive verification, not an independent restore of this final version. The older 0.16.58 source-only candidate has its own separate recovery test and is not relabelled as the final source.

**S09 target selection/inspection/review/placement integration remains unimplemented beyond the existing source manifest/import gate. S10 still needs integration, remaining playthrough/balance/visual acceptance. S11 menus/settings/persistence/accessibility work has not started.** The animation candidate is technically verified, not blanket human acceptance or completion of those stages. Continue those requested stages with the required source/target human reviews, not fabricated attestations.
