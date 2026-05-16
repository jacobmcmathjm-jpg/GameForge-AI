
const GameForgeCinematicGenreSceneComposer = {
  lastPlan: null,
  lastRun: null,

  genrePresets: {
    horror_paranormal: {
      label: "Realistic Paranormal Horror",
      heroComposition: "first-person flashlight view down a narrow damaged hallway with ghost/entity depth target",
      camera: ["human eye height", "flashlight foreground", "tight FOV", "strong depth line", "slight handheld feel"],
      lighting: ["cold moonlight back source", "warm practical lamp", "flashlight cone", "deep shadow corners", "volumetric fog"],
      materials: ["wet wood floor", "peeling plaster", "dirty cracked glass", "aged wood trim", "rusted radiator"],
      heroAssets: ["flashlight/hands", "ghost apparition", "old hallway kit", "dirty door set", "lamp", "radiator", "picture frames", "debris/cobweb decals"],
      ui: ["objective top-left", "flashlight/battery lower-left", "quality gate/report optional top-right"],
      atmosphere: ["rain", "fog pockets", "dust motes", "distant thunder", "room tone", "creaks/whispers"],
      rejectIf: ["empty hallway", "flat lighting", "cartoon ghost", "no flashlight cone", "flat walls", "low-poly building"]
    },
    zombie_shooter: {
      label: "Realistic Zombie Survival Shooter",
      heroComposition: "first-person/third-person weapon view looking down an abandoned street with zombies and damaged vehicles",
      camera: ["weapon foreground", "street vanishing point", "overcast contrast", "danger silhouettes"],
      lighting: ["cloudy daylight or night emergency lighting", "smoke/fog", "headlights/fires", "street lamps"],
      materials: ["cracked asphalt", "dirty vehicle paint", "broken glass", "brick/concrete decay", "blood-safe decals"],
      heroAssets: ["weapon set", "zombie group", "abandoned cars", "barricades", "trash/debris", "shopfronts", "road kit"],
      ui: ["ammo/health lower-right", "objective top-left", "threat indicator optional"],
      atmosphere: ["smoke", "distant sirens", "wind", "zombie groans", "dust/debris"],
      rejectIf: ["cartoon zombies", "empty street", "toy cars", "flat road", "no props", "bad weapon presentation"]
    },
    family_adventure: {
      label: "Premium Family Adventure",
      heroComposition: "bright realistic-stylised forest path or village with friendly character and clear objective",
      camera: ["slightly wider FOV", "warm readable composition", "character/friendly object foreground", "safe depth"],
      lighting: ["soft sunlight", "warm bounce", "gentle shadows", "clear sky/soft clouds"],
      materials: ["grass", "tree bark", "painted wood", "stone path", "flowers", "soft fabric"],
      heroAssets: ["friendly character/animal", "treehouse/cabin", "path kit", "flowers", "signposts", "collectibles"],
      ui: ["clear objective marker", "friendly inventory", "soft readable prompts"],
      atmosphere: ["birds", "soft wind", "sun shafts", "floating pollen"],
      rejectIf: ["cheap mobile look", "flat cartoon", "empty forest", "low-poly toy look", "harsh horror lighting"]
    },
    racing_driving: {
      label: "Realistic Racing / Driving",
      heroComposition: "low cinematic car/road angle with reflective vehicle, wet asphalt and track/road depth",
      camera: ["low car angle", "motion framing", "road vanishing point", "vehicle hero reflection"],
      lighting: ["golden hour or night track lights", "wet reflections", "headlights", "cinematic contrast"],
      materials: ["car paint", "rubber tyres", "wet asphalt", "road markings", "guardrails", "glass"],
      heroAssets: ["vehicle body", "road/track kit", "barriers", "lights", "environment backdrop", "tire smoke/water spray"],
      ui: ["speedometer", "lap/checkpoint", "mini route optional"],
      atmosphere: ["motion blur plan", "engine audio", "tire noise", "rain or heat haze"],
      rejectIf: ["toy car", "flat road", "no reflections", "bad scale", "no driving UI"]
    },
    survival_crafting: {
      label: "Realistic Survival / Crafting",
      heroComposition: "grounded camp/shelter scene with tools, resources, weather and terrain detail",
      camera: ["player/tool foreground", "shelter midground", "terrain depth", "resource readability"],
      lighting: ["campfire practical", "moonlight/sunlight", "weather haze", "soft bounce"],
      materials: ["bark", "mud", "stone", "canvas", "metal tools", "wood logs"],
      heroAssets: ["campfire", "shelter", "tools", "backpack", "resource piles", "terrain kit", "weather FX"],
      ui: ["health/stamina", "resource/objective prompt", "crafting prompt"],
      atmosphere: ["wind", "fire crackle", "rain/snow optional", "insects/birds"],
      rejectIf: ["empty terrain", "flat campfire", "no resource props", "toy shelter", "bad material scale"]
    },
    sci_fi: {
      label: "Realistic Sci-Fi Exploration",
      heroComposition: "metallic corridor or control room with volumetric light, panel detail and reflective floor",
      camera: ["corridor depth", "foreground device/weapon optional", "symmetry or strong leading lines"],
      lighting: ["blue/cyan panels", "red alert optional", "volumetric shafts", "reflections"],
      materials: ["brushed metal", "glass", "rubber floor", "panel plastics", "emissive displays"],
      heroAssets: ["corridor kit", "control panels", "doors", "cables", "lights", "screens", "device/robot optional"],
      ui: ["clean futuristic HUD", "objective/scan prompt"],
      atmosphere: ["low hum", "steam/fog", "sparks optional", "computer beeps"],
      rejectIf: ["flat grey corridor", "no panel detail", "cheap neon", "empty room", "no reflections"]
    },
    fantasy_medieval: {
      label: "Realistic Fantasy / Medieval",
      heroComposition: "stone courtyard/interior with torchlight, banners, props and realistic materials",
      camera: ["stone arch depth", "character/weapon foreground optional", "torch practical lighting"],
      lighting: ["warm torches", "cool moon/daylight", "fog/smoke", "soft shadows"],
      materials: ["stone", "wood beams", "metal armour", "cloth banners", "mud", "leather"],
      heroAssets: ["castle/courtyard kit", "torches", "banners", "props", "armour/weapon", "foliage/moss"],
      ui: ["quest objective", "inventory/health", "interaction prompts"],
      atmosphere: ["torch crackle", "wind", "distant ambience", "fog/mist"],
      rejectIf: ["flat stone", "empty castle", "toy armour", "cartoon style", "bad torch lighting"]
    },
    generic_realistic: {
      label: "Generic High-End Realistic Scene",
      heroComposition: "genre-appropriate hero view with strong depth, realistic materials and clear gameplay objective",
      camera: ["eye-level or genre-appropriate", "clear foreground/midground/background", "gameplay readable"],
      lighting: ["cinematic key/fill/rim", "manual exposure", "realistic shadows", "post-process"],
      materials: ["PBR surfaces", "real-world scale", "genre materials"],
      heroAssets: ["main character/tool/vehicle", "environment kit", "props", "objective actor"],
      ui: ["objective", "interaction prompt", "genre HUD"],
      atmosphere: ["weather/fog/ambient audio where useful"],
      rejectIf: ["indie prototype look", "empty space", "flat lighting", "placeholder assets"]
    }
  },

  inferGenre(projectState = window.projectState || {}) {
    const p = String(projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.prompt || projectState.description || "").toLowerCase();
    if (/ghost|haunt|paranormal|horror|spirit|jump scare|jumpscare|entity/.test(p)) return "horror_paranormal";
    if (/zombie|undead|infected|outbreak/.test(p)) return "zombie_shooter";
    if (/kid|kids|family|animal|forest|friendly|cute/.test(p)) return "family_adventure";
    if (/race|racing|car|drive|vehicle/.test(p)) return "racing_driving";
    if (/survival|craft|camp|shelter|resource/.test(p)) return "survival_crafting";
    if (/sci.?fi|space|station|alien|robot|futuristic/.test(p)) return "sci_fi";
    if (/fantasy|medieval|castle|dragon|wizard|knight/.test(p)) return "fantasy_medieval";
    return "generic_realistic";
  },

  createPlan(projectState = window.projectState || {}) {
    const genreKey = this.inferGenre(projectState);
    const preset = this.genrePresets[genreKey] || this.genrePresets.generic_realistic;
    const prompt = projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.prompt || projectState.description || "";

    const plan = {
      mode: "Cinematic Genre Scene Composer",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      prompt,
      genreKey,
      preset,
      goal: "Make the first generated scene feel intentional, cinematic, high-end and genre-appropriate before packaging.",
      compositionLock: {
        enabled: true,
        rule: "Build one hero benchmark scene first, score it, repair it, then expand the game around it.",
        requiredLayers: [
          "foreground gameplay object/presentation",
          "midground playable path/objective",
          "background depth target",
          "hero light source",
          "secondary contrast light",
          "atmosphere/weather layer",
          "PBR material detail layer",
          "prop dressing layer",
          "UI/HUD readability layer"
        ]
      },
      cameraAndFraming: preset.camera,
      lightingRecipe: preset.lighting,
      materialTargets: preset.materials,
      heroAssetChecklist: preset.heroAssets,
      uiHudPresentation: preset.ui,
      atmosphereAudioWeather: preset.atmosphere,
      rejectIf: preset.rejectIf,
      firstGoQualityGate: {
        minimumCompositionScore: 88,
        minimumLightingScore: 88,
        minimumMaterialScore: 86,
        minimumHeroAssetScore: 84,
        minimumGameplayReadabilityScore: 82,
        failIf: [
          "no strong composition",
          "flat lighting",
          "empty environment",
          "missing hero asset",
          "weak PBR materials",
          "no atmosphere",
          "unclear gameplay objective",
          "looks like an indie prototype"
        ]
      },
      prePackageRepairLoop: {
        enabled: true,
        maxCycles: 3,
        repairActions: [
          "recompose hero scene using preset layout",
          "reposition camera/player start for stronger framing",
          "apply cinematic lighting recipe",
          "replace weak hero assets",
          "swap flat materials for PBR materials",
          "add weather/fog/atmosphere",
          "increase prop dressing density",
          "add UI/HUD presentation layer",
          "rerun screenshot scoring before packaging"
        ]
      },
      integrationTargets: [
        "Global High-End Realism Lock",
        "High-End Asset Library",
        "Licensed Visual Reference + PBR Material Builder",
        "Realistic Structure Generator",
        "Phasmophobia-Quality Haunted Game Core",
        "Screenshot Visual Scoring",
        "Playable EXE Validator",
        "Autonomous Full Game Builder"
      ]
    };

    this.lastPlan = plan;
    projectState.cinematicGenreSceneComposerPlan = plan;
    projectState.genreScenePreset = preset;
    projectState.genreSceneKey = genreKey;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    const plan = this.createPlan(projectState);

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("composition", "Composing cinematic genre scene");
    }

    if (!window.gameforgeAPI?.cinematicGenreSceneCompose) {
      const result = { ok: true, mode: "frontend_plan_only", plan };
      this.lastRun = result;
      return result;
    }

    const result = await window.gameforgeAPI.cinematicGenreSceneCompose({ plan, projectState });
    this.lastRun = result;
    projectState.cinematicGenreSceneComposerRun = result;
    return result;
  },

  formatReport(report = this.lastRun || { plan: this.lastPlan }) {
    const plan = report.plan || this.lastPlan;
    if (!plan) return "No Cinematic Genre Scene Composer report yet.";

    return `# Cinematic Genre Scene Composer

Genre:
${plan.preset.label}

Hero Composition:
${plan.preset.heroComposition}

Camera / Framing:
${plan.cameraAndFraming.map(x => "- " + x).join("\n")}

Lighting Recipe:
${plan.lightingRecipe.map(x => "- " + x).join("\n")}

Hero Asset Checklist:
${plan.heroAssetChecklist.map(x => "- " + x).join("\n")}

Material Targets:
${plan.materialTargets.map(x => "- " + x).join("\n")}

Reject If:
${plan.rejectIf.map(x => "- " + x).join("\n")}

First-Go Quality Gate:
- Composition: ${plan.firstGoQualityGate.minimumCompositionScore}
- Lighting: ${plan.firstGoQualityGate.minimumLightingScore}
- Material: ${plan.firstGoQualityGate.minimumMaterialScore}
- Hero Assets: ${plan.firstGoQualityGate.minimumHeroAssetScore}
- Gameplay Readability: ${plan.firstGoQualityGate.minimumGameplayReadabilityScore}

Latest Status:
${report.status || "PLAN_READY"}`;
  },

  contextForHybridAI() {
    return `Cinematic Genre Scene Composer active:
- acts as art director across horror, zombie, family, racing, survival, sci-fi, fantasy and generic realistic games
- locks a hero benchmark composition before packaging
- applies genre-specific camera, lighting, material, hero asset, UI and atmosphere recipes
- runs first-go quality gate and pre-package visual repair loop`;
  }
};

window.GameForgeCinematicGenreSceneComposer = GameForgeCinematicGenreSceneComposer;
