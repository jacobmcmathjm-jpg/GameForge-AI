
const GameForgeGlobalHighEndRealismLock = {
  enabled: true,
  lastPlan: null,
  lastRun: null,

  bannedOutputStyles: [
    "pixel art",
    "retro pixel",
    "low-poly",
    "flat-shaded",
    "cartoon-only",
    "toy-like",
    "cheap mobile",
    "asset-store generic",
    "placeholder",
    "mannequin",
    "untextured",
    "flat lighting",
    "plastic-looking",
    "indie prototype visual style"
  ],

  highEndBaseline: [
    "high-end realistic 3D",
    "Unreal Engine 5 visual target",
    "PBR materials",
    "cinematic lighting",
    "real-world scale",
    "premium character and environment fidelity",
    "high-quality shadows and reflections",
    "detailed scene dressing",
    "no low-poly/pixel/cartoon-only/placeholder output"
  ],

  genreTranslations: {
    horror: "high-end realistic cinematic horror with believable buildings, PBR decay, realistic shadows, fog/haze and premium sound/lighting",
    zombie: "high-end realistic zombie survival/action with believable humans/zombies, decayed environments, cinematic lighting and detailed props",
    kids: "premium family-friendly realistic 3D adventure with soft cinematic lighting, high-quality natural environments and believable expressive characters",
    cartoon: "high-end realistic stylised 3D, animated-film quality, detailed materials and premium lighting, not flat indie cartoon graphics",
    fantasy: "high-end realistic fantasy with grounded materials, detailed creatures, cinematic lighting, realistic terrain and premium VFX",
    racing: "high-end realistic driving game with real-world car materials, reflections, road detail, weather, lighting and cinematic camera polish",
    farming: "high-end realistic farming/life sim with believable terrain, crops, animals, buildings, tools, weather and soft cinematic lighting",
    shooter: "high-end realistic action shooter with believable weapons, characters, environments, animation, lighting and VFX",
    survival: "high-end realistic survival game with detailed shelter, terrain, props, weather, materials, lighting and animation",
    default: "high-end realistic 3D game with Unreal-style cinematic presentation, PBR materials, realistic characters/environments and no indie/low-poly/pixel output"
  },

  inferGenre(prompt = "") {
    const p = String(prompt).toLowerCase();
    if (/ghost|haunt|paranormal|horror|scare|jump scare|jumpscare|spirit|entity/.test(p)) return "horror";
    if (/zombie|undead|outbreak|infected/.test(p)) return "zombie";
    if (/kid|kids|child|children|family|cute|animal/.test(p)) return "kids";
    if (/cartoon|toon|animated/.test(p)) return "cartoon";
    if (/fantasy|dragon|magic|wizard|medieval/.test(p)) return "fantasy";
    if (/race|racing|car|driving|vehicle/.test(p)) return "racing";
    if (/farm|farming|crop|harvest|life sim/.test(p)) return "farming";
    if (/shooter|gun|fps|weapon|combat/.test(p)) return "shooter";
    if (/survival|craft|build|base/.test(p)) return "survival";
    return "default";
  },

  translatePrompt(prompt = "", projectState = window.projectState || {}) {
    const genre = this.inferGenre(prompt);
    const translation = this.genreTranslations[genre] || this.genreTranslations.default;

    return {
      genre,
      translatedPrompt: `GLOBAL HIGH-END REALISM LOCK ENABLED.

Every generated game must be interpreted as a high-end realistic 3D experience.
Do not generate pixel art, low-poly, cartoon-only, flat-lit, indie prototype, mobile-looking, placeholder, mannequin or toy-like graphics.
If the user asks for cartoon/kids/fantasy/etc, translate it into premium high-end realistic stylised 3D rather than cheap/indie styling.

Genre realism translation:
${translation}

Global visual baseline:
${this.highEndBaseline.map(x => "- " + x).join("\n")}

Original user prompt:
${prompt}

Output rule:
If the visual result fails the high-end realism gate, do not label it finished. Repair, replace, retry, or mark it as failed visual quality.`
    };
  },

  createPlan(projectState = window.projectState || {}) {
    const originalPrompt = projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptCopyrightSafe || projectState.prompt || projectState.description || "";
    const translated = this.translatePrompt(originalPrompt, projectState);

    const plan = {
      mode: "Global High-End Realism Lock",
      generatedAt: new Date().toISOString(),
      enabled: this.enabled,
      gameName: projectState.name || "GameForge Game",
      originalPrompt,
      genre: translated.genre,
      lockedPrompt: translated.translatedPrompt,
      goal: "Force every generated game into a high-end realistic 3D experience and stop indie-looking output being accepted as finished.",
      globalRules: [
        "Realism mode is always on unless manually disabled by the developer.",
        "Every game genre must be translated into high-end realistic or premium realistic-stylised 3D.",
        "Pixel, low-poly, flat-lit, placeholder, mannequin and indie prototype visuals are hard-blocked.",
        "Assets must meet high-end mesh/material/scale requirements or be regenerated/replaced.",
        "Characters must meet realism/proportion/animation quality requirements.",
        "Environments must use realistic structure, PBR materials, lighting, dressing and scale.",
        "Unreal renderer settings must target cinematic high-end presentation.",
        "Output cannot be labelled finished unless the final realism gate passes."
      ],
      bannedOutputStyles: this.bannedOutputStyles,
      highEndBaseline: this.highEndBaseline,
      styleTranslator: {
        inferredGenre: translated.genre,
        translation: this.genreTranslations[translated.genre] || this.genreTranslations.default,
        examples: {
          kidsGame: "premium family-friendly realistic 3D adventure, not low-poly/cartoon mobile game",
          zombieShooter: "realistic cinematic survival shooter with believable zombies, materials and environments",
          cartoonIdea: "high-end realistic stylised 3D, animated-film quality with premium lighting",
          horrorGame: "realistic cinematic paranormal investigation with believable houses, props, lighting and sound"
        }
      },
      gates: {
        highEndAssetRequirementGate: {
          rejectIf: ["low-poly silhouette", "flat material", "missing PBR maps", "bad scale", "placeholder mesh", "generic blockout"],
          require: ["mesh detail", "PBR maps", "normal/roughness/AO", "real-world scale", "collision", "Nanite suitability for static meshes"]
        },
        realisticCharacterGate: {
          rejectIf: ["mannequin look", "bad anatomy", "plastic skin", "cartoon-only proportions", "T-pose", "missing rig", "dead eyes"],
          require: ["realistic proportions", "proper rig", "animation set", "skin/fabric/fur/cloth material quality", "first-person hand quality"]
        },
        realisticEnvironmentGate: {
          rejectIf: ["empty environment", "flat lighting", "single-colour surfaces", "blockout buildings", "toy-like props"],
          require: ["realistic buildings/terrain", "detailed props", "scene dressing", "weather/atmosphere where useful", "proper shadows/reflections"]
        },
        unrealRendererEnforcement: {
          require: ["Lumen where available", "Nanite where suitable", "Virtual Shadow Maps", "manual exposure", "post-process volume", "ambient occlusion", "subtle bloom", "fog/haze where useful", "PBR assignment"]
        },
        visualFailRetrySystem: {
          enabled: true,
          maxRepairCycles: 3,
          screenshotScoreMinimum: 86,
          requiredScreenshots: ["hero view", "character close-up", "environment wide", "gameplay/action", "interior or objective area"],
          repairActions: ["regenerate bad asset", "replace with scanned/approved asset", "rerun Blender cleanup", "upgrade material assignment", "adjust lighting", "add scene dressing", "rerun screenshot score"]
        },
        outputBlockingSystem: {
          blockFinishedLabelIfFailed: true,
          allowedFailedLabel: "FAILED VISUAL QUALITY — REPAIR REQUIRED",
          allowedPassedLabel: "HIGH-END REALISM CANDIDATE"
        }
      },
      integrationTargets: [
        "Meshy prompts",
        "self asset generator",
        "approved asset downloader",
        "Blender cleanup",
        "Unreal horror assembly",
        "photoreal scene polish",
        "AAA photoreal enforcement",
        "scanned asset connector",
        "controlled full automation runner",
        "Unreal one-click build runner"
      ]
    };

    this.lastPlan = plan;
    projectState.globalHighEndRealismLock = plan;
    projectState.promptGlobalRealismLocked = translated.translatedPrompt;
    projectState.promptAAAPhotoreal = translated.translatedPrompt;
    projectState.graphics = "Global High-End Realism Lock";
    projectState.photorealAlwaysOn = true;
    projectState.highEndRealismRequired = true;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    const plan = this.createPlan(projectState);

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("prompt", "Applying global high-end realism lock");
    }

    if (!window.gameforgeAPI?.globalHighEndRealismLock) {
      const result = { ok: true, mode: "frontend_plan_only", plan };
      this.lastRun = result;
      return result;
    }

    const result = await window.gameforgeAPI.globalHighEndRealismLock({ plan, projectState });
    this.lastRun = result;
    projectState.globalHighEndRealismLockRun = result;
    return result;
  },

  finalOutputStatus(projectState = window.projectState || {}) {
    const score = projectState.finalRealismScore ?? projectState.aaaPhotorealScore ?? null;
    if (score !== null && score >= 86) return "HIGH-END REALISM CANDIDATE";
    return "FAILED VISUAL QUALITY — REPAIR REQUIRED";
  },

  formatPlan(plan = this.lastPlan) {
    if (!plan) return "No global high-end realism lock plan yet.";
    return `# Global High-End Realism Lock

Enabled: ${plan.enabled}
Game: ${plan.gameName}
Inferred Genre: ${plan.genre}

Goal:
${plan.goal}

Global Rules:
${plan.globalRules.map(x => "- " + x).join("\n")}

Banned Output Styles:
${plan.bannedOutputStyles.map(x => "- " + x).join("\n")}

High-End Baseline:
${plan.highEndBaseline.map(x => "- " + x).join("\n")}

Style Translation:
${plan.styleTranslator.translation}

Visual Fail/Retry:
- Enabled: ${plan.gates.visualFailRetrySystem.enabled}
- Max Repair Cycles: ${plan.gates.visualFailRetrySystem.maxRepairCycles}
- Screenshot Score Minimum: ${plan.gates.visualFailRetrySystem.screenshotScoreMinimum}

Output Blocking:
- Failed Label: ${plan.gates.outputBlockingSystem.allowedFailedLabel}
- Passed Label: ${plan.gates.outputBlockingSystem.allowedPassedLabel}`;
  },

  injectUI() {
    if (document.getElementById("globalRealismLockPanel")) return;
    const panel = document.createElement("div");
    panel.id = "globalRealismLockPanel";
    panel.className = "global-realism-lock-panel advanced-panel simple-keep";
    panel.innerHTML = `
      <label class="gf-label">Global High-End Realism Lock</label>
      <div class="realism-lock-status">LOCKED ON</div>
      <p class="gf-help">Every game is forced toward premium realistic 3D. Indie, pixel, low-poly, flat-lit and placeholder output is blocked from being labelled finished.</p>
    `;
    const target = document.querySelector(".sidebar") || document.querySelector("main") || document.body;
    target.prepend(panel);
  },

  init() {
    this.enabled = true;
    this.injectUI();
    try {
      if (window.projectState) this.createPlan(window.projectState);
    } catch (e) {}
  },

  contextForHybridAI() {
    return `Global High-End Realism Lock active:
- all genres are translated into high-end realistic / premium realistic-stylised 3D
- blocks indie, pixel, low-poly, flat-lit, placeholder and mannequin-looking output
- applies asset, character, environment, Unreal renderer and screenshot fail/retry gates
- final output cannot be labelled finished unless realism gate passes`;
  }
};

window.GameForgeGlobalHighEndRealismLock = GameForgeGlobalHighEndRealismLock;
window.addEventListener("DOMContentLoaded", () => {
  try { GameForgeGlobalHighEndRealismLock.init(); }
  catch (error) { console.warn("[GlobalHighEndRealismLock] init warning:", error.message); }
});
