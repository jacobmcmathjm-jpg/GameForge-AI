
const GameForgeSafePipeline = {
  lastReport: [],

  log(message) {
    console.log("[GameForge Safe Pipeline]", message);
    const ids = ["oneClickForgeLog", "gameIntelLog", "autoAssetImportLog", "photorealModeLog"];
    for (const id of ids) {
      const el = document.getElementById(id);
      if (el) {
        el.value += `${new Date().toLocaleTimeString()} — ${message}\n`;
        el.scrollTop = el.scrollHeight;
      }
    }
  },

  async step(label, fn) {
    this.log("Starting: " + label);
    if (window.GameForgeGenerationETA) {
      const map = {
        "Game Intelligence": "input",
        "Visual Target": "photoreal",
        "Model Gatherer Manifest": "assets",
        "Model Gatherer Download": "assets",
        "Auto GLB/GLTF Import": "assets",
        "Animation Asset Gatherer": "animation",
        "Base Playable Scene": "scene",
        "Free 3D Assets": "scene",
        "Internal Mesh Pack": "scene",
        "Rigged Animation Importer": "animation",
        "Photoreal Mode": "lighting",
        "Complete Game Plan / EXE Prep": "export",
        "Runtime Reset / Refresh": "gameplay"
      };
      GameForgeGenerationETA.setStage(map[label] || "gameplay", label);
    }
    try {
      const value = await fn();
      this.log("Finished: " + label);
      const result = { ok: true, label, value };
      this.lastReport.push(result);
      return result;
    } catch (error) {
      const result = { ok: false, label, error: error?.message || String(error) };
      this.lastReport.push(result);
      this.log("Skipped after error in " + label + ": " + result.error);
      return result;
    }
  },

  getEngine(engine) {
    if (engine) return engine;
    if (window.gfEngine) return window.gfEngine;
    if (window.engine) return window.engine;
    if (window.gameEngine) return window.gameEngine;
    try { if (typeof gfEngine !== "undefined") return gfEngine; } catch(e) {}
    return null;
  },

  ensureProjectState() {
    window.projectState = window.projectState || {};
    projectState.scene = projectState.scene || {};
    projectState.scene.objects = projectState.scene.objects || [];
    projectState.runtime = projectState.runtime || {};
    return projectState;
  },

  async createStableFallbackScene(engine) {
    const ps = this.ensureProjectState();

    if (engine && typeof engine.generatePlayableSurvivalPrototype === "function") {
      return engine.generatePlayableSurvivalPrototype();
    }

    if (engine && typeof engine.generateRealisticEnvironment === "function") {
      return engine.generateRealisticEnvironment();
    }

    if (typeof forgeDraft === "function") {
      return forgeDraft();
    }

    ps.scene.objects = [
      { name: "Player Spawn", type: "player_spawn", position: { x: 0, y: 1, z: 0 } },
      { name: "Abandoned House", type: "building", position: { x: 8, y: 0, z: -14 } },
      { name: "Dark Road", type: "environment", position: { x: 0, y: 0, z: -8 } },
      { name: "Forest Tree Line", type: "environment", position: { x: -8, y: 0, z: -12 } },
      { name: "Zombie Enemy", type: "enemy", position: { x: 0, y: 0, z: -18 } },
      { name: "Flashlight Pickup", type: "pickup", position: { x: 2, y: 0, z: -2 } },
      { name: "Fuse Objective", type: "objective", position: { x: 4, y: 0, z: -10 } },
      { name: "Radio Escape Objective", type: "objective", position: { x: -4, y: 0, z: -16 } }
    ];

    ps.runtime = {
      objective: "Find the flashlight, restore power, repair the radio and escape.",
      health: 100,
      stamina: 100,
      ammo: 30,
      levels: ["The Road", "The House", "The Basement", "The Forest Escape"]
    };

    return ps.scene;
  },

  async generate(projectStateArg, engineArg, autoPlay = false) {
    // pipeline self-repair preflight
    if (window.GameForgePipelineRegistry && !GameForgePipelineRegistry.repaired) {
      GameForgePipelineRegistry.repairAll();
    }
    const ps = projectStateArg || this.ensureProjectState();
    const engine = this.getEngine(engineArg);
    this.lastReport = [];

    if (!ps.prompt || !String(ps.prompt).trim()) {
      alert("Paste a game prompt into the main Game Description / Generation box first.");
      return { ok: false, error: "Missing prompt" };
    }

    this.log("=== Starting v2.3 Safe Whole-Pipeline Generation ===");

    await this.step("Game Intelligence", async () => {
      if (window.GameForgeGameIntelligence?.createPlan) return GameForgeGameIntelligence.createPlan(ps);
      return { fallback: true, prompt: ps.prompt };
    });

    await this.step("Visual Target", async () => {
      if (window.GameForgeVisualTarget?.createRecord) {
        return GameForgeVisualTarget.createRecord(ps, {
          style: "realistic_horror",
          perspective: "first_person",
          targetQuality: "realistic_prototype"
        });
      }
      return { fallback: true };
    });

    await this.step("Model Gatherer Manifest", async () => {
      if (window.GameForgeModelGatherer?.createManifestFromRequirements) {
        return await GameForgeModelGatherer.createManifestFromRequirements();
      }
      return { skipped: true };
    });

    await this.step("Model Gatherer Download", async () => {
      if (window.GameForgeModelGatherer?.lastManifestPath && window.GameForgeModelGatherer?.runGatherer) {
        return await GameForgeModelGatherer.runGatherer(GameForgeModelGatherer.lastManifestPath);
      }
      return { skipped: true, reason: "No enabled approved model URLs" };
    });

    await this.step("Auto GLB/GLTF Import", async () => {
      if (window.GameForgeAutoAssetImporter?.runAutoImportPass) {
        return await GameForgeAutoAssetImporter.runAutoImportPass(engine);
      }
      return { skipped: true };
    });

    await this.step("Animation Asset Gatherer", async () => {
      if (window.GameForgeAnimationAssetGatherer?.createManifestFromRequirements) {
        await GameForgeAnimationAssetGatherer.createManifestFromRequirements();
        if (GameForgeAnimationAssetGatherer.lastManifestPath && GameForgeAnimationAssetGatherer.runGatherer) {
          return await GameForgeAnimationAssetGatherer.runGatherer(GameForgeAnimationAssetGatherer.lastManifestPath);
        }
      }
      return { skipped: true, reason: "No enabled approved animation URLs" };
    });

    await this.step("Base Playable Scene", async () => {
      return await this.createStableFallbackScene(engine);
    });

    await this.step("Free 3D Assets", async () => {
      if (typeof safeRunFree3DQueueIntoScene === "function") return await safeRunFree3DQueueIntoScene(engine);
      if (window.GameForgeFree3DGenerator?.runQueueIntoScene) return await GameForgeFree3DGenerator.runQueueIntoScene(engine);
      return { skipped: true };
    });

    await this.step("Internal Mesh Pack", async () => {
      if (typeof safeGenerateInternalMeshPackIntoScene === "function") return await safeGenerateInternalMeshPackIntoScene(engine, "autonomous");
      if (window.GameForgeInternalMeshGenerator?.generatePackIntoScene) return await GameForgeInternalMeshGenerator.generatePackIntoScene(engine, "autonomous");
      return { skipped: true };
    });

    await this.step("Rigged Animation Importer", async () => {
      if (window.GameForgeAnimationImporter?.runAnimationImportPass) return await GameForgeAnimationImporter.runAnimationImportPass(engine);
      return { skipped: true };
    });

    await this.step("Photoreal Mode", async () => {
      const out = {};
      if (window.GameForgePhotorealMode?.createPlan) out.plan = await GameForgePhotorealMode.createPlan(ps);
      if (window.GameForgePhotorealMode?.applyCinematicLook) out.look = GameForgePhotorealMode.applyCinematicLook(engine);
      if (window.GameForgePhotorealMode?.createPBRSurfacePack) out.pbr = await GameForgePhotorealMode.createPBRSurfacePack();
      if (window.GameForgePhotorealMode?.applySceneDetailPass) out.detail = GameForgePhotorealMode.applySceneDetailPass(engine);
      return out;
    });

    await this.step("Complete Game Plan / EXE Prep", async () => {
      if (window.GameForgeCompleteGameBuilder?.createPlan) return await GameForgeCompleteGameBuilder.createPlan(ps);
      return { skipped: true };
    });

    await this.step("Runtime Reset / Refresh", async () => {
      if (engine && typeof engine.resetRuntime === "function") engine.resetRuntime();
      try { if (typeof refreshHierarchy === "function") refreshHierarchy(); } catch(e) {}
      return { refreshed: true };
    });

    try {
      if (typeof setPanel === "function") setPanel("studio");
    } catch(e) {}

    if (autoPlay && engine && typeof engine.startPlayMode === "function") {
      await this.step("Auto Play", async () => engine.startPlayMode());
    }

    const failed = this.lastReport.filter(x => !x.ok);
    this.log(`=== Safe pipeline complete. Steps: ${this.lastReport.length}. Skipped/failed optional steps: ${failed.length}. ===`);

    return {
      ok: true,
      report: this.lastReport,
      skippedOptional: failed.length
    };
  }
};

window.GameForgeSafePipeline = GameForgeSafePipeline;
