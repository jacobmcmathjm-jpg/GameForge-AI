[CmdletBinding()]
param([switch]$Standalone,[ValidatePattern('^[a-z0-9-]{1,30}$')][string]$ProbeAttempt)
$ErrorActionPreference='Stop'
$heroRepo=Split-Path $PSScriptRoot -Parent
$heroProject='D:/ForgeCore/Workspace/Games/ForgeCore'
$heroOutput=Join-Path $heroRepo 'outputs/ForgeCore-IndieHeroReview-20261116-V1'
if(Test-Path -LiteralPath $heroOutput){throw 'Fresh hero review destination required.'}
$heroCandidates=@(Get-ChildItem -LiteralPath (Join-Path $heroProject '.forgecore/unity/character-animation-evidence') -Filter '*.json' -File|Where-Object Length -LT 2MB|ForEach-Object {$e=Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json;if($e.scenePath -ceq 'Assets/Scenes/Indie/Industrial-Indie-Hero-v1.unity'){$e}})
if($heroCandidates.Count -ne 1){throw 'One exact hero evidence record required.'}
$heroEvidence=$heroCandidates[0]
if($heroEvidence.templateKitVersion -cne '0.16.97' -or $heroEvidence.visualReviewed -or $heroEvidence.stageComplete -or $heroEvidence.indieWeaponReport.profile -cne 'original_indie_rifle_v1' -or $heroEvidence.indieWeaponReport.sourceHandsChanged){throw 'Expected unaccepted shared rifle candidate, not hand anatomy acceptance.'}
$heroImage=Join-Path $heroProject 'Previews/Indie/Runtime-Hero-v2.png'
if($Standalone){
 $heroStem='standalone-indiehero-v1';if($ProbeAttempt){$heroStem+='-'+$ProbeAttempt}
 $heroChecksPath=Join-Path $heroRepo ('work/s08-s10-verification/'+$heroStem+'-checks.json')
 $heroChecks=Get-Content -LiteralPath $heroChecksPath -Raw|ConvertFrom-Json
 if($heroChecks.checks.Count -ne 4 -or @($heroChecks.checks|Where-Object {$_.exitCode -ne 0 -or $_.report.error -or $_.report.device -eq 'Null'}).Count){throw 'Four successful real-player checks required.'}
 $heroYard=@($heroChecks.checks|Where-Object mode -ceq 'courtyard');if($heroYard.Count -ne 1){throw 'One courtyard result required.'}
 $heroOwned=[IO.Path]::GetFullPath('D:/ForgeCore/Workspace/Games/ForgeCore/Builds/Indie-Hero-20261116/')
 if(-not ([IO.Path]::GetFullPath($heroYard[0].folder)).StartsWith($heroOwned,[StringComparison]::OrdinalIgnoreCase)){throw 'Probe folder escaped owned build.'}
 $heroImage=Join-Path $heroYard[0].folder 'courtyard-spawn.png'
 $heroBuild=Get-Content -LiteralPath ($heroChecks.executable+'.forgecore-build.json') -Raw|ConvertFrom-Json
 if($heroBuild.sceneSha256 -cne $heroEvidence.sceneSha256){throw 'Player/source scene differ.'}
 $heroProvenance=[ordered]@{schemaVersion=1;kind='actual_standalone_controlled_courtyard_probe';scenePath=$heroEvidence.scenePath;sceneSha256=$heroEvidence.sceneSha256;
  pngSha256=(Get-FileHash -LiteralPath $heroImage).Hash.ToLowerInvariant();checksReceiptSha256=(Get-FileHash -LiteralPath $heroChecksPath).Hash.ToLowerInvariant();buildReceiptSha256=(Get-FileHash -LiteralPath ($heroChecks.executable+'.forgecore-build.json')).Hash.ToLowerInvariant();
  qualification='Independent exported-player fixture capture with controlled placement; not a successful production Editor preview. Both Editor preview failures remain retained.'}
}else{
 $heroProvenance=Get-Content -LiteralPath ($heroImage+'.forgecore-preview.json') -Raw|ConvertFrom-Json
 if($heroProvenance.sceneSha256 -cne $heroEvidence.sceneSha256 -or $heroProvenance.pngSha256 -cne (Get-FileHash -LiteralPath $heroImage).Hash.ToLowerInvariant() -or $heroProvenance.width -ne 1280 -or $heroProvenance.height -ne 720){throw 'Actual camera capture identity differs.'}
}
$heroReport=@($heroEvidence.artifacts|Where-Object {$_.path.EndsWith('/indie-weapon-report.json')})
if($heroReport.Count -ne 1){throw 'Unique weapon report required.'}
$heroReportSource=Join-Path $heroProject $heroReport[0].path
if((Get-FileHash -LiteralPath $heroReportSource).Hash.ToLowerInvariant() -cne $heroReport[0].sha256){throw 'Weapon report changed.'}
New-Item -ItemType Directory -Path $heroOutput|Out-Null
Copy-Item -LiteralPath $heroImage -Destination (Join-Path $heroOutput 'runtime.png')
if($Standalone){[IO.File]::WriteAllText((Join-Path $heroOutput 'runtime-provenance.json'),($heroProvenance|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))}else{Copy-Item -LiteralPath ($heroImage+'.forgecore-preview.json') -Destination (Join-Path $heroOutput 'runtime-provenance.json')}
Copy-Item -LiteralPath $heroReportSource -Destination (Join-Path $heroOutput 'indie-weapon-report.json')
$heroReadme="Actual shared-rifle candidate, 16 November 2026. Runtime camera capture, not a concept image. New original receiver, skeletal ventilated handguard, stock, open reflex optic, round muzzle and independently animated curved magazine. Shared by player and three guards. Existing hand/guard anatomy, clips, grips/sights/muzzle, audio and controls are retained, NOT newly improved anatomy or motion capture. Human visual acceptance is still pending. Small existing courtyard; no larger map, new finished genre or original-freeze fix. Keep previous whole game/app folders. Full Editor regression remains unresolved: two full PlayMode timeouts, one legacy-scene EditMode timeout, and two production Editor preview timeouts. Four targeted rifle tests are separate coverage.`n"
if($Standalone){$heroReadme+="Image is an independent controlled standalone courtyard probe, not a production Editor camera pass. See runtime-provenance.json for exact build and probe identities.`n"}
[IO.File]::WriteAllText((Join-Path $heroOutput 'README.md'),$heroReadme,[Text.UTF8Encoding]::new($false))
$heroFiles=@(Get-ChildItem -LiteralPath $heroOutput -File|Sort-Object Name|ForEach-Object {[ordered]@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})
[long]$heroTotal=0;foreach($heroFile in $heroFiles){$heroTotal+=$heroFile.bytes}
$heroManifest=[ordered]@{schemaVersion=1;kind='ForgeCore-indie-weapon-review';humanArtAccepted=$false;sourceHandsChanged=$false;fileCount=$heroFiles.Count;totalBytes=$heroTotal;files=$heroFiles}
[IO.File]::WriteAllText((Join-Path $heroOutput 'review-manifest.json'),($heroManifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Test-ArtifactManifest.ps1') -ManifestPath (Join-Path $heroOutput 'review-manifest.json') -Kind HeroReview -ReceiptPath (Join-Path $heroRepo 'work/hero-review-verified.json')
