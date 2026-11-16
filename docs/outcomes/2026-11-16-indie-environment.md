# Indie direction — first reusable environment candidate

16 November 2026. The user changes the visual target from original N64-inspired to indie-style graphics and authorizes choosing the first implementation step. [The new art-direction record](../art-direction/INDIE_TARGET.md) keeps the retro profile as an option and makes the new target explicit.

## Implemented through ForgeCore

Kit **0.16.96**, bridge **0.17.22** unchanged. Fresh non-retro industrial mission generation now applies `grounded_indie_environment_v1` after the detailed surface stage and before hero/reflection baking. Existing detailed/default prompts use this route; `Create an indie industrial courtyard named indie-yard.` and the `indie-style` spelling are explicit offline planner aliases. This is still a bounded industrial-FPS recipe, not any-genre game generation.

The pass authors an original 1024 x 512 compressed/mipped panoramic sky, a warmer/coherent coating/concrete palette, brighter ambient fill, softer warm directional definition, smaller background signs, shallow cover fittings and exterior roof/warehouse services. It reuses the fuller-detail prop/weapon/guard presentation rather than the retro derivative. Two extra batched renderers add **4,488 triangles**; the owned sky allocation measures **1,421,665 bytes**. There are **zero new colliders or realtime lights**. Dressing beyond the existing collision envelope is backdrop art, not accessible rooms or a larger level.

The existing hero stage bakes its single 128px Custom box-projected reflection after the new sky/materials/lighting, without resetting the new lighting first. Explicit retro generation bypasses this pass and keeps its previous presentation path. The source upgrade is exact-hash protected: .95 is retained under `.forgecore/unity/template-kit-backups/02afeff2ad064302ba7ae64be2b8ca8b`. Unknown modified files are not overwritten.

New host evidence validates the profile, small resource bounds, collider/socket preservation and false human-acceptance flags. The production evidence includes the report and all four new assets plus metadata. Three actual Unity tests inspect the imported sky, its compression/mip/readability settings, the two collision-free/light-free batches and retained detailed combat rig. Existing tests and gates are not removed.

## Actual execution and visual assessment

The production SecurityPolicy/ToolExecutor path installs, compiles, generates a new scene, captures the actual runtime camera and exports a separate Windows player. It reuses the exact original industrial guard source rather than making an external generation/provider call. It does not create a review decision, open the normal app/store or touch normal saves.

Fresh scene: `Assets/Scenes/Indie/Industrial-Indie-v1.unity`. Original Retro64-v2 and modern scene files remain unchanged. All **280** new generated artifacts hash-verify; all **42** audio/motion files (34 waveforms and eight clips) exactly match the retained modern candidate. Before/after audit preserves all **594** original scene/asset/shader/review-record hashes. Source/rights/listening acceptance is not invented or revoked.

Actual two-second PlayMode camera capture: `Previews/Indie/Runtime-v1.png`, **1280 x 720**, SHA-256 **82f40822afbbcf97e84588762d2ffa9e06b2c1ad6c0f131af2dc3f2be5daacc8**. Sampled magenta is zero, luminance range .030–.978. This is an actual camera image, not a generated concept; it omits IMGUI overlays. Visual inspection shows warmer concrete/metal, clearer fittings/signage and richer background definition. It remains a modest prototype: blocky weapon/hands and character art still need dedicated work. This does **not** reach the earlier realistic reference image or certify final indie/AA graphics.

Separate clean app/game export label: **20261116-IndieEnvironmentV1**. Earlier working deliveries are retained. Safe offline app configuration is substituted when publishing. Clean exports precede any diagnostic player folders. Delivery/readback/recovery results are recorded separately after completion.

Primary copied deliveries are every-file verified: app **481 files / 267,601,117 bytes**, manifest **68d706a591c84e1d1e1287410179b775b627ace4b4a8ade4494b82a14dd0af44**; game **336 files / 241,941,789 bytes**, manifest **d3dae92f220342fbd24eb5c2d1671ec8e2280a9ae7e721da1485c2c9ed0b89e3**. Safe configuration SHA-256 remains **b84302a01a8e7bd6e808747ef549d5d1c9b52c0fe033fda32cb5f6950641266d**. The delivered app's dependency self-check and eleven-action offline planner probe pass before host/store/model initialization. Planner observations are synthetic, not an additional live generation run.

