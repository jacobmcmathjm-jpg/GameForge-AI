Option Explicit
Dim shell, fso, base, bat
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
base = fso.GetParentFolderName(WScript.ScriptFullName)
bat = base & "\LOCATE_UNREAL_ENGINE.bat"
If Not fso.FileExists(bat) Then
  MsgBox "LOCATE_UNREAL_ENGINE.bat is missing.", vbCritical, "GameForge AI"
  WScript.Quit 1
End If
shell.Run "cmd /c """ & bat & """", 1, True
