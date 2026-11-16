[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repeatRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$repeatProject='D:\ForgeCore\Workspace\Games\ForgeCore'
$repeatBase=Join-Path $repeatProject 'Assets/ForgeCore/CharacterAnimations/graphics-yard-v3'
$repeatFresh=Join-Path $repeatProject 'Assets/ForgeCore/CharacterAnimations/nightly-yard-motion'
$repeatOutput=Join-Path $repeatRepo 'work/s08-s10-verification/graphics-repeatability.json'
if(Test-Path -LiteralPath $repeatOutput){throw 'Repeatability receipt exists; no overwrite.'}
$repeatMaps=@()
foreach($repeatSurface in @('Asphalt','Concrete','Steel')){foreach($repeatKind in @('Albedo','Normal','Mask')){
    $repeatName='Detail'+$repeatSurface+$repeatKind+'.png'
    $repeatA=(Get-FileHash -LiteralPath (Join-Path $repeatBase $repeatName)).Hash.ToLowerInvariant()
    $repeatB=(Get-FileHash -LiteralPath (Join-Path $repeatFresh $repeatName)).Hash.ToLowerInvariant()
    if($repeatA -cne $repeatB){throw ('Repeated recipe map differs: '+$repeatName)}
    $repeatMaps+=@{file=$repeatName;sha256=$repeatA}
}}
$repeatOldAudio=Get-Content -LiteralPath (Join-Path $repeatProject 'Assets/ForgeCore/CharacterAnimations/s10-recorded-footsteps-v26/demo-audio-report.json') -Raw|ConvertFrom-Json
$repeatNewAudio=Get-Content -LiteralPath (Join-Path $repeatFresh 'demo-audio-report.json') -Raw|ConvertFrom-Json
if(($repeatOldAudio.clips|ConvertTo-Json -Depth 6 -Compress) -cne ($repeatNewAudio.clips|ConvertTo-Json -Depth 6 -Compress)){throw 'Regenerated accepted audio clip identities differ.'}
foreach($repeatClip in $repeatNewAudio.clips){
    $repeatPath=Join-Path $repeatFresh $repeatClip.file
    if((Get-FileHash -LiteralPath $repeatPath).Hash.ToLowerInvariant() -cne $repeatClip.sha256){throw 'New audio clip changed after generation.'}
}
$repeatResult=[ordered]@{schemaVersion=1;sourceGuardIdentitiesDiffer=$true;deterministicMapCount=$repeatMaps.Count;maps=$repeatMaps;
    exactPriorAudioClips=$repeatNewAudio.clips.Count;humanArtAcceptance=$false;
    qualification='Two independently generated mission fixtures share exact nine original map bytes. All 34 regenerated audio clips match retained v26 source identities. New named-skin animation binding paths/hashes differ; this does not claim byte-identical new clips or visual acceptance.'}
$repeatStream=[IO.File]::Open($repeatOutput,[IO.FileMode]::CreateNew)
try{$repeatBytes=[Text.Encoding]::UTF8.GetBytes(($repeatResult|ConvertTo-Json -Depth 6));$repeatStream.Write($repeatBytes,0,$repeatBytes.Length)}finally{$repeatStream.Dispose()}
$repeatResult|ConvertTo-Json -Depth 6
