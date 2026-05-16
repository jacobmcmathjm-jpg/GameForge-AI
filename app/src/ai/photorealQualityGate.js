
const GameForgePhotorealQualityGate = {
  lastReport: null,

  thresholds: {
    photorealReady: 85,
    aaCandidate: 72
  },

  log(message) {
    console.log("[Photoreal Quality Gate]", message);
    const ids = ["photorealModeLog", "assetDownloaderLog", "oneClickForgeLog", "gameIntelLog"];
    for (const id of ids) {
      const el = document.getElementById(id);
      if (el) {
        el.value += `${new Date().toLocaleTimeString()} — ${message}\n`;
        el.scrollTop = el.scrollHeight;
      }
    }
  },

  counts(projectState = window.projectState || {}) {
    const sceneObjects = projectState.scene?.objects || [];
    const approved = projectState.approvedAssetRun?.summary || {};
    const selfAssets = projectState.selfAssetRun?.placedCount || projectState.selfAssetRun?.summary?.generated || 0;
    const audio = projectState.audioAssetRun?.generatedCount || projectState.audioAssetRun?.summary?.generated || 0;

    return {
      sceneObjects: sceneObjects.length,
      realAssetCount: approved.downloaded || 0,
      preparedAssetSlots: approved.prepared || 0,
      fallbackCount: selfAssets || 0,
      audioCount: audio || 0,
      buildings: sceneObjects.filter(o => /building|house|interior|level_section/i.test(`${o.type || ""} ${o.name || ""}`)).length,
      characters: sceneObjects.filter(o => /enemy|character|player|ghost|creature/i.test(`${o.type || ""} ${o.name || ""}`)).length,
      environment: sceneObjects.filter(o => /environment|road|tree|forest|terrain|ground/i.test(`${o.type || ""} ${o.name || ""}`)).length,
      objectives: sceneObjects.filter(o => /objective|pickup|win_condition|key|radio|fuse/i.test(`${o.type || ""} ${o.name || ""}`)).length
    };
  },

  addScore(report, name, max, ok, earnedWhenOk, detail, recommendation) {
    const earned = ok ? earnedWhenOk : Math.max(1, Math.floor(earnedWhenOk * 0.25));
    report.score += earned;
    report.categories.push({ name, earned, max, ok, detail });
    if (!ok) {
      report.blockers.push(`${name}: ${detail}`);
      if (recommendation) report.recommendations.push(recommendation);
    }
  },

  score(projectState = window.projectState || {}) {
    const c = this.counts(projectState);
    const report = {
      generatedAt: new Date().toISOString(),
      mode: "Photoreal Quality Gate",
      target: "real-life / looking-out-a-window graphics",
      score: 0,
      status: "Prototype Only",
      pass: false,
      counts: c,
      categories: [],
      blockers: [],
      recommendations: []
    };

    this.addScore(
      report,
      "Real imported asset coverage",
      22,
      c.realAssetCount >= 6,
      c.realAssetCount >= 12 ? 22 : 16,
      `${c.realAssetCount} downloaded/real approved assets detected`,
      "Photoreal mode needs real GLB/GLTF/texture assets for hero models, not only generated placeholders."
    );

    this.addScore(
      report,
      "Fallback asset control",
      8,
      c.fallbackCount <= 14,
      8,
      `${c.fallbackCount} self-generated/procedural fallback assets detected`,
      "Self-generated assets should support the scene, not replace all hero assets."
    );

    this.addScore(
      report,
      "World composition",
      12,
      (c.buildings >= 2 && c.environment >= 2),
      12,
      `buildings ${c.buildings}, environment ${c.environment}, objectives ${c.objectives}`,
      "Add a real exterior, real interior modules, environment dressing and reachable objectives."
    );

    this.addScore(
      report,
      "Character / enemy presence",
      10,
      c.characters >= 1,
      10,
      `${c.characters} character/enemy objects detected`,
      "Use rigged humanoid/ghost/enemy models with animation clips for better realism."
    );

    const hasPbr = Boolean(projectState.autonomousRealismPlan || projectState.approvedAssetRun || projectState.selfAssetRun);
    this.addScore(
      report,
      "PBR material system",
      14,
      hasPbr,
      14,
      hasPbr ? "PBR/self/approved material plan detected" : "no material plan detected",
      "Require albedo, normal, roughness, metallic/AO style maps for roads, walls, wood, metal and glass."
    );

    const photorealApplied = Boolean(projectState.runtime?.photoreal || projectState.photorealAlwaysOn || projectState.autonomousRealismPlan);
    this.addScore(
      report,
      "Photoreal renderer stack",
      16,
      photorealApplied,
      16,
      photorealApplied ? "photoreal lighting/post-processing metadata detected" : "renderer stack not detected",
      "Apply fog, exposure, colour grade, bloom, anti-aliasing, cinematic moonlight and shadow rules."
    );

    this.addScore(
      report,
      "Audio coverage",
      6,
      c.audioCount >= 6,
      6,
      `${c.audioCount} audio events detected`,
      "Generate or download ambience, footsteps, creaks, ghost sounds, jumpscares and UI sounds."
    );

    const exportPrep = Boolean(projectState.unrealExportPrep || projectState.completeGamePlan || projectState.unrealExportPrepResult);
    this.addScore(
      report,
      "Export / optimization readiness",
      12,
      exportPrep,
      12,
      exportPrep ? "export or Unreal preparation metadata detected" : "no export/optimization metadata detected",
      "Run draw-distance, LOD, texture budget, performance and Unreal handoff preparation."
    );

    report.score = Math.max(0, Math.min(100, report.score));

    if (report.score >= this.thresholds.photorealReady && report.blockers.length <= 1) {
      report.status = "Photoreal Ready";
      report.pass = true;
    } else if (report.score >= this.thresholds.aaCandidate) {
      report.status = "AA Candidate";
      report.pass = false;
    } else {
      report.status = "Prototype Only";
      report.pass = false;
    }

    if (!report.pass) {
      report.recommendations.unshift("Do not label this output photoreal yet. Resolve the quality blockers first.");
    }

    this.lastReport = report;
    projectState.photorealQualityReport = report;
    return report;
  },

  applyRendererStack(engine = null, projectState = window.projectState || {}) {
    projectState.runtime = projectState.runtime || {};
    projectState.runtime.photoreal = projectState.runtime.photoreal || {};
    projectState.runtime.photoreal.qualityStack = {
      name: "AA / Photoreal Renderer Stack",
      alwaysOn: true,
      lighting: "cinematic moonlight + warm practical lights",
      fog: "blue-grey volumetric-style fog",
      post: ["anti-aliasing", "bloom", "colour grading", "contrast", "exposure", "vignette", "depth hint"],
      materialRequirement: "PBR required",
      assetRequirement: "real models required for Photoreal Ready",
      qualityGate: "enabled"
    };

    try {
      if (window.GameForgePhotorealMode?.applyCinematicLook && engine) {
        GameForgePhotorealMode.applyCinematicLook(engine);
      }
      if (engine?.scene && window.BABYLON) {
        const scene = engine.scene;
        scene.clearColor = new BABYLON.Color4(0.005, 0.008, 0.012, 1);
        if (scene.fogMode !== undefined) {
          scene.fogMode = BABYLON.Scene.FOGMODE_EXP2;
          scene.fogDensity = 0.028;
          scene.fogColor = new BABYLON.Color3(0.045, 0.06, 0.08);
        }
        if (BABYLON.DefaultRenderingPipeline && engine.camera) {
          const pipeline = new BABYLON.DefaultRenderingPipeline("gf_aa_photoreal_quality_stack", true, scene, [engine.camera]);
          pipeline.fxaaEnabled = true;
          pipeline.bloomEnabled = true;
          pipeline.bloomThreshold = 0.68;
          pipeline.bloomWeight = 0.22;
          pipeline.imageProcessingEnabled = true;
          pipeline.imageProcessing.contrast = 1.32;
          pipeline.imageProcessing.exposure = 0.82;
        }
      }
    } catch (error) {
      this.log("Renderer stack warning: " + error.message);
    }

    return projectState.runtime.photoreal.qualityStack;
  },

  run(projectState = window.projectState || {}, engine = null) {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("lighting", "Applying photoreal renderer stack and quality gate");
    this.applyRendererStack(engine, projectState);
    const report = this.score(projectState);
    this.log(`Quality Gate: ${report.status} (${report.score}/100).`);
    if (!report.pass) this.log("Blockers: " + report.blockers.slice(0, 3).join(" | "));
    document.dispatchEvent(new CustomEvent("gf-photoreal-quality-report", { detail: report }));
    return { ok: true, report };
  },

  format(report = this.lastReport) {
    if (!report) return "No photoreal quality report yet.";
    return `# Photoreal Quality Gate

Status: ${report.status}
Score: ${report.score}/100
Pass: ${report.pass ? "YES" : "NO"}

Target:
${report.target}

Category Scores:
${report.categories.map(c => `- ${c.name}: ${c.earned}/${c.max} | ${c.ok ? "OK" : "Needs work"} | ${c.detail}`).join("\n")}

Blockers:
${report.blockers.length ? report.blockers.map(b => "- " + b).join("\n") : "- None"}

Recommendations:
${report.recommendations.length ? report.recommendations.map(r => "- " + r).join("\n") : "- None"}`;
  },

  contextForHybridAI() {
    return `Photoreal Quality Gate available:
- prevents blocky/prototype output being labelled photoreal
- requires real imported assets, PBR materials, renderer stack, character presence, audio and export readiness
- labels output as Prototype Only, AA Candidate or Photoreal Ready
- applies AA/photoreal renderer stack before validation`;
  }
};

window.GameForgePhotorealQualityGate = GameForgePhotorealQualityGate;
