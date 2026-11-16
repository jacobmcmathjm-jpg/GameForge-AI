[CmdletBinding()]
param([ValidateSet('Courtyard-v19','PowerBox-v20','PowerBox-v21','Demo-v22','Demo-v23','Demo-v24','Audio-v25','RecordedFootsteps-v26','NextSteps-v28','GraphicsBenchmark-v3','NightlyYard-v1','PolishedYard-v2','Retro64-v1','Retro64-v2','CurrentTiming-v1','CurrentTiming-v2','CurrentTiming-v3','StartupSplash-Off','Indie-v1','IndieHero-v1','IndieCharacter-v1','Connected-v1','Connected-v2','Connected-v3','Connected-v4','Connected-v5')][string]$Candidate='Courtyard-v19',[ValidatePattern('^[a-z0-9-]{1,30}$')][string]$Attempt)
$ErrorActionPreference='Stop'
$checkRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if($Candidate -ceq 'Connected-v5'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261116-V5'
    $checkExe=Join-Path $checkGame 'ForgeCore-Connected.exe'
}elseif($Candidate -ceq 'Connected-v4'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261116-V4'
    $checkExe=Join-Path $checkGame 'ForgeCore-Connected.exe'
}elseif($Candidate -ceq 'Connected-v3'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261116-V3'
    $checkExe=Join-Path $checkGame 'ForgeCore-Connected.exe'
}elseif($Candidate -ceq 'Connected-v2'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261116-V2'
    $checkExe=Join-Path $checkGame 'ForgeCore-Connected.exe'
}elseif($Candidate -ceq 'Connected-v1'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261116'
    $checkExe=Join-Path $checkGame 'ForgeCore-Connected.exe'
}elseif($Candidate -ceq 'IndieCharacter-v1'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Indie-Character-20261116'
    $checkExe=Join-Path $checkGame 'ForgeCore-IndieCharacter.exe'
}elseif($Candidate -ceq 'IndieHero-v1'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Indie-Hero-20261116'
    $checkExe=Join-Path $checkGame 'ForgeCore-IndieHero.exe'
}elseif($Candidate -ceq 'Indie-v1'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Indie-Environment-20261116'
    $checkExe=Join-Path $checkGame 'ForgeCore-Indie.exe'
}elseif($Candidate -ceq 'StartupSplash-Off'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\S01-StartupFade-20261116\SplashOff'
    $checkExe=Join-Path $checkGame 'ForgeCore-SplashOff.exe'
}elseif($Candidate -ceq 'CurrentTiming-v3'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\S01-AsyncStart-20261116'
    $checkExe=Join-Path $checkGame 'ForgeCore-S01Async.exe'
}elseif($Candidate -ceq 'CurrentTiming-v2'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\S01-SupportMap-20261116'
    $checkExe=Join-Path $checkGame 'ForgeCore-S01SupportMap.exe'
}elseif($Candidate -ceq 'CurrentTiming-v1'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\S01-CurrentGame-20261116'
    $checkExe=Join-Path $checkGame 'ForgeCore-S01Current.exe'
}elseif($Candidate -cin @('Retro64-v1','Retro64-v2')){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Retro\retro-yard-'+$Candidate.Substring(8)
    $checkExe=Join-Path $checkGame 'retro-yard.exe'
}elseif($Candidate -ceq 'PolishedYard-v2'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Courtyard\polished-yard-v2'
    $checkExe=Join-Path $checkGame 'polished-yard.exe'
}elseif($Candidate -ceq 'NightlyYard-v1'){
    $checkGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Courtyard\nightly-yard-6f51b245faf5'
    $checkExe=Join-Path $checkGame 'nightly-yard.exe'
}else{
    $checkPrefix=if($Candidate -ceq 'GraphicsBenchmark-v3'){'S13-'}else{'S10-'}
    $checkGame=Join-Path 'D:\ForgeCore\Workspace\Games\ForgeCore\Builds' ($checkPrefix+$Candidate)
    $checkExe=Join-Path $checkGame ($checkPrefix+$Candidate+'.exe')
}
$checkEvidence=Join-Path $checkRepo 'work/s08-s10-verification'
[IO.Directory]::CreateDirectory($checkEvidence)|Out-Null
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Unity Editor must be idle before standalone checks.'}
if(-not(Test-Path -LiteralPath $checkExe -PathType Leaf)){throw 'Expected exact fresh player.'}
$checkStem='standalone-'+$Candidate.ToLowerInvariant()
if($Attempt){$checkStem+='-'+$Attempt}
$checkReceipt=Join-Path $checkEvidence ($checkStem+'-checks.json')
if(Test-Path -LiteralPath $checkReceipt){throw 'Evidence exists; no replay or overwrite.'}
$checkResults=[Collections.Generic.List[object]]::new()
$checkModes=if($Candidate -cin @('GraphicsBenchmark-v3','NightlyYard-v1','PolishedYard-v2','Retro64-v1','Retro64-v2','CurrentTiming-v1','CurrentTiming-v2','CurrentTiming-v3','StartupSplash-Off','Indie-v1','IndieHero-v1','IndieCharacter-v1','Connected-v1','Connected-v2','Connected-v3','Connected-v4','Connected-v5') -or $Candidate.StartsWith('PowerBox-', [StringComparison]::Ordinal) -or $Candidate.StartsWith('Demo-', [StringComparison]::Ordinal) -or $Candidate.StartsWith('Audio-', [StringComparison]::Ordinal) -or $Candidate.StartsWith('RecordedFootsteps-', [StringComparison]::Ordinal) -or $Candidate.StartsWith('NextSteps-', [StringComparison]::Ordinal)){@('courtyard','powerbox','render','corpse')}else{@('courtyard','render','corpse')}
foreach($checkMode in $checkModes) {
    $checkLog=Join-Path $checkEvidence ($checkStem+"-$checkMode.log")
    if(Test-Path -LiteralPath $checkLog){throw 'Diagnostic log exists; preserve it and explicitly select a fresh attempt.'}
    $checkBefore=@(Get-ChildItem -LiteralPath $checkGame -Directory -Filter 'ForgeCoreRenderProbe-*' | ForEach-Object FullName)
    $checkProcess=Start-Process -FilePath $checkExe -WorkingDirectory $checkGame -WindowStyle Hidden -PassThru -ArgumentList @(
        '-screen-width','960','-screen-height','540','-screen-fullscreen','0','-logFile',('"'+$checkLog+'"'),"-forgecore-$checkMode-probe")
    if(-not $checkProcess.WaitForExit(45000)) {
        # Only the diagnostic process started immediately above is ours to terminate.
        Stop-Process -Id $checkProcess.Id -ErrorAction SilentlyContinue
        throw 'Owned standalone diagnostic exceeded 45 seconds; evidence retained.'
    }
    $checkProcess.Refresh()
    $checkNew=@(Get-ChildItem -LiteralPath $checkGame -Directory -Filter 'ForgeCoreRenderProbe-*' | Where-Object FullName -NotIn $checkBefore)
    if($checkNew.Count -ne 1){throw 'Expected exactly one newly owned diagnostic evidence directory.'}
    $checkReport=Get-Content -LiteralPath (Join-Path $checkNew[0].FullName 'report.json') -Raw | ConvertFrom-Json
    $checkResults.Add([ordered]@{mode=$checkMode;exitCode=$checkProcess.ExitCode;folder=$checkNew[0].FullName;report=$checkReport})
    if($checkProcess.ExitCode -ne 0 -or $checkReport.error){throw "Standalone $checkMode check failed; retain its report/log."}
    if($checkReport.device -eq 'Null'){throw 'A real graphics device is required.'}
    if(@($checkReport.samples | Where-Object magentaPixels -GT 0).Count -ne 0){throw 'Actual camera capture contains magenta.'}
    if($checkMode -eq 'render' -and (-not $checkReport.actualShotMarker -or -not $checkReport.actualGuardShotMarker)){throw 'Actual player/guard shots were not observed.'}
    if($checkMode -eq 'powerbox' -and (-not $checkReport.physicalPowerBox -or -not $checkReport.powerPlaceholderAbsent -or -not $checkReport.powerBoxPersistent -or -not $checkReport.powerBoxReset)){throw 'Actual power-box state/persistence/placeholder check failed.'}
    if($checkMode -eq 'corpse') {
        $checkSettled=@($checkReport.corpseSamples | Where-Object time -GE 1.8)
        if($checkSettled.Count -lt 4){throw 'Insufficient settled corpse samples.'}
        foreach($checkSample in $checkSettled) {
            if($checkSample.torsoMinY-$checkSample.floorY -gt .015 -or [Math]::Abs($checkSample.minY-$checkSample.floorY) -gt .02){throw 'Actual corpse does not meet independent floor-contact limits.'}
        }
    }
    [pscustomobject]@{Mode=$checkMode;ExitCode=$checkProcess.ExitCode;Evidence=$checkNew[0].FullName}
}
$checkPayload=[ordered]@{schemaVersion=1;executable=$checkExe;exeSha256=(Get-FileHash -LiteralPath $checkExe).Hash.ToLowerInvariant();
    graphicsRequired=$true;humanArtAcceptance=$false;representativePerformanceAcceptance=$false;checks=$checkResults}
$checkStream=[IO.File]::Open($checkReceipt,[IO.FileMode]::CreateNew)
try{$checkWriter=[IO.StreamWriter]::new($checkStream);try{$checkWriter.Write(($checkPayload|ConvertTo-Json -Depth 12))}finally{$checkWriter.Dispose()}}finally{$checkStream.Dispose()}
