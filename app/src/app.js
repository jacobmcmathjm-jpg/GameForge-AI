
async function safeFree3DCreateJob(typeOrPayload, name, description, style = "realistic_prototype", count = 1) {
  const payload = (typeof typeOrPayload === "object" && typeOrPayload !== null)
    ? typeOrPayload
    : { type: typeOrPayload, name, description, style, count };
  try {
    if (window.GameForgeFree3DGenerator && typeof window.GameForgeFree3DGenerator.createJob === "function") {
      return await window.GameForgeFree3DGenerator.createJob(payload.type, payload.name, payload.description, payload.style || style, payload.count || count);
    }
    if (window.gameforgeAPI && typeof window.gameforgeAPI.free3DCreateJob === "function") {
      return await window.gameforgeAPI.free3DCreateJob(payload);
    }
    return { ok: true, fallback: true, reason: "free3DCreateJob unavailable", job: payload };
  } catch (error) {
    console.warn("Free 3D job creation failed. Using fallback:", error);
    return { ok: true, fallback: true, reason: error.message, job: payload };
  }
}

async function safeRunFree3DQueueIntoScene(engine) {
  try {
    if (window.GameForgeFree3DGenerator && typeof window.GameForgeFree3DGenerator.runQueueIntoScene === "function") {
      return await window.GameForgeFree3DGenerator.runQueueIntoScene(engine);
    }
    return [];
  } catch (error) {
    console.warn("Free 3D queue failed. Skipping:", error);
    return [];
  }
}

async function safeInternalMeshCreateRecipe(typeOrPayload, name, style = "realistic_prototype", detail = "medium") {
  const payload = (typeof typeOrPayload === "object" && typeOrPayload !== null)
    ? typeOrPayload
    : { type: typeOrPayload, name, style, detail };
  try {
    if (window.GameForgeInternalMeshGenerator && typeof window.GameForgeInternalMeshGenerator.createRecipe === "function") {
      return await window.GameForgeInternalMeshGenerator.createRecipe(payload.type, payload.name, payload.style || style, payload.detail || detail);
    }
    if (window.gameforgeAPI && typeof window.gameforgeAPI.internalMeshCreateRecipe === "function") {
      return await window.gameforgeAPI.internalMeshCreateRecipe(payload);
    }
    return { ok: true, fallback: true, reason: "internalMeshCreateRecipe unavailable", recipe: payload };
  } catch (error) {
    console.warn("Internal Mesh recipe creation failed. Using fallback:", error);
    return { ok: true, fallback: true, reason: error.message, recipe: payload };
  }
}

async function safeInternalMeshCreateMaterial(presetOrPayload, name) {
  const payload = (typeof presetOrPayload === "object" && presetOrPayload !== null)
    ? presetOrPayload
    : { preset: presetOrPayload, name };
  try {
    if (window.GameForgeInternalMeshGenerator && typeof window.GameForgeInternalMeshGenerator.createMaterial === "function") {
      return await window.GameForgeInternalMeshGenerator.createMaterial(payload.preset, payload.name);
    }
    if (window.gameforgeAPI && typeof window.gameforgeAPI.internalMeshCreateMaterial === "function") {
      return await window.gameforgeAPI.internalMeshCreateMaterial(payload);
    }
    if (window.gameforgeAPI && typeof window.gameforgeAPI.pbrCreateMaterial === "function") {
      return await window.gameforgeAPI.pbrCreateMaterial(payload);
    }
    return { ok: true, fallback: true, reason: "internalMeshCreateMaterial unavailable", material: payload };
  } catch (error) {
    console.warn("Internal Mesh material creation failed. Using fallback:", error);
    return { ok: true, fallback: true, reason: error.message, material: payload };
  }
}

async function safeGenerateInternalMeshPackIntoScene(engine, packName = "autonomous") {
  try {
    if (window.GameForgeInternalMeshGenerator && typeof window.GameForgeInternalMeshGenerator.generatePackIntoScene === "function") {
      return await window.GameForgeInternalMeshGenerator.generatePackIntoScene(engine, packName);
    }
    return [];
  } catch (error) {
    console.warn("Internal Mesh pack generation failed. Skipping:", error);
    return [];
  }
}


async function createDefaultAnimationManifest() {
  const result = await GameForgeAnimationAssetGatherer.createDefaultManifest();
  $("animationGathererLog").value = result.ok
    ? `Approved animation manifest created/opened here:\n${result.manifestPath}\n\nEdit this file, add direct GLB/GLTF URLs and licence text, then run Animation Gatherer.`
    : "Manifest failed: " + result.error;
}

async function createAnimationManifestFromGame() {
  const result = await GameForgeAnimationAssetGatherer.createManifestFromRequirements();
  $("animationGathererLog").value = result.ok
    ? `Animation manifest created from current game requirements:\n${result.manifestPath}\n\nCharacters listed: ${result.manifest.characters.length}\nAnimation packs listed: ${result.manifest.animationPacks.length}\n\nFill URLs/licences and set enabled=true for assets to gather.`
    : "Manifest from game failed: " + result.error;
}

async function runAnimationGatherer() {
  const result = await GameForgeAnimationAssetGatherer.runGatherer();
  $("animationGathererLog").value = result.ok
    ? GameForgeAnimationAssetGatherer.formatReport(result.report) + `\n\nSaved report:\n${result.reportPath}`
    : "Animation gatherer failed: " + result.error;
}

async function runFullAnimationGatherImport() {
  const result = await GameForgeAnimationAssetGatherer.runFullAnimationGatherAndImport(gfEngine);
  $("animationGathererLog").value = `Animation gather + import complete.

Gather:
${result.gather?.ok ? GameForgeAnimationAssetGatherer.formatReport(result.gather.report) : "Gather error: " + result.gather?.error}

Animation Import:
${result.animPass?.assign?.ok ? `Assigned controllers: ${result.animPass.assign.assigned.length}` : "Animation pass unavailable"}

Missing Report:
${result.missing?.ok ? GameForgeAnimationAssetGatherer.formatMissingReport(result.missing.report) : "Missing report unavailable"}`;
}

async function createMissingAnimationReport() {
  const result = await GameForgeAnimationAssetGatherer.createMissingReport();
  $("animationGathererLog").value = result.ok
    ? GameForgeAnimationAssetGatherer.formatMissingReport(result.report) + `\n\nSaved:\n${result.reportPath}`
    : "Missing animation report failed: " + result.error;
}


async function createAnimationImportPlan() {
  const result = await GameForgeAnimationImporter.createPlan();
  $("animationImportLog").value = result.ok
    ? GameForgeAnimationImporter.formatPlan(result.plan) + `

Saved:
${result.planPath}`
    : "Animation plan failed: " + result.error;
}

async function runAnimationImportPass() {
  const result = await GameForgeAnimationImporter.runAnimationImportPass(gfEngine);
  $("animationImportLog").value = `Animation Import Pass complete.

Plan:
${result.plan?.ok ? GameForgeAnimationImporter.formatPlan(result.plan.plan) : "Plan failed: " + result.plan?.error}

Assigned controllers:
${result.assign?.ok ? result.assign.assigned.length : "Assignment failed"}

Runtime hook:
${result.hook?.ok ? "enabled" : result.hook?.error}`;
}

function assignAnimationControllers() {
  const result = GameForgeAnimationImporter.assignControllersToScene(gfEngine);
  $("animationImportLog").value = result.ok
    ? `Assigned controllers: ${result.assigned.length}`
    : "Assignment failed: " + result.error;
}


async function createDefaultModelManifest() {
  const result = await GameForgeModelGatherer.createDefaultManifest();
  $("modelGathererLog").value = result.ok
    ? `Approved model manifest created/opened here:\n${result.manifestPath}\n\nEdit this file, add direct GLB/GLTF URLs and licence text, then run Model Gatherer.`
    : "Manifest failed: " + result.error;
}

async function createModelManifestFromGame() {
  const result = await GameForgeModelGatherer.createManifestFromRequirements();
  $("modelGathererLog").value = result.ok
    ? `Manifest created from current game requirements:\n${result.manifestPath}\n\nModels listed: ${result.manifest.models.length}\n\nFill URLs/licences and set enabled=true for models to gather.`
    : "Manifest from game failed: " + result.error;
}

async function runModelGatherer() {
  const result = await GameForgeModelGatherer.runGatherer();
  $("modelGathererLog").value = result.ok
    ? GameForgeModelGatherer.formatReport(result.report) + `\n\nSaved report:\n${result.reportPath}`
    : "Gatherer failed: " + result.error;
}

async function runGatherAndImport() {
  const result = await GameForgeModelGatherer.runFullGatherAndImportPass(gfEngine);
  refreshHierarchy && refreshHierarchy();
  $("modelGathererLog").value = `Gather + Auto Import complete.

Gather:
${result.gather?.ok ? GameForgeModelGatherer.formatReport(result.gather.report) : "Gather error: " + result.gather?.error}

Auto Import:
${result.importPass?.plan?.ok ? GameForgeAutoAssetImporter.formatPlan(result.importPass.plan.plan) : "Auto import plan unavailable"}`;
}


async function scanAutoAssets() {
  const result = await GameForgeAutoAssetImporter.scanModels();
  $("autoAssetImportLog").value = result.ok
    ? `Local GLB/GLTF models found: ${result.assets.length}

${result.assets.map(a => `- ${a.name} (${a.type})
  ${a.path}`).join("\n\n")}`
    : "Scan failed: " + result.error;
}

async function createAutoAssetPlan() {
  const result = await GameForgeAutoAssetImporter.createPlanFromGameIntelligence();
  $("autoAssetImportLog").value = result.ok
    ? GameForgeAutoAssetImporter.formatPlan(result.plan) + `

Saved:
${result.planPath}`
    : "Plan failed: " + result.error;
}

async function runAutoAssetPass() {
  const result = await GameForgeAutoAssetImporter.runAutoImportPass(gfEngine);
  refreshHierarchy && refreshHierarchy();
  $("autoAssetImportLog").value = `Auto Import Pass complete.

Scanned local models: ${result.scanCount}

${result.plan?.ok ? GameForgeAutoAssetImporter.formatPlan(result.plan.plan) : "Plan error: " + result.plan?.error}

Placement:
${result.placement?.ok ? `Placed/assigned: ${result.placement.placed.length}
Fallbacks: ${result.placement.fallbacks.length}` : "Placement error: " + result.placement?.error}`;
}


async function createPhotorealModePlan() {
  updateProjectFromInputs();
  const result = await GameForgePhotorealMode.createPlan(projectState);
  $("photorealModeLog").value = result.ok
    ? GameForgePhotorealMode.format(result.plan) + `

Saved:
Plan: ${result.planPath}
Report: ${result.reportPath}
Checklist: ${result.checklistPath}`
    : "Photoreal plan failed: " + result.error;
}

function applyPhotorealLighting() {
  const result = GameForgePhotorealMode.applyCinematicLook(gfEngine);
  $("photorealModeLog").value += result.ok ? "\n\nCinematic lighting/fog applied." : "\n\nLighting failed: " + result.error;
}

async function createPhotorealPBRPack() {
  const result = await GameForgePhotorealMode.createPBRSurfacePack();
  $("photorealModeLog").value += result.ok ? `\n\nPBR surface pack created: ${result.results.length} material recipes.` : "\n\nPBR pack failed.";
}

function applyPhotorealDetailPass() {
  const result = GameForgePhotorealMode.applySceneDetailPass(gfEngine);
  refreshHierarchy && refreshHierarchy();
  $("photorealModeLog").value += result.ok ? `\n\nScene detail pass added: ${result.placed.length} detail items.` : "\n\nDetail pass failed: " + result.error;
}


async function createCompleteGamePlan() {
  updateProjectFromInputs();
  const result = await GameForgeCompleteGameBuilder.createPlan(projectState);
  $("completeGameLog").value = result.ok ? GameForgeCompleteGameBuilder.formatPlan(result.plan) : "Plan failed: " + result.error;
}

async function exportCompleteGameExePackage() {
  updateProjectFromInputs();
  if (!GameForgeCompleteGameBuilder.lastPlan) await createCompleteGamePlan();
  const result = await GameForgeCompleteGameBuilder.exportExePackage(projectState, gfEngine);
  if (result.ok) {
    $("completeGameLog").value = `Full game EXE package created.

Folder:
${result.exportDir}

Build script:
${result.manifest.buildScript}

Quick test:
${result.manifest.runScript}

Expected EXE after build:
${result.manifest.expectedExe}

Startup intro included:
${result.manifest.branding?.introText || "Developed by GameForge AI"}

Next step:
Open the folder and double-click BUILD_GAME_EXE.bat.`;
  } else {
    $("completeGameLog").value = "Export failed: " + result.error;
  }
}


async function launchExternalPlaytestWindow() {
  updateProjectFromInputs();
  const result = GameForgeExternalPlaytest.launchPlaytestWindow(projectState, gfEngine);
  $("playtestExportLog").value = result.ok ? "External playtest window launched.\n" : "Launch failed: " + result.error;
}

async function validatePlayableDemo() {
  updateProjectFromInputs();
  const result = await GameForgeExternalPlaytest.validate(projectState, gfEngine);
  $("playtestExportLog").value = result.ok ? GameForgeExternalPlaytest.formatValidation(result.report) : "Validation failed: " + result.error;
}

async function exportPlayableDemo() {
  updateProjectFromInputs();
  const result = await GameForgeExternalPlaytest.exportDemo(projectState, gfEngine);
  if (result.ok) {
    $("playtestExportLog").value = `Playable demo package exported.

Folder:
${result.demoDir}

Manifest:
${JSON.stringify(result.manifest, null, 2)}

Open the exported folder and double-click PLAY_DEMO.bat.`;
  } else {
    $("playtestExportLog").value = "Export failed: " + result.error;
  }
}




async function runSafeDashboardGeneration(autoPlay = false) {
  updateProjectFromInputs();
  if (window.GameForgeSafePipeline && typeof GameForgeSafePipeline.generate === "function") {
    const result = await GameForgeSafePipeline.generate(projectState, gfEngine, autoPlay);
    if (!result.ok) alert("Safe pipeline could not start: " + result.error);
    return result;
  }

  // Last fallback if safe pipeline script failed to load.
  try {
    if (typeof forgeDraft === "function") {
      forgeDraft();
      setPanel("studio");
      return { ok:true, fallback:true };
    }
  } catch (e) {
    alert("Generation fallback failed: " + e.message);
    return { ok:false, error:e.message };
  }
}

async function runDashboardBuiltInGeneration() {
  return await runSafeDashboardGeneration(false);
}

async function runOneClickForge(autoPlay=false){
  updateProjectFromInputs();
  if(!projectState.prompt||!projectState.prompt.trim()){alert("Paste a game prompt into the main Game Description / Generation box first.");return;}
  const result=await GameForgeOneClickForge.run(projectState,gfEngine,autoPlay);
  if(!result.ok)alert("Autonomous Forge hit an issue: "+result.error);
}
function clearOneClickLog(){
  if($("oneClickForgeLog")) $("oneClickForgeLog").value="";
  ["intel","visual","jobs","models","scene","free3d","mesh","animgather","anim","photo","validate","exportprep","ready"].forEach(s=>{const e=$("forgeStep_"+s);if(e){e.textContent="WAITING";e.className="";}});
}


async function createGameIntelPlan() {
  updateProjectFromInputs();
  const plan = GameForgeGameIntelligence.createPlan(projectState);
  $("gameIntelLog").value = GameForgeGameIntelligence.format(plan);
}

async function createGameIntelJobs() {
  if (!GameForgeGameIntelligence.lastPlan) await createGameIntelPlan();
  const result = await GameForgeGameIntelligence.createDownstreamJobs();
  if (result.ok) {
    $("gameIntelLog").value += `

Downstream jobs created:
Visual target: ${result.results.visualTarget ? "created" : "not created"}
Free 3D jobs: ${result.results.free3D}
Internal mesh recipes: ${result.results.internalMesh}
PBR material recipes: ${result.results.pbr}`;
  } else {
    $("gameIntelLog").value += "\n\nDownstream job creation failed: " + result.error;
  }
}

async function runGameIntelFree3DQueue() {
  if (!window.GameForgeFree3DGenerator) {
    $("gameIntelLog").value += "\n\nFree 3D Generator not available.";
    return;
  }
  const results = await safeRunFree3DQueueIntoScene(gfEngine);
  refreshHierarchy && refreshHierarchy();
  $("gameIntelLog").value += `\n\nGenerated Free 3D queue into scene: ${results.length} jobs.`;
  setPanel("studio");
}


let lastInternalMeshRecipe = null;

async function createMeshRecipe() {
  const result = await safeInternalMeshCreateRecipe(
    $("meshAssetType").value,
    $("meshAssetName").value,
    "realistic_prototype",
    $("meshDetail").value
  );
  if (result.ok) {
    lastInternalMeshRecipe = result.recipe;
    $("internalMeshLog").value = `Mesh recipe created.

Recipe:
${JSON.stringify(result.recipe, null, 2)}

Saved:
${result.recipePath}

Metadata:
${result.metaPath}`;
  } else {
    $("internalMeshLog").value = "Recipe failed: " + result.error;
  }
}

function placeMeshInScene() {
  if (!lastInternalMeshRecipe) {
    $("internalMeshLog").value += "\n\nCreate a mesh recipe first.";
    return;
  }
  const result = GameForgeInternalMeshGenerator.instantiateRecipe(gfEngine, lastInternalMeshRecipe);
  refreshHierarchy && refreshHierarchy();
  $("internalMeshLog").value += result.ok ? "\n\nPlaced mesh in scene." : "\n\nPlacement failed: " + result.error;
}

async function createPBRMaterial() {
  const result = await safeInternalMeshCreateMaterial($("pbrMaterialPreset").value);
  $("internalMeshLog").value = result.ok
    ? `PBR material recipe created:\n${JSON.stringify(result.recipe, null, 2)}\n\nSaved:\n${result.matPath}`
    : "Material failed: " + result.error;
}

