[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9-]{3,60}$')][string]$Label='20261030-RefinedV1')
$ErrorActionPreference='Stop'
$reviewRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$reviewWorkspace='D:\ForgeCore\Workspace'
$reviewProject=Join-Path $reviewWorkspace 'Games/ForgeCore'
$reviewReceipts=Join-Path $reviewRepo 'work/s08-s10-verification'
$reviewDestination=Join-Path $reviewRepo ('outputs/ForgeCore-IndustrialReview-'+$Label)
if(Test-Path -LiteralPath $reviewDestination){throw 'Review export exists; no overwrite.'}
$reviewRecipes=@('industrial_barrel_v3','wooden_crate_v2','concrete_barrier_v2','industrial_fence_v2','shipping_container_v2')
$reviewAllowed=@('asset.glb','model.blend','preview.png','basecolor.png','metallic.png','roughness.png','metrics.json','recipe.py','request.json','normal.png','prop.json','lod1.glb','lod2.glb','surface.json')
$reviewCopies=[Collections.Generic.List[object]]::new()
function Add-ReviewCopy([string]$Root,[string]$Relative,[string]$Target,[string]$Hash) {
    $source=[IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if(-not $source.StartsWith([IO.Path]::GetFullPath($Root)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Source escaped review root.'}
    $walk=$source
    while($walk -and $walk -ne [IO.Path]::GetPathRoot($walk)) {
        if((Test-Path -LiteralPath $walk) -and ((Get-Item -LiteralPath $walk -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Review export rejects reparse points.'}
        $walk=[IO.Path]::GetDirectoryName($walk)
    }
    $item=Get-Item -LiteralPath $source
    if($item.PSIsContainer -or $item.Length -lt 1 -or $item.Length -gt 100MB){throw 'Unbounded review payload.'}
    $actual=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if($Hash -and $actual -cne $Hash){throw ('Review source changed: '+$Relative)}
    if($Target -notmatch '^[A-Za-z0-9_./-]+$' -or $Target.Split('/') -contains '..'){throw 'Invalid review target.'}
    $reviewCopies.Add([pscustomobject]@{source=$source;target=$Target;sha256=$actual;bytes=$item.Length})
}
foreach($recipe in $reviewRecipes) {
    $create=Get-Content -LiteralPath (Join-Path $reviewReceipts ('refined5-create-'+$recipe+'.json')) -Raw | ConvertFrom-Json
    $import=Get-Content -LiteralPath (Join-Path $reviewReceipts ('refined5-import-'+$recipe+'.json')) -Raw | ConvertFrom-Json
    if(-not $create.Success -or -not $import.Success){throw 'Production creation/import did not succeed.'}
    $created=$create.Output|ConvertFrom-Json;$imported=$import.Output|ConvertFrom-Json;$hash=$created.bundleSha256
    if($hash -notmatch '^[a-f0-9]{64}$' -or $imported.bundleSha256 -cne $hash -or $created.assetSha256 -cne $imported.assetSha256){throw 'Exact source/import identity mismatch.'}
    $relative='.forgecore/blender-library/objects/'+$hash
    Add-ReviewCopy $reviewWorkspace ($relative+'/entry.json') ($relative+'/entry.json') $hash
    $entry=Get-Content -LiteralPath (Join-Path $reviewWorkspace ($relative+'/entry.json')) -Raw | ConvertFrom-Json
    if($entry.schemaVersion -ne 1 -or $entry.reviewState -cne 'unreviewed_local_recipe' -or $entry.request.recipe -cne $recipe -or
        $entry.files.Count -ne 14 -or (Compare-Object ($entry.files.name|Sort-Object) ($reviewAllowed|Sort-Object))){throw 'Unsupported refined bundle.'}
    foreach($file in $entry.files){Add-ReviewCopy $reviewWorkspace ($relative+'/'+$file.name) ($relative+'/'+$file.name) $file.sha256}
    $request=($entry.files|Where-Object name -CEQ 'request.json').sha256
    $reference='.forgecore/blender-library/recipes/'+$request+'.json'
    $ref=Get-Content -LiteralPath (Join-Path $reviewWorkspace $reference) -Raw | ConvertFrom-Json
    if($ref -cne $hash){throw 'Source cache reference changed.'}
    Add-ReviewCopy $reviewWorkspace $reference $reference ''
    $id='refined5-'+$recipe.Replace('_','-');$png='Previews/RefinedReview/'+$id+'.png'
    $previewMetadata=Get-Content -LiteralPath (Join-Path $reviewProject ($png+'.forgecore-art-preview.json')) -Raw | ConvertFrom-Json
    $proposal=Get-Content -LiteralPath (Join-Path $reviewReceipts ('refined5-review-proposals/'+$recipe+'-target-proposal.json')) -Raw | ConvertFrom-Json
    if($previewMetadata.assetSetId -cne $id -or $previewMetadata.assetPath -cne $imported.assetPath -or
        $previewMetadata.assetSha256 -cne $created.assetSha256 -or $previewMetadata.previewPath -cne $png -or
        $previewMetadata.editorVersion -cne '6000.5.5f1' -or $previewMetadata.bridgeVersion -cne '0.17.21' -or
        $previewMetadata.width -ne 1280 -or $previewMetadata.height -ne 720 -or $previewMetadata.viewCount -ne 4 -or
        $previewMetadata.visualReviewed -or $previewMetadata.rightsReviewed -or
        $proposal.Arguments.expected_preview_sha256 -cne $previewMetadata.previewSha256 -or
        $proposal.Arguments.expected_preview_provenance_sha256 -cne (Get-FileHash -LiteralPath (Join-Path $reviewProject ($png+'.forgecore-art-preview.json'))).Hash.ToLowerInvariant()) {throw 'Exact target preview/proposal changed.'}
    foreach($pair in @(@('.meta',$previewMetadata.metaSha256),@('.asset.json',$previewMetadata.provenanceSha256))) {
        if((Get-FileHash -LiteralPath (Join-Path $reviewProject ($imported.assetPath+$pair[0]))).Hash.ToLowerInvariant() -cne $pair[1]){throw 'Target metadata/provenance changed.'}
    }
    Add-ReviewCopy $reviewProject $png ('unity-previews/'+$id+'.png') $previewMetadata.previewSha256
    Add-ReviewCopy $reviewProject ($png+'.forgecore-art-preview.json') ('unity-previews/'+$id+'.png.forgecore-art-preview.json') ''
    foreach($suffix in @('create','cache-reuse','import','prepare','art-inspect','art-preview','source-inspect')) {
        $name='refined5-'+$suffix+'-'+$recipe+'.json';Add-ReviewCopy $reviewReceipts $name ('evidence/'+$name) ''
    }
    Add-ReviewCopy $reviewReceipts ('refined5-review-proposals/'+$recipe+'-target-proposal.json') ('proposals/'+$recipe+'-target-proposal.json') ''
    # Only the single source GLB and its own provenance/metadata, not arbitrary project contents.
    Add-ReviewCopy $reviewProject $imported.assetPath ('imported/'+$id+'.glb') $created.assetSha256
    foreach($suffix in @('.meta','.asset.json')){Add-ReviewCopy $reviewProject ($imported.assetPath+$suffix) ('imported/'+$id+'.glb'+$suffix) ''}
}
foreach($name in @('refined5-layout-source-plan.json','refined5-layout-review-gate.json')) {Add-ReviewCopy $reviewReceipts $name ('evidence/'+$name) ''}
Add-ReviewCopy $reviewRepo 'docs/outcomes/REFINED-INDUSTRIAL-REVIEW-2026-10-30.md' 'README.md' ''
if(($reviewCopies.target|Sort-Object -Unique).Count -ne $reviewCopies.Count){throw 'Duplicate export destination.'}
# Validate the complete list before creating a new output. Failures are retained rather than deleted/replaced.
[IO.Directory]::CreateDirectory($reviewDestination)|Out-Null
foreach($copy in $reviewCopies) {
    $target=[IO.Path]::GetFullPath((Join-Path $reviewDestination $copy.target))
    if(-not $target.StartsWith($reviewDestination+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Export target escaped destination.'}
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
    [IO.File]::Copy($copy.source,$target,$false)
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -cne $copy.sha256){throw 'Copied review payload changed.'}
}
$manifest=[ordered]@{schemaVersion=1;kind='ForgeCore-refined-industrial-source-and-Unity-review';humanApproved=$false;automaticPlacementApproved=$false;
    createdUtc=[DateTime]::UtcNow.ToString('O');fileCount=$reviewCopies.Count;totalBytes=($reviewCopies.bytes|Measure-Object -Sum).Sum;
    files=@($reviewCopies|Sort-Object target|ForEach-Object {[ordered]@{path=$_.target;bytes=$_.bytes;sha256=$_.sha256}})}
$stream=[IO.File]::Open((Join-Path $reviewDestination 'review-manifest.json'),[IO.FileMode]::CreateNew)
try{$data=[Text.UTF8Encoding]::new($false).GetBytes(($manifest|ConvertTo-Json -Depth 8));$stream.Write($data,0,$data.Length)}finally{$stream.Dispose()}
[pscustomobject]@{Folder=$reviewDestination;Files=$manifest.fileCount;Bytes=$manifest.totalBytes;HumanApproved=$false;PlacementApproved=$false}
