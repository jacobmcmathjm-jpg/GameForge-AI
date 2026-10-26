# ForgeCore Windows delivery

Two separate folders are delivered. Keep each entire folder intact, not just its EXE.

## ForgeCore app

Run `ForgeCore.Desktop.exe` in the ForgeCore-Windows-x64 folder. Its .NET and WPF runtimes are included. This is not an installer, code-signed release, or a standalone Unity replacement.

The current appsettings.json is configured for this workstation: the workspace and local model live under D:\ForgeCore. Unity Editor, installed Blender 5.2 for the local barrel recipe, a compatible graphics driver, local model weights, existing project/data, and any opted-in provider credentials remain external. They are not copied into this package. Starting normally uses the configured persistent app data and may resume previously authorized work. Do not start a second ForgeCore copy against the same store. Unity project operations must remain serialized; close the interactive Editor before a batch operation. Close Blender before running the local recipe. The barrel route needs no API key, paid service, model inference or downloads; cloud providers and online catalogue search are disabled in this workstation delivery.

For an offline, non-mutating dependency probe, run `ForgeCore.Desktop.exe --verify-package` and inspect its exit code. Zero means the self-contained runtime, core modules, backend payload files, native SQLite dependency, config structure, bundled Blender recipe and production-plan construction passed. This mode does not open the user's database, load model weights, start Unity/Blender, or contact providers. Nonzero codes: 1 dependency/parse exception; 2 missing runtime payload; 3 missing ForgeCore config section; 4 not self-contained; 5 missing local inference payload; 6 missing bundled Blender recipe. It does not certify the full GUI, GPU inference, credentials, installed editors, or game quality.

## Playable game

In the ForgeCore-TestGame folder, run the game executable alongside UnityPlayer.dll and its matching `_Data` folder (not UnityCrashHandler64.exe). ForgeCore and Unity Editor do not need to be running to play this build. Keep D3D12, MonoBleedingEdge, and all other shipped support files alongside it. This is a development test build, not a polished release.

These builds use procedural maps/models, basic UI and synthesized feedback. IndustrialYardV1 adds modular scenery and materials; OfflineCombatV1 adds joint walk clips, weapon fire/reload poses, armed enemies and visible swept projectiles. BlenderBarrelV1 additionally places one original Blender prop with its own PBR materials and box collision; most of the game is still procedural. These are not finished Blender characters or indie/AA visual acceptance. Follow the on-screen controls; full settings/accessibility menus remain unfinished. Do not rename the executable independently of its data folder.

`delivery-manifest.json` lists SHA-256 and size for every shipped payload file except the manifest itself. The game's original `.forgecore-build.json` provenance is retained. A manifest protects integrity, not trust or visual quality. Test logs or save data created after launch are not part of the original payload.

## Rebuild a delivery

From the repository, publish `src/ForgeCore.Desktop/ForgeCore.Desktop.csproj` with `-p:PublishProfile=WindowsFolder -o <new-staging-folder>` using the .NET 10 SDK. Runtime/package restore may require network access on a fresh machine.

Then run `scripts/New-WindowsDelivery.ps1` with the published app directory, an existing verified Unity build directory, its exact executable filename, and a new label. It refuses existing destinations, copies only Windows x64 local-inference natives into the app package, preserves the complete game directory, and writes file manifests. Review appsettings.json for machine-specific paths before distributing to another computer. Never add credentials to this file.

Assets, music, voice and plugins require appropriate redistribution rights. A delivered test build is not evidence that those rights or a commercial release checklist have been cleared.
