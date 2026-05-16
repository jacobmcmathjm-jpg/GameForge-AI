
const GameForgeScreenshotVisualScoringAutoRepair = {
  lastPlan: null,
  lastRun: null,

  createPlan(projectState = window.projectState || {}) {
    const plan = {
      mode: "Screenshot Visual Scoring + Auto Visual Repair",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      goal: "Capture required Unreal/game screenshots, score realism and repair indie-looking visuals before accepting output.",
      requiredScreenshots: [
        { id: "spawn_exterior", name: "Exterior approach", checks: ["building realism", "weather/sky", "lighting", "terrain/material detail"] },
        { id: "entry_hallway", name: "Entry hallway", checks: ["wall/floor PBR", "flashlight shadows", "scene dressing", "scale"] },
        { id: "device_closeup", name: "Paranormal device close-up", checks: ["device material detail", "UI originality", "hand/scale quality"] },
        { id: "bedroom_room", name: "Bedroom/interior room", checks: ["clutter", "furniture realism", "lighting mood", "no empty-room look"] },
        { id: "basement_objective", name: "Basement/fuse box objective", checks: ["rust/damp materials", "objective readability", "fog/darkness", "prop density"] },
        { id: "ghost_event", name: "Ghost/scare event", checks: ["entity realism", "cinematic framing", "lighting response", "audio/event feedback"] }
      ],
      scoring: {
        passScore: 88,
        warningScore: 78,
        failBelow: 78,
        scoreAreas: [
          "lighting realism",
          "PBR material quality",
          "structure realism",
          "prop/detail density",
          "character/entity realism",
          "atmosphere/fog/weather",
          "non-indie look",
          "cinematic presentation"
        ]
      },
      autoRepairs: [
        "replace flat materials with PBR material plan",
        "increase scene dressing density",
        "repair lighting/post-process/fog",
        "replace blocky structures with realistic structure generator output",
        "regenerate or replace weak ghost/device assets",
        "adjust material scale and roughness",
        "add decals/dirt/wear layers",
        "rerun screenshot capture and score"
      ],
      rejectIf: [
        "looks like indie prototype",
        "flat/cartoony lighting",
        "blocky walls/rooms",
        "empty rooms",
        "low-poly silhouettes",
        "flat pasted images",
        "cartoon ghost",
        "missing PBR maps",
        "no atmosphere"
      ]
    };

    this.lastPlan = plan;
    projectState.screenshotVisualScoringPlan = plan;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    const plan = this.createPlan(projectState);

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("visual-scoring", "Scoring screenshots for realism");
    }

    if (!window.gameforgeAPI?.screenshotVisualScore) {
      const result = { ok: true, mode: "frontend_plan_only", plan };
      this.lastRun = result;
      return result;
    }

    const result = await window.gameforgeAPI.screenshotVisualScore({ plan, projectState });
    this.lastRun = result;
    projectState.screenshotVisualScoringRun = result;
    return result;
  },

  formatReport(report = this.lastRun || { plan: this.lastPlan }) {
    const plan = report.plan || this.lastPlan;
    if (!plan) return "No screenshot visual scoring report yet.";
    return `# Screenshot Visual Scoring + Auto Visual Repair

Goal:
${plan.goal}

Required Screenshots:
${plan.requiredScreenshots.map(s => `- ${s.name}: ${s.checks.join(", ")}`).join("\n")}

Pass Score:
${plan.scoring.passScore}

Reject If:
${plan.rejectIf.map(x => "- " + x).join("\n")}

Auto Repairs:
${plan.autoRepairs.map(x => "- " + x).join("\n")}

Latest Status:
${report.status || "PLAN_READY"}`;
  },

  contextForHybridAI() {
    return `Screenshot Visual Scoring active:
- captures/scores exterior, hallway, device, room, basement and ghost-event screenshots
- rejects indie/blocky/flat/cartoony output
- creates automatic visual repair actions and requires pass score before quality label`;
  }
};

window.GameForgeScreenshotVisualScoringAutoRepair = GameForgeScreenshotVisualScoringAutoRepair;
