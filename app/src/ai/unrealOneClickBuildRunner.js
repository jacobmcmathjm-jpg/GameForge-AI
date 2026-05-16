const GameForgeUnrealOneClickBuildRunner = {
  lastPlan: null,
  lastRun: null,

  log(message) {
    console.log("[Unreal One-Click Build Runner v3.6]", message);
    for (const id of ["oneClickForgeLog", "gameIntelLog", "assetDownloaderLog", "photorealModeLog", "forgeReport"]) {
      const el = document.getElementById(id);
      if (!el) continue;
      const line = `${new Date().toLocaleTimeString()} — ${message}`;
      if (id === "forgeReport") {
        el.value = el.value ? `${el.value}\n${line}` : line;
      } else {
        el.value += `${line}\n`;
      }
      el.scrollTop = el.scrollHeight;
    }
  },

  createPlan(projectState = window.projectState || {}) {
    const gameName = projectState.name || "GameForge Game";
    const prompt = projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.promptCopyrightSafe || projectState.prompt || projectState.description || "";
    const sceneType = projectState.liveMeshySceneBlueprint?.sceneType
      || projectState.unrealPhotorealPackage?.sceneType
      || (/zombie|outbreak|open world/i.test(prompt) ? "open_world_zombie" : "haunted_house");
    const meshy = projectState.meshyAutonomousApiRun || projectState.liveMeshySceneRun || projectState.meshyFreeTestRun || null;
    const quality = projectState.photorealQualityReport || null;

    const plan = {
      mode: "Unreal One-Click Build Runner v3.6",
      generatedAt: new Date().toISOString(),
      gameName,
      prompt,
      sceneType,
      automationGoal: "Attempt real automated Unreal import, project build, cook, package, and Windows EXE output without manual handoff where possible.",
      executeAutomation: true,
      requiresInstalledTools: ["Unreal Engine 5", "Blender (recommended)", "Node.js"],
      preferredInputs: {
        horrorAssemblySystems: projectState.unrealHorrorAssemblyPlan?.requiredSystems?.length || 0,
        photorealPolishTarget: projectState.photorealScenePolishPlan?.target || null,
        paranormalDevices: projectState.paranormalJumpscarePlan?.devices?.length || 0,
        jumpScares: projectState.paranormalJumpscarePlan?.jumpScares?.length || 0,
        meshyMode: meshy?.summary?.apiMode || meshy?.mode || "free_test_or_local_mix",
        sceneObjects: projectState.scene?.objects?.length || 0,
        hasPhotorealQualityReport: Boolean(quality),
        qualityStatus: quality?.status || "unknown",
        qualityScore: quality?.score ?? null
      },
      autonomousStages: [
        "detect Unreal install/editor/UAT paths",
        "create or update .uproject scaffold",
        "write GameForge import manifest",
        "write Unreal Python import/build script",
        "copy or reference GameForge assets",
        "execute Unreal import script",
        "run BuildCookRun packaging command",
        "capture logs",
        "apply safe repair cycle if packaging fails",
        "retry once with conservative flags",
        "report final EXE output path or blockers"
      ],
      projectSettings: {
        enableLumen: true,
        preferNanite: true,
        enableVirtualShadowMaps: true,
        targetPlatform: "Win64",
        targetConfiguration: "Development",
        targetOutput: "Packaged EXE",
        introSplashText: "Development by GameForge AI"
      },
      limitations: [
        "Real packaging depends on Unreal being installed correctly on the local PC.",
        "If Meshy API is unavailable, GameForge falls back to free-test/local assets.",
        "Some plugin/compiler/environment issues may still require user attention."
      ]
    };

    this.lastPlan = plan;
    projectState.unrealOneClickBuildPlan = plan;
    return plan;
  },

  async run(projectState = window.projectState || {}) {
    this.log("Starting Unreal one-click build runner.");
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("export", "Attempting automated Unreal import and package build");

    const plan = this.createPlan(projectState);
    if (!window.gameforgeAPI?.unrealOneClickBuild) {
      const failed = { ok: false, error: "Unreal one-click backend unavailable", plan };
      this.lastRun = failed;
      return failed;
    }

    const result = await window.gameforgeAPI.unrealOneClickBuild({ plan, projectState, executeAutomation: true });
    this.lastRun = result;
    projectState.unrealOneClickBuildRun = result;

    if (result?.ok) {
      this.log(`Unreal one-click build completed. Status: ${result.status || "OK"}.`);
    } else {
      this.log("Unreal one-click build warning: " + (result?.error || result?.status || "unknown issue"));
    }

    document.dispatchEvent(new CustomEvent("gf-unreal-one-click-build-complete", { detail: result }));
    return result;
  },

  formatReport(report = this.lastRun) {
    if (!report) return "No Unreal one-click build report yet.";
    return `# Unreal One-Click Build Runner v3.6\n\nStatus: ${report.ok ? "OK" : "Warning"}\nResult: ${report.status || "N/A"}\n\nDetected Tools:\n${Object.entries(report.detectedTools || {}).map(([k,v]) => `- ${k}: ${v && (v.path || v.version || v.status || "detected")}`).join("\n")}\n\nFiles:\n${report.files ? Object.entries(report.files).map(([k,v]) => `- ${k}: ${typeof v === "string" ? v : JSON.stringify(v)}`).join("\n") : "- none"}\n\nSteps:\n${(report.steps || []).map(step => `- ${step.name}: ${step.status}${step.detail ? " — " + step.detail : ""}`).join("\n")}\n\nBlockers:\n${(report.blockers || []).length ? report.blockers.map(x => "- " + x).join("\n") : "- none"}\n\nNotes:\n${(report.notes || []).length ? report.notes.map(x => "- " + x).join("\n") : "- none"}`;
  },

  contextForHybridAI() {
    return `Unreal One-Click Build Runner v3.6 available:\n- detects Unreal editor and AutomationTool\n- creates/updates a real .uproject scaffold\n- writes Unreal Python automation scripts\n- attempts import + BuildCookRun packaging\n- captures logs and retries with a safe repair cycle\n- reports EXE output path or blockers`;
  }
};

window.GameForgeUnrealOneClickBuildRunner = GameForgeUnrealOneClickBuildRunner;
