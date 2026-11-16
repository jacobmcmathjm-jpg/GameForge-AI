[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$reviewRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$reviewRoot=Join-Path $reviewRepo 'outputs/ForgeCore-CourtyardReview-20261031-V1'
if(Test-Path -LiteralPath $reviewRoot){throw 'Review export exists; no overwrite.'}
$reviewProject='D:\ForgeCore\Workspace\Games\ForgeCore'
$reviewReceipt=Get-Content -LiteralPath (Join-Path $reviewProject '.forgecore/unity/character-animation-evidence/96a12bab028448c2beee24aa937771a6.json') -Raw|ConvertFrom-Json
if($reviewReceipt.scenePath -cne 'Assets/Scenes/CharacterAnimations/nightly-yard-6f51b245faf5.unity' -or $reviewReceipt.templateKitVersion -cne '0.16.87' -or $reviewReceipt.visualReviewed -or $reviewReceipt.stageComplete){throw 'Wrong technical candidate identity.'}
[IO.Directory]::CreateDirectory($reviewRoot)|Out-Null
$reviewFiles=@('graphics-comparison.png','graphics-report.json')
foreach($reviewName in $reviewFiles){
    $reviewEntry=@($reviewReceipt.artifacts|Where-Object {[IO.Path]::GetFileName($_.path) -ceq $reviewName})
    if($reviewEntry.Count -ne 1 -or $reviewEntry[0].path -cne ('Assets/ForgeCore/CharacterAnimations/nightly-yard-motion/'+$reviewName)){throw 'Wrong graphics artifact identity.'}
    $reviewSource=Join-Path $reviewProject $reviewEntry[0].path
    if((Get-Item -LiteralPath $reviewSource).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'No linked review payload.'}
    if((Get-FileHash -LiteralPath $reviewSource).Hash.ToLowerInvariant() -cne $reviewEntry[0].sha256){throw 'Graphics artifact changed.'}
    [IO.File]::Copy($reviewSource,(Join-Path $reviewRoot $reviewName),$false)
}
$reviewRuntime=Join-Path $reviewProject 'Previews/Courtyard/nightly-yard-6f51b245faf5-runtime.png'
if((Get-FileHash -LiteralPath $reviewRuntime).Hash.ToLowerInvariant() -cne 'fd9985ae2a2cfef5a154584dc53da81b01894b8c90b612642196babcc19e6b7e'){throw 'Runtime capture changed.'}
[IO.File]::Copy($reviewRuntime,(Join-Path $reviewRoot 'runtime.png'),$false)
[IO.File]::Copy((Join-Path $reviewRepo 'docs/outcomes/2026-10-31-courtyard-graphics-pipeline.md'),(Join-Path $reviewRoot 'README.md'),$false)
$reviewPayload=@(Get-ChildItem -LiteralPath $reviewRoot -File|Sort-Object Name|ForEach-Object {[ordered]@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})
$reviewManifest=[ordered]@{schemaVersion=1;kind='ForgeCore-courtyard-technical-review';humanArtAccepted=$false;fileCount=$reviewPayload.Count;totalBytes=($reviewPayload|ForEach-Object {[long]$_.bytes}|Measure-Object -Sum).Sum;files=$reviewPayload;
    comparison='Three static editor views: left baseline, right candidate. Separate runtime.png is an actual play-mode capture. Neither is a concept render or proof of target-image parity.'}
$reviewStream=[IO.File]::Open((Join-Path $reviewRoot 'review-manifest.json'),[IO.FileMode]::CreateNew)
try{$reviewBytes=[Text.Encoding]::UTF8.GetBytes(($reviewManifest|ConvertTo-Json -Depth 6));$reviewStream.Write($reviewBytes,0,$reviewBytes.Length)}finally{$reviewStream.Dispose()}
[pscustomobject]@{Folder=$reviewRoot;PayloadFiles=$reviewPayload.Count;ManifestSha256=(Get-FileHash -LiteralPath (Join-Path $reviewRoot 'review-manifest.json')).Hash.ToLowerInvariant()}
