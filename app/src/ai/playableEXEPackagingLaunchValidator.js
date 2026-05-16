
const GameForgePlayableEXEPackagingLaunchValidator = {
  lastPlan: null,
  lastRun: null,

  createPlan(projectState = window.projectState || {}) {
    const plan = {
      mode: "Playable EXE Packaging + Launch Validator",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      goal: "Verify the generated game is not just an Unreal project: it must package to a Windows EXE, launch, avoid instant crash, reach menu, start game and validate core gameplay.",
      validationSteps: [
        "find packaged Windows build folder",
        "detect game .exe",
        "launch EXE in validation mode",
        "wait for process start",
        "detect instant crash",
        "check logs for fatal errors",
        "verify main menu or first map loaded",
        "start new game where automation hook exists",
        "verify player spawn",
        "verify movement/input",
        "verify flashlight/action input",
        "verify one interaction/objective trigger",
        "verify audio event",
        "verify quit/exit path"
      ],
      outputLabels: [
        "PLAYABLE EXE VERIFIED",
        "EXE BUILT BUT LAUNCH FAILED",
        "EXE BUILT BUT GAMEPLAY VALIDATION FAILED",
        "PACKAGING FAILED",
        "PROJECT ONLY — NOT PLAYABLE EXE",
        "REPAIR REQUIRED"
      ],
      repairActions: [
        "rerun RunUAT packaging",
        "switch to safer Development config",
        "repair default map/game mode",
        "repair input mapping",
        "repair missing DLL/dependency blocker",
        "clean Saved/Intermediate",
        "parse crash log",
        "create blocker report"
      ]
    };

    this.lastPlan = plan;
    projectState.playableEXEValidatorPlan = plan;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    const plan = this.createPlan(projectState);

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("validation", "Validating playable EXE");
    }

    if (!window.gameforgeAPI?.playableEXEValidate) {
      const result = { ok: true, mode: "frontend_plan_only", plan };
      this.lastRun = result;
      return result;
    }

    const result = await window.gameforgeAPI.playableEXEValidate({ plan, projectState });
    this.lastRun = result;
    projectState.playableEXEValidatorRun = result;
    return result;
  },

  formatReport(report = this.lastRun || { plan: this.lastPlan }) {
    const plan = report.plan || this.lastPlan;
    if (!plan) return "No playable EXE validation report yet.";
    return `# Playable EXE Packaging + Launch Validator

Goal:
${plan.goal}

Validation Steps:
${plan.validationSteps.map(x => "- " + x).join("\n")}

Output Labels:
${plan.outputLabels.map(x => "- " + x).join("\n")}

Latest Status:
${report.status || "PLAN_READY"}

Files:
${report.files ? Object.entries(report.files).map(([k,v]) => `- ${k}: ${typeof v === "string" ? v : JSON.stringify(v)}`).join("\n") : "- none"}`;
  },

  contextForHybridAI() {
    return `Playable EXE Packaging + Launch Validator active:
- detects packaged Windows build folder and game EXE
- validates launch, crash status, menu/new game/player spawn/movement/flashlight/interaction/audio/exit
- blocks playable label unless EXE is verified`;
  }
};

window.GameForgePlayableEXEPackagingLaunchValidator = GameForgePlayableEXEPackagingLaunchValidator;
