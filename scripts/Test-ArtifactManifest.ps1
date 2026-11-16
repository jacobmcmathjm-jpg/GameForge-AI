[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][ValidateSet('Delivery','Review','CourtyardReview','VisualReview','IndieReview','HeroReview','Source')][string]$Kind,
    [Parameter(Mandatory)][string]$ReceiptPath
)
$ErrorActionPreference='Stop'
$artifactRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifactManifest=[IO.Path]::GetFullPath($ManifestPath)
$artifactRoot=[IO.Path]::GetDirectoryName($artifactManifest)
$artifactReceipt=[IO.Path]::GetFullPath($ReceiptPath)
if(-not $artifactManifest.StartsWith($artifactRepo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
    -not $artifactReceipt.StartsWith((Join-Path $artifactRepo 'work')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
    $artifactReceipt.StartsWith($artifactRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {throw 'Verify repository artifacts only; receipts must be outside the payload in repository work.'}
if(Test-Path -LiteralPath $artifactReceipt){throw 'Verification receipt exists; no overwrite.'}
function Assert-ArtifactPlain([string]$Path) {
    $walk=[IO.Path]::GetFullPath($Path)
    while($walk -and $walk -ne [IO.Path]::GetPathRoot($walk)) {
        if((Test-Path -LiteralPath $walk) -and ((Get-Item -LiteralPath $walk -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {throw 'Artifact verification rejects reparse points.'}
        $walk=[IO.Path]::GetDirectoryName($walk)
    }
}
Assert-ArtifactPlain $artifactManifest
$info=Get-Item -LiteralPath $artifactManifest
if($info.PSIsContainer -or $info.Length -lt 2 -or $info.Length -gt 5MB){throw 'Artifact manifest is not bounded JSON.'}
$manifest=Get-Content -LiteralPath $artifactManifest -Raw | ConvertFrom-Json
if($Kind -eq 'Delivery') {
    if($manifest.schema -ne 1 -or $manifest.kind -notin @('ForgeCore-self-contained-win-x64','Unity-development-test-game')){throw 'Unsupported delivery manifest.'}
}elseif($Kind -eq 'Review') {
    if($manifest.schemaVersion -ne 1 -or $manifest.kind -cne 'ForgeCore-refined-industrial-source-and-Unity-review' -or
        $manifest.humanApproved -isnot [bool] -or $manifest.automaticPlacementApproved -isnot [bool] -or
        $manifest.humanApproved -cne $false -or $manifest.automaticPlacementApproved -cne $false){throw 'Review package must not assert approval.'}
}elseif($Kind -eq 'CourtyardReview') {
    if($manifest.schemaVersion -ne 1 -or $manifest.kind -cne 'ForgeCore-courtyard-technical-review' -or
        $manifest.humanArtAccepted -isnot [bool] -or $manifest.humanArtAccepted -cne $false -or
        $manifest.fileCount -ne 4){throw 'Courtyard technical review must have the exact bounded unaccepted payload.'}
}elseif($Kind -eq 'VisualReview') {
    if($manifest.schemaVersion -ne 1 -or $manifest.kind -cne 'ForgeCore-visual-polish-technical-review' -or
        $manifest.humanArtAccepted -isnot [bool] -or $manifest.humanArtAccepted -cne $false -or
        $manifest.fileCount -ne 7){throw 'Visual review must have exactly seven unaccepted payloads.'}
}elseif($Kind -eq 'HeroReview') {
    if($manifest.schemaVersion -ne 1 -or $manifest.kind -cne 'ForgeCore-indie-weapon-review' -or $manifest.humanArtAccepted -isnot [bool] -or $manifest.humanArtAccepted -cne $false -or $manifest.sourceHandsChanged -isnot [bool] -or $manifest.sourceHandsChanged -cne $false -or $manifest.fileCount -ne 4){throw 'Hero technical review requires four unaccepted payloads and no hand anatomy overclaim.'}
}elseif($Kind -eq 'IndieReview') {
    if($manifest.schemaVersion -ne 1 -or $manifest.kind -cne 'ForgeCore-indie-environment-review' -or $manifest.humanArtAccepted -isnot [bool] -or $manifest.humanArtAccepted -cne $false -or $manifest.fileCount -ne 4){throw 'Indie technical review requires exactly four unaccepted payloads.'}
}else {
    if($manifest.schemaVersion -notin @(1,2) -or $manifest.kind -cne 'sanitized-source-checkpoint'){throw 'Unsupported source checkpoint manifest.'}
}
$files=@($manifest.files)
if($files.Count -lt 1 -or $files.Count -gt 10000 -or $files.Count -ne $manifest.fileCount){throw 'Artifact cardinality contract failed.'}
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
[long]$total=0
# Validate all paths before reading any payload. No links, private-state extensions or ambiguous duplicate paths.
foreach($file in $files) {
    $relative=[string]$file.path
    if([string]::IsNullOrWhiteSpace($relative) -or $relative.Contains('\') -or $relative.StartsWith('/') -or $relative.Contains(':') -or
        $relative.Split('/') -contains '..' -or $relative.Split('/') -contains '.' -or $relative.Split('/') -contains '' -or
        $relative -match '[\x00-\x1f]' -or $relative -match '(?i)(^|/)\.env($|\.)|\.(db|db-shm|db-wal|sqlite|sqlite3|gguf|pem|key)$' -or
        -not $seen.Add($relative) -or $file.sha256 -cnotmatch '^[a-f0-9]{64}$' -or $file.bytes -lt 0 -or $file.bytes -gt 1GB) {throw 'Invalid, duplicate, private-state or unbounded artifact entry.'}
    $full=[IO.Path]::GetFullPath((Join-Path $artifactRoot $relative))
    if(-not $full.StartsWith($artifactRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $full -ieq $artifactManifest){throw 'Artifact entry escaped root or includes its own manifest.'}
    Assert-ArtifactPlain $full
}
foreach($file in $files) {
    $full=Join-Path $artifactRoot $file.path;$item=Get-Item -LiteralPath $full
    if($item.PSIsContainer -or $item.Length -ne $file.bytes -or (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant() -cne $file.sha256){throw ('Artifact changed: '+$file.path)}
    $total+=$item.Length
}
if($total -ne $manifest.totalBytes){throw 'Artifact total byte count changed.'}
$actual=@(Get-ChildItem -LiteralPath $artifactRoot -Force -Recurse)
if($actual | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}){throw 'Unexpected reparse point in artifact folder.'}
if(@($actual | Where-Object {-not $_.PSIsContainer}).Count -ne $files.Count+1){throw 'Artifact contains unlisted or missing files.'}
$receipt=[ordered]@{schemaVersion=1;kind=$Kind;manifest=[IO.Path]::GetRelativePath($artifactRepo,$artifactManifest).Replace('\','/');
    manifestSha256=(Get-FileHash -LiteralPath $artifactManifest -Algorithm SHA256).Hash.ToLowerInvariant();fileCount=$files.Count;totalBytes=$total;
    everyPayloadVerified=$true;createdUtc=[DateTime]::UtcNow.ToString('O');qualification='Exact current path/cardinality/length/SHA-256 readback only. Not human rights, visual, runtime, gameplay or independent restore-build acceptance.'}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($artifactReceipt))|Out-Null
$stream=[IO.File]::Open($artifactReceipt,[IO.FileMode]::CreateNew)
try{$data=[Text.UTF8Encoding]::new($false).GetBytes(($receipt|ConvertTo-Json -Depth 4));$stream.Write($data,0,$data.Length)}finally{$stream.Dispose()}
$receipt|ConvertTo-Json -Depth 4
