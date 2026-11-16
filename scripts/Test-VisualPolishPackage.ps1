[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$packageRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$packageRoot=Join-Path $packageRepo 'outputs/ForgeCore-Windows-x64-20261104-VisualPolishV2'
$packageExe=Join-Path $packageRoot 'ForgeCore.Desktop.exe'
$packageReceipt=Join-Path $packageRepo 'work/s08-s10-verification/visual-polish-v2-package.json'
$packagePlanner=Join-Path $packageRepo 'work/s08-s10-verification/visual-polish-v2-delivered-planner.json'
foreach($packagePath in @($packageReceipt,$packagePlanner)){if(Test-Path -LiteralPath $packagePath){throw 'Probe evidence exists; no overwrite.'}}
if((Get-FileHash (Join-Path $packageRoot 'appsettings.json')).Hash.ToLowerInvariant() -cne 'b84302a01a8e7bd6e808747ef549d5d1c9b52c0fe033fda32cb5f6950641266d'){throw 'Verified offline configuration differs.'}
$packageModules=@('Core','Agent','LocalAI','Memory','Security','Tools')
foreach($packageModule in $packageModules){
    $packageFile='ForgeCore.'+$packageModule+'.dll'
    $packageExpected=Join-Path $packageRepo ('src/ForgeCore.'+$packageModule+'/bin/Release/net10.0/'+$packageFile)
    if((Get-FileHash $packageExpected).Hash -cne (Get-FileHash (Join-Path $packageRoot $packageFile)).Hash){throw 'Delivered module differs from tested Release source.'}
}
function Invoke-VisualPackageProbe([string[]]$Arguments) {
    $packageProcess=Start-Process -FilePath $packageExe -WorkingDirectory $packageRoot -WindowStyle Hidden -PassThru -ArgumentList $Arguments
    if(-not $packageProcess.WaitForExit(20000)){Stop-Process -Id $packageProcess.Id -ErrorAction SilentlyContinue;throw 'Only the owned offline probe timed out.'}
    $packageProcess.Refresh();if($packageProcess.ExitCode -ne 0){throw ('Offline packaged probe failed: '+$packageProcess.ExitCode)}
    return $packageProcess.ExitCode
}
$packageDependencyExit=Invoke-VisualPackageProbe @('--verify-package')
$packagePlannerExit=Invoke-VisualPackageProbe @('--verify-courtyard-pipeline',('"'+$packagePlanner+'"'))
$packagePlan=Get-Content $packagePlanner -Raw|ConvertFrom-Json
if($packagePlan.actionCount -ne 11 -or $packagePlan.realToolExecution -or $packagePlan.providerFallback -or $packagePlan.databaseOpened -or $packagePlan.humanArtAccepted){throw 'Wrong deterministic offline planner scope.'}
$packagePayload=[ordered]@{schemaVersion=1;dependencyExitCode=$packageDependencyExit;plannerExitCode=$packagePlannerExit;plannerActions=11;releaseModulesMatched=6;
    offlineConfigurationSha256='b84302a01a8e7bd6e808747ef549d5d1c9b52c0fe033fda32cb5f6950641266d';plannerSha256=(Get-FileHash $packagePlanner).Hash.ToLowerInvariant();databaseOpened=$false;humanArtAccepted=$false;
    qualification='Delivered EXE dependency and actual deterministic planner checks with synthetic observations only. No normal app, live database, model/provider or Unity tools opened.'}
$packageStream=[IO.File]::Open($packageReceipt,[IO.FileMode]::CreateNew)
try{$packageBytes=[Text.Encoding]::UTF8.GetBytes(($packagePayload|ConvertTo-Json -Depth 4));$packageStream.Write($packageBytes,0,$packageBytes.Length)}finally{$packageStream.Dispose()}
$packagePayload|ConvertTo-Json -Depth 4
