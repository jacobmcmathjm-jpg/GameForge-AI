[CmdletBinding()]
param([ValidatePattern('^[a-z0-9-]{1,30}$')][string]$Attempt='final',[ValidateSet('0.16.81','0.16.83','0.16.84','0.16.86','0.16.87','0.16.88','0.16.89','0.16.90','0.16.91','0.16.92')][string]$InstalledKit='0.16.81')
$ErrorActionPreference='Stop'
$recordProject='D:\ForgeCore\Workspace\Games\ForgeCore'
$recordRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$recordOutput=Join-Path $recordRepo ('work/s08-s10-verification/recorded-v26-artifact-audit-'+$Attempt+'.json')
if(Test-Path -LiteralPath $recordOutput){throw 'Audit already exists; no overwrite.'}
function Confirm-RecordedHash([string]$Root,[string]$Relative,[string]$Expected) {
    $recordPath=[IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if(-not $recordPath.StartsWith([IO.Path]::GetFullPath($Root)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Audit path escaped root.'}
    $recordItem=Get-Item -LiteralPath $recordPath
    if($recordItem.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Unexpected reparse point.'}
    if((Get-FileHash -LiteralPath $recordPath).Hash.ToLowerInvariant() -cne $Expected){throw ('Changed artifact: '+$Relative)}
}
$recordFresh=@(Get-ChildItem -LiteralPath (Join-Path $recordProject '.forgecore/unity/character-animation-evidence') -Filter '*.json' -File | ForEach-Object {
    $recordValue=Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
    if($recordValue.scenePath -ceq 'Assets/Scenes/S10-RecordedFootsteps-v26.unity'){$recordValue}
})
if($recordFresh.Count -ne 1){throw 'Expected exactly one fresh generation receipt.'}
$recordFresh=$recordFresh[0]
$recordOld=Get-Content -LiteralPath (Join-Path $recordProject '.forgecore/unity/character-animation-evidence/7e74cf39adb9453d9df15948dd4d7451.json') -Raw | ConvertFrom-Json
foreach($recordReceipt in @($recordFresh,$recordOld)){
    Confirm-RecordedHash $recordProject $recordReceipt.scenePath $recordReceipt.sceneSha256
    foreach($recordEntry in $recordReceipt.artifacts){Confirm-RecordedHash $recordProject $recordEntry.path $recordEntry.sha256}
}
if($recordFresh.artifacts.Count -ne 194 -or $recordOld.artifacts.Count -ne 170){throw 'Unexpected generated artifact cardinality.'}
$recordPreserved=@($recordOld.artifacts | Where-Object {$_.path -like '*.anim' -or $_.path -like '*/Demo-*.wav'})
foreach($recordEntry in $recordPreserved){
    $recordMatching=@($recordFresh.artifacts | Where-Object {[IO.Path]::GetFileName($_.path) -ceq [IO.Path]::GetFileName($recordEntry.path)})
    if($recordMatching.Count -ne 1 -or $recordMatching[0].sha256 -cne $recordEntry.sha256){throw 'An existing animation or procedural cue changed.'}
}
if($recordPreserved.Count -ne 36){throw 'Expected eight authored clips and 28 unchanged procedural cues.'}
$recordPack=Get-Content -LiteralPath (Join-Path $recordRepo 'src/ForgeCore.Tools/Audio/RecordedFootsteps.json') -Raw | ConvertFrom-Json
$recordFolder=Join-Path $recordProject 'Assets/ForgeCore/CharacterAnimations/s10-recorded-footsteps-v26'
$recordBank=Get-Content -LiteralPath (Join-Path $recordFolder 'demo-audio-report.json') -Raw | ConvertFrom-Json
if($recordBank.schemaVersion -ne 3 -or $recordBank.recordedBankId -cne $recordPack.id -or $recordBank.humanListeningAccepted -or $recordBank.clips.Count -ne 34){throw 'Wrong audio source/report contract.'}
$recordBytes=0L
foreach($recordEntry in $recordBank.clips){Confirm-RecordedHash $recordFolder $recordEntry.file $recordEntry.sha256;$recordBytes+=$recordEntry.bytes}
foreach($recordEntry in $recordPack.files){Confirm-RecordedHash $recordFolder $recordEntry.file $recordEntry.sha256}
if($recordBytes -gt 2MB -or @($recordBank.clips | Where-Object sourceKind -CEQ recorded).Count -ne 6){throw 'Audio bank budget or recording count failed.'}
$recordKits=@()
foreach($recordRoot in @((Join-Path $recordProject 'Assets/ForgeCoreGameTemplates'),(Join-Path $recordProject '.forgecore/unity/template-kit-backups/051db6f48d9d49e1a9ae983727e91896'))){
    $recordManifest=Get-Content -LiteralPath (Join-Path $recordRoot 'forgecore-game-templates.manifest.json') -Raw | ConvertFrom-Json
    foreach($recordEntry in $recordManifest.files){Confirm-RecordedHash $recordRoot $recordEntry.path $recordEntry.sha256}
    $recordKits+=@{version=$recordManifest.version;files=$recordManifest.files.Count}
}
$recordExpectedFiles=if($InstalledKit -cin @('0.16.91','0.16.92')){71}elseif($InstalledKit -cin @('0.16.89','0.16.90')){69}elseif($InstalledKit -ceq '0.16.88'){66}elseif($InstalledKit -cin @('0.16.84','0.16.86','0.16.87')){64}elseif($InstalledKit -ceq '0.16.83'){62}else{60}
if($recordKits[0].version -cne $InstalledKit -or $recordKits[0].files -ne $recordExpectedFiles -or $recordKits[1].version -cne '0.16.80' -or $recordKits[1].files -ne 58){throw 'Wrong installed/protected kit.'}
Confirm-RecordedHash $recordProject $recordFresh.assetPath 'ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0'
$recordResult=[ordered]@{schema=1;sourcePackId=$recordPack.id;rawRecordings=6;processedRecordings=6;totalAudioClips=34;pcmWavBytes=$recordBytes;preservedAuthoredClips=8;preservedProceduralCues=28;freshArtifacts=194;priorV25Artifacts=170;kit=$recordKits;sourceRightsEvidenceChecked=$true;humanListeningAccepted=$false;sceneSha256=$recordFresh.sceneSha256;priorSceneSha256=$recordOld.sceneSha256}
$recordStream=[IO.File]::Open($recordOutput,[IO.FileMode]::CreateNew)
try{$recordWriter=[IO.StreamWriter]::new($recordStream);try{$recordWriter.WriteLine(($recordResult | ConvertTo-Json -Depth 8))}finally{$recordWriter.Dispose()}}finally{$recordStream.Dispose()}
$recordResult|ConvertTo-Json -Depth 8
