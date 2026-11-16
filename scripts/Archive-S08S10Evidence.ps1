[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$workspace='D:\ForgeCore\Workspace'
$project=Join-Path $workspace 'Games\ForgeCore'
$evidence=Join-Path $repo 'work\s08-s10-verification'
$output=Join-Path $repo 'outputs\ForgeCore-S08S10-20261029'
if(Test-Path -LiteralPath $output){throw 'Evidence archive already exists; no overwrite.'}
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Json([string]$Path){Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json}
function Assert-Hash([string]$Path,[string]$Expected){if((Hash $Path) -cne $Expected){throw "Identity changed: $Path"}}
function Receipt([string]$Name){$r=Json (Join-Path $evidence ($Name+'.json'));if(-not $r.Success){throw "Operation not successful: $Name"};$r}
function Project-File([string]$Relative){
    $p=[IO.Path]::GetFullPath((Join-Path $project $Relative))
    if(-not $p.StartsWith($project+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Project evidence escaped its root.'}
    $p
}
$recipes=@('industrial_barrel_v2','wooden_crate_v1','concrete_barrier_v1','industrial_fence_v1','shipping_container_v1')
$copies=[Collections.Generic.List[object]]::new()
$bundles=[Collections.Generic.List[string]]::new()
$bundleHashes=@{}
function Copy-Later([string]$Source,[string]$Name){$copies.Add([pscustomobject]@{Source=$Source;Name=$Name})}
$sourceFiles=0;$propArtifacts=0;$historical=0;$missionArtifacts=0;$upgradedBridgeBackups=0
foreach($recipe in $recipes){
    $created=(Receipt ('create-'+$recipe)).Output | ConvertFrom-Json
    $bundle=Join-Path $workspace ('.forgecore\blender-library\objects\'+$created.bundleSha256)
    $bundles.Add($bundle)
    Assert-Hash (Join-Path $bundle 'entry.json') $created.bundleSha256
    $bundleHashes[$created.bundleSha256+'/entry.json']=$created.bundleSha256
    $entry=Json (Join-Path $bundle 'entry.json')
    foreach($file in $entry.files){Assert-Hash (Join-Path $bundle $file.name) $file.sha256;$bundleHashes[$created.bundleSha256+'/'+$file.name]=$file.sha256;$sourceFiles++}
    $sourceFiles++
    $step='prepare-'+$recipe+$(if($recipe -eq 'industrial_barrel_v2'){'-attempt2'}else{''})
    $prepared=Receipt $step
    $match=[regex]::Match($prepared.Output,'Character rig evidence: ([^\r\n]+)')
    if(-not $match.Success){throw 'Missing target evidence path.'}
    $target=Json $match.Groups[1].Value
    if($target.visualReviewed -or $target.rightsReviewed -or $target.placementApproved){throw 'Unexpected automatic acceptance.'}
    foreach($item in $target.artifacts){Assert-Hash (Project-File $item.path) $item.sha256;$propArtifacts++}
    Assert-Hash (Project-File $target.assetPath) $target.assetSha256
    Assert-Hash (Project-File $target.scenePath) $target.sceneSha256
    Assert-Hash (Project-File $target.previewPath) $target.previewSha256
    Copy-Later (Join-Path $bundle 'preview.png') ($recipe+'-blender.png')
    Copy-Later (Project-File $target.previewPath) ($recipe+'-unity.png')
    Copy-Later $match.Groups[1].Value ($recipe+'-unity-evidence.json')
}
$mission=Receipt 'mission-v2'
$match=[regex]::Match($mission.Output,'Character rig evidence: ([^\r\n]+)')
if(-not $match.Success){throw 'Missing mission evidence.'}
$m=Json $match.Groups[1].Value
foreach($item in $m.artifacts){Assert-Hash (Project-File $item.path) $item.sha256;$missionArtifacts++}
Assert-Hash (Project-File $m.scenePath) $m.sceneSha256
Copy-Later $match.Groups[1].Value 'mission-evidence.json'
Copy-Later (Project-File 'Assets/ForgeCore/CharacterAnimations/s10-industrial-infiltration-v2/mission-report.json') 'mission-report.json'
$preview=Receipt 'mission-preview'
Copy-Later (Project-File 'Previews/S10-IndustrialInfiltration-v2-live.png') 'mission-live.png'
Copy-Later (Join-Path $evidence 'manifest.json') 'asset-manifest-receipt.json'
Copy-Later (Join-Path $evidence 'review-gate.json') 'review-gate.json'
foreach($step in @('edit-tests-final','play-tests-final','build','smoke')){[void](Receipt $step);Copy-Later (Join-Path $evidence ($step+'.json')) ($step+'.json')}
Copy-Later (Join-Path $evidence 'rendered-playmode.xml') 'rendered-playmode.xml'
$render=Json (Join-Path $evidence 'rendered-playmode.json')
if($render.exitCode -ne 0 -or $render.attributes.failed -ne '0' -or $render.attributes.result -ne 'Passed'){throw 'Rendered tests did not pass.'}
$gate=Json (Join-Path $evidence 'review-gate.json')
if(-not $gate.expectedBlocked -or $gate.importedFolders -ne 0){throw 'Real review gate failed.'}
$baseline=Json (Join-Path $repo 'outputs/ForgeCore-CapabilityShowcase-20261028/baseline.json')
foreach($item in $baseline.entries.PSObject.Properties){
    $relative=$item.Name.Replace('\','/')
    if($relative.StartsWith('Games/ForgeCore/Assets/ForgeCoreGameTemplates/')){continue}
    if($relative -ceq 'Games/ForgeCore/Assets/ForgeCore/Editor/ForgeCoreBridge.cs'){
        Assert-Hash (Project-File '.forgecore/unity/bridge-backups/00e84f4492ba42759020aad2ed429643/ForgeCoreBridge.cs.bak') $item.Value
        $upgradedBridgeBackups++;continue
    }
    Assert-Hash (Join-Path $workspace $relative) $item.Value;$historical++
}
$oldKit=Project-File '.forgecore/unity/template-kit-backups/fbbbca75721a44c5ab82ccb740d985dd'
Assert-Hash (Join-Path $oldKit 'forgecore-game-templates.manifest.json') '013d8ee03fc1b71707de4d0edfb1a03d44daa996d82bd7effa38711b3202e4ee'
$oldKitManifest=Json (Join-Path $oldKit 'forgecore-game-templates.manifest.json')
foreach($entry in $oldKitManifest.files){Assert-Hash (Join-Path $oldKit $entry.path) $entry.sha256}
Assert-Hash (Project-File 'Assets/Scenes/S04-S05-ControlsAds-v2.unity') '150d34ec25e36f298d2af6b58258194cb254fed6727bb7626aeb19f2e4a7b2d6'
Assert-Hash (Project-File 'Assets/Scenes/S06-SecurityCombat-v10.unity') 'c5ae2da5972d4b97adb8a28e40edaa9ecad097372967b96963483090c07865c7'
Assert-Hash (Project-File 'Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb') 'ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0'
$hostTotals=@{passed=0;failed=0;total=0}
foreach($trx in Get-ChildItem -LiteralPath (Join-Path $evidence 'host-final-kit57') -Filter '*.trx' -File){
    [xml]$document=Get-Content -LiteralPath $trx.FullName -Raw
    $c=$document.TestRun.ResultSummary.Counters
    $hostTotals.passed += [int]$c.passed;$hostTotals.failed += [int]$c.failed;$hostTotals.total += [int]$c.total
    Copy-Later $trx.FullName ('host-'+$trx.Name)
}
if($hostTotals.failed -ne 0 -or $hostTotals.total -ne 495 -or $hostTotals.passed -ne 495){throw 'Expected final 495/495 host results.'}
New-Item -ItemType Directory -Path $output | Out-Null
foreach($copy in $copies){Copy-Item -LiteralPath $copy.Source -Destination (Join-Path $output $copy.Name)}
Compress-Archive -LiteralPath $bundles.ToArray() -DestinationPath (Join-Path $output 'original-blender-bundles.zip') -CompressionLevel Optimal
$zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $output 'original-blender-bundles.zip'))
try{
    $payloadEntries=@($zip.Entries | Where-Object {-not $_.FullName.Replace('\','/').EndsWith('/')})
    if($payloadEntries.Count -ne $bundleHashes.Count){throw 'Asset backup entry count differs.'}
    foreach($entry in $payloadEntries){
        $stream=$entry.Open()
        try{$actual=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}finally{$stream.Dispose()}
        if($actual -cne $bundleHashes[$entry.FullName.Replace('\','/')]){throw 'Asset backup payload identity differs.'}
    }
}finally{$zip.Dispose()}
$audit=[ordered]@{schemaVersion=1;sourceFilesVerified=$sourceFiles;assetBackupEntries=$bundleHashes.Count;assetBackupSha256=Hash (Join-Path $output 'original-blender-bundles.zip');propArtifactsVerified=$propArtifacts;missionArtifactsVerified=$missionArtifacts;
    historicalFilesPreserved=$historical;priorControlsScenePreserved=$true;originalGuardPreserved=$true;hostTests=$hostTotals;
    intentionallyUpgradedBridgeOriginalBackups=$upgradedBridgeBackups;originalKitBackupFilesVerified=$oldKitManifest.files.Count;
    renderedTests=$render.attributes;realReviewGateBlockedAllImports=$true;approvedAssetsPlaced=$false;humanAcceptance=$false;
    qualification='Technical evidence only; greybox mission, target placement implementation and human art/balance acceptance remain open.'}
$audit | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'audit.json') -Encoding utf8NoBOM
$payload=@(Get-ChildItem -LiteralPath $output -File | Sort-Object Name | ForEach-Object {[ordered]@{path=$_.Name;bytes=$_.Length;sha256=Hash $_.FullName}})
[ordered]@{schemaVersion=1;files=$payload} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'evidence-manifest.json') -Encoding utf8NoBOM
$audit | ConvertTo-Json -Depth 8
