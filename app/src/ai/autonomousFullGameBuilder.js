
const GameForgeAutonomousFullGameBuilder = {
  lastPlan: null,
  lastRun: null,

  createPlan(projectState = window.projectState || {}) {
    const prompt = projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.prompt || projectState.description || "";

    const plan = {
      mode: "Autonomous Full Game Builder",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      prompt,
      goal: "Use one prompt to autonomously attempt a complete playable game using approved apps, APIs and Unreal Engine.",
      userFlow: [
        "User enters game idea/prompt",
        "GameForge checks required apps and APIs",
        "If missing, GameForge prompts user and offers approved official setup/download help",
        "Once required tools are available, GameForge runs the autonomous full-game pipeline"
      ],
      autonomousAfterPrompt: [
        "preflight check required apps",
        "resolve game design and core loop",
        "resolve high-end assets",
        "generate/source custom assets",
        "run Blender cleanup/conversion",
        "create Unreal project/map",
        "assemble structures, objectives, interactions, AI/events",
        "apply photoreal materials/lighting/post-process",
        "run performance preset",
        "run packaging/build",
        "run build-test-repair loop",
        "write release readiness report"
      ],
      fullGameMustHave: [
        "main menu",
        "pause/settings",
        "player controller",
        "core gameplay loop",
        "objective system",
        "interactions",
        "audio/UI feedback",
        "win/lose or completion state",
        "save/settings where appropriate",
        "realism/asset quality gate",
        "performance report",
        "packaged build attempt",
        "test/repair report",
        "licence/release report"
      ],
      blockerRules: [
        "if Unreal is missing, pause and prompt user",
        "if Blender is missing, pause asset cleanup and prompt user",
        "if Meshy API is missing, use local/procedural fallback and mark custom AI asset generation unavailable",
        "if packaging fails, parse logs and create repair report",
        "if visual quality fails, do not label full game complete",
        "if licence metadata is unknown, block commercial release status"
      ],
      outputLabels: {
        failed: "FULL GAME FAILED — REPAIR REQUIRED",
        verticalSlice: "PLAYABLE VERTICAL SLICE CANDIDATE",
        fullGame: "FULL GAME CANDIDATE",
        commercialBlocked: "COMMERCIAL RELEASE BLOCKED UNTIL VERIFIED"
      }
    };

    this.lastPlan = plan;
    projectState.autonomousFullGameBuilderPlan = plan;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    const plan = this.createPlan(projectState);

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("studio", "Running autonomous full game builder");
    }

    if (window.GameForgeRequiredAppDetectorLauncher) {
      const preflight = await GameForgeRequiredAppDetectorLauncher.run(projectState);
      if ((preflight.missingRequired || []).length) {
        const result = {
          ok: false,
          status: "PAUSED_REQUIRED_APPS_MISSING",
          plan,
          preflight,
          blockers: (preflight.missingRequired || []).map(x => `${x.name} missing. ${x.installHint}`)
        };
        this.lastRun = result;

        if (confirm("Required tools are missing. Would you like GameForge to open approved official setup/download sources now?")) {
          await GameForgeRequiredAppDetectorLauncher.approveAndResolveMissing(preflight);
        }

        return result;
      }
    }

    if (!window.gameforgeAPI?.autonomousFullGameBuild) {
      const result = { ok: true, mode: "frontend_plan_only", plan };
      this.lastRun = result;
      return result;
    }

    const result = await window.gameforgeAPI.autonomousFullGameBuild({ plan, projectState });
    this.lastRun = result;
    projectState.autonomousFullGameBuilderRun = result;
    return result;
  },

  formatReport(report = this.lastRun || { plan: this.lastPlan }) {
    const plan = report.plan || this.lastPlan;
    if (!plan) return "No Autonomous Full Game Builder report yet.";
    return `# Autonomous Full Game Builder

Goal:
${plan.goal}

User Flow:
${plan.userFlow.map(x => "- " + x).join("\n")}

Autonomous After Prompt:
${plan.autonomousAfterPrompt.map(x => "- " + x).join("\n")}

Full Game Must Have:
${plan.fullGameMustHave.map(x => "- " + x).join("\n")}

Status:
${report.status || "PLAN_ONLY"}

Blockers:
${(report.blockers || []).length ? report.blockers.map(x => "- " + x).join("\n") : "- none"}`;
  },

  contextForHybridAI() {
    return `Autonomous Full Game Builder active:
- one prompt full-game workflow
- preflight checks required apps/APIs
- offers approved official download/setup help after user approval when files are missing
- attempts Unreal assembly, photorealism, performance, packaging, build-test-repair and release report`;
  }
};

window.GameForgeAutonomousFullGameBuilder = GameForgeAutonomousFullGameBuilder;
