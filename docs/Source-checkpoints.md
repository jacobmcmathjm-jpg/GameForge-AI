# Source checkpoints, archived outcomes and GitHub recovery

The active plan is [OUTCOMES.md](../OUTCOMES.md). Each plan snapshot under `docs/archive/` is retained as a dated historical document; future work changes the active checklist and adds a new outcome note/snapshot, rather than silently editing the old plan.

## Scope and public-upload decision

On 26 October 2026 the user initially requested private GitHub storage, then selected the existing **public** `jacobmcmathjm-jpg/GameForge-AI` repository and explicitly authorized keeping it public. Only the sanitized ForgeCore source/checklist checkpoint is approved for this upload. Public means anyone may read or copy it; existing history is not made private by creating a new checkpoint.

Do not upload live machine configuration, credentials, runtime stores/logs, model weights, generated Windows players or external Unity/Blender/vault assets. Do not copy the local `.git` directory/history into a public snapshot. Original recipe **code** is part of source; exported asset **binaries** are not. The current local worktree and user data must remain intact.

## Make a new local checkpoint

Run from a supported PowerShell/.NET environment:

```powershell
./scripts/New-SourceCheckpoint.ps1 -Label 20261026-outcomes-v1
```

Choose a new label each time. Existing labels are refused. The script:

1. Selects bounded text source/build/docs/tests from known repository roots, including currently uncommitted/untracked eligible source, without staging, committing, resetting or switching the live repository.
2. Excludes caches, backup/output directories, live configuration and runtime/binary data; rejects unsafe paths/reparse points and known credential-shaped content. Candidate secret values are never printed. A known-pattern scan is not an exhaustive security guarantee; review new source types before uploading.
3. Normalizes snapshot text to UTF-8 without BOM and LF. The live files are unchanged.
4. Substitutes `appsettings.example.json` for the snapshot's `src/ForgeCore.Desktop/appsettings.json`. Cloud, online catalogue search and automatic model loading must be disabled in that example.
5. Writes every source file's length, SHA-256 and Git blob SHA-1 to `source-checkpoint-manifest.json`, then creates a ZIP and verifies every ZIP payload hash and entry count.

Output is under ignored `backups/<label>/`. A partial failure remains isolated there; use a new label after diagnosing it. No recursive deletion, overwrite or engine execution is performed by this script.

## Restore and verify

Extract the ZIP into a **new** directory, not an existing checkout. The JSON manifest lists all source payload files except itself. Check lengths/hashes before editing. The same manifest's Git blob identities can be compared with the uploaded GitHub commit's recursive tree.

The snapshot contains source, not prebuilt engines/runtimes or a complete asset/game data backup. Install/use a compatible .NET SDK and restore the pinned dependencies from an approved cache/feed. From the restored root, build and test `ForgeCore.slnx`; configure workspace/data paths and installed Unity/Blender paths locally before live engine work. Optional local language-model weights are external; never commit credentials to configuration.

The milestone acceptance snapshot has 259 host tests. If tests need external tools/data, report that prerequisite instead of treating a skipped live test as a pass. A clean source build does not recreate the external Unity project or recover excluded databases/assets.

## GitHub checkpoint protocol

- Inspect the exact owner/repository, visibility and current branch head immediately before writing. Never assume that a repository is private because a user initially requested privacy.
- For this user-approved replacement of the older GameForge tree, retain the prior `main` head in a separate dated `codex/` archive branch and as the new commit's parent. Publish the complete sanitized current source tree as a new versioned commit; do not force-push or delete history.
- Advance `main` only with the expected prior head and a normal fast-forward. If another writer moves the branch, stop and inspect rather than overwrite it.
- Verify the resulting commit/tree, every expected Git blob identity and the outcomes/archive documents. Save the remote commit URL and local ZIP hash in a separate local receipt; do not insert a commit's own SHA into its source manifest.
- Future stages should produce a new source checkpoint and outcome note, then make an explicit approved GitHub backup. Writing this plan does **not** configure an automatic daily upload or unattended development service.

The local manifest/ZIP is recoverable without GitHub. The remote source commit is a second source copy, not a backup of excluded runtime/asset data or a certificate of production quality.
