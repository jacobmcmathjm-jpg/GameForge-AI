[CmdletBinding()]
param([ValidateSet('V1','V2')][string]$Revision='V1')
$ErrorActionPreference='Stop'
$retroRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$retroProject='D:\ForgeCore\Workspace\Games\ForgeCore'
$retroDestination=Join-Path $retroRepo ('outputs/ForgeCore-Retro64Review-20261105-'+$Revision)
if(Test-Path -LiteralPath $retroDestination){throw 'Review already exists; no overwrite.'}
$retroFiles=@(
    @{source='Previews/Retro/Industrial-Retro64-v1-runtime.png';name='Runtime.png';hash='630b20cad3fc559fa8e5df5ff89b0ad3533d35c9d6b4eddb431ccf02ab66a096'},
    @{source='Previews/Retro/Industrial-Retro64-v1-runtime.png.forgecore-preview.json';name='Runtime.provenance.json'},
    @{source='Assets/ForgeCore/CharacterAnimations/retro-yard-v1/retro64-report.json';name='retro64-report.json'})
if($Revision -ceq 'V2'){
    foreach($retroFile in $retroFiles){$retroFile.source=$retroFile.source.Replace('v1','v2')}
    $retroFiles[0].hash='5a008a749753124c69d78784db73feb9fccecd952ba175dec4eba37593557fb7'
}
[IO.Directory]::CreateDirectory($retroDestination)|Out-Null
foreach($retroFile in $retroFiles){
    $retroFrom=Join-Path $retroProject $retroFile.source
    if((Get-Item -LiteralPath $retroFrom).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'No linked review input.'}
    if($retroFile.hash -and (Get-FileHash -LiteralPath $retroFrom).Hash.ToLowerInvariant() -cne $retroFile.hash){throw 'Actual runtime capture changed.'}
    [IO.File]::Copy($retroFrom,(Join-Path $retroDestination $retroFile.name),$false)
}
$retroReadme=@'
# Original N64-inspired technical candidate

Runtime.png is an actual 1280x720 Unity play-mode camera capture, not a concept illustration. It has no IMGUI HUD. Its adjacent provenance binds the scene/camera capture. retro64-report.json describes the final active original art derivative, not the memory or contents of the whole build.

536 triangles per guard, 144 arm triangles, 120 rifle/magazine triangles, 4,960 active template-prop triangles; eight original 32-128px painted textures measure 710,544 runtime bytes. Simple vertex lighting/fog, modern controls and retained audio. No original GoldenEye models, textures, ROM content or music were used. Not a licensed remake or exact N64 hardware emulation.

The pose sheet generated during source preparation is deliberately not included: it shows the original source rig before retro styling. Existing detailed source/maps and intermediate reports are retained by the pipeline; this report does not claim the whole executable is an N64-sized asset set. Human visual/rights/source-vault approval is not asserted. Same small courtyard, not an expanded level or every-genre builder. User visual acceptance and motion/face tuning remain separate from technical passes.
'@
[IO.File]::WriteAllText((Join-Path $retroDestination 'README.md'),$retroReadme,[Text.UTF8Encoding]::new($false))
$retroPayload=@(Get-ChildItem -LiteralPath $retroDestination -File|Sort-Object Name|ForEach-Object{[ordered]@{path=$_.Name;bytes=[long]$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})
if($retroPayload.Count -ne 4){throw 'Expected exact four-file review.'}
[long]$retroTotal=0;foreach($retroFile in $retroPayload){$retroTotal+=$retroFile.bytes}
$retroManifest=[ordered]@{schemaVersion=1;kind='ForgeCore-courtyard-technical-review';profile='retro_64';humanArtAccepted=$false;fileCount=4;totalBytes=$retroTotal;files=$retroPayload}
$retroStream=[IO.File]::Open((Join-Path $retroDestination 'review-manifest.json'),[IO.FileMode]::CreateNew)
try{$retroBytes=[Text.Encoding]::UTF8.GetBytes(($retroManifest|ConvertTo-Json -Depth 6));$retroStream.Write($retroBytes,0,$retroBytes.Length)}finally{$retroStream.Dispose()}
$retroDestination
