[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$indieRepo=Split-Path $PSScriptRoot -Parent
$indieProject='D:/ForgeCore/Workspace/Games/ForgeCore'
$indieOutput=Join-Path $indieRepo 'outputs/ForgeCore-IndieReview-20261116-V2'
if(Test-Path -LiteralPath $indieOutput){throw 'Fresh review destination required.'}
$indieEvidence=Get-Content (Join-Path $indieProject '.forgecore/unity/character-animation-evidence/51e77f866d8d4337af0102a2a3c4233b.json') -Raw|ConvertFrom-Json
if($indieEvidence.templateKitVersion -cne '0.16.96' -or $indieEvidence.visualReviewed -or $indieEvidence.stageComplete -or $indieEvidence.indieEnvironmentReport.profile -cne 'grounded_indie_environment_v1'){throw 'Expected unaccepted indie technical candidate.'}
$indieImage=Join-Path $indieProject 'Previews/Indie/Runtime-v1.png'
$indieProvenance=Get-Content -LiteralPath ($indieImage+'.forgecore-preview.json') -Raw|ConvertFrom-Json
if($indieProvenance.sceneSha256 -cne $indieEvidence.sceneSha256 -or $indieProvenance.pngSha256 -cne (Get-FileHash -LiteralPath $indieImage).Hash.ToLowerInvariant() -or $indieProvenance.width -ne 1280 -or $indieProvenance.height -ne 720){throw 'Camera capture identity differs.'}
New-Item -ItemType Directory -Path $indieOutput|Out-Null
Copy-Item -LiteralPath $indieImage -Destination (Join-Path $indieOutput 'runtime.png')
Copy-Item -LiteralPath ($indieImage+'.forgecore-preview.json') -Destination (Join-Path $indieOutput 'runtime-provenance.json')
$indieReport=@($indieEvidence.artifacts|Where-Object {$_.path.EndsWith('/indie-environment-report.json')})
if($indieReport.Count -ne 1){throw 'Unique environment report required.'}
$indieReportSource=Join-Path $indieProject $indieReport[0].path
if((Get-FileHash -LiteralPath $indieReportSource).Hash.ToLowerInvariant() -cne $indieReport[0].sha256){throw 'Environment report changed.'}
Copy-Item -LiteralPath $indieReportSource -Destination (Join-Path $indieOutput 'indie-environment-report.json')
$indieReadme="Actual ForgeCore indie environment candidate, 16 November 2026.`n`nRuntime image: 1280 x 720 camera capture, not a concept image; IMGUI overlays omitted. New original sky, palette/light balance and facade details over the detailed industrial recipe. Still a modest prototype, not final indie/AA art. Weapon/hands and character art remain next. Same small 32 m navigation envelope; background buildings are not accessible levels. No new human art/rights approval. Existing accepted audio and eight clips retained. Separate app/game deliveries: 20261116-IndieEnvironmentV1. Original retro/modern deliveries remain unchanged.`n"
[IO.File]::WriteAllText((Join-Path $indieOutput 'README.md'),$indieReadme,[Text.UTF8Encoding]::new($false))
$indieFiles=@(Get-ChildItem -LiteralPath $indieOutput -File|Sort-Object Name|ForEach-Object {[ordered]@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})
[long]$indieTotal=0;foreach($indieFile in $indieFiles){$indieTotal+=$indieFile.bytes}
$indieManifest=[ordered]@{schemaVersion=1;kind='ForgeCore-indie-environment-review';humanArtAccepted=$false;fileCount=$indieFiles.Count;totalBytes=$indieTotal;files=$indieFiles}
[IO.File]::WriteAllText((Join-Path $indieOutput 'review-manifest.json'),($indieManifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Test-ArtifactManifest.ps1') -ManifestPath (Join-Path $indieOutput 'review-manifest.json') -Kind IndieReview -ReceiptPath (Join-Path $indieRepo 'work/indie-review-verified.json')
