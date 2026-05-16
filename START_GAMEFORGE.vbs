Option Explicit
Dim shell, fso, base, target
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
base = fso.GetParentFolderName(WScript.ScriptFullName)
target = base & "\START_GAMEFORGE_FAST.vbs"
If Not fso.FileExists(target) Then
  MsgBox "START_GAMEFORGE_FAST.vbs is missing.", vbCritical, "GameForge AI"
  WScript.Quit 1
End If
shell.Run "wscript.exe """ & target & """", 0, False
