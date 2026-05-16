
const GameForgeLiveMeshySceneBuilder = {
  lastBlueprint: null,
  lastRun: null,

  log(message) {
    console.log("[Live Meshy Scene Builder]", message);
    for (const id of ["oneClickForgeLog", "gameIntelLog", "assetDownloaderLog", "photorealModeLog"]) {
      const el = document.getElementById(id);
      if (el) {
        el.value += `${new Date().toLocaleTimeString()} — ${message}\n`;
        el.scrollTop = el.scrollHeight;
      }
    }
  },

  inferSceneType(projectState = window.projectState || {}) {
    const rawPrompt = String(projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptCopyrightSafe || projectState.prompt || projectState.description || "");
    if (window.GameForgeGameStyleRatingManager?.isPromptBlocked?.(rawPrompt)) return "blocked_explicit_content";
    const prompt = rawPrompt.toLowerCase();
    if (/zombie|outbreak|open world|road|forest|survival/.test(prompt)) return "open_world_zombie";
    if (/haunted|ghost|house|horror|hallway|attic|basement/.test(prompt)) return "haunted_house";
    return "generic_photoreal";
  },

  createBlueprint(projectState = window.projectState || {}) {
    const sceneType = this.inferSceneType(projectState);
    const gameName = projectState.name || "GameForge Game";
    const ratingPreset = window.GameForgeGameStyleRatingManager?.getPreset ? GameForgeGameStyleRatingManager.getPreset() : null;
    const baseStyle = "photorealistic, realistic proportions, game-ready 3D model, PBR materials, high detail, original design, no copyrighted IP, no real-world brands, no famous characters" + (ratingPreset ? ", " + ratingPreset.meshStyle : "");

    const hauntedAssets = [
      { id: "haunted_hallway_modular_set", role: "level_module", type: "interior", priority: 1, position: {x:0,y:0,z:-12}, scale: {x:1,y:1,z:1}, prompt: `A modular haunted house hallway interior kit with peeling wallpaper, timber wall panels, damaged ceiling, old doors and floorboards, ${baseStyle}`, texturePrompt: "peeling wallpaper, wet dark wood, cracked plaster, grime, dust, subtle mould, normal and roughness detail" },
      { id: "old_radiator", role: "prop", type: "metal_prop", priority: 1, position: {x:4,y:0,z:-10}, scale: {x:1,y:1,z:1}, prompt: `An old cast iron radiator for a haunted hallway, rusted and dusty, ${baseStyle}`, texturePrompt: "rusty metal, chipped paint, dust, roughness, ambient occlusion" },
      { id: "side_table_lamp", role: "practical_light_prop", type: "prop", priority: 1, position: {x:2.7,y:0,z:-12}, scale: {x:1,y:1,z:1}, prompt: `A small antique side table with a warm old lamp and photo frame, horror game prop, ${baseStyle}`, texturePrompt: "aged wood, stained lampshade, brass, dust, warm emissive lamp material" },
      { id: "ghost_woman", role: "enemy_character", type: "character", priority: 1, position: {x:0,y:0,z:-18}, scale: {x:1,y:1,z:1}, prompt: `An original ghost woman enemy character in a torn old dress, eerie face, realistic humanoid proportions, horror game enemy, ${baseStyle}`, texturePrompt: "pale translucent skin, old fabric dress, subtle dirt, ghostly blue tint, rough fabric normal detail" },
      { id: "first_person_flashlight_hands", role: "player_viewmodel", type: "character_part", priority: 1, position: {x:0,y:-0.4,z:1}, scale: {x:1,y:1,z:1}, prompt: `First-person realistic hands holding a generic black flashlight, no brand, horror game viewmodel, ${baseStyle}`, texturePrompt: "skin detail, worn sleeve fabric, black plastic flashlight, subtle scratches" },
      { id: "haunted_debris_cobweb_pack", role: "scene_dressing", type: "prop_pack", priority: 2, position: {x:0,y:0,z:-8}, scale: {x:1,y:1,z:1}, prompt: `A pack of haunted house debris props, cobwebs, broken plaster, leaves, papers, dust piles, picture frames, ${baseStyle}`, texturePrompt: "dust, paper, dry leaves, cracked plaster, cobweb translucent material" }
    ];

    const zombieAssets = [
      { id: "hero_zombie_enemy", role: "enemy_character", type: "character", priority: 1, position: {x:0,y:0,z:-12}, scale: {x:1,y:1,z:1}, prompt: `A terrifying original zombie enemy character for a first-person open-world survival shooter, torn clothing, pale decayed skin, realistic body proportions, ${baseStyle}`, texturePrompt: "decayed skin, torn dirty fabric, mud, blood stains, roughness and normal detail" },
      { id: "survivor_rifle_viewmodel", role: "player_viewmodel", type: "character_part", priority: 1, position: {x:0,y:-0.4,z:1}, scale: {x:1,y:1,z:1}, prompt: `First-person survivor arms holding a generic survival rifle, no brand, realistic sleeves and hands, ${baseStyle}`, texturePrompt: "dirty skin, worn fabric sleeves, scratched dark metal rifle" },
      { id: "abandoned_road_environment", role: "level_module", type: "environment", priority: 1, position: {x:0,y:0,z:-16}, scale: {x:1,y:1,z:1}, prompt: `A realistic abandoned rural road environment kit for zombie outbreak game, cracked asphalt, debris, grass edges, ${baseStyle}`, texturePrompt: "wet asphalt, mud, grass, road grime, concrete, cracked paint" },
      { id: "ruined_car", role: "prop", type: "vehicle", priority: 2, position: {x:5,y:0,z:-10}, scale: {x:1,y:1,z:1}, prompt: `A generic abandoned ruined car for a zombie apocalypse road scene, damaged body, broken windows, no brand, ${baseStyle}`, texturePrompt: "rust, dirt, cracked glass, faded paint, wet road grime" },
      { id: "radio_tower_landmark", role: "objective_landmark", type: "landmark", priority: 2, position: {x:0,y:0,z:-35}, scale: {x:1,y:1,z:1}, prompt: `A damaged radio tower and utility shed objective landmark for a zombie survival game, original design, ${baseStyle}`, texturePrompt: "rusty metal, concrete, warning paint without text, cables, weather damage" }
    ];

    const assets = sceneType === "open_world_zombie" ? zombieAssets : hauntedAssets;

    const blueprint = {
      mode: "Live Meshy Scene Builder",
      generatedAt: new Date().toISOString(),
      gameName,
      sceneType,
      prompt: projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptCopyrightSafe || projectState.prompt || projectState.description || "",
      renderTarget: "High-end AA / photoreal candidate",
      legalRules: [
        "Original prompts only.",
        "No copyrighted characters, brands, celebrities, logos, or trademarked weapons.",
        "Every imported asset gets provider metadata and licence review notes.",
        "Commercial release requires reviewing provider licence terms."
      ],
      lightingPlan: sceneType === "haunted_house" ? {
        flashlight: true,
        moonlightWindow: true,
        warmLamp: true,
        fog: "cold blue hallway haze",
        post: ["vignette", "film grain", "bloom", "colour grading", "ambient occlusion"]
      } : {
        flashlight: true,
        overcastSky: true,
        fog: "outbreak zone haze",
        post: ["vignette", "film grain", "bloom", "contrast", "ambient occlusion"]
      },
      scenePlacementRules: [
        "Use level_module assets as the core world layout.",
        "Place player_viewmodel in first-person camera layer.",
        "Place enemy_character assets at encounter points.",
        "Place practical_light_prop assets as light emitters.",
        "Scatter scene_dressing around walls, corners, floors and objective paths."
      ],
      assets
    };
    this.lastBlueprint = blueprint;
    return blueprint;
  },

  applySceneObjectsFromResult(result, projectState = window.projectState || {}) {
    projectState.scene = projectState.scene || {};
    projectState.scene.objects = projectState.scene.objects || [];
    const items = result?.downloads || result?.results || [];
    for (const item of items) {
      const asset = item.asset || item.request || {};
      const downloaded = item.downloaded?.downloaded || item.downloadedFiles || [];
      const modelFile = downloaded.find?.(f => f.type === "model")?.file || downloaded[0]?.file || item.file || null;
      projectState.scene.objects.push({
        name: asset.id || item.assetId || "meshy_asset",
        type: asset.role || asset.type || "meshy_generated_asset",
        source: "meshy_live_scene_builder",
        modelFile,
        position: asset.position || { x: 0, y: 0, z: -8 },
        scale: asset.scale || { x: 1, y: 1, z: 1 },
        photorealCandidate: true,
        providerMetadata: item.metaFile || item.metadata || null
      });
    }
    projectState.liveMeshySceneApplied = true;
    return projectState.scene.objects;
  },

  async run(projectState = window.projectState || {}, engine = null) {
    this.log("Creating live Meshy scene blueprint.");
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("assets", "Creating live Meshy scene blueprint");
    const blueprint = this.createBlueprint(projectState);

    if (!window.gameforgeAPI?.liveMeshyBuildScene) {
      this.log("Backend live scene builder unavailable; falling back.");
      if (window.GameForgeMeshyAutonomousAPI) return await GameForgeMeshyAutonomousAPI.run(projectState, engine);
      return { ok: false, error: "Live Meshy backend unavailable", blueprint };
    }

    const result = await window.gameforgeAPI.liveMeshyBuildScene({ blueprint, projectState });
    this.lastRun = result;

    if (result?.ok) {
      this.log(`Live Meshy Scene Builder complete: ${result.summary?.assetsProcessed || 0} assets processed.`);
      projectState.liveMeshySceneRun = result;
      this.applySceneObjectsFromResult(result, projectState);
      if (window.GameForgePhotorealQualityGate) {
        try { GameForgePhotorealQualityGate.run(projectState, engine); } catch (e) { this.log("Quality gate warning: " + e.message); }
      }
      if (window.GameForgeUnrealExportPrep) {
        try { await GameForgeUnrealExportPrep.savePackage(projectState); } catch (e) { this.log("Unreal prep warning: " + e.message); }
      }
    } else {
      this.log("Live Meshy warning: " + (result?.error || "unknown issue"));
    }

    document.dispatchEvent(new CustomEvent("gf-live-meshy-scene-builder-complete", { detail: result }));
    return result;
  },

  formatBlueprint(blueprint = this.lastBlueprint) {
    if (!blueprint) return "No Live Meshy Scene Builder blueprint yet.";
    return `# Live Meshy Scene Builder Blueprint

Game: ${blueprint.gameName}
Scene Type: ${blueprint.sceneType}
Target: ${blueprint.renderTarget}

Lighting Plan:
${Object.entries(blueprint.lightingPlan || {}).map(([k,v]) => `- ${k}: ${Array.isArray(v) ? v.join(", ") : v}`).join("\n")}

Assets:
${blueprint.assets.map(a => `- ${a.id} | ${a.role} | ${a.type} | priority ${a.priority}
  Prompt: ${a.prompt}
  Texture: ${a.texturePrompt}`).join("\n\n")}

Legal Rules:
${blueprint.legalRules.map(x => "- " + x).join("\n")}`;
  },

  contextForHybridAI() {
    return `Live Meshy Scene Builder available:
- converts a game prompt into scene-specific Meshy asset requests
- supports haunted house and open-world zombie scene blueprints
- API mode can generate/download models; free-test mode creates prompt packs
- maps generated assets back into GameForge scene roles and quality gate`;
  }
};

window.GameForgeLiveMeshySceneBuilder = GameForgeLiveMeshySceneBuilder;
