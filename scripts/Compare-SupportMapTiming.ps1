[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$supportRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$supportRoot=Join-Path $supportRepo 'work/s08-s10-verification'
$supportOutput=Join-Path $supportRepo 'work/support-map-s01/comparison.json'
if(Test-Path -LiteralPath $supportOutput){throw 'Comparison exists; no overwrite.'}
$supportRows=[Collections.Generic.List[object]]::new()
foreach($supportAttempt in @('legacy-first','cached-first','legacy-repeat','cached-repeat')){
    $supportRaw=Join-Path $supportRoot ('standalone-demo-current-timing-v2-'+$supportAttempt+'.json')
    $supportCheck=Get-Content -LiteralPath (Join-Path $supportRoot ('current-timing-v2-'+$supportAttempt+'-validated.json')) -Raw | ConvertFrom-Json
    $supportHash=(Get-FileHash -LiteralPath $supportRaw).Hash.ToLowerInvariant()
    if($supportCheck.rawReceiptSha256 -cne $supportHash -or -not $supportCheck.windowsMatchOriginal -or -not $supportCheck.measuredGameplayMaximumPassed){throw 'Missing exact validated measurement.'}
    $supportData=(Get-Content -LiteralPath $supportRaw -Raw | ConvertFrom-Json).report
    $supportTrace=$supportData.timingDiagnostics
    $supportVariant=if($supportAttempt.StartsWith('legacy-')){'legacy_property_per_influence'}else{'cached_bone_roles'}
    if($supportTrace.supportMapVariant -cne $supportVariant -or $supportData.width -ne 1920 -or $supportData.height -ne 1080 -or $supportData.passes.Count -ne 3){throw 'Mismatched workload/variant.'}
    $supportMap=@($supportTrace.scopedWork.statistics | Where-Object kind -ceq FootworkWeightMap)
    $supportSave=@($supportTrace.scopedWork.statistics | Where-Object kind -ceq NewGameCheckpoint)
    $supportFlush=@($supportTrace.scopedWork.statistics | Where-Object kind -ceq StateFlush)
    if($supportMap.Count -ne 1 -or $supportMap[0].count -ne 3){throw 'Expected three actually initialized support maps.'}
    $supportRows.Add([ordered]@{attempt=$supportAttempt;rawSha256=$supportHash;variant=$supportVariant;
        mappingTotalMilliseconds=$supportMap[0].totalMilliseconds;mappingMaximumMilliseconds=$supportMap[0].maximumMilliseconds;
        newGameCheckpointMaximumMilliseconds=$supportSave[0].maximumMilliseconds;settingsFlushMaximumMilliseconds=$supportFlush[0].maximumMilliseconds;
        unityAwakeMilliseconds=$supportTrace.unityAwakeMilliseconds;unityReadyMilliseconds=$supportTrace.unityReadyMilliseconds;
        warmupMaximumMilliseconds=($supportTrace.phases | Where-Object phase -ceq warmup).maximumWallMilliseconds;
        gameplayMaximumMilliseconds=($supportTrace.phases | Where-Object phase -ceq gameplay).maximumWallMilliseconds;
        idleSeconds=@($supportData.passes.idleSeconds);allMeasuredWindowsMatchOriginal=$true})
}
$supportImproved=$true
foreach($supportOffset in @(0,2)){
    if($supportRows[$supportOffset+1].mappingTotalMilliseconds -gt .25*$supportRows[$supportOffset].mappingTotalMilliseconds){$supportImproved=$false}
}
# Bind the measured game to the sealed clean export; launcher hashes alone do not identify Unity code.
$supportGame='D:/ForgeCore/Workspace/Games/ForgeCore/Builds/S01-SupportMap-20261116'
$supportManifestPath=Join-Path $supportRepo 'outputs/ForgeCore-TestGame-20261116-SupportMapV1/delivery-manifest.json'
$supportManifest=Get-Content -LiteralPath $supportManifestPath -Raw | ConvertFrom-Json
$supportIdentities=[Collections.Generic.List[object]]::new()
foreach($supportName in @('ForgeCore-S01SupportMap.exe','ForgeCore-S01SupportMap.exe.forgecore-build.json','ForgeCore-S01SupportMap_Data/level0','ForgeCore-S01SupportMap_Data/Managed/ForgeCoreGameTemplates.Runtime.dll','ForgeCore-S01SupportMap_Data/Managed/Assembly-CSharp.dll')){
    $supportExpected=@($supportManifest.files | Where-Object path -ceq $supportName)
    $supportActual=(Get-FileHash -LiteralPath (Join-Path $supportGame $supportName)).Hash.ToLowerInvariant()
    if($supportExpected.Count -ne 1 -or $supportActual -cne $supportExpected[0].sha256){throw 'Measured build differs from sealed export.'}
    $supportIdentities.Add([ordered]@{path=$supportName;sha256=$supportActual})
}
$supportResult=[ordered]@{schemaVersion=1;rows=$supportRows;identities=$supportIdentities;sealedDeliveryManifestSha256=(Get-FileHash -LiteralPath $supportManifestPath).Hash.ToLowerInvariant();
    mappingImprovedAtLeast75PercentInBothPairs=$supportImproved;unchangedWarmupMilliseconds=2000;unchangedGameplayMaximumBudgetMilliseconds=100;
    freezeFixed=$false;wholeSessionSmoothnessAccepted=$false;humanArtAccepted=$false;
    qualification='Same build/scene/hardware/1080p, ordered legacy/cached pairs; first pair 30s idle, repeat pair 8s. Caches and normal workstation activity not controlled. Nested scope totals must not be summed as exclusive CPU time. This setup-only optimization does not establish a fix for gameplay hitches or the initial unattributed startup interval.'}
$supportStream=[IO.File]::Open($supportOutput,[IO.FileMode]::CreateNew)
try{$supportBytes=[Text.Encoding]::UTF8.GetBytes(($supportResult|ConvertTo-Json -Depth 9));$supportStream.Write($supportBytes,0,$supportBytes.Length)}finally{$supportStream.Dispose()}
$supportRows | ForEach-Object {[pscustomobject]$_} | Format-Table attempt,mappingTotalMilliseconds,warmupMaximumMilliseconds,gameplayMaximumMilliseconds,newGameCheckpointMaximumMilliseconds
if(-not $supportImproved){throw 'Mapping improvement criterion failed; all raw/comparison evidence retained.'}
