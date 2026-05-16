
const GameForgeExternalToolDiagnosticsSelfRepairEngine = {
  lastPlan: null,
  lastRun: null,

  supportedTools: {
    unreal: {
      name: "Unreal Engine",
      checks: ["UnrealEditor path", "project path", "plugins", "maps", "blueprints", "materials", "textures", "asset imports", "shader compile", "SDK/build tools"],
      autoRepairs: ["enable installed plugin", "regenerate .uproject settings", "rebuild import manifest", "assign fallback materials", "replace missing asset", "clean Saved/Intermediate", "retry Unreal Python import"]
    },
    runuat: {
      name: "RunUAT / Unreal Packaging",
      checks: ["RunUAT path", "BuildCookRun errors", "cook failures", "stage/package failures", "missing SDK", "bad archive directory", "long paths"],
      autoRepairs: ["retry with safer packaging preset", "clean build folders", "shorten project path warning", "regenerate packaging command", "switch Development config", "write blocker report"]
    },
    blender: {
      name: "Blender",
      checks: ["blender path", "script execution", "model import", "scale/origin", "texture relink", "export format", "mesh errors"],
      autoRepairs: ["rerun cleanup script", "convert format", "fix scale/origin", "relink textures", "export FBX/GLB", "replace broken model with fallback"]
    },
    meshy: {
      name: "Meshy API",
      checks: ["API key", "rate limit", "job status", "download URLs", "output format", "asset readiness", "network/API errors"],
      autoRepairs: ["retry after delay", "lower asset count", "use fallback prompt", "switch to local source", "queue asset for later", "mark repair-required"]
    },
    ffmpeg: {
      name: "FFmpeg",
      checks: ["ffmpeg path", "bad input", "codec support", "audio/video encode failure", "output permissions"],
      autoRepairs: ["retry safer codec", "switch export format", "reduce bitrate/resolution", "skip trailer while continuing game build"]
    },
    assetPipeline: {
      name: "Asset Import Pipeline",
      checks: ["missing file", "bad path", "unsupported extension", "missing texture", "missing licence metadata", "quality score fail"],
      autoRepairs: ["resolve from asset library", "fallback to procedural asset", "update licence manifest", "queue replacement", "block final pass if placeholder remains"]
    }
  },

  createPlan(projectState = window.projectState || {}) {
    const plan = {
      mode: "External Tool Diagnostics + Self-Repair Engine",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      goal: "Diagnose failures across external tools, apply safe repairs where possible, retry failed steps, and produce clear blocker reports when user attention is required.",
      repairLevels: [
        { level: 1, name: "Automatic safe repair", description: "Change only generated/project-local files and safe settings." },
        { level: 2, name: "Retry with safer settings", description: "Retry command with conservative flags or lower-risk settings." },
        { level: 3, name: "Fallback asset/tool path", description: "Use local/procedural fallback while marking quality impact." },
        { level: 4, name: "Pause and ask user", description: "Required for installs, accounts, licences, payments, CAPTCHA, or ambiguous destructive actions." },
        { level: 5, name: "Hard blocker report", description: "Stop the step and show exact issue, evidence, and next actions." }
      ],
      supportedTools: this.supportedTools,
      logSources: {
        unreal: ["Saved/Logs/*.log", "UnrealEditor output", "AutomationTool logs"],
        blender: ["Blender stdout/stderr", "cleanup script logs"],
        meshy: ["API responses", "job status", "download response"],
        ffmpeg: ["ffmpeg stderr", "encoder logs"],
        assetPipeline: ["GameForge import manifests", "quality reports", "licence manifests"]
      },
      safetyRules: [
        "Never bypass logins, payments, CAPTCHA, or licence acceptance.",
        "Never install unknown files or download from unapproved sources.",
        "Never delete user files outside GameForge/generated build folders.",
        "Only apply automatic repairs that are reversible or project-local.",
        "Write a diagnostic report before and after repair attempts.",
        "Escape/emergency stop must cancel queued diagnostics and repairs.",
        "If unsure, pause and ask the user."
      ],
      integrationTargets: [
        "Required App Detector",
        "Approved Tool Finder",
        "Controlled Full Automation Runner",
        "Autonomous Full Game Builder",
        "Unreal One-Click Build Runner",
        "Blender cleanup",
        "Meshy API connector",
        "FFmpeg/trailer generation",
        "High-End Asset Library",
        "Autonomous Build-Test-Repair Loop"
      ],
      retryPolicy: {
        maxRetriesPerStep: 2,
        maxRepairCycles: 3,
        retryDelaySeconds: 5,
        backoffForAPIRateLimit: true,
        stopOnUserRequiredBlocker: true
      }
    };

    this.lastPlan = plan;
    projectState.externalToolDiagnosticsPlan = plan;
    return plan;
  },

  async run(projectState = window.projectState || {}, failureContext = null) {
    const plan = this.createPlan(projectState);

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("diagnostics", "Running external tool diagnostics and self-repair");
    }

    if (!window.gameforgeAPI?.externalToolDiagnosticsRun) {
      const result = { ok: true, mode: "frontend_plan_only", plan, failureContext };
      this.lastRun = result;
      return result;
    }

    const result = await window.gameforgeAPI.externalToolDiagnosticsRun({ plan, projectState, failureContext });
    this.lastRun = result;
    projectState.externalToolDiagnosticsRun = result;
    return result;
  },

  async diagnoseFailure(tool, errorText, context = {}) {
    const plan = this.lastPlan || this.createPlan(window.projectState || {});
    if (!window.gameforgeAPI?.externalToolDiagnoseFailure) {
      return { ok: false, status: "BACKEND_UNAVAILABLE", tool, errorText, context };
    }
    const result = await window.gameforgeAPI.externalToolDiagnoseFailure({ tool, errorText, context, plan });
    this.lastRun = result;
    return result;
  },

  formatReport(report = this.lastRun || { plan: this.lastPlan }) {
    const plan = report.plan || this.lastPlan;
    if (!plan) return "No external tool diagnostics report yet.";

    return `# External Tool Diagnostics + Self-Repair Engine

Goal:
${plan.goal}

Repair Levels:
${plan.repairLevels.map(r => `- Level ${r.level}: ${r.name} — ${r.description}`).join("\n")}

Supported Tools:
${Object.entries(plan.supportedTools).map(([k,v]) => `- ${v.name}
  Checks: ${v.checks.join(", ")}
  Safe Repairs: ${v.autoRepairs.join(", ")}`).join("\n")}

Latest Results:
${(report.steps || []).map(s => `- ${s.name}: ${s.status}${s.detail ? " — " + s.detail : ""}`).join("\n") || "- not run yet"}

Blockers:
${(report.blockers || []).length ? report.blockers.map(x => "- " + x).join("\n") : "- none"}

Files:
${report.files ? Object.entries(report.files).map(([k,v]) => `- ${k}: ${typeof v === "string" ? v : JSON.stringify(v)}`).join("\n") : "- none"}`;
  },

  contextForHybridAI() {
    return `External Tool Diagnostics + Self-Repair Engine active:
- diagnoses Unreal, RunUAT, Blender, Meshy API, FFmpeg and asset pipeline failures
- applies safe project-local repairs
- retries failed steps with safer settings
- pauses for user when accounts/licences/payments/CAPTCHA/installs are required
- writes diagnostic, repair and blocker reports`;
  }
};

window.GameForgeExternalToolDiagnosticsSelfRepairEngine = GameForgeExternalToolDiagnosticsSelfRepairEngine;
