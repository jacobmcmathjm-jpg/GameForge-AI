
const GameForgeUnrealHorrorGameAssemblyBuilder = {
  lastPlan: null,
  lastRun: null,

  log(message) {
    console.log("[Unreal Horror Assembly]", message);
    for (const id of ["oneClickForgeLog", "gameIntelLog", "assetDownloaderLog", "photorealModeLog", "forgeReport"]) {
      const el = document.getElementById(id);
      if (!el) continue;
      const line = `${new Date().toLocaleTimeString()} — ${message}`;
      el.value = id === "forgeReport" && el.value ? `${el.value}\n${line}` : el.value + `${line}\n`;
      el.scrollTop = el.scrollHeight;
    }
  },

  createPlan(projectState = window.projectState || {}) {
    const prompt = projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptCopyrightSafe || projectState.prompt || projectState.description || "";
    const style = projectState.gameStyleRating?.key || "mature_horror";
    const paranormal = projectState.paranormalJumpscarePlan || null;

    const plan = {
      mode: "Unreal Horror Game Assembly Builder",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Horror Game",
      prompt,
      style,
      target: "Playable first-person paranormal horror prototype assembled in Unreal",
      requiredSystems: [
        "BP_GF_FirstPersonHorrorCharacter",
        "BP_GF_FlashlightController",
        "BP_GF_DoorInteractable",
        "BP_GF_ObjectiveManager",
        "BP_GF_ParanormalDeviceBase",
        "BP_GF_DeviceInventory",
        "BP_GF_JumpScareTrigger",
        "BP_GF_GhostApparition",
        "BP_GF_AudioScareManager",
        "BP_GF_EscapeObjective"
      ],
      gameplayLoop: [
        "Arrive at haunted property",
        "Enter the building",
        "Use original paranormal devices to locate anomalies",
        "Open doors and investigate rooms",
        "Collect clues/key items",
        "Restore power or complete objective",
        "Survive triggered jump scares and ghost events",
        "Escape the location"
      ],
      levelBlueprint: {
        mapName: "GF_OneClick_Horror_Map",
        playerStart: "front porch / property entrance",
        zones: [
          { id: "exterior", purpose: "intro approach", scareDensity: "low" },
          { id: "main_hallway", purpose: "navigation hub", scareDensity: "medium" },
          { id: "bedroom", purpose: "device investigation", scareDensity: "medium-high" },
          { id: "basement", purpose: "restore power objective", scareDensity: "high" },
          { id: "escape_route", purpose: "final chase / exit", scareDensity: "high" }
        ],
        interactables: [
          "front door",
          "bedroom door",
          "basement door",
          "fuse box",
          "quest key",
          "mirror/reflection surface",
          "escape door"
        ]
      },
      deviceSystem: {
        inventorySlots: 3,
        devices: paranormal?.devices || [],
        uiStyle: "original rugged device screens, no copied UI from existing games",
        deviceEvents: [
          "thermal spike",
          "radio static burst",
          "EM distortion spike",
          "UV clue reveal",
          "motion sensor alarm"
        ]
      },
      scareSystem: {
        triggers: paranormal?.jumpScares || [],
        defaultCooldownSeconds: 45,
        globalIntensityCurve: "low at intro, medium during exploration, high after basement objective",
        audioRules: [
          "pre-scare build-up",
          "directional whisper",
          "low bass hit",
          "door slam",
          "short shriek",
          "post-scare silence gap"
        ]
      },
      unrealAssemblyOutputs: {
        map: "Content/GameForge/Maps/GF_OneClick_Horror_Map",
        blueprints: "Content/GameForge/Blueprints",
        devices: "Content/GameForge/Devices",
        scares: "Content/GameForge/Scares",
        audio: "Content/GameForge/Audio",
        ui: "Content/GameForge/UI"
      },
      copyrightSafe: true
    };

    this.lastPlan = plan;
    projectState.unrealHorrorAssemblyPlan = plan;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    this.log("Building Unreal horror game assembly plan.");
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("gameplay", "Assembling first-person horror gameplay systems");

    const plan = this.createPlan(projectState);
    if (!window.gameforgeAPI?.unrealHorrorAssembleGame) {
      const result = { ok: true, mode: "frontend_plan_only", plan };
      this.lastRun = result;
      return result;
    }

    const result = await window.gameforgeAPI.unrealHorrorAssembleGame({ plan, projectState });
    this.lastRun = result;
    projectState.unrealHorrorAssemblyRun = result;
    return result;
  },

  formatPlan(plan = this.lastPlan) {
    if (!plan) return "No Unreal horror assembly plan yet.";
    return `# Unreal Horror Game Assembly Builder

Game: ${plan.gameName}
Target: ${plan.target}

Core Gameplay Loop:
${plan.gameplayLoop.map(x => "- " + x).join("\n")}

Required Unreal Systems:
${plan.requiredSystems.map(x => "- " + x).join("\n")}

Zones:
${plan.levelBlueprint.zones.map(z => `- ${z.id}: ${z.purpose} / scare density ${z.scareDensity}`).join("\n")}

Interactables:
${plan.levelBlueprint.interactables.map(x => "- " + x).join("\n")}

Outputs:
${Object.entries(plan.unrealAssemblyOutputs).map(([k,v]) => `- ${k}: ${v}`).join("\n")}`;
  },

  contextForHybridAI() {
    return `Unreal Horror Game Assembly Builder active:
- creates first-person horror controller, flashlight, doors, objectives, device inventory and jump scare trigger plan
- builds playable horror loop: arrival, investigate, restore power, survive scares, escape
- outputs Unreal blueprint/map assembly instructions for one-click build runner`;
  }
};

window.GameForgeUnrealHorrorGameAssemblyBuilder = GameForgeUnrealHorrorGameAssemblyBuilder;
