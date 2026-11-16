[CmdletBinding()]
param([ValidatePattern('^[a-z0-9-]{1,24}$')][string]$Attempt='refined1')
$ErrorActionPreference='Stop'
$fixtureRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$fixtureRoot=Join-Path $fixtureRepo ('work/three-hour-assets/manifest-fixtures-'+$Attempt)
if(Test-Path -LiteralPath $fixtureRoot){throw 'Fixture run already exists; no overwrite.'}
[IO.Directory]::CreateDirectory($fixtureRoot)|Out-Null
$fixtureResults=[Collections.Generic.List[object]]::new()
function Write-FixtureBytes([string]$Path,[byte[]]$Data) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null
    $stream=[IO.File]::Open($Path,[IO.FileMode]::CreateNew)
    try{$stream.Write($Data,0,$Data.Length)}finally{$stream.Dispose()}
}
foreach($case in @('valid-review','valid-delivery','valid-source','valid-source-v2','wrong-hash','extra-file','traversal','backslash','duplicate','approval-claim','string-approval','private-state','wrong-total','receipt-replay')) {
    $folder=Join-Path $fixtureRoot $case;[IO.Directory]::CreateDirectory($folder)|Out-Null
    $data=[Text.Encoding]::UTF8.GetBytes('Bounded non-secret test payload.')
    Write-FixtureBytes (Join-Path $folder 'payload.txt') $data
    $entry=[ordered]@{path='payload.txt';bytes=$data.Length;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()}
    $manifest=[ordered]@{schemaVersion=1;kind='ForgeCore-refined-industrial-source-and-Unity-review';humanApproved=$false;automaticPlacementApproved=$false;fileCount=1;totalBytes=$data.Length;files=@($entry)}
    $kind='Review'
    switch($case) {
        'valid-delivery' {$kind='Delivery';$manifest.schema=1;$manifest.kind='ForgeCore-self-contained-win-x64'}
        'valid-source' {$kind='Source';$manifest.kind='sanitized-source-checkpoint'}
        'valid-source-v2' {$kind='Source';$manifest.schemaVersion=2;$manifest.kind='sanitized-source-checkpoint'}
        'wrong-hash' {$entry.sha256='a'*64}
        'extra-file' {Write-FixtureBytes (Join-Path $folder 'unlisted.txt') $data}
        'traversal' {$entry.path='../payload.txt'}
        'backslash' {$entry.path='folder\payload.txt'}
        'duplicate' {$manifest.files=@($entry,$entry);$manifest.fileCount=2;$manifest.totalBytes=$data.Length*2}
        'approval-claim' {$manifest.humanApproved=$true}
        'string-approval' {$manifest.humanApproved='false'}
        'private-state' {$entry.path='must-not-read.db'}
        'wrong-total' {$manifest.totalBytes=$data.Length+1}
    }
    $manifestPath=Join-Path $folder 'manifest.json';Write-FixtureBytes $manifestPath ([Text.Encoding]::UTF8.GetBytes(($manifest|ConvertTo-Json -Depth 7)))
    $receipt=Join-Path $fixtureRoot ($case+'-receipt.json');$passed=$false
    if($case.StartsWith('valid-') -or $case -eq 'receipt-replay') {
        & (Join-Path $PSScriptRoot 'Test-ArtifactManifest.ps1') -ManifestPath $manifestPath -Kind $kind -ReceiptPath $receipt | Out-Null
        $passed=$true
        if($case -eq 'receipt-replay') {
            $rejected=$false;try{& (Join-Path $PSScriptRoot 'Test-ArtifactManifest.ps1') -ManifestPath $manifestPath -Kind $kind -ReceiptPath $receipt | Out-Null}catch{$rejected=$true}
            if(-not $rejected){throw 'Existing receipt was overwritten.'}
        }
    }else {
        $rejected=$false;try{& (Join-Path $PSScriptRoot 'Test-ArtifactManifest.ps1') -ManifestPath $manifestPath -Kind $kind -ReceiptPath $receipt | Out-Null}catch{$rejected=$true}
        if(-not $rejected -or (Test-Path -LiteralPath $receipt)){throw ('Expected fail-closed fixture: '+$case)}
        $passed=$true
    }
    $fixtureResults.Add([ordered]@{case=$case;passed=$passed})
}
$summary=[ordered]@{schemaVersion=1;passed=$fixtureResults.Count;failed=0;cases=@($fixtureResults.ToArray());qualification='Owned tiny mechanical fixtures only; no links created, no private-state file opened and no source/rights/art decisions.'}
Write-FixtureBytes (Join-Path $fixtureRoot 'fixture-results.json') ([Text.Encoding]::UTF8.GetBytes(($summary|ConvertTo-Json -Depth 6)))
$summary|ConvertTo-Json -Depth 6
