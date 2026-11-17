[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('V5','V6','V7','V8','Encounters-v1')][string]$Candidate,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$ExpectedManifestSha256,
    [ValidatePattern('^[a-z0-9-]{1,24}$')][string]$Attempt='delivered'
)
$ErrorActionPreference='Stop'
$connectedRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$source=Join-Path $connectedRepo ('outputs/ForgeCore-TestGame-20261117-ConnectedLevel'+$Candidate)
if($Candidate -ceq 'Encounters-v1'){$source=Join-Path $connectedRepo 'outputs/ForgeCore-TestGame-20261117-ConnectedEncountersV1'}
$copy=Join-Path $connectedRepo ('work/delivered-connected-'+$Candidate.ToLowerInvariant()+'-'+$Attempt)
$stem='standalone-demo-connected-'+$Candidate.ToLowerInvariant()+'-'+$Attempt
$receipt=Join-Path $connectedRepo ('work/s08-s10-verification/'+$stem+'.json')
$log=Join-Path $connectedRepo ('work/s08-s10-verification/'+$stem+'.log')
$verified=Join-Path $connectedRepo ('work/connected-'+$Candidate.ToLowerInvariant()+'-'+$Attempt+'-payload-verified.json')
foreach($path in @($copy,$receipt,$log,$verified)){if(Test-Path -LiteralPath $path){throw 'Fresh delivered-game proof paths required; no overwrite.'}}
$manifestPath=Join-Path $source 'delivery-manifest.json'
if((Get-FileHash -LiteralPath $manifestPath).Hash.ToLowerInvariant() -cne $ExpectedManifestSha256){throw 'Delivered manifest differs from the explicitly reviewed identity.'}
& (Join-Path $PSScriptRoot 'Test-ArtifactManifest.ps1') -ManifestPath $manifestPath -Kind Delivery -ReceiptPath $verified
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
if($manifest.kind -cne 'Unity-development-test-game' -or $manifest.fileCount -ne 336){throw 'Exact bounded Unity delivery required.'}
foreach($file in $manifest.files){
    $from=Join-Path $source $file.path;$to=Join-Path $copy $file.path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to))|Out-Null
    [IO.File]::Copy($from,$to,$false)
    if((Get-FileHash -LiteralPath $to).Hash.ToLowerInvariant() -cne $file.sha256){throw 'Relocated game payload differs.'}
}
if(Get-Process Unity,blender -ErrorAction SilentlyContinue){throw 'Editors must be idle for the owned relocated-player test.'}
$exe=Join-Path $copy 'ForgeCore-Connected.exe'
$owned=Start-Process -FilePath $exe -WorkingDirectory $copy -WindowStyle Hidden -PassThru -ArgumentList @('-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-forgecore-demo-probe','-forgecore-timing-probe','-logFile',('"'+$log+'"'))
if(-not $owned.WaitForExit(260000)){Stop-Process -Id $owned.Id -ErrorAction SilentlyContinue;throw 'Only the owned relocated-player probe timed out; evidence retained.'}
$owned.Refresh()
$folders=@(Get-ChildItem -LiteralPath $copy -Directory -Filter 'ForgeCoreDemoAcceptance-*')
if($folders.Count -ne 1){throw 'Expected one fresh relocated-player report.'}
$report=Get-Content -LiteralPath (Join-Path $folders[0].FullName 'report.json') -Raw|ConvertFrom-Json
$payload=[ordered]@{schemaVersion=1;kind='actual_relocated_connected_game';executable=$exe;exeSha256=(Get-FileHash -LiteralPath $exe).Hash.ToLowerInvariant();manifestSha256=$ExpectedManifestSha256;copiedAndHashVerifiedFiles=336;
    originalDeliveryModified=$false;projectPathPassedToPlayer=$false;externalFileReadsTraced=$false;exitCode=$owned.ExitCode;folder=$folders[0].FullName;report=$report;humanAcceptance=$false;
    qualification='Exact delivered payload copied into fresh work storage and run at real 1080p through three controller/projectile missions. No Unity project argument. External file reads are not traced; this is not proof that all external dependencies are absent, human art acceptance or hardware certification.'}
$stream=[IO.File]::Open($receipt,[IO.FileMode]::CreateNew)
try{$bytes=[Text.Encoding]::UTF8.GetBytes(($payload|ConvertTo-Json -Depth 15));$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}
if($owned.ExitCode -ne 0 -or $report.error -or $report.width -ne 1920 -or $report.height -ne 1080 -or $report.device -eq 'Null' -or $report.passes.Count -ne 3 -or $report.directDamageUsed -or $report.playerTeleportUsed -or -not $report.muted -or -not $report.storageScope.StartsWith($copy+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Relocated actual game check failed; raw evidence retained.'}
if($report.layoutProfile -cne 'industrial_connected_v1' -or $report.checkpointProfile -cne 'industrial_connected_v1' -or $report.recordedFootstepBankId -cne 'gbox_subway_hardfloor_v1' -or $report.audioClips -ne 34){throw 'Relocated level/save/audio profile differs.'}
foreach($pass in $report.passes){if(-not $pass.completed -or -not $pass.checkpointRoundtrip -or $pass.visitedZones -ne 7 -or -not $pass.gateChecks -or $pass.kills -ne 3 -or $pass.shots -lt 9 -or $pass.travelled -lt 110 -or $pass.recordedFootsteps -le 15 -or $pass.idleSeconds -lt 8){throw 'Relocated actual mission incomplete.'}}
& (Join-Path $PSScriptRoot 'Test-CurrentGameTiming.ps1') -Candidate ('connected-'+$Candidate.ToLowerInvariant()) -Attempt $Attempt | Out-Null
$report.passes|Select-Object preset,completed,travelled,visitedZones,gateChecks,checkpointRoundtrip
