# Existing-capability showcase runner

This is an owner-requested demonstration harness around the production ForgeCore model wrapper, security policy and tool executor. It does not add game/asset generation capabilities or replace live human art/rights review. The four production assemblies are checked against the latest shipped app when documenting this run. No inference, cloud generation, download or user database is used.

The workstation-specific runner uses installed Unity 6000.5.5f1 and Blender 5.2, the existing `D:\ForgeCore\Workspace\Games\ForgeCore` project and fresh `Showcase20261028` scenes/build/preview paths. It requires filesystem permission for that project and the local source library, plus a working normal-user Unity licence. Never bypass a licence or approval failure. Unity/Blender work is serialized and never terminates a user-owned editor.

## Invocation

Use `dotnet run --project scripts/Make-CapabilityShowcase/Make-CapabilityShowcase.csproj -c Release --no-restore -- <mode>` from the repository root, after restoring from the local cached feed. The default execution environment may not initialize Unity's licensing client; normal-profile execution requires its own permission.

- `--baseline`: fresh output directory only. Hash existing scene, asset, kit, map, package and source-library payloads before generation. Existing EditorBuildSettings/graphics/quality configuration may be changed by production builders with their protected backups; no old scene or asset content may change.
- `--template first_person_action`: three linked prototype levels, a runtime preview, deterministic checks of all three, a Windows development build and a bounded headless startup check.
- `--template platformer`, `--template top_down_rpg`, `--template racing`: one prototype each with preview, deterministic gameplay check, Windows build and startup check. `--other-templates` runs these three serially.
- `--asset-source barrel`, `--asset-source mannequin`, `--asset-source security`: execute the actual deterministic wrapper's creation step in installed Blender, retain verified editable source, GLB, maps, metrics and original source preview, and stop before import. `--asset-sources` runs all three serially. These are three fixed recipes, not arbitrary asset generation; Blender sources contain no animation clips.
- `--asset <kind>`: the fuller existing production creation/import/inspection workflow, plus a lab preview/build for characters. This mode is intentionally distinct from the source-only showcase and must not be claimed as run unless its receipts exist.
- `--existing-labs`: fresh previews and Windows builds of the existing v10 security-combat and v3 animation-lab scenes. These are explicitly reused scenes, not newly generated character art. Clip authoring is original parameterized Unity curves, not downloaded/AI-generated mocap.
- `--map`: fresh preview/build of the existing ForgeCore-generated Frontier Valley. Its builder owns a fixed generated directory, so this runner preserves/reuses the existing map instead of overwriting that directory.
- `--verify`: rehash all protected baseline files.

## Evidence and safety

Output is `outputs/ForgeCore-CapabilityShowcase-20261028`. Every attempted step has a create-only JSON receipt with exact call, policy result, tool result and timing. The tool authority is the user's current explicit showcase-generation request; it is not fabricated evidence of a persisted GUI approval. Human review/promotion/provider tool kinds are blocked. Failed receipts and staging are retained; existing outputs are not overwritten or blindly replayed.

The first restricted Unity attempt failed during licensing initialization with exit 198 before creating any scene. `--fps-normal-profile-retry` is a one-time, specifically bounded recovery: it requires that recorded failure and verifies all campaign targets are absent. It does not remove locks, edit licences or provide a general retry path.

Runtime PNGs are actual main-camera renders, not concept images. They do not include all immediate-mode HUD overlays. Headless startup checks are not human playthroughs, graphical performance acceptance or certification of a complete game. A top-down combat arena is not a full RPG; checkpoint driving is not realistic vehicle simulation; prototype platforming is not a designed commercial platformer. No stage is completed by this showcase.
