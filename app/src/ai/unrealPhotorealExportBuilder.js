
const GameForgeUnrealPhotorealExportBuilder = {
  lastPackage: null,
  lastRun: null,

  log(message) {
    console.log("[Unreal Photoreal Export Builder]", message);
    for (const id of ["oneClickForgeLog", "gameIntelLog", "assetDownloaderLog", "photorealModeLog"]) {
      const el = document.getElementById(id);
      if (el) {
        el.value += `${new Date().toLocaleTimeString()} — ${message}\n`;
        el.scrollTop = el.scrollHeight;
      }
    }
  },

  createPackage(projectState = window.projectState || {}) {
    const prompt = projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptCopyrightSafe || projectState.prompt || projectState.description || "";
    const sceneType = window.GameForgeLiveMeshySceneBuilder?.inferSceneType
      ? GameForgeLiveMeshySceneBuilder.inferSceneType(projectState)
      : (/zombie|outbreak|open world/i.test(prompt) ? "open_world_zombie" : "haunted_house");

    const sceneObjects = projectState.scene?.objects || [];
    const liveRun = projectState.liveMeshySceneRun || projectState.liveMeshySceneBuilderRun || null;
    const quality = projectState.photorealQualityReport || window.GameForgePhotorealQualityGate?.lastReport || null;

    const pkg = {
      mode: "Unreal Photoreal Export Builder",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      prompt,
      sceneType,
      target: "High-end AA / near-AAA photoreal Unreal project handoff",
      importantLimit: "This prepares a high-end Unreal project package/handoff. Final packaging requires Unreal Engine installed and the generated project opened/built on the user's PC.",
      unrealProject: {
        projectName: (projectState.name || "GameForgeGame").replace(/[^a-z0-9_]+/gi, "_"),
        folders: [
          "Content/GameForge/Models",
          "Content/GameForge/Materials",
          "Content/GameForge/Textures",
          "Content/GameForge/Audio",
          "Content/GameForge/Maps",
          "Content/GameForge/Blueprints",
          "Content/GameForge/FX",
          "Content/GameForge/Characters"
        ],
        pluginsSuggested: [
          "Lumen enabled project settings",
          "Nanite for high-detail static meshes where appropriate",
          "Virtual Shadow Maps where available",
          "Enhanced Input for player controls"
        ]
      },
      renderingPreset: {
        lighting: sceneType === "haunted_house"
          ? ["player flashlight", "cold moonlight through hallway window", "warm practical lamp", "low bounce/fill", "volumetric-style fog"]
          : ["overcast sky", "flashlight/weapon light", "distant practical lights", "environment haze", "low sun/moon contrast"],
        postProcess: [
          "manual exposure",
          "cinematic contrast",
          "subtle bloom",
          "ambient occlusion",
          "vignette",
          "film grain",
          "cool shadows / warm highlights",
          "depth of field used lightly"
        ],
        materials: [
          "PBR base color/albedo",
          "normal maps",
          "roughness maps",
          "metallic maps where needed",
          "ambient occlusion",
          "height/displacement where appropriate",
          "emissive for lamps/screens"
        ],
        qualityTargets: [
          "reject untextured placeholder geometry",
          "flag low-resolution textures",
          "require scale/origin cleanup",
          "require collision proxies",
          "require scene dressing density"
        ]
      },
      assetHandoff: {
        source: "GameForge + Meshy + Blender",
        liveMeshyRun: liveRun,
        sceneObjects,
        meshRules: [
          "Use Nanite candidate flag for detailed static props/buildings.",
          "Do not use Nanite for skeletal characters unless validated.",
          "Generate collision proxies for interactive/static gameplay objects.",
          "Create LODs for non-Nanite fallback assets.",
          "Pack textures and store provider/licence metadata."
        ],
        characterRules: [
          "Use Meshy-generated characters as prototypes unless rigging/animation validates.",
          "For lifelike humans later, add MetaHuman/realistic character path.",
          "Require idle/walk/attack/hit/death animation states for enemies."
        ]
      },
      sceneDressing: sceneType === "haunted_house" ? [
        "dust piles", "cobwebs", "broken plaster", "loose papers", "fallen leaves", "picture frames",
        "scratched doors", "dirty glass", "wall stains", "floorboard damage", "small debris clusters"
      ] : [
        "road debris", "mud patches", "puddles", "grass clumps", "broken fences", "abandoned vehicles",
        "trash piles", "blood trails", "road signs", "crates", "rocks", "branches"
      ],
      buildReadinessChecklist: [
        "Unreal installed/detected",
        "Generated/imported models available",
        "Blender cleanup complete",
        "PBR material plan complete",
        "Lighting preset applied",
        "Post-process volume configured",
        "Scene dressing pass complete",
        "Collision pass complete",
        "Navigation/enemy paths validated",
        "Packaging/export settings prepared"
      ],
      qualityReport: quality,
      legalWarning: "Review all Meshy, asset-source and Unreal/Epic licensing terms before public or commercial release."
    };

    this.lastPackage = pkg;
    projectState.unrealPhotorealPackage = pkg;
    return pkg;
  },

  async run(projectState = window.projectState || {}) {
    this.log("Preparing Unreal photoreal export package.");
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("export", "Preparing Unreal photoreal export package");

    const pkg = this.createPackage(projectState);

    if (!window.gameforgeAPI?.unrealPhotorealBuildPackage) {
      this.log("Backend Unreal photoreal package builder unavailable.");
      return { ok: false, error: "Unreal photoreal backend unavailable", package: pkg };
    }

    const result = await window.gameforgeAPI.unrealPhotorealBuildPackage({ package: pkg, projectState });
    this.lastRun = result;
    if (result?.ok) {
      this.log("Unreal photoreal export package prepared.");
      projectState.unrealPhotorealBuildResult = result;
    } else {
      this.log("Unreal photoreal package warning: " + (result?.error || "unknown issue"));
    }
    document.dispatchEvent(new CustomEvent("gf-unreal-photoreal-package-ready", { detail: result }));
    return result;
  },

  formatPackage(pkg = this.lastPackage) {
    if (!pkg) return "No Unreal photoreal export package yet.";
    return `# Unreal Photoreal Export Builder

Game: ${pkg.gameName}
Scene Type: ${pkg.sceneType}
Target: ${pkg.target}

Important:
${pkg.importantLimit}

Rendering Preset:
- Lighting: ${pkg.renderingPreset.lighting.join(", ")}
- Post Process: ${pkg.renderingPreset.postProcess.join(", ")}
- Materials: ${pkg.renderingPreset.materials.join(", ")}

Scene Dressing:
${pkg.sceneDressing.map(x => "- " + x).join("\n")}

Build Readiness:
${pkg.buildReadinessChecklist.map(x => "- " + x).join("\n")}

Legal:
${pkg.legalWarning}`;
  },

  contextForHybridAI() {
    return `Unreal Photoreal Export Builder available:
- creates Unreal project/handoff package for high-end AA / near-AAA visuals
- includes Nanite/Lumen notes, PBR material plan, cinematic post-process, scene dressing and build readiness report
- uses Meshy/Blender/GameForge scene objects as inputs`;
  }
};

window.GameForgeUnrealPhotorealExportBuilder = GameForgeUnrealPhotorealExportBuilder;
