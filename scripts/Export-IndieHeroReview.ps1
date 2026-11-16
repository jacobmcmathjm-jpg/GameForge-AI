[CmdletBinding()]
param([switch]$Standalone,[ValidatePattern('^[a-z0-9-]{1,30}$')][string]$ProbeAttempt,[ValidateSet('V2','Quiet')][string]$Preview='V2',[ValidateSet('Historical','Quiet')][string]$Validation='Historical')
$ErrorActionPreference='Stop'
$heroRepo=Split-Path $PSScriptRoot -Parent
$heroProject='D:/ForgeCore/Workspace/Games/ForgeCore'
$heroOutput=Join-Path $heroRepo 'outputs/ForgeCore-IndieHeroReview-20261116-V1'
if(Test-Path -LiteralPath $heroOutput){throw 'Fresh hero review destination required.'}
$heroCandidates=@(Get-ChildItem -LiteralPath (Join-Path $heroProject '.forgecore/unity/character-animation-evidence') -Filter '*.json' -File|Where-Object Length -LT 2MB|ForEach-Object {$e=Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json;if($e.scenePath -ceq 'Assets/Scenes/Indie/Industrial-Indie-Hero-v1.unity'){$e}})
if($heroCandidates.Count -ne 1){throw 'One exact hero evidence record required.'}
$heroEvidence=$heroCandidates[0]
if($heroEvidence.templateKitVersion -cne '0.16.97' -or $heroEvidence.visualReviewed -or $heroEvidence.stageComplete -or $heroEvidence.indieWeaponReport.profile -cne 'original_indie_rifle_v1' -or $heroEvidence.indieWeaponReport.sourceHandsChanged){throw 'Expected unaccepted shared rifle candidate, not hand anatomy acceptance.'}
if($Validation -eq 'Quiet'){
 $full=Get-Content (Join-Path $heroRepo 'work/s08-s10-verification/indie-hero-quiet-v1-playmode.json') -Raw|ConvertFrom-Json
 $edit=Get-Content (Join-Path $heroRepo 'work/indie-hero/edit-tests-v97-quiet-v1.json') -Raw|ConvertFrom-Json
 $audit=Get-Content (Join-Path $heroRepo 'work/indie-hero/preservation-quiet-v1.json') -Raw|ConvertFrom-Json
 if($full.testScope -cne 'All' -or $full.result -cne 'Passed' -or $full.passed -ne 116 -or $full.failed -ne 0 -or $full.skipped -ne 0 -or -not $edit.Success -or $edit.Output -notmatch 'total=19, passed=19, failed=0, skipped=0' -or @($audit.files.PSObject.Properties).Count -ne 877){throw 'Quiet full-suite/Editor/preservation evidence incomplete.'}
 foreach($name in @('quiet-first','quiet-repeat')){$timing=Get-Content (Join-Path $heroRepo ('work/s08-s10-verification/indie-hero-v1-'+$name+'-validated.json')) -Raw|ConvertFrom-Json;if(-not $timing.windowsMatchOriginal -or -not $timing.measuredGameplayMaximumPassed -or $timing.wholeSessionSmoothnessAccepted){throw 'Quiet mission timing evidence incomplete or overclaimed.'}}
}
$heroImage=Join-Path $heroProject 'Previews/Indie/Runtime-Hero-v2.png'
if($Preview -eq 'Quiet'){$heroImage=Join-Path $heroProject 'Previews/Indie/Runtime-Hero-Quiet-v1.png'}
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
 $heroScenes=@($heroBuild.scenes)
 if($heroBuild.sceneCount -ne 1 -or $heroScenes.Count -ne 1 -or $heroScenes[0].path -cne $heroEvidence.scenePath -or $heroScenes[0].sha256 -cne $heroEvidence.sceneSha256){throw 'Player/source scene differ.'}
 $heroLevel=Join-Path $heroOwned 'ForgeCore-IndieHero_Data/level0';$heroDll=Join-Path $heroOwned 'ForgeCore-IndieHero_Data/Managed/ForgeCoreGameTemplates.Runtime.dll'
 if((Get-FileHash -LiteralPath $heroLevel).Hash.ToLowerInvariant() -cne $heroBuild.level0Sha256 -or (Get-FileHash -LiteralPath $heroDll).Hash.ToLowerInvariant() -cne '818a26fa55e17ea38ad5b2d2bb494b27671ec7e0d22771aa668da5457233d7f0'){throw 'Compiled player identity changed.'}
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
if($Validation -eq 'Quiet'){$heroReadme="Actual shared-rifle candidate, 16 November 2026; not a concept image. Quiet-workstation validation completes 116 full PlayMode checks, 19 Editor checks, six full 1080p missions, unchanged measured gameplay gates and preservation of 877 older hashes. Earlier timed-out attempts remain historical failures, not erased passes; resource contention was not causally proven. New shared rifle and curved reload magazine only: hand/guard anatomy, motion, audio and controls are retained. Human art acceptance, whole-session smoothness, larger maps and new finished genres are NOT claimed. Keep older whole app/game folders.`n"}
if($Standalone){$heroReadme+="Image is an independent controlled standalone courtyard probe, not a production Editor camera pass. See runtime-provenance.json for exact build and probe identities.`n"}
[IO.File]::WriteAllText((Join-Path $heroOutput 'README.md'),$heroReadme,[Text.UTF8Encoding]::new($false))
$heroFiles=@(Get-ChildItem -LiteralPath $heroOutput -File|Sort-Object Name|ForEach-Object {[ordered]@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})
[long]$heroTotal=0;foreach($heroFile in $heroFiles){$heroTotal+=$heroFile.bytes}
$heroManifest=[ordered]@{schemaVersion=1;kind='ForgeCore-indie-weapon-review';validationState=$Validation;humanArtAccepted=$false;sourceHandsChanged=$false;fileCount=$heroFiles.Count;totalBytes=$heroTotal;files=$heroFiles}
[IO.File]::WriteAllText((Join-Path $heroOutput 'review-manifest.json'),($heroManifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Test-ArtifactManifest.ps1') -ManifestPath (Join-Path $heroOutput 'review-manifest.json') -Kind HeroReview -ReceiptPath (Join-Path $heroRepo 'work/hero-review-verified.json')
