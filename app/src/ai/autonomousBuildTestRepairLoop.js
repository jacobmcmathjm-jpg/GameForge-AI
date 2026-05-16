
const GameForgeAutonomousBuildTestRepairLoop = {
  lastPlan: null,
  lastRun: null,

  createPlan(projectState = window.projectState || {}) {
    const prompt = projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptCopyrightSafe || projectState.prompt || projectState.description || "";
    const globalLock = projectState.globalHighEndRealismLock || null;
    const aaa = projectState.aaaPhotorealEnforcementPlan || null;

    const plan = {
      mode: "Autonomous Build-Test-Repair Loop",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      prompt,
      goal: "Generate, launch, test, screenshot, score, repair, rebuild and retry until the game passes high-end realism and playability gates or is clearly marked failed.",
      enabled: true,
      maxRepairCycles: 3,
      passThresholds: {
        visualRealismScore: 86,
        gameplayScore: 80,
        buildStabilityScore: 85,
        audioScareScore: 75,
        overallScore: 84
      },
      buildStages: [
        {
          id: "package_or_editor_launch",
          name: "Launch Unreal project or packaged build",
          test: "Can GameForge start the generated project/build without crashing?",
          repairIfFail: ["re-run Unreal one-click build", "regenerate project scaffold", "repair missing map/default project settings"]
        },
        {
          id: "screenshot_capture",
          name: "Capture required screenshots",
          test: "Can GameForge capture visual evidence from key locations?",
          repairIfFail: ["create screenshot camera points", "rebuild default map", "re-run editor automation"]
        },
        {
          id: "visual_realism_score",
          name: "Score high-end realism",
          test: "Do screenshots pass realism, lighting, material, character and environment quality gates?",
          repairIfFail: ["replace weak assets", "upgrade PBR material assignments", "adjust lighting/post-process", "increase scene dressing", "regenerate bad Meshy assets"]
        },
        {
          id: "gameplay_test",
          name: "Run core gameplay route",
          test: "Can the player move, interact, trigger objectives and complete a short path?",
          repairIfFail: ["fix player start", "fix input mapping", "fix collision", "fix objective trigger", "fix door interaction"]
        },
        {
          id: "event_test",
          name: "Run scare/event/audio tests",
          test: "Do devices, jump scares, audio stingers, lights and ghost events trigger correctly?",
          repairIfFail: ["rebuild trigger volumes", "reassign audio cues", "reconnect ghost apparition blueprint", "repair cooldown/chance rules"]
        },
        {
          id: "log_parse",
          name: "Parse Unreal/build logs",
          test: "Are there missing asset/material/blueprint/package errors?",
          repairIfFail: ["regenerate missing manifests", "reassign missing materials", "remove broken references", "retry BuildCookRun"]
        }
      ],
      screenshotRequirements: [
        {
          id: "hero_environment",
          description: "wide hero shot of main environment",
          failIf: ["empty scene", "flat lighting", "low-poly buildings", "weak material detail"]
        },
        {
          id: "character_or_enemy_closeup",
          description: "close-up of human, ghost, zombie, creature or main character",
          failIf: ["mannequin look", "bad anatomy", "plastic skin", "missing rig", "cartoon-only proportions"]
        },
        {
          id: "gameplay_interaction",
          description: "player interacting with door, device, weapon, tool or objective",
          failIf: ["missing hands/device", "placeholder UI", "broken interaction", "low-quality prop"]
        },
        {
          id: "lighting_material_check",
          description: "surface close-up showing PBR material and shadows",
          failIf: ["flat material", "missing normal/roughness", "stretched texture", "bad reflections"]
        },
        {
          id: "event_moment",
          description: "action, scare, combat, puzzle, objective or high-interest moment",
          failIf: ["no event feedback", "no audio/visual response", "weak cinematic framing"]
        }
      ],
      scoringRubric: {
        visualRealism: [
          "realistic lighting",
          "PBR materials",
          "proper scale",
          "detailed environment",
          "believable characters",
          "cinematic framing",
          "no indie/placeholder visuals"
        ],
        gameplay: [
          "player movement works",
          "camera works",
          "interactions work",
          "objective path works",
          "events trigger",
          "audio plays",
          "no soft-lock on test route"
        ],
        buildStability: [
          "Unreal opens",
          "map loads",
          "packaging completes or produces actionable errors",
          "logs parsed",
          "critical errors absent"
        ]
      },
      repairCycleRules: {
        stopWhenPassed: true,
        stopWhenMaxCyclesReached: true,
        preserveFailedPrototype: true,
        failedOutputLabel: "FAILED VISUAL QUALITY — REPAIR REQUIRED",
        passedOutputLabel: "HIGH-END REALISM CANDIDATE",
        commercialReadyLabelBlocked: true
      },
      futureUpgradeNotes: {
        v4_2: "Asset Source Marketplace + Realism Library Manager",
        v4_3: "AI Director / Game Designer Agent",
        v4_4: "Performance Optimisation + Hardware Scaling",
        v4_5: "Commercial Release Readiness System"
      },
      linkedSystems: {
        globalHighEndRealismLock: Boolean(globalLock),
        aaaPhotorealEnforcement: Boolean(aaa),
        scannedAssetCharacterConnector: Boolean(projectState.scannedAssetCharacterRealismPlan),
        controlledAutomation: Boolean(projectState.controlledFullAutomationPlan),
        unrealOneClickBuild: Boolean(projectState.unrealOneClickBuildPlan)
      }
    };

    this.lastPlan = plan;
    projectState.autonomousBuildTestRepairPlan = plan;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    const plan = this.createPlan(projectState);

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("testing", "Running autonomous build-test-repair loop");
    }

    if (!window.gameforgeAPI?.autonomousBuildTestRepair) {
      const result = { ok: true, mode: "frontend_plan_only", plan };
      this.lastRun = result;
      return result;
    }

    const result = await window.gameforgeAPI.autonomousBuildTestRepair({ plan, projectState });
    this.lastRun = result;
    projectState.autonomousBuildTestRepairRun = result;
    return result;
  },

  formatReport(report = this.lastRun || { plan: this.lastPlan }) {
    const plan = report.plan || this.lastPlan;
    if (!plan) return "No autonomous build-test-repair report yet.";

    return `# Autonomous Build-Test-Repair Loop

Goal:
${plan.goal}

Pass Thresholds:
${Object.entries(plan.passThresholds).map(([k,v]) => `- ${k}: ${v}`).join("\n")}

Build/Test Stages:
${plan.buildStages.map(s => `- ${s.name}
  Test: ${s.test}
  Repair: ${s.repairIfFail.join(", ")}`).join("\n")}

Screenshot Requirements:
${plan.screenshotRequirements.map(s => `- ${s.id}: ${s.description}`).join("\n")}

Repair Cycle Rules:
${Object.entries(plan.repairCycleRules).map(([k,v]) => `- ${k}: ${v}`).join("\n")}

Latest Results:
${(report.steps || []).map(s => `- ${s.name}: ${s.status}${s.detail ? " — " + s.detail : ""}`).join("\n") || "- not run yet"}

Blockers:
${(report.blockers || []).length ? report.blockers.map(x => "- " + x).join("\n") : "- none"}

Files:
${report.files ? Object.entries(report.files).map(([k,v]) => `- ${k}: ${typeof v === "string" ? v : JSON.stringify(v)}`).join("\n") : "- none"}`;
  },

  contextForHybridAI() {
    return `Autonomous Build-Test-Repair Loop active:
- launches generated project/build where possible
- captures required screenshots
- scores visual realism, gameplay, build stability and audio/event quality
- repairs weak assets/materials/lighting/characters/collision/events/log errors
- retries up to max repair cycles
- blocks finished label unless quality gates pass`;
  }
};

window.GameForgeAutonomousBuildTestRepairLoop = GameForgeAutonomousBuildTestRepairLoop;