Review handoff is **ForgeCore-IndieReview-20261116-V2**: four payloads / 728,829 bytes, manifest **f20e38011e4686e2c63cfe5b23671c4a21f52f986b8c7fabdd9ffd320e0e48f8**. V1 is retained as a failed metadata attempt: PowerShell dictionary aggregation produced an invalid total; the strict verifier rejected it. V2 explicitly sums byte counts, then every-payload verification passes. No image/game/scene is changed to fix that metadata error.

## Validation boundaries

Actual Unity: **112 graphics-enabled PlayMode + 19 EditMode checks pass**, zero failures/skips. PlayMode XML SHA-256 **cf4b05c51ee84f3290874e8169307cc2380c32525ecedc042f98729489795b8e**. New profile/alias host cases and exported-player checks are tracked with the final receipts. No machine image or resource gate is human art acceptance.

Host regression: **946 pass** (592 Core, 40 Agent, 314 Security), zero failures/skips. This includes fifteen profile-contract and two ordinary offline indie-prompt cases. Four actual standalone courtyard/power-box/render/corpse modes pass: no sampled magenta, real player/guard firing markers, physical terminal state and unchanged independent corpse-contact limits.

Six full actual controller/projectile 1920 x 1080 Low/Medium/High missions pass with checkpoint roundtrip, three kills, fourteen shots and recorded footsteps, without direct-damage/teleport helpers. First run has three 30-second idle soaks; repeat has three 8-second soaks. These runs use the exact project export; independent every-file delivery copying is verified, but an additional portable-location mission launch is not claimed. Missions record zero hostile shots; the separate firing-marker check covers guard firing, not sustained enemy-combat balance.

Measured gameplay maxima: first **16.827 / 16.949 / 16.962 ms**; repeat **16.977 / 16.879 / 17.038 ms** at the explicit 60 FPS cap. All twelve gameplay/idle windows exactly match the retained sampler counts/maxima and pass the unchanged provisional measured gameplay gate. **Warmup maxima are 632.026 / 569.670 ms**, including startup/presentation/first-use work. New-game enqueue maxima are **24.017 / 5.526 ms**; the source behavior is unchanged, and prior smaller measurements are not a universal latency guarantee. No improvement is causally attributed to these graphics changes, no old hitch is erased, and caches/workstation activity are uncontrolled. Raw mission-receipt hashes: **fc12a2acaa3c4f8809cfae6f1c7b4a7af2671714ab519168dbdb3e7f19451770**, **4db9934e95f146c98ec448bc7f1b55e4171d08cfea49dac6c99f3b26785cf539**.

Scene SHA-256 **4f450993ffdd9bafa6e602f795ecdd3a7eb9da20592147436f2d714550f566bb**; installed kit manifest **22258b9272ab2f61c285fae01e8e108c564150ea66387eaf3a934f53a1a15a4c**; build level0 **95220e0c3d9f57490eab80214ce496763db5aaa356fd7c049695554a79106c6e**; actual generated runtime DLL **f53a25ba349de20ebf85636ab4c624b0e6f16008c9c589307f7655eab13d59c5**. Version/provenance metadata changes mean the .96 DLL is not claimed byte-identical to .95. Generic launcher hash alone is not build identity.

The earlier intermittent freeze and measured gameplay hitches are not claimed fixed. The first wall interval still includes the retained splash fade. Warmup stays two seconds; maximum gameplay gate stays provisionally 100 ms; whole-session smoothness remains unaccepted. A graphics increment must not hide timing failures behind p95 or turn old approvals into new art acceptance.

## Next quality work

Review the actual candidate image/game before accepting this direction. Then improve the visible weapon/hands and guard source art against the same environment, preserving grips/sights/muzzle/animation/audio and exact source/target reviews. Expand connected playable areas and a second genre in separate tested increments. This pass neither enlarges the 32 m navigation envelope nor implements another genre's full gameplay.

Technical reference for the chosen built-in sky material: [Unity panoramic skybox documentation](https://docs.unity3d.com/ja/current/Manual/VideoPanoramic-skybox.html). No pipeline conversion, new packages, downloaded assets or paid/API generation are required by this pass.

Sanitized source checkpoint: **20261116-indie-environment-v1**, with safe example configuration and no runtime state/raw logs/traces. Publish on **codex/indie-environment-20261116**, leaving the S01 timing branch, draft PR #3 and main untouched. Independent exact-ZIP offline restore/build and copied payload receipts are local outside the sealed source archive.
