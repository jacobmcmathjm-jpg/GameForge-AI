[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]{1,30}$')][string]$Attempt)
$ErrorActionPreference='Stop'
$encounterRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$encounterGame='D:\ForgeCore\Workspace\Games\ForgeCore\Builds\Connected-Encounters-20261117-V1'
$encounterExe=Join-Path $encounterGame 'ForgeCore-Connected.exe'
$encounterOut=Join-Path $encounterRepo 'work/connected-encounters-v1'
[IO.Directory]::CreateDirectory($encounterOut)|Out-Null
$encounterReceipt=Join-Path $encounterOut ($Attempt+'-player.json')
$encounterLog=Join-Path $encounterOut ($Attempt+'-player.log')
foreach($encounterPath in @($encounterReceipt,$encounterLog)){if(Test-Path -LiteralPath $encounterPath){throw 'Fresh attempt required; preserve prior evidence.'}}
if(Get-Process Unity,blender -ErrorAction SilentlyContinue){throw 'Editors must be idle.'}
if(-not(Test-Path -LiteralPath $encounterExe -PathType Leaf)){throw 'Expected exact exported encounter candidate.'}
$encounterBefore=@(Get-ChildItem -LiteralPath $encounterGame -Directory -Filter 'ForgeCoreEncounterProbe-*' | ForEach-Object FullName)
$encounterProcess=Start-Process -FilePath $encounterExe -WorkingDirectory $encounterGame -WindowStyle Hidden -PassThru -ArgumentList @('-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-logFile',('"'+$encounterLog+'"'),'-forgecore-connected-encounters-probe')
if(-not $encounterProcess.WaitForExit(45000)){Stop-Process -Id $encounterProcess.Id -ErrorAction SilentlyContinue;throw 'Only the owned encounter fixture timed out; evidence retained.'}
$encounterProcess.Refresh()
$encounterNew=@(Get-ChildItem -LiteralPath $encounterGame -Directory -Filter 'ForgeCoreEncounterProbe-*' | Where-Object FullName -NotIn $encounterBefore)
if($encounterNew.Count -ne 1){throw 'Expected one owned new fixture report folder.'}
$encounterRaw=Join-Path $encounterNew[0].FullName 'report.json'
if((Get-Item -LiteralPath $encounterRaw).Length -gt 32768){throw 'Fixture report exceeds bounded input.'}
$encounterReport=Get-Content -LiteralPath $encounterRaw -Raw | ConvertFrom-Json
if($encounterReport.scene -cne 'Assets/Scenes/Indie/Industrial-Connected-Encounters-v1.unity'){throw 'Wrong exported encounter scene.'}
$encounterState=[IO.Path]::GetFullPath($encounterReport.storageRoot)
if(-not $encounterState.StartsWith($encounterGame+'\',[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($encounterState) -notmatch '^ForgeCoreDemoProbe-[a-f0-9]{32}$'){throw 'Normal or external save root refused.'}
$encounterOk=$encounterProcess.ExitCode -eq 0 -and -not $encounterReport.error -and $encounterReport.schemaVersion -eq 1 -and $encounterReport.kind -ceq 'connected_room_controlled_return_fire_fixture' -and $encounterReport.device -ne 'Null' -and $encounterReport.controlledRepositioning -eq $true -and $encounterReport.directDamageUsed -eq $false -and $encounterReport.missionTraversalAccepted -eq $false -and $encounterReport.humanBalanceAccepted -eq $false -and $encounterReport.encounters.Count -eq 3
for($encounterIndex=0;$encounterIndex -lt $encounterReport.encounters.Count;$encounterIndex++){
    $encounterItem=$encounterReport.encounters[$encounterIndex]
    $encounterZone=if($encounterIndex -eq 2){'CONTROL ROOM'}else{'WAREHOUSE'}
    if($encounterItem.guardIndex -ne $encounterIndex -or $encounterItem.zone -cne $encounterZone -or $encounterItem.healthBefore -ne 100 -or $encounterItem.healthAfter -ge 100 -or $encounterItem.healthAfter -le 0 -or $encounterItem.shots -lt 1 -or $encounterItem.markers -lt 1 -or $encounterItem.projectilePeak -lt 1 -or $encounterItem.elapsed -le 0 -or $encounterItem.elapsed -gt 7 -or $encounterItem.clearLane -ne $true -or $encounterItem.pausedWithoutShotsOrDamage -ne $true){$encounterOk=$false}
}
$encounterPayload=[ordered]@{schemaVersion=1;passed=$encounterOk;exitCode=$encounterProcess.ExitCode;rawReport=$encounterRaw;rawSha256=(Get-FileHash -LiteralPath $encounterRaw).Hash.ToLowerInvariant();report=$encounterReport;qualification='Controlled room repositioning and actual AI/animation/projectile damage, not traversal, art, frame performance or human combat balance acceptance.'}
$encounterBytes=[Text.Encoding]::UTF8.GetBytes(($encounterPayload|ConvertTo-Json -Depth 10))
$encounterStream=[IO.File]::Open($encounterReceipt,[IO.FileMode]::CreateNew)
try{$encounterStream.Write($encounterBytes,0,$encounterBytes.Length)}finally{$encounterStream.Dispose()}
$encounterPayload|ConvertTo-Json -Depth 10
if(-not $encounterOk){throw 'Exported room encounter fixture failed; evidence retained.'}
