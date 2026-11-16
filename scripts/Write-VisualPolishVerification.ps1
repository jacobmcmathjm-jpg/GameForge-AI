[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$proofRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$proofLabel='20261104-visual-polish-v2'
$proofRoot=Join-Path $proofRepo ('backups/'+$proofLabel)
$proofWork=Join-Path $proofRepo 'work/s08-s10-verification'
$proofRestore=Join-Path $proofRepo ('work/three-hour-assets/restore-'+$proofLabel+'-final')
$proofOut=Join-Path $proofRoot 'verification-receipt.json'
$proofCopies=Join-Path $proofRoot 'verification-evidence'
foreach($proofPath in @($proofOut,$proofCopies)){if(Test-Path -LiteralPath $proofPath){throw 'Final verification evidence exists; no overwrite.'}}
function Read-VisualJson([string]$Name){Get-Content -LiteralPath (Join-Path $proofWork $Name) -Raw|ConvertFrom-Json}
$proofApp=Read-VisualJson 'visual-polish-v2-app-close.json'
$proofGame=Read-VisualJson 'visual-polish-v2-game-close.json'
$proofReview=Read-VisualJson 'visual-polish-v2-review-payload.json'
$proofSource=Read-VisualJson 'visual-polish-v2-source-payload.json'
foreach($proofPayload in @($proofApp,$proofGame,$proofReview,$proofSource)){if(-not $proofPayload.everyPayloadVerified){throw 'A required manifest has not verified.'}}
$proofPreserved=Read-VisualJson 'visual-polish-v2-preservation-close.json'
if($proofPreserved.candidateArtifactsVerified -ne 272 -or $proofPreserved.audioClipsMatched -ne 34 -or -not $proofPreserved.originalGuardVerified){throw 'Preservation proof incomplete.'}
$proofUnity=Read-VisualJson 'visual-polish-v2-playmode.json'
if($proofUnity.exitCode -ne 0 -or $proofUnity.passed -ne 102 -or $proofUnity.failed -ne 0 -or $proofUnity.skipped -ne 0){throw 'Actual PlayMode proof incomplete.'}
$proofEdit=Read-VisualJson 'visual-polish-v2-edit-tests.json'
if(-not $proofEdit.Success -or $proofEdit.Output -notmatch 'total=19, passed=19, failed=0, skipped=0'){throw 'Actual EditMode proof incomplete.'}
$proofPackage=Read-VisualJson 'visual-polish-v2-package.json'
if($proofPackage.dependencyExitCode -ne 0 -or $proofPackage.plannerActions -ne 11 -or $proofPackage.databaseOpened){throw 'Packaged offline proof incomplete.'}
$proofPlayer=Read-VisualJson 'standalone-demo-polished-v2-full-1080p.json'
$proofPortable=Read-VisualJson 'visual-polish-v2-delivered-game.json'
$proofFixtures=Read-VisualJson 'standalone-polishedyard-v2-checks.json'
if($proofFixtures.checks.Count -ne 4 -or @($proofFixtures.checks|Where-Object {$_.exitCode -ne 0 -or $_.report.error -or $_.report.device -eq 'Null'}).Count){throw 'Standalone fixture proof incomplete.'}
$proofFiring=$proofFixtures.checks|Where-Object mode -CEQ render
if(-not $proofFiring.report.actualShotMarker -or -not $proofFiring.report.actualGuardShotMarker){throw 'Actual firing markers missing.'}
foreach($proofRun in @($proofPlayer,$proofPortable)){
    if($proofRun.exitCode -ne 0 -or $proofRun.report.error -or $proofRun.report.passes.Count -ne 3 -or $proofRun.report.width -ne 1920 -or $proofRun.report.height -ne 1080 -or $proofRun.report.device -eq 'Null'){throw 'Actual player proof incomplete.'}
    foreach($proofPass in $proofRun.report.passes){if(-not $proofPass.completed -or $proofPass.kills -ne 3 -or -not $proofPass.checkpointRoundtrip){throw 'An actual mission did not complete.'}}
}
$proofRecovery=Get-Content (Join-Path $proofRestore 'restore-verification.json') -Raw|ConvertFrom-Json
$proofZip=Join-Path $proofRoot ('ForgeCore-source-'+$proofLabel+'.zip')
if(-not $proofRecovery.releaseBuildPassed -or -not $proofRecovery.hostTestsRequested -or -not $proofRecovery.exclusiveLocalPackageSource -or
    $proofRecovery.archiveSha256 -cne (Get-FileHash $proofZip).Hash.ToLowerInvariant()){throw 'Independent exact-ZIP recovery proof incomplete.'}
$proofCounts=[ordered]@{}
foreach($proofSuite in @('core','agent','security')){
    $proofXml=[xml](Get-Content (Join-Path $proofRestore ($proofSuite+'.trx')) -Raw)
    $proofCounter=$proofXml.SelectSingleNode('//*[local-name()="Counters"]')
    $proofCount=[int]$proofCounter.GetAttribute('passed')
    if([int]$proofCounter.GetAttribute('failed') -ne 0 -or [int]$proofCounter.GetAttribute('total') -ne $proofCount){throw 'Independent recovered host tests fail or skip.'}
    $proofCounts[$proofSuite]=$proofCount
}
if($proofCounts.core -ne 495 -or $proofCounts.agent -ne 40 -or $proofCounts.security -ne 314){throw 'Unexpected exact recovered host coverage.'}
[IO.Directory]::CreateDirectory($proofCopies)|Out-Null
$proofFiles=@('visual-polish-v2-app-close.json','visual-polish-v2-game-close.json','visual-polish-v2-review-payload.json','visual-polish-v2-source-payload.json',
    'visual-polish-v2-preservation-close.json','recorded-v26-artifact-audit-visual-polish-v2.json','visual-polish-v2-playmode.json','visual-polish-v2-playmode.xml',
    'visual-polish-v2-edit-tests.json','visual-polish-v2-static.json','visual-polish-v2-mission.json','visual-polish-v2-runtime.json','visual-polish-v2-build.json',
    'visual-polish-v2-startup.json','visual-polish-v2-package.json','visual-polish-v2-delivered-planner.json','standalone-polishedyard-v2-checks.json',
    'standalone-demo-polished-v2-full-1080p.json','visual-polish-v2-delivered-game.json')
$proofBindings=@()
foreach($proofFile in $proofFiles){
    $proofFrom=Join-Path $proofWork $proofFile
    if((Get-Item $proofFrom).Length -gt 10MB -or (Get-Item $proofFrom).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Unbounded/linked verification artifact.'}
    [IO.File]::Copy($proofFrom,(Join-Path $proofCopies $proofFile),$false)
    if((Get-FileHash $proofFrom).Hash -cne (Get-FileHash (Join-Path $proofCopies $proofFile)).Hash){throw 'Copied proof artifact differs.'}
    $proofBindings+=@{path='verification-evidence/'+$proofFile;sha256=(Get-FileHash $proofFrom).Hash.ToLowerInvariant()}
}
foreach($proofFile in @('restore-verification.json','core.trx','agent.trx','security.trx')){
    $proofFrom=Join-Path $proofRestore $proofFile;[IO.File]::Copy($proofFrom,(Join-Path $proofCopies $proofFile),$false)
    if((Get-FileHash $proofFrom).Hash -cne (Get-FileHash (Join-Path $proofCopies $proofFile)).Hash){throw 'Copied recovery evidence differs.'}
    $proofBindings+=@{path='verification-evidence/'+$proofFile;sha256=(Get-FileHash $proofFrom).Hash.ToLowerInvariant()}
}
$proofReceipt=[ordered]@{schemaVersion=1;kind='ForgeCore-visual-polish-external-verification';sourceArchiveSha256=$proofRecovery.archiveSha256;sourceFiles=$proofRecovery.sourceFiles;
    sourceManifestSha256=$proofSource.manifestSha256;appManifestSha256=$proofApp.manifestSha256;gameManifestSha256=$proofGame.manifestSha256;reviewManifestSha256=$proofReview.manifestSha256;
    independentRecoveredHostTests=$proofCounts;uniqueUnityTests=121;actualMissionPasses=6;separateStandaloneFixtureModes=4;plannerActions=11;generatedArtifactsVerified=272;audioClipsUnchanged=34;
    humanArtAccepted=$false;githubUploaded=$false;externalFileReadsTraced=$false;evidence=$proofBindings;
    limits=@('Small original industrial FPS template only','Not a new detailed character or motion set','No approved Blender/vault placement','Not reference-image or AA quality acceptance','92.05ms first-use hitch remains','Scripted full missions have zero hostile shots; firing checked separately','One RTX3070, capped 1080p, no broad hardware/GPU timer certification');
    qualification='External binding of exact source ZIP/recovery and delivered app/game/review evidence. This receipt and diagnostics are outside the immutable source ZIP. No live database, human art decisions or external model provider is used.'}
$proofStream=[IO.File]::Open($proofOut,[IO.FileMode]::CreateNew)
try{$proofBytes=[Text.Encoding]::UTF8.GetBytes(($proofReceipt|ConvertTo-Json -Depth 8));$proofStream.Write($proofBytes,0,$proofBytes.Length)}finally{$proofStream.Dispose()}
$proofReceipt|Select-Object kind,sourceFiles,uniqueUnityTests,actualMissionPasses,@{n='ReceiptSha256';e={(Get-FileHash $proofOut).Hash.ToLowerInvariant()}}|ConvertTo-Json
