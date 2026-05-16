
const GameForgeMeshyAutonomousAPI = {
  lastQueue: null,
  lastRun: null,

  log(message) {
    console.log("[Meshy API]", message);
    for (const id of ["oneClickForgeLog", "gameIntelLog", "assetDownloaderLog", "photorealModeLog"]) {
      const el = document.getElementById(id);
      if (el) {
        el.value += `${new Date().toLocaleTimeString()} — ${message}\n`;
        el.scrollTop = el.scrollHeight;
      }
    }
  },

  buildAssetQueue(projectState = window.projectState || {}) {
    const requests = window.GameForgeMeshyFreeTestProvider?.inferAssetRequests
      ? GameForgeMeshyFreeTestProvider.inferAssetRequests(projectState)
      : [
          { id: "hero_zombie", type: "character", priority: 1, prompt: "Original photoreal zombie enemy, game-ready 3D model, PBR, no copyrighted IP", texturePrompt: "decayed skin, torn clothing, grime, PBR textures" },
          { id: "abandoned_house", type: "building", priority: 1, prompt: "Original abandoned house, photoreal game-ready 3D model, PBR, no brands", texturePrompt: "weathered wood, dirty glass, rusty metal" }
        ];

    const queue = {
      mode: "Meshy Autonomous API Connector",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      prompt: projectState.prompt || "",
      targetFormats: ["glb"],
      legalRules: [
        "Use original prompts only.",
        "No copyrighted IP, famous characters, brands, celebrities or trademarked weapons.",
        "Store provider, prompt, task id, licence and credit metadata for every asset.",
        "Keep Free Test Mode unless a Meshy API key and paid/API mode are enabled."
      ],
      requests: requests.map((r, i) => ({
        ...r,
        queueIndex: i + 1,
        enabled: r.priority <= 2,
        status: "queued",
        previewTaskId: null,
        refineTaskId: null,
        downloadedFiles: []
      }))
    };
    this.lastQueue = queue;
    return queue;
  },

  async loadSettings() {
    if (window.gameforgeAPI?.meshyLoadSettings) return await window.gameforgeAPI.meshyLoadSettings();
    return { ok: true, settings: { mode: "free_test", apiKeySaved: false, paidProvidersAllowed: false, monthlyCreditCap: 100 } };
  },

  async run(projectState = window.projectState || {}, engine = null) {
    const settingsResult = await this.loadSettings();
    const settings = settingsResult.settings || {};
    const queue = this.buildAssetQueue(projectState);

    if (!settings.apiKeySaved || settings.mode !== "api" || !settings.paidProvidersAllowed) {
      this.log("Meshy API mode is OFF. Preparing free-test prompt pack instead.");
      if (window.GameForgeMeshyFreeTestProvider) {
        const freeResult = await GameForgeMeshyFreeTestProvider.preparePromptPack(projectState);
        this.lastRun = { ok: true, mode: "free_test_fallback", queue, freeResult };
        return this.lastRun;
      }
      return { ok: true, mode: "free_test_fallback", queue };
    }

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("assets", "Sending asset queue to Meshy API");
    }

    if (!window.gameforgeAPI?.meshyRunAutonomousQueue) {
      return { ok: false, error: "Meshy API backend runner unavailable", queue };
    }

    projectState.meshyApiStatus = 'running';
    const result = await window.gameforgeAPI.meshyRunAutonomousQueue({
      queue,
      projectState,
      settings: {
        monthlyCreditCap: settings.monthlyCreditCap || 100,
        maxAssetsPerRun: settings.maxAssetsPerRun || 8,
        commercialReleaseMode: Boolean(settings.commercialReleaseMode)
      }
    });

    this.lastRun = result;
    projectState.meshyApiStatus = result?.ok ? 'complete' : 'warning';
    if (result?.ok) {
      this.log(`Meshy API run complete. Downloaded ${result.summary?.downloaded || 0} files.`);
      projectState.meshyApiRun = result;
      if (window.GameForgePhotorealQualityGate) {
        try { GameForgePhotorealQualityGate.run(projectState, engine); } catch (e) { this.log("Quality gate warning: " + e.message); }
      }
    } else {
      this.log("Meshy API warning: " + (result?.error || "unknown issue"));
    }

    return result;
  },

  formatQueue(queue = this.lastQueue) {
    if (!queue) return "No Meshy API queue yet.";
    return `# Meshy Autonomous API Queue

Mode: ${queue.mode}
Game: ${queue.gameName}
Target formats: ${queue.targetFormats.join(", ")}

Legal Rules:
${queue.legalRules.map(x => "- " + x).join("\n")}

Requests:
${queue.requests.map(r => `- ${r.id} | ${r.type} | priority ${r.priority} | enabled ${r.enabled}
  Prompt: ${r.prompt}
  Texture: ${r.texturePrompt}`).join("\n\n")}`;
  },

  contextForHybridAI() {
    return `Meshy Autonomous API Connector available:
- Free Test Mode fallback by default
- API mode only when API key, mode=api and paidProvidersAllowed are enabled
- prepares asset queue, sends preview/refine jobs, polls, downloads files and stores metadata`;
  }
};

window.GameForgeMeshyAutonomousAPI = GameForgeMeshyAutonomousAPI;
