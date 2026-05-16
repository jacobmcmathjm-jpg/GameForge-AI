
const GameForgeControlledFullAutomationRunner = {
  enabled: false,
  emergencyStopped: false,
  lastPlan: null,
  lastRun: null,

  approvedTools: [
    "GameForge",
    "Meshy API",
    "Blender",
    "UnrealEditor",
    "RunUAT",
    "FFmpeg",
    "GameForge generated folders"
  ],

  blockedActions: [
    "access payment pages",
    "enter passwords",
    "bypass CAPTCHA",
    "open private documents",
    "send email/messages",
    "delete files outside GameForge folders",
    "browse unrelated websites",
    "change unrelated system settings"
  ],

  log(message) {
    console.log("[Controlled Full Automation]", message);
    for (const id of ["oneClickForgeLog", "gameIntelLog", "assetDownloaderLog", "photorealModeLog", "forgeReport"]) {
      const el = document.getElementById(id);
      if (!el) continue;
      const line = `${new Date().toLocaleTimeString()} — ${message}`;
      if (id === "forgeReport") {
        el.value = el.value ? `${el.value}\n${line}` : line;
      } else {
        el.value += `${line}\n`;
      }
      el.scrollTop = el.scrollHeight;
    }
  },

  enable() {
    this.enabled = true;
    this.emergencyStopped = false;
    localStorage.setItem("gameforge_controlled_automation_enabled", "true");
    this.log("Controlled automation enabled.");
  },

  disable(reason = "disabled by user") {
    this.enabled = false;
    localStorage.setItem("gameforge_controlled_automation_enabled", "false");
    this.log("Controlled automation disabled: " + reason);
  },

  emergencyStop(reason = "Escape pressed") {
    this.emergencyStopped = true;
    this.enabled = false;
    localStorage.setItem("gameforge_controlled_automation_enabled", "false");
    this.log("EMERGENCY STOP: " + reason);
    if (window.gameforgeAPI?.automationEmergencyStop) {
      window.gameforgeAPI.automationEmergencyStop({ reason }).catch(() => {});
    }
  },

  createPlan(projectState = window.projectState || {}) {
    const meshySettings = projectState.meshySettings || projectState.meshySavedSettings || {};
    const plan = {
      mode: "Controlled Full Automation Runner",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      prompt: projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptCopyrightSafe || projectState.prompt || projectState.description || "",
      enabled: this.enabled,
      emergencyStopped: this.emergencyStopped,
      safetyBoundary: {
        approvedTools: this.approvedTools,
        blockedActions: this.blockedActions,
        escapeEmergencyStop: true,
        dryRunFirstAvailable: true,
        userApprovalRequiredFor: [
          "first automation enable",
          "third-party account login",
          "payment/subscription changes",
          "licence approval",
          "deleting generated projects",
          "opening external web pages"
        ]
      },
      autonomousStages: [
        {
          id: "preflight",
          name: "Preflight tool detection",
          tools: ["Node", "Blender", "UnrealEditor", "RunUAT", "FFmpeg"],
          autonomous: true
        },
        {
          id: "meshy_assets",
          name: "Meshy asset generation",
          tools: ["Meshy API"],
          autonomous: "only if API key/mode is enabled",
          fallback: "create Meshy prompt pack and pause for manual third-party steps"
        },
        {
          id: "download_assets",
          name: "Download/import generated assets",
          tools: ["Meshy API", "GameForge folders"],
          autonomous: true
        },
        {
          id: "blender_cleanup",
          name: "Blender cleanup and conversion",
          tools: ["Blender"],
          autonomous: true
        },
        {
          id: "unreal_project",
          name: "Create/update Unreal project",
          tools: ["UnrealEditor"],
          autonomous: true
        },
        {
          id: "unreal_import",
          name: "Run Unreal Python import/assembly scripts",
          tools: ["UnrealEditor", "Unreal Python"],
          autonomous: true
        },
        {
          id: "photoreal_polish",
          name: "Apply photoreal lighting/material/post-process plan",
          tools: ["UnrealEditor"],
          autonomous: true
        },
        {
          id: "package_build",
          name: "Run RunUAT BuildCookRun packaging",
          tools: ["RunUAT"],
          autonomous: true
        },
        {
          id: "log_watch_repair",
          name: "Watch logs and run safe repair/retry",
          tools: ["GameForge", "Unreal logs"],
          autonomous: true
        }
      ],
      rules: [
        "Prefer APIs and command-line automation over mouse/keyboard UI automation.",
        "Do not automate third-party login, payment, CAPTCHA, or licence acceptance.",
        "Never access private user folders except generated/import folders selected by GameForge.",
        "All automation actions must be logged.",
        "Escape stops all queued automation and kills only GameForge-started child processes.",
        "If Meshy API is not enabled, generate prompt packs and pause with exact instructions."
      ]
    };

    this.lastPlan = plan;
    projectState.controlledFullAutomationPlan = plan;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    if (!this.enabled && localStorage.getItem("gameforge_controlled_automation_enabled") === "true") {
      this.enabled = true;
    }

    const plan = this.createPlan(projectState);

    if (!this.enabled) {
      this.log("Controlled automation is not enabled. Running plan/report mode only.");
      return { ok: true, status: "PLAN_ONLY_NOT_ENABLED", plan };
    }

    if (this.emergencyStopped) {
      return { ok: false, status: "EMERGENCY_STOPPED", plan };
    }

    this.log("Starting controlled full automation pipeline.");
    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("export", "Running controlled full automation pipeline");
    }

    if (!window.gameforgeAPI?.controlledAutomationRun) {
      return { ok: false, status: "BACKEND_UNAVAILABLE", error: "Controlled automation backend unavailable", plan };
    }

    const result = await window.gameforgeAPI.controlledAutomationRun({ plan, projectState });
    this.lastRun = result;
    projectState.controlledFullAutomationRun = result;

    if (result?.ok) {
      this.log("Controlled full automation completed: " + (result.status || "OK"));
    } else {
      this.log("Controlled full automation warning: " + (result?.status || result?.error || "unknown issue"));
    }

    document.dispatchEvent(new CustomEvent("gf-controlled-full-automation-complete", { detail: result }));
    return result;
  },

  formatReport(report = this.lastRun || { plan: this.lastPlan }) {
    const plan = report.plan || this.lastPlan;
    if (!plan) return "No controlled automation report yet.";

    return `# Controlled Full Automation Runner

Status: ${report.status || "PLAN"}
Enabled: ${plan.enabled}
Emergency Stopped: ${plan.emergencyStopped}

Approved Tools:
${plan.safetyBoundary.approvedTools.map(x => "- " + x).join("\n")}

Blocked Actions:
${plan.safetyBoundary.blockedActions.map(x => "- " + x).join("\n")}

Stages:
${plan.autonomousStages.map(s => `- ${s.name}: ${s.autonomous}`).join("\n")}

Results:
${(report.steps || []).map(s => `- ${s.name}: ${s.status}${s.detail ? " — " + s.detail : ""}`).join("\n") || "- not run yet"}

Blockers:
${(report.blockers || []).length ? report.blockers.map(x => "- " + x).join("\n") : "- none"}

Files:
${report.files ? Object.entries(report.files).map(([k,v]) => `- ${k}: ${typeof v === "string" ? v : JSON.stringify(v)}`).join("\n") : "- none"}`;
  },

  injectUI() {
    if (document.getElementById("controlledAutomationPanel")) return;

    const panel = document.createElement("div");
    panel.id = "controlledAutomationPanel";
    panel.className = "controlled-automation-panel advanced-panel";
    panel.innerHTML = `
      <label class="gf-label">Controlled Full Automation</label>
      <div class="controlled-row">
        <button id="gfEnableAutomation" class="gf-mini-button">Enable Automation</button>
        <button id="gfDisableAutomation" class="gf-mini-button danger">Disable</button>
      </div>
      <p class="gf-help">Controls only approved game-build tools. Press Escape anytime for emergency stop.</p>
    `;

    const target = document.querySelector(".sidebar") || document.querySelector("main") || document.body;
    target.prepend(panel);

    document.getElementById("gfEnableAutomation")?.addEventListener("click", () => {
      this.enable();
      alert("Controlled automation enabled. Press Escape anytime to stop GameForge-started automation.");
    });

    document.getElementById("gfDisableAutomation")?.addEventListener("click", () => {
      this.disable("manual button");
    });
  },

  init() {
    this.enabled = localStorage.getItem("gameforge_controlled_automation_enabled") === "true";
    this.injectUI();

    window.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        this.emergencyStop("Escape pressed");
      }
    });
  },

  contextForHybridAI() {
    return `Controlled Full Automation Runner active:
- drives the GameForge build pipeline through approved tools only
- Meshy is fully autonomous only when API key/mode is enabled
- Blender/Unreal/RunUAT can be launched with command automation
- Escape emergency stop kills GameForge-started child processes and cancels queued steps
- blocks payment/login/CAPTCHA/private file/random web automation`;
  }
};

window.GameForgeControlledFullAutomationRunner = GameForgeControlledFullAutomationRunner;
window.addEventListener("DOMContentLoaded", () => {
  try { GameForgeControlledFullAutomationRunner.init(); }
  catch (error) { console.warn("[ControlledFullAutomationRunner] init warning:", error.message); }
});
