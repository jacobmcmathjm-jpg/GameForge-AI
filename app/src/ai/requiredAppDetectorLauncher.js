
const GameForgeRequiredAppDetectorLauncher = {
  lastCheck: null,

  requiredApps: [
    {
      id: "unreal",
      name: "Unreal Engine 5",
      executable: "UnrealEditor.exe",
      required: true,
      purpose: "Create, assemble, run and package Unreal projects.",
      installHint: "Install Unreal Engine 5 through Epic Games Launcher, or select UnrealEditor.exe manually.",
      downloadAction: "open_official_page"
    },
    {
      id: "runuat",
      name: "Unreal Automation Tool",
      executable: "RunUAT.bat",
      required: true,
      purpose: "Cook/package Windows builds through BuildCookRun.",
      installHint: "Included with a full Unreal Engine install.",
      downloadAction: "install_unreal"
    },
    {
      id: "blender",
      name: "Blender",
      executable: "blender.exe",
      required: true,
      purpose: "Clean, scale, convert and optimise generated 3D assets.",
      installHint: "Install Blender or select blender.exe manually.",
      downloadAction: "download_official_installer"
    },
    {
      id: "ffmpeg",
      name: "FFmpeg",
      executable: "ffmpeg.exe",
      required: false,
      purpose: "Video/audio processing and trailer generation.",
      installHint: "Install FFmpeg or continue without video/trailer automation.",
      downloadAction: "open_official_page"
    },
    {
      id: "meshy",
      name: "Meshy API",
      executable: "API key",
      required: false,
      purpose: "Autonomous custom 3D asset generation.",
      installHint: "Add Meshy API key in GameForge settings.",
      downloadAction: "open_official_page"
    }
  ],

  async run(projectState = window.projectState || {}) {
    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("preflight", "Checking required apps and APIs");
    }

    if (!window.gameforgeAPI?.requiredAppPreflightCheck) {
      const result = { ok: false, status: "BACKEND_UNAVAILABLE", requiredApps: this.requiredApps };
      this.lastCheck = result;
      return result;
    }

    const result = await window.gameforgeAPI.requiredAppPreflightCheck({
      requiredApps: this.requiredApps,
      projectState
    });

    this.lastCheck = result;
    projectState.requiredAppPreflightCheck = result;

    if ((result.missingRequired || []).length || (result.missingOptional || []).length) {
      this.showMissingPrompt(result);
    }

    return result;
  },

  async approveAndResolveMissing(result = this.lastCheck) {
    if (!result) return { ok: false, error: "No preflight check available." };

    const missing = [...(result.missingRequired || []), ...(result.missingOptional || [])];
    if (!missing.length) return { ok: true, status: "NO_MISSING_TOOLS" };

    const msg = "GameForge found missing tools/APIs:\n\n"
      + missing.map(x => `• ${x.name}\n  ${x.installHint}`).join("\n\n")
      + "\n\nDo you want GameForge to open approved official download/setup sources where available?";

    if (!confirm(msg)) return { ok: false, status: "USER_DECLINED_DOWNLOAD_HELP" };

    if (!window.gameforgeAPI?.approvedToolDownloadRequest) {
      return { ok: false, status: "DOWNLOAD_MANAGER_UNAVAILABLE" };
    }

    return await window.gameforgeAPI.approvedToolDownloadRequest({ missing });
  },

  showMissingPrompt(result) {
    const missingRequired = result.missingRequired || [];
    const missingOptional = result.missingOptional || [];
    const lines = [];

    if (missingRequired.length) {
      lines.push("Required tools missing:");
      for (const item of missingRequired) lines.push(`• ${item.name}: ${item.installHint}`);
    }

    if (missingOptional.length) {
      lines.push("");
      lines.push("Optional tools/API missing:");
      for (const item of missingOptional) lines.push(`• ${item.name}: ${item.installHint}`);
    }

    lines.push("");
    lines.push("GameForge can open approved official download/setup sources after your approval.");

    alert(lines.join("\n"));
  },

  formatReport(report = this.lastCheck) {
    if (!report) return "No required app check has run yet.";
    return `# Required App Detector + Guided Setup

Status: ${report.status || "UNKNOWN"}

Detected:
${Object.entries(report.detected || {}).map(([k,v]) => `- ${k}: ${v.path || v.status || "unknown"}`).join("\n")}

Missing Required:
${(report.missingRequired || []).length ? report.missingRequired.map(x => `- ${x.name}: ${x.installHint}`).join("\n") : "- none"}

Missing Optional:
${(report.missingOptional || []).length ? report.missingOptional.map(x => `- ${x.name}: ${x.installHint}`).join("\n") : "- none"}

Rules:
- Missing required tools pause full autonomous generation.
- Missing optional tools continue with reduced automation.
- Downloads/setup pages only run through approved official sources after user approval.
- GameForge does not bypass logins, payments, CAPTCHA or licence acceptance.`;
  },

  contextForHybridAI() {
    return `Required App Detector active:
- checks UnrealEditor, RunUAT, Blender, FFmpeg and Meshy API
- prompts user when required files are missing
- can request approved official download/setup source after user approval
- pauses full autonomous build when required tools are missing`;
  }
};

window.GameForgeRequiredAppDetectorLauncher = GameForgeRequiredAppDetectorLauncher;
