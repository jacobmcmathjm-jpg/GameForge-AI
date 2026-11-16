[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$connectedRepo=Split-Path $PSScriptRoot -Parent
$connectedProject='D:/ForgeCore/Workspace/Games/ForgeCore'
$output=Join-Path $connectedRepo 'outputs/ForgeCore-ConnectedReview-20261117-V6'
if(Test-Path -LiteralPath $output){throw 'Fresh connected review folder required.'}
$records=@(Get-ChildItem (Join-Path $connectedProject '.forgecore/unity/character-animation-evidence') -Filter '*.json' -File|Where-Object Length -LT 2MB|ForEach-Object {$e=Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json;if($e.scenePath -ceq 'Assets/Scenes/Indie/Industrial-Connected-v6.unity'){$e}})
if($records.Count -ne 1){throw 'One exact successful final connected record required.'}
$e=$records[0];$r=$e.connectedLevelReport
if($e.templateKitVersion -cne '0.16.105' -or $e.bridgeVersion -cne '0.17.23' -or $e.selectedLayout -cne 'industrial_connected_v1' -or $e.stageComplete -or $e.visualReviewed -or -not $r.worldSignsDepthTested -or -not $r.legacyRoofFittingsTrimmed -or $r.renderers -gt 300 -or -not $r.doorGatingVerified -or -not $r.stagedRouteReachable){throw 'Connected identity/gating evidence differs.'}
$full=Get-Content (Join-Path $connectedRepo 'work/s08-s10-verification/connected-v6-playmode.json') -Raw|ConvertFrom-Json
$edit=Get-Content (Join-Path $connectedRepo 'work/connected-polish/edit-tests-v105.json') -Raw|ConvertFrom-Json
$audit=Get-Content (Join-Path $connectedRepo 'work/connected-polish/preservation-v6-after.json') -Raw|ConvertFrom-Json
if($full.testScope -cne 'All' -or $full.result -cne 'Passed' -or $full.passed -ne 127 -or $full.failed -ne 0 -or $full.skipped -ne 0 -or -not $edit.Success -or $edit.Output -notmatch 'total=19, passed=19, failed=0, skipped=0' -or @($audit.files.PSObject.Properties).Count -ne 1756){throw 'Complete connected regression/preservation evidence required.'}
foreach($attempt in @('first','repeat')){
 $t=Get-Content (Join-Path $connectedRepo ('work/s08-s10-verification/connected-v6-'+$attempt+'-validated.json')) -Raw|ConvertFrom-Json
 $m=Get-Content (Join-Path $connectedRepo ('work/s08-s10-verification/standalone-demo-connected-v6-'+$attempt+'.json')) -Raw|ConvertFrom-Json
 if($m.report.passes.Count -ne 3 -or $t.rawReceiptSha256 -cne (Get-FileHash (Join-Path $connectedRepo ('work/s08-s10-verification/standalone-demo-connected-v6-'+$attempt+'.json'))).Hash.ToLowerInvariant()){throw 'Mission receipt cardinality/identity differs from validated timing.'}
 if(-not $t.windowsMatchOriginal -or -not $t.measuredGameplayMaximumPassed -or $t.wholeSessionSmoothnessAccepted -or $m.report.layoutProfile -cne 'industrial_connected_v1' -or $m.report.checkpointProfile -cne 'industrial_connected_v1' -or @($m.report.passes|Where-Object {-not $_.completed -or $_.visitedZones -ne 7 -or -not $_.gateChecks -or -not $_.checkpointRoundtrip -or $_.travelled -lt 110}).Count -ne 0){throw 'Actual connected three-zone mission/gate/checkpoint evidence required.'}
}
$image=Join-Path $connectedProject 'Previews/Indie/Runtime-Connected-v6.png'
$p=Get-Content -LiteralPath ($image+'.forgecore-preview.json') -Raw|ConvertFrom-Json
if($p.sceneSha256 -cne $e.sceneSha256 -or $p.pngSha256 -cne (Get-FileHash $image).Hash.ToLowerInvariant()){throw 'Final runtime image identity differs.'}
$item=@($e.artifacts|Where-Object {$_.path.EndsWith('/connected-level-report.json')});if($item.Count -ne 1){throw 'Unique final layout report required.'}
$report=Join-Path $connectedProject $item[0].path
if((Get-FileHash $report).Hash.ToLowerInvariant() -cne $item[0].sha256){throw 'Final layout report changed.'}
New-Item -ItemType Directory -Path $output|Out-Null
Copy-Item -LiteralPath $image -Destination (Join-Path $output 'runtime.png')
Copy-Item -LiteralPath ($image+'.forgecore-preview.json') -Destination (Join-Path $output 'runtime-provenance.json')
Copy-Item -LiteralPath $report -Destination (Join-Path $output 'connected-level-report.json')
[IO.File]::WriteAllText((Join-Path $output 'README.md'),"Actual final connected-level runtime exterior capture, not concept art or proof of finished interior presentation. Three accessible single-floor zones: service yard, warehouse, control room. Two stage-controlled doors, 64m bounded navigation envelope (not an entirely walkable square), isolated save profile and preserved provisional combat assets. Final route about 118m, 299 total renderers and 49 colliders. 127 PlayMode / 19 Editor checks and six actual 1080p missions pass, including far-warehouse save/load and all-zone visits. Earlier v1 renderer and v2 metadata failures remain retained. Parent art comparison sheets show the original courtyard prepass, not this full layout. Human art/balance, interior polish and original freeze remain open; no stairs, open-world or new-genre claim.",[Text.UTF8Encoding]::new($false))
$files=@(Get-ChildItem $output -File|Sort-Object Name|ForEach-Object {[ordered]@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}})
[long]$total=0;foreach($f in $files){$total+=$f.bytes}
$manifest=[ordered]@{schemaVersion=1;kind='ForgeCore-connected-level-review';humanArtAccepted=$false;stageComplete=$false;fileCount=$files.Count;totalBytes=$total;files=$files}
[IO.File]::WriteAllText((Join-Path $output 'review-manifest.json'),($manifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Test-ArtifactManifest.ps1') -ManifestPath (Join-Path $output 'review-manifest.json') -Kind ConnectedReview -ReceiptPath (Join-Path $connectedRepo 'work/connected-polish-review-verified.json')