async function generateMeshPack() {
  $("internalMeshLog").value = "Generating internal mesh horror asset pack into scene...";
  const results = await safeGenerateInternalMeshPackIntoScene(gfEngine, "zombie_horror");
  refreshHierarchy && refreshHierarchy();
  $("internalMeshLog").value += `\n\nGenerated ${results.length} mesh assets into the scene.`;
  setPanel("studio");
}

async function createGLBExportPlan() {
  const result = await GameForgeInternalMeshGenerator.createExportPlan($("meshAssetName").value, "internal_mesh_recipe_or_current_scene");
  $("internalMeshLog").value = result.ok
    ? `GLB export plan created:\n${JSON.stringify(result.plan, null, 2)}\n\nSaved:\n${result.planPath}`
    : "Export plan failed: " + result.error;
}


async function createVisualTargetPlan() {
  updateProjectFromInputs();
  const record = GameForgeVisualTarget.createRecord(projectState, {
    style: $("visualTargetStyle").value,
    perspective: $("visualTargetPerspective").value,
    targetQuality: $("visualTargetQuality").value
  });
  $("visualTargetLog").value = GameForgeVisualTarget.format(record);
}
async function copyVisualPrompt() {
  if (!GameForgeVisualTarget.lastRecord) await createVisualTargetPlan();
  const text = GameForgeVisualTarget.lastRecord?.visualPrompt || "";
  try { await navigator.clipboard.writeText(text); $("visualTargetLog").value += "\n\nVisual prompt copied."; }
  catch(e) { $("visualTargetLog").value += "\n\nCopy failed. Prompt:\n" + text; }
}
async function createVisualFree3DJobs() {
  if (!GameForgeVisualTarget.lastRecord) await createVisualTargetPlan();
  const result = await GameForgeVisualTarget.createFree3DJobsFromTarget();
  $("visualTargetLog").value += result.ok ? `\n\nFree 3D jobs created: ${result.results.length}` : "\n\nFailed: " + result.error;
}
async function createVisualAI3DJobs() {
  if (!GameForgeVisualTarget.lastRecord) await createVisualTargetPlan();
  const result = await GameForgeVisualTarget.createAI3DJobsFromTarget();
  $("visualTargetLog").value += result.ok ? `\n\nAI 3D job records created: ${result.results.length}` : "\n\nFailed: " + result.error;
}
async function runVisualFree3DQueue() {
  if (!window.GameForgeFree3DGenerator) { $("visualTargetLog").value += "\n\nFree 3D Generator not available."; return; }
  const results = await safeRunFree3DQueueIntoScene(gfEngine);
  refreshHierarchy && refreshHierarchy();
  $("visualTargetLog").value += `\n\nGenerated Free 3D queue into scene: ${results.length} jobs.`;
  setPanel("studio");
}


async function createFree3DJob() {
  const result = await safeFree3DCreateJob(
    $("free3DAssetType").value,
    $("free3DAssetName").value,
    $("free3DPrompt").value,
    $("free3DStyle").value,
    1
  );
  $("free3DLog").value = result.ok
    ? `Free 3D job created.

Job:
${JSON.stringify(result.job, null, 2)}

Queue:
${GameForgeFree3DGenerator.formatQueue()}`
    : "Job failed: " + result.error;
}

async function createFree3DAssetPack() {
  updateProjectFromInputs();
  const result = await GameForgeFree3DGenerator.createAssetPack(projectState, $("free3DStyle").value, 1);
  $("free3DLog").value = result.ok
    ? `Free 3D asset pack queued.

Jobs created: ${result.jobs.length}

Queue:
${GameForgeFree3DGenerator.formatQueue()}`
    : "Asset pack failed: " + result.error;
}

async function runFree3DQueue() {
  $("free3DLog").value = "Generating Free 3D queue into scene...\n";
  const results = await safeRunFree3DQueueIntoScene(gfEngine);
  refreshHierarchy && refreshHierarchy();
  $("free3DLog").value += `\nCompleted ${results.length} job(s).

Completed:
${GameForgeFree3DGenerator.formatCompleted()}`;
}

function clearFree3DLog() {
  $("free3DLog").value = "";
}


async function loadAI3DSettings() {
  const result = await GameForgeAI3DAssetPipeline.loadSettings();
  const log = $("ai3DLog");
  if (result.ok) {
    $("ai3DProvider").value = result.settings.provider || "manual";
    $("ai3DQuality").value = result.settings.defaultQuality || "game_ready";
    $("ai3DLicenceMode").value = result.settings.licenceMode || "commercial_safe";
    $("ai3DRequireApproval").checked = result.settings.requireHumanApproval !== false;
    $("ai3DAutoImport").checked = result.settings.autoImportApprovedGLB !== false;
    if (result.settings.providerApiKeySaved) $("ai3DProviderKey").placeholder = "Provider API key saved locally";
    if (log) log.value = `AI 3D settings loaded.
Provider: ${result.settings.provider}
API key saved: ${result.settings.providerApiKeySaved ? "Yes" : "No"}
Quality: ${result.settings.defaultQuality}`;
  } else if (log) {
    log.value = "Failed to load AI 3D settings: " + result.error;
  }
}

async function saveAI3DSettings() {
  const result = await GameForgeAI3DAssetPipeline.saveSettings({
    provider: $("ai3DProvider").value,
    providerApiKey: $("ai3DProviderKey").value.trim(),
    defaultQuality: $("ai3DQuality").value,
    licenceMode: $("ai3DLicenceMode").value,
    requireHumanApproval: $("ai3DRequireApproval").checked,
    autoImportApprovedGLB: $("ai3DAutoImport").checked
  });
  $("ai3DLog").value = result.ok
    ? `AI 3D settings saved.
Provider: ${result.settings.provider}
API key saved: ${result.settings.providerApiKeySaved ? "Yes" : "No"}
Quality: ${result.settings.defaultQuality}
Licence mode: ${result.settings.licenceMode}`
    : "Save failed: " + result.error;
  if (result.ok && $("ai3DProviderKey").value) $("ai3DProviderKey").value = "";
}

async function createAI3DPlan() {
  updateProjectFromInputs();
  const result = await GameForgeAI3DAssetPipeline.createAssetPlan(projectState);
  $("ai3DLog").value = result.ok ? GameForgeAI3DAssetPipeline.formatPlan(result.plan) : "Plan failed: " + result.error;
}

async function createAI3DJobs() {
  await saveAI3DSettings();
  if (!GameForgeAI3DAssetPipeline.lastPlan) {
    await createAI3DPlan();
  }
  const result = await GameForgeAI3DAssetPipeline.createJobsFromPlan();
  if (!result.ok) {
    $("ai3DLog").value += "\n\nJob creation failed: " + result.error;
    return;
  }
  $("ai3DLog").value += `\n\nProvider job records created: ${result.results.length}\n\n` + result.results.map(r => `- ${r.job?.assetName || "asset"}: ${r.message}\n  Job file: ${r.jobPath || ""}`).join("\n\n");
}

async function importAI3DModel() {
  const result = await GameForgeAI3DAssetPipeline.importModelUrl({
    modelUrl: $("ai3DModelUrl").value.trim(),
    assetName: $("ai3DImportName").value.trim(),
    assetType: $("ai3DImportType").value,
    licenceText: $("ai3DLicenceText").value.trim(),
    provider: $("ai3DProvider").value
  });
  if (result.ok) {
    $("ai3DLog").value = `Generated model imported.

Name: ${result.asset.name}
Saved: ${result.asset.path}
Copied into Asset Library: ${result.asset.libraryPath}
Metadata: ${result.metadataPath}

Now scan Asset Library / Character Models to use it.`;
    await scanAssetLibrary();
    if (window.GameForgeCharacterImporter) await GameForgeCharacterImporter.renderList();
  } else {
    $("ai3DLog").value = "Import failed: " + result.error;
  }
}

async function rewriteSafeAI3DPrompt() {
  const result = await GameForgeAI3DAssetPipeline.sanitisePrompt($("rawAI3DPrompt").value, $("safePromptAssetType").value);
  $("safeAI3DPromptResult").value = result.ok ? result.safePrompt : "Rewrite failed: " + result.error;
}

function showAI3DPolicy() {
  $("ai3DLog").value = `# AI 3D Copyright Safety Policy

Avoid prompts that reference:
- copyrighted game/movie franchises
- famous characters
- famous actors or celebrity likenesses
- exact replicas
- branded products
- "make it look like X"

Use original descriptions instead:
- original realistic infected survivor
- original abandoned rural service station
- original survival pistol
- original pine forest tree
- original horror farmhouse

Store metadata:
- provider
- prompt
- safe prompt
- job ID
- model URL
- licence/terms text
- date generated
- commercial-use status

Before commercial release:
- verify provider terms
- verify commercial-use rights
- keep metadata/receipts
- run Licence Auditor`;
}


async function stableGeneratePrototype() {
  try {
    if (window.gfEngine?.generatePlayableSurvivalPrototype) {
      gfEngine.generatePlayableSurvivalPrototype();
      refreshHierarchy && refreshHierarchy();
      if (typeof setPanel === "function") setPanel("studio");
    } else {
      alert("Playable prototype engine is not ready yet.");
    }
  } catch (error) {
    console.error("Stable prototype generation failed", error);
    alert("Stable prototype generation failed: " + error.message);
  }
}

function stableResetRuntime() {
  try {
    if (window.gfEngine?.resetRuntime) {
      gfEngine.resetRuntime();
      if (window.gfEngine.camera) {
        gfEngine.camera.position = new BABYLON.Vector3(0, 2, -8);
        gfEngine.camera.setTarget(new BABYLON.Vector3(0, 1.5, 0));
      }
      alert("Runtime reset.");
    }
  } catch (error) {
    alert("Runtime reset failed: " + error.message);
  }
}

document.addEventListener('DOMContentLoaded', () => document.body.classList.add('native-window-stability','smooth-window-mode','always-smooth-window'));

function enableSmoothWindowMode() {
  document.body.classList.add("smooth-window-mode", "always-smooth-window");
  document.body.classList.remove("restore-visual-effects");
  try { localStorage.setItem("gameforgeSmoothWindowMode", "true"); } catch(e) {}
  alert("Smooth Window Mode is now built into the app by default.");
}

function disableSmoothWindowMode() {
  // This now only restores heavier visual effects if the user wants them.
  document.body.classList.remove("smooth-window-mode");
  document.body.classList.add("restore-visual-effects");
  try { localStorage.setItem("gameforgeSmoothWindowMode", "false"); } catch(e) {}
  alert("Visual effects restored. If dragging feels slow again, restart or use Smooth Mode.");
}

function restoreWindowPerformancePreference() {
  // v3.3.1: Smooth mode defaults ON, no manual enable required.
  try {
    const pref = localStorage.getItem("gameforgeSmoothWindowMode");
    if (pref === "false") {
      document.body.classList.remove("smooth-window-mode");
      document.body.classList.add("restore-visual-effects");
    } else {
      document.body.classList.add("smooth-window-mode", "always-smooth-window");
      document.body.classList.remove("restore-visual-effects");
    }
  } catch(e) {
    document.body.classList.add("smooth-window-mode", "always-smooth-window");
  }
}

let gfEngine;
let localAiSettings = {
  localProvider: "ollama",
  localEndpoint: "http://localhost:11434/api/generate",
  localModel: "llama3.1:8b",
  temperature: 0.35,
  autoFallback: true
};

let projectState = {
  name: "Dead Hollow Prototype",
  prompt: "",
  mode: "Studio",
  size: "Playable Demo",
  graphics: "Realistic",
  perspective: "First Person",
  designDoc: "",
  forgeReport: "",
  logicScript: "",
  assetPlan: "",
  materialPlan: "",
  testReportMarkdown: "",
  components: {},
  scene: {}
};

function $(id) { return document.getElementById(id); }

function setPanel(panelId) {
  document.querySelectorAll(".nav").forEach(btn => btn.classList.toggle("active", btn.dataset.panel === panelId));
  document.querySelectorAll(".panel").forEach(panel => panel.classList.toggle("active-panel", panel.id === panelId));
  const map = {
    dashboard: ["Dashboard", "Create, edit, generate, test, and play inside GameForge AI Engine."],
    forgewizard: ["Forge Wizard", "Run the full game creation pipeline from one guided screen."],
    hypergame: ["Hypergame Mode", "Generate fast playable drafts from templates."],
    forge: ["Forge Mode", "Run staged offline generation."],
    studio: ["Studio Editor", "Edit the world and test play inside the standalone engine."],
    components: ["Components", "Attach gameplay-style component data to objects."],
    terrain: ["Terrain Tools", "Apply terrain presets and scene foundations."],
    realism: ["Realism Pack", "Generate offline realistic-looking environments and asset plans."],
    materials: ["Materials", "Apply prototype materials and plan future PBR assets."],
    systems: ["Gameplay Systems", "Generate offline gameplay logic templates."],
    assets: ["Asset Forge", "Plan assets for characters, weapons, maps, UI and audio."],
    assetpacks: ["Asset Pack Manager", "Download, import, scan and use game asset packs."],
    characters: ["Characters", "Import and assign realistic GLB/GLTF human and zombie models."],
    curatedassets: ["Curated CC0 Assets", "Download only licence-safe CC0/Public Domain assets from approved sources."],
    aicost: ["AI Cost Estimator", "Estimate AI/API generation cost before running future Hybrid AI jobs."],
    hybridai: ["Hybrid AI", "Connect OpenAI with cost approval and Local AI fallback."],
    ai3dassets: ["AI 3D Assets", "Plan, generate/import and licence-check realistic GLB/GLTF assets."],
    free3d: ["Free 3D Generator", "Generate original procedural prototype assets for free."],
    testing: ["Test Agent", "Run checks against the generated build."],
    playable: ["Playable Prototype", "Generate and test a more complete playable survival draft."],
    audio: ["Audio Pack", "Enable and test offline placeholder audio."],
    localmedia: ["Local Media", "Generate local textures, icons and sounds automatically."],
    websounds: ["Web Sounds", "Search and import licence-aware web sound previews."],
    legalsounds: ["Legal Sound Finder", "Find sounds with licence filtering and credits metadata."],
    licenceaudit: ["Licence Auditor", "Gather licence data, classify risk and create credits files."],
    buildsystem: ["Build System", "Validate and export a 1920x1080 playable game draft."],
    nativeexport: ["Native EXE Export", "Prepare a standalone Windows .exe package workflow."],
    autonomous: ["Autonomous AI", "Generate a playable game draft from your prompt using local AI."],
    visualtarget: ["Visual Target", "Create a visual target prompt and asset matching plan."],
    internalmesh: ["Internal Mesh", "Create procedural mesh recipes, PBR materials and export plans."],
    gameintel: ["Game Intelligence", "Gather game brief, asset, material, audio and validation plans."],
    oneclickforge: ["One-Click Forge", "Run the full prompt-to-playable prototype pipeline."],
    playtestexport: ["Playtest / Export", "Launch a game-only playtest window and export demo package."],
    completegame: ["Complete Game EXE", "Export a full-game prototype package with EXE build script."],
    photoreal: ["Startup Hotfix v3.3.1", "Apply cinematic lighting, PBR surfaces and detail passes."],
    autoassets: ["Auto Assets", "Automatically scan, match and assign GLB/GLTF models."],
    modelgatherer: ["Model Gatherer", "Gather approved GLB/GLTF models and import them automatically."],
    animationimporter: ["Animations", "Assign rigged/procedural character animation controllers."],
    animationgatherer: ["Animation Gatherer", "Gather approved rigged characters and animation packs."],
    settings: ["Settings", "Configure offline/AI generation settings."]
  };
  $("pageTitle").textContent = map[panelId][0];
  $("pageSubtitle").textContent = map[panelId][1];
  if (panelId === "studio" && gfEngine?.engine) setTimeout(() => gfEngine.engine.resize(), 100);
}


function logAutonomous(message) {
  const box = $("autonomousLog");
  if (!box) return;
  const time = new Date().toLocaleTimeString();
  box.value += `[${time}] ${message}\n`;
  box.scrollTop = box.scrollHeight;
}

function getLocalAiSettingsFromInputs() {
  return {
    localProvider: $("localProvider")?.value || "ollama",
    localEndpoint: $("localEndpoint")?.value || "http://localhost:11434/api/generate",
    localModel: $("localModel")?.value || "llama3.1:8b",
    temperature: Number($("localTemperature")?.value || 0.35),
    autoFallback: true
  };
}

function setLocalAiInputs(settings) {
  if (!settings) return;
  if ($("localProvider")) $("localProvider").value = settings.localProvider || "ollama";
  if ($("localEndpoint")) $("localEndpoint").value = settings.localEndpoint || "http://localhost:11434/api/generate";
  if ($("localModel")) $("localModel").value = settings.localModel || "llama3.1:8b";
  if ($("localTemperature")) $("localTemperature").value = settings.temperature ?? 0.35;
}

async function loadLocalAiSettings() {
  if (!window.gameforgeAPI?.loadSettings) return;
  const result = await window.gameforgeAPI.loadSettings();
  if (result.ok) {
    localAiSettings = { ...localAiSettings, ...result.settings };
    setLocalAiInputs(localAiSettings);
  }
}

async function saveLocalAiSettings() {
  localAiSettings = getLocalAiSettingsFromInputs();
  const result = await window.gameforgeAPI.saveSettings(localAiSettings);
  if (result.ok) {
    localAiSettings = result.settings;
    setLocalAiInputs(localAiSettings);
    alert("Autonomous Local AI settings saved.");
  }
}

async function testLocalAi() {
  localAiSettings = getLocalAiSettingsFromInputs();
  logAutonomous("Testing local AI connection...");
  const result = await window.gameforgeAPI.testLocalAI(localAiSettings);
  if (result.ok) logAutonomous("Local AI connected: " + result.response);
  else logAutonomous("Local AI connection failed: " + result.error);
}


