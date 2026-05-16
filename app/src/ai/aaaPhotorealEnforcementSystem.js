
const GameForgeAAAPhotorealEnforcementSystem = {
  lastPlan: null,
  lastRun: null,

  hardRejectStyles: [
    "pixel art",
    "low poly",
    "cartoon",
    "toy-like",
    "stylized indie",
    "flat lighting",
    "untextured",
    "placeholder",
    "mobile-looking",
    "plastic-looking",
    "mannequin-looking",
    "asset-store generic"
  ],

  requiredVisualStandards: [
    "UE5 Lumen lighting target",
    "Nanite-ready static mesh path",
    "PBR materials with albedo/normal/roughness/AO",
    "real-world scale and proportions",
    "manual cinematic exposure",
    "Virtual Shadow Maps where available",
    "volumetric fog/haze plan",
    "wetness/reflection cues where suitable",
    "realistic flashlight falloff",
    "high-density scene dressing",
    "realistic human/ghost proportions",
    "screenshot quality review loop"
  ],

  createPlan(projectState = window.projectState || {}) {
    const prompt = projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptCopyrightSafe || projectState.prompt || projectState.description || "";
    const polish = projectState.photorealScenePolishPlan || {};
    const style = projectState.gameStyleRating?.key || "mature_horror";

    const plan = {
      mode: "AAA Photoreal Enforcement System",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      prompt,
      style,
      target: "High-end realistic ghost-investigation horror visuals; no indie/pixel/cartoon/low-poly output accepted.",
      hardRejectStyles: this.hardRejectStyles,
      requiredVisualStandards: this.requiredVisualStandards,
      enforcePromptRewrite: true,
      promptPrefix: [
        "Generate as a high-end UE5 photoreal horror game.",
        "Reject indie, pixel, cartoon, low-poly, toy-like or mobile-game aesthetics.",
        "Use realistic buildings, human proportions, cinematic horror lighting and PBR materials.",
        "Use original copyright-safe game identity; do not copy existing games."
      ],
      ue5RendererPreset: {
        lumen: "required where available",
        nanite: "required for static high-detail geometry where suitable",
        virtualShadowMaps: "required where available",
        exposure: "manual cinematic exposure",
        colorGrade: "cool shadows, warm practical highlights, realistic contrast",
        fog: "volumetric-style fog and haze pockets",
        reflections: "wet floor and dirty glass reflection cues",
        flashlight: "physically plausible cone, falloff, shadows and volumetric haze",
        postProcess: [
          "ambient occlusion",
          "subtle bloom",
          "film grain",
          "vignette",
          "depth of field used lightly",
          "chromatic edge used lightly"
        ]
      },
      assetValidation: {
        minTextureResolution: "2K preferred, 4K for hero surfaces",
        requirePBRMaps: ["albedo/baseColor", "normal", "roughness", "ambientOcclusion"],
        rejectMissingMaterials: true,
        rejectBadScale: true,
        rejectBadNormals: true,
        rejectPlaceholderMeshes: true,
        rejectCartoonProportions: true,
        preferScannedAssetsFor: [
          "walls",
          "floors",
          "doors",
          "windows",
          "mud",
          "wood",
          "plaster",
          "rusted metal",
          "furniture",
          "debris",
          "exterior environment"
        ]
      },
      screenshotReviewLoop: {
        enabled: true,
        minimumVisualScore: 86,
        reviewShots: [
          "exterior approach",
          "main hallway",
          "device close-up",
          "door jump scare",
          "basement objective",
          "ghost apparition"
        ],
        failIf: [
          "looks indie",
          "flat lighting",
          "low-poly silhouettes",
          "bad human/ghost anatomy",
          "generic placeholder props",
          "low-res or stretched textures",
          "weak atmosphere",
          "missing scare audio/lighting response"
        ],
        repairActions: [
          "replace weak assets with scanned/production-quality assets",
          "regenerate Meshy assets with stricter photoreal prompt",
          "re-run Blender cleanup",
          "upgrade PBR material assignment",
          "increase scene dressing density",
          "adjust lighting/post-process/fog",
          "rerun Unreal render/build check"
        ]
      },
      mergedPhotorealPolish: polish,
      outputStatusRules: {
        allowPlayablePrototype: true,
        blockCommercialReadyClaimUnlessPassed: true,
        labelOutputAs: "AAA Candidate only after visual gate passes"
      }
    };

    this.lastPlan = plan;
    projectState.aaaPhotorealEnforcementPlan = plan;
    projectState.promptAAAPhotoreal = `${plan.promptPrefix.join("\n")}\n\nOriginal user prompt:\n${prompt}`;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    const plan = this.createPlan(projectState);

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("lighting", "Enforcing AAA photoreal visual gate");
    }

    if (!window.gameforgeAPI?.aaaPhotorealEnforce) {
      const result = { ok: true, mode: "frontend_plan_only", plan };
      this.lastRun = result;
      return result;
    }

    const result = await window.gameforgeAPI.aaaPhotorealEnforce({ plan, projectState });
    this.lastRun = result;
    projectState.aaaPhotorealEnforcementRun = result;
    return result;
  },

  formatPlan(plan = this.lastPlan) {
    if (!plan) return "No AAA photoreal enforcement plan yet.";
    return `# AAA Photoreal Enforcement System

Target:
${plan.target}

Hard Reject Styles:
${plan.hardRejectStyles.map(x => "- " + x).join("\n")}

Required Visual Standards:
${plan.requiredVisualStandards.map(x => "- " + x).join("\n")}

UE5 Renderer Preset:
${Object.entries(plan.ue5RendererPreset).map(([k,v]) => `- ${k}: ${Array.isArray(v) ? v.join(", ") : v}`).join("\n")}

Asset Validation:
- Minimum Texture Resolution: ${plan.assetValidation.minTextureResolution}
- Required PBR Maps: ${plan.assetValidation.requirePBRMaps.join(", ")}
- Prefer Scanned Assets For: ${plan.assetValidation.preferScannedAssetsFor.join(", ")}

Screenshot Review Loop:
- Enabled: ${plan.screenshotReviewLoop.enabled}
- Minimum Visual Score: ${plan.screenshotReviewLoop.minimumVisualScore}
- Fail If: ${plan.screenshotReviewLoop.failIf.join(", ")}
- Repair Actions: ${plan.screenshotReviewLoop.repairActions.join(", ")}`;
  },

  contextForHybridAI() {
    return `AAA Photoreal Enforcement System active:
- rejects indie, pixel, low-poly, cartoon, mobile-looking and placeholder output
- requires UE5-style Lumen/Nanite/PBR/cinematic horror lighting standards
- adds screenshot-based visual review and repair loop
- outputs should be labelled AAA Candidate only when the visual gate passes`;
  }
};

window.GameForgeAAAPhotorealEnforcementSystem = GameForgeAAAPhotorealEnforcementSystem;
