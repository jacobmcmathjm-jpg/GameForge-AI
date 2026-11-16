# Guard-motion refinement candidate — 29 October 2026

## Scope and current status

User feedback: the greybox guards and objectives work and pause works, but every guard animation and the graphics need refinement. The requested continuation remains animation fixes, S09 target placement, S10 acceptance and then S11.

This initial increment changed the reusable ForgeCore source, **not the running/delivered game**. Its source kit **0.16.58** contains 41 files and authors motion recipe `generic_mannequin_motion_v2`; at that checkpoint the external Unity project remained on **0.16.57**, bridge **0.17.21**. No fresh clips, scenes, renders, Unity test runs or game build had been produced. Source improvements were candidates, not accepted visual fixes or completed stages. The later authorized Unity follow-up below supersedes that installation status, not the historical candidate-checkpoint evidence.

## Source changes

- Added an engine-independent guard motion profile. The exact Core source is embedded in the Unity runtime kit, rather than maintaining different tested/shipped curve implementations.
- Added walking/running pelvis weight shift and torso/head counter-rotation; restrained idle/aim breathing; marker-aligned recoil; staged reload reach/head movement; hit recovery; and a staged death fall/knee fold/settle.
- Replaced sine foot lift with a squared-sine lift and quintic travel easing to reduce take-off/touchdown velocity discontinuities. Flat-ground/contact/navigation limits remain unchanged.
- Initial hypothesis: the support hand was pinned to a firing foregrip. Later Unity validation disproved that description: the original builder actually parented its support socket to the magazine, so both firing and reloading followed the magazine. Kit 0.16.58's extra target offset therefore moved the hand only about 3 mm relative to that socket. The subsequent correction separates the firing foregrip and reload magazine targets; the rejected initial hypothesis is not a verified old-runtime defect.
- Removed the guard's duplicate LateUpdate weapon/body solve. The existing post-animation event still solves contacts before emitting the evaluated muzzle marker.
- Updated the embedded Unity regression to require visible departure from the foregrip and actual magazine-target contact, rather than incorrectly requiring both hands to stay on firing grips throughout reload. **This updated runtime regression has not run in Unity.**
- Added an exact 40-file 0.16.57 upgrade whitelist; the installer still refuses modified/unknown known-source content and retains a protected prior-kit backup when a future authorized upgrade runs.

No character mesh/material improvement, mocap, new tactical AI, arbitrary terrain support, S09 placement implementation, S11 implementation, paid provider, download or human visual/rights attestation was added.

## Verification

- Release solution build: **0 warnings, 0 errors**.
- Final host tests: **519 passed, 0 failed/skipped** (Core 236, Agent 40, Security/Tools 243), at `work/guard-motion-v2-final-host/`. Twenty-four additional cases cover the curve/channel bounds, looping/action endpoints, finite inputs, reload timing, recoil marker, death progression, soft touchdown and exact embedded source/runtime-test wiring.
- Initial host run exposed floating-point easing overshoot, producing a small negative reload-contact weight. The curve output was clamped; final tests passed without widening the nonnegative assertion. Initial failed receipts remain at `work/guard-motion-v2-host/`.
- `scripts/Verify-GuardMotion/` parses all **37 C# kit files** as C# 9 and semantically compiles Runtime and Editor assemblies against read-only installed Unity reference assemblies. Final receipt: `work/guard-motion-v2-static/compilation-modular.json`. No Unity editor process or external project write occurs. Test sources were parsed, not executed; compiler checks are not physics, import, animation-deformation or rendered proof.
- The first static checker attempt combined Unity's legacy monolithic editor reference with modular references, producing duplicate-type errors. Corrected reference selection passes; the failed diagnostic receipt is retained separately at `work/guard-motion-v2-static/compilation.json`.
- Read-only integrity check: all **40 installed 0.16.57 kit files** and **335 delivered IndustrialMissionV2 payloads** match their manifests. Installed bridge SHA-256 remains `14c3b64892e11f47ded81e6ca1ce0eaeb443e922eb9551ab292c38616f05db79`.

## Blocker and next safe action

The filesystem permission request for `D:\ForgeCore\Workspace\Games\ForgeCore` returned **no granted write paths**. Only source work and read-only external integrity/reference checks were performed. Do not treat this as a grant, launch a project-writing Unity process, or work around the boundary.

After access is granted, close the existing test player normally, install the exact candidate kit with its protected backup, generate a fresh motion lab/mission, and run real EditMode/PlayMode, skin/loop/contact/weapon tests and rendered full-action comparisons. Inspect for new deformation, grip-reach or transition regressions before delivering a fresh EXE. Preserve all existing outputs. If these curves do not improve the visual benchmark, refine or reject the candidate rather than declaring success from host tests.

Then finish the unimplemented S09 target integration and S10 acceptance before claiming completion or moving to dependent S11 delivery. Independent authorized source work may proceed, but no pending human art review is fabricated.

Sanitized source checkpoint: `backups/20261029-guard-motion-v2-candidate/`. This is a source-only candidate checkpoint, not a new game or a full external-project backup; its manifest/archive readback and any separate recovery receipt describe the checks actually performed.

## Later authorized Unity follow-up

The user requested the folder prompt and the permission tool then granted D-drive project write access for that turn. Kit 0.16.58 installed and compiled, retaining the exact 40-file 0.16.57 kit at `.forgecore/unity/template-kit-backups/8583ee354d0f4930a9be6a10fdb095b3`; its backup hashes were verified. Fresh scene `Assets/Scenes/S10-IndustrialMotion-v3.unity` and its 76-artifact mission generated successfully from the unchanged original guard. Sampled loop error was 0, stance residual approximately 0.000000251 m and maximum edge stretch approximately 2.2373. These are technical measurements, not visual acceptance.

EditMode passed 19/19. PlayMode reported 67 passed, 3 failed, 1 intentional graphics skip: the strengthened reload test found only about 3 mm of hand departure at each of 30/60/120 Hz. Receipts `work/s08-s10-verification/edit-tests-motion-v2.json` and `play-tests-motion-v2.json` retain the real results. The initial foregrip hypothesis was corrected rather than lowering the visibility assertion.

Candidate kit 0.16.59 now creates a distinct guard firing foregrip on the weapon, while the reload support target follows the actual magazine. Its installation protects the exact prior 41-file candidate at `.forgecore/unity/template-kit-backups/6a706655bd7e44838645de59549ea8b9`. Fresh v4 generation and validation results must be recorded separately when completed; failed v3 artifacts are retained, not overwritten. Earlier source-only checkpoint receipts remain statements about their historical source version.
