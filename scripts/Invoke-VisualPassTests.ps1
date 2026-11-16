[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]{1,40}$')][string]$Attempt,[int]$ExpectedPassed=99,[ValidateRange(240,600)][int]$ProcessLimitSeconds=240,[ValidateSet('All','IndieWeapon')][string]$TestScope='All')
$ErrorActionPreference='Stop'
$visualRepo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$visualOut=Join-Path $visualRepo 'work/s08-s10-verification'
$visualXml=Join-Path $visualOut ($Attempt+'-playmode.xml')
$visualLog=Join-Path $visualOut ($Attempt+'-playmode.log')
$visualJson=Join-Path $visualOut ($Attempt+'-playmode.json')
foreach($visualPath in @($visualXml,$visualLog,$visualJson)){if(Test-Path -LiteralPath $visualPath){throw 'Attempt already exists; keep diagnostics and choose a fresh attempt.'}}
if(Get-Process Unity,blender -ErrorAction SilentlyContinue){throw 'Editors must be idle before this owned Unity run.'}
if($TestScope -eq 'IndieWeapon' -and $ExpectedPassed -ne 4){throw 'Targeted rifle scope has exactly four checks; do not report full-suite coverage.'}
$visualArgs=@('-batchmode','-projectPath','D:\ForgeCore\Workspace\Games\ForgeCore','-runTests','-testPlatform','PlayMode','-testResults',('"'+$visualXml+'"'),'-logFile',('"'+$visualLog+'"'))
if($TestScope -eq 'IndieWeapon'){$visualArgs+=@('-testFilter','ForgeCoreGameTemplates.Tests.IndieWeaponTests')}
$visualProcess=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -WindowStyle Hidden -PassThru -ArgumentList $visualArgs
if(-not $visualProcess.WaitForExit($ProcessLimitSeconds*1000)){Stop-Process -Id $visualProcess.Id -ErrorAction SilentlyContinue;throw 'Only the owned Unity test timed out; diagnostics retained. Process startup/compilation allowance is separate from gameplay timing gates.'}
$visualProcess.Refresh()
$visualSettings=[Xml.XmlReaderSettings]::new();$visualSettings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$visualSettings.XmlResolver=$null
$visualReader=[Xml.XmlReader]::Create($visualXml,$visualSettings)
try{$visualResult=[Xml.XmlDocument]::new();$visualResult.Load($visualReader)}finally{$visualReader.Dispose()}
$visualRun=$visualResult.'test-run'
$visualPayload=[ordered]@{schemaVersion=1;testScope=$TestScope;processLimitSeconds=$ProcessLimitSeconds;exitCode=$visualProcess.ExitCode;result=[string]$visualRun.result;passed=[int]$visualRun.passed;failed=[int]$visualRun.failed;skipped=[int]$visualRun.skipped;
    xmlSha256=(Get-FileHash -LiteralPath $visualXml).Hash.ToLowerInvariant();graphicsRequired=$true;humanArtAccepted=$false;qualification='Actual graphics-enabled Unity PlayMode tests, not human visual acceptance.'}
$visualStream=[IO.File]::Open($visualJson,[IO.FileMode]::CreateNew)
try{$visualBytes=[Text.Encoding]::UTF8.GetBytes(($visualPayload|ConvertTo-Json -Depth 5));$visualStream.Write($visualBytes,0,$visualBytes.Length)}finally{$visualStream.Dispose()}
$visualPayload|ConvertTo-Json -Depth 5
if($visualProcess.ExitCode -ne 0 -or $visualRun.result -cne 'Passed' -or [int]$visualRun.passed -ne $ExpectedPassed -or [int]$visualRun.failed -ne 0 -or [int]$visualRun.skipped -ne 0){throw 'Actual rendered tests failed or coverage differs; inspect retained XML.'}
