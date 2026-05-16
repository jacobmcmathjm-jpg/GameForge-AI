
const GameForgeUnrealAutoRepairRunner = {
  lastReport: null,

  log(message) {
    console.log("[Unreal Auto Repair Runner]", message);
    for (const id of ["oneClickForgeLog", "gameIntelLog", "assetDownloaderLog", "photorealModeLog"]) {
      const el = document.getElementById(id);
      if (el) {
        el.value += `${new Date().toLocaleTimeString()} — ${message}\n`;
        el.scrollTop = el.scrollHeight;
      }
    }
  },

  createRepairPlan(projectState = window.projectState || {}) {
    const quality = projectState.photorealQualityReport || window.GameForgePhotorealQualityGate?.lastReport || null;
    const toolchain = projectState.toolchainRun?.detection || null;
    const meshy = projectState.liveMeshySceneRun || projectState.meshyAutonomousApiRun || null;
    const unreal = projectState.unrealPhotorealBuildResult || null;

    const plan = {
      mode: "Unreal Automation + Auto Repair Runner",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      prompt: projectState.prompt || projectState.description || "",
      goal: "Automatically detect issues, repair safe pipeline problems, re-run validation, and improve photoreal readiness.",
      currentStatus: {
        qualityStatus: quality?.status || "Not yet scored",
        qualityScore: quality?.score ?? null,
        hasMeshyRun: Boolean(meshy),
        hasUnrealPackage: Boolean(unreal),
        hasToolchainDetection: Boolean(toolchain)
      },
      repairCycles: 3,
      checks: [
        "Node/npm app startup check",
        "Meshy API settings check",
        "Meshy asset generation/import check",
        "Blender detection/check",
        "Unreal detection/check",
        "Unreal handoff/package check",
        "PBR material plan check",
        "lighting/post-process plan check",
        "scene dressing density check",
        "photoreal quality gate re-score"
      ],
      safeAutoRepairs: [
        "create missing output folders",
        "create missing import manifests",
        "create missing legal metadata templates",
        "create missing Blender handoff script",
        "create missing Unreal import stub",
        "create missing material/lighting/post-process plan",
        "re-run quality gate after fixes",
        "write repair report and next actions"
      ],
      cannotAutoRepairWithoutUser: [
        "install Unreal Engine if missing",
        "install Blender if missing",
        "provide Meshy API key/subscription",
        "accept third-party licences",
        "fix provider billing/credit errors",
        "manually approve commercial release licensing"
      ]
    };

    return plan;
  },

  async run(projectState = window.projectState || {}) {
    this.log("Starting auto test / auto repair cycle.");
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("export", "Running auto test and repair cycle");

    const plan = this.createRepairPlan(projectState);

    if (!window.gameforgeAPI?.unrealAutoRepairRun) {
      return { ok: false, error: "Backend auto repair runner unavailable", plan };
    }

    const result = await window.gameforgeAPI.unrealAutoRepairRun({ plan, projectState });
    this.lastReport = result;

    if (result?.ok) {
      this.log(`Auto repair complete. Cycles: ${result.summary?.cyclesRun || 0}, repairs: ${result.summary?.repairsApplied || 0}.`);
      projectState.unrealAutoRepairRun = result;

      if (window.GameForgePhotorealQualityGate) {
        try {
          const q = GameForgePhotorealQualityGate.run(projectState, window.gfEngine || null);
          projectState.photorealQualityReport = q.report;
        } catch (e) {
          this.log("Quality gate after repair warning: " + e.message);
        }
      }
    } else {
      this.log("Auto repair warning: " + (result?.error || "unknown issue"));
    }

    document.dispatchEvent(new CustomEvent("gf-unreal-auto-repair-complete", { detail: result }));
    return result;
  },

  formatReport(report = this.lastReport) {
    if (!report) return "No auto repair report yet.";
    return `# Unreal Auto Repair Runner

Status: ${report.ok ? "OK" : "Warning"}
Cycles Run: ${report.summary?.cyclesRun ?? "N/A"}
Repairs Applied: ${report.summary?.repairsApplied ?? "N/A"}
Blockers: ${report.summary?.blockers ?? "N/A"}

Checks:
${(report.checks || []).map(c => `- ${c.name}: ${c.status}${c.detail ? " — " + c.detail : ""}`).join("\n")}

Repairs:
${(report.repairs || []).map(r => `- ${r}`).join("\n")}

Remaining Blockers:
${(report.remainingBlockers || []).map(b => `- ${b}`).join("\n")}

Files:
${report.files ? Object.entries(report.files).map(([k,v]) => `- ${k}: ${v}`).join("\n") : "- none"}`;
  },

  contextForHybridAI() {
    return `Unreal Auto Repair Runner available:
- detects missing tools/settings/assets/manifests
- applies safe repairs
- creates Unreal automation/import/test scripts
- runs repair cycles and photoreal quality gate re-score
- reports blockers that require user action`;
  }
};

window.GameForgeUnrealAutoRepairRunner = GameForgeUnrealAutoRepairRunner;
