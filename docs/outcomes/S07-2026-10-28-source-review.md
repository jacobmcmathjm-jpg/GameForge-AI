# S07 — exact local editable-source review interface

Status: **Technical review-interface pass; S07 and human asset acceptance remain open.** User authorized continuing the next S07 task. This is an app-only change; previous games, source objects and installed Unity kit remain untouched.

## Implemented

- Read-only `InspectLocalBlenderAsset` and public preview reader verify the entire fixed-recipe immutable bundle. The review identity binds bundle, GLB, editable `.blend`, PNG preview and metrics SHA-256. The PNG display has explicit compressed-size/decoded-dimension limits and is loaded into a frozen in-memory bitmap, not executed or downloaded.
- Existing `ReviewGameArtAsset` live-human gate now supports exact local library sources without weakening old imported-asset review rules. Source proposals cannot contain decisions, attestation markers or arbitrary file paths. Only the agent's live approval binding adds an explicit approved/rejected decision; cancellation writes none. Blanket grants, sidebar accept shortcuts, persisted queues and restart recovery still cannot accept human attestations.
- Dedicated WPF dialog shows source preview, dimensions/coordinates, geometry/material/texture/rig measurements, provenance/source paths and all five hashes. Acceptance starts disabled behind an unchecked acknowledgement, is never the default Enter action, and requires an explicit click. Rejection is separate; closing/Escape records nothing. Preview-load failures remove their pending/live approval state.
- Source decisions are append-only, create-only records plus integrity digests outside immutable source objects. Execution re-verifies the bundle after the user decision. Changed source, altered/missing record/digest, exhausted review-history bounds and replayed call IDs fail closed. Later explicit rejection supersedes approval; source visual and rights decisions remain independent.
- Search reports source review status separately from target-project `visualReviewed`, `rightsReviewed` and `placementApproved`, which remain false. No automatic vault promotion, imported-project approval, placement, licence invention or stage completion.

## How to request a review

Search first: `Search local Blender assets for barrel.` Copy the exact 64-character bundle hash from its result. Then request independently:

- `Review local Blender asset <bundle_sha256> visually.`
- `Review local Blender asset <bundle_sha256> rights.`

ForgeCore first reads the exact packet, then asks for a live source-only decision. Source quality is not Unity runtime quality; rights require independent human judgement. No developer test grants that judgement for the user.

## Actual verification

- Release app/solution build and Windows publish: zero warnings/errors after correcting an internal-helper visibility issue. An initial test raw-string syntax error was fixed without weakening the gate. Final host suite: **391/391** (Core 178, Agent 40, Security/Tools 173), none failed/skipped; `work/s07-review-verification/final-sealed-host`. This adds 27 cases to the prior 364 tests. Intermediate 388/389 passes are retained, not relabelled as the final revision.
- Actual barrel read-only wrapper → policy → tool preparation for both visual/rights stops at **ApprovalRequired**. All **99 library source files unchanged**, source review records **0 before / 0 after**. Receipt: `work/s07-review-verification/live-source-packet.json`, SHA-256 `fff98769324d8b1476555fa93ab96e307e5b506da82f14dfa7ca5002b403c9c8`. No live dialog shown and no user decision executed by the helper.
- Final published app offscreen UI probe returned **0**: four visual/rights PNGs at 1000×720 and 760×560; default acceptance disabled/non-default; synthetic preview failure cancels the in-memory inbox. `work/s07-review-verification/ui-final/ui-probe.json`. The inspected full-size visual PNG is byte-identical between initial and final app renders: SHA-256 `c5f6223d03e1d5cef68f00af4bd00653ed37019c35eaa2f229af000e6958b581`. These are actual rendered owned WPF controls with real source imagery, **not live user interaction, DPI coverage or human art acceptance**.
- No Blender/Unity launch, provider/model call, package download, user database open, real approval/rejection record, source edit, new game build, GitHub upload or automation. Unity kit/bridge remain **0.16.49 / 0.17.18**; no new Unity test pass is claimed. Synthetic test databases/decisions remain confined to disposable fixtures.

## Delivery and remaining scope

Matching app-only target: `outputs/ForgeCore-Windows-x64-20261028-S07SourceReviewV1/ForgeCore.Desktop.exe`. Keep its entire folder and close any older ForgeCore copy before starting against the same store. Preview: `outputs/ForgeCore-S07-SourceReview-v1.png`. Final payload/offline-probe and sanitized source-restore results are recorded in `backups/20261028-s07-source-review-v1/verification-receipt.json`; the checkpoint excludes external assets/projects, live configuration, weights, stores/logs and full Git history.

Copied app: **481 payload files / 265,050,994 bytes**, every SHA-256/length/aggregate verified; manifest SHA-256 `6edab170cb2186d1fd202ebb39e4ac42e814fc9670225f68c8f8080ee9627776`. Copied offline `--verify-package` exited **0**. Cloud providers/online catalogue are disabled and no nonempty actual credential fields were found. The old v10 scene and kit-manifest hashes still match `c5ae2da5972d4b97adb8a28e40edaa9ecad097372967b96963483090c07865c7` / `0e155c6a920bcc91c4b936fb6c9e81643b0d831371f5491aef45ad3020f38895`.

Remaining: actual human source visual/rights decisions, provenance-aware approved-vault promotion, animation/material/rig compatibility, target-project Unity/resource/collision/runtime placement gates, and full persisted WPF/restart workflow acceptance. The next technical step is local-source promotion with independent target-game gates, then industrial prop recipes; this UI does not make S04 art or S07 complete.
