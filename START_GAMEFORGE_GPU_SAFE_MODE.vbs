Option Explicit
Dim shell, fso, base, runner
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
base = fso.GetParentFolderName(WScript.ScriptFullName)
runner = base & "\_system\gpu_safe_start.bat"
If Not fso.FileExists(runner) Then
  MsgBox "GPU safe runner is missing.", vbCritical, "GameForge AI"
  WScript.Quit 1
End If
shell.Run "cmd /c """ & runner & """", 0, False
