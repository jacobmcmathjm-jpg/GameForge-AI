[CmdletBinding()]
param([ValidateSet('V1','V2')][string]$Revision='V1')
$ErrorActionPreference='Stop'
$retroRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$retroRoot=Join-Path $retroRepo ('outputs/ForgeCore-Windows-x64-20261105-Retro64'+$Revision)
$retroExe=Join-Path $retroRoot 'ForgeCore.Desktop.exe'
$retroEvidence=Join-Path $retroRepo 'work/s08-s10-verification'
$retroStem='retro64-'+$Revision.ToLowerInvariant()
$retroReceipt=Join-Path $retroEvidence ($retroStem+'-delivered-package.json')
if(Test-Path -LiteralPath $retroReceipt){throw 'Receipt exists; no overwrite.'}
$retroConfigHash='b84302a01a8e7bd6e808747ef549d5d1c9b52c0fe033fda32cb5f6950641266d'
if((Get-FileHash -LiteralPath (Join-Path $retroRoot 'appsettings.json')).Hash.ToLowerInvariant() -cne $retroConfigHash){throw 'Expected verified offline config.'}
foreach($retroModule in @('Core','Agent','LocalAI','Memory','Security','Tools')){
    $retroFile='ForgeCore.'+$retroModule+'.dll'
    $retroExpected=Join-Path $retroRepo ('src/ForgeCore.'+$retroModule+'/bin/Release/net10.0/'+$retroFile)
    if((Get-FileHash -LiteralPath $retroExpected).Hash -cne (Get-FileHash -LiteralPath (Join-Path $retroRoot $retroFile)).Hash){throw 'Delivered module differs from Release build.'}
}
function Invoke-RetroPackageProbe([string[]]$Arguments){
    $retroProcess=Start-Process -FilePath $retroExe -WorkingDirectory $retroRoot -WindowStyle Hidden -PassThru -ArgumentList $Arguments
    if(-not $retroProcess.WaitForExit(20000)){Stop-Process -Id $retroProcess.Id -ErrorAction SilentlyContinue;throw 'Only the owned offline probe timed out.'}
    $retroProcess.Refresh();if($retroProcess.ExitCode -ne 0){throw ('Packaged probe failed: '+$retroProcess.ExitCode)}
}
Invoke-RetroPackageProbe @('--verify-package')
$retroPlans=[Collections.Generic.List[object]]::new()
foreach($retroStyle in @('default','retro_64')){
    $retroPlanPath=Join-Path $retroEvidence ($retroStem+'-delivered-'+$retroStyle+'-planner.json')
    if(Test-Path -LiteralPath $retroPlanPath){throw 'Planner receipt exists; no replay.'}
    $retroFlag=if($retroStyle -ceq 'retro_64'){'--verify-retro-courtyard-pipeline'}else{'--verify-courtyard-pipeline'}
    Invoke-RetroPackageProbe @($retroFlag,('"'+$retroPlanPath+'"'))
    $retroPlan=Get-Content -LiteralPath $retroPlanPath -Raw|ConvertFrom-Json
    if($retroPlan.actionCount -ne 11 -or $retroPlan.selectedStyle -cne $retroStyle -or $retroPlan.realToolExecution -or $retroPlan.providerFallback -or $retroPlan.databaseOpened -or $retroPlan.humanArtAccepted){throw 'Wrong planner scope.'}
    $retroPlans.Add([ordered]@{style=$retroStyle;actionCount=11;sha256=(Get-FileHash -LiteralPath $retroPlanPath).Hash.ToLowerInvariant()})
}
$retroPayload=[ordered]@{schemaVersion=1;kind='delivered_retro64_app_offline_probes';dependencyExitCode=0;releaseModulesMatched=6;offlineConfigurationSha256=$retroConfigHash;plans=$retroPlans;
    databaseOpened=$false;realToolExecution=$false;humanAccepted=$false;qualification='Actual delivered desktop assembly, dependency load and two deterministic planner routes with synthetic observations. No normal UI conversation, model/provider, database or Unity tools opened.'}
$retroStream=[IO.File]::Open($retroReceipt,[IO.FileMode]::CreateNew)
try{$retroBytes=[Text.Encoding]::UTF8.GetBytes(($retroPayload|ConvertTo-Json -Depth 6));$retroStream.Write($retroBytes,0,$retroBytes.Length)}finally{$retroStream.Dispose()}
$retroPayload|ConvertTo-Json -Depth 6
