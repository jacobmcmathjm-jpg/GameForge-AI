# Offline first-person level workflow

The default workstation development configuration now disables `EnableMeshyGeneration`, `EnableSeeleGeneration` and `EnableOnlineCatalogSearch`. No provider API key is required for the deterministic first-person production workflow. Optional cloud adapters remain in the source but are disabled by policy in this configuration; this does not make them free or locally implemented.

## What the app does

An explicit request such as `Create a first person shooter game with 1 level` routes to ForgeCore's persisted production service, rather than asking the local language model to improvise every tool call. The service creates unique scene/build destinations, searches the reviewed offline asset vault, prepares the reviewed runtime, creates the level, tests it, captures a preview, builds Windows, and checks the exact player.

New first-person scenes use the industrial-yard environment and original rigid-part joint rigs. Their runtime provides walk clips with hip/knee articulation, an animated weapon kick and reload/magazine movement, visible swept projectiles, enemy guns, ammunition/reload timing and line-of-sight cover checks. First-person walking currently uses camera-attached weapon bob; a complete visible player body and finished skinned character motion are not claimed. Other genres and earlier scenes without the new components retain their previous runtime path.

The new rendered-profile gate checks that the actual standalone game exercised player projectile creation, enemy firing and player reload animation, in addition to the existing movement/action/performance checks. Unit/runtime tests remain distinct from a human playtest or a complete live playthrough.

## Running it

1. Use the newly delivered ForgeCore app matching the installed template-kit version. Do not run two copies against the same configured database.
2. Keep the configured Unity Editor installed and close its interactive project window before starting a batch production run.
3. Authorize the intended project scope in ForgeCore. The existing timed/action-bounded Trust Project grant covers eligible typed Unity work, not unlimited computer access. Revocation, expiry and Emergency Stop still stop dependent work.
4. Submit the first-person creation request. Keep ForgeCore running while its queue executes. Check its task/approval panel if it pauses or fails; do not treat a stopped queue as a completed game.
5. Play the generated executable with its entire Unity data/support folder intact. Review the preview, runtime results and remaining requirements before accepting a new version.

The self-contained app includes .NET/WPF, not the Unity Editor or model weights. The local LLamaSharp model is still an external configured file. The deterministic production service itself does not need a cloud model call. A machine must have the configured tools, correct paths, adequate hardware/storage and any required engine activation/licence. Unity may still perform its own activation/package/telemetry network operations; ForgeCore's disabled provider flags are not an OS firewall or a guarantee that every vendor process is network-silent.

## Explicit limitations

- This creates a template-driven first-person prototype, not arbitrary finished games of any genre/style.
- The original joint clips are not Blender-generated, skinned/motion-captured or commercially polished animation sets. Blender creation/export/import orchestration remains separate unfinished work.
- No reviewed asset vault entries are silently created from procedural stand-ins. Human visual/rights attestations remain separate, and existing missing art/system requirements stay visible in the blueprint/report.
- Audio remains synthesized feedback, not a finished sound/music/voice pipeline. Enemy path planning, combat balance, accessibility, settings, art direction and longer playtests need further work.
- No API bills does not mean no hardware/electricity/licensing/storage costs. Developing ForgeCore with a coding assistant is separate from running ForgeCore's local workflow.
