[CmdletBinding()]
param([ValidateSet('V1','V2')][string]$Revision='V1')
$ErrorActionPreference='Stop'
$retroRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$retroSource=Join-Path $retroRepo ('outputs/ForgeCore-TestGame-20261105-Retro64'+$Revision)
$retroStem='retro64-'+$Revision.ToLowerInvariant()
$retroCopy=Join-Path $retroRepo ('work/delivered-'+$retroStem+'-runtime')
$retroEvidence=Join-Path $retroRepo ('work/s08-s10-verification/'+$retroStem+'-portable-copy.json')
if((Test-Path -LiteralPath $retroCopy) -or (Test-Path -LiteralPath $retroEvidence)){throw 'Fresh copy/receipt required; no overwrite.'}
$retroManifestPath=Join-Path $retroSource 'delivery-manifest.json'
$retroHash='fe44a75d29f1267ece96aa4cab0db1c91e2bbc2602d1ffc80f5a4f5633b237f7'
if($Revision -ceq 'V2'){$retroHash='9e513b233015bcb2324c83b684152d1b254a97cc9623a0ebdb739058bf3eb8c9'}
if((Get-FileHash -LiteralPath $retroManifestPath).Hash.ToLowerInvariant() -cne $retroHash){throw 'Unexpected delivered game.'}
$retroManifest=Get-Content -LiteralPath $retroManifestPath -Raw|ConvertFrom-Json
$retroExpectedBytes=if($Revision -ceq 'V2'){210883710}else{210883664}
if($retroManifest.fileCount -ne 336 -or $retroManifest.totalBytes -ne $retroExpectedBytes){throw 'Wrong payload cardinality/bytes.'}
$retroSeen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($retroFile in $retroManifest.files){
    if($retroFile.path.Split('/') -contains '..' -or $retroFile.path.Contains(':') -or $retroFile.path.StartsWith('/') -or -not $retroSeen.Add($retroFile.path)){throw 'Unsafe/duplicate payload path.'}
    $retroFrom=[IO.Path]::GetFullPath((Join-Path $retroSource $retroFile.path));$retroTo=[IO.Path]::GetFullPath((Join-Path $retroCopy $retroFile.path))
    if(-not $retroFrom.StartsWith($retroSource+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not $retroTo.StartsWith($retroCopy+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Copy escaped exact folders.'}
    if((Get-Item -LiteralPath $retroFrom).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'No linked payloads.'}
    if((Get-Item -LiteralPath $retroFrom).Length -ne $retroFile.bytes -or (Get-FileHash -LiteralPath $retroFrom).Hash.ToLowerInvariant() -cne $retroFile.sha256){throw 'Delivery payload changed.'}
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($retroTo))|Out-Null
    [IO.File]::Copy($retroFrom,$retroTo,$false)
    if((Get-FileHash -LiteralPath $retroTo).Hash.ToLowerInvariant() -cne $retroFile.sha256){throw 'Copied payload changed.'}
}
if($retroSeen.Count -ne 336){throw 'Copy incomplete.'}
$retroPayload=[ordered]@{schemaVersion=1;kind='exact_retro64_delivered_payload_copy';manifestSha256=$retroHash;copiedAndHashVerifiedFiles=336;copy=$retroCopy;originalDeliveryModified=$false;externalFileReadsTraced=$false;gameplayTested=$false}
$retroStream=[IO.File]::Open($retroEvidence,[IO.FileMode]::CreateNew)
try{$retroBytes=[Text.Encoding]::UTF8.GetBytes(($retroPayload|ConvertTo-Json -Depth 4));$retroStream.Write($retroBytes,0,$retroBytes.Length)}finally{$retroStream.Dispose()}
$retroPayload|ConvertTo-Json -Depth 4
