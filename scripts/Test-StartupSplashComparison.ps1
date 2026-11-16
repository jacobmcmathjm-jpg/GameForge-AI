[CmdletBinding()]
param([ValidatePattern('^[a-z0-9-]{1,30}$')][string]$Attempt='comparison-v1')
$ErrorActionPreference='Stop'
$startupRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$startupRoot=Join-Path $startupRepo 'work/startup-cpu'
$startupTarget=Join-Path $startupRoot ($Attempt+'.json')
if(Test-Path -LiteralPath $startupTarget){throw 'Comparison exists; preserve prior evidence.'}
$startupResults=@()
foreach($startupName in @('splash-on-plain','splash-off-plain','splash-on-repeat')){
 $startupReceiptPath=Join-Path $startupRoot ($startupName+'/receipt.json')
 $startupReceipt=Get-Content -LiteralPath $startupReceiptPath -Raw|ConvertFrom-Json
 if($startupReceipt.schemaVersion -ne 2 -or $startupReceipt.profiled -or $startupReceipt.exitCode -ne 0){throw 'Expected unprofiled successful receipt.'}
 $startupFolder=[IO.Path]::GetFullPath([string]$startupReceipt.reportFolder)
 if(-not $startupFolder.StartsWith((Join-Path $startupRoot ($startupName+'/Player/')),[StringComparison]::OrdinalIgnoreCase)){throw 'Report folder escaped its owned player.'}
 $startupReportPath=Join-Path $startupFolder 'report.json'
 if((Get-FileHash -LiteralPath $startupReportPath).Hash.ToLowerInvariant() -cne $startupReceipt.reportSha256){throw 'Report identity changed.'}
 $startupReport=Get-Content -LiteralPath $startupReportPath -Raw|ConvertFrom-Json
 if($startupReport.error -or $startupReport.passes.Count -ne 3 -or $startupReport.width -ne 1920 -or $startupReport.height -ne 1080 -or
    $startupReport.device -notlike 'Direct3D*' -or $startupReport.directDamageUsed -or $startupReport.playerTeleportUsed -or
    $startupReport.recordedFootstepBankId -cne 'gbox_subway_hardfloor_v1' -or $startupReport.audioClips -ne 34){throw 'Missing real current-game graphics/audio/combat evidence.'}
 foreach($startupPass in $startupReport.passes){if(-not $startupPass.completed -or -not $startupPass.checkpointRoundtrip -or $startupPass.kills -ne 3 -or $startupPass.shots -lt 9 -or $startupPass.recordedFootsteps -le 15){throw 'Incomplete mission.'}}
 $startupData=$startupReport.timingDiagnostics
 if($startupData.schemaVersion -ne 3 -or $startupData.checkpointDispatch -cne 'queued_nonblocking' -or $startupData.warmupMilliseconds -ne 2000 -or
    $startupData.provisionalGameplayMaximumMilliseconds -ne 100 -or $startupData.wholeSessionSmoothnessAccepted -ne $false -or $startupData.truncated){throw 'Diagnostic gates changed or incomplete.'}
 $startupFirst=@($startupData.events|Where-Object frame -eq 1)
 if($startupFirst.Count -ne 1 -or $startupFirst[0].phase -cne 'warmup'){throw 'Expected first awake-to-update interval retained.'}
 $startupDllKey='ForgeCore-Splash'+$startupReceipt.variant+'_Data/Managed/ForgeCoreGameTemplates.Runtime.dll'
 if($startupReceipt.identity.$startupDllKey -cne 'e3a56176ab7625ed409ef142c62c6229f369c091a0bab22efc96231916d890e9'){throw 'Runtime is not the retained .95 game.'}
 $startupLevelKey='ForgeCore-Splash'+$startupReceipt.variant+'_Data/level0'
 if($startupReceipt.identity.$startupLevelKey -cne '3c2aa7d31f436e920e60ef3063f9393285f72d0b8887c9eabc9ab78c33bb5a0e'){throw 'Level data changed.'}
 $startupGameplay=@($startupData.phases|Where-Object phase -ceq 'gameplay')
 if($startupGameplay.Count -ne 1){throw 'Missing gameplay phase.'}
 $startupResults+= [pscustomobject]@{attempt=$startupName;variant=$startupReceipt.variant;receiptSha256=(Get-FileHash -LiteralPath $startupReceiptPath).Hash.ToLowerInvariant();
  awakeToReady=$startupData.unityReadyMilliseconds-$startupData.unityAwakeMilliseconds;firstWall=$startupFirst[0].wallMilliseconds;gameplayMaximum=$startupGameplay[0].maximumWallMilliseconds;
  gameplayGatePassed=$startupData.measuredGameplayMaximumPassed;identity=$startupReceipt.identity}
}
if($startupResults[0].variant -cne 'On' -or $startupResults[1].variant -cne 'Off' -or $startupResults[2].variant -cne 'On'){throw 'Expected on/off/on ordering.'}
$startupReduction=[Math]::Min($startupResults[0].firstWall,$startupResults[2].firstWall)-$startupResults[1].firstWall
$startupPassed=$startupReduction -ge 400 -and $startupResults[1].firstWall -lt 200
$startupComparison=[ordered]@{schemaVersion=1;runs=$startupResults;minimumFirstIntervalReductionMilliseconds=$startupReduction;splashHypothesisPassed=$startupPassed;
 qualification='Splash-only on/off/on export comparison. Startup fade attribution only, not a gameplay-freeze fix, controlled cold-cache experiment, art acceptance or whole-session smoothness. First wall interval includes startup callbacks; native/Unity clocks differ.'}
$startupStream=[IO.File]::Open($startupTarget,[IO.FileMode]::CreateNew)
try{$startupBytes=[Text.Encoding]::UTF8.GetBytes(($startupComparison|ConvertTo-Json -Depth 8));$startupStream.Write($startupBytes,0,$startupBytes.Length)}finally{$startupStream.Dispose()}
$startupComparison|ConvertTo-Json -Depth 8
if(-not $startupPassed){throw 'Splash-only reduction criterion failed; evidence retained.'}
if(@($startupResults|Where-Object {-not $_.gameplayGatePassed}).Count){throw 'A measured gameplay maximum gate failed; do not call this performance acceptance.'}
