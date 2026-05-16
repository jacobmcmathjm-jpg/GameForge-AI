
const GameForgePipelineRegistry = {
  report: [],
  repaired: false,

  log(message) {
    console.log("[Pipeline Self-Repair]", message);
    const ids = ["oneClickForgeLog", "gameIntelLog", "autoAssetImportLog", "photorealModeLog", "animationImportLog"];
    for (const id of ids) {
      const el = document.getElementById(id);
      if (el) {
        el.value += `${new Date().toLocaleTimeString()} — ${message}\n`;
        el.scrollTop = el.scrollHeight;
      }
    }
  },

  entry(name, status, detail = "") {
    const item = { name, status, detail, time: new Date().toISOString() };
    this.report.push(item);
    this.log(`${name}: ${status}${detail ? " — " + detail : ""}`);
    return item;
  },

  ns(name) {
    window[name] = window[name] || {};
    return window[name];
  },

  ps() {
    window.projectState = window.projectState || {};
    projectState.prompt = projectState.prompt || "";
    projectState.scene = projectState.scene || { objects: [] };
    projectState.scene.objects = projectState.scene.objects || [];
    projectState.runtime = projectState.runtime || {};
    return projectState;
  },

  engine() {
    try { if (typeof gfEngine !== "undefined") return gfEngine; } catch(e) {}
    return window.gfEngine || window.engine || window.gameEngine || null;
  },

  addObject(name, type, position = { x: 0, y: 0, z: 0 }, extra = {}) {
    const ps = this.ps();
    const obj = { name, type, position, ...extra };
    ps.scene.objects.push(obj);
    return obj;
  },

  buildFallbackScene() {
    const ps = this.ps();
    ps.scene.objects = [
      { name: "Player Spawn", type: "player_spawn", position: { x: 0, y: 1, z: 0 } },
      { name: "Level 1 - The Road", type: "level", position: { x: 0, y: 0, z: 0 } },
      { name: "Abandoned House Exterior", type: "building", position: { x: 8, y: 0, z: -16 } },
      { name: "House Interior", type: "building", position: { x: 12, y: 0, z: -18 } },
      { name: "Basement", type: "building", position: { x: 14, y: -2, z: -20 } },
      { name: "Forest Escape", type: "environment", position: { x: -12, y: 0, z: -24 } },
      { name: "Stalker Enemy", type: "enemy", position: { x: 0, y: 0, z: -18 } },
      { name: "Flashlight Pickup", type: "pickup", position: { x: 2, y: 0, z: -2 } },
      { name: "Fuse Box Objective", type: "objective", position: { x: 6, y: 0, z: -12 } },
      { name: "Radio Objective", type: "objective", position: { x: -5, y: 0, z: -22 } },
      { name: "Escape Zone", type: "win_condition", position: { x: -15, y: 0, z: -32 } }
    ];
    ps.runtime = {
      objective: "Find the flashlight, restore power, repair the radio, and escape.",
      health: 100,
      stamina: 100,
      ammo: 30,
      levelIndex: 0,
      levels: ["The Road", "The House", "The Basement", "The Forest Escape"],
      hud: true,
      pauseMenu: true,
      winCondition: "Reach the escape zone"
    };
    return ps.scene;
  },

  repairGameIntelligence() {
    const ns = this.ns("GameForgeGameIntelligence");
    if (typeof ns.createPlan !== "function") {
      ns.createPlan = (projectState = this.ps()) => {
        const prompt = String(projectState.prompt || "");
        const plan = {
          title: projectState.title || "GameForge Generated Game",
          prompt,
          assets: [
            { name: "Player Spawn", type: "player_spawn", category: "gameplay", description: "first person player start" },
            { name: "Abandoned House", type: "building", category: "building", description: "old abandoned horror house" },
            { name: "Forest Trees", type: "environment", category: "environment", description: "dark forest tree line" },
            { name: "Road", type: "environment", category: "environment", description: "wet dark road" },
            { name: "Stalker Enemy", type: "enemy", category: "character", description: "horror enemy" },
            { name: "Flashlight", type: "pickup", category: "prop", description: "flashlight pickup" },
            { name: "Fuse Box", type: "objective", category: "prop", description: "restore power objective" },
            { name: "Radio", type: "objective", category: "prop", description: "repair radio escape objective" }
          ],
          materials: [
            { name: "wet_asphalt", preset: "wet_asphalt" },
            { name: "old_wood", preset: "weathered_wood" },
            { name: "rusty_metal", preset: "rusty_metal" },
            { name: "dirty_glass", preset: "dirty_glass" },
            { name: "zombie_skin", preset: "zombie_skin" }
          ],
          levels: ["The Road", "The House", "The Basement", "The Forest Escape"],
          objectives: ["Find the flashlight", "Enter the house", "Restore power", "Find the radio part", "Escape through the forest"]
        };
        ns.lastPlan = plan;
        return plan;
      };
      this.entry("Game Intelligence", "repaired", "fallback planner registered");
    } else this.entry("Game Intelligence", "ready");

    if (typeof ns.createDownstreamJobs !== "function") {
      ns.createDownstreamJobs = async () => ({ ok: true, fallback: true, results: { free3D: 0, internalMesh: 0, pbr: 0 } });
      this.entry("Game Intelligence jobs", "repaired", "safe fallback downstream job creator registered");
    }
  },

  repairVisualTarget() {
    const ns = this.ns("GameForgeVisualTarget");
    if (typeof ns.createRecord !== "function") {
      ns.createRecord = (projectState, options = {}) => {
        ns.lastRecord = {
          style: options.style || "realistic_horror",
          perspective: options.perspective || "first_person",
          targetQuality: options.targetQuality || "realistic_prototype",
          assets: window.GameForgeGameIntelligence?.lastPlan?.assets || []
        };
        return ns.lastRecord;
      };
      this.entry("Visual Target", "repaired", "fallback visual target registered");
    } else this.entry("Visual Target", "ready");
  },

  repairFree3D() {
    const ns = this.ns("GameForgeFree3DGenerator");
    ns.queue = ns.queue || [];

    if (typeof ns.createJob !== "function") {
      ns.createJob = async (type, name, description, style = "realistic_prototype", count = 1) => {
        const job = { id: "free3d_fallback_" + Date.now(), type, name, description, style, count, fallback: true };
        ns.queue.push(job);
        return { ok: true, job, fallback: true };
      };
      this.entry("Free 3D createJob", "repaired", "fallback job queue registered");
    } else this.entry("Free 3D createJob", "ready");

    if (typeof ns.runQueueIntoScene !== "function") {
      ns.runQueueIntoScene = async (engine) => {
        const jobs = ns.queue.length ? ns.queue : [
          { name: "Fallback House", type: "building" },
          { name: "Fallback Enemy", type: "enemy" },
          { name: "Fallback Trees", type: "environment" }
        ];
        const placed = jobs.map((job, i) => this.addObject(job.name || "Fallback Asset", job.type || "prop", { x: i * 3, y: 0, z: -6 - i * 3 }, { source: "free3d_fallback" }));
        ns.queue = [];
        return placed;
      };
      this.entry("Free 3D queue", "repaired", "fallback scene placement registered");
    } else this.entry("Free 3D queue", "ready");
  },

  repairInternalMesh() {
    const ns = this.ns("GameForgeInternalMeshGenerator");
    ns.recipes = ns.recipes || [];
    ns.materials = ns.materials || [];

    if (typeof ns.createRecipe !== "function") {
      ns.createRecipe = async (type, name, style = "realistic_prototype", detail = "medium") => {
        const recipe = { id: "mesh_recipe_" + Date.now(), type, name, style, detail, fallback: true };
        ns.recipes.push(recipe);
        return { ok: true, recipe, fallback: true };
      };
      this.entry("Internal Mesh createRecipe", "repaired", "fallback recipe creator registered");
    } else this.entry("Internal Mesh createRecipe", "ready");

    if (typeof ns.createMaterial !== "function") {
      ns.createMaterial = async (preset, name) => {
        const material = { id: "mat_" + Date.now(), preset, name, fallback: true };
        ns.materials.push(material);
        return { ok: true, material, fallback: true };
      };
      this.entry("Internal Mesh createMaterial", "repaired", "fallback material creator registered");
    } else this.entry("Internal Mesh createMaterial", "ready");

    if (typeof ns.generatePackIntoScene !== "function") {
      ns.generatePackIntoScene = async (engine, packName = "autonomous") => {
        const plan = window.GameForgeGameIntelligence?.lastPlan;
        const assets = plan?.assets?.length ? plan.assets : [
          { name: "Procedural Abandoned House", type: "building" },
          { name: "Procedural Horror Enemy", type: "enemy" },
          { name: "Procedural Trees", type: "environment" },
          { name: "Procedural Road", type: "environment" },
          { name: "Procedural Flashlight", type: "pickup" }
        ];
        return assets.map((asset, i) => this.addObject(asset.name, asset.type || asset.category || "prop", { x: (i % 4) * 4 - 6, y: 0, z: -8 - Math.floor(i / 4) * 5 }, { source: "internal_mesh_fallback", packName }));
      };
      this.entry("Internal Mesh pack", "repaired", "fallback procedural placement registered");
    } else this.entry("Internal Mesh pack", "ready");
  },

  repairSceneBuilder() {
    const engine = this.engine();
    const build = () => this.buildFallbackScene();

    if (engine) {
      if (typeof engine.generateRealisticEnvironment !== "function") {
        engine.generateRealisticEnvironment = build;
        this.entry("Scene Builder realistic environment", "repaired", "guaranteed fallback builder registered");
      } else this.entry("Scene Builder realistic environment", "ready");

      if (typeof engine.generatePlayableSurvivalPrototype !== "function") {
        engine.generatePlayableSurvivalPrototype = build;
        this.entry("Scene Builder playable prototype", "repaired", "guaranteed playable scene registered");
      } else this.entry("Scene Builder playable prototype", "ready");

      if (typeof engine.resetRuntime !== "function") {
        engine.resetRuntime = () => {
          const ps = this.ps();
          ps.runtime.health = ps.runtime.health || 100;
          ps.runtime.stamina = ps.runtime.stamina || 100;
          ps.runtime.objective = ps.runtime.objective || "Survive and complete the objective.";
        };
        this.entry("Runtime reset", "repaired", "fallback reset registered");
      } else this.entry("Runtime reset", "ready");
    } else {
      this.entry("Scene Builder", "repaired", "projectState fallback builder ready");
    }
  },

  repairPhotoreal() {
    const ns = this.ns("GameForgePhotorealMode");

    if (typeof ns.createPlan !== "function") {
      ns.createPlan = async () => {
        ns.lastPlan = {
          mode: "Photoreal Target Fallback",
          renderPipeline: { lighting: "cinematic horror", fog: "blue-grey", contrast: "high", wetSurfaceLook: true },
          pbrSurfacePack: ["wet_asphalt", "old_wood", "rusty_metal", "dirty_glass", "zombie_skin"]
        };
        return { ok: true, plan: ns.lastPlan, fallback: true };
      };
      this.entry("Photoreal plan", "repaired");
    } else this.entry("Photoreal plan", "ready");

    if (typeof ns.applyCinematicLook !== "function") {
      ns.applyCinematicLook = () => {
        const ps = this.ps();
        ps.runtime.photoreal = { lighting: "cinematic_horror", fog: "blue_grey", bloom: "subtle", fallback: true };
        return { ok: true, fallback: true };
      };
      this.entry("Photoreal lighting", "repaired");
    } else this.entry("Photoreal lighting", "ready");

    if (typeof ns.createPBRSurfacePack !== "function") {
      ns.createPBRSurfacePack = async () => ({ ok: true, fallback: true, materials: ["wet_asphalt", "old_wood", "rusty_metal", "dirty_glass", "zombie_skin"] });
      this.entry("Photoreal PBR pack", "repaired");
    } else this.entry("Photoreal PBR pack", "ready");

    if (typeof ns.applySceneDetailPass !== "function") {
      ns.applySceneDetailPass = () => {
        const placed = [
          this.addObject("Debris Cluster", "prop", { x: 3, y: 0, z: -10 }, { source: "photoreal_detail_fallback" }),
          this.addObject("Fog Layer", "effect", { x: 0, y: 1, z: -12 }, { source: "photoreal_detail_fallback" }),
          this.addObject("Puddle Detail", "prop", { x: -2, y: 0, z: -6 }, { source: "photoreal_detail_fallback" })
        ];
        return { ok: true, fallback: true, placed };
      };
      this.entry("Photoreal detail pass", "repaired");
    } else this.entry("Photoreal detail pass", "ready");
  },

  repairModelAndAnimation() {
    const model = this.ns("GameForgeModelGatherer");
    if (typeof model.createManifestFromRequirements !== "function") {
      model.createManifestFromRequirements = async () => ({ ok: true, fallback: true, manifestPath: "fallback_manifest_not_required" });
      model.runGatherer = async () => ({ ok: true, fallback: true, report: { downloaded: 0, skipped: 0, note: "No approved URLs supplied; fallback assets used." } });
      this.entry("Model Gatherer", "repaired", "safe no-download fallback registered");
    } else this.entry("Model Gatherer", "ready");

    const auto = this.ns("GameForgeAutoAssetImporter");
    if (typeof auto.runAutoImportPass !== "function") {
      auto.runAutoImportPass = async () => ({ ok: true, fallback: true, scanCount: 0, placement: { ok: true, placed: [], fallbacks: [] } });
      this.entry("Auto GLB/GLTF Importer", "repaired", "safe no-model fallback registered");
    } else this.entry("Auto GLB/GLTF Importer", "ready");

    const animGather = this.ns("GameForgeAnimationAssetGatherer");
    if (typeof animGather.createManifestFromRequirements !== "function") {
      animGather.createManifestFromRequirements = async () => ({ ok: true, fallback: true, manifestPath: "fallback_animation_manifest_not_required" });
      animGather.runGatherer = async () => ({ ok: true, fallback: true, report: { downloaded: 0, skipped: 0, note: "No approved animation URLs supplied; procedural fallback used." } });
      this.entry("Animation Asset Gatherer", "repaired", "safe no-download fallback registered");
    } else this.entry("Animation Asset Gatherer", "ready");

    const anim = this.ns("GameForgeAnimationImporter");
    if (typeof anim.runAnimationImportPass !== "function") {
      anim.runAnimationImportPass = async () => ({
        ok: true,
        fallback: true,
        assign: { ok: true, assigned: [{ role: "zombie_enemy", fallbackProcedural: true }] },
        hook: { ok: true, fallback: true }
      });
      this.entry("Animation Importer", "repaired", "procedural enemy animation fallback registered");
    } else this.entry("Animation Importer", "ready");
  },

  repairCompleteGame() {
    const ns = this.ns("GameForgeCompleteGameBuilder");
    if (typeof ns.createPlan !== "function") {
      ns.createPlan = async (projectState = this.ps()) => {
        ns.lastPlan = {
          title: projectState.title || "GameForge Full Game",
          branding: { introText: "Developed by GameForge AI" },
          modes: ["single_player", "host_coop_placeholder", "join_coop_placeholder"],
          levels: ["The Road", "The House", "The Basement", "The Forest Escape"],
          systems: ["menu", "hud", "objectives", "enemies", "settings", "export_prep"],
          fallback: true
        };
        return { ok: true, plan: ns.lastPlan, fallback: true };
      };
      this.entry("Complete Game Builder", "repaired", "fallback complete game plan registered");
    } else this.entry("Complete Game Builder", "ready");
  },

  repairAll() {
    this.report = [];
    this.log("=== Pre-flight pipeline self-repair started ===");
    this.ps();
    this.repairGameIntelligence();
    this.repairVisualTarget();
    this.repairFree3D();
    this.repairInternalMesh();
    this.repairSceneBuilder();
    this.repairPhotoreal();
    this.repairModelAndAnimation();
    this.repairCompleteGame();
    this.repaired = true;
    this.log("=== Pre-flight pipeline self-repair complete ===");
    return { ok: true, report: this.report };
  },

  async noSkipGenerate(projectState, engine, autoPlay = false) {
    this.repairAll();
    if (window.GameForgeSafePipeline?.generate) {
      return await GameForgeSafePipeline.generate(projectState || this.ps(), engine || this.engine(), autoPlay);
    }
    this.buildFallbackScene();
    return { ok: true, fallback: true, report: this.report };
  }
};

window.GameForgePipelineRegistry = GameForgePipelineRegistry;