function renderAssetLibrary(library) {
  const list = $("assetLibraryList");
  const log = $("assetLibraryLog");
  if (!list || !log) return;

  const assets = library?.assets || [];
  const counts = {};
  assets.forEach(a => counts[a.type] = (counts[a.type] || 0) + 1);

  log.value = `Asset Library Root:\n${library?.root || "Not scanned yet"}\n\nCounts:\n${JSON.stringify(counts, null, 2)}\n\nUse imported/downloaded assets during generation to give Local AI more context.\n`;

  list.innerHTML = "";
  if (!assets.length) {
    list.innerHTML = "<div class='component-row'>No assets found yet. Import or download a pack, then scan.</div>";
    return;
  }

  assets.forEach(a => {
    const div = document.createElement("div");
    div.className = "asset-item";
    div.innerHTML = `<span class="asset-type">${a.type}</span><div><strong>${a.name}</strong><div class="asset-path">${a.relativePath}</div></div><span>${Math.round((a.sizeBytes || 0)/1024)} KB</span>`;
    list.appendChild(div);
  });

  renderModelReferences();
}


function renderModelReferences() {
  const el = $("modelReferenceList");
  if (!el || !window.GameForgeAssetManager) return;
  const models = GameForgeAssetManager.modelAssets();
  el.innerHTML = "";
  if (!models.length) {
    el.innerHTML = "<div class='component-row'>No GLB/GLTF models found. Import a .glb or .gltf file first.</div>";
    return;
  }
  models.forEach(asset => {
    const div = document.createElement("div");
    div.className = "asset-item";
    div.innerHTML = `
      <span class="asset-type">GLB</span>
      <div><strong>${asset.name}</strong><div class="asset-path">${asset.relativePath}</div></div>
      <div class="model-actions">
        <button data-place-model="${asset.relativePath}">Place</button>
        <button data-copy-model="${asset.relativePath}">Copy Ref</button>
      </div>`;
    el.appendChild(div);
  });

  el.querySelectorAll("[data-place-model]").forEach(btn => {
    btn.onclick = () => {
      const asset = GameForgeAssetManager.getByRelativePath(btn.dataset.placeModel);
      if (asset) {
        gfEngine.placeImportedModelAsset(asset, {
          name: asset.name.replace(/\\.[^.]+$/, ""),
          position: new BABYLON.Vector3(Math.random()*8-4, 0, Math.random()*8-4),
          scale: new BABYLON.Vector3(1,1,1),
          type: "imported_model"
        });
        setPanel("studio");
      }
    };
  });

  el.querySelectorAll("[data-copy-model]").forEach(btn => {
    const ref = btn.dataset.copyModel;
    btn.onclick = async () => {
      try {
        await navigator.clipboard.writeText(`"ImportedAsset": {"relativePath": "${ref}", "type": "model"}`);
        alert("Model reference copied.");
      } catch {
        alert(`Model reference:\\n${ref}`);
      }
    };
  });
}


async function scanAssetLibrary() {
  const result = await GameForgeAssetManager.scan();
  if (result.ok) renderAssetLibrary({ root: result.root, assets: result.assets });
  else if ($("assetLibraryLog")) $("assetLibraryLog").value = "Scan failed: " + result.error;
}

async function importAssetFiles() {
  const result = await GameForgeAssetManager.importFiles();
  if (result.ok) renderAssetLibrary(GameForgeAssetManager.library);
  else if (!result.canceled && $("assetLibraryLog")) $("assetLibraryLog").value = "Import failed: " + result.error;
}

async function downloadAssetPack() {
  const url = $("assetDownloadUrl").value.trim();
  const licenseNote = $("assetLicenseNote").value.trim();
  const log = $("assetLibraryLog");
  log.value = "Downloading asset pack/file...\n" + url;
  const result = await GameForgeAssetManager.download(url, licenseNote);
  if (result.ok) {
    renderAssetLibrary(GameForgeAssetManager.library);
    log.value += "\n\nDownloaded: " + result.asset.name;
  } else {
    log.value += "\n\nDownload failed: " + result.error;
  }
}

async function saveAssetManifest() {
  const manifest = GameForgeAssetManager.generateManifest(projectState.name + "_AssetManifest", $("assetLicenseNote")?.value || "");
  const result = await window.gameforgeAPI.saveAssetManifest(manifest);
  if (result.ok) alert("Asset manifest saved:\n" + result.path);
}

function buildAssetContextForAI() {
  const checkbox = $("useAssetPacks");
  const checkboxPanel = $("useAssetPacksPanel");
  const use = (checkbox && checkbox.checked) || (checkboxPanel && checkboxPanel.checked);
  if (!use) return "";
  const selected = GameForgeAssetManager.selectBestAssetsForPrompt(projectState.prompt);
  const counts = GameForgeAssetManager.summary();
  return `Detected asset library counts: ${JSON.stringify(counts)}.
Relevant imported/downloaded assets:
${selected.map(a => `- ${a.type}: ${a.name} (${a.relativePath})`).join("\\n") || "No highly relevant assets detected."}

When useful, add ImportedAsset component references like:
"components": {"ImportedAsset": {"relativePath": "models/example_tree.glb", "type": "model"}}

If no suitable imported asset exists, use procedural placeholders.`;
}




let approvedLegalSounds = [];





let wizardState = {
  mediaGenerated: false,
  assetsScanned: false,
  licenceAudited: false,
  buildValidated: false,
  nativeReady: false,
  hasWarnings: false
};

function wizardLog(message) {
  const box = $("wizardLog");
  if (!box) return;
  box.value += `[${new Date().toLocaleTimeString()}] ${message}\n`;
  box.scrollTop = box.scrollHeight;
}

function renderWizardSteps(activeIndex = -1, completed = []) {
  const el = $("wizardProgress");
  if (!el) return;
  el.innerHTML = "";
  GameForgeWizard.steps.forEach((step, i) => {
    const div = document.createElement("div");
    div.className = "stage";
    if (completed.includes(i)) div.classList.add("done");
    if (i === activeIndex) div.classList.add("running");
    div.textContent = `${completed.includes(i) ? "✓" : i === activeIndex ? "→" : "○"} ${step}`;
    el.appendChild(div);
  });
}

function updateProjectHealth() {
  updateProjectFromInputs();
  const health = GameForgeWizard.calculateHealth(projectState, projectState.scene, projectState.components, wizardState);
  const el = $("healthScoreCards");
  if (!el) return health;
  el.innerHTML = `
    <div class="health-card pass"><strong>Playable Score</strong><div class="big-score">${health.score}%</div></div>
    <div class="health-card ${health.commercialScore >= 70 ? "pass" : "warn"}"><strong>Commercial Readiness</strong><div class="big-score">${health.commercialScore}%</div></div>
    ${health.checks.map(c => `<div class="health-card ${c.ready ? "pass" : "warn"}"><strong>${c.ready ? "PASS" : "WARN"} — ${c.label}</strong><span>${c.ready ? "Ready" : "Needs attention"}</span></div>`).join("")}
  `;
  return health;
}

function runReadinessGate() {
  const health = updateProjectHealth();
  const blocking = health.checks.filter(c => !c.ready && ["playerSpawn","objective","build"].includes(c.key));
  const warnings = health.checks.filter(c => !c.ready && !["playerSpawn","objective","build"].includes(c.key));
  $("readinessGateLog").value = `Build Readiness Gate

Playable Score: ${health.score}%
Commercial Readiness: ${health.commercialScore}%

Blocking Issues:
${blocking.map(b => "- " + b.label).join("\n") || "- None"}

Warnings:
${warnings.map(w => "- " + w.label).join("\n") || "- None"}

Recommendation:
${blocking.length ? "Do not export yet. Run Auto-Repair or Make It Better first." : "Ready for prototype export. Still verify licences and QA before commercial use."}`;
  return { blocking, warnings, health };
}

async function runForgeWizardPipeline() {
  setPanel("forgewizard");
  $("wizardLog").value = "";
  $("readinessGateLog").value = "";
  wizardState = { mediaGenerated:false, assetsScanned:false, licenceAudited:false, buildValidated:false, nativeReady:false, hasWarnings:false };
  const completed = [];
  renderWizardSteps(0, completed);
  wizardLog("Starting one-click full game pipeline...");

  const preset = $("wizardPreset").value;
  const graphics = $("wizardGraphics").value;
  const length = $("wizardLength").value;
  const originalPrompt = $("gamePrompt").value;
  $("gamePrompt").value = GameForgeWizard.buildWizardPrompt(originalPrompt, preset, graphics, length);
  projectState.objective = GameForgeWizard.getPreset(preset).objective;

  async function step(index, label, fn) {
    renderWizardSteps(index, completed);
    wizardLog(label);
    try {
      const result = await fn();
      completed.push(index);
      renderWizardSteps(-1, completed);
      return result;
    } catch (error) {
      wizardState.hasWarnings = true;
      wizardLog("Warning/error: " + error.message);
      completed.push(index);
      return null;
    }
  }

  await step(0, "Reading prompt and preset...", async () => true);
  await step(1, "Expanded prompt with preset details.", async () => true);

  await step(2, "Running built-in GameForge generation pipeline...", async () => {
    await runDashboardBuiltInGeneration();
    setPanel("forgewizard");
  });

  if ($("wizardGenerateMedia").checked) {
    await step(3, "Generating local media...", async () => {
      await generateLocalMediaPack();
      wizardState.mediaGenerated = true;
    });
  } else completed.push(3);

  if ($("wizardScanAssets").checked) {
    await step(4, "Scanning asset library...", async () => {
      await scanAssetLibrary();
      wizardState.assetsScanned = true;
    });
  } else completed.push(4);

  if ($("wizardRunRepair").checked) {
    await step(5, "Running auto-repair pass...", async () => {
      updateProjectFromInputs();
      const repairs = GameForgeWizard.repairScene(gfEngine, projectState);
      $("logicScript").value = projectState.logicScript || $("logicScript").value;
      wizardLog(repairs.length ? repairs.join("\n") : "No repairs needed.");
      refreshHierarchy();
    });
  } else completed.push(5);

  if ($("wizardRunLicence").checked) {
    await step(6, "Running licence audit...", async () => {
      await runLicenceAudit();
      wizardState.licenceAudited = true;
      setPanel("forgewizard");
    });
  } else completed.push(6);

  if ($("wizardValidateBuild").checked) {
    await step(7, "Validating playable build...", async () => {
      await validatePlayableBuild();
      wizardState.buildValidated = true;
      setPanel("forgewizard");
    });
  } else completed.push(7);

  if ($("wizardExportNative").checked) {
    await step(8, "Exporting native package...", async () => {
      await exportNativePackage();
      wizardState.nativeReady = true;
      setPanel("forgewizard");
    });
  } else {
    await step(8, "Preparing native package plan without export...", async () => {
      wizardState.nativeReady = false;
    });
  }

  await step(9, "Creating project health report...", async () => {
    const health = updateProjectHealth();
    $("readinessGateLog").value = GameForgeWizard.formatHealthReport(health);
  });

  wizardLog("Pipeline complete.");
  runReadinessGate();
}

async function makeCurrentGameBetter() {
  setPanel("forgewizard");
  $("wizardLog").value = "";
  wizardLog("Running Make It Better pass...");
  updateProjectFromInputs();

  const repairs = GameForgeWizard.repairScene(gfEngine, projectState);
  wizardLog(repairs.length ? repairs.join("\n") : "Core systems already present.");

  if ($("wizardGenerateMedia")?.checked !== false) {
    wizardLog("Refreshing generated media...");
    await generateLocalMediaPack();
    wizardState.mediaGenerated = true;
  }

  wizardLog("Improving world density...");
  if (gfEngine.generateBushlandArea) gfEngine.generateBushlandArea();
  if (gfEngine.addLootCrate) for (let i=0; i<3; i++) gfEngine.addLootCrate();
  if (gfEngine.spawnEnemyWave) gfEngine.spawnEnemyWave(3);

  updateProjectFromInputs();
  updateProjectHealth();
  runReadinessGate();
  wizardLog("Make It Better pass complete.");
}






async function loadHybridAISettings() {
  const result = await GameForgeHybridAI.loadSettings();
  const log = $("hybridAILog");
  if (result.ok) {
    $("hybridMode").value = result.settings.aiMode || "hybrid";
    $("openaiModel").value = result.settings.openaiModel || "gpt-5.4-mini";
    $("advancedModel").value = result.settings.advancedModel || "gpt-5.5";
    $("hybridUseCostGate").checked = result.settings.useCostGate !== false;
    $("hybridMaxUsd").value = result.settings.maxApprovedUsd || 25;
    if (result.settings.apiKeySaved) $("openaiApiKey").placeholder = "API key saved locally";
    if (log) log.value = `Hybrid AI settings loaded.\nAPI key saved: ${result.settings.apiKeySaved ? "Yes" : "No"}\nMode: ${result.settings.aiMode}`;
  } else if (log) {
    log.value = "Failed to load settings: " + result.error;
  }
}

async function saveHybridAISettings() {
  const result = await GameForgeHybridAI.saveSettings({
    aiMode: $("hybridMode").value,
    openaiApiKey: $("openaiApiKey").value.trim(),
    openaiModel: $("openaiModel").value,
    advancedModel: $("advancedModel").value,
    useCostGate: $("hybridUseCostGate").checked,
    maxApprovedUsd: Number($("hybridMaxUsd").value || 25)
  });
  $("hybridAILog").value = result.ok
    ? `Settings saved.\nAPI key saved: ${result.settings.apiKeySaved ? "Yes" : "No"}\nMode: ${result.settings.aiMode}\nModel: ${result.settings.openaiModel}`
    : "Save failed: " + result.error;
  if (result.ok && $("openaiApiKey").value) $("openaiApiKey").value = "";
}

async function testHybridConnection() {
  $("hybridAILog").value = "Testing OpenAI connection...\n";
  const result = await GameForgeHybridAI.testConnection($("openaiApiKey").value.trim());
  $("hybridAILog").value += result.ok ? result.message : "Connection failed: " + result.error;
}

function approveCurrentEstimateForHybrid() {
  if (!lastAICostEstimate) estimateAICost();
  const approval = GameForgeHybridAI.approveEstimate(lastAICostEstimate);
  $("hybridAILog").value = `Cost estimate approved for Hybrid AI run.

Approved estimate: $${approval.totalUsd.toFixed(2)} USD
Profile: ${approval.profileLabel}
Approved at: ${approval.approvedAt}

You can now click Generate With Hybrid AI.`;
}

