# Unity setup for ForgeCore v0.11

ForgeCore does not download Unity, Unity Hub, platform modules, packages, licences, or credentials. Install and activate Unity manually as a normal Windows user before enabling Editor actions.

## Required local setup

1. Install a supported Unity 6 Editor through Unity Hub. For Windows builds, include the Windows Build Support module.
2. Open the Editor manually once, complete licence activation, and confirm it can create/open a local project.
3. Close that project. Unity batch mode cannot open the same project while another Editor instance has it open.
4. Set `ForgeCore:UnityEditorPath` in `src/ForgeCore.Desktop/appsettings.json` to the exact local executable, for example:

```json
"UnityEditorPath": "C:\\Program Files\\Unity\\Hub\\Editor\\6000.0.65f1\\Editor\\Unity.exe",
"UnityEditorVersion": "6000.0.65f1",
"UnityTimeoutSeconds": 600,
"EnableUnityProjectPackages": false
```

The configured file must be a fully qualified local path ending in `Unity.exe`, and `UnityEditorVersion` must exactly match `ProjectSettings/ProjectVersion.txt`. This prevents ForgeCore from opening a project through an unintended Editor upgrade path. UNC paths, missing files, arbitrary executables, version mismatches, and administrator operation are denied. Keep the path/version empty to leave all Unity execution disabled while the rest of ForgeCore continues to work.

## First supervised workflow

1. Ask ForgeCore to scaffold a Unity project with the exact installed Editor version, or point it at an existing workspace project.
2. Approve `install_unity_bridge` if the project did not come from the current scaffold. ForgeCore writes only `Assets/ForgeCore/Editor/ForgeCoreBridge.cs` and its version manifest and never overwrites a mismatch.
3. Approve a `capabilities` action and confirm the reported bridge and Editor versions.
4. Create a no-overwrite `basic_2d` or `basic_3d` scene. ForgeCore snapshots existing Build Settings and registers the new scene as enabled, then adds primitives/components one approved action at a time.
5. For Frontier Valley, approve `scaffold_unity_map_demo`, compile, then approve one `create_map_demo` action with a new `Assets/.../*.unity` path and bounded seed. The map builder never overwrites its scene or generated-assets directory.
6. Compile before testing. `run_tests` requires Unity Test Framework to already be available in the project/cache. With `EnableUnityProjectPackages=false`, ForgeCore launches with `-noUpm`. With it enabled, all direct packages must already be pinned and locally cached; manifest and lock-file bytes are protected and package acquisition remains unauthorized.
7. Build only after compilation/tests. Windows output must be a new `Builds/.../*.exe` path; existing executable or `_Data` output is refused.

## Operational limits

- Project C#, Editor scripts, asset importers, tests, and build callbacks execute as your normal Windows user. Review unfamiliar code before approval.
- ForgeCore does not supply Unity credentials or automate licence activation.
- ForgeCore does not install packages or enable the API updater. A project that needs package acquisition or API migration must be handled manually and reviewed.
- Play Mode is limited to 30 seconds per action. Every Editor process has a configured hard limit and is killed as a process tree on cancellation/timeout.
- Scene mutations receive a local pre-change snapshot. ForgeCore does not automatically restore it because restoration would itself be a mutation requiring review.
- Logs remain local under `<project>/.forgecore/unity/logs`; one-use request and result files are deleted after processing.

## Verified workstation configuration

The current workstation uses Unity `6000.5.5f1` at `C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe`. The real `D:\ForgeCore\Workspace\Games\ForgeCore` project completed map-kit installation, compilation, seeded Frontier Valley generation, bounded Play Mode, Windows build, and standalone Direct3D 11 startup. This path is configured in the desktop `appsettings.json`; change both values together if Unity is upgraded or moved.

## Optional Unity AI and MCP collaboration

Use this only for a project you are willing to link to Unity Cloud. ForgeCore's deterministic bridge remains the primary foundation/build path; Unity AI is a specialist polish layer.

1. Open the intended workspace project in Unity 6 as a normal user and link that project to the correct Unity Cloud organization.
2. Use Unity's AI button/package workflow to install the official AI Assistant package. Confirm `Packages/manifest.json` contains `com.unity.ai.assistant`. ForgeCore will not install or modify this package.
3. In Unity, open **Edit > Project Settings > AI > Unity MCP** and start the MCP server. On Windows the official relay is normally `%USERPROFILE%\.unity\relay\relay_win.exe`.
4. Keep that exact project open. Set the following values in ForgeCore configuration and restart ForgeCore:

```json
"EnableUnityMcp": true,
"UnityMcpRelayPath": "C:\\Users\\YOURNAME\\.unity\\relay\\relay_win.exe",
"AllowedUnityMcpTools": [],
"UnityMcpTimeoutSeconds": 120,
"MaxUnityMcpArgumentsCharacters": 16000,
"MaxUnityMcpResponseCharacters": 100000,
"UnityProjectPath": "Games/ForgeCore"
```

5. Ask ForgeCore to discover Unity MCP tools for the project. Approve the ForgeCore `Execute` dialog, then approve the **Pending Connection** inside the intended Unity Editor. Discovery invokes no Unity tool.
6. Review the returned tool names and schemas. Add only the exact tools you want ForgeCore to use to `AllowedUnityMcpTools`, then restart ForgeCore. Do not add a wildcard.
7. Ask for a specific polish operation. ForgeCore shows the exact tool name and compact JSON arguments before every call. Unity's server validates them again and may show its own UI.

Recommended split:

| ForgeCore owns | Unity AI may polish |
|---|---|
| Project scaffold, source control-ready files, gameplay C#, scenes, deterministic changes | Visual ideation and Editor-aware refinements |
| Workspace asset provenance and safe import | Textures, materials, sprites, audio, animation, and 3D generations offered by Unity |
| Compile, tests, repair loop, Build Settings, Windows executable | Suggestions informed by the live Unity project |
| Permissions, cancellation, action limits, local audit trail | Unity account, Cloud project, service terms, and AI credit accounting |

The relay connects to the Editor session that the user approves, so approve the pending connection only in the intended project. Unity MCP tool descriptions are untrusted metadata, not permission grants. A discovered tool remains unusable until its exact name is added to ForgeCore's allow-list and a separate call is approved. ForgeCore never copies Unity login tokens, generated service credentials, or browser sessions into configuration, logs, or SQLite.

On the verified workstation, the local `Games/ForgeCore` project was linked to the existing ForgeCore Unity Cloud project and the secured `Unity_AssetGeneration_GetModels` call completed through the official relay. Unity returned an empty model list, so the bridge and approval flow are operational but Unity asset generation remains unavailable for that account/project until Unity supplies eligible models. ForgeCore does not bypass that service-side restriction.
