
const GameForgeIntricateGameplaySystemsArchitectV12Bridge = {
  lastPlan: null,
  importedPackageName: "GameForge AI v12.1 Intricate Gameplay Systems Architect",

  createPlan(projectState = window.projectState || {}) {
    const prompt = projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.prompt || projectState.description || "";

    const plan = {
      mode: "Intricate Gameplay Systems Architect v12 Bridge",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      prompt,
      source: this.importedPackageName,
      goal: "Merge the advanced gameplay/system design layer from the v12.1 package into the current GameForge UI/PC app generation pipeline.",
      gameplaySystems: {
        coreLoop: ["player goal", "moment-to-moment loop", "reward/feedback", "failure/retry", "completion condition"],
        missionAndObjectiveDesign: ["primary objective chain", "secondary objectives", "branching tasks", "dependencies", "completion report"],
        progression: ["upgrades", "equipment/tool progression", "difficulty escalation", "unlock gates", "skill/perk structure"],
        interactionSystems: ["doors", "pickups", "devices/tools/weapons", "inventory/loadout", "interaction prompts", "environment triggers"],
        enemyAndNPCSystems: ["spawn director", "patrol/investigation", "chase/attack/event states", "difficulty tuning", "boss/special entity support"],
        worldSystems: ["level flow", "zone logic", "safe/danger areas", "loot/resource placement", "event pacing", "replayable/randomised elements"],
        uiFeedback: ["objective HUD", "interaction prompts", "inventory/tool UI", "status indicators", "mission completion feedback", "pause/settings/menu flow"]
      },
      genreAdaptation: {
        horror: "investigation loop, evidence/device systems, scare pacing, escape objective",
        zombie: "weapon/resource loop, enemy waves, survival/extraction objectives",
        familyAdventure: "safe exploration, collectibles, friendly quests, gentle progression",
        racing: "checkpoint/lap/objective loop, upgrade progression, event routes",
        survival: "resource gathering, crafting, shelter, weather/threat escalation",
        sciFi: "exploration, scanning, systems repair, environmental hazards",
        fantasy: "quest chain, combat/magic/tool progression, exploration and boss encounter"
      },
      outputRules: [
        "Do not label full/playable unless the core loop can be completed.",
        "Objectives must have start, progress and completion states.",
        "Interactions must provide player feedback.",
        "AI/enemy/event systems must have safe fallback behaviours.",
        "UI prompts must be present for core interactions.",
        "If a gameplay system is planned but not implemented/testable, mark repair-required."
      ],
      integrationTargets: [
        "Cinematic Genre Scene Composer",
        "Autonomous Full Game Builder",
        "Advanced Unreal Scene Builder",
        "Gameplay Systems Builder",
        "Advanced AI / Enemy Behaviour Builder",
        "Playable EXE Validator",
        "Build-Test-Repair Loop",
        "Commercial Release Readiness"
      ]
    };

    this.lastPlan = plan;
    projectState.intricateGameplaySystemsArchitectV12Plan = plan;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    const plan = this.createPlan(projectState);

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("gameplay", "Applying intricate gameplay architecture");
    }

    if (!window.gameforgeAPI?.intricateGameplayArchitectRun) {
      const result = { ok: true, mode: "frontend_plan_only", plan };
      projectState.intricateGameplaySystemsArchitectV12Run = result;
      return result;
    }

    const result = await window.gameforgeAPI.intricateGameplayArchitectRun({ plan, projectState });
    projectState.intricateGameplaySystemsArchitectV12Run = result;
    return result;
  },

  formatReport(report = { plan: this.lastPlan }) {
    const plan = report.plan || this.lastPlan;
    if (!plan) return "No Intricate Gameplay Systems Architect report yet.";
    return `# Intricate Gameplay Systems Architect v12 Bridge

Source:
${plan.source}

Goal:
${plan.goal}

Core Gameplay Systems:
${Object.entries(plan.gameplaySystems).map(([k,v]) => `- ${k}: ${v.join(", ")}`).join("\n")}

Genre Adaptation:
${Object.entries(plan.genreAdaptation).map(([k,v]) => `- ${k}: ${v}`).join("\n")}

Output Rules:
${plan.outputRules.map(x => "- " + x).join("\n")}`;
  },

  contextForHybridAI() {
    return `Intricate Gameplay Systems Architect v12 Bridge active:
- merges the uploaded v12.1 gameplay architecture into the current GameForge UI/PC app
- strengthens core loop, missions, progression, interactions, AI/NPC, world systems and UI feedback
- requires objectives/interactions/gameplay loop to be testable before full-game label`;
  }
};

window.GameForgeIntricateGameplaySystemsArchitectV12Bridge = GameForgeIntricateGameplaySystemsArchitectV12Bridge;
