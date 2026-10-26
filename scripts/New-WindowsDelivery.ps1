[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishedApp,
    [Parameter(Mandatory)][string]$UnityGameFolder,
    [Parameter(Mandatory)][string]$UnityExecutableName,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]{1,60}$')][string]$Label
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$outputRoot = Join-Path $repoRoot 'outputs'
$appSource = (Get-Item -LiteralPath $PublishedApp).FullName
$gameSource = (Get-Item -LiteralPath $UnityGameFolder).FullName
if ([IO.Path]::GetFileName($UnityExecutableName) -cne $UnityExecutableName -or
    [IO.Path]::GetExtension($UnityExecutableName) -ine '.exe') { throw 'Expected an executable filename, not a path.' }
$gameStem = [IO.Path]::GetFileNameWithoutExtension($UnityExecutableName)
foreach ($file in @('ForgeCore.Desktop.exe', 'coreclr.dll', 'ForgeCore.Desktop.runtimeconfig.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $appSource $file) -PathType Leaf)) { throw "Missing app payload: $file" }
}
foreach ($file in @($UnityExecutableName, 'UnityPlayer.dll', "$UnityExecutableName.forgecore-build.json")) {
    if (-not (Test-Path -LiteralPath (Join-Path $gameSource $file) -PathType Leaf)) { throw "Missing game payload: $file" }
}
if (-not (Test-Path -LiteralPath (Join-Path $gameSource ($gameStem + '_Data')) -PathType Container)) { throw 'Missing Unity game data folder.' }

function New-DeliveryFolder([string]$Source, [string]$Destination, [bool]$App) {
    $resolvedDestination = [IO.Path]::GetFullPath($Destination)
    if (-not $resolvedDestination.StartsWith($outputRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Deliveries must remain under this repository outputs directory.'
    }
    if (Test-Path -LiteralPath $resolvedDestination) { throw "Refusing to overwrite an existing delivery: $resolvedDestination" }
    $items = @(Get-Item -LiteralPath $Source) + @(Get-ChildItem -LiteralPath $Source -Recurse -Force)
    if ($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Payload must not contain reparse points.' }
    $files = @($items | Where-Object { -not $_.PSIsContainer })
    [IO.Directory]::CreateDirectory($resolvedDestination) | Out-Null
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($Source, $file.FullName)
        if ($App -and $relative -match '^runtimes[\\/]' -and $relative -notmatch '^runtimes[\\/]win-x64[\\/]') { continue }
        if ($relative -match '(?i)(^|[\\/])\.env($|\.)|\.(db|db-shm|db-wal|gguf)$') { throw 'Private state or model weights must not be bundled.' }
        $target = Join-Path $resolvedDestination $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        [IO.File]::Copy($file.FullName, $target, $false)
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/Windows-delivery.md') -Destination (Join-Path $resolvedDestination 'README.md')
    $payload = @(Get-ChildItem -LiteralPath $resolvedDestination -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path=[IO.Path]::GetRelativePath($resolvedDestination, $_.FullName).Replace('\','/'); bytes=$_.Length;
            sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    $manifest = [ordered]@{ schema=1; createdUtc=[DateTime]::UtcNow.ToString('O'); kind=$(if ($App) { 'ForgeCore-self-contained-win-x64' } else { 'Unity-development-test-game' });
        fileCount=$payload.Count; totalBytes=($payload | ForEach-Object { [long]$_.bytes } | Measure-Object -Sum).Sum; files=$payload }
    $manifestPath = Join-Path $resolvedDestination 'delivery-manifest.json'
    $stream = [IO.File]::Open($manifestPath, [IO.FileMode]::CreateNew)
    try {
        $writer = [IO.StreamWriter]::new($stream)
        try { $writer.WriteLine(($manifest | ConvertTo-Json -Depth 8)) } finally { $writer.Dispose() }
    } finally { $stream.Dispose() }
    [pscustomobject]@{ Folder=$resolvedDestination; Files=$payload.Count; Bytes=$manifest.totalBytes }
}

New-DeliveryFolder $appSource (Join-Path $outputRoot "ForgeCore-Windows-x64-$Label") $true
New-DeliveryFolder $gameSource (Join-Path $outputRoot "ForgeCore-TestGame-$Label") $false
