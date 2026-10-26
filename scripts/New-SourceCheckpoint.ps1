[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]{2,79}$')][string]$Label)

$ErrorActionPreference = 'Stop'
$checkpointRepo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$checkpointBackups = Join-Path $checkpointRepo 'backups'
$checkpointRoot = [IO.Path]::GetFullPath((Join-Path $checkpointBackups $Label))
if (-not $checkpointRoot.StartsWith($checkpointBackups + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Checkpoint must stay under this repository backups directory.'
}
if (Test-Path -LiteralPath $checkpointRoot) { throw 'Checkpoint label already exists; prior checkpoints are never overwritten.' }

function Assert-CheckpointPlainPath([string]$Path) {
    $checkpointCandidate = [IO.Path]::GetFullPath($Path)
    if (-not $checkpointCandidate.StartsWith($checkpointRepo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Source checkpoint path escaped its repository.'
    }
    while ($checkpointCandidate -and $checkpointCandidate -ne $checkpointRepo) {
        if ((Test-Path -LiteralPath $checkpointCandidate) -and
            ((Get-Item -LiteralPath $checkpointCandidate -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Reparse points are not allowed in a source checkpoint.'
        }
        $checkpointCandidate = [IO.Path]::GetDirectoryName($checkpointCandidate)
    }
}

# Only source/build/docs/test text from known roots, never a recursive workspace copy.
$checkpointExtensions = @('.cs','.xaml','.csproj','.sln','.slnx','.props','.targets','.json','.md','.py','.ps1','.manifest','.config')
$checkpointFiles = @(& git -C $checkpointRepo ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate repository source files.' }
$checkpointFiles = @($checkpointFiles | Sort-Object -Unique | Where-Object {
    $checkpointRelative = $_.Replace('\','/')
    $checkpointExtension = [IO.Path]::GetExtension($checkpointRelative).ToLowerInvariant()
    $checkpointRootFile = -not $checkpointRelative.Contains('/')
    ($checkpointRelative -eq '.gitignore' -or
        (($checkpointRootFile -or $checkpointRelative -match '^(src|tests|docs|scripts)/') -and
            $checkpointExtension -in $checkpointExtensions)) -and
        $checkpointRelative -notmatch '(^|/)(bin|obj|work|outputs|backups|\.git|\.forgecore|node_modules)/' -and
        $checkpointRelative -notmatch '(^|/)(\.env($|\.)|appsettings\.(Development|Production|Local)\.json$)' -and
        $checkpointRelative -ne 'src/ForgeCore.Desktop/appsettings.json'
})
if ($checkpointFiles.Count -lt 20) { throw 'Unexpectedly small source selection; inspect before archiving.' }

$checkpointUtf8 = [Text.UTF8Encoding]::new($false, $true)
$checkpointSelected = [Collections.Generic.List[object]]::new()
$checkpointFindings = [Collections.Generic.List[object]]::new()
$checkpointSecretPatterns = [ordered]@{
    'private-key-envelope' = '-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----'
    'github-token-shape' = '(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{30,})'
    'provider-key-shape' = '\bsk-(?:(?:proj|svcacct)-)?[A-Za-z0-9_-]{24,}'
    'aws-access-key-shape' = '\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'
    'credentialed-url' = 'https?://[^\s/"''<>:]+:[^\s/"''<>@]+@'
}
foreach ($checkpointRelative in $checkpointFiles) {
    if ($checkpointRelative -notmatch '^[A-Za-z0-9_./-]+$' -or $checkpointRelative.Split('/') -contains '..') {
        throw 'Unsupported source path; manual review is required.'
    }
    $checkpointSource = Join-Path $checkpointRepo $checkpointRelative
    Assert-CheckpointPlainPath $checkpointSource
    $checkpointItem = Get-Item -LiteralPath $checkpointSource -Force
    if ($checkpointItem.PSIsContainer -or $checkpointItem.Length -gt 5MB) { throw 'Source file is not bounded text.' }
    $checkpointText = [IO.File]::ReadAllText($checkpointSource, $checkpointUtf8)
    if ($checkpointText.Contains([char]0)) { throw 'Binary content in source selection.' }
    foreach ($checkpointRule in $checkpointSecretPatterns.GetEnumerator()) {
        if ([regex]::IsMatch($checkpointText,$checkpointRule.Value)) {
            $checkpointFindings.Add([pscustomobject]@{path=$checkpointRelative; rule=$checkpointRule.Key})
        }
    }
    # A portable text checkpoint, not a byte-for-byte copy of machine-specific line endings.
    $checkpointSelected.Add([pscustomobject]@{path=$checkpointRelative.Replace('\','/'); text=$checkpointText.Replace("`r`n","`n").Replace("`r","`n")})
}
if ($checkpointFindings.Count) {
    # Never echo candidate secret values into the terminal/log.
    $checkpointFindings | ConvertTo-Json -Compress | Write-Output
    throw 'Potential secret pattern found; checkpoint stopped before any source was copied. Review these file/rule names privately.'
}
$checkpointExample = $checkpointSelected | Where-Object path -eq 'appsettings.example.json'
if ($null -eq $checkpointExample) { throw 'Safe configuration example is required.' }
$checkpointConfig = $checkpointExample.text | ConvertFrom-Json
if ($checkpointConfig.ForgeCore.EnableSeeleGeneration -or $checkpointConfig.ForgeCore.EnableMeshyGeneration -or
    $checkpointConfig.ForgeCore.EnableOnlineCatalogSearch -or $checkpointConfig.LocalModel.EnableLlamaSharp) {
    throw 'Safe configuration example must disable cloud, online search and local model loading.'
}
$checkpointSelected.Add([pscustomobject]@{path='src/ForgeCore.Desktop/appsettings.json';text=$checkpointExample.text})
$checkpointSourceRoot = Join-Path $checkpointRoot 'source'
Assert-CheckpointPlainPath $checkpointSourceRoot
[IO.Directory]::CreateDirectory($checkpointSourceRoot) | Out-Null
$checkpointManifestFiles = [Collections.Generic.List[object]]::new()
foreach ($checkpointFile in ($checkpointSelected | Sort-Object path)) {
    $checkpointTarget = Join-Path $checkpointSourceRoot $checkpointFile.path
    Assert-CheckpointPlainPath $checkpointTarget
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($checkpointTarget)) | Out-Null
    $checkpointData = $checkpointUtf8.GetBytes($checkpointFile.text)
    $checkpointStream = [IO.File]::Open($checkpointTarget,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $checkpointStream.Write($checkpointData,0,$checkpointData.Length) } finally { $checkpointStream.Dispose() }
    $checkpointSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($checkpointData)).ToLowerInvariant()
    $checkpointHeader = [Text.Encoding]::UTF8.GetBytes(('blob '+$checkpointData.Length+[char]0))
    $checkpointGitBytes = [byte[]]::new($checkpointHeader.Length + $checkpointData.Length)
    [Buffer]::BlockCopy($checkpointHeader,0,$checkpointGitBytes,0,$checkpointHeader.Length)
    [Buffer]::BlockCopy($checkpointData,0,$checkpointGitBytes,$checkpointHeader.Length,$checkpointData.Length)
    $checkpointGitSha = [Convert]::ToHexString([Security.Cryptography.SHA1]::HashData($checkpointGitBytes)).ToLowerInvariant()
    $checkpointManifestFiles.Add([ordered]@{path=$checkpointFile.path;bytes=$checkpointData.Length;sha256=$checkpointSha;gitBlobSha1=$checkpointGitSha})
}
$checkpointManifest = [ordered]@{
    schemaVersion=1;label=$Label;createdUtc=[DateTime]::UtcNow.ToString('O');kind='sanitized-source-checkpoint'
    textEncoding='UTF-8 without BOM; LF';fileCount=$checkpointManifestFiles.Count
    totalBytes=($checkpointManifestFiles | ForEach-Object {[long]$_.bytes} | Measure-Object -Sum).Sum
    configuration='Live Desktop configuration excluded; safe example substituted in this snapshot only.'
    excluded=@('Full Git history','Secrets and live configuration','Model weights','SQLite/runtime data and logs','Generated games and build outputs','External Unity project','Blender/vault binary assets')
    secretScan='Known-pattern source scan passed; not a guarantee of exhaustive secret detection.'
    files=@($checkpointManifestFiles.ToArray())
}
$checkpointManifestPath = Join-Path $checkpointSourceRoot 'source-checkpoint-manifest.json'
[IO.File]::WriteAllText($checkpointManifestPath,($checkpointManifest | ConvertTo-Json -Depth 7)+"`n",$checkpointUtf8)
$checkpointZip = Join-Path $checkpointRoot ('ForgeCore-source-'+$Label+'.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($checkpointSourceRoot,$checkpointZip,[IO.Compression.CompressionLevel]::Optimal,$false)
$checkpointArchive = [IO.Compression.ZipFile]::OpenRead($checkpointZip)
try {
    if ($checkpointArchive.Entries.Count -ne $checkpointManifest.fileCount+1) { throw 'Archive entry count does not match source checkpoint.' }
    foreach ($checkpointEntry in $checkpointManifest.files) {
        $checkpointZipEntry = $checkpointArchive.GetEntry($checkpointEntry.path)
        if ($null -eq $checkpointZipEntry -or $checkpointZipEntry.Length -ne $checkpointEntry.bytes) { throw 'Archive payload is missing or wrong length.' }
        $checkpointRead = $checkpointZipEntry.Open()
        try { $checkpointHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($checkpointRead)).ToLowerInvariant() }
        finally { $checkpointRead.Dispose() }
        if ($checkpointHash -cne $checkpointEntry.sha256) { throw 'Archive payload hash mismatch.' }
    }
} finally { $checkpointArchive.Dispose() }
[pscustomobject]@{Checkpoint=$checkpointRoot;Source=$checkpointSourceRoot;Archive=$checkpointZip;Files=$checkpointManifest.fileCount
    SourceBytes=$checkpointManifest.totalBytes;ArchiveSHA256=(Get-FileHash -LiteralPath $checkpointZip -Algorithm SHA256).Hash.ToLowerInvariant()} | ConvertTo-Json
