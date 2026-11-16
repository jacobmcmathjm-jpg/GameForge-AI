[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$retroRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$retroCheckpoint=Join-Path $retroRepo 'backups/20261105-retro64-v2'
$retroOutput=Join-Path $retroCheckpoint 'verification-receipt.json'
if(Test-Path -LiteralPath $retroOutput){throw 'Verification receipt exists; no overwrite.'}
$retroRestoreRoot=Join-Path $retroRepo 'work/three-hour-assets/restore-20261105-retro64-v2-first'
$retroRestorePath=Join-Path $retroRestoreRoot 'restore-verification.json'
$retroRestore=Get-Content -LiteralPath $retroRestorePath -Raw|ConvertFrom-Json
$retroArchive=Join-Path $retroCheckpoint 'ForgeCore-source-20261105-retro64-v2.zip'
$retroArchiveHash=(Get-FileHash -LiteralPath $retroArchive).Hash.ToLowerInvariant()
if($retroRestore.archiveSha256 -cne $retroArchiveHash -or -not $retroRestore.releaseBuildPassed -or -not $retroRestore.hostTestsRequested -or -not $retroRestore.exclusiveLocalPackageSource -or -not $retroRestore.archivePayloadHashesVerified -or $retroRestore.normalAppStarted -or $retroRestore.liveDatabaseOpened -or $retroRestore.inferenceStarted){throw 'Independent restored-source proof incomplete.'}
function Read-RetroTestCounts([string]$Path){
    $retroSettings=[Xml.XmlReaderSettings]::new();$retroSettings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$retroSettings.XmlResolver=$null
    $retroReader=[Xml.XmlReader]::Create($Path,$retroSettings)
    try{$retroXml=[Xml.XmlDocument]::new();$retroXml.Load($retroReader)}finally{$retroReader.Dispose()}
    $retroCounters=$retroXml.SelectSingleNode('//*[local-name()="Counters"]')
    if($null -eq $retroCounters -or [int]$retroCounters.failed -ne 0 -or [int]$retroCounters.notExecuted -ne 0 -or [int]$retroCounters.passed -ne [int]$retroCounters.total){throw 'Host suite has failures/skips/incomplete results.'}
    return [int]$retroCounters.passed
}
$retroRootTrx=@(Get-ChildItem -LiteralPath (Join-Path $retroRepo 'work/s08-s10-verification') -File -Filter 'retro64-v2-final*.trx')
if($retroRootTrx.Count -ne 3){throw 'Expected exactly three final host receipts.'}
$retroRootCounts=@($retroRootTrx|ForEach-Object {Read-RetroTestCounts $_.FullName}|Sort-Object)
if(($retroRootCounts -join ',') -cne '40,314,535'){throw 'Wrong current host coverage.'}
$retroPaths=[Collections.Generic.List[string]]::new();$retroPaths.Add($retroRestorePath)
foreach($retroSuite in @(@{name='core';count=535},@{name='agent';count=40},@{name='security';count=314})){
    $retroPath=Join-Path $retroRestoreRoot ($retroSuite.name+'.trx')
    if((Read-RetroTestCounts $retroPath) -ne $retroSuite.count){throw 'Wrong independent restored host coverage.'}
    $retroPaths.Add($retroPath)
}
foreach($retroFile in (Get-ChildItem -LiteralPath (Join-Path $retroRepo 'work/s08-s10-verification') -File -Filter 'retro64-v2*'|Where-Object Extension -In @('.json','.xml','.trx'))){$retroPaths.Add($retroFile.FullName)}
foreach($retroName in @('standalone-retro64-v2-checks.json','standalone-demo-retro64-v2-final-1080p-portable.json','recorded-v26-artifact-audit-retro64-v2.json')){$retroPaths.Add((Join-Path $retroRepo ('work/s08-s10-verification/'+$retroName)))}
$retroPlay=Get-Content -LiteralPath (Join-Path $retroRepo 'work/s08-s10-verification/retro64-v2-playmode.json') -Raw|ConvertFrom-Json
if($retroPlay.result -cne 'Passed' -or $retroPlay.passed -ne 106 -or $retroPlay.failed -ne 0 -or $retroPlay.skipped -ne 0){throw 'Actual PlayMode proof incomplete.'}
$retroEvidence=@($retroPaths|Sort-Object -Unique|ForEach-Object{$retroItem=Get-Item -LiteralPath $_;[ordered]@{path=[IO.Path]::GetRelativePath($retroRepo,$retroItem.FullName).Replace('\','/');bytes=$retroItem.Length;sha256=(Get-FileHash -LiteralPath $retroItem.FullName).Hash.ToLowerInvariant()}})
$retroPayload=[ordered]@{schemaVersion=1;kind='retro64_v2_external_recovery_and_test_binding';archiveSha256=$retroArchiveHash;sourceFiles=$retroRestore.sourceFiles;
    sourceManifestSha256=(Get-FileHash -LiteralPath (Join-Path $retroCheckpoint 'source/source-checkpoint-manifest.json')).Hash.ToLowerInvariant();exclusiveOfflineRestore=$true;independentReleaseBuild=$true;
    currentHostChecks=889;restoredHostChecks=889;actualProjectPlayModeChecks=106;actualProjectEditModeChecks=19;unityProjectRestored=$false;humanArtAccepted=$false;githubUploaded=$false;evidence=$retroEvidence;
    qualification='External receipt binds this exact sanitized immutable source archive, independently restored offline Release build and all 889 rerun host checks. Unity/player proofs are from the original project and exact delivered-game copy, not a restored external Unity project. Not a full-machine backup, art/rights approval, cold-start hitch fix or genre/graphics-quality guarantee.'}
$retroStream=[IO.File]::Open($retroOutput,[IO.FileMode]::CreateNew)
try{$retroBytes=[Text.Encoding]::UTF8.GetBytes(($retroPayload|ConvertTo-Json -Depth 7));$retroStream.Write($retroBytes,0,$retroBytes.Length)}finally{$retroStream.Dispose()}
[pscustomobject]@{receipt=$retroOutput;sha256=(Get-FileHash -LiteralPath $retroOutput).Hash.ToLowerInvariant();archiveSha256=$retroArchiveHash;boundEvidence=$retroEvidence.Count;restoredHostChecks=889}|ConvertTo-Json
