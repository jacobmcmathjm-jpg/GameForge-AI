[CmdletBinding()]
param([ValidateSet('v22','v23','v24','v25','v26','v27','v28','courtyard-v1','polished-v2','retro64-v1','retro64-v2','current-timing-v1','current-timing-v2','current-timing-v3','indie-v1','indie-hero-v1','indie-character-v1','connected-v1','connected-v2','connected-v3','connected-v4','connected-v5','connected-v6','connected-v7','connected-v8')][string]$Candidate='v22',[ValidatePattern('^[a-z0-9-]{1,30}$')][string]$Attempt='initial',[switch]$LongSoak,[ValidateSet('540p','720p','1080p')][string]$Viewport='540p',[switch]$Portable,[switch]$LegacySupportMap,[switch]$BlockingCheckpointControl)
$ErrorActionPreference='Stop'
$demoRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$demoName=if($Candidate -in @('v27','v28')){'S10-NextSteps-'+$Candidate}elseif($Candidate -eq 'v26'){'S10-RecordedFootsteps-v26'}elseif($Candidate -eq 'v25'){'S10-Audio-v25'}else{'S10-Demo-'+$Candidate}
$demoFolder=Join-Path 'D:\ForgeCore\Workspace\Games\ForgeCore\Builds' $demoName
$demoExe=Join-Path $demoFolder ($demoName+'.exe')
if($Candidate -ceq 'courtyard-v1'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Courtyard\nightly-yard-6f51b245faf5';$demoExe=Join-Path $demoFolder 'nightly-yard.exe'}
if($Candidate -ceq 'polished-v2'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Courtyard\polished-yard-v2';$demoExe=Join-Path $demoFolder 'polished-yard.exe'}
if($Candidate -cin @('retro64-v1','retro64-v2')){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Retro\retro-yard-'+$Candidate.Substring(8);$demoExe=Join-Path $demoFolder 'retro-yard.exe'}
if($Portable){if($Candidate -cnotin @('retro64-v1','retro64-v2')){throw 'Portable option is scoped to the verified retro candidate.'};$demoFolder=Join-Path $demoRepo ('work/delivered-'+$Candidate+'-runtime');$demoExe=Join-Path $demoFolder 'retro-yard.exe'}
if($Candidate -ceq 'current-timing-v1'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\S01-CurrentGame-20261116';$demoExe=Join-Path $demoFolder 'ForgeCore-S01Current.exe'}
if($Candidate -ceq 'current-timing-v2'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\S01-SupportMap-20261116';$demoExe=Join-Path $demoFolder 'ForgeCore-S01SupportMap.exe'}
if($Candidate -ceq 'current-timing-v3'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\S01-AsyncStart-20261116';$demoExe=Join-Path $demoFolder 'ForgeCore-S01Async.exe'}
if($Candidate -ceq 'indie-v1'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Indie-Environment-20261116';$demoExe=Join-Path $demoFolder 'ForgeCore-Indie.exe'}
if($Candidate -ceq 'connected-v8'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261117-V8';$demoExe=Join-Path $demoFolder 'ForgeCore-Connected.exe'}
if($Candidate -ceq 'connected-v7'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261117-V7';$demoExe=Join-Path $demoFolder 'ForgeCore-Connected.exe'}
if($Candidate -ceq 'connected-v6'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261117-V6';$demoExe=Join-Path $demoFolder 'ForgeCore-Connected.exe'}
if($Candidate -ceq 'connected-v5'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261116-V5';$demoExe=Join-Path $demoFolder 'ForgeCore-Connected.exe'}
if($Candidate -ceq 'connected-v4'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261116-V4';$demoExe=Join-Path $demoFolder 'ForgeCore-Connected.exe'}
if($Candidate -ceq 'connected-v3'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261116-V3';$demoExe=Join-Path $demoFolder 'ForgeCore-Connected.exe'}
if($Candidate -ceq 'connected-v2'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261116-V2';$demoExe=Join-Path $demoFolder 'ForgeCore-Connected.exe'}
if($Candidate -ceq 'connected-v1'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Level-20261116';$demoExe=Join-Path $demoFolder 'ForgeCore-Connected.exe'}
if($Candidate -ceq 'indie-character-v1'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Indie-Character-20261116';$demoExe=Join-Path $demoFolder 'ForgeCore-IndieCharacter.exe'}
if($Candidate -ceq 'indie-hero-v1'){$demoFolder='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Indie-Hero-20261116';$demoExe=Join-Path $demoFolder 'ForgeCore-IndieHero.exe'}
if($BlockingCheckpointControl -and $Candidate -cne 'current-timing-v3'){throw 'Blocking checkpoint control is scoped to v3 development timing.'}
if($LegacySupportMap -and $Candidate -cne 'current-timing-v2'){throw 'Legacy mapping comparison is scoped to the development v2 diagnostic.'}
$demoEvidence=Join-Path $demoRepo 'work/s08-s10-verification'
[IO.Directory]::CreateDirectory($demoEvidence)|Out-Null
$demoStem='standalone-demo-'+$Candidate+'-'+$Attempt
if($Portable){$demoStem+='-portable'}
$demoLog=Join-Path $demoEvidence ($demoStem+'.log')
$demoReceipt=Join-Path $demoEvidence ($demoStem+'.json')
if((Test-Path -LiteralPath $demoLog) -or (Test-Path -LiteralPath $demoReceipt)){throw 'Evidence exists; explicitly choose a new attempt; no overwrite.'}
if(-not(Test-Path -LiteralPath $demoExe -PathType Leaf)){throw 'Expected exact demo executable.'}
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Unity Editor must be idle before the owned standalone graphics test.'}
$demoBefore=@(Get-ChildItem -LiteralPath $demoFolder -Directory -Filter 'ForgeCoreDemoAcceptance-*' | ForEach-Object FullName)
$demoWidth=if($Viewport -ceq '1080p'){1920}elseif($Viewport -ceq '720p'){1280}else{960};$demoHeight=if($Viewport -ceq '1080p'){1080}elseif($Viewport -ceq '720p'){720}else{540}
$demoArguments=@('-screen-width',([string]$demoWidth),'-screen-height',([string]$demoHeight),'-screen-fullscreen','0','-logFile',('"'+$demoLog+'"'),'-forgecore-demo-probe')
if($Candidate -cin @('current-timing-v1','current-timing-v2','current-timing-v3','indie-v1','indie-hero-v1','indie-character-v1','connected-v1','connected-v2','connected-v3','connected-v4','connected-v5','connected-v6','connected-v7','connected-v8')){$demoArguments+='-forgecore-timing-probe'}
if($LegacySupportMap){$demoArguments+='-forgecore-footwork-legacy-probe'}
if($BlockingCheckpointControl){$demoArguments+='-forgecore-blocking-checkpoint-probe'}
if($LongSoak){if($Candidate -notin @('v27','v28','courtyard-v1','polished-v2','retro64-v1','retro64-v2','current-timing-v1','current-timing-v2','current-timing-v3','indie-v1','indie-hero-v1','indie-character-v1','connected-v1','connected-v2','connected-v3','connected-v4','connected-v5','connected-v6','connected-v7','connected-v8')){throw 'Long soak requires the new profiled candidate.'};$demoArguments+='-forgecore-soak-30'}
$demoProcess=Start-Process -FilePath $demoExe -WorkingDirectory $demoFolder -WindowStyle Hidden -PassThru -ArgumentList $demoArguments
if(-not $demoProcess.WaitForExit(260000)){
    Stop-Process -Id $demoProcess.Id -ErrorAction SilentlyContinue
    throw 'Owned 240s test plus shutdown allowance expired; only its own process stopped; log retained.'
}
$demoProcess.Refresh()
$demoNew=@(Get-ChildItem -LiteralPath $demoFolder -Directory -Filter 'ForgeCoreDemoAcceptance-*' | Where-Object FullName -NotIn $demoBefore)
if($demoNew.Count -ne 1){throw 'Expected exactly one owned acceptance evidence directory.'}
$demoReport=Get-Content -LiteralPath (Join-Path $demoNew[0].FullName 'report.json') -Raw | ConvertFrom-Json
if($demoReport.width -ne $demoWidth -or $demoReport.height -ne $demoHeight){throw 'Actual rendered resolution differs from the requested test viewport; do not count this as a requested-resolution pass.'}
$demoPayload=[ordered]@{schemaVersion=1;executable=$demoExe;exeSha256=(Get-FileHash -LiteralPath $demoExe).Hash.ToLowerInvariant();
    exitCode=$demoProcess.ExitCode;folder=$demoNew[0].FullName;report=$demoReport;humanAcceptance=$false}
$demoStream=[IO.File]::Open($demoReceipt,[IO.FileMode]::CreateNew)
try{$demoWriter=[IO.StreamWriter]::new($demoStream);try{$demoWriter.Write(($demoPayload|ConvertTo-Json -Depth 15))}finally{$demoWriter.Dispose()}}finally{$demoStream.Dispose()}
if($demoProcess.ExitCode -ne 0 -or $demoReport.error){throw ('Actual demo test failed; retained evidence: '+$demoReceipt)}
if($demoReport.device -eq 'Null' -or $demoReport.passes.Count -ne 3 -or -not $demoReport.muted -or $demoReport.directDamageUsed -or $demoReport.playerTeleportUsed){throw 'Missing required graphics/real-combat/isolation evidence.'}
foreach($demoPass in $demoReport.passes){
    if($Candidate -cin @('connected-v1','connected-v2','connected-v3','connected-v4','connected-v5','connected-v6','connected-v7','connected-v8') -and ($demoReport.layoutProfile -cne 'industrial_connected_v1' -or $demoReport.checkpointProfile -cne 'industrial_connected_v1' -or $demoPass.visitedZones -ne 7 -or -not $demoPass.gateChecks -or $demoPass.travelled -lt 110)){throw 'Missing actual three-zone connected route, gate and far-room checkpoint evidence.'}
    if(-not $demoPass.completed -or -not $demoPass.checkpointRoundtrip -or $demoPass.kills -ne 3 -or $demoPass.shots -lt 9 -or $demoPass.travelled -lt 45 -or $demoPass.frames.totalFrames -lt 100){throw 'Incomplete actual mission pass.'}
    if($Candidate -eq 'v25' -and ($demoReport.audioRecipe -ne 'forgecore_layered_surface_audio_v2' -or $demoReport.audioClips -ne 28 -or $demoPass.footsteps -le 15)){throw 'Missing actual new-bank/grounded-footstep evidence.'}
    if($Candidate -in @('v26','v27','v28','courtyard-v1','polished-v2','retro64-v1','retro64-v2','current-timing-v1','current-timing-v2','current-timing-v3','indie-v1','indie-hero-v1','indie-character-v1','connected-v1','connected-v2','connected-v3','connected-v4','connected-v5','connected-v6','connected-v7','connected-v8') -and ($demoReport.recordedFootstepBankId -cne 'gbox_subway_hardfloor_v1' -or $demoReport.audioClips -ne 34 -or $demoPass.recordedFootsteps -le 15)){throw 'Missing actual recorded-bank gameplay evidence.'}
    if($Candidate -in @('v27','v28','courtyard-v1','polished-v2','retro64-v1','retro64-v2','current-timing-v1','current-timing-v2','current-timing-v3','indie-v1','indie-hero-v1','indie-character-v1','connected-v1','connected-v2','connected-v3','connected-v4','connected-v5','connected-v6','connected-v7','connected-v8') -and (-not $demoPass.work -or $demoPass.work.totalSamples -le 0)){throw 'Missing named work profiling.'}
    if($Candidate -in @('v28','courtyard-v1','polished-v2','retro64-v1','retro64-v2','current-timing-v1','current-timing-v2','current-timing-v3','indie-v1','indie-hero-v1','indie-character-v1','connected-v1','connected-v2','connected-v3','connected-v4','connected-v5','connected-v6','connected-v7','connected-v8')){
        $flush=@($demoPass.work.statistics | Where-Object kind -eq 'BackgroundFlush')
        $mainFlush=@($demoPass.work.statistics | Where-Object kind -eq 'StateFlush')
        if($flush.Count -ne 1 -or $flush[0].count -lt 5 -or ($mainFlush.Count -gt 0 -and $mainFlush[0].count -gt 0)){throw 'Automatic checkpoint flushes must run on the worker, not the gameplay thread.'}
    }
    if($LongSoak -and ($demoPass.idleSeconds -lt 30 -or $demoPass.idleFrames.totalFrames -lt 1000)){throw 'Incomplete actual thirty-second idle soak.'}
}
$demoReport.passes | Select-Object preset,completed,health,shots,hostileShots,travelled,seconds,@{n='p95ms';e={$_.frames.p95}},@{n='maxMs';e={$_.frames.maximum}}
