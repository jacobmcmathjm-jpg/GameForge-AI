[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]{2,79}$')][string]$Label,
    [ValidatePattern('^[a-z0-9-]{1,24}$')][string]$Attempt='first',
    [switch]$RunHostTests
)
$ErrorActionPreference='Stop'
$restoreRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$restoreCheckpoint=Join-Path $restoreRepo ('backups/'+$Label)
$restoreZip=Join-Path $restoreCheckpoint ('ForgeCore-source-'+$Label+'.zip')
$restoreSource=Join-Path $restoreRepo ('work/restored-checkpoints/'+$Label+'-'+$Attempt)
$restoreLogs=Join-Path $restoreRepo ('work/three-hour-assets/restore-'+$Label+'-'+$Attempt)
$restoreCache=Join-Path $restoreRepo 'work/dotnet-home/.nuget/packages'
function Assert-RestorePlainPath([string]$Path) {
    $walk=[IO.Path]::GetFullPath($Path)
    while($walk -and $walk -ne [IO.Path]::GetPathRoot($walk)) {
        if((Test-Path -LiteralPath $walk) -and ((Get-Item -LiteralPath $walk -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Restore verification rejects reparse points.'}
        $walk=[IO.Path]::GetDirectoryName($walk)
    }
}
foreach($path in @($restoreZip,$restoreSource,$restoreLogs,$restoreCache)){Assert-RestorePlainPath $path}
foreach($path in @($restoreSource,$restoreLogs)){if(Test-Path -LiteralPath $path){throw 'Restore probe destination exists; no overwrite.'}}
if(-not (Test-Path -LiteralPath $restoreCache -PathType Container)){throw 'Offline package cache is unavailable.'}
$archive=[IO.Compression.ZipFile]::OpenRead($restoreZip)
try {
    $manifestEntry=$archive.GetEntry('source-checkpoint-manifest.json');if($null -eq $manifestEntry -or $manifestEntry.Length -gt 5MB){throw 'No bounded source manifest.'}
    $reader=[IO.StreamReader]::new($manifestEntry.Open());try{$manifest=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
    if($manifest.schemaVersion -notin @(1,2) -or $manifest.kind -cne 'sanitized-source-checkpoint' -or $archive.Entries.Count -ne $manifest.fileCount+1){throw 'Source archive cardinality/schema failed.'}
    foreach($file in $manifest.files) {
        if($file.path -notmatch '^[A-Za-z0-9_./-]+$' -or $file.path.StartsWith('/') -or $file.path.EndsWith('/') -or
            $file.path.Split('/') -contains '..' -or $file.path.Split('/') -contains '.' -or $file.path.Split('/') -contains ''){throw 'Unsafe source archive path.'}
        $entry=$archive.GetEntry($file.path)
        if($null -eq $entry -or $entry.Length -ne $file.bytes){throw 'Source archive length failed.'}
        if((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000 -or ($entry.ExternalAttributes -band [int][IO.FileAttributes]::ReparsePoint)){throw 'Source archive link entry is not allowed.'}
        $stream=$entry.Open();try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}finally{$stream.Dispose()}
        if($hash -cne $file.sha256){throw 'Source archive content changed.'}
    }
}finally{$archive.Dispose()}
[IO.Directory]::CreateDirectory($restoreLogs)|Out-Null
[IO.Compression.ZipFile]::ExtractToDirectory($restoreZip,$restoreSource)
$example=[IO.File]::ReadAllBytes((Join-Path $restoreSource 'appsettings.example.json'))
$snapshotConfig=[IO.File]::ReadAllBytes((Join-Path $restoreSource 'src/ForgeCore.Desktop/appsettings.json'))
if(-not [Linq.Enumerable]::SequenceEqual[byte]($example,$snapshotConfig)){throw 'Restored configuration is not the safe example.'}
$config=[IO.File]::ReadAllText((Join-Path $restoreSource 'appsettings.example.json'))|ConvertFrom-Json
if($config.LocalModel.EnableLlamaSharp -or $config.ForgeCore.EnableSeeleGeneration -or $config.ForgeCore.EnableMeshyGeneration -or $config.ForgeCore.EnableOnlineCatalogSearch){throw 'Restored source configuration must remain offline.'}
function Invoke-RestoreDotnet([string]$Step,[string[]]$Arguments) {
    $start=[Diagnostics.ProcessStartInfo]::new('dotnet');$start.WorkingDirectory=$restoreSource;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $restoreRepo 'work/dotnet-home';$start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($start);if($null -eq $process){throw 'No owned dotnet process.'}
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(600000)){$process.Kill($true);throw 'Owned restore/build probe exceeded ten minutes.'}
        $text=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
        $bytes=[Text.Encoding]::UTF8.GetBytes($text);$file=[IO.File]::Open((Join-Path $restoreLogs ($Step+'.log')),[IO.FileMode]::CreateNew)
        try{$file.Write($bytes,0,$bytes.Length)}finally{$file.Dispose()}
        if($process.ExitCode -ne 0){throw ('Independent restored-source step failed: '+$Step+'; retained bounded local log.')}
        Write-Output ('RESTORED_SOURCE_PASS '+$Step)
    }finally{$process.Dispose()}
}
# Exclusive local source and disabled audit prevent network/feed calls. No normal app, model, game or live database is started.
Invoke-RestoreDotnet 'offline-restore' @('restore','ForgeCore.sln','--source',$restoreCache,'--disable-parallel','-p:NuGetAudit=false','-v','minimal')
$assetFiles=@(Get-ChildItem -LiteralPath (Join-Path $restoreSource 'src'),(Join-Path $restoreSource 'tests') -Filter project.assets.json -Recurse -File)
if($assetFiles.Count -lt 8){throw 'Incomplete independent package restore.'}
foreach($file in $assetFiles) {
    $assets=Get-Content -LiteralPath $file.FullName -Raw|ConvertFrom-Json
    foreach($source in $assets.project.restore.sources.PSObject.Properties.Name){if([IO.Path]::GetFullPath($source).TrimEnd('\') -ine $restoreCache.TrimEnd('\')){throw 'Restore used a source other than the exclusive local package cache.'}}
}
Invoke-RestoreDotnet 'release-build' @('build','ForgeCore.sln','--no-restore','-c','Release','-v','minimal')
if($RunHostTests) {
    foreach($suite in @('Core','Agent','Security')) {
        Invoke-RestoreDotnet ($suite.ToLowerInvariant()+'-tests') @('test',('tests/ForgeCore.'+$suite+'.Tests/ForgeCore.'+$suite+'.Tests.csproj'),'--no-restore','--no-build','-c','Release','--logger',('trx;LogFileName='+$suite.ToLowerInvariant()+'.trx'),'--results-directory',$restoreLogs,'-v','minimal')
    }
}
$receipt=[ordered]@{schemaVersion=1;checkpointLabel=$Label;archiveSha256=(Get-FileHash -LiteralPath $restoreZip).Hash.ToLowerInvariant();sourceFiles=$manifest.fileCount;archivePayloadHashesVerified=$true;
    exclusiveLocalPackageSource=$true;releaseBuildPassed=$true;hostTestsRequested=[bool]$RunHostTests;normalAppStarted=$false;liveDatabaseOpened=$false;inferenceStarted=$false;
    qualification='Independent extraction/offline restore/Release build of this exact sanitized source ZIP. Optional host suites use disposable fixtures. Not a Unity project/game/art/rights acceptance test.'}
$output=[IO.File]::Open((Join-Path $restoreLogs 'restore-verification.json'),[IO.FileMode]::CreateNew)
try{$data=[Text.Encoding]::UTF8.GetBytes(($receipt|ConvertTo-Json -Depth 5));$output.Write($data,0,$data.Length)}finally{$output.Dispose()}
$receipt|ConvertTo-Json -Depth 5
