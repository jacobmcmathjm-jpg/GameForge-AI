[CmdletBinding()]
param([ValidatePattern('^[a-z0-9-]{1,24}$')][string]$Attempt='final')
$ErrorActionPreference='Stop'
$preserveRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$preserveProject='D:\ForgeCore\Workspace\Games\ForgeCore'
$preserveOut=Join-Path $preserveRepo ('work/s08-s10-verification/visual-polish-v2-preservation-'+$Attempt+'.json')
if(Test-Path -LiteralPath $preserveOut){throw 'Preservation receipt already exists; no overwrite.'}
function Confirm-VisualHash([string]$Relative,[string]$Expected){
    $preservePath=[IO.Path]::GetFullPath((Join-Path $preserveProject $Relative))
    if(-not $preservePath.StartsWith($preserveProject+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escaped project.'}
    if((Get-Item -LiteralPath $preservePath).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'No linked artifacts.'}
    if((Get-FileHash -LiteralPath $preservePath).Hash.ToLowerInvariant() -cne $Expected){throw ('Preservation hash changed: '+$Relative)}
}
Confirm-VisualHash 'Assets/Scenes/S10-RecordedFootsteps-v26.unity' '887887a69a073ee48da9bc611786e886fa997a93ed60377d0f7eb08be2922d45'
Confirm-VisualHash 'Assets/Scenes/CharacterAnimations/nightly-yard-6f51b245faf5.unity' 'ce18e6de8bf3bad5139721977db423294f8f44e8a946823c45e39642691c394b'
Confirm-VisualHash 'Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb' 'ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0'
$preserveRecords=@(Get-ChildItem (Join-Path $preserveProject '.forgecore/unity/character-animation-evidence') -Filter '*.json' -File|ForEach-Object {
    $preserveValue=Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json
    if($preserveValue.scenePath -ceq 'Assets/Scenes/S13-PolishedCourtyard-v2.unity'){$preserveValue}
})
if($preserveRecords.Count -ne 1){throw 'Expected one exact final production candidate.'}
$preserveRecord=$preserveRecords[0]
if($preserveRecord.templateKitVersion -cne '0.16.90' -or $preserveRecord.artifacts.Count -ne 272 -or $preserveRecord.visualReviewed -or $preserveRecord.stageComplete){throw 'Wrong final identity.'}
Confirm-VisualHash $preserveRecord.scenePath $preserveRecord.sceneSha256
foreach($preserveEntry in $preserveRecord.artifacts){Confirm-VisualHash $preserveEntry.path $preserveEntry.sha256}
$preserveAudio=Get-Content (Join-Path $preserveProject 'Assets/ForgeCore/CharacterAnimations/polished-yard-v2/demo-audio-report.json') -Raw|ConvertFrom-Json
$preserveOldAudio=Get-Content (Join-Path $preserveProject 'Assets/ForgeCore/CharacterAnimations/nightly-yard-motion/demo-audio-report.json') -Raw|ConvertFrom-Json
if($preserveAudio.clips.Count -ne 34 -or $preserveOldAudio.clips.Count -ne 34){throw 'Exact 34-clip bank required.'}
foreach($preserveClip in $preserveAudio.clips){
    $preserveMatch=@($preserveOldAudio.clips|Where-Object file -CEQ $preserveClip.file)
    if($preserveMatch.Count -ne 1 -or $preserveMatch[0].sha256 -cne $preserveClip.sha256){throw 'An accepted audio cue was changed.'}
}
$preserveRecipes=@{ 'IndustrialSecurity.py'='693c27499a71f2271b739c9211167af41e426dbda91216d5bec37b0b51b2bac3';'IndustrialProps.py'='83e4d0c59b9d728e8c684349e831ebb012a1702ef864b2035d98b4a4668b05d0';'IndustrialPropsV2.py'='2d4b3e1acde0a42240555eb92a2d89d894a3b9f31326b43eefd0927559ef805e' }
foreach($preserveRecipe in $preserveRecipes.GetEnumerator()){if((Get-FileHash (Join-Path $preserveRepo ('src/ForgeCore.Tools/Blender/'+$preserveRecipe.Key))).Hash.ToLowerInvariant() -cne $preserveRecipe.Value){throw 'Frozen Blender recipe changed.'}}
$preservePayload=[ordered]@{schemaVersion=1;candidateScene=$preserveRecord.scenePath;sceneSha256=$preserveRecord.sceneSha256;candidateArtifactsVerified=272;oldScenesVerified=2;originalGuardVerified=$true;frozenRecipesVerified=3;audioClipsMatched=34;humanArtAccepted=$false;
    qualification='Exact file/recipe/audio readback. Original animation source recipe is retained; no new character anatomy or motion acceptance is inferred.'}
$preserveStream=[IO.File]::Open($preserveOut,[IO.FileMode]::CreateNew)
try{$preserveBytes=[Text.Encoding]::UTF8.GetBytes(($preservePayload|ConvertTo-Json -Depth 4));$preserveStream.Write($preserveBytes,0,$preserveBytes.Length)}finally{$preserveStream.Dispose()}
$preservePayload|ConvertTo-Json -Depth 4