async function generateWithHybridAI() {
  updateProjectFromInputs();
  await saveHybridAISettings();
  const log = $("hybridAILog");
  log.value += "\n\nPreparing Hybrid AI generation...\n";

  const contexts = [];
  if (window.GameForgeAppPerformanceMode) contexts.push(GameForgeAppPerformanceMode.contextForHybridAI());
  if (window.GameForgeCuratedDownloader) contexts.push(GameForgeCuratedDownloader.contextForLocalAI());
  if (window.GameForgeCharacterImporter) contexts.push(await GameForgeCharacterImporter.contextForLocalAI(projectState.prompt));
  if (window.GameForgeMediaGenerator) contexts.push(GameForgeMediaGenerator.contextForAI());
  if (window.GameForgeAI3DAssetPipeline) contexts.push(GameForgeAI3DAssetPipeline.contextForHybridAI());
  if (window.GameForgeFree3DGenerator) contexts.push(GameForgeFree3DGenerator.contextForHybridAI());
  if (window.GameForgeVisualTarget) contexts.push(GameForgeVisualTarget.contextForHybridAI());
  if (window.GameForgeInternalMeshGenerator) contexts.push(GameForgeInternalMeshGenerator.contextForHybridAI());
  if (window.GameForgeGameIntelligence) contexts.push(GameForgeGameIntelligence.contextForHybridAI());
  if (window.GameForgePhotorealMode) contexts.push(GameForgePhotorealMode.contextForHybridAI());
  if (window.GameForgeAutonomousRealismPipeline) contexts.push(GameForgeAutonomousRealismPipeline.contextForHybridAI());
  if (window.GameForgeApprovedAssetDownloader) contexts.push(GameForgeApprovedAssetDownloader.contextForHybridAI());
  if (window.GameForgeSelfAssetGenerator) contexts.push(GameForgeSelfAssetGenerator.contextForHybridAI());
  if (window.GameForgeAudioAssetGenerator) contexts.push(GameForgeAudioAssetGenerator.contextForHybridAI());
  if (window.GameForgePhotorealQualityGate) contexts.push(GameForgePhotorealQualityGate.contextForHybridAI());
  if (window.GameForgeUnrealExportPrep) contexts.push(GameForgeUnrealExportPrep.contextForHybridAI());
  if (window.GameForgeToolchainOrchestrator) contexts.push(GameForgeToolchainOrchestrator.contextForHybridAI());
  if (window.GameForgeMeshyFreeTestProvider) contexts.push(GameForgeMeshyFreeTestProvider.contextForHybridAI());
  if (window.GameForgeMeshyAutonomousAPI) contexts.push(GameForgeMeshyAutonomousAPI.contextForHybridAI());
  if (window.GameForgeLiveMeshySceneBuilder) contexts.push(GameForgeLiveMeshySceneBuilder.contextForHybridAI());
  if (window.GameForgeUnrealPhotorealExportBuilder) contexts.push(GameForgeUnrealPhotorealExportBuilder.contextForHybridAI());
  if (window.GameForgeUnrealAutoRepairRunner) contexts.push(GameForgeUnrealAutoRepairRunner.contextForHybridAI());
  if (window.GameForgeUnrealOneClickBuildRunner) contexts.push(GameForgeUnrealOneClickBuildRunner.contextForHybridAI());
  if (window.GameForgeGameStyleRatingManager) contexts.push(GameForgeGameStyleRatingManager.contextForHybridAI());
  if (window.GameForgeCopyrightSafeVisualIdentityGuard) contexts.push(GameForgeCopyrightSafeVisualIdentityGuard.contextForHybridAI());
  if (window.GameForgeParanormalDeviceJumpscareSystem) contexts.push(GameForgeParanormalDeviceJumpscareSystem.contextForHybridAI());
  if (window.GameForgeUnrealHorrorGameAssemblyBuilder) contexts.push(GameForgeUnrealHorrorGameAssemblyBuilder.contextForHybridAI());
  if (window.GameForgePhotorealScenePolishSystem) contexts.push(GameForgePhotorealScenePolishSystem.contextForHybridAI());
  if (window.GameForgeControlledFullAutomationRunner) contexts.push(GameForgeControlledFullAutomationRunner.contextForHybridAI());
  if (window.GameForgeGlobalHighEndRealismLock) contexts.push(GameForgeGlobalHighEndRealismLock.contextForHybridAI());
  if (window.GameForgeAutonomousBuildTestRepairLoop) contexts.push(GameForgeAutonomousBuildTestRepairLoop.contextForHybridAI());
  if (window.GameForgeRealisticStructureGenerator) contexts.push(GameForgeRealisticStructureGenerator.contextForHybridAI());
  if (window.GameForgeTrueStudioMode) contexts.push(GameForgeTrueStudioMode.contextForHybridAI());
  if (window.GameForgeHighEndAssetLibraryManager) contexts.push(GameForgeHighEndAssetLibraryManager.contextForHybridAI());
  if (window.GameForgeLegalVisualSourceResolver) contexts.push(GameForgeLegalVisualSourceResolver.contextForHybridAI());
  if (window.GameForgeLicensedVisualReferencePBRBuilder) contexts.push(GameForgeLicensedVisualReferencePBRBuilder.contextForHybridAI());
  if (window.GameForgeRequiredAppDetectorLauncher) contexts.push(GameForgeRequiredAppDetectorLauncher.contextForHybridAI());
  if (window.GameForgeApprovedToolDownloadManager) contexts.push(GameForgeApprovedToolDownloadManager.contextForHybridAI());
  if (window.GameForgeAutonomousFullGameBuilder) contexts.push(GameForgeAutonomousFullGameBuilder.contextForHybridAI());
  if (window.GameForgePhasmophobiaQualityHauntedGameCore) contexts.push(GameForgePhasmophobiaQualityHauntedGameCore.contextForHybridAI());
  if (window.GameForgePlayableEXEPackagingLaunchValidator) contexts.push(GameForgePlayableEXEPackagingLaunchValidator.contextForHybridAI());
  if (window.GameForgeScreenshotVisualScoringAutoRepair) contexts.push(GameForgeScreenshotVisualScoringAutoRepair.contextForHybridAI());
  if (window.GameForgeCinematicGenreSceneComposer) contexts.push(GameForgeCinematicGenreSceneComposer.contextForHybridAI());
  if (window.GameForgeHeroAssetChecklistEnforcer) contexts.push(GameForgeHeroAssetChecklistEnforcer.contextForHybridAI());
  if (window.GameForgeFirstGoQualityGate) contexts.push(GameForgeFirstGoQualityGate.contextForHybridAI());
  if (window.GameForgeExternalToolFailurePatternLibrary) contexts.push(GameForgeExternalToolFailurePatternLibrary.contextForHybridAI());
  if (window.GameForgeExternalToolDiagnosticsSelfRepairEngine) contexts.push(GameForgeExternalToolDiagnosticsSelfRepairEngine.contextForHybridAI());
  if (window.GameForgeAdvancedUnrealSceneBuilder) contexts.push(GameForgeAdvancedUnrealSceneBuilder.contextForHybridAI());
  if (window.GameForgeFullGameDesignDirector) contexts.push(GameForgeFullGameDesignDirector.contextForHybridAI());
  if (window.GameForgeGameplaySystemsBuilder) contexts.push(GameForgeGameplaySystemsBuilder.contextForHybridAI());
  if (window.GameForgeIntricateGameplaySystemsArchitectV12Bridge) contexts.push(GameForgeIntricateGameplaySystemsArchitectV12Bridge.contextForHybridAI());
  if (window.GameForgeAdvancedAIEnemyBehaviourBuilder) contexts.push(GameForgeAdvancedAIEnemyBehaviourBuilder.contextForHybridAI());
  if (window.GameForgeProceduralOpenWorldExpansionSystem) contexts.push(GameForgeProceduralOpenWorldExpansionSystem.contextForHybridAI());
  if (window.GameForgeMultiplayerOnlineSystemsBuilder) contexts.push(GameForgeMultiplayerOnlineSystemsBuilder.contextForHybridAI());
  if (window.GameForgePerformanceOptimisationHardwareScaling) contexts.push(GameForgePerformanceOptimisationHardwareScaling.contextForHybridAI());
  if (window.GameForgeCommercialReleaseReadinessSystem) contexts.push(GameForgeCommercialReleaseReadinessSystem.contextForHybridAI());
  if (window.GameForgePBRMaterialLibrary) contexts.push(GameForgePBRMaterialLibrary.contextForHybridAI());
  if (window.GameForgeCharacterCreatureRealismLibrary) contexts.push(GameForgeCharacterCreatureRealismLibrary.contextForHybridAI());
  if (window.GameForgeAnimationPackManager) contexts.push(GameForgeAnimationPackManager.contextForHybridAI());
  if (window.GameForgeEnvironmentKitManager) contexts.push(GameForgeEnvironmentKitManager.contextForHybridAI());
  if (window.GameForgeAudioSfxLibrary) contexts.push(GameForgeAudioSfxLibrary.contextForHybridAI());
  if (window.GameForgeLicenceCommercialTracker) contexts.push(GameForgeLicenceCommercialTracker.contextForHybridAI());
  if (window.GameForgeAssetQualityScorer) contexts.push(GameForgeAssetQualityScorer.contextForHybridAI());
  if (window.GameForgeAssetReplacementRules) contexts.push(GameForgeAssetReplacementRules.contextForHybridAI());
  if (window.GameForgeOfflineFallbackLibrary) contexts.push(GameForgeOfflineFallbackLibrary.contextForHybridAI());
  if (window.GameForgeAAAPhotorealEnforcementSystem) contexts.push(GameForgeAAAPhotorealEnforcementSystem.contextForHybridAI());
  if (window.GameForgeScannedAssetCharacterRealismConnector) contexts.push(GameForgeScannedAssetCharacterRealismConnector.contextForHybridAI());
  if (window.GameForgeAutoAssetImporter) contexts.push(GameForgeAutoAssetImporter.contextForHybridAI());
  if (window.GameForgeModelGatherer) contexts.push(GameForgeModelGatherer.contextForHybridAI());
  if (window.GameForgeAnimationImporter) contexts.push(GameForgeAnimationImporter.contextForHybridAI());
  if (window.GameForgeAnimationAssetGatherer) contexts.push(GameForgeAnimationAssetGatherer.contextForHybridAI());
  const context = contexts.join("\n\n");

  const result = await GameForgeHybridAI.generateGame(projectState, context);
  if (!result.ok) {
    log.value += `Hybrid AI failed or blocked: ${result.error}\n`;
    if (result.fallbackRecommended) {
      log.value += "\nFallback: use Local AI generation, save an API key, or approve the cost estimate.";
    }
    return;
  }

  log.value = GameForgeHybridAI.formatResult(result);

  if (result.scene && gfEngine?.applyGeneratedSceneData) {
    projectState.scene = result.scene;
    projectState.components = {};
    gfEngine.applyGeneratedSceneData(result.scene, window.GameForgeAssetManager?.library || null);
    refreshHierarchy();
    setPanel("studio");
  }

  if (result.result && $("forgeReport")) {
    $("forgeReport").value = JSON.stringify(result.result, null, 2);
  }
}

let lastAICostEstimate = null;

function estimateAICost() {
  const profile = $("costProfile").value;
  const quality = $("costQuality").value;
  const options = {
    includeRepairPasses: $("costRepairPasses").checked,
    includeReleaseReview: $("costReleaseReview").checked,
    includeAssetPlanning: $("costAssetPlanning").checked,
    batchMode: $("costBatchMode").checked
  };
  lastAICostEstimate = GameForgeAICostEstimator.estimate(profile, quality, options);
  $("aiCostLog").value = GameForgeAICostEstimator.formatEstimate(lastAICostEstimate);
  return lastAICostEstimate;
}

function approveEstimatedRun() {
  if (!lastAICostEstimate) estimateAICost();
  $("aiCostLog").value += `

APPROVED FOR FUTURE HYBRID AI RUN:
Estimated total: $${lastAICostEstimate.total.toFixed(2)} USD
Mode: ${lastAICostEstimate.profileLabel} / ${lastAICostEstimate.modelLabel}

This approval is stored only in the current app session. v3.3.1 can use this as a cost gate before calling a real AI API.`;
}

function showCostPolicy() {
  $("aiCostLog").value = `# GameForge AI Cost Policy

Purpose:
- Estimate likely AI/API cost before generation.
- Prevent accidental expensive runs.
- Let the user choose Cheap, Balanced or High Quality generation.
- Keep Local AI as free/offline fallback.

Recommended usage:
- Early idea testing: Cheap / Draft
- First playable demo: Balanced
- Code repair and release-readiness: High Quality
- Long overnight work: Batch mode where supported

Safety:
- Never run a paid cloud AI job without user approval.
- Show estimate before generation.
- Show actual usage after generation in v3.3.1.
- Allow monthly budget limits later.`;
}


async function generateCuratedPlan() {
  updateProjectFromInputs();
  const result = await GameForgeCuratedDownloader.createPlan(projectState);
  const log = $("curatedAssetLog");
  if (result.ok) {
    log.value = GameForgeCuratedDownloader.formatPlan(result.plan);
  } else {
    log.value = "Plan failed: " + result.error;
  }
}

async function checkCuratedAsset() {
  const asset = {
    url: $("curatedAssetUrl").value.trim(),
    sourcePage: $("curatedSourcePage").value.trim(),
    creator: $("curatedCreator").value.trim(),
    license: $("curatedLicenceText").value.trim(),
    licenceText: $("curatedLicenceText").value.trim()
  };
  const result = await GameForgeCuratedDownloader.checkAsset(asset);
  const log = $("curatedAssetLog");
  if (result.ok) {
    log.value = `Licence Gate Result

Decision: ${result.decision.decision}
Allowed: ${result.decision.allowed}
Reason: ${result.decision.reason}

Approved domain: ${result.decision.approvedDomain}
Has proof: ${result.decision.hasProof}

Licence:
${JSON.stringify(result.decision.licenceAudit, null, 2)}`;
  } else {
    log.value = "Check failed: " + result.error;
  }
}

function renderCuratedDownloads() {
  const el = $("curatedDownloadList");
  if (!el) return;
  const items = GameForgeCuratedDownloader.approvedDownloads || [];
  el.innerHTML = "";
  if (!items.length) {
    el.innerHTML = "<div class='component-row'>No approved curated downloads yet.</div>";
    return;
  }
  items.forEach(item => {
    const div = document.createElement("div");
    div.className = "asset-item";
    div.innerHTML = `<span class="asset-type">${item.type}</span><div><strong>${item.name}</strong><div class="asset-path">${item.libraryPath || item.relativePath}</div></div><span class="legal-badge">CC0 Gate</span>`;
    el.appendChild(div);
  });
}

async function downloadCuratedAsset() {
  const log = $("curatedAssetLog");
  log.value = "Running licence gate and downloading only if approved...\n";
  const result = await GameForgeCuratedDownloader.download({
    url: $("curatedAssetUrl").value.trim(),
    sourcePage: $("curatedSourcePage").value.trim(),
    creator: $("curatedCreator").value.trim(),
    licenceText: $("curatedLicenceText").value.trim(),
    license: $("curatedLicenceText").value.trim()
  });
  if (result.ok) {
    log.value += `\nApproved and downloaded:
${result.asset.name}

Saved to:
${result.asset.path}

Copied into GameForge Asset Library:
${result.asset.libraryPath}

Metadata:
${result.metadataPath}

Warning: verify licence/source before commercial release.`;
    renderCuratedDownloads();
    await scanAssetLibrary();
    if (window.GameForgeCharacterImporter) await GameForgeCharacterImporter.renderList();
  } else {
    log.value += "\nBlocked/failed: " + result.error;
  }
}

async function showCuratedRegistry() {
  const result = await GameForgeCuratedDownloader.registry();
  $("curatedAssetLog").value = result.ok ? JSON.stringify(result.registry, null, 2) : "Failed: " + result.error;
}


async function scanCharacterModels() {
  const models = await GameForgeCharacterImporter.renderList();
  const log = $("characterPlanLog");
  if (log) log.value = models.length
    ? `Detected ${models.length} character model(s):\n\n` + models.map(m => `- ${m.role}: ${m.name}\n  ${m.relativePath}`).join("\n")
    : "No character GLB/GLTF models detected yet. Import realistic models in Asset Pack Manager first.";
}

async function autoAssignCharacterModels() {
  const result = await GameForgeCharacterImporter.autoAssignToScene(gfEngine);
  $("characterPlanLog").value = result.message;
  refreshHierarchy();
}

async function generateCharacterPlan() {
  updateProjectFromInputs();
  $("characterPlanLog").value = await GameForgeCharacterImporter.generatePlan(projectState);
}


async function generateFullGamePackage() {
  const log = $("nativeExportLog");
  log.value = "Generating full game package workflow...\n";
  await generateCompletePlayableDraft();
  await validatePlayableBuild();
  await runLicenceAudit();
  updateProjectFromInputs();
  const result = await GameForgeNativeExporter.exportNativePackage(projectState);
  if (result.ok) {
    log.value += `\nNative package created:\n${result.path}\n\n${result.instructions}\n\nNext: open the folder and run build_windows.bat.`;
  } else {
    log.value += "\nNative package failed: " + result.error;
  }
}

async function validateNativePackage() {
  updateProjectFromInputs();
  const result = await GameForgeNativeExporter.validate(projectState);
  const log = $("nativeExportLog");
  if (!result.ok) {
    log.value = "Validation failed: " + result.error;
    return;
  }
  log.value = "Native Package Validation\n\n" + result.results.map(r => `${r.status}: ${r.name} — ${r.detail}`).join("\n");
}

async function exportNativePackage() {
  updateProjectFromInputs();
  GameForgeNativeExporter.settings.targetResolution = $("nativeResolution").value;
  const result = await GameForgeNativeExporter.exportNativePackage(projectState);
  const log = $("nativeExportLog");
  if (result.ok) {
    log.value = `Native package exported:\n${result.path}\n\n${result.instructions}`;
  } else {
    log.value = "Export failed: " + result.error;
  }
}

function showNativeChecklist() {
  $("nativeExportLog").value = GameForgeNativeExporter.checklist();
}


async function generateCompletePlayableDraft() {
  updateProjectFromInputs();
  $("buildSystemLog").value = "Generating complete playable draft...\n";
  if ($("autoGenerateMedia")) $("autoGenerateMedia").checked = true;
  await generateLocalMediaPack();
  await autonomousGenerate(true);
  if (gfEngine.generatePlayableSurvivalPrototype) gfEngine.generatePlayableSurvivalPrototype();
  projectState.objective = "Find supplies, survive enemies, and reach safety.";
  projectState.menuPlan = GameForgeBuildSystem.createMenuPlan(projectState);
  projectState.saveLoadPlan = GameForgeBuildSystem.createSaveLoadPlan(projectState);
  $("buildSystemLog").value += "\nComplete playable draft generated. Open Studio Editor and press Play.\nTarget: 1920x1080.";
  setPanel("studio");
}

async function validatePlayableBuild() {
  updateProjectFromInputs();
  GameForgeBuildSystem.settings.targetResolution = $("targetResolution").value;
  GameForgeBuildSystem.settings.quality = $("buildQuality").value;
  const result = await GameForgeBuildSystem.validate(projectState);
  const log = $("buildSystemLog");
  if (!result.ok) {
    log.value = "Validation failed: " + result.error;
    return;
  }
  log.value = "Playable Build Validation\n\n" + result.results.map(r => `${r.status}: ${r.name} — ${r.detail}`).join("\n");
}

async function exportPlayableBuildV06() {
  updateProjectFromInputs();
  GameForgeBuildSystem.settings.targetResolution = $("targetResolution").value;
  GameForgeBuildSystem.settings.quality = $("buildQuality").value;
  const validation = await GameForgeBuildSystem.validate(projectState);
  const result = await GameForgeBuildSystem.exportBuild(projectState);
  const log = $("buildSystemLog");
  if (!result.ok) {
    log.value = "Export failed: " + result.error;
    return;
  }
  log.value = `Playable build exported.

Path:
${result.path}

Target:
${JSON.stringify(result.buildSettings, null, 2)}

Validation:
${validation.ok ? validation.results.map(r => `${r.status}: ${r.name}`).join("\n") : "Validation unavailable"}`;
}

function generateMenuPlans() {
  updateProjectFromInputs();
  const menu = GameForgeBuildSystem.createMenuPlan(projectState);
  const save = GameForgeBuildSystem.createSaveLoadPlan(projectState);
  $("buildSystemLog").value = menu + "\n\n" + save;
}

function updatePauseOverlay(runtime) {
  const overlay = $("pauseMenuOverlay");
  if (!overlay || !runtime) return;
  overlay.classList.toggle("hidden", !runtime.paused);
}


