[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$dispatchRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$dispatchEvidence=Join-Path $dispatchRepo 'work/s08-s10-verification'
$dispatchOutput=Join-Path $dispatchRepo 'work/async-start-s01/comparison.json'
if(Test-Path -LiteralPath $dispatchOutput){throw 'Immutable comparison exists.'}
$dispatchRows=[Collections.Generic.List[object]]::new()
foreach($dispatchAttempt in @('blocking-control','async-first','async-repeat')){
    $dispatchRaw=Join-Path $dispatchEvidence ('standalone-demo-current-timing-v3-'+$dispatchAttempt+'.json')
    $dispatchCheck=Get-Content -LiteralPath (Join-Path $dispatchEvidence ('current-timing-v3-'+$dispatchAttempt+'-validated.json')) -Raw | ConvertFrom-Json
    $dispatchHash=(Get-FileHash -LiteralPath $dispatchRaw).Hash.ToLowerInvariant()
    if($dispatchCheck.rawReceiptSha256 -cne $dispatchHash -or -not $dispatchCheck.windowsMatchOriginal -or -not $dispatchCheck.measuredGameplayMaximumPassed){throw 'Missing exact validated windows.'}
    $dispatchData=(Get-Content -LiteralPath $dispatchRaw -Raw | ConvertFrom-Json).report
    $dispatchTrace=$dispatchData.timingDiagnostics
    $dispatchExpected=if($dispatchAttempt -ceq 'blocking-control'){'blocking_control'}else{'queued_nonblocking'}
    if($dispatchTrace.checkpointDispatch -cne $dispatchExpected -or $dispatchData.width -ne 1920 -or $dispatchData.height -ne 1080 -or $dispatchData.passes.Count -ne 3){throw 'Wrong mode/workload.'}
    $dispatchScope=@($dispatchTrace.scopedWork.statistics | Where-Object kind -ceq NewGameCheckpoint)
    $dispatchBackground=@($dispatchTrace.scopedWork.statistics | Where-Object kind -ceq BackgroundFlush)
    if($dispatchScope.Count -ne 1 -or $dispatchScope[0].count -ne 6 -or $dispatchBackground[0].count -lt 6){throw 'Missing start/checkpoint durability scopes.'}
    $dispatchRows.Add([ordered]@{attempt=$dispatchAttempt;rawSha256=$dispatchHash;dispatch=$dispatchExpected;
        newGameCheckpointMaximumMilliseconds=$dispatchScope[0].maximumMilliseconds;newGameCheckpointTotalMilliseconds=$dispatchScope[0].totalMilliseconds;
        backgroundDurableFlushMaximumMilliseconds=$dispatchBackground[0].maximumMilliseconds;
        settingsFlushMaximumMilliseconds=($dispatchTrace.scopedWork.statistics | Where-Object kind -ceq StateFlush).maximumMilliseconds;
        warmupMaximumMilliseconds=($dispatchTrace.phases | Where-Object phase -ceq warmup).maximumWallMilliseconds;
        gameplayMaximumMilliseconds=($dispatchTrace.phases | Where-Object phase -ceq gameplay).maximumWallMilliseconds;
        allMeasuredWindowsMatchOriginal=$true;idleSeconds=@($dispatchData.passes.idleSeconds)})
}
$dispatchImproved=$true
foreach($dispatchRow in @($dispatchRows[1],$dispatchRows[2])){
    if($dispatchRow.newGameCheckpointMaximumMilliseconds -gt 10 -or $dispatchRow.newGameCheckpointMaximumMilliseconds -gt .25*$dispatchRows[0].newGameCheckpointMaximumMilliseconds){$dispatchImproved=$false}
}
$dispatchFolder='D:/ForgeCore/Workspace/Games/ForgeCore/Builds/S01-AsyncStart-20261116'
$dispatchManifest=Get-Content (Join-Path $dispatchRepo 'outputs/ForgeCore-TestGame-20261116-AsyncStartV1/delivery-manifest.json') -Raw | ConvertFrom-Json
$dispatchIdentities=[Collections.Generic.List[object]]::new()
foreach($dispatchName in @('ForgeCore-S01Async.exe.forgecore-build.json','ForgeCore-S01Async_Data/level0','ForgeCore-S01Async_Data/Managed/ForgeCoreGameTemplates.Runtime.dll')){
    $dispatchExpected=@($dispatchManifest.files | Where-Object path -ceq $dispatchName)
    $dispatchActual=(Get-FileHash -LiteralPath (Join-Path $dispatchFolder $dispatchName)).Hash.ToLowerInvariant()
    if($dispatchExpected.Count -ne 1 -or $dispatchActual -cne $dispatchExpected[0].sha256){throw 'Game differs from sealed pre-probe export.'}
    $dispatchIdentities.Add([ordered]@{path=$dispatchName;sha256=$dispatchActual})
}
$dispatchResult=[ordered]@{schemaVersion=1;rows=$dispatchRows;identities=$dispatchIdentities;newGameDispatchCriterionPassed=$dispatchImproved;
    warmupMilliseconds=2000;gameplayMaximumBudgetMilliseconds=100;initialStartupFixed=$false;wholeSessionSmoothnessAccepted=$false;
    qualification='Same build/scene/hardware at 1080p; control and first async pair have 30s idle, async repeat has 8s. Caches/workstation activity not controlled. Queue durability is retained; removing caller wait is not a claim of faster physical storage. Settings/manual save/load and forced-OS-exit fallback remain explicit synchronous boundaries.'}
$dispatchStream=[IO.File]::Open($dispatchOutput,[IO.FileMode]::CreateNew)
try{$dispatchBytes=[Text.Encoding]::UTF8.GetBytes(($dispatchResult|ConvertTo-Json -Depth 8));$dispatchStream.Write($dispatchBytes,0,$dispatchBytes.Length)}finally{$dispatchStream.Dispose()}
$dispatchRows | ForEach-Object {[pscustomobject]$_} | Format-Table attempt,newGameCheckpointMaximumMilliseconds,backgroundDurableFlushMaximumMilliseconds,warmupMaximumMilliseconds,gameplayMaximumMilliseconds
if(-not $dispatchImproved){throw 'New-game dispatch criterion failed; comparison/raw evidence retained.'}
