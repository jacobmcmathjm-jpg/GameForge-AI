
const GameForgeUnreal57DetectorFix = {
  likelyPaths: [
    "C:\\Program Files\\Epic Games\\UE_5.7\\Engine\\Binaries\\Win64\\UnrealEditor.exe",
    "C:\\Program Files\\Epic Games\\UE_5.6\\Engine\\Binaries\\Win64\\UnrealEditor.exe",
    "C:\\Program Files\\Epic Games\\UE_5.5\\Engine\\Binaries\\Win64\\UnrealEditor.exe",
    "D:\\Epic Games\\UE_5.7\\Engine\\Binaries\\Win64\\UnrealEditor.exe",
    "D:\\Epic Games\\UE_5.6\\Engine\\Binaries\\Win64\\UnrealEditor.exe"
  ],
  showHelp() {
    const text = [
      "Unreal detector fix active.",
      "If GameForge cannot find Unreal, run SET_UNREAL_PATH.vbs from the main GF folder.",
      "Usually: C:\\Program Files\\Epic Games\\UE_5.7\\Engine\\Binaries\\Win64\\UnrealEditor.exe"
    ].join("\n");
    console.info(text);
    return text;
  },
  contextForHybridAI() {
    return "Unreal 5.7 Detector Fix active: supports UE_5.7 path discovery and manual SET_UNREAL_PATH.vbs helper.";
  }
};
window.GameForgeUnreal57DetectorFix = GameForgeUnreal57DetectorFix;
window.addEventListener("DOMContentLoaded", () => setTimeout(() => GameForgeUnreal57DetectorFix.showHelp(), 500));
