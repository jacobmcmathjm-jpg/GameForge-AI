[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$project='D:/ForgeCore/Workspace/Games/ForgeCore'
$output=Join-Path $repo 'outputs/ForgeCore-ConnectedInteriorReview-20261117-V8'
if(Test-Path -LiteralPath $output){throw 'Fresh interior review output required.'}
$records=@(Get-ChildItem (Join-Path $project '.forgecore/unity/character-animation-evidence') -Filter '*.json' -File|Where-Object Length -LT 2MB|ForEach-Object {$record=Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json;if($record.scenePath -ceq 'Assets/Scenes/Indie/Industrial-Connected-v8.unity'){$record}})
if($records.Count -ne 1){throw 'One exact successful v8 source record required.'}
$e=$records[0];$r=$e.connectedLevelReport
if($e.templateKitVersion -cne '0.16.107' -or $e.bridgeVersion -cne '0.17.23' -or $e.selectedLayout -cne 'industrial_connected_v1' -or $e.stageComplete -or $e.visualReviewed -or -not $r.worldSignsDepthTested -or -not $r.legacyRoofFittingsTrimmed -or $r.renderers -gt 300 -or -not $r.doorGatingVerified -or -not $r.stagedRouteReachable){throw 'Final connected identity/physics gate differs.'}
$full=Get-Content (Join-Path $repo 'work/s08-s10-verification/connected-v8-playmode.json') -Raw|ConvertFrom-Json
$edit=Get-Content (Join-Path $repo 'work/connected-interior-v8/edit-tests-v107.json') -Raw|ConvertFrom-Json
$audit=Get-Content (Join-Path $repo 'work/connected-interior-v8/preservation-v8-after.json') -Raw|ConvertFrom-Json
if($full.testScope -cne 'All' -or $full.result -cne 'Passed' -or $full.passed -ne 129 -or $full.failed -ne 0 -or $full.skipped -ne 0 -or -not $edit.Success -or $edit.Output -notmatch 'total=19, passed=19, failed=0, skipped=0' -or @($audit.files.PSObject.Properties).Count -ne 2364){throw 'Complete v8 Unity/preservation evidence required.'}
foreach($attempt in @('first','repeat')){
 $raw=Join-Path $repo ('work/s08-s10-verification/standalone-demo-connected-v8-'+$attempt+'.json')
 $m=Get-Content $raw -Raw|ConvertFrom-Json
 $t=Get-Content (Join-Path $repo ('work/s08-s10-verification/connected-v8-'+$attempt+'-validated.json')) -Raw|ConvertFrom-Json
 if($t.rawReceiptSha256 -cne (Get-FileHash $raw).Hash.ToLowerInvariant() -or -not $t.windowsMatchOriginal -or -not $t.measuredGameplayMaximumPassed -or $t.wholeSessionSmoothnessAccepted -or $m.report.layoutProfile -cne 'industrial_connected_v1' -or $m.report.passes.Count -ne 3 -or @($m.report.passes|Where-Object {-not $_.completed -or $_.visitedZones -ne 7 -or -not $_.gateChecks -or -not $_.checkpointRoundtrip -or $_.travelled -lt 110}).Count){throw 'Actual v8 mission/timing evidence required.'}
}
$sources=@{}
foreach($name in @('connected-level-report.json','connected-interior-report.json','connected-interior-review.png','connected-interior-review.json')){
 $a=@($e.artifacts|Where-Object {$_.path.EndsWith('/'+$name)})
 if($a.Count -ne 1){throw 'Unique owned interior artifact required.'}
 $path=Join-Path $project $a[0].path
 if((Get-FileHash $path).Hash.ToLowerInvariant() -cne $a[0].sha256){throw 'Interior artifact changed.'}
 $sources[$name]=$path
}
$detail=Get-Content $sources['connected-interior-report.json'] -Raw|ConvertFrom-Json
$comparison=Get-Content $sources['connected-interior-review.json'] -Raw|ConvertFrom-Json
if($detail.profile -cne 'industrial_connected_interior_v1' -or -not $detail.collisionUnchanged -or $detail.addedColliders -ne 0 -or $detail.additionalMaterialBatches -ne 1 -or $detail.screenWidth -ne 256 -or $detail.screenHeight -ne 128 -or $detail.screenTextureBytes -le 0 -or $detail.screenTextureBytes -gt 131072 -or $detail.partsBeforeMaterialBatch -gt 180 -or $detail.addedTriangles -gt 20000 -or $detail.visualReviewed -or $detail.stageComplete){throw 'Original visual-only interior contract differs.'}
if($comparison.scope -cne 'static_editor_comparison_left_before_right_after_not_runtime_animation' -or $comparison.baseline -cne 'fresh_same_output_before_optional_interior_dressing_not_previous_v6_binary' -or $comparison.width -ne 1536 -or $comparison.height -ne 1296 -or $comparison.viewCount -ne 3 -or -not $comparison.combatAndObjectivePosesUnchanged -or $comparison.protectedPoseTransforms -lt 10 -or $comparison.magentaPixels -ne 0 -or $comparison.changedPixels -lt 1000 -or $comparison.humanArtAccepted -or $comparison.stageComplete){throw 'Static comparison scope/quality/non-approval contract differs.'}
$image=Join-Path $project 'Previews/Indie/Runtime-Connected-v8.png'
$provenance=Get-Content ($image+'.forgecore-preview.json') -Raw|ConvertFrom-Json
if($provenance.sceneSha256 -cne $e.sceneSha256 -or $provenance.pngSha256 -cne (Get-FileHash $image).Hash.ToLowerInvariant()){throw 'Runtime camera provenance differs.'}
New-Item -ItemType Directory -Path $output|Out-Null
Copy-Item -LiteralPath $image -Destination (Join-Path $output 'runtime.png')
Copy-Item -LiteralPath ($image+'.forgecore-preview.json') -Destination (Join-Path $output 'runtime-provenance.json')
foreach($name in $sources.Keys){Copy-Item -LiteralPath $sources[$name] -Destination (Join-Path $output $name)}
[IO.File]::WriteAllText((Join-Path $output 'README.md'),'Original connected industrial interior candidate, not final art acceptance. runtime.png is the actual exterior runtime camera capture. connected-interior-review.png is a STATIC EDITOR environment comparison: BEFORE LEFT, AFTER RIGHT; warehouse entry, warehouse reverse, control-room workstations from top to bottom. Before is the fresh same-output layout prior to optional dressing, not the earlier v6 binary; no animation/gameplay proof is inferred from that sheet. Decorative original framing, cargo details and nine monitors reuse existing geometry/materials plus one bounded 256x128 screen pattern; no added collision or realtime lights. 129 graphics PlayMode / 19 Editor checks and six actual 1080p missions pass; audio/clips/controls, staged doors and the three-zone save/navigation profile remain. Art/balance, original freeze, stairs and additional completed genres remain open.',[Text.UTF8Encoding]::new($false))
$files=@(Get-ChildItem $output -File|Sort-Object Name|ForEach-Object {[ordered]@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}})
[long]$total=0;foreach($f in $files){$total+=$f.bytes}
$manifest=[ordered]@{schemaVersion=1;kind='ForgeCore-connected-interior-review';humanArtAccepted=$false;stageComplete=$false;fileCount=$files.Count;totalBytes=$total;files=$files}
[IO.File]::WriteAllText((Join-Path $output 'review-manifest.json'),($manifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Test-ArtifactManifest.ps1') -ManifestPath (Join-Path $output 'review-manifest.json') -Kind ConnectedInteriorReview -ReceiptPath (Join-Path $repo 'work/connected-interior-v8-review-verified.json')