async function runLicenceAudit() {
  const mode = $("licenceAuditMode").value;
  const manual = $("manualLicenceText").value;
  const log = $("licenceAuditLog");
  log.value = "Running licence audit...\n";
  await scanAssetLibrary();
  const result = await GameForgeLicenceAuditor.audit(mode, manual);
  if (!result.ok) {
    log.value += "\nAudit failed: " + result.error;
    return;
  }
  const s = result.report.summary;
  log.value = `Licence audit complete.

Mode: ${result.report.mode}

Summary:
- Total assets: ${s.totalAssets}
- Allowed: ${s.allowed}
- Blocked: ${s.blocked}
- High risk: ${s.highRisk}
- Medium risk: ${s.mediumRisk}
- Low risk: ${s.lowRisk}

Files:
- JSON report: ${result.reportPath}
- Markdown report: ${result.mdPath}
- Credits file: ${result.creditsPath}

Blocked assets should not be used until verified.`;
}

async function collectLicenceMetadata() {
  const log = $("licenceAuditLog");
  log.value = "Collecting known licence/credit metadata files...\n";
  const result = await GameForgeLicenceAuditor.collectMetadata();
  if (result.ok) {
    log.value += `Found ${result.records.length} metadata/licence files.\n\n` + result.records.map(r => `- ${r.file}: ${r.audit.normalised} (${r.audit.risk})`).join("\n");
  } else {
    log.value += "Failed: " + result.error;
  }
}

async function classifyManualLicence() {
  const text = $("manualLicenceText").value;
  const log = $("licenceAuditLog");
  const result = await GameForgeLicenceAuditor.classify(text);
  if (result.ok) {
    log.value = "Manual licence classification:\n\n" + JSON.stringify(result.audit, null, 2);
  } else {
    log.value = "Classification failed: " + result.error;
  }
}

function showAuditNote() {
  $("licenceAuditLog").value = `Licence Auditor checks:
- Licence text
- Stored metadata files
- Source/creator fields
- CC0/Public Domain detection
- Attribution detection
- NonCommercial blocking
- Unknown licence blocking
- Commercial-use risk
- Whether credits should be generated

It creates:
- licence_audit JSON
- LICENCE_AUDIT.md
- CREDITS_ALL_ASSETS.md

This is not legal advice. It is a safety workflow to help you avoid obvious licensing mistakes.`;
}


async function generateSoundEventQueries() {
  updateProjectFromInputs();
  const result = await GameForgeMediaGenerator.soundEventQueries(projectState.prompt);
  const log = $("legalSoundLog");
  if (result.ok) {
    log.value = "Required sound event queries:\n\n" + result.events.map(e => `${e.event}: ${e.query}`).join("\n");
    $("legalSoundQuery").value = result.events.map(e => e.query).join(" OR ");
  } else {
    log.value = "Failed to generate sound queries: " + result.error;
  }
}

function renderLegalSoundResults(results) {
  const el = $("legalSoundResults");
  if (!el) return;
  el.innerHTML = "";
  approvedLegalSounds = results || [];
  if (!approvedLegalSounds.length) {
    el.innerHTML = "<div class='component-row'>No approved legal sounds found yet.</div>";
    return;
  }
  approvedLegalSounds.forEach((sound, idx) => {
    const preview = sound.previews?.["preview-lq-mp3"] || sound.previews?.["preview-hq-mp3"] || "";
    const div = document.createElement("div");
    div.className = "web-sound-row";
    div.innerHTML = `
      <strong>${sound.name || "Unnamed sound"} <span class="legal-badge">${sound.normalisedLicence || "Allowed"}</span></strong>
      <div class="web-sound-meta">Creator: ${sound.username || "Unknown"} · Licence: ${sound.license || "Unknown"} · Duration: ${sound.duration || "?"}s</div>
      <div class="web-sound-actions">
        ${preview ? `<audio controls src="${preview}"></audio>` : ""}
        <button data-download-legal-sound="${idx}">Download Approved Sound</button>
      </div>`;
    el.appendChild(div);
  });
  el.querySelectorAll("[data-download-legal-sound]").forEach(btn => {
    btn.onclick = async () => {
      const sound = approvedLegalSounds[Number(btn.dataset.downloadLegalSound)];
      const result = await GameForgeMediaGenerator.downloadWebSound(sound);
      if (result.ok) alert("Downloaded approved sound:\n" + result.sound.path + "\n\nCredits metadata saved.");
      else alert("Download failed: " + result.error);
    };
  });
}

async function searchLegalSounds() {
  const query = $("legalSoundQuery").value.trim();
  const token = $("legalSoundToken").value.trim();
  const licenceMode = $("licenceMode").value;
  const log = $("legalSoundLog");
  log.value = "Searching web sounds and filtering licences...\n";
  const result = await GameForgeMediaGenerator.searchWebSounds(query, token);
  if (!result.ok) {
    log.value += "\nSearch failed: " + result.error;
    return;
  }
  const filtered = await GameForgeMediaGenerator.filterLegalSounds(result.results, licenceMode);
  if (!filtered.ok) {
    log.value += "\nFilter failed: " + filtered.error;
    return;
  }
  log.value += `\nRaw results: ${result.results.length}\nApproved results: ${filtered.results.length}\nLicence mode: ${licenceMode}\nBlocked: Unknown and NonCommercial licences.`;
  renderLegalSoundResults(filtered.results);
}

async function autoFindLegalSounds() {
  updateProjectFromInputs();
  const token = $("legalSoundToken").value.trim();
  const licenceMode = $("licenceMode").value;
  const log = $("legalSoundLog");
  log.value = "Auto-finding legal sounds for prompt...\n";
  const q = await GameForgeMediaGenerator.soundEventQueries(projectState.prompt);
  if (!q.ok) { log.value += "\nCould not create queries: " + q.error; return; }
  const allApproved = [];
  for (const event of q.events.slice(0, 5)) {
    log.value += `\nSearching ${event.event}: ${event.query}`;
    const result = await GameForgeMediaGenerator.searchWebSounds(event.query, token);
    if (!result.ok) { log.value += `\n  Failed: ${result.error}`; continue; }
    const filtered = await GameForgeMediaGenerator.filterLegalSounds(result.results, licenceMode);
    if (filtered.ok && filtered.results.length) {
      const best = filtered.results[0];
      best.eventName = event.event;
      allApproved.push(best);
      log.value += `\n  Approved: ${best.name} (${best.normalisedLicence})`;
    } else {
      log.value += "\n  No approved sounds found.";
    }
  }
  renderLegalSoundResults(allApproved);
  log.value += `\n\nAuto-find complete. Approved sounds: ${allApproved.length}`;
}

async function generateAudioCreditsFile() {
  const sounds = approvedLegalSounds.map(s => ({
    sourceName: s.name,
    creator: s.username,
    license: s.license,
    sourcePage: s.url
  }));
  const result = await GameForgeMediaGenerator.generateAudioCredits(sounds);
  if (result.ok) alert("Audio credits generated:\n" + result.path);
  else alert("Credits generation failed: " + result.error);
}


async function generateLocalMediaPack() {
  updateProjectFromInputs();
  const log = $("localMediaLog");
  if (log) log.value = "Generating local texture/icon/audio media pack...\n";
  const result = await GameForgeMediaGenerator.generatePack(projectState);
  if (result.ok) {
    if (log) log.value = GameForgeMediaGenerator.summary(result);
    if ($("audioPlan")) $("audioPlan").value += "\n\n# Local Media Generated\n" + GameForgeMediaGenerator.contextForAI(result);
  } else if (log) log.value += "\nGeneration failed: " + result.error;
  return result;
}
async function maybeAutoGenerateMedia() {
  const dash = $("autoGenerateMedia"), panel = $("autoGenerateMediaPanel");
  const should = (dash && dash.checked) || (panel && panel.checked);
  if (!should) return "";
  const result = await generateLocalMediaPack();
  return result.ok ? GameForgeMediaGenerator.contextForAI(result) : "";
}
async function suggestWebSoundKeywords() {
  updateProjectFromInputs();
  const result = await GameForgeMediaGenerator.suggestWebKeywords(projectState.prompt);
  if (result.ok && result.keywords?.length) $("webSoundQuery").value = result.keywords.join(" ");
}
function renderWebSoundResults(results) {
  const el = $("webSoundResults");
  if (!el) return;
  el.innerHTML = "";
  if (!results || !results.length) { el.innerHTML = "<div class='component-row'>No web sound results yet.</div>"; return; }
  results.forEach((sound, idx) => {
    const preview = sound.previews?.["preview-lq-mp3"] || sound.previews?.["preview-hq-mp3"] || "";
    const div = document.createElement("div");
    div.className = "web-sound-row";
    div.innerHTML = `<strong>${sound.name || "Unnamed sound"}</strong>
      <div class="web-sound-meta">Creator: ${sound.username || "Unknown"} · Licence: ${sound.license || "Unknown"} · Duration: ${sound.duration || "?"}s</div>
      <div class="web-sound-actions">${preview ? `<audio controls src="${preview}"></audio>` : ""}<button data-download-web-sound="${idx}">Download Preview</button></div>`;
    el.appendChild(div);
  });
  el.querySelectorAll("[data-download-web-sound]").forEach(btn => {
    btn.onclick = async () => {
      const sound = GameForgeMediaGenerator.webResults[Number(btn.dataset.downloadWebSound)];
      const result = await GameForgeMediaGenerator.downloadWebSound(sound);
      if (result.ok) alert("Downloaded web sound:\n" + result.sound.path + "\n\nRemember to check licence/creator metadata.");
      else alert("Download failed: " + result.error);
    };
  });
}
async function searchWebSounds() {
  const query = $("webSoundQuery").value.trim();
  const token = $("webSoundToken").value.trim();
  const el = $("webSoundResults");
  if (el) el.innerHTML = "<div class='component-row'>Searching web sounds...</div>";
  const result = await GameForgeMediaGenerator.searchWebSounds(query, token);
  if (result.ok) renderWebSoundResults(result.results);
  else if (el) el.innerHTML = `<div class='component-row'>Search failed: ${result.error}</div>`;
}

async function autonomousGenerate(makePlayable = true) {
  updateProjectFromInputs();
  localAiSettings = getLocalAiSettingsFromInputs();
  setPanel("autonomous");
  $("autonomousLog").value = "";
  logAutonomous("Starting autonomous local AI generation...");
  logAutonomous("Sending prompt to local model: " + localAiSettings.localModel);

  await scanAssetLibrary();
  const assetContext = buildAssetContextForAI();
  const mediaContext = await maybeAutoGenerateMedia();
  const characterContext = window.GameForgeCharacterImporter ? await GameForgeCharacterImporter.contextForLocalAI(projectState.prompt) : '';
  const curatedContext = window.GameForgeCuratedDownloader ? GameForgeCuratedDownloader.contextForLocalAI() : '';
  const prompt = GameForgeLocalAI.buildPrompt(projectState, assetContext + '\n\n' + mediaContext + '\n\n' + characterContext + '\n\n' + curatedContext);
  const result = await window.gameforgeAPI.generateWithLocalAI({ settings: localAiSettings, prompt });

  if (!result.ok) {
    logAutonomous("Local AI failed: " + result.error);
    logAutonomous("Using emergency offline template fallback.");
    forgeDraft();
    return;
  }

  try {
    logAutonomous("Local AI response received. Parsing and validating JSON...");
    const parsed = GameForgeLocalAI.parse(result.response);
    const validated = GameForgeLocalAI.validate(parsed);

    projectState.name = validated.gameTitle || projectState.name;
    projectState.designDoc = `# ${validated.gameTitle}\n\n${validated.summary}\n\n## Objective\n${validated.objective}\n`;
    projectState.logicScript = validated.gameplayLogic || Object.entries(validated.systems || {}).map(([k, v]) => `${k}: ${v}`).join("\n\n");
    projectState.assetPlan = validated.assetPlan || "";
    projectState.forgeReport = `# Autonomous Local AI Report\n\nGenerated by: ${localAiSettings.localModel}\n\n## Summary\n${validated.summary}\n\n## Objective\n${validated.objective}\n\n## Test Plan\n${validated.testPlan || "Run GameForge Test Agent."}\n`;
    if ($("audioPlan")) $("audioPlan").value = validated.audioPlan || $("audioPlan").value;

    $("gameName").value = projectState.name;
    $("logicScript").value = projectState.logicScript;
    $("assetPlan").value = projectState.assetPlan;
    $("forgeReport").value = projectState.forgeReport;

    logAutonomous("Building generated 3D scene...");
    gfEngine.applyGeneratedSceneData(validated, GameForgeAssetManager.library);

    if (makePlayable) {
      logAutonomous("Ensuring playable loop: enemies, loot, weapon pickup, audio and objective...");
      for (let i = 0; i < 3; i++) gfEngine.addLootCrate();
      for (let i = 0; i < 2; i++) gfEngine.addWeaponPickup();
      if (gfEngine.enableAudio) gfEngine.enableAudio();
    }

    $("currentBuildSummary").textContent = `${projectState.name} generated by Autonomous Local AI. Open Studio Editor and press Play.`;
    logAutonomous("Complete. Open Studio Editor and press Play.");
    setPanel("studio");
    refreshHierarchy();
  } catch (error) {
    logAutonomous("Could not use local AI JSON: " + error.message);
    logAutonomous("Using emergency offline fallback.");
    forgeDraft();
  }
}











