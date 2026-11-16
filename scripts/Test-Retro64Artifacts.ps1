[CmdletBinding()]
param([ValidateSet('V1','V2')][string]$Revision='V1')
$ErrorActionPreference='Stop'
$retroProject='D:\ForgeCore\Workspace\Games\ForgeCore'
$retroRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$retroOutput=Join-Path $retroRepo ('work/s08-s10-verification/retro64-'+$Revision.ToLowerInvariant()+'-artifact-audit.json')
if(Test-Path -LiteralPath $retroOutput){throw 'Audit exists; no overwrite.'}
$retroCurrentRelative='.forgecore/unity/character-animation-evidence/7a9bc8b4c849476c913619f3d6997d7f.json'
if($Revision -ceq 'V2'){
    $retroMatches=@(Get-ChildItem -LiteralPath (Join-Path $retroProject '.forgecore/unity/character-animation-evidence') -File -Filter '*.json'|Where-Object{(Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json).scenePath -ceq 'Assets/Scenes/Retro/Industrial-Retro64-v2.unity'})
    if($retroMatches.Count -ne 1){throw 'Expected exactly one V2 generation receipt.'}
    $retroCurrentRelative=[IO.Path]::GetRelativePath($retroProject,$retroMatches[0].FullName)
}
$retroOldRelative='.forgecore/unity/character-animation-evidence/c9eda7a1c44c4ea29e5b5d12c442b425.json'
$retroCurrent=Get-Content -LiteralPath (Join-Path $retroProject $retroCurrentRelative) -Raw|ConvertFrom-Json
$retroOld=Get-Content -LiteralPath (Join-Path $retroProject $retroOldRelative) -Raw|ConvertFrom-Json
function Confirm-RetroArtifact([string]$Relative,[string]$Hash){
    $retroPath=[IO.Path]::GetFullPath((Join-Path $retroProject $Relative))
    if(-not $retroPath.StartsWith($retroProject+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Artifact path escaped project.'}
    if((Get-Item -LiteralPath $retroPath).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'No linked artifacts.'}
    if((Get-FileHash -LiteralPath $retroPath).Hash.ToLowerInvariant() -cne $Hash){throw ('Artifact changed: '+$Relative)}
}
$retroKit=if($Revision -ceq 'V2'){'0.16.92'}else{'0.16.91'}
if($retroCurrent.selectedStyle -cne 'retro_64' -or $retroCurrent.templateKitVersion -cne $retroKit -or $retroCurrent.artifacts.Count -ne 314 -or $retroOld.artifacts.Count -ne 272){throw 'Wrong candidate/evidence cardinality.'}
foreach($retroReceipt in @($retroCurrent,$retroOld)){
    Confirm-RetroArtifact $retroReceipt.scenePath $retroReceipt.sceneSha256
    Confirm-RetroArtifact $retroReceipt.previewPath $retroReceipt.previewSha256
    foreach($retroArtifact in $retroReceipt.artifacts){Confirm-RetroArtifact $retroArtifact.path $retroArtifact.sha256}
}
$retroRetained=@($retroOld.artifacts|Where-Object {$_.path -like '*.anim' -or $_.path -like '*.wav'})
if($retroRetained.Count -ne 42){throw 'Expected eight clips and 34 audio cues.'}
foreach($retroOriginal in $retroRetained){
    $retroMatches=@($retroCurrent.artifacts|Where-Object {[IO.Path]::GetFileName($_.path) -ceq [IO.Path]::GetFileName($retroOriginal.path)})
    if($retroMatches.Count -ne 1 -or $retroMatches[0].sha256 -cne $retroOriginal.sha256){throw 'Retro altered an existing action clip or audio cue.'}
}
$retroPayload=[ordered]@{schemaVersion=1;kind='retro64_exact_artifact_readback';currentArtifacts=314;previousModernArtifacts=272;unchangedActionClips=8;unchangedAudioCues=34;
    currentEvidenceSha256=(Get-FileHash -LiteralPath (Join-Path $retroProject $retroCurrentRelative)).Hash.ToLowerInvariant();previousEvidenceSha256=(Get-FileHash -LiteralPath (Join-Path $retroProject $retroOldRelative)).Hash.ToLowerInvariant();sceneSha256=$retroCurrent.sceneSha256;humanArtAccepted=$false;
    qualification='Both generation manifests and all listed payloads independently hash-checked. Original pose sheet is source-rig evidence before retro styling; use final runtime captures for retro appearance. Retained modern scene checked unchanged.'}
$retroStream=[IO.File]::Open($retroOutput,[IO.FileMode]::CreateNew)
try{$retroBytes=[Text.Encoding]::UTF8.GetBytes(($retroPayload|ConvertTo-Json -Depth 4));$retroStream.Write($retroBytes,0,$retroBytes.Length)}finally{$retroStream.Dispose()}
$retroPayload|ConvertTo-Json -Depth 4
