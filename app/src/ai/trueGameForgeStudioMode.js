
const GameForgeTrueStudioMode = {
  lastPlan: null,
  lastRun: null,

  createPlan(projectState = window.projectState || {}) {
    const prompt = projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptCopyrightSafe || projectState.prompt || projectState.description || "";

    const plan = {
      mode: "True GameForge Studio Mode",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      prompt,
      goal: "One-prompt full-game generation workflow that coordinates asset sourcing, structure generation, game design, AI, performance, build/test/repair and release readiness.",
      modules: {
        highEndAssetLibraryManager: {
          enabled: true,
          linkedPlan: projectState.highEndAssetLibraryPlan || null,
          rule: "Select, score, replace and track high-end assets before Unreal build.",
        },
        guaranteedAssetSourceResolver: {
          enabled: true,
          tiers: [
            "approved scanned / Unreal-ready assets",
            "Meshy API or approved 3D generation API",
            "local GameForge asset library",
            "procedural realistic fallback",
            "quality-blocked repair placeholder"
          ],
          rule: "There is always an asset path, but weak fallback assets cannot pass the final quality gate."
        },
        realisticStructureGenerator: {
          enabled: true,
          linkedPlan: projectState.realisticStructureGeneratorPlan || null,
          rule: "Structures must use real scale, architectural logic, PBR materials and scene dressing."
        },
        aiDirectorGameDesignerAgent: {
          enabled: true,
          outputs: ["game loop", "objectives", "level flow", "mission path", "enemy/event pacing", "difficulty curve", "win/lose conditions"]
        },
        performanceOptimisationHardwareScaling: {
          enabled: true,
          presets: ["Cinematic Ultra", "High-End PC", "Balanced PC", "Development Test Mode"],
          rule: "Optimise without breaking high-end realism target."
        },
        commercialReleaseReadiness: {
          enabled: true,
          statuses: ["Prototype Only", "Internal Testing Ready", "Public Demo Candidate", "Commercial Release Blocked", "Commercial Release Candidate"],
          rule: "Commercial status is blocked until licences, build stability and quality gates are verified."
        },
        multiplayerOnlineSystems: {
          planned: true,
          note: "Used for co-op/multiplayer prompts; otherwise disabled."
        },
        advancedAICharactersEnemies: {
          planned: true,
          note: "Used for zombies, ghosts, NPCs, animals and enemies."
        },
        proceduralOpenWorldExpansion: {
          planned: true,
          note: "Used for open-world or large-map prompts."
        },
        trailerMarketingGenerator: {
          planned: true,
          note: "Generates trailer/screenshots/marketing assets after a successful build."
        }
      },
      fullGameOutputTargets: [
        "playable build",
        "Unreal project folder",
        "asset/source/licence report",
        "quality and realism report",
        "repair history",
        "performance report",
        "release readiness status",
        "screenshots/trailer plan"
      ],
      nonPrototypeRule: "Do not call the result a full game unless core loop, objectives, UI, audio, assets, performance, packaging and test/repair gates pass."
    };

    this.lastPlan = plan;
    projectState.trueGameForgeStudioModePlan = plan;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    const plan = this.createPlan(projectState);

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("studio", "Coordinating True GameForge Studio Mode");
    }

    if (!window.gameforgeAPI?.trueStudioModeRun) {
      const result = { ok: true, mode: "frontend_plan_only", plan };
      this.lastRun = result;
      return result;
    }

    const result = await window.gameforgeAPI.trueStudioModeRun({ plan, projectState });
    this.lastRun = result;
    projectState.trueGameForgeStudioModeRun = result;
    return result;
  },

  formatPlan(plan = this.lastPlan) {
    if (!plan) return "No True Studio Mode plan yet.";
    return `# True GameForge Studio Mode

Goal:
${plan.goal}

Modules:
${Object.entries(plan.modules).map(([k,v]) => `- ${k}: ${v.enabled || v.planned ? "ON/PLANNED" : "OFF"}${v.rule ? " — " + v.rule : ""}${v.note ? " — " + v.note : ""}`).join("\n")}

Full Game Output Targets:
${plan.fullGameOutputTargets.map(x => "- " + x).join("\n")}

Non-Prototype Rule:
${plan.nonPrototypeRule}`;
  },

  contextForHybridAI() {
    return `True GameForge Studio Mode active:
- coordinates full-game generation instead of isolated prototype pieces
- includes guaranteed asset source resolver, realistic structure generator, AI director, performance, release readiness and build-test-repair coordination
- does not label result a full game unless core loop, assets, UI, audio, performance, packaging and test gates pass`;
  }
};

window.GameForgeTrueStudioMode = GameForgeTrueStudioMode;
