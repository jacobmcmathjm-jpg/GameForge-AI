GAMEFORGE AI v6.8.2 — UNREAL 5.7 DETECTOR FIX

Use normally:
1. Extract to C:\GF
2. Run START_GAMEFORGE_FAST.vbs

If GameForge says Unreal is missing even though Unreal is installed:
1. Close GameForge
2. Run SET_UNREAL_PATH.vbs
3. It will look for UnrealEditor.exe
4. If it cannot find it, paste the full path manually
5. Restart GameForge
6. Try Generate Full Game again

Usually UnrealEditor.exe is here:
C:\Program Files\Epic Games\UE_5.7\Engine\Binaries\Win64\UnrealEditor.exe

Usually RunUAT is here:
C:\Program Files\Epic Games\UE_5.7\Engine\Build\BatchFiles\RunUAT.bat

Meshy API detection working is a good sign. Unreal detection was likely a path/version problem.
