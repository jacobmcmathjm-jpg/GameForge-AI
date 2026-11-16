# ForgeCore experience-based improvement — 31 October 2026

The user clarified that the learner is **ForgeCore across props, models and movement**, not a new guard mesh. This increment changes the reusable host pipeline, not the accepted guard, animation, audio or playable map.

## Implemented

- `AssetImprovementModel` is a deterministic local advice model, not a trained neural network. It evaluates structured bounded experience, never edits code or grants approvals.
- Every completed local bundled Blender creation/reuse and ordinary failed execution attempts to retain a content-addressed event under `.forgecore/blender-library/experience/v1`. All existing supported prop/character families participate. No raw prompts/errors, environment values or arbitrary free-text instructions are stored. Cancellation before completion creates no false success. External providers/arbitrary imported assets are not covered.
- Successful exact owned-source Unity rig/motion evidence records bounded errors. Failed owned-source rig/motion attempts are separate, with no invented cause. Existing host validators still gate successful evidence. A failed journal write does not turn a completed generation into a retryable failure. Source results expose separate `LearningStatus`; rig/motion journal failures do not invalidate their technical outputs.
- Read-only source inspection includes an experience plan. Paths are namespace-bounded before opening, hashes are rechecked, and unexpected fields/changed measurements/stale evidence are rejected. Persisted data is advisory, not an authenticated quality/approval authority; no journal-supplied action executes.
- Distinct candidates/configurations are counted separately from cache reuses. Numerical rig/motion comparisons preserve non-dominated baselines only in matching source/test contexts; tradeoffs remain tradeoffs. Source triangle/material counts are resource measurements, **not visual-quality scores**.
- Current exact source visual/rights decisions come from the existing human-review mechanism. Rejection advises revision. Source approval remains separate from target-game acceptance.
- Explicit **experience-guided** creation consults compatible sources, prefers candidates with both source reviews, rechecks the selected source/history and proposes exact reuse through the existing separately approved import/Unity pipeline. Rejected/invalid-review candidates are not silently regenerated. A complete valid search with no compatible history uses the existing bounded baseline recipe. This is review-informed selection/reuse, not self-authored geometry or motion code.

## Prompts in the new app

`Create an experience-guided wooden crate in Blender. Name it yard-crate.`

Other exact labels: `industrial barrel`, `concrete barrier`, `industrial fence`, `shipping container`, `rigged mannequin`, `industrial security character`. Ordinary creation prompts retain prior behavior: guided selection is opt-in, not universal.

`Plan improvements for local Blender asset <exact bundle hash>.`

`Plan movement improvements for local Blender asset <exact bundle hash>.`

Both planning prompts return read-only source/rig/motion experience. They execute no advice and train no weights. Movement history stays scoped to the exact source asset. Source reviews still require the existing verified-preview dialogs.

The first journal is bounded to 256 events. Full journals explicitly stop adding events; scans report bounded/partial history, not unlimited lifetime learning. Existing history is **not** backfilled or labelled as fresh testing. No neural model file, API key, paid service, online training or automatic recipe rewrite is introduced.

## Verification

- Release solution build: zero warnings/errors.
- **721 host checks pass:** 374 Core / 40 Agent / 307 Security; zero failures/skips. `work/asset-learning/core-learning-final.trx`, `agent-learning-full.trx`, `security-learning-full.trx`.
- Real Blender production test in a fresh repository-owned scratch project generates two frozen-recipe crate variants and exactly reuses one. Its experience report distinguishes two models from one reuse. Production executor/policy, no model/provider fallback. `scripts/Verify-AssetLearning` and `work/asset-learning/production-v1/verification.json`.
- An actual built-in guided prompt reads that library/history and imports an exact compatible source into the scratch project **without another generation**. Stops before Unity; `guided-reuse-verification.json` retains three steps.
- Synthetic host plumbing fixtures cover source, successful rig/motion, failed motion, and invalidation of stale motion proof. These are **not new real Unity animation/runtime-quality tests**.
- Tamper tests cover changed bytes, forged approval fields, forbidden paths, changed measurements and an unwritable journal. Earlier focus failures remain: the missing `InvalidDataException` catch was corrected; the combined motion fixture needed its synthetic Unity metadata. Final regressions pass without relaxing actual validators.
- App publishing uses the prior verified offline configuration, not live config, weights, SQLite or secrets. No normal app/store opens during packaging checks.

## Delivery and preserved work

App-only: `outputs/ForgeCore-Windows-x64-20261031-AssetLearningV1/ForgeCore.Desktop.exe`. Keep the entire folder; close old ForgeCore Desktop first. Existing workspace/store configuration is unchanged. Previous deliveries remain untouched. Packaging/recovery evidence is in `work/asset-learning` and the new dated backup.

Original recipe bytes remain unchanged: `IndustrialSecurity.py` SHA-256 `693c27499a71f2271b739c9211167af41e426dbda91216d5bec37b0b51b2bac3`; original props `83e4d0c59b9d728e8c684349e831ebb012a1702ef864b2035d98b4a4668b05d0`; refined props `2d4b3e1acde0a42240555eb92a2d89d894a3b9f31326b43eefd0927559ef805e`. Bridge/kit sources and live D-drive projects are untouched. No new game, bigger map, improved guard appearance, changed animation/audio, art decision or stage completion is claimed.

## Next work, not delivered here

1. Evidence-driven bounded candidate parameter/revision proposals and comparison checkpoints. A rejected model still requires a real recipe/parameter revision; it is not automatically fixed.
2. Structured human critique and offline perceptual/animation evaluation. Rejection alone cannot identify anatomy, cloth, lighting, timing or contact problems; numerical tests cannot judge realism.
3. Preserve known-good versions and reject measured regressions during proposed iteration, keeping visual/rights decisions human.
4. Resume approved-source level assembly, physics/navigation/runtime verification, then larger connected maps. This remains separate from general all-genre autonomous production.

ForgeCore now has persistent experience and review-informed reuse. It does **not** independently learn new sculpting techniques, train a general 3D/animation network, guarantee improvement after every model, or create a finished indie/AA game.

## Post-freeze recovery verification

The delivered app has **481 payload files / 266,475,781 bytes**, every payload hash verified, matching tested module hashes, exact prior offline configuration and dependency-only probe exit zero. Sanitized source checkpoint `20261031-asset-learning-v1` contains **286 source files / 4,122,290 bytes**; ZIP **1,226,525 bytes**, SHA-256 `dd506a919f4841dbedd911840d9b6b4e4349f1e38f0f8e53b1c29feaec8da4e4`. That exact ZIP independently extracts/restores exclusively from the local package cache, Release-builds, and passes another **721/721 host tests** (374/40/307, zero failed/skipped). Retained v28 game's 370 payloads all reverify unchanged. External `backups/20261031-asset-learning-v1/verification-receipt.json` binds these results. This note/receipt follows the frozen source ZIP; packaged README/manifests and prior deliveries are not rewritten.
