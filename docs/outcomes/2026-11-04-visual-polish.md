# Courtyard visual increment — 4 November 2026

Scope: resume shaped-prop work, then five bounded increments: material finish, visible weapon/hand presentation, lighting/reflections, guard finish, and independent validation/delivery. This is a repeatable original industrial-FPS template candidate, not a larger map, approved Blender-library placement, new character anatomy/animations or AA reference-image parity. Machine tests cannot replace a morning visual review.

## Reusable changes

- Ten original proxy renderers replaced by shared shaped meshes: round barrel, planked/braced crate, tapered concrete barrier, open welded fence and sealed corrugated cargo. Each family has three decreasing LODs; original solid-box colliders/transforms/layers remain unchanged. Fence openings remain conservatively solid for physics/projectiles, matching the retained collision convention, not individual-wire collision.
- Original procedural PBR finish: restrained surface relief, dielectric concrete/asphalt/fabric, sparse exposed-metal scratches and co-located darker/smoother wet asphalt regions. Opaque elliptical puddle overlays are disabled, not advertised as realistic water simulation.
- Original hero weapon: slimmer receiver, octagonal handguard, stock rails, cooling recesses, accessory rail and small hardware; original sight/muzzle/grip/magazine sockets retained. Gloves/sleeves receive improved material finish; no new hand anatomy/finger animation is claimed.
- One actual 128px baked, box-projected courtyard reflection with no realtime refresh and no extra realtime lights, plus bounded cool exterior/warm existing practical-light balance. Built-in Standard only, no pipeline conversion/package download.
- Guard cloth/glove finish and 12 small rig-bound trim/webbing parts per guard. Original source mesh and eight motion clips remain intact; this is not a new detailed character or motion-capture pass.
- Exact source review/promotion and target-review gates remain separate. No approval is invented for the five Blender-library candidates.

## Development diagnostics

The resumed 0.16.88 source builds; 824 host checks pass (471 Core, 40 Agent, 313 Security). The restricted Unity launch fails before compilation with licence/device access errors (exit 198); a separately approved normal desktop-context launch compiles and generates the original shaped-prop scene. No licensing settings or activation are altered.

The real shaped-prop report records 10 LOD groups, 22,644 highest-detail triangles, 25 highest-detail material draws, 5,682,672 measured texture bytes, zero new colliders/lights, unchanged collision and zero sampled magenta. Family LOD triangles: barrel 1424/560/240; crate 2200/1188/264; barrier 644/468/28; fence 2156/1144/704; cargo 4532/2156/1012. This is not artistic acceptance.

The first 99-test rendered run retains 90 passes / 9 failures: three new test setups used the wrong session lookup, while an already-loaded courtyard interfered with independent projectile/guard tests. The lookup is corrected and a test-only setup fixture disables/restores already-loaded demo roots in transient PlayMode. It does not save/modify scene assets or relax gameplay assertions. Failed logs/XML remain retained. Final revalidation is required below before release.

## Verification in progress

Final source is kit **0.16.90 / 69 files**, bridge **0.17.21** unchanged. Exact prior installations are backed up before upgrade: .87 → .88 at `98ce8c2b304a47aeb0078fe1b2028638`, .88 → .89 at `4ef5cbd8e05b44adad3242447392b262`; .89 is also preserved before the reflection-reference fix. Unknown source modifications are never overwritten.

**849 host checks pass again on .90**: 495 Core / 40 Agent / 314 Security, zero failures/skips (`visual-polish-v2-final` TRX). Runtime/Editor generated C#9 source statically compiles against installed Unity references (`visual-polish-v2-static.json`). The intermediate actual production V1 mission generation passes with 272 hash-bound artifacts; it remains diagnostic history, not the release game. This is production policy/tool execution from the authorised harness, not a normal live chat or a human art decision.

The real hero report measures 4 weapon presentations, 3 guards, 36 small rig-bound trim parts, 44 added renderers / 11,328 triangles and **17,046,966 texture bytes** for six 1K maps. Collision and sight/grip/muzzle sockets compare unchanged. The actual baked scene reflection is 128px. The surface report measures **25,570,185 bytes**, 3 decorative batches / 5,408 triangles, no new collider, unchanged collision and zero sampled magenta in its static views. Hero/lighting follows that comparison; only a later actual runtime capture represents the final combined image.

The next actual rendered run passes **101/102**. The remaining test catches a real reference-persistence defect: the baked scene probe's `bakedTexture` is absent after prefab instantiation. Version .90 independently bakes the scene cubemap, imports it as linear/mipped BC6H and explicitly assigns `customBakedTexture` with Custom mode. It still has **no realtime refresh**; only the persisted reference mechanism changes. A fresh V2 scene and complete revalidation are required, without overwriting the V1 diagnostics or weakening the non-null runtime probe check.

The corrected V2 generation passes at `Assets/Scenes/S13-PolishedCourtyard-v2.unity`, SHA-256 `f495c7f5ea927ed179483c41eabfdd0088d289de03ce68a33013a4ea0ba8974f`. Exact .89 backup: `03db858eea2c485fb3be401b53186b28`. All **102/102 graphics-enabled PlayMode checks pass**, zero failed/skipped, including the non-null instantiated Custom probe and linear/mipped BC6H import. XML SHA-256 `791d25e686c9274f4e27e94912f8a730e5bb2dda101a6336de30f32e0b30f337`. The 90/99 and 101/102 failed runs remain diagnostic history, not accepted release tests.

