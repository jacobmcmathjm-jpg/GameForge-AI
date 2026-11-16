[CmdletBinding()]
param([ValidatePattern('^[a-z0-9-]{1,30}$')][string]$Attempt='')
$ErrorActionPreference='Stop'
$audioProject='D:\ForgeCore\Workspace\Games\ForgeCore'
$audioRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$audioSuffix=if($Attempt){'-'+$Attempt}else{''}
$audioEvidence=Join-Path $audioRepo ('work/s08-s10-verification/audio-v25-artifact-audit'+$audioSuffix+'.json')
if(Test-Path -LiteralPath $audioEvidence){throw 'Audit receipt exists; no overwrite.'}
function Confirm-AudioHash([string]$Root,[string]$Relative,[string]$Expected) {
    $audioFull=[IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if(-not $audioFull.StartsWith([IO.Path]::GetFullPath($Root)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Audit path escaped its root.'}
    if((Get-Item -LiteralPath $audioFull).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Unexpected audit reparse point.'}
    if((Get-FileHash -LiteralPath $audioFull -Algorithm SHA256).Hash.ToLowerInvariant() -cne $Expected){throw ('Hash mismatch: '+$Relative)}
}
$audioFresh=Get-Content -LiteralPath (Join-Path $audioProject '.forgecore/unity/character-animation-evidence/7e74cf39adb9453d9df15948dd4d7451.json') -Raw | ConvertFrom-Json
$audioOld=@(Get-ChildItem -LiteralPath (Join-Path $audioProject '.forgecore/unity/character-animation-evidence') -File -Filter '*.json' | ForEach-Object {
    $audioReceipt=Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
    if($audioReceipt.scenePath -ceq 'Assets/Scenes/S10-Demo-v22.unity'){$audioReceipt}
})
if($audioOld.Count -ne 1){throw 'Expected exactly one prior v22 mission-generation receipt.'}
foreach($audioReceipt in @($audioFresh,$audioOld[0])) {
    Confirm-AudioHash $audioProject $audioReceipt.scenePath $audioReceipt.sceneSha256
    foreach($audioArtifact in $audioReceipt.artifacts){
        Confirm-AudioHash $audioProject $audioArtifact.path $audioArtifact.sha256
        if((Get-Item -LiteralPath (Join-Path $audioProject $audioArtifact.path)).Length -ne $audioArtifact.bytes){throw 'Artifact byte count changed.'}
    }
}
if($audioFresh.artifacts.Count -ne 170 -or $audioOld[0].artifacts.Count -ne 126){throw 'Unexpected artifact cardinality.'}
$audioClips=@($audioFresh.artifacts | Where-Object path -Like '*.anim')
if($audioClips.Count -ne 8){throw 'Expected eight authored clips.'}
foreach($audioClip in $audioClips){
    $audioPrevious=@($audioOld[0].artifacts | Where-Object { [IO.Path]::GetFileName($_.path) -ceq [IO.Path]::GetFileName($audioClip.path) })
    if($audioPrevious.Count -ne 1 -or $audioPrevious[0].sha256 -cne $audioClip.sha256){throw 'Authored animation changed.'}
}
$audioKit=Join-Path $audioProject 'Assets/ForgeCoreGameTemplates'
$audioBackup=Join-Path $audioProject '.forgecore/unity/template-kit-backups/b2249fee824f46609cb5f08ba884be8f'
$audioKitCounts=@()
foreach($audioRoot in @($audioKit,$audioBackup)){
    $audioManifest=Get-Content -LiteralPath (Join-Path $audioRoot 'forgecore-game-templates.manifest.json') -Raw | ConvertFrom-Json
    foreach($audioEntry in $audioManifest.files){Confirm-AudioHash $audioRoot $audioEntry.path $audioEntry.sha256}
    $audioKitCounts+=@{version=$audioManifest.version;files=$audioManifest.files.Count}
}
if($audioKitCounts[0].version -cne '0.16.80' -or $audioKitCounts[0].files -ne 58 -or $audioKitCounts[1].version -cne '0.16.79' -or $audioKitCounts[1].files -ne 57){throw 'Unexpected installed/prior kit.'}
$audioFolder=Join-Path $audioProject 'Assets/ForgeCore/CharacterAnimations/s10-audio-v25'
$audioBank=Get-Content -LiteralPath (Join-Path $audioFolder 'Demo-audio-report.json') -Raw | ConvertFrom-Json
if($audioBank.recipe -cne 'forgecore_layered_surface_audio_v2' -or $audioBank.schemaVersion -ne 2 -or $audioBank.sampleRate -ne 48000 -or $audioBank.clips.Count -ne 28 -or $audioBank.humanListeningAccepted){throw 'Wrong bank identity or fabricated human acceptance.'}
$audioBytes=0L
foreach($audioEntry in $audioBank.clips){Confirm-AudioHash $audioFolder $audioEntry.file $audioEntry.sha256;$audioBytes+=$audioEntry.bytes}
if($audioBytes -gt 2MB){throw 'PCM bank budget exceeded.'}
Confirm-AudioHash $audioProject $audioFresh.assetPath 'ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0'
$audioResult=[ordered]@{schema=1;freshArtifacts=$audioFresh.artifacts.Count;priorArtifacts=$audioOld[0].artifacts.Count;authoredClipsUnchanged=$audioClips.Count;kit=$audioKitCounts;recipe=$audioBank.recipe;clips=$audioBank.clips.Count;pcmBytes=$audioBytes;humanListeningAccepted=$false;recordingsImported=$false;freshSceneSha256=$audioFresh.sceneSha256;priorSceneSha256=$audioOld[0].sceneSha256}
$audioStream=[IO.File]::Open($audioEvidence,[IO.FileMode]::CreateNew)
try{$audioWriter=[IO.StreamWriter]::new($audioStream);try{$audioWriter.WriteLine(($audioResult|ConvertTo-Json -Depth 8))}finally{$audioWriter.Dispose()}}finally{$audioStream.Dispose()}
$audioResult|ConvertTo-Json -Depth 8
