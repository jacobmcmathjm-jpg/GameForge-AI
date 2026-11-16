[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$portableRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$portableSource=Join-Path $portableRepo 'outputs/ForgeCore-TestGame-20261104-VisualPolishV2'
$portableCopy=Join-Path $portableRepo 'work/delivered-visual-polish-v2-runtime'
$portableEvidence=Join-Path $portableRepo 'work/s08-s10-verification/visual-polish-v2-delivered-game.json'
$portableLog=Join-Path $portableRepo 'work/s08-s10-verification/visual-polish-v2-delivered-game.log'
foreach($portablePath in @($portableCopy,$portableEvidence,$portableLog)){if(Test-Path -LiteralPath $portablePath){throw 'Portable-game proof destination exists; no overwrite.'}}
$portableManifestPath=Join-Path $portableSource 'delivery-manifest.json'
if((Get-FileHash -LiteralPath $portableManifestPath).Hash.ToLowerInvariant() -cne '58529a9d3b8a530e2c3f13fbc788c52801aa2ef2a0943146228f13979c74ca14'){throw 'Unexpected delivered-game manifest.'}
$portableManifest=Get-Content -LiteralPath $portableManifestPath -Raw|ConvertFrom-Json
if($portableManifest.fileCount -ne 336 -or $portableManifest.totalBytes -ne 240725763){throw 'Wrong delivered payload.'}
[IO.Directory]::CreateDirectory($portableCopy)|Out-Null
foreach($portableFile in $portableManifest.files){
    if($portableFile.path.Split('/') -contains '..' -or $portableFile.path.Contains(':') -or $portableFile.path.StartsWith('/')){throw 'Unsafe delivered relative path.'}
    $portableFrom=[IO.Path]::GetFullPath((Join-Path $portableSource $portableFile.path))
    $portableTo=[IO.Path]::GetFullPath((Join-Path $portableCopy $portableFile.path))
    if(-not $portableFrom.StartsWith($portableSource+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not $portableTo.StartsWith($portableCopy+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Portable copy escaped its exact root.'}
    if((Get-Item -LiteralPath $portableFrom).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'No linked delivered payload.'}
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($portableTo))|Out-Null
    [IO.File]::Copy($portableFrom,$portableTo,$false)
    if((Get-FileHash -LiteralPath $portableTo).Hash.ToLowerInvariant() -cne $portableFile.sha256){throw 'Copied delivered payload changed.'}
}
if(Get-Process Unity,blender -ErrorAction SilentlyContinue){throw 'Editors must be idle for the owned portable-player test.'}
$portableExe=Join-Path $portableCopy 'polished-yard.exe'
$portableProcess=Start-Process -FilePath $portableExe -WorkingDirectory $portableCopy -WindowStyle Hidden -PassThru -ArgumentList @('-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-forgecore-demo-probe','-logFile',('"'+$portableLog+'"'))
if(-not $portableProcess.WaitForExit(260000)){Stop-Process -Id $portableProcess.Id -ErrorAction SilentlyContinue;throw 'Only the owned portable diagnostic timed out; evidence retained.'}
$portableProcess.Refresh()
$portableFolders=@(Get-ChildItem -LiteralPath $portableCopy -Directory -Filter 'ForgeCoreDemoAcceptance-*')
if($portableFolders.Count -ne 1){throw 'Expected one fresh portable-game probe report.'}
$portableReport=Get-Content -LiteralPath (Join-Path $portableFolders[0].FullName 'report.json') -Raw|ConvertFrom-Json
if($portableProcess.ExitCode -ne 0 -or $portableReport.error -or $portableReport.width -ne 1920 -or $portableReport.height -ne 1080 -or $portableReport.device -eq 'Null' -or
   $portableReport.passes.Count -ne 3 -or $portableReport.directDamageUsed -or $portableReport.playerTeleportUsed -or -not $portableReport.muted -or -not $portableReport.storageScope.StartsWith($portableCopy+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Portable exported-game diagnostic failed; no acceptance inferred.'}
foreach($portablePass in $portableReport.passes){
    if(-not $portablePass.completed -or -not $portablePass.checkpointRoundtrip -or $portablePass.kills -ne 3 -or $portablePass.shots -lt 9 -or $portablePass.travelled -lt 45 -or $portablePass.recordedFootsteps -le 15 -or $portablePass.idleSeconds -lt 8){throw 'Portable mission run incomplete.'}
}
$portablePayload=[ordered]@{schemaVersion=1;kind='actual_delivered_game_copy_run';manifestSha256='58529a9d3b8a530e2c3f13fbc788c52801aa2ef2a0943146228f13979c74ca14';copiedAndHashVerifiedFiles=336;
    originalDeliveryModified=$false;projectPathPassedToPlayer=$false;externalFileReadsTraced=$false;exitCode=$portableProcess.ExitCode;folder=$portableFolders[0].FullName;report=$portableReport;
    qualification='Exact delivered payload copied into a fresh isolated work folder and executed there at real 1080p through three controller/projectile missions. Diagnostic saves stay in that work copy. External file reads are not traced. Not human art/listening acceptance, unrestricted generation or broad hardware certification.'}
$portableStream=[IO.File]::Open($portableEvidence,[IO.FileMode]::CreateNew)
try{$portableBytes=[Text.Encoding]::UTF8.GetBytes(($portablePayload|ConvertTo-Json -Depth 15));$portableStream.Write($portableBytes,0,$portableBytes.Length)}finally{$portableStream.Dispose()}
$portableReport.passes|Select-Object preset,completed,kills,shots,travelled,@{n='maxMs';e={$_.frames.maximum}}
