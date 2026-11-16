# S04–S06 local verification harness

This development helper invokes the real ForgeCore policy/tool/model components against the existing `D:/ForgeCore/Workspace/Games/ForgeCore` project. It is not the ForgeCore desktop app, a game executable or a persisted GUI/restart acceptance test. It does not bypass Unity licensing or activate accounts. Paths/version are explicit in source; no credentials or arbitrary scripts are accepted.

Build with .NET 10. Unity/Blender must be closed for serialized preparation. Run only the needed explicit mode:

- `--create`: generate/import the fresh original S04 source; refuses an occupied import.
- `--reuse`: hash-verify the immutable S04 bundle without Blender launch.
- `--install`: install/upgrade only the exact recognized bridge/kit with protected backups; compile Unity.
- `--animate`: create the fresh S04 isolated pose/animation review; refuses existing outputs.
- `--combat`: create the new v4 security combat lab, clips, extracted skin, weapon and reports.
- `--tests`: serialized Unity EditMode then PlayMode suites.
- `--play-tests`: PlayMode-only diagnostic rerun; it does not substitute for the final full `--tests` regression.
- `--preview`: five-second v4 runtime-camera capture to a fresh PNG.
- `--build`: fresh Windows development game folder.
- `--workflow`: actual bounded offline model → policy → tools prompt route using a fresh fixed name, seven actions and no fallback provider. Approvals are in the calling development session's explicit scope; no persistent app authorization is fabricated.
- `--inspect-art`: non-approving exact-source Unity resource inspection.
- `--self-test-failure`: deliberately return exit 1 with a bounded handled diagnostic, without launching Unity/Blender or touching the project/store.
- `--polish-combat`, `--polish-preview`, `--polish-build`: fresh v6 destinations for the separate weapon/contact refinement. Failed-contact diagnostic v5 is retained. These refuse occupied outputs; the earlier v4 modes remain unchanged. Use `--tests` after generating the matching latest fixture.
- `--motion-combat`, `--motion-preview`, `--motion-build`: fresh v8 destinations for the flat-lab enemy-motion pass, including stop/turn recovery. v7 remains the first tested diagnostic revision. Preview uses a 15-second wall-clock interval (not a guaranteed simulation duration); all prior destinations remain unchanged. Use `--tests` only after generating the matching latest fixture.
- `--visual-combat`, `--visual-preview`, `--visual-build`: fresh v10 destinations for the industrial finish/lighting/weapon pass; six bounded PNGs, one synthetic reflection cubemap, separate material variants and chamfer geometry are hash-bound in the receipt. v9 is retained diagnostic content after its emission material changed during Standard validation; v10 includes explicit flags and a byte-stability regression. Render first, inspect, then run full tests/build and recheck all receipt hashes. Earlier outputs remain unchanged.

The helper catches top-level failures; it never converts a failure to success. A failed/partial write remains retained for inspection; do not blindly retry it. Choose a fresh explicit destination after inspecting what happened, rather than removing a lock or overwriting an unknown artifact. Original source bundles, old scenes and prior deliveries stay intact.
