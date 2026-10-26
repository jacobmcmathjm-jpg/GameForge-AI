# Seele 3D Setup for ForgeCore

ForgeCore v0.12 can ask Seele to generate one original 3D asset, wait for the asynchronous job, and quarantine a returned GLB for inspection. It does not automatically buy credits, subscribe, upload your Unity project, or import generated output into `Assets`.

## One-time account setup

1. Sign in at the Seele developer console and create an API key for your own account.
2. In Windows, open **Edit environment variables for your account** from Start.
3. Under **User variables**, choose **New**.
4. Set the name to `SEELE_API_KEY` and the value to your key.
5. Close every running ForgeCore window and restart ForgeCore so the new process can read the variable.

Do not paste the key into chat, `appsettings.json`, source control, screenshots, prompts, or Unity. The application configuration names only the environment variable; it does not contain the secret.

This workstation's desktop configuration already has `EnableSeeleGeneration` enabled. Without the environment variable, ForgeCore continues to launch and displays `SEELE KEY NEEDED`; generation requests fail closed.

## First supervised request

Open the ForgeCore desktop and send a focused request such as:

> Generate an original realistic weathered timber ranger watchtower for the Games/ForgeCore Unity project. Use efficient game-ready topology, a single self-contained GLB, grounded proportions, no logos, no text, and neutral PBR materials. Name it frontier-watchtower.

ForgeCore will show a permission window with the exact provider, project, model, asset name, and prompt. Approval authorizes only that request. `AUTHORIZE SESSION` never auto-approves cloud generation.

The job may take several minutes. Stop or Emergency Stop cancels local waiting and downloading. A remote service may continue a job already submitted; check your Seele account if you cancel after submission.

## Quarantine and Unity import

Returned passive files are limited to GLB and bounded preview-image types. Archives, packages, scripts, projects, and executables are rejected. Accepted output is written under:

`<project>/.forgecore/staging/<asset-name>/`

ForgeCore removes signed query parameters before recording provenance, calculates SHA-256 while downloading, and structurally checks the GLB immediately. It marks generated output `SEELE_GENERATED_REVIEW_REQUIRED`; that is a review status, not a licence guarantee.

Use a separate `inspect_staged_asset` action and review geometry, materials, scale, visual quality, originality, and usage rights. Only then approve `import_staged_asset` to copy the exact hash-bound file into the Unity project. Keep a human art and performance pass in the weekend demo plan.

## Current limits

- The fixed provider endpoint is Seele API v2; arbitrary API hosts are not configurable.
- The configured timeout is 30 minutes and polling interval is 15 seconds.
- Some Seele plans may expose only a preview URL rather than downloadable artifacts. ForgeCore reports that honestly and stages nothing.
- `Seele02-pro` availability and credit cost depend on the user's Seele plan; start with `Seele02-flash`.
- No generated asset has been live-tested on this workstation until a valid user-owned API key and eligible Seele plan are present.
