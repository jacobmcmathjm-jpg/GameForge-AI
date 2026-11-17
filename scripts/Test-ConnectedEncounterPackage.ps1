[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$packageRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$packageRoot=Join-Path $packageRepo 'outputs/ForgeCore-Windows-x64-20261117-ConnectedEncountersV1'
$packageExe=Join-Path $packageRoot 'ForgeCore.Desktop.exe'
$packageEvidence=Join-Path $packageRepo 'work/connected-encounters-v1'
$packageReceipt=Join-Path $packageEvidence 'delivered-package.json'
if(Test-Path -LiteralPath $packageReceipt){throw 'Receipt exists; no overwrite.'}
[IO.Directory]::CreateDirectory($packageEvidence)|Out-Null
# Exact safe example from the independently verified 418-file parent checkpoint (not the live app config).
$packageConfigHash='f2bb167a526a47f4348d9a4d16bce9f9e6899bbb7f426ff8948af7eaab32bd54'
if((Get-FileHash -LiteralPath (Join-Path $packageRoot 'appsettings.json')).Hash.ToLowerInvariant() -cne $packageConfigHash){throw 'Expected sanitized offline config.'}
foreach($packageModule in @('Core','Agent','LocalAI','Memory','Security','Tools')){
    $packageFile='ForgeCore.'+$packageModule+'.dll'
    $packageExpected=Join-Path $packageRepo ('src/ForgeCore.'+$packageModule+'/bin/Release/net10.0/'+$packageFile)
    if((Get-FileHash -LiteralPath $packageExpected).Hash -cne (Get-FileHash -LiteralPath (Join-Path $packageRoot $packageFile)).Hash){throw 'Delivered module differs from Release build.'}
}
function Invoke-PackageProbe([string[]]$Arguments){
    $packageProcess=Start-Process -FilePath $packageExe -WorkingDirectory $packageRoot -WindowStyle Hidden -PassThru -ArgumentList $Arguments
    if(-not $packageProcess.WaitForExit(20000)){Stop-Process -Id $packageProcess.Id -ErrorAction SilentlyContinue;throw 'Only the owned offline probe timed out.'}
    $packageProcess.Refresh();if($packageProcess.ExitCode -ne 0){throw ('Packaged probe failed: '+$packageProcess.ExitCode)}
}
Invoke-PackageProbe @('--verify-package')
$packagePlans=[Collections.Generic.List[object]]::new()
foreach($packageRoute in @('courtyard','retro','connected')){
    $packagePath=Join-Path $packageEvidence ($packageRoute+'-planner.json')
    if(Test-Path -LiteralPath $packagePath){throw 'Planner receipt exists; no replay.'}
    $packageFlag=switch($packageRoute){'courtyard'{'--verify-courtyard-pipeline'}'retro'{'--verify-retro-courtyard-pipeline'}'connected'{'--verify-connected-industrial-pipeline'}}
    Invoke-PackageProbe @($packageFlag,('"'+$packagePath+'"'))
    $packagePlan=Get-Content -LiteralPath $packagePath -Raw|ConvertFrom-Json
    $packageStyle=if($packageRoute -ceq 'retro'){'retro_64'}else{'default'}
    $packageLayout=if($packageRoute -ceq 'connected'){'industrial_connected_v1'}else{'original_courtyard'}
    if($packagePlan.actionCount -ne 11 -or $packagePlan.selectedStyle -cne $packageStyle -or $packagePlan.selectedLayout -cne $packageLayout -or $packagePlan.realToolExecution -or $packagePlan.providerFallback -or $packagePlan.databaseOpened -or $packagePlan.humanArtAccepted){throw 'Wrong synthetic planner scope.'}
    $packagePlans.Add([ordered]@{route=$packageRoute;actionCount=11;sha256=(Get-FileHash -LiteralPath $packagePath).Hash.ToLowerInvariant()})
}
$packagePayload=[ordered]@{schemaVersion=1;dependencyExitCode=0;releaseModulesMatched=6;offlineConfigurationSha256=$packageConfigHash;plans=$packagePlans;databaseOpened=$false;realToolExecution=$false;humanAccepted=$false;qualification='Actual delivered assembly/dependency load and three deterministic planner routes with synthetic observations, not a model conversation, live tool execution, database or human art acceptance.'}
$packageStream=[IO.File]::Open($packageReceipt,[IO.FileMode]::CreateNew)
try{$packageBytes=[Text.Encoding]::UTF8.GetBytes(($packagePayload|ConvertTo-Json -Depth 6));$packageStream.Write($packageBytes,0,$packageBytes.Length)}finally{$packageStream.Dispose()}
$packagePayload|ConvertTo-Json -Depth 6
