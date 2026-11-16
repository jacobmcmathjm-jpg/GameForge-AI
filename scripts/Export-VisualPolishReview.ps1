[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$polishRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$polishProject='D:\ForgeCore\Workspace\Games\ForgeCore'
$polishOutput=Join-Path $polishRepo 'outputs/ForgeCore-VisualReview-20261104-V3'
if(Test-Path -LiteralPath $polishOutput){throw 'Review export exists; no overwrite.'}
$polishEnvelope=Get-Content (Join-Path $polishRepo 'work/s08-s10-verification/visual-polish-v2-mission.json') -Raw|ConvertFrom-Json
if(-not $polishEnvelope.Success){throw 'No successful production mission receipt.'}
$polishEvidence=@(Get-ChildItem (Join-Path $polishProject '.forgecore/unity/character-animation-evidence') -Filter '*.json' -File|ForEach-Object {
    $polishValue=Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json
    if($polishValue.scenePath -ceq 'Assets/Scenes/S13-PolishedCourtyard-v2.unity'){$polishValue}
})
if($polishEvidence.Count -ne 1){throw 'Expected one exact candidate receipt.'}
$polishEvidence=$polishEvidence[0]
if($polishEvidence.templateKitVersion -cne '0.16.90' -or $polishEvidence.visualReviewed -or $polishEvidence.stageComplete -or -not $polishEvidence.heroFinishReport.socketsUnchanged){throw 'Wrong technical candidate.'}
if((Get-FileHash (Join-Path $polishProject $polishEvidence.scenePath)).Hash.ToLowerInvariant() -cne $polishEvidence.sceneSha256){throw 'Candidate scene changed.'}
foreach($polishEntry in $polishEvidence.artifacts){
    $polishSource=[IO.Path]::GetFullPath((Join-Path $polishProject $polishEntry.path))
    if(-not $polishSource.StartsWith($polishProject+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Get-Item $polishSource).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Invalid source artifact path.'}
    if((Get-FileHash $polishSource).Hash.ToLowerInvariant() -cne $polishEntry.sha256){throw 'Candidate source artifact changed.'}
}
[IO.Directory]::CreateDirectory($polishOutput)|Out-Null
foreach($polishName in @('prop-comparison.png','graphics-comparison.png','prop-presentation-report.json','graphics-report.json','visual-polish-report.json')){
    $polishEntry=@($polishEvidence.artifacts|Where-Object {[IO.Path]::GetFileName($_.path) -ceq $polishName})
    if($polishEntry.Count -ne 1){throw 'Exact unique review artifact required.'}
    [IO.File]::Copy((Join-Path $polishProject $polishEntry[0].path),(Join-Path $polishOutput $polishName),$false)
}
$polishRuntime=Join-Path $polishProject 'Previews/Graphics/S13-PolishedCourtyard-v2-runtime.png'
$polishRuntimeReceipt=Get-Content (Join-Path $polishRepo 'work/s08-s10-verification/visual-polish-v2-runtime.json') -Raw|ConvertFrom-Json
if(-not $polishRuntimeReceipt.Success){throw 'No successful runtime camera capture.'}
$polishProvenance=Get-Content -LiteralPath ($polishRuntime+'.forgecore-preview.json') -Raw|ConvertFrom-Json
if($polishProvenance.scenePath -cne $polishEvidence.scenePath -or $polishProvenance.sceneSha256 -cne $polishEvidence.sceneSha256 -or
    $polishProvenance.width -ne 1280 -or $polishProvenance.height -ne 720 -or
    (Get-FileHash -LiteralPath $polishRuntime).Hash.ToLowerInvariant() -cne $polishProvenance.pngSha256){throw 'Runtime screenshot identity/provenance changed.'}
[IO.File]::Copy($polishRuntime,(Join-Path $polishOutput 'runtime.png'),$false)
[IO.File]::Copy((Join-Path $polishRepo 'docs/outcomes/2026-11-04-visual-polish.md'),(Join-Path $polishOutput 'README.md'),$false)
$polishPayload=@(Get-ChildItem $polishOutput -File|Sort-Object Name|ForEach-Object {[ordered]@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}})
$polishManifest=[ordered]@{schemaVersion=1;kind='ForgeCore-visual-polish-technical-review';humanArtAccepted=$false;fileCount=$polishPayload.Count;totalBytes=($polishPayload|ForEach-Object {[long]$_.bytes}|Measure-Object -Sum).Sum;files=$polishPayload;
    scope='Original template candidate. Static comparisons show left baseline/right candidate for props then surfaces, before final hero/lighting pass. runtime.png is an actual final play-mode camera capture without IMGUI HUD; not concept art, approved library placement or AA parity.'}
$polishStream=[IO.File]::Open((Join-Path $polishOutput 'review-manifest.json'),[IO.FileMode]::CreateNew)
try{$polishBytes=[Text.Encoding]::UTF8.GetBytes(($polishManifest|ConvertTo-Json -Depth 6));$polishStream.Write($polishBytes,0,$polishBytes.Length)}finally{$polishStream.Dispose()}
$polishManifest|Select-Object kind,fileCount,totalBytes
