[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$characterRepo=Split-Path $PSScriptRoot -Parent
$characterProject='D:/ForgeCore/Workspace/Games/ForgeCore'
$characterOutput=Join-Path $characterRepo 'outputs/ForgeCore-IndieCharacterReview-20261116-V1'
if(Test-Path -LiteralPath $characterOutput){throw 'Fresh character review destination required.'}
$records=@(Get-ChildItem (Join-Path $characterProject '.forgecore/unity/character-animation-evidence') -Filter '*.json' -File|Where-Object Length -LT 2MB|ForEach-Object {$e=Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json;if($e.scenePath -ceq 'Assets/Scenes/Indie/Industrial-Indie-Character-v1.unity'){$e}})
if($records.Count -ne 1){throw 'One exact character evidence record required.'}
$e=$records[0];$report=$e.indieCharacterReport
if($e.templateKitVersion -cne '0.16.99' -or $e.visualReviewed -or $e.stageComplete -or $report.profile -cne 'original_indie_character_v1' -or $report.individualFingerAnimation -or $report.visualReviewed -or $report.stageComplete){throw 'Expected unaccepted wrist-skinned character derivative.'}
$full=Get-Content (Join-Path $characterRepo 'work/s08-s10-verification/indie-character-v1-playmode.json') -Raw|ConvertFrom-Json
$edit=Get-Content (Join-Path $characterRepo 'work/indie-character/edit-tests-v99.json') -Raw|ConvertFrom-Json
$audit=Get-Content (Join-Path $characterRepo 'work/indie-character/preservation-after.json') -Raw|ConvertFrom-Json
if($full.testScope -cne 'All' -or $full.result -cne 'Passed' -or $full.passed -ne 120 -or $full.failed -ne 0 -or $full.skipped -ne 0 -or -not $edit.Success -or $edit.Output -notmatch 'total=19, passed=19, failed=0, skipped=0' -or @($audit.files.PSObject.Properties).Count -ne 1166){throw 'Complete character regression/preservation evidence required.'}
$timing=Get-Content (Join-Path $characterRepo 'work/s08-s10-verification/indie-character-v1-first-validated.json') -Raw|ConvertFrom-Json
if(-not $timing.windowsMatchOriginal -or -not $timing.measuredGameplayMaximumPassed -or $timing.wholeSessionSmoothnessAccepted -or $timing.freezeFixed){throw 'Original gameplay gates and qualified timing required.'}
$image=Join-Path $characterProject 'Previews/Indie/Runtime-Character-v1.png'
$provenance=Get-Content -LiteralPath ($image+'.forgecore-preview.json') -Raw|ConvertFrom-Json
if($provenance.sceneSha256 -cne $e.sceneSha256 -or $provenance.pngSha256 -cne (Get-FileHash -LiteralPath $image).Hash.ToLowerInvariant() -or $provenance.width -ne 1280 -or $provenance.height -ne 720){throw 'Actual runtime camera identity differs.'}
$item=@($e.artifacts|Where-Object {$_.path.EndsWith('/indie-character-report.json')})
if($item.Count -ne 1){throw 'Unique character report required.'}
$reportSource=Join-Path $characterProject $item[0].path
if((Get-FileHash -LiteralPath $reportSource).Hash.ToLowerInvariant() -cne $item[0].sha256){throw 'Generated character report changed.'}
New-Item -ItemType Directory -Path $characterOutput|Out-Null
Copy-Item -LiteralPath $image -Destination (Join-Path $characterOutput 'runtime.png')
Copy-Item -LiteralPath ($image+'.forgecore-preview.json') -Destination (Join-Path $characterOutput 'runtime-provenance.json')
Copy-Item -LiteralPath $reportSource -Destination (Join-Path $characterOutput 'indie-character-report.json')
$readme="Actual ForgeCore character-derivative candidate, not a concept image. Original code-authored shaped gloves with five pre-curled digit forms, cuff/knuckle and knee/boot details, sharing the hash-bound original Blender body/skeleton and existing textures. This is NOT a new detailed human head/body or articulated finger animation. Existing eight clips, controls, sights, muzzle flash and audio are retained. Four new mesh checks are included in 120 full PlayMode tests; 19 Editor tests and three full actual 1080p missions pass. Earlier compiler failure remains retained. Human visual acceptance and the original freeze remain open; no whole-session smoothness, larger map or new genre claim. Keep earlier game/app folders."
[IO.File]::WriteAllText((Join-Path $characterOutput 'README.md'),$readme,[Text.UTF8Encoding]::new($false))
$files=@(Get-ChildItem -LiteralPath $characterOutput -File|Sort-Object Name|ForEach-Object {[ordered]@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})
[long]$total=0;foreach($f in $files){$total+=$f.bytes}
$manifest=[ordered]@{schemaVersion=1;kind='ForgeCore-indie-character-review';humanArtAccepted=$false;individualFingerAnimation=$false;fileCount=$files.Count;totalBytes=$total;files=$files}
[IO.File]::WriteAllText((Join-Path $characterOutput 'review-manifest.json'),($manifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Test-ArtifactManifest.ps1') -ManifestPath (Join-Path $characterOutput 'review-manifest.json') -Kind CharacterReview -ReceiptPath (Join-Path $characterRepo 'work/character-review-verified.json')

