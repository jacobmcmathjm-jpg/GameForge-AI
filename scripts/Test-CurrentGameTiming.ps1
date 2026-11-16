[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]{1,30}$')][string]$Attempt)
$ErrorActionPreference='Stop'
$timingRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$timingEvidence=Join-Path $timingRepo 'work/s08-s10-verification'
$timingReceipt=Join-Path $timingEvidence ('standalone-demo-current-timing-v1-'+$Attempt+'.json')
$timingValidation=Join-Path $timingEvidence ('current-timing-'+$Attempt+'-validated.json')
if(Test-Path -LiteralPath $timingValidation){throw 'Immutable validation receipt exists.'}
$timingPayload=Get-Content -LiteralPath $timingReceipt -Raw | ConvertFrom-Json
$timingReport=$timingPayload.report
$timing=$timingReport.timingDiagnostics
if(-not $timing -or $timing.schemaVersion -ne 1 -or $timing.clock -cne 'stopwatch_update_intervals' -or $timing.warmupMilliseconds -ne 2000 -or $timing.provisionalGameplayMaximumMilliseconds -ne 100 -or $timing.wholeSessionSmoothnessAccepted){throw 'Missing/incorrect timing contract.'}
if($timing.truncated -or $timing.droppedFrames -or $timing.droppedWindows -or $timing.events.Count -gt 64 -or $timing.worstByPhase.Count -gt 5 -or $timing.observedFrames -gt 1000000){throw 'Diagnostic capacity/overflow failure.'}
if($timing.windows.Count -ne 6 -or $timingReport.passes.Count -ne 3){throw 'Expected three full gameplay and idle windows.'}
$timingPassed=$true
$timingComparisons=[Collections.Generic.List[object]]::new()
for($timingIndex=0;$timingIndex -lt 3;$timingIndex++){
    $timingPass=$timingReport.passes[$timingIndex]
    foreach($timingKind in @('gameplay','idle')){
        $timingOffset=if($timingKind -ceq 'idle'){1}else{0}
        $timingWindow=$timing.windows[2*$timingIndex+$timingOffset]
        $timingLegacy=if($timingOffset){$timingPass.idleFrames}else{$timingPass.frames}
        if($timingWindow.kind -cne $timingKind -or $timingWindow.frames -ne $timingLegacy.totalFrames -or [Math]::Abs($timingWindow.maximumWallMilliseconds-$timingLegacy.maximum) -gt 0.0001){throw 'New diagnostics altered/disagree with existing exact sampling windows.'}
        if($timingKind -ceq 'gameplay' -and $timingWindow.maximumWallMilliseconds -gt 100){$timingPassed=$false}
        $timingComparisons.Add([ordered]@{preset=$timingPass.preset;kind=$timingKind;frames=$timingWindow.frames;maximum=$timingWindow.maximumWallMilliseconds;legacyMaximum=$timingLegacy.maximum})
    }
}
if($timing.measuredGameplayMaximumPassed -ne $timingPassed){throw 'Incorrect provisional measured gameplay decision.'}
foreach($timingEvent in @($timing.events)+@($timing.worstByPhase)){
    if($timingEvent.overlappingWork.Count -gt 8 -or $timingEvent.endMilliseconds -lt $timingEvent.startMilliseconds -or [Math]::Abs($timingEvent.wallMilliseconds-($timingEvent.endMilliseconds-$timingEvent.startMilliseconds)) -gt 0.0001){throw 'Unbounded or inconsistent retained interval.'}
}
$timingResult=[ordered]@{schemaVersion=1;rawReceiptSha256=(Get-FileHash -LiteralPath $timingReceipt).Hash.ToLowerInvariant();windowsMatchOriginal=$true;
    measuredGameplayMaximumPassed=$timingPassed;wholeSessionSmoothnessAccepted=$false;freezeFixed=$false;comparisons=$timingComparisons;
    phases=$timing.phases;unityAwakeMilliseconds=$timing.unityAwakeMilliseconds;unityReadyMilliseconds=$timing.unityReadyMilliseconds;
    qualification='Ordered real rendered launches; OS/driver caches not controlled. Stopwatch Update intervals are primary; Unity unscaled delta supplementary. Correlation is not attribution.'}
$timingStream=[IO.File]::Open($timingValidation,[IO.FileMode]::CreateNew)
try{$timingBytes=[Text.Encoding]::UTF8.GetBytes(($timingResult|ConvertTo-Json -Depth 12));$timingStream.Write($timingBytes,0,$timingBytes.Length)}finally{$timingStream.Dispose()}
$timingResult|ConvertTo-Json -Depth 8
if(-not $timingPassed){throw 'Provisional gameplay maximum failed; validation/raw receipts retained.'}