Readback verifies all **272 final generated artifacts**, both retained old scene hashes, the original guard, three frozen Blender recipes and all **34 identical regenerated audio clips**. The separate recorded-footstep audit also verifies old 194/170-artifact fixtures, eight original clips, 28 procedural cues, six original/processed recordings and the exact installed 69-file kit. Its machine `humanListeningAccepted=false` does not revoke the user's separately recorded acceptance of the footsteps.

## Windows candidate and actual image

The final actual play-mode camera capture passes sampled render sanity (no sampled magenta, luminance .016–.978), **1280×720 / 747,119 bytes**, SHA-256 `87d7ef3c1cf1f08991750415c82bf9ab99d8b3818ca1f965b0faebd9b040124a`. It does not include IMGUI HUD overlays and is not a concept image. Visual inspection shows better silhouettes, readable fence mesh, slimmer weapon detail and removal of flat puddle ellipses, but still a modest, visibly stylised/prototype environment—not realistic target-image parity. Existing overlapping legacy cover placements and conservative solid collision are not certified as final environmental art/collision.

Production Windows build and five-second hash-bound startup pass. Clean deliveries are created **before** running in-folder diagnostics: `outputs/ForgeCore-Windows-x64-20261104-VisualPolishV2` (**481 files / 266,794,166 bytes**, manifest `cb95edc5318744f8110c8cb109ac7262522c711b9fe2f95c307f386811ccd4f3`) and `outputs/ForgeCore-TestGame-20261104-VisualPolishV2` (**336 files / 240,725,763 bytes**, manifest `58529a9d3b8a530e2c3f13fbc788c52801aa2ef2a0943146228f13979c74ca14`). Every payload verifies. Keep whole folders and prior deliveries; no old game is replaced.

The delivered app's dependency check exits zero, and its actual deterministic planner completes all eleven actions with synthetic observations; six delivered modules match Release assemblies. Verified offline configuration remains `b84302a01a8e7bd6e808747ef549d5d1c9b52c0fe033fda32cb5f6950641266d`. These offline probes run before host/store initialization; no normal live app, database, provider/model or human review is opened. An earlier direct GUI-executable shell invocation did not provide reliable synchronous `$LASTEXITCODE`; the verified probe uses its owned process's actual exit code instead.

## Actual player verification

All **19/19 EditMode checks pass**, zero failed/skipped: total unique Unity checks are **121** (19 EditMode + 102 PlayMode), not the sum of repeated/failed diagnostic runs.

The exported player passes all four opt-in graphics modes on **Direct3D12**: courtyard, persistent/resettable physical power box, real player and guard firing markers, and independently measured original corpse-skin floor contact. Images contain zero sampled magenta. These fixture modes use controlled repositioning/damage where needed; they are separate from full mission playthrough evidence.

Three **1920×1080** Low/Medium/High actual-controller/projectile missions pass on the workstation's **RTX 3070**, each with checkpoint roundtrip, all objectives, three kills, 14 shots, ~54.8 m travel, 40 recorded footsteps and a separate **30-second idle soak**. Muted diagnostic saves are isolated; the actual mission probe uses **no teleport or direct damage**. At an explicit 60 FPS cap, p95 intervals are ~16.74 ms; warm Medium/High maxima are **16.98/16.97 ms**. First Low spikes to **92.05 ms**: the first-use hitch remains, and no hitch-free claim is made. The scripted missions record **zero hostile shots**; separate firing-marker checks cover guard firing, not sustained-combat pressure or balance certification. No GPU-timer/broad hardware/art acceptance is implied.

The exact **336 delivered game payloads** are separately copied/hash-verified into `work/delivered-visual-polish-v2-runtime` and execute three more complete **1080p** controller/projectile missions with eight-second idle periods. All objectives/checkpoints/three kills/14 shots/recorded-footstep gates pass. No project path is passed to the player; saves stay in the work copy. External file reads are **not traced**, so this is delivered-folder execution, not proof of zero external reads. Warm copy-run maxima are **17.10/17.02/16.98 ms**; this does not erase the earlier cold 92.05 ms hitch. Original delivery remains immutable. Total: six actual full missions and four separate fixture modes, not additional unique source-test cases.

## Recovery and handoff

Sanitized source checkpoint label: `20261104-visual-polish-v2`. It preserves known source/docs/scripts/test text, exact recipe bytes and an offline example configuration; no live database, weights, secrets or entire D-drive workspace copy. Independent exact-ZIP restore/build/host-test results and payload hashes are bound by external `backups/20261104-visual-polish-v2/verification-receipt.json`. That receipt and recovery logs live **outside the immutable ZIP**, avoiding a circular self-certification claim. No GitHub upload is performed.

Use the separate app/game folders above and `outputs/ForgeCore-VisualReview-20261104-V3` for the actual runtime image and technical comparisons/reports. Static sheets show **left baseline / right candidate**, at prop and surface stages **before final hero/lighting**; the runtime camera image shows the final combined result. Human review is still pending. The next meaningful quality step is reviewed/improved source art, physically suitable collision/placement, better weapon/hands and character anatomy/animation before scaling to a connected larger layout. This pass does not complete the realistic reference goal, unrestricted game generation or a full indie/AA title.

The first review export (V2) is retained as explicitly failed diagnostics: its images/reports are intact, but a PowerShell dictionary aggregation emitted null total bytes. The strict payload verifier rejected it. V3 corrects the aggregation and uses a fresh destination; it is the only review handoff. This metadata fix does not alter the .90 app, game or scene.
