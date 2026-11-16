[CmdletBinding()]
param([ValidatePattern('^[a-z0-9-]{1,30}$')][string]$Attempt='harness-v1')
# Synthetic comparator contract tests only. These fixtures are NOT game evidence.
$ErrorActionPreference='Stop'
$harnessRepo=Split-Path $PSScriptRoot -Parent
$harnessRoot=Join-Path $harnessRepo ('work/startup-cpu/'+$Attempt)
if(Test-Path -LiteralPath $harnessRoot){throw 'Fresh harness required.'}
function Write-HarnessJson($path,$value){
 [IO.Directory]::CreateDirectory((Split-Path $path -Parent))|Out-Null
 $stream=[IO.File]::Open($path,[IO.FileMode]::CreateNew)
 try{$bytes=[Text.Encoding]::UTF8.GetBytes(($value|ConvertTo-Json -Depth 10));$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}
}
$harnessCases=@('valid','slow-off','small-reduction','changed-report','profiled','legacy','escaped-path','runtime','incomplete','warmup')
foreach($harnessCase in $harnessCases){
 $caseRoot=Join-Path $harnessRoot $harnessCase
 [IO.Directory]::CreateDirectory((Join-Path $caseRoot 'scripts'))|Out-Null
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Test-StartupSplashComparison.ps1') -Destination (Join-Path $caseRoot 'scripts/Test-StartupSplashComparison.ps1')
 foreach($name in @('splash-on-plain','splash-off-plain','splash-on-repeat')){
  $variant=if($name -eq 'splash-off-plain'){'Off'}else{'On'}
  $ms=if($variant -eq 'Off'){67}else{568}
  if($variant -eq 'Off' -and $harnessCase -eq 'slow-off'){$ms=201}
  if($variant -eq 'Off' -and $harnessCase -eq 'small-reduction'){$ms=180}
  $folder=Join-Path $caseRoot ('work/startup-cpu/'+$name+'/Player/ForgeCoreDemoAcceptance-synthetic')
  $pass=@{completed=$true;checkpointRoundtrip=$true;kills=3;shots=14;recordedFootsteps=20}
  $report=@{synthetic=$true;width=1920;height=1080;device='Direct3D12';directDamageUsed=$false;playerTeleportUsed=$false;recordedFootstepBankId='gbox_subway_hardfloor_v1';audioClips=34;
   passes=@($pass,$pass,$pass);timingDiagnostics=@{schemaVersion=3;checkpointDispatch='queued_nonblocking';warmupMilliseconds=2000;provisionalGameplayMaximumMilliseconds=100;
    wholeSessionSmoothnessAccepted=$false;truncated=$false;measuredGameplayMaximumPassed=$true;unityReadyMilliseconds=2500;unityAwakeMilliseconds=2000;
    events=@(@{frame=1;phase='warmup';wallMilliseconds=$ms});phases=@(@{phase='gameplay';maximumWallMilliseconds=20})}}
  if($harnessCase -eq 'legacy'){$report.timingDiagnostics.schemaVersion=2}
  if($harnessCase -eq 'incomplete'){$report.passes=@($pass)}
  if($harnessCase -eq 'warmup'){$report.timingDiagnostics.warmupMilliseconds=3000}
  $reportPath=Join-Path $folder 'report.json';Write-HarnessJson $reportPath $report
  $identity=@{}
  $identity[('ForgeCore-Splash'+$variant+'_Data/Managed/ForgeCoreGameTemplates.Runtime.dll')]='e3a56176ab7625ed409ef142c62c6229f369c091a0bab22efc96231916d890e9'
  $identity[('ForgeCore-Splash'+$variant+'_Data/level0')]='3c2aa7d31f436e920e60ef3063f9393285f72d0b8887c9eabc9ab78c33bb5a0e'
  if($harnessCase -eq 'runtime'){$identity[('ForgeCore-Splash'+$variant+'_Data/Managed/ForgeCoreGameTemplates.Runtime.dll')]='0'*64}
  $receipt=@{schemaVersion=2;profiled=($harnessCase -eq 'profiled');exitCode=0;reportFolder=$folder;variant=$variant;identity=$identity;reportSha256=(Get-FileHash -LiteralPath $reportPath).Hash.ToLowerInvariant()}
  if($harnessCase -eq 'changed-report'){$receipt.reportSha256='0'*64}
  if($harnessCase -eq 'escaped-path'){$receipt.reportFolder=$caseRoot}
  Write-HarnessJson (Join-Path $caseRoot ('work/startup-cpu/'+$name+'/receipt.json')) $receipt
 }
 $failed=$false
 try{& (Join-Path $caseRoot 'scripts/Test-StartupSplashComparison.ps1') -Attempt result | Out-Null}catch{$failed=$true}
 if(($harnessCase -eq 'valid' -and $failed) -or ($harnessCase -ne 'valid' -and -not $failed)){throw ('Comparator contract failed: '+$harnessCase)}
 Write-Output ('SYNTHETIC_COMPARATOR_PASS '+$harnessCase)
}
Write-HarnessJson (Join-Path $harnessRoot 'receipt.json') @{synthetic=$true;checks=$harnessCases.Count;passed=$true;qualification='Comparator tests only, never real game/performance evidence.'}
