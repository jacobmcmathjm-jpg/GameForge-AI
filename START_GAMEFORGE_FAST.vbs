Option Explicit
Dim shell, fso, base, appDir, runner, nodeModules, result
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

base = fso.GetParentFolderName(WScript.ScriptFullName)
appDir = base & "\app"
runner = base & "\_system\fast_start.bat"
nodeModules = appDir & "\node_modules"

If Not fso.FileExists(appDir & "\package.json") Then
  MsgBox "GameForge app files are missing." & vbCrLf & _
         "Expected: " & appDir & "\package.json" & vbCrLf & vbCrLf & _
         "Extract the ZIP directly to C:\GF and try again.", vbCritical, "GameForge AI"
  WScript.Quit 1
End If

result = shell.Run("cmd /c where npm >nul 2>nul", 0, True)
If result <> 0 Then
  MsgBox "Node.js/npm was not found." & vbCrLf & vbCrLf & _
         "Install Node.js LTS or build the real GameForge installer first.", vbExclamation, "GameForge AI"
  WScript.Quit 10
End If

If Not fso.FolderExists(nodeModules) Then
  MsgBox "First launch setup is needed. GameForge will install packages once, then open." & vbCrLf & vbCrLf & _
         "This can take a few minutes the first time only.", vbInformation, "GameForge AI"
End If

shell.Run "cmd /c """ & runner & """", 0, False
