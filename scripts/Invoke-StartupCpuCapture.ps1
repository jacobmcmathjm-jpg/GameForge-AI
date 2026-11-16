[CmdletBinding()]
param([ValidatePattern('^[a-z0-9-]{1,30}$')][string]$Attempt='v95-first',
 [ValidateSet('Current','On','Off')][string]$Variant='Current',[switch]$NoProfiler)
$ErrorActionPreference='Stop'
$Variant=switch($Variant.ToLowerInvariant()){'current'{'Current'}'on'{'On'}'off'{'Off'}}
$cpuRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$cpuRoot=Join-Path $cpuRepo ('work/startup-cpu/'+$Attempt)
if(Test-Path -LiteralPath $cpuRoot){throw 'Fresh capture required; preserve prior attempts.'}
if(Get-Process Unity,ForgeCore-S01Async,ForgeCore-SplashOn,ForgeCore-SplashOff -ErrorAction SilentlyContinue){throw 'Editors/previous target players must be idle.'}
$cpuSource='C:/Users/jacob/Documents/Codex/2026-07-28/v/outputs/ForgeCore-TestGame-20261116-AsyncStartV1'
$cpuName='ForgeCore-S01Async'
if($Variant -cne 'Current'){
 $cpuSource='D:/ForgeCore/Workspace/Games/ForgeCore/Builds/S01-StartupFade-20261116/Splash'+$Variant
 $cpuName='ForgeCore-Splash'+$Variant
}
if(-not(Test-Path -LiteralPath (Join-Path $cpuSource ($cpuName+'.exe')))){throw 'Exact diagnostic export required.'}
$cpuPlayer=Join-Path $cpuRoot 'Player'
[IO.Directory]::CreateDirectory($cpuRoot)|Out-Null
Copy-Item -LiteralPath $cpuSource -Destination $cpuPlayer -Recurse
$cpuExe=Join-Path $cpuPlayer ($cpuName+'.exe')
$cpuRaw=Join-Path $cpuRoot 'startup.raw'
$cpuLog=Join-Path $cpuRoot 'player.log'
$cpuArguments=@(
 '-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',('"'+$cpuLog+'"'),
 '-forgecore-demo-probe','-forgecore-timing-probe')
if(-not $NoProfiler){$cpuArguments+=@('-profiler-enable','-profiler-log-file',('"'+$cpuRaw+'"'),'-profiler-capture-frame-count','240','-profiler-maxusedmemory','67108864')}
$cpuIdentity=[ordered]@{}
foreach($cpuRelative in @(($cpuName+'_Data/level0'),($cpuName+'_Data/globalgamemanagers'),($cpuName+'_Data/Managed/ForgeCoreGameTemplates.Runtime.dll'))){
 $cpuOriginal=Join-Path $cpuSource $cpuRelative;$cpuCopy=Join-Path $cpuPlayer $cpuRelative
 $cpuHash=(Get-FileHash -LiteralPath $cpuOriginal).Hash.ToLowerInvariant()
 if((Get-FileHash -LiteralPath $cpuCopy).Hash.ToLowerInvariant() -cne $cpuHash){throw 'Diagnostic payload copy differs.'}
 $cpuIdentity[$cpuRelative]=$cpuHash
}
$cpuProcess=Start-Process -FilePath $cpuExe -WorkingDirectory $cpuPlayer -WindowStyle Hidden -PassThru -ArgumentList $cpuArguments
if(-not $cpuProcess.WaitForExit(260000)){Stop-Process -Id $cpuProcess.Id -ErrorAction SilentlyContinue;throw 'Owned profile player timed out; capture/logs retained.'}
$cpuProcess.Refresh()
$cpuReports=@(Get-ChildItem -LiteralPath $cpuPlayer -Directory -Filter 'ForgeCoreDemoAcceptance-*')
if($cpuProcess.ExitCode -ne 0 -or $cpuReports.Count -ne 1 -or (-not $NoProfiler -and -not(Test-Path -LiteralPath $cpuRaw))){throw 'Profile player/capture failed; all attempts retained.'}
$cpuReport=Get-Content -LiteralPath (Join-Path $cpuReports[0].FullName 'report.json') -Raw | ConvertFrom-Json
if($cpuReport.error -or $cpuReport.device -ceq 'Null' -or $cpuReport.passes.Count -ne 3 -or $cpuReport.width -ne 1920 -or $cpuReport.height -ne 1080 -or $cpuReport.directDamageUsed -or $cpuReport.playerTeleportUsed){throw 'Missing real-game completion.'}
foreach($cpuPass in $cpuReport.passes){if(-not $cpuPass.completed -or -not $cpuPass.checkpointRoundtrip -or $cpuPass.kills -ne 3 -or $cpuPass.shots -lt 9 -or $cpuPass.recordedFootsteps -le 15){throw 'Incomplete actual-combat/audio/save pass.'}}
$cpuReceipt=[ordered]@{schemaVersion=2;variant=$Variant;profiled=(-not $NoProfiler);executable=$cpuExe;identity=$cpuIdentity;exitCode=$cpuProcess.ExitCode;reportFolder=$cpuReports[0].FullName;
 reportSha256=(Get-FileHash -LiteralPath (Join-Path $cpuReports[0].FullName 'report.json')).Hash.ToLowerInvariant();
 qualification='Same .95 runtime and preserved scene; splash-only diagnostic exports. Profiler runs are attribution evidence, not performance acceptance. Caches/workstation activity uncontrolled; no original-freeze or whole-session acceptance.'}
if(-not $NoProfiler){$cpuReceipt.rawPath=$cpuRaw;$cpuReceipt.rawBytes=(Get-Item -LiteralPath $cpuRaw).Length;$cpuReceipt.rawSha256=(Get-FileHash -LiteralPath $cpuRaw).Hash.ToLowerInvariant()}
$cpuStream=[IO.File]::Open((Join-Path $cpuRoot 'receipt.json'),[IO.FileMode]::CreateNew)
try{$cpuBytes=[Text.Encoding]::UTF8.GetBytes(($cpuReceipt|ConvertTo-Json -Depth 6));$cpuStream.Write($cpuBytes,0,$cpuBytes.Length)}finally{$cpuStream.Dispose()}
$cpuReceipt|ConvertTo-Json