async function runLiveMeshySceneBuilder() {
  if (window.GameForgeGameStyleRatingManager) GameForgeGameStyleRatingManager.applyToProjectState(projectState);
  if (!window.GameForgeLiveMeshySceneBuilder) return { ok: true, skipped: true, reason: "Game Style + Rating Selector not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("assets", "Running Game Style + Rating Selector");
    const result = await GameForgeLiveMeshySceneBuilder.run(projectState, gfEngine);
    projectState.liveMeshySceneBuilderRun = result;
    if ($("forgeReport") && window.GameForgeLiveMeshySceneBuilder.lastBlueprint) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeLiveMeshySceneBuilder.formatBlueprint();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Game Style + Rating Selector warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runMeshyAutonomousAPI() {
  if (!window.GameForgeMeshyAutonomousAPI) return { ok: true, skipped: true, reason: "Meshy API module not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("assets", "Preparing Meshy API / free-test asset queue");
    const result = await GameForgeMeshyAutonomousAPI.run(projectState, gfEngine);
    projectState.meshyAutonomousApiRun = result;
    if ($("forgeReport") && window.GameForgeMeshyAutonomousAPI.lastQueue) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeMeshyAutonomousAPI.formatQueue();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Meshy API warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runMeshyFreeTestProvider() {
  if (!window.GameForgeMeshyFreeTestProvider) return { ok: true, skipped: true, reason: "Meshy provider not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("assets", "Preparing Meshy free-test asset prompts");
    const result = await GameForgeMeshyFreeTestProvider.runAutonomous(projectState, gfEngine);
    projectState.meshyFreeTestRun = result;
    if ($("forgeReport") && window.GameForgeMeshyFreeTestProvider.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeMeshyFreeTestProvider.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Meshy free-test provider warning:", error.message);
    return { ok: false, warning: error.message };
  }
}



async function runUnrealAutoRepairRunner() {
  if (!window.GameForgeUnrealAutoRepairRunner) return { ok: true, skipped: true, reason: "Game Style + Rating Selector not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("export", "Running auto repair and validation");
    const result = await GameForgeUnrealAutoRepairRunner.run(projectState);
    projectState.unrealAutoRepairRunnerRun = result;
    if ($("forgeReport") && result) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeUnrealAutoRepairRunner.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Auto repair runner warning:", error.message);
    return { ok: false, warning: error.message };
  }
}



async function runParanormalDeviceJumpscareSystem() {
  if (!window.GameForgeParanormalDeviceJumpscareSystem) return { ok: true, skipped: true, reason: "Paranormal jump scare system not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("gameplay", "Building paranormal devices and triggered jump scares");
    const result = await GameForgeParanormalDeviceJumpscareSystem.run(projectState);
    projectState.paranormalDeviceJumpscareRun = result;
    if ($("forgeReport") && window.GameForgeParanormalDeviceJumpscareSystem.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeParanormalDeviceJumpscareSystem.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Paranormal jump scare warning:", error.message);
    return { ok: false, warning: error.message };
  }
}


async function runUnrealHorrorGameAssemblyBuilder() {
  if (!window.GameForgeUnrealHorrorGameAssemblyBuilder) return { ok: true, skipped: true, reason: "Unreal horror assembly builder not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("gameplay", "Assembling Unreal first-person horror gameplay");
    const result = await GameForgeUnrealHorrorGameAssemblyBuilder.run(projectState);
    projectState.unrealHorrorAssemblyRun = result;
    if ($("forgeReport") && window.GameForgeUnrealHorrorGameAssemblyBuilder.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeUnrealHorrorGameAssemblyBuilder.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Unreal horror assembly warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runPhotorealScenePolishSystem() {
  if (!window.GameForgePhotorealScenePolishSystem) return { ok: true, skipped: true, reason: "Photoreal scene polish system not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("lighting", "Applying photoreal scene polish");
    const result = await GameForgePhotorealScenePolishSystem.run(projectState);
    projectState.photorealScenePolishRun = result;
    if ($("forgeReport") && window.GameForgePhotorealScenePolishSystem.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgePhotorealScenePolishSystem.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Photoreal scene polish warning:", error.message);
    return { ok: false, warning: error.message };
  }
}




async function runGlobalHighEndRealismLock() {
  if (!window.GameForgeGlobalHighEndRealismLock) return { ok: true, skipped: true, reason: "Global realism lock not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("prompt", "Applying global high-end realism lock");
    const result = await GameForgeGlobalHighEndRealismLock.run(projectState);
    projectState.globalHighEndRealismLockRun = result;
    if ($("forgeReport") && window.GameForgeGlobalHighEndRealismLock.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeGlobalHighEndRealismLock.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Global high-end realism lock warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runAAAPhotorealEnforcementSystem() {
  if (!window.GameForgeAAAPhotorealEnforcementSystem) return { ok: true, skipped: true, reason: "AAA photoreal enforcement system not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("lighting", "Running AAA photoreal enforcement gate");
    const result = await GameForgeAAAPhotorealEnforcementSystem.run(projectState);
    projectState.aaaPhotorealEnforcementRun = result;
    if ($("forgeReport") && window.GameForgeAAAPhotorealEnforcementSystem.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeAAAPhotorealEnforcementSystem.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] AAA photoreal enforcement warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runScannedAssetCharacterRealismConnector() {
  if (!window.GameForgeScannedAssetCharacterRealismConnector) return { ok: true, skipped: true, reason: "Scanned asset/character connector not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("assets", "Preparing scanned assets and character realism path");
    const result = await GameForgeScannedAssetCharacterRealismConnector.run(projectState);
    projectState.scannedAssetCharacterRealismRun = result;
    if ($("forgeReport") && window.GameForgeScannedAssetCharacterRealismConnector.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeScannedAssetCharacterRealismConnector.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Scanned asset/character connector warning:", error.message);
    return { ok: false, warning: error.message };
  }
}





async function runLicensedVisualReferencePBRBuilder() {
  // GF66_runLicensedVisualReferencePBRBuilder
  if(window.GameForgeFlawlessGenerationProcessController) GameForgeFlawlessGenerationProcessController.setStep("materials","running","runLicensedVisualReferencePBRBuilder started");

  if (!window.GameForgeLicensedVisualReferencePBRBuilder) return { ok: true, skipped: true, reason: "Licensed visual reference/PBR builder not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("materials", "Building legal visual references and PBR materials");
    const result = await GameForgeLicensedVisualReferencePBRBuilder.run(projectState);
    projectState.licensedVisualReferencePBRBuilderRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeLicensedVisualReferencePBRBuilder.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Licensed visual reference/PBR warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runHighEndAssetLibraryManager() {
  // GF66_runHighEndAssetLibraryManager
  if(window.GameForgeFlawlessGenerationProcessController) GameForgeFlawlessGenerationProcessController.setStep("assets","running","runHighEndAssetLibraryManager started");

  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("assets", "Resolving high-end asset library sources");

    const planners = [
      window.GameForgePBRMaterialLibrary,
      window.GameForgeCharacterCreatureRealismLibrary,
      window.GameForgeAnimationPackManager,
      window.GameForgeEnvironmentKitManager,
      window.GameForgeAudioSfxLibrary,
      window.GameForgeLicenceCommercialTracker,
      window.GameForgeAssetQualityScorer,
      window.GameForgeAssetReplacementRules,
      window.GameForgeOfflineFallbackLibrary
    ];

    for (const planner of planners) {
      if (planner?.createPlan) planner.createPlan(projectState);
    }

    if (!window.GameForgeHighEndAssetLibraryManager) return { ok: true, skipped: true, reason: "High-end asset library manager not loaded" };

    const result = await GameForgeHighEndAssetLibraryManager.run(projectState);
    projectState.highEndAssetLibraryManagerRun = result;

    if ($("forgeReport") && window.GameForgeHighEndAssetLibraryManager.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeHighEndAssetLibraryManager.formatPlan();
    }

    return result;
  } catch (error) {
    console.warn("[GameForge] High-end asset library warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runRealisticStructureGenerator() {
  if (!window.GameForgeRealisticStructureGenerator) return { ok: true, skipped: true, reason: "Realistic structure generator not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("structures", "Generating realistic structures");
    const result = await GameForgeRealisticStructureGenerator.run(projectState);
    projectState.realisticStructureGeneratorRun = result;
    if ($("forgeReport") && window.GameForgeRealisticStructureGenerator.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeRealisticStructureGenerator.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Realistic structure generator warning:", error.message);
    return { ok: false, warning: error.message };
  }
}



async function runIntricateGameplaySystemsArchitectV12Bridge() {
  if (!window.GameForgeIntricateGameplaySystemsArchitectV12Bridge) return { ok: true, skipped: true, reason: "Intricate gameplay systems architect bridge not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("gameplay", "Applying intricate gameplay architecture");
    const result = await GameForgeIntricateGameplaySystemsArchitectV12Bridge.run(projectState);
    projectState.intricateGameplaySystemsArchitectV12BridgeRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeIntricateGameplaySystemsArchitectV12Bridge.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Intricate gameplay systems bridge warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runAdvancedFullGameSystems() {
  // GF66_runAdvancedFullGameSystems
  if(window.GameForgeFlawlessGenerationProcessController) GameForgeFlawlessGenerationProcessController.setStep("gameplay","running","runAdvancedFullGameSystems started");

  const systems = [
    window.GameForgeFullGameDesignDirector,
    window.GameForgeAdvancedUnrealSceneBuilder,
    window.GameForgeGameplaySystemsBuilder,
    window.GameForgeAdvancedAIEnemyBehaviourBuilder,
    window.GameForgeProceduralOpenWorldExpansionSystem,
    window.GameForgeMultiplayerOnlineSystemsBuilder,
    window.GameForgePerformanceOptimisationHardwareScaling,
    window.GameForgeCommercialReleaseReadinessSystem
  ];

  const results = [];
  for (const system of systems) {
    try {
      if (system?.run) results.push(await system.run(projectState));
    } catch (error) {
      console.warn("[GameForge] Advanced full game system warning:", error.message);
    }
  }
  return { ok: true, results };
}


async function runExternalToolDiagnosticsSelfRepairEngine(failureContext = null) {
  // GF66_runExternalToolDiagnosticsSelfRepairEngine
  if(window.GameForgeFlawlessGenerationProcessController) GameForgeFlawlessGenerationProcessController.setStep("diagnostics","running","runExternalToolDiagnosticsSelfRepairEngine started");

  if (!window.GameForgeExternalToolDiagnosticsSelfRepairEngine) return { ok: true, skipped: true, reason: "External diagnostics engine not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("diagnostics", "Running external tool diagnostics");
    const result = await GameForgeExternalToolDiagnosticsSelfRepairEngine.run(projectState, failureContext);
    projectState.externalToolDiagnosticsSelfRepairRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeExternalToolDiagnosticsSelfRepairEngine.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] External diagnostics warning:", error.message);
    return { ok: false, warning: error.message };
  }
}



async function runCinematicGenreSceneComposer() {
  // GF66_runCinematicGenreSceneComposer
  if(window.GameForgeFlawlessGenerationProcessController) GameForgeFlawlessGenerationProcessController.setStep("composition","running","runCinematicGenreSceneComposer started");

  if (!window.GameForgeCinematicGenreSceneComposer) return { ok: true, skipped: true, reason: "Cinematic Genre Scene Composer not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("composition", "Composing cinematic genre scene");
    const result = await GameForgeCinematicGenreSceneComposer.run(projectState);
    projectState.cinematicGenreSceneComposerRun = result;

    if (window.GameForgeHeroAssetChecklistEnforcer?.createPlan) {
      GameForgeHeroAssetChecklistEnforcer.createPlan(projectState);
    }

    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeCinematicGenreSceneComposer.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Cinematic Genre Scene Composer warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runFirstGoQualityGate() {
  if (!window.GameForgeFirstGoQualityGate) return { ok: true, skipped: true, reason: "First-Go Quality Gate not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("first-go-quality", "Checking first-go quality");
    const result = await GameForgeFirstGoQualityGate.run(projectState);
    projectState.firstGoQualityGateRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeFirstGoQualityGate.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] First-Go Quality Gate warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runPhasmophobiaQualityHauntedGameCore() {
  if (!window.GameForgePhasmophobiaQualityHauntedGameCore) return { ok: true, skipped: true, reason: "Phasmophobia-quality haunted game core not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("template", "Preparing Phasmophobia-quality haunted game core");
    const result = await GameForgePhasmophobiaQualityHauntedGameCore.run(projectState);
    projectState.phasmophobiaQualityHauntedGameCoreRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgePhasmophobiaQualityHauntedGameCore.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Phasmophobia-quality core warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runScreenshotVisualScoringAutoRepair() {
  if (!window.GameForgeScreenshotVisualScoringAutoRepair) return { ok: true, skipped: true, reason: "Screenshot visual scoring not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("visual-scoring", "Scoring screenshots for realism");
    const result = await GameForgeScreenshotVisualScoringAutoRepair.run(projectState);
    projectState.screenshotVisualScoringAutoRepairRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeScreenshotVisualScoringAutoRepair.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Screenshot visual scoring warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runPlayableEXEPackagingLaunchValidator() {
  // GF66_runPlayableEXEPackagingLaunchValidator
  if(window.GameForgeFlawlessGenerationProcessController) GameForgeFlawlessGenerationProcessController.setStep("validate","running","runPlayableEXEPackagingLaunchValidator started");

  if (!window.GameForgePlayableEXEPackagingLaunchValidator) return { ok: true, skipped: true, reason: "Playable EXE validator not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("validation", "Validating playable EXE");
    const result = await GameForgePlayableEXEPackagingLaunchValidator.run(projectState);
    projectState.playableEXEPackagingLaunchValidatorRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgePlayableEXEPackagingLaunchValidator.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Playable EXE validation warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runAutonomousFullGameBuilder() {
  // GF66_runAutonomousFullGameBuilder
  if(window.GameForgeFlawlessGenerationProcessController) GameForgeFlawlessGenerationProcessController.setStep("unreal","running","runAutonomousFullGameBuilder started");

  if (!window.GameForgeAutonomousFullGameBuilder) return { ok: true, skipped: true, reason: "Autonomous Full Game Builder not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("studio", "Running autonomous full game builder");
    const result = await GameForgeAutonomousFullGameBuilder.run(projectState);
    projectState.autonomousFullGameBuilderRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeAutonomousFullGameBuilder.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Autonomous full game builder warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runTrueGameForgeStudioMode() {
  if (!window.GameForgeTrueStudioMode) return { ok: true, skipped: true, reason: "True Studio Mode not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("studio", "Running High-End Asset Library + Realism Source Manager");
    const result = await GameForgeTrueStudioMode.run(projectState);
    projectState.trueGameForgeStudioModeRun = result;
    if ($("forgeReport") && window.GameForgeTrueStudioMode.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeTrueStudioMode.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] True Studio Mode warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runAutonomousBuildTestRepairLoop() {
  if (!window.GameForgeAutonomousBuildTestRepairLoop) return { ok: true, skipped: true, reason: "Autonomous build-test-repair loop not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("testing", "Running autonomous build-test-repair loop");
    const result = await GameForgeAutonomousBuildTestRepairLoop.run(projectState);
    projectState.autonomousBuildTestRepairLoopRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeAutonomousBuildTestRepairLoop.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Autonomous build-test-repair warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runControlledFullAutomationRunner() {
  if (!window.GameForgeControlledFullAutomationRunner) return { ok: true, skipped: true, reason: "Controlled automation runner not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("export", "Running controlled full automation");
    const result = await GameForgeControlledFullAutomationRunner.run(projectState);
    projectState.controlledFullAutomationRunnerRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeControlledFullAutomationRunner.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Controlled automation warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runUnrealOneClickBuildRunner() {
  if (!window.GameForgeUnrealOneClickBuildRunner) return { ok: true, skipped: true, reason: "Unreal one-click build runner not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("export", "Running automated Unreal import + package build");
    const result = await GameForgeUnrealOneClickBuildRunner.run(projectState);
    projectState.unrealOneClickBuildRun = result;
    if ($("forgeReport") && result) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeUnrealOneClickBuildRunner.formatReport(result);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Unreal one-click build warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runUnrealPhotorealExportBuilder() {
  if (!window.GameForgeUnrealPhotorealExportBuilder) return { ok: true, skipped: true, reason: "Game Style + Rating Selector not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("export", "Preparing Unreal photoreal export package");
    const result = await GameForgeUnrealPhotorealExportBuilder.run(projectState);
    projectState.unrealPhotorealExportRun = result;
    if ($("forgeReport") && window.GameForgeUnrealPhotorealExportBuilder.lastPackage) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeUnrealPhotorealExportBuilder.formatPackage();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Unreal photoreal export warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runFreeLegalToolchainOrchestrator() {
  if (!window.GameForgeToolchainOrchestrator) return { ok: true, skipped: true, reason: "Toolchain orchestrator not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("export", "Running free/legal external toolchain orchestrator");
    const result = await GameForgeToolchainOrchestrator.runAutonomous(projectState, gfEngine);
    projectState.toolchainRun = result;
    if ($("forgeReport")) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeToolchainOrchestrator.formatDetection(result.detection);
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Toolchain orchestrator warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runPhotorealQualityGate() {
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("lighting", "Running photoreal quality gate");
    if (!window.GameForgePhotorealQualityGate) return { ok: true, skipped: true, reason: "Quality gate not loaded" };

    const result = GameForgePhotorealQualityGate.run(projectState, gfEngine);
    projectState.photorealQualityReport = result.report;

    if ($("forgeReport") && window.GameForgePhotorealQualityGate.lastReport) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgePhotorealQualityGate.format();
    }

    if (window.GameForgeUnrealExportPrep) {
      const prep = await GameForgeUnrealExportPrep.savePackage(projectState);
      projectState.unrealExportPrepResult = prep;
    }

    return result;
  } catch (error) {
    console.warn("[GameForge] Photoreal quality gate warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runAudioAssetGeneration() {
  if (!window.GameForgeAudioAssetGenerator) return { ok: true, skipped: true, reason: "Audio asset generator not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("gameplay", "Generating missing audio assets and binding sound events");
    const result = await GameForgeAudioAssetGenerator.runAutonomous(projectState);
    projectState.audioAssetRun = result;
    if ($("forgeReport") && window.GameForgeAudioAssetGenerator.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeAudioAssetGenerator.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Audio asset generation warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runSelfAssetGeneration() {
  if (!window.GameForgeSelfAssetGenerator) return { ok: true, skipped: true, reason: "Self-asset generator not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("assets", "Generating missing assets locally");
    const result = await GameForgeSelfAssetGenerator.runAutonomous(projectState, gfEngine);
    projectState.selfAssetRun = result;
    if ($("forgeReport") && window.GameForgeSelfAssetGenerator.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeSelfAssetGenerator.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Self-asset generation warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runApprovedAssetAcquisition() {
  if (!window.GameForgeApprovedAssetDownloader) return { ok: true, skipped: true, reason: "Approved asset downloader not loaded" };
  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("assets", "Acquiring approved assets and licence metadata");
    const result = await GameForgeApprovedAssetDownloader.runAutonomous(projectState, gfEngine);
    projectState.approvedAssetRun = result;
    if ($("forgeReport") && window.GameForgeApprovedAssetDownloader.lastPlan) {
      $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + GameForgeApprovedAssetDownloader.formatPlan();
    }
    return result;
  } catch (error) {
    console.warn("[GameForge] Approved asset acquisition warning:", error.message);
    return { ok: false, warning: error.message };
  }
}

async function runAutonomousRealismPrep() {
  if (!window.GameForgeAutonomousRealismPipeline) return { ok: true, skipped: true, reason: 'Pipeline module not loaded' };
  // v3.3.1: photoreal realism prep always runs for every generated game.

  const statusBits = [];
  try {
    await runGlobalHighEndRealismLock();
    await runCinematicGenreSceneComposer();
    await runPhasmophobiaQualityHauntedGameCore();
    await runRealisticStructureGenerator();
    await runHighEndAssetLibraryManager();
    await runLicensedVisualReferencePBRBuilder();
    await runIntricateGameplaySystemsArchitectV12Bridge();
    await runAdvancedFullGameSystems();
    await runApprovedAssetAcquisition();
    await runSelfAssetGeneration();
    await runAudioAssetGeneration();
    await runParanormalDeviceJumpscareSystem();
    await runUnrealHorrorGameAssemblyBuilder();
    await runPhotorealScenePolishSystem();
    await runAAAPhotorealEnforcementSystem();
    await runScannedAssetCharacterRealismConnector();
    await runPhotorealQualityGate();
    await runLiveMeshySceneBuilder();
    await runUnrealPhotorealExportBuilder();
    await runUnrealAutoRepairRunner();
    await runFreeLegalToolchainOrchestrator();
    await runUnrealOneClickBuildRunner();
    await runControlledFullAutomationRunner();
    await runScreenshotVisualScoringAutoRepair();
    await runFirstGoQualityGate();
    await runAutonomousBuildTestRepairLoop();
    await runTrueGameForgeStudioMode();
    await runExternalToolDiagnosticsSelfRepairEngine();
    await runAutonomousFullGameBuilder();
    await runPlayableEXEPackagingLaunchValidator();
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("assets", "Creating asset/material/HDRI/animation plan");
    const planResult = await GameForgeAutonomousRealismPipeline.autoRun(projectState, gfEngine);
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("lighting", "Applying photoreal lighting and detail pass");
    if (planResult?.ok && planResult.plan) {
      projectState.autonomousRealismPlan = planResult.plan;
      if ($("forgeReport")) {
        const text = GameForgeAutonomousRealismPipeline.format(planResult.plan);
        $("forgeReport").value = ($("forgeReport").value ? $("forgeReport").value + "\n\n" : "") + text;
      }
      if ($("photorealModeLog")) {
        $("photorealModeLog").value += `${new Date().toLocaleTimeString()} — Photoreal always-on pipeline prepared.\n`;
        $("photorealModeLog").scrollTop = $("photorealModeLog").scrollHeight;
      }
      statusBits.push('autonomous realism prepared');
    }
  } catch (error) {
    console.warn('[GameForge] Autonomous realism prep warning:', error.message);
    statusBits.push('realism prep warning: ' + error.message);
  }
  return { ok: true, detail: statusBits.join('; ') };
}


function updateProjectFromInputs() {
  projectState.name = $("gameName").value.trim() || "Untitled Game";
  projectState.prompt = $("gamePrompt").value.trim();
  projectState.mode = $("buildMode").value;
  projectState.size = $("gameSize").value;
  projectState.graphics = $("graphicsTarget").value || "Photoreal Always-On";
  projectState.photorealAlwaysOn = true;
  projectState.highEndRealismRequired = true;
  if (window.GameForgeGlobalHighEndRealismLock) GameForgeGlobalHighEndRealismLock.createPlan(projectState);
  if (window.GameForgeCopyrightSafeVisualIdentityGuard) GameForgeCopyrightSafeVisualIdentityGuard.apply(projectState);
  projectState.autonomousFullPipeline = true;
  projectState.perspective = $("perspective").value;
  projectState.logicScript = $("logicScript").value || projectState.logicScript;
  projectState.assetPlan = $("assetPlan").value || projectState.assetPlan;
  projectState.materialPlan = $("materialPlan").value || projectState.materialPlan;
  projectState.forgeReport = $("forgeReport").value || projectState.forgeReport;
  projectState.scene = gfEngine ? gfEngine.serializeScene() : {};
  projectState.components = gfEngine ? gfEngine.objectComponents : {};
}

function refreshHierarchy() {
  if (!gfEngine) return;
  const hierarchy = $("sceneHierarchy");
  const scene = gfEngine.serializeScene();
  hierarchy.innerHTML = "";
  scene.objects.forEach(obj => {
    const item = document.createElement("div");
    item.className = "hierarchy-item" + (gfEngine.selectedMesh?.name === obj.name ? " selected" : "");
    item.textContent = `${obj.name} · ${obj.type}`;
    item.onclick = () => {
      const mesh = gfEngine.scene.getMeshByName(obj.name);
      if (mesh) gfEngine.selectMesh(mesh);
    };
    hierarchy.appendChild(item);
  });
  updateComponentPanel();
}

function selectedObjectText(obj) {
  return `
    <p><strong>${obj.name}</strong></p>
    <p>Type: ${obj.type}</p>
    <p>Material: ${obj.material}</p>
    <p>Position: ${obj.position.x}, ${obj.position.y}, ${obj.position.z}</p>
    <p>Scale: ${obj.scale.x}, ${obj.scale.y}, ${obj.scale.z}</p>
  `;
}

function updateComponentPanel() {
  const list = $("componentList");
  const jsonBox = $("componentJson");
  if (!gfEngine?.selectedMesh) {
    list.innerHTML = "<div class='component-row'>No object selected.</div>";
    jsonBox.value = JSON.stringify(gfEngine?.objectComponents || {}, null, 2);
    return;
  }
  const name = gfEngine.selectedMesh.name;
  const comps = gfEngine.objectComponents[name] || {};
  list.innerHTML = "";
  Object.entries(comps).forEach(([key, val]) => {
    const row = document.createElement("div");
    row.className = "component-row";
    row.innerHTML = `<strong>${key}</strong><pre>${JSON.stringify(val, null, 2)}</pre>`;
    list.appendChild(row);
  });
  if (!Object.keys(comps).length) list.innerHTML = "<div class='component-row'>No components yet.</div>";
  jsonBox.value = JSON.stringify(gfEngine.objectComponents, null, 2);
}


function updateHud(runtime) {
  const hud = $("gameHud");
  if (!hud || !runtime) return;
  $("healthBar").style.width = `${Math.max(0, runtime.health)}%`;
  $("staminaBar").style.width = `${Math.max(0, runtime.stamina)}%`;
  $("ammoHud").textContent = `Ammo: ${runtime.ammo} / ${runtime.reserveAmmo}`;
  $("scoreHud").textContent = `Score: ${runtime.score}`;
  $("objectiveHud").textContent = `Objective: ${runtime.objective}`;
  const prompt = $("interactionPrompt");
  if (runtime.interactTarget) {
    const comps = gfEngine.objectComponents[runtime.interactTarget.name] || {};
    const text = comps.Interactable?.prompt || "Interact";
    prompt.textContent = `Press E — ${text}`;
    prompt.style.display = "block";
  } else {
    prompt.style.display = "none";
  }
}

function generatePlayablePrototype() {
  updateProjectFromInputs();
  gfEngine.generatePlayableSurvivalPrototype();
  projectState.logicScript = OfflineGenerator.logic("full") + "\\n\\n" + `runtime PlayableSurvivalLoop {
  health: enabled;
  stamina: enabled;
  shooting: enabled;
  reload: enabled;
  enemy_chase_attack: enabled;
  loot_pickups: enabled;
  objective: find_5_supplies;
}`;
  $("logicScript").value = projectState.logicScript;
  $("playableNotes").value = OfflineGenerator.playableSystemNotes() + "\n\n" + OfflineGenerator.audioPlan();
  $("currentBuildSummary").textContent = `${projectState.name} generated with a playable survival loop. Open Studio Editor and press Play.`;
  setPanel("studio");
  refreshHierarchy();
}


function forgeDraft() {
  updateProjectFromInputs();
  projectState.designDoc = OfflineGenerator.designDoc(projectState);
  projectState.logicScript = OfflineGenerator.logic("full");
  projectState.assetPlan = OfflineGenerator.assetPlan(projectState);
  projectState.materialPlan = OfflineGenerator.materialPlan();
  projectState.forgeReport = OfflineGenerator.forgeReport(projectState);

  $("logicScript").value = projectState.logicScript;
  $("assetPlan").value = projectState.assetPlan;
  $("materialPlan").value = projectState.materialPlan;
  $("forgeReport").value = projectState.forgeReport;

  gfEngine.generateSurvivalMap();
  updateProjectFromInputs();
  $("currentBuildSummary").textContent = `${projectState.name} generated as a ${projectState.graphics} ${projectState.size} using offline GameForge templates.`;
  setPanel("studio");
  refreshHierarchy();
}

function runForgeStages() {
  const container = $("forgeStages");
  container.innerHTML = "";
  OfflineGenerator.forgeStages.forEach((stage, index) => {
    const row = document.createElement("div");
    row.className = "stage";
    row.textContent = `○ ${stage}`;
    container.appendChild(row);
    setTimeout(() => {
      row.classList.add("done");
      row.textContent = `✓ ${stage}`;
      if (index === OfflineGenerator.forgeStages.length - 1) {
        forgeDraft();
        setPanel("forge");
      }
    }, 130 * (index + 1));
  });
}

function generateHypergame() {
  const selected = $("hypergameTemplate").value;
  const desc = OfflineGenerator.hypergames[selected];
  $("gameName").value = selected.replace(/\s+/g, "_");
  $("gamePrompt").value = desc;
  $("buildMode").value = "Hypergame";
  $("gameSize").value = "Small Prototype";
  $("hypergameOutput").value = `# ${selected}\n\n${desc}\n\nGenerated as offline Hypergame template.\n\nIncludes:\n- player spawn\n- small playable map\n- basic gameplay logic\n- test checklist\n- asset plan`;
  forgeDraft();
  setPanel("hypergame");
}

function runTests() {
  updateProjectFromInputs();
  const results = OfflineGenerator.test(projectState.scene, projectState.logicScript || "", projectState.components || {});
  const report = $("testReport");
  report.innerHTML = "";
  projectState.testReportMarkdown = "# Test Report\n\n";
  results.forEach(([name, status, detail]) => {
    const cls = status === "Passed" ? "ok" : status === "Warning" ? "warn" : "bad";
    const row = document.createElement("div");
    row.className = "test-row";
    row.innerHTML = `<span class="dot ${cls}"></span><strong>${name}: ${status}</strong><p>${detail}</p>`;
    report.appendChild(row);
    projectState.testReportMarkdown += `- ${name}: ${status} — ${detail}\n`;
  });
}

function repairWarnings() {
  if (!gfEngine.serializeScene().objects.some(o => o.type === "enemy")) gfEngine.addEnemy();
  const required = ["weapons", "inventory", "crafting", "skills"];
  required.forEach(s => {
    if (!$("logicScript").value.includes(s === "skills" ? "SkillTree" : s[0].toUpperCase() + s.slice(1))) {
      $("logicScript").value += "\n\n" + OfflineGenerator.logic(s);
    }
  });
  refreshHierarchy();
  runTests();
}

async function saveProject() {
  updateProjectFromInputs();
  const result = await window.gameforgeAPI.saveProject(projectState);
  if (result.ok) alert(`Project saved:\n${result.path}`);
}

async function loadProject() {
  const result = await window.gameforgeAPI.loadProject();
  if (!result.ok) return;
  const data = result.data;
  projectState = { ...projectState, ...data };
  $("gameName").value = projectState.name || "";
  $("gamePrompt").value = projectState.prompt || "";
  $("buildMode").value = projectState.mode || "Studio";
  $("gameSize").value = projectState.size || "Playable Demo";
  $("graphicsTarget").value = projectState.graphics || "Realistic";
  $("perspective").value = projectState.perspective || "First Person";
  $("logicScript").value = projectState.logicScript || "";
  $("assetPlan").value = projectState.assetPlan || "";
  $("materialPlan").value = projectState.materialPlan || "";
  $("forgeReport").value = projectState.forgeReport || "";
  gfEngine.loadSceneData(projectState.scene, projectState.components);
  refreshHierarchy();
  setPanel("studio");
}

async function exportProject() {
  updateProjectFromInputs();
  const result = await window.gameforgeAPI.exportProject(projectState);
  if (result.ok) alert(`Project exported:\n${result.path}`);
}

async function exportPlayable() {
  updateProjectFromInputs();
  const result = await window.gameforgeAPI.exportPlayableDraft(projectState);
  if (result.ok) alert(`Playable draft exported:\n${result.path}`);
}

window.addEventListener("DOMContentLoaded", () => {
  gfEngine = new GameForgeEngine("renderCanvas");
  gfEngine.init();
  loadLocalAiSettings();
  refreshHierarchy();

  document.querySelectorAll(".nav").forEach(btn => btn.onclick = () => setPanel(btn.dataset.panel));

  document.addEventListener("gf-scene-updated", refreshHierarchy);
  document.addEventListener("gf-runtime-updated", e => { updateHud(e.detail); updatePauseOverlay(e.detail); });

  document.addEventListener("gf-object-selected", e => {
    const obj = e.detail;
    $("selectionBadge").textContent = obj.name;
    $("inspectorContent").innerHTML = selectedObjectText(obj);
    updateComponentPanel();
  });

  if ($("dashboardForgeBtn")) $("dashboardForgeBtn").onclick = forgeDraft;
  $("forgeDraftBtn").onclick = forgeDraft;
  $("runForgeStagesBtn").onclick = runForgeStages;
  $("generateHypergameBtn").onclick = generateHypergame;

  $("addCubeBtn").onclick = () => gfEngine.addCube();
  $("addEnemyBtn").onclick = () => gfEngine.addEnemy();
  $("addLightBtn").onclick = () => gfEngine.addLight();
  $("generateTownBtn").onclick = () => gfEngine.generateTownBlock();
  $("generateForestBtn").onclick = () => gfEngine.generateForest();
  $("generateSurvivalMapBtn").onclick = () => gfEngine.generateSurvivalMap();
  $("clearSceneBtn").onclick = () => gfEngine.clearScene();

  document.querySelectorAll(".transformBtn").forEach(btn => {
    btn.onclick = () => gfEngine.transformSelected(btn.dataset.axis, btn.dataset.delta);
  });
  $("scaleUpBtn").onclick = () => gfEngine.scaleSelected(1.15);
  $("scaleDownBtn").onclick = () => gfEngine.scaleSelected(0.85);
  $("duplicateBtn").onclick = () => gfEngine.duplicateSelected();
  $("deleteSelectedBtn").onclick = () => gfEngine.deleteSelected();

  document.querySelectorAll(".componentBtn").forEach(btn => btn.onclick = () => gfEngine.addComponentToSelected(btn.dataset.comp));
  document.querySelectorAll(".materialBtn").forEach(btn => btn.onclick = () => gfEngine.applyMaterial(btn.dataset.mat));

  $("terrainFlatBtn").onclick = () => { gfEngine.setTerrainPreset("flat"); $("terrainNotes").value = "Flat prototype ground applied."; };
  $("terrainForestBtn").onclick = () => { gfEngine.setTerrainPreset("forest"); gfEngine.generateForest(); $("terrainNotes").value = "Forest terrain preset applied with generated trees."; };
  $("terrainTownBtn").onclick = () => { gfEngine.setTerrainPreset("town"); gfEngine.generateTownBlock(); $("terrainNotes").value = "Town terrain preset applied with road/building layout."; };
  $("terrainHorrorBtn").onclick = () => { gfEngine.setTerrainPreset("horror"); gfEngine.generateSurvivalMap(); $("terrainNotes").value = "Horror survival terrain preset generated."; };


  $("generateRealisticWorldBtn").onclick = () => gfEngine.generateRealisticEnvironment();
  $("addHumanBtn").onclick = () => gfEngine.addHumanPlaceholder();
  $("addGrassBtn").onclick = () => gfEngine.addGrassPatch();
  $("addRockBtn").onclick = () => gfEngine.addRock();

  $("terrainRealisticBtn").onclick = () => {
    gfEngine.generateRealisticEnvironment();
    $("terrainNotes").value = "Realistic everyday terrain generated with grass, rocks, trees, fog, daylight and human placeholders.";
  };

  $("generateRealisticEnvironmentBtn").onclick = () => {
    gfEngine.generateRealisticEnvironment();
    $("realismNotes").value = OfflineGenerator.realismAssetPlan(projectState);
    setPanel("studio");
  };
  $("generateBushlandBtn").onclick = () => gfEngine.generateBushlandArea();
  $("generateRockFieldBtn").onclick = () => gfEngine.generateRockField();
  $("generateHumanGroupBtn").onclick = () => {
    for (let i = 0; i < 5; i++) gfEngine.addHumanPlaceholder();
  };
  $("applyRealisticLightingBtn").onclick = () => gfEngine.applyRealisticLighting();
  $("generateRealismAssetPlanBtn").onclick = () => {
    updateProjectFromInputs();
    $("realismNotes").value = OfflineGenerator.realismAssetPlan(projectState);
  };



  $("generatePlayableSurvivalBtn").onclick = generatePlayablePrototype;
  $("generatePlayablePrototypeBtn").onclick = generatePlayablePrototype;
  $("resetRuntimeBtn").onclick = () => gfEngine.resetRuntime();
  $("spawnLootBtn").onclick = () => {
    for (let i = 0; i < 5; i++) gfEngine.addLootCrate();
    for (let i = 0; i < 2; i++) gfEngine.addWeaponPickup();
  };
  $("spawnEnemyWaveBtn").onclick = () => gfEngine.spawnEnemyWave(6);
  $("playableNotes").value = OfflineGenerator.playableSystemNotes() + "\n\n" + OfflineGenerator.audioPlan();



  $("enableAudioBtn").onclick = () => gfEngine.enableAudio();
  $("testAudioBtn").onclick = () => { gfEngine.enableAudio(); gfEngine.playSound("gunshot"); setTimeout(() => gfEngine.playSound("reload"), 220); setTimeout(() => gfEngine.playSound("pickup"), 520); };
  $("enableAudioPanelBtn").onclick = () => gfEngine.enableAudio();
  $("testGunshotBtn").onclick = () => { gfEngine.enableAudio(); gfEngine.playSound("gunshot"); };
  $("testReloadBtn").onclick = () => { gfEngine.enableAudio(); gfEngine.playSound("reload"); };
  $("testPickupBtn").onclick = () => { gfEngine.enableAudio(); gfEngine.playSound("pickup"); };
  $("testEnemyBtn").onclick = () => { gfEngine.enableAudio(); gfEngine.playSound("enemyHit"); };
  $("testAmbientBtn").onclick = () => { gfEngine.enableAudio(); gfEngine.playSound("ambientWind"); };
  $("generateAudioPlanBtn").onclick = () => $("audioPlan").value = OfflineGenerator.audioPlan();
  $("audioPlan").value = OfflineGenerator.audioPlan();
















  if ($("performanceModeBtn")) $("performanceModeBtn").onclick = enableSmoothWindowMode;
  if ($("normalVisualModeBtn")) $("normalVisualModeBtn").onclick = disableSmoothWindowMode;
  restoreWindowPerformancePreference();




  if ($("createFree3DJobBtn")) $("createFree3DJobBtn").onclick = createFree3DJob;
  if ($("createFree3DAssetPackBtn")) $("createFree3DAssetPackBtn").onclick = createFree3DAssetPack;
  if ($("runFree3DQueueBtn")) $("runFree3DQueueBtn").onclick = runFree3DQueue;
  if ($("clearFree3DLogBtn")) $("clearFree3DLogBtn").onclick = clearFree3DLog;

  if ($("saveAI3DSettingsBtn")) $("saveAI3DSettingsBtn").onclick = saveAI3DSettings;
  if ($("createAI3DPlanBtn")) $("createAI3DPlanBtn").onclick = createAI3DPlan;
  if ($("createAI3DJobsBtn")) $("createAI3DJobsBtn").onclick = createAI3DJobs;
  if ($("openAI3DPolicyBtn")) $("openAI3DPolicyBtn").onclick = showAI3DPolicy;
  if ($("importAI3DModelBtn")) $("importAI3DModelBtn").onclick = importAI3DModel;
  if ($("scanAfterAI3DImportBtn")) $("scanAfterAI3DImportBtn").onclick = async () => { await scanAssetLibrary(); if (window.GameForgeCharacterImporter) await GameForgeCharacterImporter.renderList(); };
  if ($("rewriteSafeAI3DPromptBtn")) $("rewriteSafeAI3DPromptBtn").onclick = rewriteSafeAI3DPrompt;
  if ($("ai3DLog")) loadAI3DSettings();

  if ($("stableGeneratePrototypeBtn")) $("stableGeneratePrototypeBtn").onclick = stableGeneratePrototype;
  if ($("stableOpenStudioBtn")) $("stableOpenStudioBtn").onclick = () => setPanel("studio");
  if ($("stableResetRuntimeBtn")) $("stableResetRuntimeBtn").onclick = stableResetRuntime;
  if ($("stableOpenCostBtn")) $("stableOpenCostBtn").onclick = () => setPanel("aicost");

  if ($("quickAICostBtn")) $("quickAICostBtn").onclick = () => setPanel("aicost");
  if ($("quickHybridAIBtn")) $("quickHybridAIBtn").onclick = () => setPanel("hybridai");
  if ($("quickForgeWizardBtn")) $("quickForgeWizardBtn").onclick = () => setPanel("forgewizard");
  if ($("quickStudioBtn")) $("quickStudioBtn").onclick = () => setPanel("studio");

  $("saveHybridSettingsBtn").onclick = saveHybridAISettings;
  $("testHybridConnectionBtn").onclick = testHybridConnection;
  $("approveCurrentEstimateBtn").onclick = approveCurrentEstimateForHybrid;
  $("generateWithHybridAIBtn").onclick = generateWithHybridAI;
  if ($("hybridAILog")) loadHybridAISettings();


  $("estimateAICostBtn").onclick = estimateAICost;
  $("approveEstimatedRunBtn").onclick = approveEstimatedRun;
  $("showCostPolicyBtn").onclick = showCostPolicy;
  if ($("aiCostLog")) estimateAICost();


  $("generateCuratedPlanBtn").onclick = generateCuratedPlan;
  $("checkCuratedAssetBtn").onclick = checkCuratedAsset;
  $("downloadCuratedAssetBtn").onclick = downloadCuratedAsset;
  $("openCuratedRegistryBtn").onclick = showCuratedRegistry;
  renderCuratedDownloads();


  $("scanCharacterModelsBtn").onclick = scanCharacterModels;
  $("autoAssignCharacterModelsBtn").onclick = autoAssignCharacterModels;
  $("generateCharacterPlanBtn").onclick = generateCharacterPlan;
  $("openAssetPackFromCharactersBtn").onclick = () => setPanel("assetpacks");
  document.addEventListener("gf-assets-updated", () => { if ($("characterModelList")) GameForgeCharacterImporter.renderList(); });
  if ($("characterModelList")) GameForgeCharacterImporter.renderList();


  $("openWizardBtn").onclick = () => setPanel("forgewizard");
  $("runForgeWizardBtn").onclick = runForgeWizardPipeline;
  $("makeItBetterBtn").onclick = makeCurrentGameBetter;
  $("refreshHealthBtn").onclick = updateProjectHealth;
  $("runReadinessGateBtn").onclick = runReadinessGate;


  $("generateFullGamePackageBtn").onclick = generateFullGamePackage;
  $("validateNativePackageBtn").onclick = validateNativePackage;
  $("exportNativePackageBtn").onclick = exportNativePackage;
  $("nativeChecklistBtn").onclick = showNativeChecklist;


  $("generateCompletePlayableBtn").onclick = generateCompletePlayableDraft;
  $("validatePlayableBuildBtn").onclick = validatePlayableBuild;
  $("exportPlayableBuildBtn").onclick = exportPlayableBuildV06;
  $("generateMenuPlansBtn").onclick = generateMenuPlans;
  $("resumeRuntimeBtn").onclick = () => { if (gfEngine?.runtime) { gfEngine.runtime.paused = false; gfEngine.runtime.menuState = "gameplay"; updatePauseOverlay(gfEngine.runtime); } };
  $("runtimeSaveBtn").onclick = () => { if (gfEngine?.runtime) { gfEngine.runtime.saveSlot = JSON.stringify(gfEngine.runtime); alert("Runtime state saved in prototype memory."); } };
  $("runtimeLoadBtn").onclick = () => { if (gfEngine?.runtime?.saveSlot) { Object.assign(gfEngine.runtime, JSON.parse(gfEngine.runtime.saveSlot)); updateHud(gfEngine.runtime); alert("Runtime state loaded."); } else alert("No runtime save slot yet."); };
  $("runtimeSettingsBtn").onclick = () => alert("Settings menu placeholder: resolution, volume and sensitivity are planned for runtime UI.");


  $("runLicenceAuditBtn").onclick = runLicenceAudit;
  $("collectLicenceMetadataBtn").onclick = collectLicenceMetadata;
  $("classifyLicenceBtn").onclick = classifyManualLicence;
  $("openAuditNoteBtn").onclick = showAuditNote;


  $("generateSoundEventQueriesBtn").onclick = generateSoundEventQueries;
  $("searchLegalSoundsBtn").onclick = searchLegalSounds;
  $("autoFindLegalSoundsBtn").onclick = autoFindLegalSounds;
  $("generateCreditsBtn").onclick = generateAudioCreditsFile;


  $("generateLocalMediaBtn").onclick = generateLocalMediaPack;
  $("generateAndScanMediaBtn").onclick = async () => { await generateLocalMediaPack(); await scanAssetLibrary(); };
  $("suggestWebSoundKeywordsBtn").onclick = suggestWebSoundKeywords;
  $("searchWebSoundsBtn").onclick = searchWebSounds;


  $("downloadAssetBtn").onclick = downloadAssetPack;
  $("importAssetFilesBtn").onclick = importAssetFiles;
  $("scanAssetLibraryBtn").onclick = scanAssetLibrary;
  $("saveAssetManifestBtn").onclick = saveAssetManifest;
  $("refreshModelRefsBtn").onclick = renderModelReferences;
  document.addEventListener("gf-assets-updated", e => renderAssetLibrary(e.detail));
  scanAssetLibrary();


  $("autonomousGenerateBtn").onclick = () => runSafeDashboardGeneration(false);
  $("autonomousPanelGenerateBtn").onclick = () => autonomousGenerate(true);
  $("autonomousSceneOnlyBtn").onclick = () => autonomousGenerate(false);
  $("saveLocalAiSettingsBtn").onclick = saveLocalAiSettings;
  $("testLocalAiBtn").onclick = testLocalAi;


  document.querySelectorAll(".systemBtn").forEach(btn => {
    btn.onclick = () => {
      const add = OfflineGenerator.logic(btn.dataset.system);
      $("logicScript").value = ($("logicScript").value + "\n\n" + add).trim();
      projectState.logicScript = $("logicScript").value;
    };
  });

  $("generateAssetListBtn").onclick = () => { updateProjectFromInputs(); $("assetPlan").value = OfflineGenerator.assetPlan(projectState); };
  $("createPlaceholderAssetsBtn").onclick = () => { $("assetPlan").value += "\n\n## Placeholder Asset Notes\n- Capsule enemies\n- Greybox buildings\n- Primitive weapons\n- Placeholder trees\n- Basic UI icons\n"; };
  $("materialPlan").value = OfflineGenerator.materialPlan();
  if ($("realismNotes")) $("realismNotes").value = "Offline Realism Pack ready. Generate realistic environments using procedural grass, trees, rocks, terrain, lighting, fog and human placeholders.";

  $("runTestsBtn").onclick = runTests;
  $("repairWarningsBtn").onclick = repairWarnings;

  $("playBtn").onclick = () => {
    gfEngine.playSound("uiClick");
    gfEngine.setPlayMode(!gfEngine.playMode);
    $("playBtn").textContent = gfEngine.playMode ? "Stop" : "Play";
    $("gameHud").classList.toggle("hidden", !gfEngine.playMode);
  };

  $("saveProjectBtn").onclick = saveProject;
  $("loadProjectBtn").onclick = loadProject;
  $("exportProjectBtn").onclick = exportProject;
  $("exportPlayableBtn").onclick = exportPlayable;
  $("saveSettingsBtn").onclick = () => alert("Offline settings saved for this prototype session.");
});
window.addEventListener('DOMContentLoaded', () => {

  if ($("createVisualTargetBtn")) $("createVisualTargetBtn").onclick = createVisualTargetPlan;
  if ($("copyVisualPromptBtn")) $("copyVisualPromptBtn").onclick = copyVisualPrompt;
  if ($("createVisualFree3DJobsBtn")) $("createVisualFree3DJobsBtn").onclick = createVisualFree3DJobs;
  if ($("createVisualAI3DJobsBtn")) $("createVisualAI3DJobsBtn").onclick = createVisualAI3DJobs;
  if ($("runVisualFree3DQueueBtn")) $("runVisualFree3DQueueBtn").onclick = runVisualFree3DQueue;

});

window.addEventListener('DOMContentLoaded', () => {

  if ($("createMeshRecipeBtn")) $("createMeshRecipeBtn").onclick = createMeshRecipe;
  if ($("placeMeshInSceneBtn")) $("placeMeshInSceneBtn").onclick = placeMeshInScene;
  if ($("createPBRMaterialBtn")) $("createPBRMaterialBtn").onclick = createPBRMaterial;
  if ($("generateMeshPackBtn")) $("generateMeshPackBtn").onclick = generateMeshPack;
  if ($("createGLBExportPlanBtn")) $("createGLBExportPlanBtn").onclick = createGLBExportPlan;

});

window.addEventListener('DOMContentLoaded', () => {

  if ($("createGameIntelPlanBtn")) $("createGameIntelPlanBtn").onclick = createGameIntelPlan;
  if ($("createGameIntelJobsBtn")) $("createGameIntelJobsBtn").onclick = createGameIntelJobs;
  if ($("runGameIntelFree3DQueueBtn")) $("runGameIntelFree3DQueueBtn").onclick = runGameIntelFree3DQueue;
  if ($("openGameIntelStudioBtn")) $("openGameIntelStudioBtn").onclick = () => setPanel("studio");

});

window.addEventListener('DOMContentLoaded', () => {

  if ($("runOneClickForgeBtn")) $("runOneClickForgeBtn").onclick = () => runOneClickForge(false);
  if ($("runOneClickForgePlayBtn")) $("runOneClickForgePlayBtn").onclick = () => runOneClickForge(true);
  if ($("quickOneClickForgeBtn")) $("quickOneClickForgeBtn").onclick = () => runOneClickForge(false);
  if ($("quickOneClickForgePlayBtn")) $("quickOneClickForgePlayBtn").onclick = () => runOneClickForge(true);
  if ($("openOneClickStudioBtn")) $("openOneClickStudioBtn").onclick = () => setPanel("studio");
  if ($("clearOneClickLogBtn")) $("clearOneClickLogBtn").onclick = clearOneClickLog;

});

window.addEventListener('DOMContentLoaded', () => {

  if ($("launchPlaytestWindowBtn")) $("launchPlaytestWindowBtn").onclick = launchExternalPlaytestWindow;
  if ($("validatePlayableDemoBtn")) $("validatePlayableDemoBtn").onclick = validatePlayableDemo;
  if ($("exportPlayableDemoBtn")) $("exportPlayableDemoBtn").onclick = exportPlayableDemo;
  if ($("openPlaytestStudioBtn")) $("openPlaytestStudioBtn").onclick = () => setPanel("studio");

});

window.addEventListener('DOMContentLoaded', () => {

  if ($("createCompleteGamePlanBtn")) $("createCompleteGamePlanBtn").onclick = createCompleteGamePlan;
  if ($("exportCompleteGameExeBtn")) $("exportCompleteGameExeBtn").onclick = exportCompleteGameExePackage;
  if ($("openCompleteGameStudioBtn")) $("openCompleteGameStudioBtn").onclick = () => setPanel("studio");

});

window.addEventListener('DOMContentLoaded', () => {

  if ($("createPhotorealModePlanBtn")) $("createPhotorealModePlanBtn").onclick = createPhotorealModePlan;
  if ($("applyPhotorealLightingBtn")) $("applyPhotorealLightingBtn").onclick = applyPhotorealLighting;
  if ($("createPhotorealPBRPackBtn")) $("createPhotorealPBRPackBtn").onclick = createPhotorealPBRPack;
  if ($("applyPhotorealDetailPassBtn")) $("applyPhotorealDetailPassBtn").onclick = applyPhotorealDetailPass;
  if ($("openPhotorealStudioBtn")) $("openPhotorealStudioBtn").onclick = () => setPanel("studio");

});

window.addEventListener('DOMContentLoaded', () => {

  if ($("scanAutoAssetsBtn")) $("scanAutoAssetsBtn").onclick = scanAutoAssets;
  if ($("createAutoAssetPlanBtn")) $("createAutoAssetPlanBtn").onclick = createAutoAssetPlan;
  if ($("runAutoAssetPassBtn")) $("runAutoAssetPassBtn").onclick = runAutoAssetPass;
  if ($("openAutoAssetsStudioBtn")) $("openAutoAssetsStudioBtn").onclick = () => setPanel("studio");

});

window.addEventListener('DOMContentLoaded', () => {

  if ($("createDefaultModelManifestBtn")) $("createDefaultModelManifestBtn").onclick = createDefaultModelManifest;
  if ($("createModelManifestFromGameBtn")) $("createModelManifestFromGameBtn").onclick = createModelManifestFromGame;
  if ($("runModelGathererBtn")) $("runModelGathererBtn").onclick = runModelGatherer;
  if ($("runGatherAndImportBtn")) $("runGatherAndImportBtn").onclick = runGatherAndImport;

});

window.addEventListener('DOMContentLoaded', () => {

  if ($("createAnimationImportPlanBtn")) $("createAnimationImportPlanBtn").onclick = createAnimationImportPlan;
  if ($("runAnimationImportPassBtn")) $("runAnimationImportPassBtn").onclick = runAnimationImportPass;
  if ($("assignAnimationControllersBtn")) $("assignAnimationControllersBtn").onclick = assignAnimationControllers;
  if ($("openAnimationStudioBtn")) $("openAnimationStudioBtn").onclick = () => setPanel("studio");

});

window.addEventListener('DOMContentLoaded', () => {

  if ($("createDefaultAnimationManifestBtn")) $("createDefaultAnimationManifestBtn").onclick = createDefaultAnimationManifest;
  if ($("createAnimationManifestFromGameBtn")) $("createAnimationManifestFromGameBtn").onclick = createAnimationManifestFromGame;
  if ($("runAnimationGathererBtn")) $("runAnimationGathererBtn").onclick = runAnimationGatherer;
  if ($("runFullAnimationGatherImportBtn")) $("runFullAnimationGatherImportBtn").onclick = runFullAnimationGatherImport;
  if ($("createMissingAnimationReportBtn")) $("createMissingAnimationReportBtn").onclick = createMissingAnimationReport;

});


// gf-v230-dashboard-force-hook
window.addEventListener("DOMContentLoaded", () => {
  const btn = document.getElementById("autonomousGenerateBtn");
  if (btn) btn.onclick = () => runSafeDashboardGeneration(false);
});


async function runSelfRepairGeneration(autoPlay = false) {
  updateProjectFromInputs();

  if (!projectState.prompt || !projectState.prompt.trim()) {
    alert("Paste a game prompt into the main Game Description / Generation box first.");
    return { ok: false, error: "Missing prompt" };
  }

  if (window.GameForgeGenerationETA) {
    GameForgeGenerationETA.start(projectState);
    GameForgeGenerationETA.setStage("input", "Reading your game name, description and generation settings");
  }

  try {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("photoreal", "Photoreal always-on prep is starting");
    await runAutonomousRealismPrep();

    if (window.GameForgeGenerationETA) GameForgeGenerationETA.setStage("repair", "Checking and repairing required generation systems");
    let result;
    if (window.GameForgePipelineRegistry && typeof GameForgePipelineRegistry.noSkipGenerate === "function") {
      result = await GameForgePipelineRegistry.noSkipGenerate(projectState, gfEngine, autoPlay);
    } else if (typeof runSafeDashboardGeneration === "function") {
      result = await runSafeDashboardGeneration(autoPlay);
    } else {
      if (typeof forgeDraft === "function") result = forgeDraft();
      else throw new Error("No generation pipeline available");
    }

    if (window.GameForgeGenerationETA) {
      GameForgeGenerationETA.setStage("export", "Preparing complete game / EXE export package");
      const q = projectState.photorealQualityReport;
      if (q && !q.pass) {
        GameForgeGenerationETA.finish(`Generation complete, but ${q.status}: ${q.score}/100. Photoreal target not met — check Forge Report.`);
      } else {
        GameForgeGenerationETA.finish("Photoreal-ready generation complete. Open Studio Editor and press Play.");
      }
    }

    return result || { ok: true };
  } catch (error) {
    if (window.GameForgeGenerationETA) GameForgeGenerationETA.fail(error);
    alert("Autonomous generation hit an issue: " + error.message);
    return { ok: false, error: error.message };
  }
}

window.addEventListener("DOMContentLoaded", () => {
  const btn = document.getElementById("autonomousGenerateBtn");
  if (btn) btn.onclick = () => runSelfRepairGeneration(false);
});


// gf-v232-final-dashboard-hook
window.addEventListener("DOMContentLoaded", () => {
  const btn = document.getElementById("autonomousGenerateBtn");
  if (btn) {
    btn.onclick = () => {
      if (typeof runSelfRepairGeneration === "function") return runSelfRepairGeneration(false);
      if (typeof runSafeDashboardGeneration === "function") return runSafeDashboardGeneration(false);
      if (typeof forgeDraft === "function") return forgeDraft();
      alert("No generation pipeline available.");
    };
  }
});


// gf-v242-autonomous-eta-hook
window.addEventListener("DOMContentLoaded", () => {
  if (window.GameForgeGenerationETA) GameForgeGenerationETA.ensurePanel();
  const btn = document.getElementById("autonomousGenerateBtn");
  if (btn) {
    btn.textContent = "Generate Full Game Autonomously";
    btn.onclick = () => runSelfRepairGeneration(false);
  }
});

// GF66_LIVE_WATCHER_HOOK
window.addEventListener("DOMContentLoaded", () => {
  setTimeout(() => {
    document.querySelectorAll("button").forEach(btn => {
      const t=(btn.textContent||"").toLowerCase();
      if(t.includes("generate") || t.includes("forge") || t.includes("play")){
        btn.addEventListener("click", () => {
          if(window.GameForgeFlawlessGenerationProcessController){
            GameForgeFlawlessGenerationProcessController.createRun(window.projectState || {});
            GameForgeFlawlessGenerationProcessController.log("User started a build/generation action.", "info");
          }
        }, {capture:true});
      }
    });
  }, 1000);
});
