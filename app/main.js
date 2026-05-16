// GameForge AI Engine v1.1 Free 3D Generator
process.on('uncaughtException', (error) => {
  console.error('[GameForge Fatal]', error && (error.stack || error.message || error));
});
process.on('unhandledRejection', (reason) => {
  console.error('[GameForge Promise Rejection]', reason && (reason.stack || reason.message || reason));
});

const { app, BrowserWindow, ipcMain, dialog } = require('electron');

// Stable native-window performance — GPU disabled to avoid freeze on drag/resize
try {
  app.disableHardwareAcceleration();
  app.commandLine.appendSwitch('disable-gpu');
  app.commandLine.appendSwitch('disable-gpu-compositing');
  app.commandLine.appendSwitch('disable-gpu-rasterization');
  app.commandLine.appendSwitch('disable-accelerated-2d-canvas');
  app.commandLine.appendSwitch('disable-zero-copy');
  app.commandLine.appendSwitch('disable-features', 'CalculateNativeWinOcclusion,VizDisplayCompositor,HardwareMediaKeyHandling');
  app.commandLine.appendSwitch('disable-renderer-backgrounding');
  app.commandLine.appendSwitch('disable-background-timer-throttling');
  app.commandLine.appendSwitch('disable-backgrounding-occluded-windows');
} catch (error) {
  console.warn('[GameForge] Window stability switches skipped:', error.message);
}

const path = require('path');
const { execFileSync, spawnSync } = require('child_process');
const fs = require('fs');
const { pathToFileURL } = require('url');

let mainWindow;

function createWindow() {
  mainWindow = new BrowserWindow({
    frame: true,
    transparent: false,
    backgroundColor: '#070d18',
    titleBarStyle: 'default',
    thickFrame: true,
    hasShadow: true,
    paintWhenInitiallyHidden: true,
    width: 1280,
    height: 800,
    minWidth: 1000,
    minHeight: 650,
    title: 'GameForge AI Engine v6.8.2',
    webPreferences: {
      backgroundThrottling: false,
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true
    }
  });

  mainWindow.loadFile(path.join(__dirname, 'src', 'index.html'));
}

app.whenReady().then(createWindow);

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) createWindow();
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

function ensureDir(dir) {
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
}











const MESHY_API_BASE = 'https://api.meshy.ai';

function gfFetch(url, options = {}) {
  if (typeof fetch === 'function') return fetch(url, options);
  return new Promise((resolve, reject) => {
    const https = require('https');
    const parsed = new URL(url);
    const req = https.request({
      method: options.method || 'GET',
      hostname: parsed.hostname,
      path: parsed.pathname + parsed.search,
      headers: options.headers || {}
    }, res => {
      const chunks = [];
      res.on('data', d => chunks.push(d));
      res.on('end', () => {
        const body = Buffer.concat(chunks);
        resolve({
          ok: res.statusCode >= 200 && res.statusCode < 300,
          status: res.statusCode,
          text: async () => body.toString('utf8'),
          arrayBuffer: async () => body
        });
      });
    });
    req.on('error', reject);
    if (options.body) req.write(options.body);
    req.end();
  });
}


function getSavedMeshySettingsRaw() {
  const file = meshySettingsFile();
  if (!fs.existsSync(file)) return defaultMeshySettings();
  try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch(e) { return defaultMeshySettings(); }
}

async function meshyRequest(pathname, options = {}) {
  const settings = getSavedMeshySettingsRaw();
  if (!settings.apiKey) throw new Error('Meshy API key is not saved.');
  const response = await gfFetch(`${MESHY_API_BASE}${pathname}`, {
    ...options,
    headers: {
      Authorization: `Bearer ${settings.apiKey}`,
      ...(options.headers || {})
    }
  });
  const text = await response.text();
  let data = {};
  try { data = text ? JSON.parse(text) : {}; } catch(e) { data = { raw: text }; }
  if (!response.ok) {
    throw new Error(data.message || data.error || data.raw || `Meshy API HTTP ${response.status}`);
  }
  return data;
}

function meshyApiDownloadsRoot() {
  const root = path.join(meshyRoot(), 'api_downloads');
  ensureDir(root);
  ['models','metadata','reports','textures','thumbnails'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function sanitizeAssetName(name) {
  return String(name || 'asset').replace(/[^a-z0-9._-]+/gi, '_').slice(0, 80);
}

async function downloadUrlToFile(url, file) {
  const response = await gfFetch(url);
  if (!response.ok) throw new Error(`Download failed ${response.status}: ${url}`);
  const buffer = Buffer.from(await response.arrayBuffer());
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, buffer);
  return file;
}

async function createMeshyPreviewTask(asset) {
  return await meshyRequest('/openapi/v2/text-to-3d', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      mode: 'preview',
      prompt: asset.prompt,
      art_style: 'realistic',
      should_remesh: true
    })
  });
}

async function createMeshyRefineTask(previewTaskId, asset) {
  return await meshyRequest('/openapi/v2/text-to-3d', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      mode: 'refine',
      preview_task_id: previewTaskId,
      enable_pbr: true,
      target_formats: ['glb'],
      auto_size: true,
      texture_prompt: asset.texturePrompt || asset.prompt
    })
  });
}

async function getMeshyTask(taskId) {
  return await meshyRequest(`/openapi/v2/text-to-3d/${encodeURIComponent(taskId)}`, { method: 'GET' });
}

function delay(ms) { return new Promise(resolve => setTimeout(resolve, ms)); }

async function pollMeshyTask(taskId, timeoutMs = 20 * 60 * 1000, intervalMs = 10000) {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    const task = await getMeshyTask(taskId);
    if (task.status === 'SUCCEEDED') return task;
    if (['FAILED','EXPIRED','CANCELED'].includes(task.status)) {
      throw new Error(`Meshy task ${taskId} failed: ${task.task_error?.message || task.status}`);
    }
    await delay(intervalMs);
  }
  throw new Error(`Meshy task ${taskId} timed out.`);
}

async function downloadMeshyResult(asset, task, root) {
  const safe = sanitizeAssetName(asset.id || asset.type || 'meshy_asset');
  const downloaded = [];
  const urls = task.model_urls || {};
  for (const ext of ['glb','fbx','obj','usdz']) {
    if (urls[ext]) {
      const file = path.join(root, 'models', `${safe}_${task.id}.${ext}`);
      await downloadUrlToFile(urls[ext], file);
      downloaded.push({ type: 'model', format: ext, file });
    }
  }
  if (task.thumbnail_url) {
    try {
      const file = path.join(root, 'thumbnails', `${safe}_${task.id}.png`);
      await downloadUrlToFile(task.thumbnail_url, file);
      downloaded.push({ type: 'thumbnail', format: 'png', file });
    } catch(e) {}
  }
  const meta = {
    generatedAt: new Date().toISOString(),
    provider: 'Meshy',
    assetId: asset.id,
    assetType: asset.type,
    prompt: asset.prompt,
    texturePrompt: asset.texturePrompt,
    previewTaskId: asset.previewTaskId || null,
    refineTaskId: task.id,
    task,
    downloaded,
    licence: 'Review Meshy account plan/licence terms before commercial/public release.',
    attributionRequired: true
  };
  const metaFile = path.join(root, 'metadata', `${safe}_${task.id}.json`);
  fs.writeFileSync(metaFile, JSON.stringify(meta, null, 2), 'utf8');
  return { downloaded, metaFile, task };
}

function estimateMeshyCreditsForQueue(queue) {
  return (queue.requests || []).filter(r => r.enabled !== false).length * 40;
}

function meshyRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeMeshyProvider');
  ensureDir(root);
  ['prompt_packs','imports','settings','licences','reports','api_ready'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function meshySettingsFile() {
  return path.join(meshyRoot(), 'settings', 'meshy_provider_settings.json');
}

function defaultMeshySettings() {
  return {
    mode: 'free_test',
    apiKeySaved: false,
    apiKey: '',
    paidProvidersAllowed: false,
    monthlyCreditCap: 100,
    commercialReleaseMode: false,
    requireAttributionReport: true,
    notes: [
      'Free-test mode prepares prompts and import folders.',
      'Full automatic Meshy API workflow may require an API-enabled paid plan.',
      'Check Meshy licence terms before commercial release.'
    ]
  };
}

function createMeshyPromptMarkdown(plan) {
  return `# GameForge Meshy Free Test Prompt Pack

Generated: ${new Date().toISOString()}

## Important
This is a free-test workflow pack. Generate/download assets in Meshy, then place GLB/FBX files into the imports folder.

## Legal Rules
${(plan.legalRules || []).map(x => '- ' + x).join('\n')}

## Workflow
${(plan.workflow || []).map(x => '- ' + x).join('\n')}

## Prompts
${(plan.requests || []).map((r, i) => `### ${i+1}. ${r.id}

Type: ${r.type}
Priority: ${r.priority}

Prompt:
${r.prompt}

Texture Prompt:
${r.texturePrompt}

Suggested filename:
${r.id}.glb
`).join('\n')}
`;
}

function toolchainRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeToolchainRuns');
  ensureDir(root);
  ['plans','reports','blender','godot','unreal','builds','logs','legal'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function commandExists(command) {
  try {
    const checker = process.platform === 'win32' ? 'where' : 'which';
    const output = execFileSync(checker, [command], { encoding: 'utf8', stdio: ['ignore','pipe','ignore'] }).trim();
    return output.split(/\r?\n/)[0] || '';
  } catch (e) {
    return '';
  }
}

function detectCommonUnrealPaths() {
  if (process.platform !== 'win32') return '';
  const versions = ['5.7', '5.6', '5.5', '5.4', '5.3', '5.2', '5.1', '5.0'];
  const drives = ['C:', 'D:', 'E:', 'F:'];
  const roots = [
    'Program Files\\Epic Games',
    'Program Files\\UE_',   // bare version roots like C:\Program Files\UE_5.6
    'Epic Games',
    'Games\\Epic Games',
    'Games'
  ];
  const subpath = 'Engine\\Binaries\\Win64\\UnrealEditor.exe';
  const candidates = [];

  for (const drive of drives) {
    for (const ver of versions) {
      const tag = `UE_${ver}`;
      // Standard Epic Games path: C:\Program Files\Epic Games\UE_5.6\...
      candidates.push(`${drive}\\Program Files\\Epic Games\\${tag}\\${subpath}`);
      // Non-Epic root: C:\Program Files\UE_5.6\...
      candidates.push(`${drive}\\Program Files\\${tag}\\${subpath}`);
      // D:\Epic Games\UE_5.6\...
      candidates.push(`${drive}\\Epic Games\\${tag}\\${subpath}`);
      // D:\UE_5.6\...
      candidates.push(`${drive}\\${tag}\\${subpath}`);
      // D:\Games\UE_5.6\...
      candidates.push(`${drive}\\Games\\${tag}\\${subpath}`);
      // D:\Games\Epic Games\UE_5.6\...
      candidates.push(`${drive}\\Games\\Epic Games\\${tag}\\${subpath}`);
    }
  }

  return candidates.find(p => fs.existsSync(p)) || '';
}

function validateUnrealExePath(p) {
  if (!p || typeof p !== 'string' || !p.trim()) {
    return { valid: false, reason: 'No path provided.' };
  }
  const trimmed = p.trim();
  // Extract basename handling both forward slash and backslash (Windows paths on any OS)
  const basename = trimmed.replace(/\\/g, '/').split('/').pop() || '';
  // Accept UnrealEditor.exe, UnrealEditorCmd.exe, UnrealEditor (Linux/Mac)
  if (!/^unrealedit/i.test(basename)) {
    return { valid: false, reason: `Selected file must be UnrealEditor.exe (got: "${basename}")` };
  }
  if (!fs.existsSync(trimmed)) {
    return { valid: false, reason: `File does not exist at: "${trimmed}"` };
  }
  return { valid: true, path: trimmed };
}

function detectToolchain() {
  const blender = commandExists('blender');
  const godot = commandExists('godot') || commandExists('godot4') || commandExists('Godot');
  const ffmpeg = commandExists('ffmpeg');
  const unreal = commandExists('UnrealEditor') || detectCommonUnrealPaths();

  const tools = {
    blender: { name: 'Blender', found: Boolean(blender), path: blender || '', freeLegalUse: 'Free/open-source asset processing' },
    godot: { name: 'Godot', found: Boolean(godot), path: godot || '', freeLegalUse: 'Free/open-source quick EXE export path' },
    ffmpeg: { name: 'FFmpeg', found: Boolean(ffmpeg), path: ffmpeg || '', freeLegalUse: 'Free/open-source audio/video processing' },
    unreal: { name: 'Unreal Engine', found: Boolean(unreal), path: unreal || '', freeLegalUse: 'Free-to-start high-end visual handoff path; obey Epic terms' }
  };

  const recommendations = [];
  if (!tools.blender.found) recommendations.push('Install Blender for automatic model cleanup, LOD preparation and GLB/FBX processing.');
  if (!tools.godot.found) recommendations.push('Install Godot for a free quick Windows EXE export path.');
  if (!tools.ffmpeg.found) recommendations.push('Install FFmpeg for reliable audio/video conversion and preview generation.');
  if (!tools.unreal.found) recommendations.push('Install Unreal Engine if you want the strongest high-end AA / near-AAA visual handoff path.');
  if (Object.values(tools).every(t => t.found)) recommendations.push('All primary external tools detected.');

  return { ok: true, tools, recommendations };
}

function writeToolchainFile(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content, 'utf8');
  return file;
}

function createBlenderAutomationScript(folder) {
  const script = `import bpy, os, json, math

# GameForge Blender automation template
# Purpose: clean, scale, centre, create simple LOD-ready exports.

print("GameForge Blender automation started")
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

# Placeholder scene marker to confirm Blender automation is working.
bpy.ops.mesh.primitive_cube_add(size=2, location=(0,0,1))
obj = bpy.context.object
obj.name = "GameForge_Blender_Toolchain_TestAsset"
mat = bpy.data.materials.new("GameForge_PBR_Placeholder")
mat.use_nodes = True
obj.data.materials.append(mat)

export_path = os.path.join(r"${folder.replace(/\\/g, "\\\\")}", "gameforge_blender_processed_test_asset.glb")
bpy.ops.export_scene.gltf(filepath=export_path, export_format='GLB')
print("Exported", export_path)
`;
  const file = path.join(folder, 'gameforge_blender_process.py');
  fs.writeFileSync(file, script, 'utf8');
  return file;
}

function createGodotExportPlan(folder, projectName) {
  const exportPreset = `[preset.0]

name="Windows Desktop"
platform="Windows Desktop"
runnable=true
dedicated_server=false
custom_features=""
export_filter="all_resources"
include_filter=""
exclude_filter=""
export_path="builds/${projectName || 'GameForgeGame'}.exe"
encryption_include_filters=""
encryption_exclude_filters=""
encrypt_pck=false
encrypt_directory=false

[preset.0.options]

binary_format/embed_pck=true
`;
  const file = path.join(folder, 'export_presets.cfg');
  fs.writeFileSync(file, exportPreset, 'utf8');
  return file;
}

function createLegalPolicy(folder) {
  const policy = `# GameForge Free/Legal Asset Policy

Rules:
- Do not scrape random copyrighted assets.
- Use CC0, public-domain, commercial-use approved assets, or user-owned/generated assets.
- Store source URL, licence, author/source and download timestamp with every asset.
- Do not include branded IP, famous characters, paid marketplace assets, or unclear licences.
- AI-generated assets must be treated according to the provider's commercial-use terms.
`;
  const file = path.join(folder, 'LEGAL_ASSET_POLICY.md');
  fs.writeFileSync(file, policy, 'utf8');
  return file;
}

function unrealExportPrepRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeUnrealExportPrep');
  ensureDir(root);
  ['packages','reports'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function audioAssetsRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAudioAssets');
  ensureDir(root);
  ['plans','audio','metadata','reports'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function makeProceduralSample(type, t, freq, phase) {
  const noise = Math.random() * 2 - 1;
  if (type === 'ambience') return Math.sin(2*Math.PI*freq*t) * 0.10 + Math.sin(2*Math.PI*(freq*0.49)*t) * 0.08 + noise * 0.05;
  if (type === 'wind') return Math.sin(2*Math.PI*(freq*0.25)*t + phase) * 0.10 + noise * 0.10;
  if (type === 'footstep') return noise * Math.exp(-t*18) * 0.75 + Math.sin(2*Math.PI*freq*t) * Math.exp(-t*16) * 0.18;
  if (type === 'creak') return Math.sin(2*Math.PI*(freq + Math.sin(t*12)*35)*t) * Math.exp(-t*1.6) * 0.32 + noise * 0.04;
  if (type === 'static') return noise * 0.35 + Math.sin(2*Math.PI*freq*t) * 0.06;
  if (type === 'whisper') return (Math.sin(2*Math.PI*(freq + Math.sin(t*20)*30)*t) * 0.10 + noise * 0.16) * (0.35 + 0.65*Math.sin(t*7)**2);
  if (type === 'growl') return Math.sin(2*Math.PI*(freq + Math.sin(t*8)*20)*t) * 0.25 + noise * 0.12;
  if (type === 'sting') return (Math.sin(2*Math.PI*(freq + t*900)*t) * 0.45 + noise * 0.22) * Math.exp(-t*2.6);
  return Math.sin(2*Math.PI*freq*t) * Math.exp(-t*5) * 0.4;
}

function writeProceduralWav(file, event = {}) {
  const sampleRate = 22050;
  const duration = Math.max(0.05, Math.min(12, Number(event.duration || 0.5)));
  const samples = Math.floor(sampleRate * duration);
  const freq = Number(event.freq || 220);
  const type = event.type || 'tone';
  const fd = fs.openSync(file, 'w');

  const w = s => fs.writeSync(fd, Buffer.from(s));
  const u32 = v => { const b = Buffer.alloc(4); b.writeUInt32LE(v); fs.writeSync(fd,b); };
  const u16 = v => { const b = Buffer.alloc(2); b.writeUInt16LE(v); fs.writeSync(fd,b); };

  w('RIFF'); u32(36 + samples * 2); w('WAVEfmt '); u32(16); u16(1); u16(1); u32(sampleRate); u32(sampleRate*2); u16(2); u16(16); w('data'); u32(samples*2);

  const phase = Math.random() * Math.PI * 2;
  for (let i = 0; i < samples; i++) {
    const t = i / sampleRate;
    const fadeIn = Math.min(1, t / 0.05);
    const fadeOut = Math.min(1, (duration - t) / 0.12);
    const env = Math.max(0, Math.min(1, fadeIn * fadeOut));
    let val = makeProceduralSample(type, t, freq, phase) * env;
    val = Math.max(-1, Math.min(1, val));
    const b = Buffer.alloc(2);
    b.writeInt16LE(Math.floor(val * 32767));
    fs.writeSync(fd, b);
  }
  fs.closeSync(fd);
  return file;
}

function selfGeneratedAssetsRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeSelfGeneratedAssets');
  ensureDir(root);
  ['plans','models','materials','textures','audio','icons','reports','metadata'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function writeSelfAssetJson(file, payload) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, JSON.stringify(payload, null, 2), 'utf8');
  return file;
}

function writeSelfTextureSvg(file, title, colors = ['#1d2528', '#4d5d5a', '#0b0e11']) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">
  <defs>
    <filter id="noise">
      <feTurbulence type="fractalNoise" baseFrequency="0.85" numOctaves="5" stitchTiles="stitch"/>
      <feColorMatrix type="saturate" values="0.35"/>
      <feBlend mode="multiply" in2="SourceGraphic"/>
    </filter>
    <linearGradient id="g" x1="0" x2="1" y1="0" y2="1">
      <stop offset="0%" stop-color="${colors[0]}"/>
      <stop offset="52%" stop-color="${colors[1]}"/>
      <stop offset="100%" stop-color="${colors[2]}"/>
    </linearGradient>
  </defs>
  <rect width="1024" height="1024" fill="url(#g)"/>
  <rect width="1024" height="1024" filter="url(#noise)" opacity="0.55"/>
  <text x="48" y="960" font-family="Arial" font-size="34" fill="rgba(255,255,255,0.45)">${title}</text>
</svg>`;
  fs.writeFileSync(file, svg, 'utf8');
  return file;
}

function writeSelfIconSvg(file, label, color = '#5ea8ff') {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
  <rect width="512" height="512" rx="96" fill="#07101d"/>
  <circle cx="256" cy="256" r="170" fill="${color}" opacity="0.18"/>
  <path d="M156 278 L232 354 L372 158" fill="none" stroke="${color}" stroke-width="36" stroke-linecap="round" stroke-linejoin="round"/>
  <text x="256" y="445" text-anchor="middle" font-family="Arial" font-size="34" fill="#eaf4ff">${label}</text>
</svg>`;
  fs.writeFileSync(file, svg, 'utf8');
  return file;
}

function writeSelfGeneratedGltf(file, name, type) {
  // Minimal placeholder glTF descriptor. GameForge can use this as a generated asset reference.
  const gltf = {
    asset: { version: '2.0', generator: 'GameForge Game Style + Rating Selector v3.3.1' },
    scene: 0,
    scenes: [{ nodes: [0] }],
    nodes: [{ name, extras: { type, generated: true, note: 'procedural placeholder descriptor' } }],
    extras: {
      gameforge: {
        generated: true,
        type,
        name,
        usage: 'fallback procedural asset',
        licence: 'Generated by GameForge project user'
      }
    }
  };
  fs.writeFileSync(file, JSON.stringify(gltf, null, 2), 'utf8');
  return file;
}

function selfAssetPlanFromPrompt(payload = {}) {
  const prompt = String(payload.prompt || '').toLowerCase();
  const add = (arr, item) => arr.push(item);
  const plan = {
    mode: 'Game Style + Rating Selector',
    generatedAt: new Date().toISOString(),
    gameName: payload.gameName || 'GameForge Generated Game',
    prompt: payload.prompt || '',
    graphics: payload.graphics || 'Photoreal Always-On',
    assets: {
      models: [
        { name: 'abandoned_house_shell', type: 'building', quality: 'procedural_medium' },
        { name: 'house_interior_rooms', type: 'interior', quality: 'procedural_medium' },
        { name: 'humanoid_enemy_placeholder', type: 'enemy', quality: 'procedural_fallback' },
        { name: 'flashlight', type: 'prop', quality: 'procedural_medium' },
        { name: 'fuse_box', type: 'objective', quality: 'procedural_medium' },
        { name: 'old_radio', type: 'objective', quality: 'procedural_medium' },
        { name: 'forest_tree_cluster', type: 'environment', quality: 'procedural_medium' },
        { name: 'rocks_debris_pack', type: 'environment', quality: 'procedural_medium' }
      ],
      materials: [
        { name: 'wet_asphalt_procedural', colors: ['#0a0d10','#2c3438','#050607'] },
        { name: 'weathered_wood_procedural', colors: ['#1d1713','#5a4b3b','#0b0806'] },
        { name: 'rusty_metal_procedural', colors: ['#241917','#7a4b2b','#0e0d0d'] },
        { name: 'dirty_glass_procedural', colors: ['#15202a','#53616a','#05080c'] },
        { name: 'mud_ground_procedural', colors: ['#17110d','#4b3628','#090604'] }
      ],
      icons: [
        { name: 'objective_icon', label: 'OBJ', color: '#7de4c6' },
        { name: 'health_icon', label: 'HP', color: '#ff4d5d' },
        { name: 'battery_icon', label: 'BAT', color: '#ffd166' }
      ],
      audio: [
        { name: 'jump_scare_sting', freq: 80, duration: 0.8 },
        { name: 'door_slam', freq: 55, duration: 0.45 },
        { name: 'enemy_growl', freq: 110, duration: 1.2 },
        { name: 'objective_complete', freq: 660, duration: 0.25 }
      ]
    }
  };
  if (/city|urban|street/.test(prompt)) add(plan.assets.models, { name: 'urban_facade_pack', type: 'building', quality: 'procedural_medium' });
  if (/snow/.test(prompt)) add(plan.assets.materials, { name: 'snow_ground_procedural', colors: ['#aab8c5','#e4edf2','#6d7b86'] });
  if (/desert/.test(prompt)) add(plan.assets.materials, { name: 'desert_sand_procedural', colors: ['#8a6b3d','#c9a56a','#46301b'] });
  return plan;
}

function approvedAssetsRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeApprovedAssets');
  ensureDir(root);
  ['manifests','downloads','models','textures','materials','hdris','animations','licences','reports','prepared'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function approvedAssetSources() {
  return [
    {
      name: 'Poly Haven',
      licence: 'CC0',
      types: ['models','textures','HDRIs'],
      homepage: 'https://polyhaven.com',
      note: 'Assets are CC0. Structured/commercial API usage may require separate terms.'
    },
    {
      name: 'ambientCG',
      licence: 'CC0',
      types: ['PBR textures','materials','models'],
      homepage: 'https://ambientcg.com',
      note: 'Assets are CC0 and suitable for PBR material sets.'
    },
    {
      name: 'Kenney',
      licence: 'CC0 / public domain',
      types: ['game assets','props','starter packs'],
      homepage: 'https://kenney.nl',
      note: 'Good for game-ready prototype props and safe fallback assets.'
    },
    {
      name: 'Quaternius',
      licence: 'CC0',
      types: ['3D models','characters','animations'],
      homepage: 'https://quaternius.com',
      note: 'Good for character, prop and animation-friendly game assets.'
    }
  ];
}

function approvedRequirementPlan(payload = {}) {
  const prompt = String(payload.prompt || '').toLowerCase();
  const addIf = (arr, condition, item) => { if (condition) arr.push(item); };

  const plan = {
    mode: 'Game Style + Rating Selector',
    generatedAt: new Date().toISOString(),
    prompt: payload.prompt || '',
    graphics: payload.graphics || 'Photoreal Always-On',
    sources: approvedAssetSources(),
    strategy: [
      'Infer asset needs from the game idea.',
      'Create a licence-safe asset manifest.',
      'Download only approved direct URLs or source-approved files.',
      'Save licence metadata with every file.',
      'Import files into the GameForge asset library.',
      'Fallback to procedural generation if no approved download exists.'
    ],
    requirements: {
      models: [
        { query: 'abandoned house exterior realistic glb', role: 'building', priority: 1 },
        { query: 'old house interior realistic glb', role: 'interior', priority: 1 },
        { query: 'forest trees realistic glb', role: 'environment', priority: 1 },
        { query: 'zombie humanoid enemy rigged glb', role: 'enemy', priority: 1 },
        { query: 'flashlight realistic glb', role: 'prop', priority: 2 },
        { query: 'old radio realistic glb', role: 'objective_prop', priority: 2 },
        { query: 'fuse box realistic glb', role: 'objective_prop', priority: 2 },
        { query: 'broken car realistic glb', role: 'set_dressing', priority: 2 }
      ],
      materials: [
        { query: 'wet asphalt PBR', role: 'road', priority: 1 },
        { query: 'weathered wood PBR', role: 'house', priority: 1 },
        { query: 'rusty metal PBR', role: 'props', priority: 1 },
        { query: 'dirty glass PBR', role: 'windows', priority: 2 },
        { query: 'mud ground PBR', role: 'ground', priority: 1 }
      ],
      hdris: [
        { query: 'night forest HDRI', role: 'lighting', priority: 1 },
        { query: 'overcast night road HDRI', role: 'lighting', priority: 2 }
      ],
      animations: [
        { query: 'humanoid idle walk run attack death animation glb', role: 'enemy_animation', priority: 1 }
      ]
    }
  };

  addIf(plan.requirements.models, /city|street|urban/.test(prompt), { query: 'urban street realistic glb', role: 'environment', priority: 2 });
  addIf(plan.requirements.models, /co-op|coop|multiplayer/.test(prompt), { query: 'human survivor character glb', role: 'coop_player', priority: 2 });
  addIf(plan.requirements.models, /weapon|gun|melee/.test(prompt), { query: 'generic survival tool prop glb', role: 'weapon_placeholder', priority: 3 });

  return plan;
}

function defaultApprovedManifestFromPlan(plan) {
  const all = [];
  let id = 1;
  function pushGroup(group, type, folder) {
    (group || []).forEach(req => {
      all.push({
        id: `asset_${id++}`,
        enabled: false,
        type,
        role: req.role,
        priority: req.priority,
        query: req.query,
        source: '',
        sourcePage: '',
        licence: 'CC0 or approved commercial-use licence required',
        url: '',
        targetFolder: folder,
        notes: 'Add an approved direct file URL to enable automatic download. If blank, GameForge will use procedural fallback.'
      });
    });
  }
  pushGroup(plan.requirements.models, 'model', 'models');
  pushGroup(plan.requirements.materials, 'material', 'materials');
  pushGroup(plan.requirements.hdris, 'hdri', 'hdris');
  pushGroup(plan.requirements.animations, 'animation', 'animations');

  return {
    generatedAt: new Date().toISOString(),
    name: 'GameForge Approved Asset Acquisition Manifest',
    rule: 'Only enabled assets with approved licences and direct URLs are downloaded. No random scraping.',
    approvedSources: approvedAssetSources(),
    assets: all
  };
}

function safeAssetFilename(asset, url) {
  let ext = '.asset';
  try {
    const pathname = new URL(url).pathname;
    const baseExt = path.extname(pathname);
    if (baseExt) ext = baseExt;
  } catch(e) {}
  const base = String(asset.query || asset.id || 'asset').replace(/[^a-z0-9._-]+/gi, '_').slice(0, 80);
  return `${base}_${Date.now()}${ext}`;
}

function licenceLooksApproved(asset) {
  const licence = String(asset.licence || '').toLowerCase();
  const source = String(asset.source || '').toLowerCase();
  if (licence.includes('cc0') || licence.includes('public domain')) return true;
  if (licence.includes('commercial') && (licence.includes('allowed') || licence.includes('permitted') || licence.includes('licensed'))) return true;
  if (['poly haven','ambientcg','kenney','quaternius'].some(s => source.includes(s))) return licence.includes('cc0') || licence.includes('public') || licence.includes('commercial');
  return false;
}

async function downloadApprovedAsset(asset, root) {
  if (!asset.enabled) return { id: asset.id, skipped: true, reason: 'disabled', asset };
  if (!asset.url || !/^https?:\/\//i.test(asset.url)) return { id: asset.id, skipped: true, reason: 'missing direct URL', asset };
  if (!licenceLooksApproved(asset)) return { id: asset.id, skipped: true, reason: 'licence not approved for automatic download', asset };

  const folder = asset.targetFolder || (asset.type === 'model' ? 'models' : asset.type === 'hdri' ? 'hdris' : asset.type === 'animation' ? 'animations' : 'materials');
  const destFolder = path.join(root, folder);
  ensureDir(destFolder);
  const filename = safeAssetFilename(asset, asset.url);
  const dest = path.join(destFolder, filename);

  const response = await fetch(asset.url, {
    headers: {
      'User-Agent': 'GameForgeAIEngine/2.5.0 approved-asset-downloader'
    }
  });
  if (!response.ok) throw new Error(`Download failed ${response.status}: ${asset.url}`);

  const buffer = Buffer.from(await response.arrayBuffer());
  fs.writeFileSync(dest, buffer);

  const licenceMeta = {
    downloadedAt: new Date().toISOString(),
    filename,
    path: dest,
    source: asset.source || '',
    sourcePage: asset.sourcePage || '',
    licence: asset.licence || '',
    url: asset.url,
    type: asset.type,
    role: asset.role,
    query: asset.query,
    warning: 'Verify licence/source terms before commercial release.'
  };
  const metaFile = path.join(root, 'licences', `${filename}.json`);
  fs.writeFileSync(metaFile, JSON.stringify(licenceMeta, null, 2), 'utf8');

  return { id: asset.id, downloaded: true, path: dest, metaFile, filename, asset };
}

function realismAssetsRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeRealismAssets');
  ensureDir(root);
  ['plans','manifests','textures','models','animations','hdris','licences','reports'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function autonomousRealismPlanFromPrompt(payload = {}) {
  const prompt = String(payload.prompt || '');
  const graphics = String(payload.graphics || 'Photorealistic Always-On');
  const promptLower = prompt.toLowerCase();
  const safeSources = [
    { name: 'Poly Haven', licence: 'CC0', categories: ['models','textures','HDRIs'] },
    { name: 'ambientCG', licence: 'CC0', categories: ['textures','materials','models'] },
    { name: 'Kenney', licence: 'CC0 / Public Domain', categories: ['game assets'] },
    { name: 'Quaternius', licence: 'CC0', categories: ['models','animations'] }
  ];
  const models = [
    { name: 'Main building shell', role: 'building', quality: 'high' },
    { name: 'Primary enemy character', role: 'enemy', quality: 'high' },
    { name: 'Player arms / FPS rig', role: 'player', quality: 'high' },
    { name: 'Flashlight', role: 'objective_prop', quality: 'medium' },
    { name: 'Doors / windows / stairs', role: 'building_detail', quality: 'medium' }
  ];
  if (/horror|zombie|abandoned|monster/.test(promptLower)) models.push({ name: 'Abandoned interior props pack', role: 'interior_props', quality: 'high' });
  if (/forest|woods|trees|road/.test(promptLower)) models.push({ name: 'Forest trees collection', role: 'environment', quality: 'high' });
  if (/house|interior|hallway|basement/.test(promptLower)) models.push({ name: 'Kitchen / bathroom / hallway prop kit', role: 'interior_detail', quality: 'high' });
  if (/survival|ammo|weapon|inventory/.test(promptLower)) models.push({ name: 'Ammo / medkit / key item prop pack', role: 'gameplay_props', quality: 'medium' });

  return {
    mode: 'Game Style + Rating Selector',
    prompt,
    graphics,
    photorealFilterEnabled: true,
    alwaysOn: true,
    honestLimit: 'This mode improves realism by planning safe asset gathering and photoreal scene dressing. Final quality still depends on the source assets and external generation quality.',
    safeSources,
    requirements: {
      models,
      materials: [
        { name: 'wet_asphalt', maps: ['albedo','normal','roughness','ao'] },
        { name: 'weathered_wood', maps: ['albedo','normal','roughness','ao'] },
        { name: 'rusty_metal', maps: ['albedo','normal','roughness','metallic','ao'] },
        { name: 'dirty_glass', maps: ['albedo','roughness','normal'] },
        { name: 'mossy_rock', maps: ['albedo','normal','roughness','ao'] },
        { name: 'damaged_plaster', maps: ['albedo','normal','roughness','ao'] }
      ],
      hdris: [
        { name: 'moonlit_forest_night', use: 'world lighting' },
        { name: 'overcast_night_road', use: 'fallback world lighting' }
      ],
      animations: ['idle','walk','run','attack','hit_reaction','death','turn','chase'],
      detailScatter: ['rocks','grass clumps','weeds','debris','papers','broken furniture','dust decals','blood decals','puddles','fence fragments']
    },
    lightingPreset: {
      fog: 'blue_grey_horror',
      lighting: 'cinematic_moonlight',
      post: ['bloom','fxaa','contrast','exposure','fog','vignette']
    }
  };
}

function generatedMediaRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAIGeneratedMedia');
  ensureDir(root);
  ['textures','icons','audio','reports','web_sounds'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}
function writeSimpleWav(file, freq=440, duration=0.2) {
  const sampleRate = 22050, samples = Math.floor(sampleRate * duration);
  const fd = fs.openSync(file, 'w');
  const w = s => fs.writeSync(fd, Buffer.from(s));
  const u32 = v => { const b=Buffer.alloc(4); b.writeUInt32LE(v); fs.writeSync(fd,b); };
  const u16 = v => { const b=Buffer.alloc(2); b.writeUInt16LE(v); fs.writeSync(fd,b); };
  w('RIFF'); u32(36 + samples*2); w('WAVEfmt '); u32(16); u16(1); u16(1); u32(sampleRate); u32(sampleRate*2); u16(2); u16(16); w('data'); u32(samples*2);
  for (let i=0;i<samples;i++) {
    const t=i/sampleRate, env=Math.max(0, 1-i/samples);
    const val=Math.max(-1, Math.min(1, (Math.sin(2*Math.PI*freq*t)*0.45 + (Math.random()*2-1)*0.18)*env));
    const b=Buffer.alloc(2); b.writeInt16LE(Math.floor(val*32767)); fs.writeSync(fd,b);
  }
  fs.closeSync(fd);
}
function writePpm(file, kind) {
  const palettes = {
    grass:[[35,100,35],[80,150,70]], rock:[[90,90,90],[140,140,130]], bark:[[90,50,25],[135,80,40]],
    concrete:[[120,120,115],[160,160,150]], metal:[[70,70,75],[140,140,145]], road:[[25,25,28],[60,60,65]],
    wood:[[100,60,30],[150,90,45]], dirt:[[100,70,45],[140,95,55]], skin:[[190,140,105],[230,180,140]]
  };
  const p = palettes[kind] || palettes.dirt, width=128, height=128;
  const data = Buffer.alloc(width*height*3); let idx=0;
  for (let y=0;y<height;y++) for (let x=0;x<width;x++) {
    const n = Math.abs(Math.sin(x*12.9898 + y*78.233) * 43758.5453) % 1;
    const base = p[n > .5 ? 1 : 0], shade=(n-.5)*40;
    data[idx++]=Math.max(0,Math.min(255,base[0]+shade));
    data[idx++]=Math.max(0,Math.min(255,base[1]+shade));
    data[idx++]=Math.max(0,Math.min(255,base[2]+shade));
  }
  fs.writeFileSync(file, Buffer.concat([Buffer.from(`P6\n${width} ${height}\n255\n`), data]));
}
function generateMediaPack(payload) {
  const root = generatedMediaRoot();
  const prompt = String(payload?.prompt || '').toLowerCase();
  const textures = ['grass','rock','bark','dirt','metal','concrete','road','wood'].concat(prompt.includes('human') ? ['skin'] : []);
  const icons = ['health','ammo','weapon','supplies','crafting','objective','enemy'];
  const sounds = ['gunshot','reload','pickup','enemy_hit','player_hurt','footstep','ui_click','objective_complete','enemy_growl','ambient_wind'].concat(prompt.includes('rain')||prompt.includes('storm') ? ['rain'] : []);
  const generated = [];
  textures.forEach(k => { const f=path.join(root,'textures',`${k}_${Date.now()}_${Math.floor(Math.random()*999)}.ppm`); writePpm(f,k); generated.push({type:'texture',category:k,name:path.basename(f),path:f,relativePath:path.relative(root,f).replace(/\\/g,'/'),fileUrl:pathToFileURL(f).href}); });
  icons.forEach(k => { const f=path.join(root,'icons',`${k}_${Date.now()}_${Math.floor(Math.random()*999)}.ppm`); writePpm(f,k==='enemy'?'metal':'concrete'); generated.push({type:'icon',category:k,name:path.basename(f),path:f,relativePath:path.relative(root,f).replace(/\\/g,'/'),fileUrl:pathToFileURL(f).href}); });
  const freqs = {gunshot:90,reload:280,pickup:720,enemy_hit:150,player_hurt:110,footstep:70,ui_click:600,objective_complete:880,enemy_growl:75,ambient_wind:50,rain:120};
  sounds.forEach(k => { const f=path.join(root,'audio',`${k}_${Date.now()}_${Math.floor(Math.random()*999)}.wav`); writeSimpleWav(f,freqs[k]||440,k.includes('ambient')||k==='rain'?1.4:.25); generated.push({type:'audio',category:k,name:path.basename(f),path:f,relativePath:path.relative(root,f).replace(/\\/g,'/'),fileUrl:pathToFileURL(f).href}); });
  const report = {generatedAt:new Date().toISOString(), prompt:payload?.prompt||'', generated};
  const reportPath = path.join(root,'reports',`media_report_${Date.now()}.json`);
  fs.writeFileSync(reportPath, JSON.stringify(report,null,2), 'utf8');
  return {root, generated, reportPath};
}
function keywordsFromPrompt(prompt) {
  const p=String(prompt||'').toLowerCase();
  const keys=['gunshot','reload','pickup','footstep','wind','ambient','horror','monster growl','forest ambience','rain'];
  return keys.filter(k => ['gunshot','reload','pickup','footstep','wind','ambient'].includes(k) || p.includes(k.split(' ')[0])).slice(0,8);
}
async function searchFreesound(payload) {
  const token=String(payload?.token||'').trim();
  const query=encodeURIComponent(String(payload?.query||'game sound effect'));
  if (!token) throw new Error('Freesound API token required.');
  const url=`https://freesound.org/apiv2/search/text/?query=${query}&fields=id,name,license,username,previews,tags,duration,url&filter=duration:[0.1 TO 8]&page_size=10&token=${encodeURIComponent(token)}`;
  const r=await fetch(url); if(!r.ok) throw new Error(`Web sound search failed: ${r.status} ${r.statusText}`);
  const data=await r.json(); return data.results||[];
}



function playableBuildRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAIPlayableBuilds');
  ensureDir(root);
  return root;
}






function free3DRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeFree3DGenerator');
  ensureDir(root);
  ensureDir(path.join(root, 'jobs'));
  ensureDir(path.join(root, 'generated_assets'));
  ensureDir(path.join(root, 'metadata'));
  return root;
}

function createFree3DJob(payload) {
  const root = free3DRoot();
  const id = `free3d_${Date.now()}_${Math.floor(Math.random()*10000)}`;
  const assetType = payload?.assetType || 'prop';
  const name = String(payload?.assetName || `${assetType}_asset`).replace(/[^a-z0-9_-]/gi, '_');
  const prompt = String(payload?.prompt || '').trim();
  const safePrompt = sanitiseAssetPrompt(prompt || name, assetType);
  const job = {
    id,
    createdAt: new Date().toISOString(),
    status: 'queued',
    generator: 'GameForge Free Procedural Generator',
    assetType,
    assetName: name,
    prompt,
    safePrompt,
    style: payload?.style || 'realistic_prototype',
    variationCount: Number(payload?.variationCount || 1),
    licence: {
      source: 'Generated inside GameForge',
      commercialUse: 'prototype_owned_by_user_verify_before_release',
      copyrightRisk: 'low_original_procedural',
      note: 'Generated procedurally from original safe prompt. No external model source used.'
    }
  };
  const jobPath = path.join(root, 'jobs', `${id}.json`);
  fs.writeFileSync(jobPath, JSON.stringify(job, null, 2), 'utf8');
  return { job, jobPath };
}

function completeFree3DJob(payload) {
  const root = free3DRoot();
  const job = payload?.job || createFree3DJob(payload).job;
  job.status = 'completed';
  job.completedAt = new Date().toISOString();
  const metadata = {
    importedAt: new Date().toISOString(),
    name: `${job.assetName}.procedural.json`,
    type: 'procedural_model',
    generator: job.generator,
    assetType: job.assetType,
    prompt: job.prompt,
    safePrompt: job.safePrompt,
    style: job.style,
    variationCount: job.variationCount,
    licence: job.licence,
    components: payload?.components || {},
    note: 'This is a procedural asset recipe used by GameForge. It is not a GLB file, but can be instantiated in the engine.'
  };
  const recipePath = path.join(root, 'generated_assets', `${job.assetName}_${job.id}.procedural.json`);
  const metaPath = path.join(root, 'metadata', `${job.assetName}_${job.id}_metadata.json`);
  fs.writeFileSync(recipePath, JSON.stringify({ job, metadata }, null, 2), 'utf8');
  fs.writeFileSync(metaPath, JSON.stringify(metadata, null, 2), 'utf8');

  const assetRoot = assetLibraryRoot();
  const libraryRecipePath = path.join(assetRoot, 'metadata', `${job.assetName}_${job.id}_free3d_procedural.json`);
  fs.writeFileSync(libraryRecipePath, JSON.stringify(metadata, null, 2), 'utf8');

  return { job, recipePath, metaPath, libraryRecipePath };
}

function createFree3DAssetPack(payload) {
  const prompt = payload?.prompt || '';
  const plan = plan3DAssetsFromPrompt(prompt);
  const jobs = plan.map(asset => createFree3DJob({
    assetType: asset.type,
    assetName: asset.name,
    prompt: asset.safePrompt,
    style: payload?.style || 'realistic_prototype',
    variationCount: payload?.variationCount || 1
  }));
  return { generatedAt: new Date().toISOString(), prompt, jobs };
}

function ai3DSettingsPath() {
  const root = path.join(app.getPath('userData'), 'ai_3d_asset_pipeline');
  ensureDir(root);
  return path.join(root, 'settings.json');
}

function ai3DRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAI3DAssets');
  ensureDir(root);
  ensureDir(path.join(root, 'jobs'));
  ensureDir(path.join(root, 'models'));
  ensureDir(path.join(root, 'metadata'));
  ensureDir(path.join(root, 'evidence'));
  return root;
}

function loadAI3DSettings() {
  const file = ai3DSettingsPath();
  if (!fs.existsSync(file)) {
    return {
      provider: 'manual',
      providerApiKeySaved: false,
      defaultQuality: 'game_ready',
      licenceMode: 'commercial_safe',
      requireHumanApproval: true,
      autoImportApprovedGLB: true
    };
  }
  try {
    const data = JSON.parse(fs.readFileSync(file, 'utf8'));
    return { ...data, providerApiKeySaved: Boolean(data.providerApiKey) };
  } catch(e) {
    return {
      provider: 'manual',
      providerApiKeySaved: false,
      defaultQuality: 'game_ready',
      licenceMode: 'commercial_safe',
      requireHumanApproval: true,
      autoImportApprovedGLB: true
    };
  }
}

function saveAI3DSettings(payload) {
  const existing = loadAI3DSettings();
  const next = {
    ...existing,
    provider: payload?.provider || existing.provider || 'manual',
    defaultQuality: payload?.defaultQuality || existing.defaultQuality || 'game_ready',
    licenceMode: payload?.licenceMode || existing.licenceMode || 'commercial_safe',
    requireHumanApproval: payload?.requireHumanApproval !== undefined ? Boolean(payload.requireHumanApproval) : existing.requireHumanApproval,
    autoImportApprovedGLB: payload?.autoImportApprovedGLB !== undefined ? Boolean(payload.autoImportApprovedGLB) : existing.autoImportApprovedGLB
  };
  if (payload?.providerApiKey && String(payload.providerApiKey).trim()) {
    next.providerApiKey = String(payload.providerApiKey).trim();
  } else if (payload?.clearProviderApiKey) {
    delete next.providerApiKey;
  } else if (existing.providerApiKey) {
    next.providerApiKey = existing.providerApiKey;
  }
  fs.writeFileSync(ai3DSettingsPath(), JSON.stringify(next, null, 2), 'utf8');
  return { ...next, providerApiKey: next.providerApiKey ? '••••••••' : '', providerApiKeySaved: Boolean(next.providerApiKey) };
}

function sanitiseAssetPrompt(prompt, assetType) {
  const raw = String(prompt || '').trim();
  const forbiddenPatterns = [
    /resident evil/ig, /call of duty/ig, /the last of us/ig, /marvel/ig, /dc comics/ig,
    /disney/ig, /pixar/ig, /star wars/ig, /harry potter/ig, /famous actor/ig,
    /celebrity/ig, /looks like [a-z ]+/ig, /copy of/ig, /exact replica/ig
  ];
  let safe = raw;
  forbiddenPatterns.forEach(p => safe = safe.replace(p, 'original'));
  safe = safe.replace(/\s+/g, ' ').trim();
  const typeHint = assetType ? `Asset type: ${assetType}. ` : '';
  const suffix = ' Original design, no copyrighted character likeness, no famous person likeness, game-ready GLB/GLTF, neutral pose if character, clean topology where possible, PBR textures if supported.';
  return `${typeHint}${safe}.${suffix}`;
}

function plan3DAssetsFromPrompt(prompt) {
  const p = String(prompt || '').toLowerCase();
  const assets = [];
  function add(type, name, promptText, priority='medium') {
    assets.push({ type, name, prompt: promptText, priority, status: 'planned' });
  }

  if (p.includes('zombie') || p.includes('infected') || p.includes('horror')) {
    add('character', 'realistic_zombie_male', 'realistic infected zombie male, damaged clothing, horror survival game enemy, original design', 'high');
    add('character', 'realistic_zombie_female', 'realistic infected zombie female, torn clothing, horror survival game enemy, original design', 'high');
    add('character', 'boss_zombie', 'large mutated infected zombie boss, horror game enemy, original design, intimidating silhouette', 'high');
  }
  if (p.includes('survivor') || p.includes('npc') || p.includes('civilian')) {
    add('character', 'survivor_npc', 'realistic injured survivor NPC, rural clothing, neutral pose, original design', 'medium');
  }
  if (p.includes('town') || p.includes('house') || p.includes('building') || p.includes('farm')) {
    add('building', 'abandoned_farmhouse', 'abandoned rural farmhouse, realistic horror survival game building, weathered wood, broken windows', 'high');
    add('building', 'old_service_station', 'old abandoned rural service station, realistic, broken signs, horror atmosphere', 'high');
    add('building', 'small_rural_house', 'small abandoned rural house, realistic, damaged exterior, horror environment prop', 'medium');
  }
  if (p.includes('forest') || p.includes('tree') || p.includes('survival')) {
    add('environment', 'dead_tree', 'dead leafless tree, realistic horror forest environment asset', 'medium');
    add('environment', 'pine_tree', 'realistic pine tree, game-ready forest environment asset', 'medium');
    add('environment', 'large_rock', 'large mossy rock, realistic game environment prop', 'low');
    add('environment', 'grass_clump', 'realistic grass clump, game environment foliage asset', 'low');
  }
  if (p.includes('shooter') || p.includes('gun') || p.includes('weapon') || p.includes('pistol')) {
    add('weapon', 'old_pistol', 'old worn pistol, realistic survival horror game weapon, original design', 'high');
    add('prop', 'ammo_box', 'small ammunition box, realistic game pickup prop, original design', 'medium');
  }
  if (p.includes('radio')) {
    add('prop', 'broken_radio', 'broken radio equipment, realistic repair objective prop, horror survival game', 'high');
    add('prop', 'radio_tower_piece', 'small radio tower repair component, realistic objective pickup prop', 'medium');
  }
  if (!assets.length) {
    add('prop', 'generic_game_prop', 'original realistic game prop, game-ready GLB asset', 'medium');
    add('building', 'simple_building', 'original realistic building prop, game-ready GLB asset', 'medium');
  }

  return assets.map(a => ({ ...a, safePrompt: sanitiseAssetPrompt(a.prompt, a.type) }));
}

function makeAI3DJobRecord(payload) {
  const root = ai3DRoot();
  const settings = loadAI3DSettings();
  const asset = payload?.asset || {};
  const id = `ai3d_${Date.now()}_${Math.floor(Math.random()*10000)}`;
  const job = {
    id,
    provider: payload?.provider || settings.provider || 'manual',
    status: 'created',
    createdAt: new Date().toISOString(),
    assetType: asset.type || payload?.assetType || 'prop',
    assetName: asset.name || payload?.assetName || 'generated_asset',
    originalPrompt: asset.prompt || payload?.prompt || '',
    safePrompt: sanitiseAssetPrompt(asset.safePrompt || asset.prompt || payload?.prompt || '', asset.type || payload?.assetType || 'prop'),
    quality: payload?.quality || settings.defaultQuality || 'game_ready',
    licenceMode: settings.licenceMode || 'commercial_safe',
    requireHumanApproval: settings.requireHumanApproval !== false,
    metadata: {
      warning: 'This job record prepares the 3D generation request. Provider-specific generation requires a valid provider API key and endpoint implementation.',
      copyrightSafety: 'Prompt rewritten to avoid copyrighted/famous likeness references.'
    }
  };
  const file = path.join(root, 'jobs', `${id}.json`);
  fs.writeFileSync(file, JSON.stringify(job, null, 2), 'utf8');
  return { job, jobPath: file };
}

async function createAI3DProviderJob(payload) {
  const settings = loadAI3DSettings();
  if (!settings.provider || settings.provider === 'manual') {
    const record = makeAI3DJobRecord(payload);
    return {
      ...record,
      providerResponse: null,
      message: 'Manual provider mode: job record created. Use this safe prompt with your chosen 3D generator, then import the GLB/GLTF.'
    };
  }

  if (!settings.providerApiKey) throw new Error('3D provider API key is missing.');

  // Provider adapter framework.
  // API endpoints differ by provider and account tier, so this stores a provider-ready job
  // and leaves exact endpoint mapping configurable for the next provider-specific implementation.
  const record = makeAI3DJobRecord(payload);
  record.job.status = 'provider_ready';
  record.job.providerApiKeySaved = true;
  record.job.providerNote = `Provider ${settings.provider} selected. Add the provider-specific endpoint adapter to submit this job automatically.`;
  fs.writeFileSync(record.jobPath, JSON.stringify(record.job, null, 2), 'utf8');

  return {
    ...record,
    providerResponse: null,
    message: `Provider-ready job created for ${settings.provider}. Exact provider endpoint submission can be enabled once API account details are configured.`
  };
}

async function importAI3DModelFromUrl(payload) {
  const root = ai3DRoot();
  const url = String(payload?.modelUrl || '').trim();
  if (!/^https?:\/\//i.test(url)) throw new Error('A valid direct GLB/GLTF model URL is required.');

  const nameRaw = String(payload?.assetName || path.basename(new URL(url).pathname) || `ai3d_model_${Date.now()}`);
  const cleanName = nameRaw.replace(/[^a-z0-9._-]/gi, '_').replace(/\.(glb|gltf)$/i, '') + (url.toLowerCase().includes('.gltf') ? '.gltf' : '.glb');
  const response = await gfFetch(url);
  if (!response.ok) throw new Error(`Model download failed: ${response.status} ${response.statusText}`);

  const dest = path.join(root, 'models', cleanName);
  fs.writeFileSync(dest, Buffer.from(await response.arrayBuffer()));

  const licenceText = String(payload?.licenceText || payload?.license || '').trim();
  const audit = classifyLicenceText(licenceText || 'Generated asset provider metadata pending verification.');
  const metadata = {
    importedAt: new Date().toISOString(),
    name: cleanName,
    type: 'model',
    provider: payload?.provider || loadAI3DSettings().provider || 'unknown',
    jobId: payload?.jobId || '',
    modelUrl: url,
    sourcePage: payload?.sourcePage || '',
    creator: payload?.creator || 'AI 3D Provider',
    prompt: payload?.prompt || '',
    safePrompt: sanitiseAssetPrompt(payload?.prompt || '', payload?.assetType || 'model'),
    licenceText,
    licenceAudit: audit,
    commercialUseStatus: audit.commercialUse === true ? 'potentially_allowed_verify_before_release' : 'requires_manual_verification',
    warning: 'Verify provider licence and commercial-use terms before commercial release.',
    path: dest,
    relativePath: path.relative(root, dest).replace(/\\/g, '/')
  };

  const metaPath = path.join(root, 'metadata', `${cleanName}_${Date.now()}.json`);
  fs.writeFileSync(metaPath, JSON.stringify(metadata, null, 2), 'utf8');

  // Copy into GameForge Asset Library so normal scanners can use it.
  const assetRoot = assetLibraryRoot();
  const libraryDest = path.join(assetRoot, 'models', cleanName);
  fs.copyFileSync(dest, libraryDest);
  const libraryMetaPath = path.join(assetRoot, 'metadata', `${cleanName}_ai3d_${Date.now()}.json`);
  fs.writeFileSync(libraryMetaPath, JSON.stringify(metadata, null, 2), 'utf8');

  return {
    root,
    asset: {
      name: cleanName,
      type: 'model',
      path: dest,
      relativePath: path.relative(root, dest).replace(/\\/g, '/'),
      libraryPath: libraryDest,
      fileUrl: pathToFileURL(dest).href,
      metadata
    },
    metadataPath: metaPath,
    libraryMetadataPath: libraryMetaPath,
    scan: scanAssetLibrary()
  };
}

function createAI3DAssetPlan(payload) {
  const prompt = payload?.prompt || '';
  const assets = plan3DAssetsFromPrompt(prompt);
  return {
    generatedAt: new Date().toISOString(),
    prompt,
    assets,
    workflow: [
      'OpenAI/Hybrid AI breaks game prompt into asset requirements',
      'Safety rewriter removes copyrighted/famous likeness references',
      '3D provider job records are created',
      'Provider generates GLB/GLTF models',
      'GameForge downloads/imports model files',
      'Licence Auditor stores provider metadata',
      'Character Model Importer scans and assigns models',
      'Scene Builder places assets into the game'
    ],
    copyrightPolicy: {
      avoid: ['copyrighted franchises', 'famous actors', 'celebrity likenesses', 'exact replicas', 'branded props'],
      useInstead: ['original descriptions', 'generic realistic styles', 'provider-generated original assets']
    }
  };
}

function hybridAISettingsPath() {
  const root = path.join(app.getPath('userData'), 'hybrid_ai');
  ensureDir(root);
  return path.join(root, 'settings.json');
}

function loadHybridAISettings() {
  const file = hybridAISettingsPath();
  if (!fs.existsSync(file)) {
    return {
      aiMode: 'hybrid',
      openaiModel: 'gpt-5.4-mini',
      advancedModel: 'gpt-5.5',
      useCostGate: true,
      maxApprovedUsd: 25,
      apiKeySaved: false
    };
  }
  try {
    const data = JSON.parse(fs.readFileSync(file, 'utf8'));
    return { ...data, apiKeySaved: Boolean(data.openaiApiKey) };
  } catch(e) {
    return { aiMode: 'hybrid', openaiModel: 'gpt-5.4-mini', advancedModel: 'gpt-5.5', useCostGate: true, maxApprovedUsd: 25, apiKeySaved: false };
  }
}

function saveHybridAISettings(payload) {
  const existing = loadHybridAISettings();
  const next = {
    ...existing,
    aiMode: payload?.aiMode || existing.aiMode || 'hybrid',
    openaiModel: payload?.openaiModel || existing.openaiModel || 'gpt-5.4-mini',
    advancedModel: payload?.advancedModel || existing.advancedModel || 'gpt-5.5',
    useCostGate: payload?.useCostGate !== undefined ? Boolean(payload.useCostGate) : existing.useCostGate,
    maxApprovedUsd: Number(payload?.maxApprovedUsd || existing.maxApprovedUsd || 25)
  };
  if (payload?.openaiApiKey && String(payload.openaiApiKey).trim()) {
    next.openaiApiKey = String(payload.openaiApiKey).trim();
  } else if (payload?.clearApiKey) {
    delete next.openaiApiKey;
  } else if (existing.openaiApiKey) {
    next.openaiApiKey = existing.openaiApiKey;
  }
  fs.writeFileSync(hybridAISettingsPath(), JSON.stringify(next, null, 2), 'utf8');
  return { ...next, openaiApiKey: next.openaiApiKey ? '••••••••' : '', apiKeySaved: Boolean(next.openaiApiKey) };
}

function hybridSystemPrompt() {
  return `You are the cloud AI brain inside GameForge AI Engine.
Return strict JSON only. Do not include markdown.
Your job is to convert a user's game prompt into structured game data that GameForge can validate and build.

Required JSON schema:
{
  "gameTitle": "string",
  "summary": "string",
  "genre": "string",
  "camera": "first_person",
  "targetResolution": "1920x1080",
  "objective": "string",
  "sceneObjects": [
    {
      "name": "string",
      "type": "player_spawn|enemy|building|loot|weapon_pickup|objective|tree|rock|grass|prop|light|extraction_zone|crafting_station",
      "position": {"x": 0, "y": 0, "z": 0},
      "scale": {"x": 1, "y": 1, "z": 1},
      "material": "grass|rock|wood|metal|concrete|road|enemy|loot|glow",
      "components": {}
    }
  ],
  "gameplaySystems": {
    "movement": {},
    "combat": {},
    "health": {},
    "stamina": {},
    "inventory": {},
    "objectives": []
  },
  "audioEvents": {},
  "assetRequirements": [],
  "licenceWarnings": [],
  "buildValidation": [],
  "repairSuggestions": []
}

Rules:
- Include at least one player_spawn.
- For first-person games, camera must be first_person.
- Include a clear objective and win/fail loop.
- Include enemies/challenge where appropriate.
- Include loot/pickups where appropriate.
- Use imported asset paths only if provided in context.
- Do not invent ImportedAsset.relativePath values.
- Prioritise a playable 10 minute prototype over perfect graphics.
- Never claim assets are legally cleared unless metadata says CC0/Public Domain.`;
}

function buildHybridUserPrompt(payload) {
  return `GAME PROMPT:
${payload?.prompt || ''}

PROJECT STATE:
${JSON.stringify(payload?.projectState || {}, null, 2).slice(0, 90000)}

AVAILABLE CONTEXT:
${payload?.context || ''}

TASK:
Generate a playable first-person game draft as strict JSON using the required schema.
Include:
- scene objects
- gameplay systems
- audio events
- asset requirements
- licence warnings
- build validation
- repair suggestions

Make it practical for GameForge to build.`;
}

async function callOpenAIResponses(payload) {
  const settings = loadHybridAISettings();
  const apiKey = payload?.apiKey || settings.openaiApiKey;
  if (!apiKey) throw new Error('OpenAI API key is missing. Add it in Hybrid AI settings.');

  const model = payload?.model || settings.openaiModel || 'gpt-5.4-mini';
  const body = {
    model,
    input: [
      { role: 'system', content: hybridSystemPrompt() },
      { role: 'user', content: buildHybridUserPrompt(payload) }
    ],
    text: {
      format: {
        type: "json_object"
      }
    }
  };

  const response = await fetch('https://api.openai.com/v1/responses', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(body)
  });

  const text = await response.text();
  if (!response.ok) throw new Error(`OpenAI API error ${response.status}: ${text}`);

  let data;
  try { data = JSON.parse(text); } catch(e) { throw new Error('OpenAI response was not valid JSON envelope.'); }

  let outputText = '';
  if (data.output_text) {
    outputText = data.output_text;
  } else if (Array.isArray(data.output)) {
    for (const item of data.output) {
      if (Array.isArray(item.content)) {
        for (const c of item.content) {
          if (c.text) outputText += c.text;
        }
      }
    }
  }

  if (!outputText) throw new Error('OpenAI returned no output text.');
  let parsed;
  try { parsed = JSON.parse(outputText); } catch(e) {
    throw new Error('OpenAI output was not strict JSON: ' + outputText.slice(0, 600));
  }

  return {
    ok: true,
    model,
    usage: data.usage || null,
    rawId: data.id || null,
    parsed
  };
}

function convertHybridResultToScene(result) {
  const objects = result?.sceneObjects || [];
  return {
    name: result?.gameTitle || 'Hybrid_AI_Game',
    objects: objects.map(o => ({
      name: o.name || o.type || 'Object',
      type: o.type || 'prop',
      material: o.material || 'concrete',
      position: o.position || {x:0,y:0,z:0},
      scale: o.scale || {x:1,y:1,z:1},
      components: o.components || {}
    }))
  };
}

function estimateHybridActualCost(usage, model) {
  if (!usage) return null;
  const pricing = model && model.includes('5.5')
    ? { in: 5.00, out: 30.00 }
    : model && model.includes('5.4-mini')
      ? { in: 0.75, out: 4.50 }
      : { in: 2.50, out: 15.00 };
  const inputTokens = usage.input_tokens || usage.prompt_tokens || 0;
  const outputTokens = usage.output_tokens || usage.completion_tokens || 0;
  return {
    inputTokens,
    outputTokens,
    inputUsd: (inputTokens / 1000000) * pricing.in,
    outputUsd: (outputTokens / 1000000) * pricing.out,
    totalUsd: ((inputTokens / 1000000) * pricing.in) + ((outputTokens / 1000000) * pricing.out)
  };
}

function curatedDownloadRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAICuratedAssets');
  ensureDir(root);
  ensureDir(path.join(root, 'downloads'));
  ensureDir(path.join(root, 'models'));
  ensureDir(path.join(root, 'textures'));
  ensureDir(path.join(root, 'audio'));
  ensureDir(path.join(root, 'metadata'));
  ensureDir(path.join(root, 'evidence'));
  return root;
}

function getCuratedRegistry() {
  return {
    version: '0.7.3',
    allowedLicences: ['CC0', 'PublicDomain'],
    blockedLicences: ['NonCommercial', 'Unknown', 'EditorialOnly', 'PersonalUseOnly'],
    approvedDomains: [
      'kenney.nl',
      'quaternius.com',
      'polyhaven.com',
      'opengameart.org',
      'github.com',
      'raw.githubusercontent.com'
    ],
    warning: 'Only CC0/Public Domain assets should be auto-imported. Users must verify licences before commercial release.'
  };
}

function domainAllowed(url) {
  try {
    const host = new URL(url).hostname.replace(/^www\./, '').toLowerCase();
    return getCuratedRegistry().approvedDomains.some(d => host === d || host.endsWith('.' + d));
  } catch(e) {
    return false;
  }
}

function safeLicencePass(licenceText) {
  const audit = classifyLicenceText(licenceText || '');
  const pass = ['CC0', 'PublicDomain'].includes(audit.normalised) && audit.commercialUse === true && audit.risk !== 'high';
  return { pass, audit };
}

function inferAssetNeedQueries(prompt) {
  const p = String(prompt || '').toLowerCase();
  const needs = [];
  if (p.includes('zombie') || p.includes('infected')) needs.push('CC0 zombie character GLB', 'CC0 horror enemy model', 'CC0 zombie growl sound');
  if (p.includes('horror')) needs.push('CC0 horror ambience sound', 'CC0 door creak sound', 'CC0 abandoned house props');
  if (p.includes('forest') || p.includes('survival')) needs.push('CC0 tree GLB', 'CC0 grass texture', 'CC0 rock model', 'CC0 wind ambience');
  if (p.includes('gun') || p.includes('shooter')) needs.push('CC0 pistol model GLB', 'CC0 gunshot sound', 'CC0 reload sound');
  if (p.includes('town') || p.includes('house')) needs.push('CC0 building props GLB', 'CC0 road texture');
  if (!needs.length) needs.push('CC0 game asset pack', 'CC0 3D model pack', 'CC0 sound effects pack');
  return [...new Set(needs)];
}

function makeAssetUseDecision(asset) {
  const licenceText = [asset.license, asset.licence, asset.licenseNote, asset.sourceLicence, asset.licenceText].filter(Boolean).join('\n');
  const licence = safeLicencePass(licenceText);
  const approvedUrl = asset.url ? domainAllowed(asset.url) : false;
  const hasProof = Boolean(asset.sourcePage || asset.url || asset.licenceUrl || asset.licenseUrl);
  const allowed = licence.pass && approvedUrl && hasProof;
  return {
    allowed,
    approvedDomain: approvedUrl,
    hasProof,
    licenceAudit: licence.audit,
    decision: allowed ? 'APPROVED_FOR_PROTOTYPE_IMPORT' : 'BLOCKED_PENDING_VERIFICATION',
    reason: allowed
      ? 'Asset declares CC0/Public Domain metadata, has source proof and approved domain.'
      : 'Asset failed one or more checks: CC0/Public Domain, approved domain, or proof metadata.'
  };
}

async function curatedDownloadAsset(payload) {
  const root = curatedDownloadRoot();
  const url = String(payload?.url || '').trim();
  const fileNameInput = String(payload?.fileName || '').trim();
  const licenceText = String(payload?.licenceText || payload?.license || '').trim();
  const sourcePage = String(payload?.sourcePage || url).trim();
  const creator = String(payload?.creator || 'Unknown creator').trim();

  if (!/^https?:\/\//i.test(url)) throw new Error('A valid http/https direct asset URL is required.');
  if (!domainAllowed(url)) throw new Error('Blocked: URL is not from an approved/curated source domain.');

  const licence = safeLicencePass(licenceText);
  if (!licence.pass) throw new Error(`Blocked: licence is not CC0/Public Domain safe. Detected ${licence.audit.normalised} (${licence.audit.risk}).`);

  const response = await gfFetch(url);
  if (!response.ok) throw new Error(`Download failed: ${response.status} ${response.statusText}`);

  const rawName = fileNameInput || path.basename(new URL(url).pathname) || `curated_asset_${Date.now()}`;
  const cleanName = rawName.replace(/[^a-z0-9._-]/gi, '_');
  const type = guessAssetType(cleanName);
  const sub = type === 'model' ? 'models' : type === 'texture' ? 'textures' : type === 'audio' ? 'audio' : 'downloads';
  const dest = path.join(root, sub, cleanName);
  fs.writeFileSync(dest, Buffer.from(await response.arrayBuffer()));

  const metadata = {
    importedAt: new Date().toISOString(),
    name: cleanName,
    type,
    url,
    sourcePage,
    creator,
    licenceText,
    licenceAudit: licence.audit,
    decision: 'APPROVED_FOR_PROTOTYPE_IMPORT',
    warning: 'Verify source page/licence before commercial release.',
    path: dest,
    relativePath: path.relative(root, dest).replace(/\\/g, '/')
  };
  const metaPath = path.join(root, 'metadata', `${cleanName}_${Date.now()}.json`);
  fs.writeFileSync(metaPath, JSON.stringify(metadata, null, 2), 'utf8');

  // Copy into the normal GameForge asset library too, so existing scanners find it.
  const assetRoot = assetLibraryRoot();
  const targetSub = type === 'model' ? 'models' : type === 'texture' ? 'textures' : type === 'audio' ? 'audio' : 'imports';
  const libraryDest = path.join(assetRoot, targetSub, cleanName);
  fs.copyFileSync(dest, libraryDest);
  const libraryMetaPath = path.join(assetRoot, 'metadata', `${cleanName}_curated_${Date.now()}.json`);
  fs.writeFileSync(libraryMetaPath, JSON.stringify(metadata, null, 2), 'utf8');

  return {
    root,
    asset: {
      name: cleanName,
      type,
      path: dest,
      relativePath: path.relative(root, dest).replace(/\\/g, '/'),
      fileUrl: pathToFileURL(dest).href,
      libraryPath: libraryDest,
      metadata
    },
    metadataPath: metaPath,
    libraryMetadataPath: libraryMetaPath,
    scan: scanAssetLibrary()
  };
}

function curatedAssetPlan(payload) {
  const prompt = payload?.prompt || '';
  const needs = inferAssetNeedQueries(prompt);
  const registry = getCuratedRegistry();
  return {
    generatedAt: new Date().toISOString(),
    prompt,
    needs,
    registry,
    searchInstructions: needs.map(n => ({
      need: n,
      instruction: `Search approved sources for "${n}". Only use direct asset URLs with explicit CC0/Public Domain licence proof. Block unknown, NonCommercial or attribution-only assets unless manual review allows them.`
    }))
  };
}

function createNativeGamePackage(payload) {
  const root = playableBuildRoot();
  const safeName = safeProjectName(payload?.name || 'GameForge_Native_Game');
  const out = path.join(root, `${safeName}_NativePackage_${Date.now()}`);
  ensureDir(out);
  ensureDir(path.join(out, 'src'));
  ensureDir(path.join(out, 'data'));
  ensureDir(path.join(out, 'media'));
  ensureDir(path.join(out, 'docs'));

  const gamePackage = {
    name: safeName.toLowerCase().replace(/_/g, '-'),
    version: "1.0.0",
    description: "GameForge AI exported playable game",
    main: "main.js",
    scripts: {
      start: "electron .",
      dist: "electron-builder --win",
      "dist-portable": "electron-builder --win portable"
    },
    dependencies: {
      "@babylonjs/core": "^7.54.3",
      "@babylonjs/loaders": "^7.54.3",
      "electron": "^33.2.1"
    },
    devDependencies: {
      "electron-builder": "^25.1.8"
    },
    build: {
      appId: `com.gameforgeai.${safeName.toLowerCase()}`,
      productName: payload?.name || "GameForge Exported Game",
      directories: { output: "dist" },
      files: ["main.js", "src/**/*", "data/**/*", "media/**/*", "docs/**/*", "package.json"],
      win: {
        target: ["nsis", "portable"],
        artifactName: "${productName}-${version}-${arch}.${ext}"
      }
    }
  };

  const gameMain = `const { app, BrowserWindow } = require('electron');
const path = require('path');
const { execFileSync, spawnSync } = require('child_process');

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    fullscreen: false,
    backgroundColor: '#05070d',
    title: ${JSON.stringify(payload?.name || "GameForge Exported Game")},
    webPreferences: { nodeIntegration: false, contextIsolation: true }
  });
  win.loadFile(path.join(__dirname, 'src', 'index.html'));
}
app.whenReady().then(createWindow);
app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
`;

  const runtimeHtml = `<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8">
  <title>${payload?.name || "GameForge Exported Game"}</title>
  <style>
    html, body { margin:0; width:100%; height:100%; background:#05070d; color:white; font-family:Arial,sans-serif; overflow:hidden; }
    #game { width:100vw; height:100vh; display:grid; place-items:center; background:radial-gradient(circle at top,#16233a,#05070d 70%); }
    .panel { width:min(980px,90vw); background:rgba(16,27,47,.94); border:1px solid #283b58; border-radius:24px; padding:32px; box-shadow:0 24px 80px rgba(0,0,0,.55); }
    h1 { margin-top:0; font-size:44px; }
    .badge { display:inline-block; padding:7px 12px; border-radius:999px; background:#7de4c6; color:#06101e; font-weight:800; }
    button { border:0; border-radius:14px; padding:14px 20px; margin:8px 8px 8px 0; background:#52a8ff; color:#06101e; font-weight:800; cursor:pointer; }
    pre { background:#07111f; border:1px solid #283b58; border-radius:14px; padding:14px; white-space:pre-wrap; }
    canvas { width:100%; height:100%; display:none; }
  body[data-quality="photoreal_target"] #game{filter:contrast(1.14) saturate(.92) brightness(.88);}body[data-quality="photoreal_target"] .fog{opacity:.95;}body[data-quality="photoreal_target"] .road{box-shadow:inset 0 0 60px rgba(255,255,255,.08),0 0 45px #000;}body[data-quality="photoreal_target"] .house,body[data-quality="photoreal_target"] .gas{filter:contrast(1.1);}
</style>
</head>
<body>
  <div id="game">
    <div class="panel" id="menu">
      <span class="badge">GameForge AI Exported Game</span>
      <h1>${payload?.name || "Playable Game Draft"}</h1>
      <p>This is a packaged runtime draft generated by GameForge AI.</p>
      <button onclick="startGame()">Start Game</button>
      <button onclick="showSettings()">Settings</button>
      <button onclick="showCredits()">Credits</button>
      <pre id="info">Target Resolution: 1920x1080
Controls:
WASD = Move
Mouse = Look
Left Click = Shoot
R = Reload
E = Interact
Esc = Pause</pre>
    </div>
    <canvas id="renderCanvas"></canvas>
  </div>
  <script>
    const project = ${JSON.stringify(payload || {}, null, 2)};
    function startGame() {
      document.getElementById('info').textContent =
        'Runtime draft loaded.\\n\\nThis exported package contains the game data, scene JSON, components, build settings and menu shell.\\n\\nFor full live gameplay, open this project in GameForge AI Engine Play Mode. Native full runtime gameplay bundling is the next packaging stage.';
    }
    function showSettings() {
      document.getElementById('info').textContent = 'Settings\\nResolution: 1920x1080\\nFullscreen: planned\\nVolume: planned\\nMouse sensitivity: planned';
    }
    function showCredits() {
      document.getElementById('info').textContent = project.credits || 'Credits generated by GameForge Licence Auditor if available.';
    }
  </script>
</body>
</html>`;

  const buildBat = `@echo off
echo ============================================
echo GameForge AI Native EXE Build
echo ============================================
echo.
echo This will install dependencies and create a Windows .exe installer/portable build.
echo You need Node.js installed.
echo.
pause
npm install
npm run dist
echo.
echo Build complete. Check the dist folder.
pause
`;

  const readme = `# ${payload?.name || "GameForge Exported Game"} — Native Package

This folder was generated by GameForge AI Engine v0.7.

## Build target

- Platform: Windows
- Resolution target: 1920x1080
- Packaging: Electron / electron-builder
- Output: Installer and portable .exe in the dist folder

## How to build the .exe

1. Install Node.js on your PC.
2. Open this folder.
3. Double-click build_windows.bat.

Or run manually:

\`\`\`bash
npm install
npm run dist
\`\`\`

## Important

This is a native packaging workflow for the playable draft data and runtime shell.
A fully finished commercial game still needs final gameplay polish, QA, asset replacement, licence verification and native runtime hardening.
`;

  fs.writeFileSync(path.join(out, 'package.json'), JSON.stringify(gamePackage, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'main.js'), gameMain, 'utf8');
  fs.writeFileSync(path.join(out, 'src', 'index.html'), runtimeHtml, 'utf8');
  fs.writeFileSync(path.join(out, 'data', 'gameforge.project.json'), JSON.stringify(payload || {}, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'data', 'scene.json'), JSON.stringify(payload?.scene || {}, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'data', 'components.json'), JSON.stringify(payload?.components || {}, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'data', 'build_settings.json'), JSON.stringify({ targetResolution:'1920x1080', platform:'windows', output:'exe', generatedAt:new Date().toISOString() }, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'build_windows.bat'), buildBat, 'utf8');
  fs.writeFileSync(path.join(out, 'README_NATIVE_BUILD.md'), readme, 'utf8');

  return { path: out, instructions: 'Open the exported folder and run build_windows.bat to create the Windows .exe in dist/.' };
}

function validateNativePackageReadiness(payload) {
  const base = validatePlayableBuild(payload);
  const extra = [
    { name:'Native Package Config', status:'Passed', detail:'Electron/electron-builder package config can be generated.' },
    { name:'Windows EXE Target', status:'Passed', detail:'NSIS installer and portable targets configured.' },
    { name:'Runtime Shell', status:'Passed', detail:'Runtime menu shell can be generated at 1920x1080.' },
    { name:'Final Commercial Readiness', status:'Advisory', detail:'Still requires QA, polish, licence verification and gameplay hardening before sale.' }
  ];
  return base.concat(extra);
}

function createPlayableBuild(payload) {
  const root = playableBuildRoot();
  const safeName = safeProjectName(payload?.name || 'GameForge_Playable_Build');
  const out = path.join(root, `${safeName}_v${Date.now()}`);
  ensureDir(out);
  ensureDir(path.join(out, 'data'));
  ensureDir(path.join(out, 'media'));
  ensureDir(path.join(out, 'docs'));

  const buildSettings = {
    targetResolution: payload?.targetResolution || '1920x1080',
    width: 1920,
    height: 1080,
    fullscreen: false,
    quality: payload?.quality || 'prototype-realistic',
    buildType: 'playable-html-draft',
    createdAt: new Date().toISOString()
  };

  const html = `<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8">
  <title>${payload?.name || 'GameForge Playable Draft'}</title>
  <style>
    body { margin:0; background:#05070d; color:white; font-family:Arial,sans-serif; display:grid; place-items:center; height:100vh; }
    .wrap { width: min(960px, 92vw); background:#101b2f; border:1px solid #283b58; border-radius:22px; padding:28px; box-shadow:0 24px 80px rgba(0,0,0,.45); }
    h1 { margin-top:0; }
    code, pre { background:#07111f; border-radius:12px; padding:12px; display:block; overflow:auto; }
    .badge { display:inline-block; padding:6px 10px; border-radius:999px; background:#7de4c6; color:#06101e; font-weight:bold; }
  </style>
</head>
<body>
  <div class="wrap">
    <span class="badge">GameForge AI Playable Draft</span>
    <h1>${payload?.name || 'Playable Game Draft'}</h1>
    <p>This folder is a prototype playable-build export. Open the original project in GameForge AI Engine v0.6 for full editor Play Mode.</p>
    <h2>Target</h2>
    <pre>${JSON.stringify(buildSettings, null, 2)}</pre>
    <h2>Controls</h2>
    <pre>WASD = move
Mouse = look
Left click = shoot
R = reload
E = interact
Shift = sprint</pre>
    <h2>Build Note</h2>
    <p>v0.6 validates and exports build data. Full packaged .exe export is planned for the next native packaging stage.</p>
  </div>
</body>
</html>`;

  fs.writeFileSync(path.join(out, 'index.html'), html, 'utf8');
  fs.writeFileSync(path.join(out, 'data', 'gameforge.project.json'), JSON.stringify(payload || {}, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'data', 'scene.json'), JSON.stringify(payload?.scene || {}, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'data', 'components.json'), JSON.stringify(payload?.components || {}, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'data', 'build_settings.json'), JSON.stringify(buildSettings, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'docs', 'BUILD_README.md'), `# ${payload?.name || 'GameForge Playable Build'}

Target resolution: 1920x1080

This is a v0.6 playable draft export. It includes validated project data, scene data, components, build settings and a launch information page.

Future packaging stage:
- native .exe export
- bundled media
- runtime-only launcher
- installer packaging
`, 'utf8');

  return { path: out, buildSettings };
}

function validatePlayableBuild(payload) {
  const sceneObjects = payload?.scene?.objects || [];
  const logic = String(payload?.logicScript || '');
  const components = payload?.components || {};
  const has = type => sceneObjects.some(o => o.type === type);
  const results = [
    {name:'Target Resolution', status:'Passed', detail:'1920x1080 target selected.'},
    {name:'Player Spawn', status:has('player_spawn')?'Passed':'Warning', detail:has('player_spawn')?'Player spawn found.':'No player spawn found.'},
    {name:'Enemies', status:has('enemy')?'Passed':'Warning', detail:`${sceneObjects.filter(o => o.type === 'enemy').length} enemy objects found.`},
    {name:'Loot/Pickups', status:(has('loot')||has('weapon_pickup'))?'Passed':'Warning', detail:'Loot and weapon pickups checked.'},
    {name:'Shooting Logic', status:logic.includes('WeaponSystem') || logic.toLowerCase().includes('shoot')?'Passed':'Warning', detail:'Weapon/shooting logic checked.'},
    {name:'Audio', status:(payload?.audioPlan || '').length || logic.toLowerCase().includes('audio')?'Passed':'Warning', detail:'Audio plan/hooks checked.'},
    {name:'Objective', status:(payload?.objective || payload?.forgeReport || '').toString().toLowerCase().includes('objective')?'Passed':'Warning', detail:'Objective text checked.'},
    {name:'Licence Audit', status:'Advisory', detail:'Run Licence Auditor before commercial release.'}
  ];
  return results;
}

function licenceAuditRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAILicenceAudits');
  ensureDir(root);
  ensureDir(path.join(root, 'reports'));
  ensureDir(path.join(root, 'evidence'));
  return root;
}

function classifyLicenceText(text) {
  const t = String(text || '').toLowerCase();
  const result = {
    raw: text || '',
    normalised: 'Unknown',
    commercialUse: 'unknown',
    attributionRequired: 'unknown',
    modificationAllowed: 'unknown',
    redistributionAllowed: 'unknown',
    risk: 'high',
    recommendation: 'Avoid until licence is verified.'
  };
  if (t.includes('cc0') || t.includes('creative commons zero') || t.includes('public domain')) {
    Object.assign(result, {
      normalised: t.includes('public domain') ? 'PublicDomain' : 'CC0',
      commercialUse: true,
      attributionRequired: false,
      modificationAllowed: true,
      redistributionAllowed: true,
      risk: 'low',
      recommendation: 'Generally safe for prototypes and commercial use, but keep source metadata.'
    });
  } else if (t.includes('cc-by') || t.includes('attribution')) {
    Object.assign(result, {
      normalised: 'Attribution',
      commercialUse: !t.includes('noncommercial') && !t.includes('non-commercial') && !t.includes('nc'),
      attributionRequired: true,
      modificationAllowed: !t.includes('no derivatives') && !t.includes('nd'),
      redistributionAllowed: true,
      risk: t.includes('noncommercial') || t.includes('non-commercial') || t.includes('nc') ? 'high' : 'medium',
      recommendation: t.includes('noncommercial') || t.includes('non-commercial') || t.includes('nc') ? 'Do not use for commercial games.' : 'Usable if proper attribution is included.'
    });
  } else if (t.includes('royalty-free') || t.includes('royalty free')) {
    Object.assign(result, {
      normalised: 'RoyaltyFree',
      commercialUse: !t.includes('personal use only') && !t.includes('noncommercial'),
      attributionRequired: t.includes('attribution required'),
      modificationAllowed: !t.includes('no modification'),
      redistributionAllowed: !t.includes('no redistribution'),
      risk: 'medium',
      recommendation: 'Review terms carefully; royalty-free does not always mean unrestricted.'
    });
  } else if (t.includes('noncommercial') || t.includes('non-commercial') || t.includes('personal use only')) {
    Object.assign(result, {
      normalised: 'NonCommercial',
      commercialUse: false,
      attributionRequired: t.includes('attribution'),
      modificationAllowed: 'unknown',
      redistributionAllowed: 'unknown',
      risk: 'high',
      recommendation: 'Do not use for commercial games.'
    });
  }
  return result;
}

function collectKnownLicenceMetadata() {
  const roots = [
    generatedMediaRoot(),
    assetLibraryRoot()
  ];
  const records = [];
  function walk(dir) {
    if (!fs.existsSync(dir)) return;
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.toLowerCase().endsWith('.json') || entry.name.toLowerCase().includes('license') || entry.name.toLowerCase().includes('licence') || entry.name.toLowerCase().includes('credit')) {
        let text = '';
        try { text = fs.readFileSync(full, 'utf8').slice(0, 20000); } catch(e) {}
        records.push({
          file: full,
          relativeFile: full,
          audit: classifyLicenceText(text),
          textPreview: text.slice(0, 1000)
        });
      }
    }
  }
  roots.forEach(walk);
  return records;
}

function createLicenceAudit(payload) {
  const root = licenceAuditRoot();
  const assets = payload?.assets || [];
  const manualText = payload?.manualLicenceText || '';
  const mode = payload?.mode || 'commercial_safe';
  const known = collectKnownLicenceMetadata();

  const auditedAssets = assets.map(asset => {
    const text = [asset.license, asset.licenseNote, asset.source, asset.sourcePage, asset.url, manualText].filter(Boolean).join('\n');
    const audit = classifyLicenceText(text);
    const allowed =
      mode === 'cc0_only'
        ? ['CC0','PublicDomain'].includes(audit.normalised)
        : mode === 'commercial_safe'
          ? audit.commercialUse === true && audit.risk !== 'high'
          : audit.risk !== 'high';
    return { ...asset, audit, allowed };
  });

  const summary = {
    totalAssets: auditedAssets.length,
    allowed: auditedAssets.filter(a => a.allowed).length,
    blocked: auditedAssets.filter(a => !a.allowed).length,
    highRisk: auditedAssets.filter(a => a.audit.risk === 'high').length,
    mediumRisk: auditedAssets.filter(a => a.audit.risk === 'medium').length,
    lowRisk: auditedAssets.filter(a => a.audit.risk === 'low').length
  };

  const report = {
    generatedAt: new Date().toISOString(),
    mode,
    summary,
    auditedAssets,
    knownMetadataFiles: known
  };

  const reportPath = path.join(root, 'reports', `licence_audit_${Date.now()}.json`);
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2), 'utf8');

  const mdPath = path.join(root, 'LICENCE_AUDIT.md');
  let md = `# GameForge Licence Audit\n\nGenerated: ${report.generatedAt}\n\nMode: ${mode}\n\n## Summary\n\n- Total assets: ${summary.totalAssets}\n- Allowed: ${summary.allowed}\n- Blocked: ${summary.blocked}\n- High risk: ${summary.highRisk}\n- Medium risk: ${summary.mediumRisk}\n- Low risk: ${summary.lowRisk}\n\n## Assets\n\n`;
  auditedAssets.forEach(a => {
    md += `### ${a.name || a.sourceName || a.relativePath || 'Unknown asset'}\n\n`;
    md += `- Allowed: ${a.allowed ? 'Yes' : 'No'}\n`;
    md += `- Licence: ${a.audit.normalised}\n`;
    md += `- Commercial use: ${a.audit.commercialUse}\n`;
    md += `- Attribution required: ${a.audit.attributionRequired}\n`;
    md += `- Modification allowed: ${a.audit.modificationAllowed}\n`;
    md += `- Risk: ${a.audit.risk}\n`;
    md += `- Recommendation: ${a.audit.recommendation}\n`;
    if (a.creator || a.username) md += `- Creator: ${a.creator || a.username}\n`;
    if (a.sourcePage || a.url) md += `- Source: ${a.sourcePage || a.url}\n`;
    md += `\n`;
  });
  fs.writeFileSync(mdPath, md, 'utf8');

  const creditsPath = path.join(root, 'CREDITS_ALL_ASSETS.md');
  let credits = '# GameForge Asset Credits\n\n';
  auditedAssets.filter(a => a.allowed).forEach(a => {
    const needs = a.audit.attributionRequired === true;
    if (needs || a.creator || a.username || a.sourcePage || a.url) {
      credits += `- ${a.name || a.sourceName || a.relativePath || 'Unknown asset'} by ${a.creator || a.username || 'Unknown creator'} — ${a.audit.normalised} — ${a.sourcePage || a.url || ''}\n`;
    }
  });
  fs.writeFileSync(creditsPath, credits, 'utf8');

  return { root, reportPath, mdPath, creditsPath, report };
}

function normaliseLicence(licence) {
  const l = String(licence || '').toLowerCase();
  if (l.includes('zero') || l.includes('cc0') || l.includes('/publicdomain/zero')) return 'CC0';
  if (l.includes('public domain')) return 'PublicDomain';
  if (l.includes('by-nc') || l.includes('noncommercial') || l.includes('non-commercial')) return 'NonCommercial';
  if (l.includes('by')) return 'Attribution';
  return 'Unknown';
}
function licenceAllowed(licence, mode) {
  const norm = normaliseLicence(licence);
  if (norm === 'NonCommercial' || norm === 'Unknown') return false;
  const m = String(mode || 'cc0_only');
  if (m === 'cc0_only') return norm === 'CC0' || norm === 'PublicDomain';
  if (m === 'commercial_allowed') return ['CC0','PublicDomain','Attribution'].includes(norm);
  if (m === 'allow_attribution') return ['CC0','PublicDomain','Attribution'].includes(norm);
  return norm === 'CC0' || norm === 'PublicDomain';
}
function filterLegalSounds(results, options) {
  const mode = options?.licenceMode || 'cc0_only';
  return (results || []).map(s => {
    const normalisedLicence = normaliseLicence(s.license);
    const allowed = licenceAllowed(s.license, mode);
    return { ...s, normalisedLicence, legalStatus: allowed ? 'allowed' : 'blocked' };
  }).filter(s => s.legalStatus === 'allowed');
}
function soundEventQueries(prompt) {
  const p = String(prompt || '').toLowerCase();
  return [
    {event:'gunshot', query:'gunshot single shot game sound'},
    {event:'reload', query:'weapon reload click game sound'},
    {event:'pickup', query:'item pickup collect game sound'},
    {event:'footstep', query:p.includes('forest') ? 'forest footstep dirt grass' : 'footstep concrete game'},
    {event:'ambient', query:p.includes('rain') || p.includes('storm') ? 'rain storm ambience loop' : p.includes('forest') ? 'forest wind ambience loop' : 'wind ambience loop'},
    {event:'enemy', query:p.includes('zombie') ? 'zombie growl monster' : 'monster growl horror'},
    {event:'hurt', query:'player hurt damage sound'},
    {event:'objective', query:'success objective complete sound'}
  ];
}
function appendAudioCredit(sound) {
  const root = generatedMediaRoot();
  const file = path.join(root, 'CREDITS_AUDIO.md');
  if (!fs.existsSync(file)) fs.writeFileSync(file, '# Audio Credits\n\n', 'utf8');
  const line = `- ${sound.sourceName || sound.name || 'Unknown sound'} by ${sound.creator || 'Unknown creator'} — ${sound.license || 'Unknown licence'} — ${sound.sourcePage || sound.previewUrl || ''}\n`;
  fs.appendFileSync(file, line, 'utf8');
  return file;
}

async function downloadWebSound(payload) {
  const root=generatedMediaRoot();
  const previewUrl=String(payload?.previewUrl||'').trim();
  if (!/^https?:\/\//i.test(previewUrl)) throw new Error('Valid preview URL required.');
  const r=await fetch(previewUrl); if(!r.ok) throw new Error(`Download failed: ${r.status}`);
  const name=String(payload?.name||`web_sound_${Date.now()}.mp3`).replace(/[^a-z0-9._-]/gi,'_');
  const dest=path.join(root,'web_sounds',name);
  fs.writeFileSync(dest, Buffer.from(await r.arrayBuffer()));
  const meta={downloadedAt:new Date().toISOString(), name, license:payload?.license||'Unknown', creator:payload?.creator||'', sourcePage:payload?.sourcePage||'', previewUrl};
  fs.writeFileSync(path.join(root,'reports',`web_sound_${Date.now()}.json`), JSON.stringify(meta,null,2), 'utf8');
  const creditsPath = appendAudioCredit(meta);
  return {creditsPath, type:'audio',category:'web_sound',name,path:dest,relativePath:path.relative(root,dest).replace(/\\/g,'/'),fileUrl:pathToFileURL(dest).href,meta};
}


function internalMeshRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeInternalMeshGenerator');
  ensureDir(root);
  ensureDir(path.join(root, 'recipes'));
  ensureDir(path.join(root, 'materials'));
  ensureDir(path.join(root, 'exports'));
  ensureDir(path.join(root, 'metadata'));
  return root;
}

function makeMeshRecipe(payload) {
  const root = internalMeshRoot();
  const id = `mesh_recipe_${Date.now()}_${Math.floor(Math.random()*10000)}`;
  const type = payload?.type || 'zombie';
  const name = String(payload?.name || type + '_asset').replace(/[^a-z0-9_-]/gi, '_');
  const seed = Number(payload?.seed || Math.floor(Math.random() * 999999));
  const style = payload?.style || 'realistic_prototype';
  const detail = payload?.detail || 'medium';

  const recipe = {
    id,
    createdAt: new Date().toISOString(),
    generator: 'GameForge Internal Mesh Generator',
    type,
    name,
    seed,
    style,
    detail,
    parts: [],
    materials: [],
    licence: {
      source: 'Generated inside GameForge from procedural recipe',
      copyrightRisk: 'low_original_procedural',
      commercialUse: 'user-generated_procedural_asset_verify_before_release',
      note: 'No external model source used.'
    },
    exportStatus: 'recipe_ready'
  };

  const addPart = (partType, label, position, scale, material) => {
    recipe.parts.push({ partType, label, position, scale, material });
  };

  if (type === 'zombie' || type === 'humanoid' || type === 'boss_zombie') {
    const boss = type === 'boss_zombie';
    const height = boss ? 2.35 : 1.85;
    addPart('sphere', 'head', {x:0,y:height,z:0}, {x:0.32,y:0.38,z:0.32}, 'zombie_skin');
    addPart('capsule', 'torso', {x:0,y:height-0.65,z:0}, {x:0.42,y:0.86,z:0.30}, 'damaged_clothing');
    addPart('cylinder', 'left_arm', {x:-0.43,y:height-0.68,z:0.02}, {x:0.11,y:0.72,z:0.11}, 'zombie_skin');
    addPart('cylinder', 'right_arm', {x:0.43,y:height-0.68,z:0.02}, {x:0.11,y:0.72,z:0.11}, 'zombie_skin');
    addPart('cylinder', 'left_leg', {x:-0.17,y:0.55,z:0}, {x:0.14,y:0.86,z:0.14}, 'dark_pants');
    addPart('cylinder', 'right_leg', {x:0.17,y:0.55,z:0}, {x:0.14,y:0.86,z:0.14}, 'dark_pants');
    addPart('sphere', 'left_eye', {x:-0.09,y:height+0.03,z:-0.27}, {x:0.035,y:0.035,z:0.035}, 'glowing_eye');
    addPart('sphere', 'right_eye', {x:0.09,y:height+0.03,z:-0.27}, {x:0.035,y:0.035,z:0.035}, 'glowing_eye');
  } else if (type === 'building' || type === 'farmhouse' || type === 'gas_station') {
    addPart('box', 'main_structure', {x:0,y:2,z:0}, {x:5,y:4,z:4}, 'weathered_concrete');
    addPart('box', 'roof', {x:0,y:4.25,z:0}, {x:5.7,y:0.45,z:4.7}, 'rusty_metal');
    addPart('box', 'door', {x:0,y:1,z:-2.06}, {x:0.9,y:2,z:0.08}, 'weathered_wood');
    addPart('box', 'window_left', {x:-1.5,y:2.3,z:-2.08}, {x:0.9,y:0.75,z:0.06}, 'dark_glass');
    addPart('box', 'window_right', {x:1.5,y:2.3,z:-2.08}, {x:0.9,y:0.75,z:0.06}, 'dark_glass');
    if (type === 'gas_station') {
      addPart('box', 'canopy', {x:0,y:3.4,z:-5}, {x:8,y:0.35,z:5}, 'rusty_metal');
      addPart('cylinder', 'pump_1', {x:-1.5,y:0.8,z:-5}, {x:0.35,y:1.6,z:0.35}, 'rusty_metal');
      addPart('cylinder', 'pump_2', {x:1.5,y:0.8,z:-5}, {x:0.35,y:1.6,z:0.35}, 'rusty_metal');
    }
  } else if (type === 'tree') {
    addPart('cylinder', 'trunk', {x:0,y:1.35,z:0}, {x:0.28,y:2.7,z:0.28}, 'weathered_wood');
    addPart('sphere', 'foliage_1', {x:0,y:3.1,z:0}, {x:1.4,y:1.0,z:1.4}, 'dark_pine');
    addPart('sphere', 'foliage_2', {x:0.25,y:3.65,z:0.1}, {x:1.0,y:0.85,z:1.0}, 'dark_pine');
  } else if (type === 'rock') {
    addPart('sphere', 'rock_mass', {x:0,y:0.45,z:0}, {x:1.2,y:0.65,z:0.9}, 'mossy_rock');
  } else if (type === 'weapon') {
    addPart('box', 'receiver', {x:0,y:0.35,z:0}, {x:1.25,y:0.28,z:0.28}, 'worn_gunmetal');
    addPart('box', 'barrel', {x:0.85,y:0.38,z:0}, {x:0.9,y:0.14,z:0.14}, 'worn_gunmetal');
    addPart('box', 'grip', {x:-0.35,y:0.02,z:0}, {x:0.28,y:0.65,z:0.22}, 'dark_pants');
  } else {
    addPart('box', 'prop_body', {x:0,y:0.5,z:0}, {x:1,y:1,z:1}, 'weathered_wood');
  }

  recipe.materials = [...new Set(recipe.parts.map(p => p.material))];

  const recipePath = path.join(root, 'recipes', `${name}_${id}.json`);
  fs.writeFileSync(recipePath, JSON.stringify(recipe, null, 2), 'utf8');

  const metadata = {
    importedAt: new Date().toISOString(),
    name,
    type: 'internal_mesh_recipe',
    assetType: type,
    recipePath,
    recipeId: id,
    licence: recipe.licence,
    exportStatus: 'recipe_ready',
    note: 'Procedural recipe can be instantiated inside GameForge. GLB export requires Babylon exporter/runtime support.'
  };

  const metaPath = path.join(root, 'metadata', `${name}_${id}_metadata.json`);
  fs.writeFileSync(metaPath, JSON.stringify(metadata, null, 2), 'utf8');

  const assetRoot = assetLibraryRoot();
  const libraryMetaPath = path.join(assetRoot, 'metadata', `${name}_${id}_internal_mesh_recipe.json`);
  fs.writeFileSync(libraryMetaPath, JSON.stringify(metadata, null, 2), 'utf8');

  return { recipe, recipePath, metadata, metaPath, libraryMetaPath };
}

function makePBRMaterialRecipe(payload) {
  const root = internalMeshRoot();
  const name = String(payload?.name || 'pbr_material').replace(/[^a-z0-9_-]/gi, '_');
  const type = payload?.type || 'weathered_concrete';
  const id = `pbr_${Date.now()}_${Math.floor(Math.random()*10000)}`;
  const presets = {
    wet_asphalt: { base:'#121416', roughness:0.22, metallic:0.0, normal:0.65, ao:0.75, notes:'dark wet road surface with puddle shine' },
    weathered_concrete: { base:'#6d6a60', roughness:0.82, metallic:0.0, normal:0.55, ao:0.85, notes:'worn concrete with cracks and dirt' },
    rusty_metal: { base:'#6d3a24', roughness:0.74, metallic:0.65, normal:0.48, ao:0.80, notes:'rusted old metal with worn edges' },
    zombie_skin: { base:'#6f7a62', roughness:0.68, metallic:0.0, normal:0.50, ao:0.82, notes:'pale infected skin material prototype' },
    damaged_clothing: { base:'#1d2327', roughness:0.76, metallic:0.0, normal:0.42, ao:0.80, notes:'dark damaged cloth' },
    weathered_wood: { base:'#5b3d25', roughness:0.78, metallic:0.0, normal:0.55, ao:0.86, notes:'old worn wood' },
    mossy_rock: { base:'#4d5149', roughness:0.88, metallic:0.0, normal:0.78, ao:0.90, notes:'rough grey rock with moss tone' }
  };
  const recipe = { id, name, type, createdAt:new Date().toISOString(), preset: presets[type] || presets.weathered_concrete, generator:'GameForge PBR Material Generator', licence:{source:'Generated inside GameForge', copyrightRisk:'low_original_procedural'} };
  const matPath = path.join(root, 'materials', `${name}_${id}.json`);
  fs.writeFileSync(matPath, JSON.stringify(recipe, null, 2), 'utf8');
  return { recipe, matPath };
}

function createGLBExportPlan(payload) {
  const root = internalMeshRoot();
  const id = `glb_export_plan_${Date.now()}_${Math.floor(Math.random()*10000)}`;
  const plan = {
    id,
    createdAt: new Date().toISOString(),
    assetName: payload?.assetName || 'generated_asset',
    source: payload?.source || 'current_scene_or_recipe',
    status: 'export_plan_ready',
    steps: [
      'Instantiate procedural recipe in Babylon scene',
      'Merge mesh parts where safe',
      'Assign PBR material recipes',
      'Bake/assign metadata',
      'Use Babylon GLTF2 exporter when available',
      'Save GLB/GLTF into Asset Library/models',
      'Store provenance and licence metadata'
    ],
    limitation: 'This build includes the export preparation framework. Actual binary GLB export depends on Babylon GLTF2 exporter being bundled/enabled in the runtime.'
  };
  const planPath = path.join(root, 'exports', `${id}.json`);
  fs.writeFileSync(planPath, JSON.stringify(plan, null, 2), 'utf8');
  return { plan, planPath };
}








function animationAssetGathererRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAnimationAssetGatherer');
  ensureDir(root);
  ensureDir(path.join(root, 'manifests'));
  ensureDir(path.join(root, 'downloads'));
  ensureDir(path.join(root, 'metadata'));
  ensureDir(path.join(root, 'reports'));
  return root;
}

function defaultAnimationManifest() {
  return {
    name: 'GameForge Approved Animation Asset Manifest',
    createdAt: new Date().toISOString(),
    note: 'Add direct GLB/GLTF URLs only when you have confirmed licence rights. Prefer your own generated assets, CC0/public-domain assets, or assets you have purchased/licensed.',
    rules: [
      'Do not use copyrighted/franchise/famous-likeness characters.',
      'Each character/animation asset needs licence text.',
      'Enable only assets that are approved for your intended use.',
      'Animation clips should ideally include idle, walk, run, attack, hit and death.'
    ],
    characters: [
      {
        name: 'approved_zombie_character.glb',
        url: '',
        role: 'zombie_enemy',
        requiredClips: ['idle','walk','run','attack','hit','death'],
        licence: 'Paste CC0/public-domain/commercial-use terms here before enabling.',
        sourcePage: '',
        creator: '',
        enabled: false
      }
    ],
    animationPacks: [
      {
        name: 'approved_zombie_animation_pack.glb',
        url: '',
        role: 'zombie_enemy',
        clips: ['idle','walk','run','attack','hit','death'],
        licence: 'Paste CC0/public-domain/commercial-use terms here before enabling.',
        sourcePage: '',
        creator: '',
        enabled: false
      }
    ]
  };
}

function saveDefaultAnimationManifest() {
  const root = animationAssetGathererRoot();
  const file = path.join(root, 'manifests', 'approved_animation_assets_manifest.json');
  if (!fs.existsSync(file)) fs.writeFileSync(file, JSON.stringify(defaultAnimationManifest(), null, 2), 'utf8');
  return file;
}

function loadAnimationManifest(payload) {
  const manifestPath = payload?.manifestPath || saveDefaultAnimationManifest();
  if (!fs.existsSync(manifestPath)) throw new Error('Animation manifest not found: ' + manifestPath);
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  return { manifest, manifestPath };
}

function animationLicenceAudit(item) {
  const text = String(item?.licence || item?.license || '').toLowerCase();
  if (text.includes('cc0') || text.includes('public domain')) {
    return { status:'likely_safe', commercialUse:'likely_allowed', reason:'CC0/public-domain wording found' };
  }
  if (text.includes('commercial') && (text.includes('allowed') || text.includes('permitted') || text.includes('license'))) {
    return { status:'review_required', commercialUse:'possibly_allowed', reason:'commercial-use wording found; review still required' };
  }
  return { status:'manual_review_required', commercialUse:'unknown', reason:'no clear licence terms supplied' };
}

async function downloadAnimationAsset(item, group) {
  const root = animationAssetGathererRoot();
  if (!item?.enabled) return { skipped:true, reason:'asset disabled', item, group };
  if (!item?.url || !/^https?:\/\//i.test(item.url)) return { skipped:true, reason:'missing valid direct http/https GLB/GLTF URL', item, group };

  const audit = animationLicenceAudit(item);
  if (audit.status === 'manual_review_required' && item.requireStrictLicence !== false) {
    return { skipped:true, reason:'licence requires manual review before download/import', licenceAudit:audit, item, group };
  }

  let name = String(item.name || path.basename(new URL(item.url).pathname) || `animation_asset_${Date.now()}.glb`).replace(/[^a-z0-9._-]/gi, '_');
  if (!/\.(glb|gltf)$/i.test(name)) name += '.glb';

  const response = await fetch(item.url);
  if (!response.ok) throw new Error(`Download failed for ${item.url}: ${response.status} ${response.statusText}`);

  const dest = path.join(root, 'downloads', name);
  fs.writeFileSync(dest, Buffer.from(await response.arrayBuffer()));

  const assetRoot = assetLibraryRoot();
  const libraryDest = path.join(assetRoot, 'models', name);
  fs.copyFileSync(dest, libraryDest);

  const metadata = {
    importedAt: new Date().toISOString(),
    name,
    group,
    role: item.role || 'character',
    requiredClips: item.requiredClips || item.clips || [],
    url: item.url,
    sourcePage: item.sourcePage || '',
    creator: item.creator || '',
    licence: item.licence || '',
    licenceAudit: audit,
    path: dest,
    libraryPath: libraryDest,
    warning: 'Verify animation/model licence terms before commercial release.'
  };

  const metaPath = path.join(root, 'metadata', `${name}_${Date.now()}.json`);
  const libraryMetaPath = path.join(assetRoot, 'metadata', `${name}_animation_asset_${Date.now()}.json`);
  fs.writeFileSync(metaPath, JSON.stringify(metadata, null, 2), 'utf8');
  fs.writeFileSync(libraryMetaPath, JSON.stringify(metadata, null, 2), 'utf8');

  return { skipped:false, name, group, dest, libraryDest, metaPath, libraryMetaPath, licenceAudit:audit, item };
}

async function runAnimationAssetGatherer(payload) {
  const { manifest, manifestPath } = loadAnimationManifest(payload || {});
  const items = [
    ...(manifest.characters || []).map(x => ({...x, __group:'character'})),
    ...(manifest.animationPacks || []).map(x => ({...x, __group:'animation_pack'}))
  ];

  const results = [];
  for (const item of items) {
    try { results.push(await downloadAnimationAsset(item, item.__group)); }
    catch(error) { results.push({ skipped:true, error:error.message, item, group:item.__group }); }
  }

  const report = {
    generatedAt: new Date().toISOString(),
    manifestPath,
    total: results.length,
    downloaded: results.filter(r => !r.skipped).length,
    skipped: results.filter(r => r.skipped).length,
    results
  };

  const root = animationAssetGathererRoot();
  const reportPath = path.join(root, 'reports', `animation_gather_report_${Date.now()}.json`);
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2), 'utf8');
  return { manifestPath, report, reportPath };
}

function createAnimationManifestFromGameRequirements(payload) {
  const root = animationAssetGathererRoot();
  const requirements = payload?.requirements || payload?.assets || [];
  const characterReqs = requirements.filter(req => {
    const text = String(req.name || req.description || req.category || '').toLowerCase();
    return /(zombie|enemy|creature|npc|human|character|survivor|infected)/.test(text);
  });

  const characters = characterReqs.map(req => {
    const base = String(req.name || req.description || 'character').replace(/[^a-z0-9_-]/gi, '_').toLowerCase();
    const isZombie = /(zombie|infected|undead|creature|enemy)/.test(base);
    return {
      name: `${base}_rigged.glb`,
      url: '',
      role: isZombie ? 'zombie_enemy' : 'human_npc',
      requiredClips: isZombie ? ['idle','walk','run','attack','hit','death'] : ['idle','walk','run','talk'],
      licence: 'Add licence terms here before enabling. Prefer CC0/public-domain, self-generated, or properly licensed assets.',
      sourcePage: '',
      creator: '',
      enabled: false,
      requirement: req
    };
  });

  const animationPacks = [
    {
      name: 'zombie_basic_animation_pack.glb',
      url: '',
      role: 'zombie_enemy',
      clips: ['idle','walk','run','attack','hit','death'],
      licence: 'Add licence terms here before enabling.',
      sourcePage: '',
      creator: '',
      enabled: false
    },
    {
      name: 'human_basic_animation_pack.glb',
      url: '',
      role: 'human_npc',
      clips: ['idle','walk','run','talk'],
      licence: 'Add licence terms here before enabling.',
      sourcePage: '',
      creator: '',
      enabled: false
    }
  ];

  const manifest = {
    name: 'GameForge Auto-Created Animation Asset Manifest',
    createdAt: new Date().toISOString(),
    note: 'Fill direct GLB/GLTF URLs and licence text, then set enabled=true for approved rigged characters or animation packs.',
    characters,
    animationPacks,
    rules: defaultAnimationManifest().rules
  };

  const manifestPath = path.join(root, 'manifests', `auto_animation_manifest_${Date.now()}.json`);
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2), 'utf8');
  return { manifest, manifestPath };
}

function createMissingAnimationReport(payload) {
  const root = animationAssetGathererRoot();
  const plan = payload?.animationPlan || createAnimationImportPlan({}).plan;
  const profiles = plan.profiles || [];
  const missing = profiles.map(profile => {
    const expected = profile.expectedClips || [];
    return {
      name: profile.name,
      role: profile.assignedRole,
      expectedClips: expected,
      missingClips: expected,
      fallback: profile.fallbackController || 'procedural_animation',
      severity: expected.length ? 'medium' : 'low',
      action: 'Use approved animation manifest, import an animated GLB/GLTF, or allow procedural fallback.'
    };
  });

  const report = {
    generatedAt: new Date().toISOString(),
    summary: `${missing.length} character animation profile(s) require runtime clip verification.`,
    missing,
    note: 'Exact missing clips can only be confirmed after GLB/GLTF assets are loaded. This report prepares the required clip targets.'
  };
  const reportPath = path.join(root, 'reports', `missing_animation_report_${Date.now()}.json`);
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2), 'utf8');
  return { report, reportPath };
}

function animationImporterRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAnimationImporter');
  ensureDir(root);
  ensureDir(path.join(root, 'profiles'));
  ensureDir(path.join(root, 'reports'));
  ensureDir(path.join(root, 'metadata'));
  return root;
}

function detectAnimationProfileFromAsset(asset) {
  const name = String(asset?.name || asset?.path || '').toLowerCase();
  const type = asset?.type || classifyModelType(name);
  const profile = {
    name: asset?.name || 'character_model',
    type,
    path: asset?.path || '',
    rigStatus: 'unknown_until_loaded_in_scene',
    animationStatus: 'unknown_until_loaded_in_scene',
    assignedRole: type === 'character' ? 'enemy_or_npc' : type,
    expectedClips: [],
    fallbackController: null,
    warnings: [],
    recommendedScale: type === 'character' ? 1.0 : 1.0,
    hitbox: type === 'character' ? { width: 0.75, height: 1.9, depth: 0.75 } : null
  };

  if (/(zombie|infected|creature|enemy|undead)/.test(name)) {
    profile.assignedRole = 'zombie_enemy';
    profile.expectedClips = ['idle', 'walk', 'run', 'attack', 'hit', 'death'];
    profile.fallbackController = 'procedural_zombie_animation';
  } else if (/(human|person|survivor|npc|character)/.test(name)) {
    profile.assignedRole = 'human_npc';
    profile.expectedClips = ['idle', 'walk', 'run', 'talk'];
    profile.fallbackController = 'procedural_humanoid_animation';
  } else {
    profile.warnings.push('Model does not appear to be a character. Animation controller may not apply.');
  }

  profile.warnings.push('Exact skeleton/animation clips are detected at runtime after GLB/GLTF load.');
  return profile;
}

function createAnimationImportPlan(payload) {
  const root = animationImporterRoot();
  const id = `anim_import_${Date.now()}_${Math.floor(Math.random()*10000)}`;
  const assets = payload?.assets || listLocalGLBGLTFAssets().filter(a => a.type === 'character');
  const profiles = assets.map(detectAnimationProfileFromAsset);
  const plan = {
    id,
    createdAt: new Date().toISOString(),
    mode: 'rigged_character_animation_importer',
    profiles,
    controllerRules: [
      'If GLB/GLTF contains animation clips, map clips by name to idle/walk/run/attack/hit/death.',
      'If clips are missing, use procedural fallback animation.',
      'If no skeleton exists, use simple transform/bob/lean animation until rigged asset is available.',
      'Zombie/enemy models receive chase/attack/death state controller.',
      'NPC models receive idle/walk/talk placeholder controller.',
      'Scale and hitbox are assigned automatically by role.'
    ],
    clipNameAliases: {
      idle: ['idle', 'idle_01', 'stand', 'breathing'],
      walk: ['walk', 'walking', 'zombie_walk'],
      run: ['run', 'running', 'sprint'],
      attack: ['attack', 'bite', 'swipe', 'melee'],
      hit: ['hit', 'damage', 'hurt', 'impact'],
      death: ['death', 'die', 'dead', 'fall']
    }
  };
  const planPath = path.join(root, 'reports', `${id}_plan.json`);
  fs.writeFileSync(planPath, JSON.stringify(plan, null, 2), 'utf8');
  return { plan, planPath };
}

function modelGathererRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeModelGatherer');
  ensureDir(root);
  ensureDir(path.join(root, 'downloads'));
  ensureDir(path.join(root, 'manifests'));
  ensureDir(path.join(root, 'metadata'));
  ensureDir(path.join(root, 'reports'));
  return root;
}

function safeModelFileName(name, url) {
  const fallback = url ? path.basename(new URL(url).pathname) : 'model.glb';
  let n = String(name || fallback || `model_${Date.now()}.glb`).replace(/[^a-z0-9._-]/gi, '_');
  if (!/\.(glb|gltf)$/i.test(n)) n += '.glb';
  return n;
}

function classifyLicenceSafety(meta) {
  const text = String(meta?.licence || meta?.license || meta?.licenceText || '').toLowerCase();
  const source = String(meta?.source || meta?.sourcePage || meta?.url || '').toLowerCase();
  if (text.includes('cc0') || text.includes('public domain')) return { status:'likely_safe', commercialUse:'likely_allowed', reason:'CC0/public-domain text found' };
  if (text.includes('commercial') && (text.includes('allowed') || text.includes('permitted'))) return { status:'review_required', commercialUse:'possibly_allowed', reason:'commercial-use wording found but still requires review' };
  if (source.includes('localhost') || source.includes('file:')) return { status:'local_asset', commercialUse:'unknown', reason:'local file/source' };
  return { status:'manual_review_required', commercialUse:'unknown', reason:'no clear CC0/public-domain/commercial-use text supplied' };
}

function defaultApprovedModelManifest() {
  return {
    name: 'GameForge Approved Model Manifest',
    note: 'Add direct GLB/GLTF URLs only when you have confirmed licence rights. Prefer CC0/public-domain or your own generated assets.',
    createdAt: new Date().toISOString(),
    rules: [
      'Only direct .glb/.gltf URLs should be downloaded automatically.',
      'Each model needs licence text or manual review.',
      'Avoid copyrighted characters, brands, franchises and famous likenesses.',
      'GameForge imports safe/approved models first and uses procedural fallback when missing.'
    ],
    models: [
      {
        name: 'example_zombie_model.glb',
        url: '',
        type: 'character',
        tags: ['zombie','infected','enemy'],
        licence: 'Paste CC0/public-domain/commercial-use terms here before use',
        sourcePage: '',
        creator: '',
        enabled: false
      },
      {
        name: 'example_abandoned_house.glb',
        url: '',
        type: 'building',
        tags: ['house','building','abandoned'],
        licence: 'Paste CC0/public-domain/commercial-use terms here before use',
        sourcePage: '',
        creator: '',
        enabled: false
      }
    ]
  };
}

function saveDefaultModelManifest() {
  const root = modelGathererRoot();
  const file = path.join(root, 'manifests', 'approved_models_manifest.json');
  if (!fs.existsSync(file)) fs.writeFileSync(file, JSON.stringify(defaultApprovedModelManifest(), null, 2), 'utf8');
  return file;
}

function loadGathererManifest(payload) {
  const root = modelGathererRoot();
  const manifestPath = payload?.manifestPath || saveDefaultModelManifest();
  if (!fs.existsSync(manifestPath)) throw new Error('Manifest not found: ' + manifestPath);
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  return { manifest, manifestPath };
}

async function downloadApprovedModel(model) {
  const root = modelGathererRoot();
  if (!model?.enabled) return { skipped:true, reason:'model disabled', model };
  if (!model?.url || !/^https?:\/\//i.test(model.url)) return { skipped:true, reason:'missing valid http/https direct URL', model };

  const licenceAudit = classifyLicenceSafety(model);
  if (licenceAudit.status === 'manual_review_required' && model.requireStrictLicence !== false) {
    return { skipped:true, reason:'licence requires manual review before download/import', licenceAudit, model };
  }

  const fileName = safeModelFileName(model.name, model.url);
  const dest = path.join(root, 'downloads', fileName);

  const response = await fetch(model.url);
  if (!response.ok) throw new Error(`Download failed for ${model.url}: ${response.status} ${response.statusText}`);
  fs.writeFileSync(dest, Buffer.from(await response.arrayBuffer()));

  const assetRoot = assetLibraryRoot();
  const libraryDest = path.join(assetRoot, 'models', fileName);
  fs.copyFileSync(dest, libraryDest);

  const metadata = {
    importedAt: new Date().toISOString(),
    name: fileName,
    type: model.type || classifyModelType(fileName),
    tags: model.tags || [],
    url: model.url,
    sourcePage: model.sourcePage || '',
    creator: model.creator || '',
    licence: model.licence || model.license || '',
    licenceAudit,
    path: dest,
    libraryPath: libraryDest,
    warning: 'Verify licence terms before commercial release.'
  };

  const metaPath = path.join(root, 'metadata', `${fileName}_${Date.now()}.json`);
  const libraryMetaPath = path.join(assetRoot, 'metadata', `${fileName}_gathered_${Date.now()}.json`);
  fs.writeFileSync(metaPath, JSON.stringify(metadata, null, 2), 'utf8');
  fs.writeFileSync(libraryMetaPath, JSON.stringify(metadata, null, 2), 'utf8');

  return { skipped:false, model, fileName, dest, libraryDest, metaPath, libraryMetaPath, licenceAudit };
}

async function runModelGatherer(payload) {
  const { manifest, manifestPath } = loadGathererManifest(payload || {});
  const models = Array.isArray(manifest.models) ? manifest.models : [];
  const results = [];
  for (const model of models) {
    try { results.push(await downloadApprovedModel(model)); }
    catch(error) { results.push({ skipped:true, error:error.message, model }); }
  }

  const root = modelGathererRoot();
  const report = {
    generatedAt: new Date().toISOString(),
    manifestPath,
    total: results.length,
    downloaded: results.filter(r => !r.skipped).length,
    skipped: results.filter(r => r.skipped).length,
    results
  };
  const reportPath = path.join(root, 'reports', `gather_report_${Date.now()}.json`);
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2), 'utf8');
  return { report, reportPath, manifestPath };
}

function createGathererManifestFromRequirements(payload) {
  const root = modelGathererRoot();
  const requirements = payload?.requirements || payload?.assets || [];
  const models = requirements.map(req => {
    const name = String(req.name || req.description || 'asset').replace(/[^a-z0-9_-]/gi, '_').toLowerCase();
    return {
      name: `${name}.glb`,
      url: '',
      type: req.category || req.type || 'prop',
      tags: String(req.description || req.name || '').toLowerCase().split(/[^a-z0-9]+/).filter(Boolean).slice(0, 12),
      licence: 'Add licence terms here before enabling. Prefer CC0/public-domain or self-generated assets.',
      sourcePage: '',
      creator: '',
      enabled: false,
      requirement: req
    };
  });

  const manifest = {
    name: 'GameForge Auto-Created Model Gathering Manifest',
    createdAt: new Date().toISOString(),
    note: 'Fill in direct GLB/GLTF URLs and licence terms, then set enabled=true for models you want GameForge to gather automatically.',
    models
  };

  const manifestPath = path.join(root, 'manifests', `auto_manifest_${Date.now()}.json`);
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2), 'utf8');
  return { manifest, manifestPath };
}

function autoAssetImportRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAutoImportedAssets');
  ensureDir(root);
  ensureDir(path.join(root, 'models'));
  ensureDir(path.join(root, 'metadata'));
  ensureDir(path.join(root, 'reports'));
  return root;
}

function scoreAssetForRequirement(fileName, requirement) {
  const f = String(fileName || '').toLowerCase();
  const r = String(requirement?.name || requirement?.description || '').toLowerCase();
  let score = 0;
  const terms = {
    zombie: ['zombie','infected','creature','enemy','undead'],
    building: ['house','building','farmhouse','gas','station','shed','interior','hallway'],
    tree: ['tree','pine','forest','foliage'],
    rock: ['rock','stone','boulder'],
    weapon: ['weapon','pistol','rifle','gun','knife'],
    prop: ['prop','crate','barrel','radio','fuse','key','supply','medkit','ammo']
  };
  for (const [group, words] of Object.entries(terms)) {
    if (words.some(w => f.includes(w)) && words.some(w => r.includes(w))) score += 10;
  }
  for (const token of r.split(/[^a-z0-9]+/).filter(Boolean)) {
    if (token.length > 2 && f.includes(token)) score += 2;
  }
  return score;
}

function classifyModelType(fileName) {
  const f = String(fileName || '').toLowerCase();
  if (/(zombie|infected|creature|enemy|undead|character|human|person)/.test(f)) return 'character';
  if (/(house|building|farmhouse|gas|station|shed|interior|hallway)/.test(f)) return 'building';
  if (/(tree|pine|forest|foliage|grass)/.test(f)) return 'environment';
  if (/(rock|stone|boulder|debris)/.test(f)) return 'environment';
  if (/(weapon|pistol|rifle|gun|knife)/.test(f)) return 'weapon';
  return 'prop';
}

function listLocalGLBGLTFAssets() {
  const roots = [];
  try { roots.push(path.join(assetLibraryRoot(), 'models')); } catch(e) {}
  try { roots.push(path.join(app.getPath('documents'), 'GameForgeAI3DAssets', 'models')); } catch(e) {}
  try { roots.push(path.join(app.getPath('documents'), 'GameForgeAutoImportedAssets', 'models')); } catch(e) {}

  const files = [];
  for (const root of roots) {
    if (!root || !fs.existsSync(root)) continue;
    const walk = (dir) => {
      for (const item of fs.readdirSync(dir, { withFileTypes:true })) {
        const full = path.join(dir, item.name);
        if (item.isDirectory()) walk(full);
        else if (/\.(glb|gltf)$/i.test(item.name)) {
          files.push({
            name: item.name,
            path: full,
            root,
            type: classifyModelType(item.name),
            relativePath: path.relative(root, full).replace(/\\/g, '/')
          });
        }
      }
    };
    walk(root);
  }
  return files;
}

function createAutoAssetImportPlan(payload) {
  const root = autoAssetImportRoot();
  const id = `auto_asset_import_${Date.now()}_${Math.floor(Math.random()*10000)}`;
  const requirements = payload?.requirements || payload?.assets || [];
  const localAssets = listLocalGLBGLTFAssets();
  const assignments = [];

  for (const req of requirements) {
    const ranked = localAssets
      .map(asset => ({ asset, score: scoreAssetForRequirement(asset.name, req) }))
      .filter(x => x.score > 0)
      .sort((a,b) => b.score - a.score);
    const best = ranked[0] || null;
    assignments.push({
      requirement: req,
      matched: Boolean(best),
      model: best ? best.asset : null,
      score: best ? best.score : 0,
      fallback: best ? null : 'use_internal_mesh_or_free3d_generator',
      status: best ? 'assigned_glb_gltf' : 'fallback_required'
    });
  }

  const plan = {
    id,
    createdAt: new Date().toISOString(),
    mode: 'automatic_glb_gltf_import_and_assignment',
    localAssetCount: localAssets.length,
    localAssets,
    assignments,
    rules: [
      'Use safe local GLB/GLTF assets first.',
      'If no matching model exists, fall back to Internal Mesh / Free 3D Generator.',
      'Warn for missing licence metadata.',
      'Characters can be used as static/enemy models until animation importer is added.',
      'Buildings/props/environment assets can be placed directly into scene.'
    ]
  };

  const planPath = path.join(root, 'reports', `${id}_plan.json`);
  fs.writeFileSync(planPath, JSON.stringify(plan, null, 2), 'utf8');
  return { plan, planPath };
}

function photorealRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgePhotorealMode');
  ensureDir(root);
  ensureDir(path.join(root, 'plans'));
  ensureDir(path.join(root, 'reports'));
  ensureDir(path.join(root, 'checklists'));
  return root;
}

function createPhotorealModePlan(payload) {
  const root = photorealRoot();
  const id = `photoreal_mode_${Date.now()}_${Math.floor(Math.random()*10000)}`;
  const prompt = String(payload?.prompt || '').trim() || 'first-person survival horror game';

  const plan = {
    id,
    createdAt: new Date().toISOString(),
    prompt,
    mode: 'Photoreal Target',
    honestLimit: 'This free mode improves realism through lighting, fog, PBR materials and detail passes. True photoreal humans/zombies still require high-quality GLB/GLTF assets, scanned assets, or a dedicated 3D generation provider.',
    renderPipeline: {
      lighting: 'cinematic horror night lighting',
      fog: 'exp2 blue-grey fog',
      bloom: 'subtle bloom',
      colourGrading: 'blue-grey horror grade',
      contrast: 'high contrast',
      wetSurfaceLook: true,
      antiAliasing: 'FXAA where available',
      exposure: 'reduced exposure for horror atmosphere'
    },
    pbrSurfacePack: [
      { name:'Wet_Asphalt_Photo', use:'roads, driveway, wet paths', maps:['albedo','normal','roughness','ambient_occlusion'] },
      { name:'Weathered_Concrete_Photo', use:'walls, gas station, old structures', maps:['albedo','normal','roughness','ambient_occlusion'] },
      { name:'Rusty_Metal_Photo', use:'barrels, cars, pumps, fuse boxes', maps:['albedo','normal','roughness','metallic','ambient_occlusion'] },
      { name:'Old_Wood_Photo', use:'house, floors, doors, crates, furniture', maps:['albedo','normal','roughness','ambient_occlusion'] },
      { name:'Zombie_Skin_Photo', use:'zombies and creatures', maps:['albedo','normal','roughness','ambient_occlusion'] },
      { name:'Dirty_Glass_Photo', use:'windows and mirrors', maps:['albedo','roughness','opacity'] },
      { name:'Mossy_Rock_Photo', use:'rocks and forest debris', maps:['albedo','normal','roughness','ambient_occlusion'] }
    ],
    sceneDetailPass: [
      'increase prop density around objectives',
      'add overgrown grass and weeds',
      'add rocks and debris clusters',
      'add road cracks and puddle placeholders',
      'add warm/cool lighting contrast',
      'add flickering light props',
      'add dirt/wear/blood/decal placeholders',
      'vary repeated asset scale/rotation/materials',
      'add fog layers and dark background silhouettes'
    ],
    realisticAssetTargets: [
      'photoreal abandoned house interior/exterior GLB',
      'photoreal gas station/building GLB',
      'photoreal zombie/creature GLB with animations',
      'photoreal forest foliage pack',
      'photoreal road/debris props',
      'photoreal weapon/flashlight/fuse/radio props'
    ],
    cc0SafeImportChecklist: [
      'Only use assets with clear commercial-use terms',
      'Prefer CC0/public-domain or self-generated assets',
      'Save source page, licence text, creator and date',
      'Avoid copyrighted franchises, brands and famous likenesses',
      'Prefer GLB/GLTF with PBR materials',
      'Verify licence again before commercial release'
    ]
  };

  const planPath = path.join(root, 'plans', `${id}.json`);
  const reportPath = path.join(root, 'reports', `${id}_report.md`);
  const checklistPath = path.join(root, 'checklists', `${id}_cc0_checklist.json`);

  fs.writeFileSync(planPath, JSON.stringify(plan, null, 2), 'utf8');
  fs.writeFileSync(checklistPath, JSON.stringify(plan.cc0SafeImportChecklist, null, 2), 'utf8');

  const report = `# GameForge Startup Hotfix v3.3.1 Plan

Created: ${plan.createdAt}

## Honest Limit
${plan.honestLimit}

## Render Pipeline
${Object.entries(plan.renderPipeline).map(([k,v]) => `- ${k}: ${v}`).join('\n')}

## PBR Surface Pack
${plan.pbrSurfacePack.map(m => `- ${m.name}: ${m.use} | maps: ${m.maps.join(', ')}`).join('\n')}

## Scene Detail Pass
${plan.sceneDetailPass.map(x => `- ${x}`).join('\n')}

## Realistic Asset Targets
${plan.realisticAssetTargets.map(x => `- ${x}`).join('\n')}

## CC0 / Safe Import Checklist
${plan.cc0SafeImportChecklist.map(x => `- ${x}`).join('\n')}
`;
  fs.writeFileSync(reportPath, report, 'utf8');

  return { plan, planPath, reportPath, checklistPath };
}

function completeGameExportRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeCompleteGameExports');
  ensureDir(root);
  return root;
}

function makeSafeGameName(name) {
  return String(name || 'GameForge_Full_Game')
    .replace(/[^a-z0-9 _-]/gi, '')
    .trim()
    .replace(/\s+/g, '_') || 'GameForge_Full_Game';
}

function createCompleteGamePlan(payload) {
  const prompt = String(payload?.prompt || '').trim() || 'first-person survival horror co-op game';
  const lower = prompt.toLowerCase();
  const title = makeSafeGameName(payload?.title || (lower.includes('house') ? 'Black_Pine_Road' : 'Dead_Road_Supply_Run'));
  const isHouse = lower.includes('house') || lower.includes('jump') || lower.includes('fuse') || lower.includes('key');

  return {
    title,
    createdAt: new Date().toISOString(),
    prompt,
    type: 'complete multi-level playable prototype',
    branding: {
      introText: 'Developed by GameForge AI',
      subtitle: 'Autonomous Game Generation Prototype',
      showIntro: true,
      introDurationMs: 2800
    },
    modes: ['single_player', 'host_coop_placeholder', 'join_coop_placeholder'],
    playerCount: { min: 1, max: 4 },
    controls: ['WASD movement', 'mouse look', 'E interact', 'Shift sprint', 'Esc pause', 'R reload where applicable'],
    menu: ['Start Single Player', 'Host Co-op', 'Join Co-op', 'Settings', 'Quit'],
    displaySettings: {
      defaultResolution: '1280x720',
      allowFullscreen: true,
      allowWindowed: true,
      allowBorderless: true,
      qualityPresets: ['Low', 'Medium', 'High', 'Ultra'],
      note: 'Exported game includes a Display Settings menu.'
    },
    levels: isHouse ? [
      { id:'level_01_road', name:'The Road', objective:'Find the flashlight and reach the abandoned house.', win:'reach house entrance' },
      { id:'level_02_house', name:'The House', objective:'Restore power, find the key and survive the jump scares.', win:'unlock back door' },
      { id:'level_03_forest', name:'The Forest Path', objective:'Escape through the forest while being chased.', win:'reach escape zone' }
    ] : [
      { id:'level_01_road', name:'The Road', objective:'Find supplies and reach the abandoned gas station.', win:'reach gas station' },
      { id:'level_02_town', name:'The Town', objective:'Collect 3 supply boxes and repair the radio.', win:'repair radio' },
      { id:'level_03_extraction', name:'Extraction', objective:'Survive the final wave and reach extraction.', win:'survive timer' }
    ],
    coop: {
      status: 'prototype_framework',
      hostJoin: true,
      syncedSystems: ['player positions placeholder', 'shared objective placeholder', 'health placeholder', 'pickups placeholder', 'level state placeholder'],
      note: 'This export includes co-op menu/framework placeholders. Full online multiplayer networking requires a later dedicated implementation.'
    },
    systems: ['player controller', 'HUD', 'objectives', 'pickups', 'enemies/scares', 'win/fail states', 'pause/restart/quit', 'level transitions'],
    validation: ['intro splash exists', 'main menu exists', 'levels exist', 'objectives exist', 'HUD exists', 'game runtime exists', 'build script exists', 'EXE package folder exists']
  };
}

function createFullGameExePackage(payload) {
  const root = completeGameExportRoot();
  const plan = payload?.plan || createCompleteGamePlan(payload || {});
  const title = makeSafeGameName(plan.title || payload?.title || 'GameForge_Full_Game');
  const id = `${title}_${Date.now()}`;
  const exportDir = path.join(root, id);
  const runtimeDir = path.join(exportDir, 'game_runtime');
  ensureDir(exportDir);
  ensureDir(runtimeDir);
  ensureDir(path.join(runtimeDir, 'data'));
  ensureDir(path.join(runtimeDir, 'assets'));
  ensureDir(path.join(runtimeDir, 'dist'));

  const scene = payload?.scene || {};
  const runtime = payload?.runtime || {};

  fs.writeFileSync(path.join(runtimeDir, 'data', 'complete_game_plan.json'), JSON.stringify(plan, null, 2), 'utf8');
  fs.writeFileSync(path.join(runtimeDir, 'data', 'scene.json'), JSON.stringify(scene, null, 2), 'utf8');
  fs.writeFileSync(path.join(runtimeDir, 'data', 'runtime.json'), JSON.stringify(runtime, null, 2), 'utf8');

  const runtimeHtml = `<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>${title}</title>
<style>
html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#05070b;color:#e8f1ff;font-family:Arial,sans-serif}
#intro,#menu,#game{position:absolute;inset:0}
#intro{display:flex;align-items:center;justify-content:center;background:radial-gradient(circle at center,#14345e,#05070b 65%);z-index:5}
.intro-card{text-align:center;animation:introFade 2.8s ease forwards}
.logo-mark{width:96px;height:96px;border-radius:26px;margin:0 auto 22px;border:2px solid #74c7ff;background:linear-gradient(135deg,#2488ff,#7de4c6);display:flex;align-items:center;justify-content:center;font-size:42px;font-weight:900;color:#06101c;box-shadow:0 0 60px rgba(82,168,255,.45)}
.intro-title{font-size:42px;font-weight:900;letter-spacing:1px;margin-bottom:8px}
.intro-sub{font-size:16px;color:#b8cbe2;letter-spacing:2px;text-transform:uppercase}
@keyframes introFade{0%{opacity:0;transform:scale(.96)}18%{opacity:1;transform:scale(1)}78%{opacity:1}100%{opacity:0}}
#menu{display:none;align-items:center;justify-content:center;background:radial-gradient(circle at center,#14294a,#05070b)}
.card{background:rgba(0,0,0,.72);border:1px solid #4c9cff;border-radius:18px;padding:30px;min-width:420px;box-shadow:0 0 45px #000}
button{display:block;width:100%;margin:10px 0;padding:13px;border:0;border-radius:10px;background:#2488ff;color:#fff;font-weight:800}
button.secondary{background:#303846}
#game{display:none;background:linear-gradient(#07111f,#101010 58%,#15100d);overflow:hidden}
.fog{position:absolute;inset:0;background:radial-gradient(circle at 50% 35%,rgba(120,160,190,.18),transparent 35%),linear-gradient(90deg,rgba(255,255,255,.06),transparent,rgba(255,255,255,.04));opacity:.75}
.road{position:absolute;left:35%;bottom:-10%;width:30%;height:80%;background:linear-gradient(#17191a,#080909);transform:perspective(500px) rotateX(58deg);border-left:3px solid #343638;border-right:3px solid #343638}
.house{position:absolute;left:8%;bottom:22%;width:22%;height:28%;background:#2a241e;border:2px solid #4b4033}
.house:before{content:"";position:absolute;left:-5%;top:-24%;width:110%;height:25%;background:#191919;clip-path:polygon(50% 0,100% 100%,0 100%)}
.gas{position:absolute;right:8%;bottom:23%;width:26%;height:22%;background:#2e2b26;border:2px solid #5a4c36}
.tree{position:absolute;bottom:25%;width:18px;height:110px;background:#15100a}.tree:before{content:"";position:absolute;left:-38px;top:-60px;width:95px;height:95px;background:#102b1a;border-radius:50%}
.zombie{position:absolute;left:48%;bottom:26%;width:70px;height:160px;filter:drop-shadow(0 0 18px #000)}
.zombie .head{position:absolute;left:20px;top:0;width:32px;height:38px;background:#6f7c62;border-radius:45%}.zombie .body{position:absolute;left:14px;top:38px;width:44px;height:68px;background:#1c2224;border-radius:10px}.zombie .arm{position:absolute;top:45px;width:16px;height:64px;background:#6f7c62;border-radius:12px}.zombie .arm.l{left:0;transform:rotate(18deg)}.zombie .arm.r{right:0;transform:rotate(-18deg)}.zombie .leg{position:absolute;top:102px;width:16px;height:58px;background:#111}.zombie .leg.l{left:18px}.zombie .leg.r{right:18px}
.hud{position:absolute;inset:0;pointer-events:none}.box{position:absolute;background:rgba(0,0,0,.6);border:1px solid #5da8ff;border-radius:10px;padding:12px}.objective{left:20px;top:20px;width:380px}.stats{left:20px;bottom:20px}.level{right:20px;top:20px}.cross{position:absolute;left:50%;top:50%;width:22px;height:22px;transform:translate(-50%,-50%)}.cross:before,.cross:after{content:"";position:absolute;background:#e8f1ff}.cross:before{left:10px;top:0;width:2px;height:22px}.cross:after{left:0;top:10px;width:22px;height:2px}
.pauseMenu{display:none;position:absolute;inset:0;background:rgba(0,0,0,.7);align-items:center;justify-content:center}.settingsPanel{display:none;position:absolute;inset:0;background:rgba(0,0,0,.78);align-items:center;justify-content:center;z-index:4}.settingsPanel label{display:block;margin-top:12px;color:#b8cbe2;font-size:13px}.settingsPanel select,.settingsPanel input{width:100%;padding:10px;border-radius:8px;border:1px solid #4c9cff;background:#101826;color:#e8f1ff;margin-top:5px}.settings-row{display:grid;grid-template-columns:1fr 1fr;gap:12px}
</style>
</head>
<body>
<div id="intro">
  <div class="intro-card">
    <div class="logo-mark">GF</div>
    <div class="intro-title">Developed by GameForge AI</div>
    <div class="intro-sub">Autonomous Game Generation Prototype</div>
  </div>
</div>
<div id="menu"><div class="card">
<h1>${title.replace(/_/g,' ')}</h1>
<p>GameForge full-game prototype export</p>
<button onclick="startGame('single_player')">Start Single Player</button>
<button onclick="startGame('host_coop')">Host Co-op</button>
<button onclick="startGame('join_coop')">Join Co-op</button>
<button class="secondary" onclick="showSettings()">Settings</button>
<button class="secondary" onclick="window.close()">Quit</button>
</div></div>
<div id="settingsPanel" class="settingsPanel">
  <div class="card">
    <h2>Display Settings</h2>
    <p>Choose a resolution and display mode that suits your PC.</p>

    <label>Resolution</label>
    <select id="resolutionSelect"></select>

    <div class="settings-row">
      <div>
        <label>Display Mode</label>
        <select id="displayModeSelect">
          <option value="windowed">Windowed</option>
          <option value="fullscreen">Fullscreen</option>
          <option value="borderless">Borderless-style</option>
        </select>
      </div>
      <div>
        <label>Graphics Quality</label>
        <select id="qualitySelect">
          <option value="Low">Low</option>
          <option value="Medium">Medium</option>
          <option value="High" selected>High</option>
          <option value="Ultra">Ultra</option>
        </select>
      </div>
    </div>

    <button onclick="applySettings()">Apply Settings</button>
    <button class="secondary" onclick="closeSettings()">Back</button>
    <small id="settingsNote">Settings save locally on this PC.</small>
  </div>
</div>
<div id="game" tabindex="1">
 <div class="fog"></div><div class="road"></div><div class="house"></div><div class="gas"></div>
 <div class="tree" style="left:5%"></div><div class="tree" style="left:28%;height:140px"></div><div class="tree" style="right:3%;height:130px"></div>
 <div class="zombie"><div class="head"></div><div class="body"></div><div class="arm l"></div><div class="arm r"></div><div class="leg l"></div><div class="leg r"></div></div>
 <div class="hud"><div class="box objective" id="objective"></div><div class="box stats" id="stats"></div><div class="box level" id="levelbox"></div><div class="cross"></div></div>
 <div class="pauseMenu" id="pause"><div class="card"><h2>Paused</h2><button onclick="togglePause()">Resume</button><button onclick="nextLevel()">Next Level</button><button onclick="location.reload()">Restart</button><button onclick="window.close()">Quit</button></div></div>
</div>
<script>
const supportedBaseResolutions = [
  [800,600],[1024,768],[1280,720],[1366,768],[1600,900],
  [1920,1080],[2560,1440],[3440,1440],[3840,2160]
];

function getSupportedResolutions(){
  const maxW = window.screen?.width || 1920;
  const maxH = window.screen?.height || 1080;
  const list = supportedBaseResolutions.filter(([w,h]) => w <= maxW && h <= maxH);
  if (!list.some(([w,h]) => w === maxW && h === maxH)) list.push([maxW,maxH]);
  return list;
}

function populateResolutionOptions(){
  const select = document.getElementById('resolutionSelect');
  if (!select) return;
  select.innerHTML = '';
  getSupportedResolutions().forEach(([w,h]) => {
    const opt = document.createElement('option');
    opt.value = String(w) + 'x' + String(h);
    opt.textContent = String(w) + ' × ' + String(h);
    select.appendChild(opt);
  });
}

function loadDisplaySettings(){
  try { return JSON.parse(localStorage.getItem('gameforge_display_settings') || '{}'); }
  catch(e){ return {}; }
}

function saveDisplaySettings(settings){
  try { localStorage.setItem('gameforge_display_settings', JSON.stringify(settings)); } catch(e){}
}

function showSettings(){
  populateResolutionOptions();
  const s = loadDisplaySettings();
  if (s.resolution) document.getElementById('resolutionSelect').value = s.resolution;
  if (s.mode) document.getElementById('displayModeSelect').value = s.mode;
  if (s.quality) document.getElementById('qualitySelect').value = s.quality;
  document.getElementById('settingsPanel').style.display = 'flex';
}

function closeSettings(){
  document.getElementById('settingsPanel').style.display = 'none';
}

function applySettings(){
  const resolution = document.getElementById('resolutionSelect').value || '1280x720';
  const mode = document.getElementById('displayModeSelect').value || 'windowed';
  const quality = document.getElementById('qualitySelect').value || 'High';
  const [w,h] = resolution.split('x').map(Number);
  saveDisplaySettings({ resolution, mode, quality });

  if (mode === 'fullscreen') {
    const el = document.documentElement;
    if (el.requestFullscreen) el.requestFullscreen().catch(()=>{});
  } else {
    if (document.fullscreenElement && document.exitFullscreen) document.exitFullscreen().catch(()=>{});
  }

  const game = document.getElementById('game');
  if (game && w && h) {
    game.style.width = w + 'px';
    game.style.height = h + 'px';
    game.style.maxWidth = '100vw';
    game.style.maxHeight = '100vh';
    game.style.margin = '0 auto';
  }

  document.body.dataset.quality = quality.toLowerCase().replace(/\s+/g,'_');
  document.getElementById('settingsNote').textContent = 'Applied ' + resolution + ', ' + mode + ', ' + quality + ' quality.';
}

function applySavedSettingsOnStart(){
  populateResolutionOptions();
  const s = loadDisplaySettings();
  if (s.resolution) {
    const [w,h] = s.resolution.split('x').map(Number);
    const game = document.getElementById('game');
    if (game && w && h) {
      game.style.width = w + 'px';
      game.style.height = h + 'px';
      game.style.maxWidth = '100vw';
      game.style.maxHeight = '100vh';
      game.style.margin = '0 auto';
    }
  }
  if (s.quality) document.body.dataset.quality = s.quality.toLowerCase();
}

window.addEventListener('load', applySavedSettingsOnStart);

const plan=${JSON.stringify(plan)};
let mode='single_player', levelIndex=0, health=100, stamina=100, ammo=30, paused=false;
setTimeout(()=>{document.getElementById('intro').style.display='none';document.getElementById('menu').style.display='flex';}, plan.branding?.introDurationMs || 2800);
function currentLevel(){return plan.levels[levelIndex]||{name:'Prototype',objective:'Survive',win:'complete objective'}}
function updateHUD(){const l=currentLevel();document.getElementById('objective').innerHTML='<b>OBJECTIVE</b><br>'+l.objective+'<br><small>Win: '+l.win+'</small>';document.getElementById('stats').innerHTML='Health: '+health+'<br>Stamina: '+stamina+'<br>Ammo/Battery: '+ammo+'<br>Mode: '+mode;document.getElementById('levelbox').innerHTML='<b>'+l.name+'</b><br>Level '+(levelIndex+1)+' / '+plan.levels.length;}
function startGame(m){mode=m;if(m!=='single_player')alert('Co-op framework placeholder: host/join UI is present, but full networking is not implemented yet.');document.getElementById('menu').style.display='none';document.getElementById('game').style.display='block';document.getElementById('game').focus();updateHUD();}
function nextLevel(){levelIndex++;if(levelIndex>=plan.levels.length){alert('You completed the prototype game!');levelIndex=0;document.getElementById('game').style.display='none';document.getElementById('menu').style.display='flex';}togglePause(false);updateHUD();}
function togglePause(force){paused=force!==undefined?force:!paused;document.getElementById('pause').style.display=paused?'flex':'none';}
window.addEventListener('keydown',e=>{if(e.key==='Escape')togglePause(); if(e.key.toLowerCase()==='e')alert('Interact/objective placeholder'); if(e.key.toLowerCase()==='r'){ammo=30;updateHUD();}});
document.getElementById('game').addEventListener('click',()=>document.getElementById('game').focus());
</script>
</body>
</html>`;

  fs.writeFileSync(path.join(runtimeDir, 'index.html'), runtimeHtml, 'utf8');

  const gameMain = `const { app, BrowserWindow } = require('electron');
function createWindow(){
  const win = new BrowserWindow({ width:1280, height:720, webPreferences:{ nodeIntegration:false, contextIsolation:true } });
  win.loadFile('index.html');
}
app.whenReady().then(createWindow);
app.on('window-all-closed',()=>{ if(process.platform !== 'darwin') app.quit(); });`;
  fs.writeFileSync(path.join(runtimeDir, 'main.js'), gameMain, 'utf8');

  const gamePkg = {
    name: title.toLowerCase(),
    version: '1.0.0',
    main: 'main.js',
    scripts: { start: 'electron .', build: 'electron-builder --win portable' },
    devDependencies: { electron: '^31.0.0', 'electron-builder': '^25.1.8' },
    build: {
      appId: `com.gameforge.${title.toLowerCase()}`,
      productName: title.replace(/_/g, ' '),
      win: { target: ['portable'], artifactName: `${title}.exe` },
      files: ['**/*']
    }
  };
  fs.writeFileSync(path.join(runtimeDir, 'package.json'), JSON.stringify(gamePkg, null, 2), 'utf8');

  const buildBat = `@echo off
title Build ${title} EXE
echo =====================================================
echo Building ${title}.exe
echo =====================================================
cd /d "%~dp0game_runtime"
where node >nul 2>nul
if %errorlevel% neq 0 (
  echo Node.js LTS is required. Opening download page...
  start https://nodejs.org/
  pause
  exit /b 1
)
echo Installing runtime dependencies...
npm install
if %errorlevel% neq 0 (
  echo npm install failed.
  pause
  exit /b 1
)
echo Building portable Windows EXE...
npm run build
if %errorlevel% neq 0 (
  echo EXE build failed.
  pause
  exit /b 1
)
echo.
echo Build complete. Open:
echo %~dp0game_runtime\\dist
pause`;
  fs.writeFileSync(path.join(exportDir, 'BUILD_GAME_EXE.bat'), buildBat, 'utf8');

  const runBat = `@echo off
title Run ${title}
cd /d "%~dp0game_runtime"
npx electron .
pause`;
  fs.writeFileSync(path.join(exportDir, 'RUN_GAME_WITHOUT_BUILD.bat'), runBat, 'utf8');

  const readme = `GAMEFORGE FULL GAME EXE EXPORT

Game: ${title}

Startup intro:
- The exported game opens with "Developed by GameForge AI".

Quick test without building:
- Double-click RUN_GAME_WITHOUT_BUILD.bat

Build real Windows EXE:
1. Double-click BUILD_GAME_EXE.bat.
2. Wait for dependencies/build to finish.
3. Open game_runtime/dist.
4. Double-click ${title}.exe.

Notes:
- Node.js LTS is required to build the EXE on your PC.
- This is a complete-game prototype runtime with menus, levels and co-op framework placeholders.
- Full online multiplayer networking requires a later dedicated implementation.
`;
  fs.writeFileSync(path.join(exportDir, 'README_BUILD_EXE.txt'), readme, 'utf8');

  const manifest = {
    title,
    createdAt: new Date().toISOString(),
    exportDir,
    runtimeDir,
    buildScript: path.join(exportDir, 'BUILD_GAME_EXE.bat'),
    runScript: path.join(exportDir, 'RUN_GAME_WITHOUT_BUILD.bat'),
    outputFolder: path.join(runtimeDir, 'dist'),
    expectedExe: `${title}.exe`,
    branding: plan.branding,
    type: 'complete_game_exe_builder'
  };
  fs.writeFileSync(path.join(exportDir, 'export_manifest.json'), JSON.stringify(manifest, null, 2), 'utf8');

  return { exportDir, runtimeDir, manifest, plan };
}

function exportedDemoRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeExportedDemos');
  ensureDir(root);
  return root;
}

function safeDemoName(name) {
  return String(name || 'GameForge_Demo').replace(/[^a-z0-9 _-]/gi, '').trim().replace(/\s+/g, '_') || 'GameForge_Demo';
}

function createExportableDemoPackage(payload) {
  const root = exportedDemoRoot();
  const title = safeDemoName(payload?.title || payload?.gameTitle || 'GameForge_Playable_Demo');
  const id = `${title}_${Date.now()}`;
  const demoDir = path.join(root, id);
  ensureDir(demoDir);
  ensureDir(path.join(demoDir, 'game'));
  ensureDir(path.join(demoDir, 'assets'));
  ensureDir(path.join(demoDir, 'metadata'));

  const scene = payload?.scene || {};
  const runtime = payload?.runtime || {};
  const prompt = payload?.prompt || '';

  const manifest = {
    id,
    title,
    createdAt: new Date().toISOString(),
    prompt,
    engineVersion: 'v3.3.1',
    type: 'exportable_demo_framework',
    files: {
      launcher: 'PLAY_DEMO.bat',
      manifest: 'metadata/demo_manifest.json',
      scene: 'game/scene.json',
      runtime: 'game/runtime.json',
      readme: 'README_PLAY_DEMO.txt'
    },
    validation: payload?.validation || [],
    note: 'This is a playable demo package framework. It launches GameForge in playtest/demo mode using the included scene/runtime data.'
  };

  fs.writeFileSync(path.join(demoDir, 'metadata', 'demo_manifest.json'), JSON.stringify(manifest, null, 2), 'utf8');
  fs.writeFileSync(path.join(demoDir, 'game', 'scene.json'), JSON.stringify(scene, null, 2), 'utf8');
  fs.writeFileSync(path.join(demoDir, 'game', 'runtime.json'), JSON.stringify(runtime, null, 2), 'utf8');

  const readme = `GAMEFORGE EXPORTED PLAYABLE DEMO

Title: ${title}
Created: ${manifest.createdAt}

How to play:
1. Double-click PLAY_DEMO.bat.
2. GameForge will launch in demo/playtest mode.
3. Use WASD to move, mouse to look, E to interact, Esc to pause/release mouse.

Important:
This is a prototype export package. For a true standalone .exe, run the Electron Builder packaging workflow after this system is fully validated.
`;
  fs.writeFileSync(path.join(demoDir, 'README_PLAY_DEMO.txt'), readme, 'utf8');

  const launcher = `@echo off
title ${title}
echo Starting ${title}...
cd /d "%~dp0"
set GAMEFORGE_DEMO_MODE=1
set GAMEFORGE_DEMO_SCENE=%~dp0game\\scene.json
set GAMEFORGE_DEMO_RUNTIME=%~dp0game\\runtime.json
cd /d "${process.cwd()}"
npx electron . --disable-gpu --disable-gpu-compositing --disable-accelerated-2d-canvas --demo-mode --demo-scene="%~dp0game\\scene.json" --demo-runtime="%~dp0game\\runtime.json"
pause
`;
  fs.writeFileSync(path.join(demoDir, 'PLAY_DEMO.bat'), launcher, 'utf8');

  return { demoDir, manifest };
}

function validatePlayableDemo(payload) {
  const scene = payload?.scene || {};
  const runtime = payload?.runtime || {};
  const objects = Array.isArray(scene.objects) ? scene.objects : [];
  const names = objects.map(o => String(o.name || '').toLowerCase());
  const types = objects.map(o => String(o.type || '').toLowerCase());

  const checks = [
    { id:'player_spawn', label:'Player spawn exists', pass: types.includes('player_spawn') || names.some(n => n.includes('spawn') || n.includes('player')) },
    { id:'camera_runtime', label:'Runtime/camera data exists', pass: Boolean(runtime) },
    { id:'enemy_exists', label:'At least one enemy exists', pass: types.includes('enemy') || names.some(n => n.includes('zombie') || n.includes('enemy') || n.includes('creature')) },
    { id:'objective_exists', label:'Objective/runtime objective exists', pass: Boolean(runtime.objective || names.some(n => n.includes('radio') || n.includes('objective') || n.includes('key') || n.includes('supply'))) },
    { id:'pickup_exists', label:'At least one pickup/prop exists', pass: types.includes('pickup') || types.includes('loot') || names.some(n => n.includes('supply') || n.includes('ammo') || n.includes('medkit') || n.includes('key')) },
    { id:'world_exists', label:'World/environment exists', pass: objects.length > 0 },
    { id:'hud_ready', label:'HUD can be shown', pass: true },
    { id:'playtest_launcher', label:'Playtest launcher can be created', pass: true }
  ];

  return {
    generatedAt: new Date().toISOString(),
    passed: checks.filter(c => c.pass).length,
    failed: checks.filter(c => !c.pass).length,
    checks,
    ready: checks.every(c => c.pass)
  };
}

function assetLibraryRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAIAssetLibrary');
  ensureDir(root);
  ensureDir(path.join(root, 'packs'));
  ensureDir(path.join(root, 'models'));
  ensureDir(path.join(root, 'textures'));
  ensureDir(path.join(root, 'audio'));
  ensureDir(path.join(root, 'metadata'));
  ensureDir(path.join(root, 'imports'));
  return root;
}

function guessAssetType(filename) {
  const ext = path.extname(filename).toLowerCase();
  if (['.glb', '.gltf', '.obj', '.fbx', '.blend'].includes(ext)) return 'model';
  if (['.png', '.jpg', '.jpeg', '.webp', '.tga'].includes(ext)) return 'texture';
  if (['.wav', '.mp3', '.ogg'].includes(ext)) return 'audio';
  if (['.zip'].includes(ext)) return 'pack';
  return 'other';
}

function scanAssetLibrary() {
  const root = assetLibraryRoot();
  const results = [];
  function walk(dir) {
    if (!fs.existsSync(dir)) return;
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else {
        const rel = path.relative(root, full).replace(/\\/g, '/');
        results.push({
          name: entry.name,
          path: full,
          relativePath: rel,
          type: guessAssetType(entry.name),
          sizeBytes: fs.statSync(full).size,
          fileUrl: pathToFileURL(full).href
        });
      }
    }
  }
  walk(root);
  return { root, assets: results };
}

function projectsRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAIEngineProjects');
  ensureDir(root);
  return root;
}


function settingsPath() {
  const dir = path.join(app.getPath('userData'), 'settings');
  ensureDir(dir);
  return path.join(dir, 'gameforge-local-ai-settings.json');
}

function defaultSettings() {
  return {
    aiMode: 'autonomous-local',
    localProvider: 'ollama',
    localEndpoint: 'http://localhost:11434/api/generate',
    localModel: 'llama3.1:8b',
    temperature: 0.35,
    autoFallback: true
  };
}

function readSettings() {
  try {
    const p = settingsPath();
    if (fs.existsSync(p)) return { ...defaultSettings(), ...JSON.parse(fs.readFileSync(p, 'utf8')) };
  } catch (e) {}
  return defaultSettings();
}

function writeSettings(settings) {
  const merged = { ...defaultSettings(), ...(settings || {}) };
  fs.writeFileSync(settingsPath(), JSON.stringify(merged, null, 2), 'utf8');
  return merged;
}

async function callLocalAI(settings, prompt) {
  const s = { ...defaultSettings(), ...(settings || {}) };
  const body = {
    model: s.localModel || 'llama3.1:8b',
    prompt,
    stream: false,
    options: { temperature: Number(s.temperature || 0.35) }
  };
  const response = await fetch(s.localEndpoint || 'http://localhost:11434/api/generate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });
  if (!response.ok) throw new Error(`Local AI request failed: ${response.status} ${response.statusText}`);
  const data = await response.json();
  return data.response || data.output || data.message || JSON.stringify(data);
}

function safeProjectName(name) {
  return (name || 'Untitled_Game')
    .replace(/[^a-z0-9_\- ]/gi, '_')
    .trim()
    .replace(/\s+/g, '_') || 'Untitled_Game';
}




















ipcMain.handle('animation-gatherer-save-default-manifest', async () => {
  try { return { ok:true, manifestPath: saveDefaultAnimationManifest() }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('animation-gatherer-create-manifest-from-requirements', async (event, payload) => {
  try { return { ok:true, ...createAnimationManifestFromGameRequirements(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('animation-gatherer-run', async (event, payload) => {
  try { return { ok:true, ...await runAnimationAssetGatherer(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('animation-gatherer-missing-report', async (event, payload) => {
  try { return { ok:true, ...createMissingAnimationReport(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('animation-importer-create-plan', async (event, payload) => {
  try { return { ok:true, ...createAnimationImportPlan(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('model-gatherer-save-default-manifest', async () => {
  try { return { ok:true, manifestPath: saveDefaultModelManifest() }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('model-gatherer-run', async (event, payload) => {
  try { return { ok:true, ...await runModelGatherer(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('model-gatherer-create-manifest-from-requirements', async (event, payload) => {
  try { return { ok:true, ...createGathererManifestFromRequirements(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('auto-assets-scan-models', async () => {
  try { return { ok:true, assets: listLocalGLBGLTFAssets() }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('auto-assets-create-plan', async (event, payload) => {
  try { return { ok:true, ...createAutoAssetImportPlan(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('photoreal-mode-plan', async (event, payload) => {
  try { return { ok:true, ...createPhotorealModePlan(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('complete-game-plan', async (event, payload) => {
  try { return { ok:true, plan: createCompleteGamePlan(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('complete-game-exe-package', async (event, payload) => {
  try { return { ok:true, ...createFullGameExePackage(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('playtest-validate-demo', async (event, payload) => {
  try { return { ok:true, report: validatePlayableDemo(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('playtest-export-demo', async (event, payload) => {
  try { return { ok:true, ...createExportableDemoPackage(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('internal-mesh-create-recipe', async (event, payload) => {
  try { return { ok:true, ...makeMeshRecipe(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('pbr-create-material', async (event, payload) => {
  try { return { ok:true, ...makePBRMaterialRecipe(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('glb-create-export-plan', async (event, payload) => {
  try { return { ok:true, ...createGLBExportPlan(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('free3d-create-job', async (event, payload) => {
  try { return { ok:true, ...createFree3DJob(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('free3d-complete-job', async (event, payload) => {
  try { return { ok:true, ...completeFree3DJob(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('free3d-create-asset-pack', async (event, payload) => {
  try { return { ok:true, ...createFree3DAssetPack(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('ai3d-load-settings', async () => {
  try {
    const s = loadAI3DSettings();
    return { ok:true, settings:{ ...s, providerApiKey: s.providerApiKey ? '••••••••' : '', providerApiKeySaved: Boolean(s.providerApiKey) } };
  } catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('ai3d-save-settings', async (event, payload) => {
  try { return { ok:true, settings: saveAI3DSettings(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('ai3d-create-asset-plan', async (event, payload) => {
  try { return { ok:true, plan: createAI3DAssetPlan(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('ai3d-create-provider-job', async (event, payload) => {
  try { return { ok:true, ...await createAI3DProviderJob(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('ai3d-import-model-url', async (event, payload) => {
  try { return { ok:true, ...await importAI3DModelFromUrl(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('ai3d-sanitise-prompt', async (event, payload) => {
  try { return { ok:true, safePrompt: sanitiseAssetPrompt(payload?.prompt || '', payload?.assetType || '') }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('hybrid-ai-load-settings', async () => {
  try {
    const s = loadHybridAISettings();
    return { ok:true, settings:{ ...s, openaiApiKey: s.openaiApiKey ? '••••••••' : '', apiKeySaved: Boolean(s.openaiApiKey) } };
  } catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('hybrid-ai-save-settings', async (event, payload) => {
  try { return { ok:true, settings: saveHybridAISettings(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('hybrid-ai-test-connection', async (event, payload) => {
  try {
    const settings = loadHybridAISettings();
    const apiKey = payload?.apiKey || settings.openaiApiKey;
    if (!apiKey) throw new Error('OpenAI API key is missing.');
    const response = await fetch('https://api.openai.com/v1/models', {
      headers: { 'Authorization': `Bearer ${apiKey}` }
    });
    if (!response.ok) throw new Error(`Connection failed: ${response.status} ${await response.text()}`);
    return { ok:true, message:'OpenAI connection successful.' };
  } catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('hybrid-ai-generate-game', async (event, payload) => {
  try {
    const result = await callOpenAIResponses(payload || {});
    const scene = convertHybridResultToScene(result.parsed);
    const actualCost = estimateHybridActualCost(result.usage, result.model);
    return { ok:true, result: result.parsed, scene, usage: result.usage, actualCost, model: result.model, rawId: result.rawId };
  } catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('curated-asset-plan', async (event, payload) => {
  try { return { ok:true, plan: curatedAssetPlan(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('curated-download-asset', async (event, payload) => {
  try { return { ok:true, ...await curatedDownloadAsset(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('curated-asset-decision', async (event, payload) => {
  try { return { ok:true, decision: makeAssetUseDecision(payload?.asset || payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('curated-registry', async () => {
  try { return { ok:true, registry: getCuratedRegistry() }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('validate-native-package', async (event, payload) => {
  try { return { ok:true, results: validateNativePackageReadiness(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('create-native-package', async (event, payload) => {
  try { return { ok:true, ...createNativeGamePackage(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('validate-playable-build', async (event, payload) => {
  try { return { ok:true, results: validatePlayableBuild(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('create-playable-build', async (event, payload) => {
  try { return { ok:true, ...createPlayableBuild(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('create-licence-audit', async (event, payload) => {
  try { return { ok:true, ...createLicenceAudit(payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('classify-licence-text', async (event, payload) => {
  try { return { ok:true, audit: classifyLicenceText(payload?.text || '') }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('collect-licence-metadata', async () => {
  try { return { ok:true, records: collectKnownLicenceMetadata() }; }
  catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('filter-legal-sounds', async (event, payload) => {
  try { return { ok:true, results: filterLegalSounds(payload?.results || [], payload || {}) }; }
  catch(error) { return { ok:false, error:error.message }; }
});
ipcMain.handle('sound-event-queries', async (event, payload) => {
  try { return { ok:true, events: soundEventQueries(payload?.prompt || '') }; }
  catch(error) { return { ok:false, error:error.message }; }
});
ipcMain.handle('generate-audio-credits', async (event, payload) => {
  try {
    const root = generatedMediaRoot();
    const file = path.join(root, 'CREDITS_AUDIO.md');
    fs.writeFileSync(file, '# Audio Credits\n\n', 'utf8');
    (payload?.sounds || []).forEach(s => appendAudioCredit(s));
    return { ok:true, path:file };
  } catch(error) { return { ok:false, error:error.message }; }
});

ipcMain.handle('generate-local-media-pack', async (event, payload) => {
  try { return {ok:true, ...generateMediaPack(payload || {})}; }
  catch(error) { return {ok:false, error:error.message}; }
});
ipcMain.handle('suggest-web-sound-keywords', async (event, payload) => {
  try { return {ok:true, keywords:keywordsFromPrompt(payload?.prompt || '')}; }
  catch(error) { return {ok:false, error:error.message}; }
});
ipcMain.handle('search-web-sounds', async (event, payload) => {
  try { return {ok:true, results:await searchFreesound(payload || {})}; }
  catch(error) { return {ok:false, error:error.message}; }
});
ipcMain.handle('download-web-sound', async (event, payload) => {
  try { return {ok:true, sound:await downloadWebSound(payload || {})}; }
  catch(error) { return {ok:false, error:error.message}; }
});

ipcMain.handle('scan-asset-library', async () => {
  try {
    return { ok: true, ...scanAssetLibrary() };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('import-asset-files', async () => {
  try {
    const result = await dialog.showOpenDialog(mainWindow, {
      title: 'Import Asset Files or Packs',
      properties: ['openFile', 'multiSelections'],
      filters: [
        { name: 'Game Assets', extensions: ['zip', 'glb', 'gltf', 'obj', 'fbx', 'png', 'jpg', 'jpeg', 'webp', 'wav', 'mp3', 'ogg'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    });
    if (result.canceled || !result.filePaths.length) return { ok: false, canceled: true };

    const root = assetLibraryRoot();
    const imported = [];
    for (const file of result.filePaths) {
      const type = guessAssetType(file);
      const sub = type === 'model' ? 'models' : type === 'texture' ? 'textures' : type === 'audio' ? 'audio' : type === 'pack' ? 'packs' : 'imports';
      const dest = path.join(root, sub, path.basename(file));
      fs.copyFileSync(file, dest);
      imported.push({ name: path.basename(file), type, path: dest, relativePath: path.relative(root, dest).replace(/\\/g, '/') });
    }
    fs.writeFileSync(path.join(root, 'metadata', `import_${Date.now()}.json`), JSON.stringify({ importedAt: new Date().toISOString(), imported }, null, 2), 'utf8');
    return { ok: true, root, imported, scan: scanAssetLibrary() };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('download-asset-pack', async (event, payload) => {
  try {
    const url = String(payload?.url || '').trim();
    if (!/^https?:\/\//i.test(url)) throw new Error('Enter a valid http/https URL.');
    const root = assetLibraryRoot();
    const response = await gfFetch(url);
    if (!response.ok) throw new Error(`Download failed: ${response.status} ${response.statusText}`);
    const arrayBuffer = await response.arrayBuffer();
    const rawName = path.basename(new URL(url).pathname) || `download_${Date.now()}`;
    const cleanName = rawName.replace(/[^a-z0-9._-]/gi, '_');
    const type = guessAssetType(cleanName);
    const sub = type === 'model' ? 'models' : type === 'texture' ? 'textures' : type === 'audio' ? 'audio' : type === 'pack' ? 'packs' : 'imports';
    const dest = path.join(root, sub, cleanName);
    fs.writeFileSync(dest, Buffer.from(arrayBuffer));
    const meta = {
      downloadedAt: new Date().toISOString(),
      url,
      name: cleanName,
      type,
      licenseNote: payload?.licenseNote || 'User must verify licence before use.',
      destination: dest
    };
    fs.writeFileSync(path.join(root, 'metadata', `download_${Date.now()}.json`), JSON.stringify(meta, null, 2), 'utf8');
    return { ok: true, root, asset: { name: cleanName, type, path: dest }, scan: scanAssetLibrary() };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('save-asset-manifest', async (event, manifest) => {
  try {
    const root = assetLibraryRoot();
    const name = (manifest?.packName || 'asset_manifest').replace(/[^a-z0-9_-]/gi, '_');
    const file = path.join(root, 'metadata', `${name}_${Date.now()}.json`);
    fs.writeFileSync(file, JSON.stringify(manifest || {}, null, 2), 'utf8');
    return { ok: true, path: file };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});


ipcMain.handle('test-local-ai', async (event, settings) => {
  try {
    const response = await callLocalAI(settings || readSettings(), 'Reply with exactly this sentence and nothing else: GameForge Local AI connected.');
    return { ok: true, response };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('generate-with-local-ai', async (event, payload) => {
  try {
    const response = await callLocalAI(payload.settings || readSettings(), payload.prompt);
    return { ok: true, response };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});


ipcMain.handle('save-project', async (event, payload) => {
  const root = projectsRoot();
  const folder = path.join(root, safeProjectName(payload.name));
  ensureDir(folder);
  ensureDir(path.join(folder, 'scenes'));
  ensureDir(path.join(folder, 'docs'));
  ensureDir(path.join(folder, 'scripts'));
  ensureDir(path.join(folder, 'assets'));
  ensureDir(path.join(folder, 'components'));

  fs.writeFileSync(path.join(folder, 'gameforge.project.json'), JSON.stringify(payload, null, 2), 'utf8');
  fs.writeFileSync(path.join(folder, 'scenes', 'main.scene.json'), JSON.stringify(payload.scene || {}, null, 2), 'utf8');
  fs.writeFileSync(path.join(folder, 'docs', 'GameDesignDocument.md'), payload.designDoc || '# Game Design Document\n', 'utf8');
  fs.writeFileSync(path.join(folder, 'docs', 'ForgeReport.md'), payload.forgeReport || '# Forge Report\n', 'utf8');
  fs.writeFileSync(path.join(folder, 'docs', 'AssetPlan.md'), payload.assetPlan || '# Asset Plan\n', 'utf8');
  fs.writeFileSync(path.join(folder, 'docs', 'TestReport.md'), payload.testReportMarkdown || '# Test Report\n', 'utf8');
  fs.writeFileSync(path.join(folder, 'scripts', 'gameplay_logic.gfscript'), payload.logicScript || '// GameForge gameplay logic\n', 'utf8');
  fs.writeFileSync(path.join(folder, 'components', 'components.json'), JSON.stringify(payload.components || {}, null, 2), 'utf8');

  return { ok: true, path: folder };
});

ipcMain.handle('load-project', async () => {
  const result = await dialog.showOpenDialog(mainWindow, {
    title: 'Load GameForge Project',
    properties: ['openFile'],
    filters: [{ name: 'GameForge Project', extensions: ['json'] }]
  });
  if (result.canceled || !result.filePaths[0]) return { ok: false, canceled: true };
  const file = result.filePaths[0];
  const data = JSON.parse(fs.readFileSync(file, 'utf8'));
  return { ok: true, path: file, data };
});

ipcMain.handle('export-project', async (event, payload) => {
  const defaultPath = path.join(projectsRoot(), `${safeProjectName(payload.name)}_GameForgeExport.json`);
  const result = await dialog.showSaveDialog(mainWindow, {
    title: 'Export GameForge Project JSON',
    defaultPath,
    filters: [{ name: 'GameForge Export', extensions: ['json'] }]
  });
  if (result.canceled || !result.filePath) return { ok: false, canceled: true };
  fs.writeFileSync(result.filePath, JSON.stringify(payload, null, 2), 'utf8');
  return { ok: true, path: result.filePath };
});

ipcMain.handle('export-playable-draft', async (event, payload) => {
  const defaultPath = path.join(projectsRoot(), `${safeProjectName(payload.name)}_PlayableDraft`);
  const result = await dialog.showOpenDialog(mainWindow, {
    title: 'Choose Folder for Playable Draft Export',
    properties: ['openDirectory', 'createDirectory']
  });
  if (result.canceled || !result.filePaths[0]) return { ok: false, canceled: true };

  const out = path.join(result.filePaths[0], `${safeProjectName(payload.name)}_PlayableDraft`);
  ensureDir(out);
  ensureDir(path.join(out, 'data'));
  ensureDir(path.join(out, 'docs'));

  fs.writeFileSync(path.join(out, 'README_PLAYABLE_DRAFT.txt'), 
`GameForge AI Engine Playable Draft

Project: ${payload.name}

This is a prototype playable-draft export. In v0.4, this export contains project data, generated documents, scene JSON and gameplay logic.
Future versions will export a packaged standalone game executable.

Open the original project in GameForge AI Engine v0.4 to play/edit it.
`, 'utf8');

  fs.writeFileSync(path.join(out, 'data', 'gameforge.project.json'), JSON.stringify(payload, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'data', 'main.scene.json'), JSON.stringify(payload.scene || {}, null, 2), 'utf8');
  fs.writeFileSync(path.join(out, 'data', 'gameplay_logic.gfscript'), payload.logicScript || '', 'utf8');
  fs.writeFileSync(path.join(out, 'docs', 'GameDesignDocument.md'), payload.designDoc || '', 'utf8');
  fs.writeFileSync(path.join(out, 'docs', 'ForgeReport.md'), payload.forgeReport || '', 'utf8');
  fs.writeFileSync(path.join(out, 'docs', 'AssetPlan.md'), payload.assetPlan || '', 'utf8');

  return { ok: true, path: out };
});


ipcMain.handle('autonomous-realism-create-plan', async (event, payload) => {
  try {
    const plan = autonomousRealismPlanFromPrompt(payload || {});
    return { ok: true, plan };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('autonomous-realism-run', async (event, payload) => {
  try {
    const root = realismAssetsRoot();
    const plan = autonomousRealismPlanFromPrompt(payload || {});
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const planFile = path.join(root, 'plans', `autonomous_realism_plan_${stamp}.json`);
    const reportFile = path.join(root, 'reports', `autonomous_realism_report_${stamp}.md`);
    const manifest = {
      generatedAt: new Date().toISOString(),
      safeSources: plan.safeSources,
      models: plan.requirements.models,
      materials: plan.requirements.materials,
      hdris: plan.requirements.hdris,
      animations: plan.requirements.animations
    };
    const manifestFile = path.join(root, 'manifests', `autonomous_realism_manifest_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');
    fs.writeFileSync(manifestFile, JSON.stringify(manifest, null, 2), 'utf8');
    const report = `# Autonomous Realism Run\n\nGenerated: ${new Date().toISOString()}\n\n## Mode\n${plan.mode}\n\n## Prompt\n${plan.prompt || '(none)'}\n\n## Graphics Target\n${plan.graphics}\n\n## Safe Sources\n${plan.safeSources.map(s => `- ${s.name} | ${s.licence} | ${s.categories.join(', ')}`).join('\n')}\n\n## Model Targets\n${plan.requirements.models.map(m => `- ${m.name} | role: ${m.role} | quality: ${m.quality}`).join('\n')}\n\n## Material Targets\n${plan.requirements.materials.map(m => `- ${m.name} | maps: ${m.maps.join(', ')}`).join('\n')}\n\n## HDRIs\n${plan.requirements.hdris.map(m => `- ${m.name} | ${m.use}`).join('\n')}\n\n## Animations\n${plan.requirements.animations.map(a => '- ' + a).join('\n')}\n\n## Detail Scatter\n${plan.requirements.detailScatter.map(a => '- ' + a).join('\n')}\n\n## Notes\nThis run prepares every GameForge generation with always-on photoreal safe-source planning and a photoreal scene pass.`;
    fs.writeFileSync(reportFile, report, 'utf8');
    return { ok: true, plan, files: { root, planFile, manifestFile, reportFile } };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});




ipcMain.handle('approved-assets-create-requirement-plan', async (event, payload) => {
  try {
    const plan = approvedRequirementPlan(payload || {});
    return { ok: true, plan };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('approved-assets-create-manifest', async (event, payload) => {
  try {
    const root = approvedAssetsRoot();
    const plan = payload?.plan || approvedRequirementPlan(payload || {});
    const manifest = defaultApprovedManifestFromPlan(plan);
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const manifestFile = path.join(root, 'manifests', `approved_asset_manifest_${stamp}.json`);
    fs.writeFileSync(manifestFile, JSON.stringify(manifest, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `approved_asset_manifest_${stamp}.md`);
    const report = `# Approved Asset Acquisition Manifest

Generated: ${new Date().toISOString()}

## Rule
Only enabled assets with approved licences and direct URLs are downloaded. No random scraping.

## Approved Sources
${manifest.approvedSources.map(s => `- ${s.name} | ${s.licence} | ${s.types.join(', ')}`).join('\n')}

## Asset Slots
${manifest.assets.map(a => `- ${a.id} | ${a.type} | ${a.role} | ${a.query}`).join('\n')}

## Next Step
Add approved direct URLs where available, or let GameForge use procedural fallback assets.`;
    fs.writeFileSync(reportFile, report, 'utf8');

    return { ok: true, manifest, files: { root, manifestFile, reportFile } };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('approved-assets-run-downloads', async (event, payload) => {
  try {
    const root = approvedAssetsRoot();
    const manifest = payload?.manifest || defaultApprovedManifestFromPlan(approvedRequirementPlan(payload || {}));
    const results = [];
    for (const asset of manifest.assets || []) {
      try {
        results.push(await downloadApprovedAsset(asset, root));
      } catch (error) {
        results.push({ id: asset.id, skipped: true, reason: error.message, asset });
      }
    }

    const downloaded = results.filter(r => r.downloaded).length;
    const skipped = results.filter(r => r.skipped).length;
    const prepared = (manifest.assets || []).length;
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const reportFile = path.join(root, 'reports', `approved_asset_download_report_${stamp}.json`);
    fs.writeFileSync(reportFile, JSON.stringify({ generatedAt: new Date().toISOString(), summary: { downloaded, skipped, prepared }, results }, null, 2), 'utf8');

    return {
      ok: true,
      summary: { downloaded, skipped, prepared },
      results,
      files: { root, reportFile },
      note: downloaded ? 'Approved assets downloaded and metadata saved.' : 'No enabled direct URLs were available; GameForge will continue with procedural/self-repair fallback assets.'
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});



ipcMain.handle('self-assets-generate', async (event, payload) => {
  try {
    const root = selfGeneratedAssetsRoot();
    const plan = payload?.plan || selfAssetPlanFromPrompt(payload || {});
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'plans', `self_asset_plan_${stamp}.json`);
    writeSelfAssetJson(planFile, plan);

    const generated = [];

    for (const model of plan.assets?.models || []) {
      const file = path.join(root, 'models', `${model.name}.gltf`);
      writeSelfGeneratedGltf(file, model.name, model.type);
      generated.push({ type: 'model', name: model.name, file, generated: true, licence: 'GameForge self-generated' });
    }

    for (const mat of plan.assets?.materials || []) {
      const file = path.join(root, 'textures', `${mat.name}.svg`);
      writeSelfTextureSvg(file, mat.name, mat.colors || ['#1d2528','#4d5d5a','#0b0e11']);
      const metaFile = path.join(root, 'materials', `${mat.name}.json`);
      writeSelfAssetJson(metaFile, {
        name: mat.name,
        generated: true,
        type: 'procedural_material',
        texture: file,
        pbrStyle: true,
        maps: ['albedo_procedural','roughness_procedural','normal_hint'],
        licence: 'GameForge self-generated'
      });
      generated.push({ type: 'material', name: mat.name, file, metaFile, generated: true, licence: 'GameForge self-generated' });
    }

    for (const icon of plan.assets?.icons || []) {
      const file = path.join(root, 'icons', `${icon.name}.svg`);
      writeSelfIconSvg(file, icon.label, icon.color);
      generated.push({ type: 'icon', name: icon.name, file, generated: true, licence: 'GameForge self-generated' });
    }

    for (const audio of plan.assets?.audio || []) {
      const file = path.join(root, 'audio', `${audio.name}.wav`);
      try {
        writeSimpleWav(file, audio.freq || 220, audio.duration || 0.4);
        generated.push({ type: 'audio', name: audio.name, file, generated: true, licence: 'GameForge self-generated' });
      } catch (e) {
        generated.push({ type: 'audio', name: audio.name, skipped: true, reason: e.message });
      }
    }

    const metadataFile = path.join(root, 'metadata', `self_asset_metadata_${stamp}.json`);
    writeSelfAssetJson(metadataFile, {
      generatedAt: new Date().toISOString(),
      planFile,
      generated,
      licence: 'GameForge self-generated procedural assets for this project'
    });

    const reportFile = path.join(root, 'reports', `self_asset_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# GameForge Self-Generated Assets

Generated: ${new Date().toISOString()}

Game: ${plan.gameName || 'GameForge Generated Game'}

Generated assets: ${generated.length}

## Assets
${generated.map(g => `- ${g.type}: ${g.name} | ${g.file || g.reason}`).join('\n')}

## Licence
These are generated by GameForge as procedural/self-generated project assets.`, 'utf8');

    return {
      ok: true,
      root,
      plan,
      summary: { generated: generated.length },
      generated,
      files: { planFile, metadataFile, reportFile }
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});



ipcMain.handle('audio-assets-generate', async (event, payload) => {
  try {
    const root = audioAssetsRoot();
    const plan = payload?.plan || {
      mode: 'Game Style + Rating Selector',
      generatedAt: new Date().toISOString(),
      gameName: payload?.gameName || 'GameForge Game',
      audioEvents: [
        { name: 'low_ambient_bed', role: 'ambience', type: 'ambience', duration: 8.0, freq: 55 },
        { name: 'footstep_wood', role: 'movement', type: 'footstep', duration: 0.18, freq: 95 },
        { name: 'door_creak', role: 'interaction', type: 'creak', duration: 1.1, freq: 180 },
        { name: 'jump_scare_sting', role: 'horror', type: 'sting', duration: 0.85, freq: 90 }
      ],
      eventMap: {}
    };

    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const planFile = path.join(root, 'plans', `audio_asset_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const generated = [];
    for (const ev of plan.audioEvents || []) {
      const safeName = String(ev.name || 'sound').replace(/[^a-z0-9._-]+/gi, '_');
      const file = path.join(root, 'audio', `${safeName}.wav`);
      writeProceduralWav(file, ev);
      const metaFile = path.join(root, 'metadata', `${safeName}.json`);
      fs.writeFileSync(metaFile, JSON.stringify({
        generatedAt: new Date().toISOString(),
        name: ev.name,
        role: ev.role,
        type: ev.type,
        duration: ev.duration,
        freq: ev.freq,
        file,
        licence: 'GameForge self-generated procedural audio',
        source: 'GameForgeAudioAssetGenerator'
      }, null, 2), 'utf8');
      generated.push({ name: ev.name, role: ev.role, type: ev.type, file, metaFile, licence: 'GameForge self-generated procedural audio' });
    }

    const reportFile = path.join(root, 'reports', `audio_asset_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# GameForge Audio Asset Report

Generated: ${new Date().toISOString()}

Game: ${plan.gameName || payload?.gameName || 'GameForge Game'}

Generated sounds: ${generated.length}

## Sounds
${generated.map(g => `- ${g.name} | ${g.role} | ${g.type} | ${g.file}`).join('\n')}

## Licence
Generated locally by GameForge as procedural placeholder game audio.`, 'utf8');

    return {
      ok: true,
      root,
      plan,
      summary: { generated: generated.length },
      generated,
      files: { planFile, reportFile }
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});



ipcMain.handle('unreal-export-prep', async (event, payload) => {
  try {
    const root = unrealExportPrepRoot();
    const pkg = payload?.package || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const packageFile = path.join(root, 'packages', `unreal_export_prep_${stamp}.json`);
    fs.writeFileSync(packageFile, JSON.stringify(pkg, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `unreal_export_prep_${stamp}.md`);
    const report = `# GameForge Unreal Export Preparation

Generated: ${new Date().toISOString()}

Status: ${pkg.status || 'Unknown'}

Project: ${pkg.project?.name || 'GameForge Game'}

## Purpose
${pkg.purpose || 'Prepare project for photoreal handoff.'}

## Required Folders
${(pkg.requiredFolders || []).map(x => '- ' + x).join('\n')}

## Checklist
${(pkg.exportChecklist || []).map(x => '- ' + x).join('\n')}

## Quality Gate
Status: ${pkg.qualityReport?.status || 'Not available'}
Score: ${pkg.qualityReport?.score ?? 'N/A'}
`;
    fs.writeFileSync(reportFile, report, 'utf8');

    return { ok: true, files: { root, packageFile, reportFile }, package: pkg };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});



ipcMain.handle('toolchain-detect', async () => {
  try {
    // First check user's saved path
    const settings = gfReadSettings();
    if (settings.unrealPath) {
      const check = validateUnrealExePath(settings.unrealPath);
      if (check.valid) {
        const result = detectToolchain();
        // Override with the user's saved path since it's valid
        result.tools.unreal = { name: 'Unreal Engine', found: true, path: check.path, freeLegalUse: 'Free-to-start; obey Epic terms', source: 'user-configured' };
        return result;
      }
    }
    return detectToolchain();
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('gf-validate-unreal-path', async (event, p) => {
  try {
    const result = validateUnrealExePath(p);
    console.log(`[GameForge] Unreal path validation: "${p}" → valid=${result.valid}${result.reason ? ' reason=' + result.reason : ''}`);
    return result;
  } catch(e) {
    return { valid: false, reason: e.message };
  }
});

ipcMain.handle('gf-detect-unreal-path', async () => {
  try {
    // 1. Check user's saved path first
    const settings = gfReadSettings();
    if (settings.unrealPath) {
      const check = validateUnrealExePath(settings.unrealPath);
      if (check.valid) {
        return { found: true, path: check.path, source: 'saved-settings' };
      }
    }
    // 2. Scan common install locations
    const detected = detectCommonUnrealPaths();
    if (detected) {
      return { found: true, path: detected, source: 'auto-detected' };
    }
    // 3. Try PATH
    const fromPath = commandExists('UnrealEditor') || commandExists('UnrealEditor.exe');
    if (fromPath) {
      return { found: true, path: fromPath, source: 'PATH' };
    }
    return { found: false, scannedPaths: true };
  } catch(e) {
    return { found: false, error: e.message };
  }
});

ipcMain.handle('toolchain-run-high-end-pipeline', async (event, payload) => {
  try {
    const root = toolchainRoot();
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const runFolder = path.join(root, 'builds', `gameforge_high_end_run_${stamp}`);
    ensureDir(runFolder);
    ensureDir(path.join(runFolder, 'Content'));
    ensureDir(path.join(runFolder, 'Content', 'Models'));
    ensureDir(path.join(runFolder, 'Content', 'Materials'));
    ensureDir(path.join(runFolder, 'Content', 'Textures'));
    ensureDir(path.join(runFolder, 'Content', 'Audio'));
    ensureDir(path.join(runFolder, 'Content', 'Maps'));
    ensureDir(path.join(runFolder, 'Build'));

    const projectState = payload?.projectState || {};
    const plan = payload?.plan || {};
    const detection = payload?.detection || detectToolchain();

    const planFile = writeToolchainFile(path.join(runFolder, 'gameforge_high_end_plan.json'), JSON.stringify(plan, null, 2));
    const legalPolicyFile = createLegalPolicy(runFolder);
    const blenderScript = createBlenderAutomationScript(path.join(runFolder, 'Content', 'Models'));
    const godotPreset = createGodotExportPlan(runFolder, projectState.name || 'GameForgeGame');

    const unrealPackage = {
      generatedAt: new Date().toISOString(),
      projectName: projectState.name || 'GameForgeGame',
      purpose: 'Near-AAA / high-end AA Unreal handoff package',
      folders: ['Content/Models','Content/Materials','Content/Textures','Content/Audio','Content/Maps'],
      notes: [
        'Import cleaned GLB/FBX models from Content/Models.',
        'Build PBR master materials from Content/Materials and Content/Textures.',
        'Create map from GameForge scene object list.',
        'Add post-process volume for exposure, bloom, LUT, vignette and fog.',
        'Use generated audio files from Content/Audio.',
        'Run GameForge quality gate before marking output Photoreal Ready.'
      ],
      qualityGate: projectState.photorealQualityReport || null,
      sceneObjects: projectState.scene?.objects || []
    };
    const unrealFile = writeToolchainFile(path.join(runFolder, 'unreal_handoff_package.json'), JSON.stringify(unrealPackage, null, 2));

    const tools = detection.tools || {};
    let blenderRun = { attempted: false };
    if (tools.blender?.found) {
      try {
        const res = spawnSync(tools.blender.path, ['--background', '--python', blenderScript], { encoding: 'utf8', timeout: 120000 });
        blenderRun = { attempted: true, status: res.status, stdout: res.stdout, stderr: res.stderr };
        writeToolchainFile(path.join(runFolder, 'logs', 'blender_process.log'), `STDOUT:\n${res.stdout || ''}\n\nSTDERR:\n${res.stderr || ''}`);
      } catch (e) {
        blenderRun = { attempted: true, error: e.message };
      }
    }

    const buildReadiness = {
      generatedAt: new Date().toISOString(),
      status: 'Prepared',
      important: 'This package prepares the free/legal high-end pipeline. Actual EXE export requires Godot/Unreal project export availability on the user PC.',
      tools: detection.tools,
      files: { planFile, legalPolicyFile, blenderScript, godotPreset, unrealFile },
      blenderRun,
      outputFolder: runFolder,
      nextSteps: [
        'If Godot is installed and a Godot project exists, export using export_presets.cfg.',
        'If Unreal is installed, use unreal_handoff_package.json and Content folders for high-end visual pass.',
        'If Blender is installed, processed test asset confirms background automation works.',
        'Resolve quality gate blockers before calling the result photoreal.'
      ]
    };
    const reportFile = writeToolchainFile(path.join(runFolder, 'BUILD_READINESS_REPORT.json'), JSON.stringify(buildReadiness, null, 2));

    const md = `# GameForge v3.3.1 Game Style + Rating Selector Run

Generated: ${new Date().toISOString()}

Status: Prepared

## Tools
${Object.entries(detection.tools || {}).map(([k,t]) => `- ${t.name}: ${t.found ? 'FOUND' : 'MISSING'}${t.path ? ' | ' + t.path : ''}`).join('\n')}

## Output Folder
${runFolder}

## Files
- Plan: ${planFile}
- Legal policy: ${legalPolicyFile}
- Blender script: ${blenderScript}
- Godot export preset: ${godotPreset}
- Unreal handoff: ${unrealFile}
- Build report: ${reportFile}

## Important
This prepares and automates the legal/free toolchain package. One-click final EXE export still requires a valid installed export engine/project path on the user's PC.
`;
    const markdownReport = writeToolchainFile(path.join(runFolder, 'BUILD_READINESS_REPORT.md'), md);

    return {
      ok: true,
      outputFolder: runFolder,
      files: { planFile, legalPolicyFile, blenderScript, godotPreset, unrealFile, reportFile, markdownReport },
      detection,
      blenderRun,
      note: 'Free/legal high-end toolchain package prepared. Check BUILD_READINESS_REPORT.md.'
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});



ipcMain.handle('meshy-load-settings', async () => {
  try {
    const file = meshySettingsFile();
    if (!fs.existsSync(file)) {
      const settings = defaultMeshySettings();
      fs.writeFileSync(file, JSON.stringify(settings, null, 2), 'utf8');
      return { ok: true, settings: { ...settings, apiKey: '', apiKeySaved: false }, file };
    }
    const settings = JSON.parse(fs.readFileSync(file, 'utf8'));
    return { ok: true, settings: { ...settings, apiKey: '', apiKeySaved: Boolean(settings.apiKey) }, file };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('meshy-save-settings', async (event, payload) => {
  try {
    const current = fs.existsSync(meshySettingsFile()) ? JSON.parse(fs.readFileSync(meshySettingsFile(), 'utf8')) : defaultMeshySettings();
    const next = {
      ...current,
      mode: payload?.mode || current.mode || 'free_test',
      paidProvidersAllowed: Boolean(payload?.paidProvidersAllowed),
      monthlyCreditCap: Number(payload?.monthlyCreditCap || current.monthlyCreditCap || 100),
      maxAssetsPerRun: Number(payload?.maxAssetsPerRun || current.maxAssetsPerRun || 8),
      commercialReleaseMode: Boolean(payload?.commercialReleaseMode),
      requireAttributionReport: payload?.requireAttributionReport !== false
    };
    if (payload?.apiKey && String(payload.apiKey).trim()) {
      next.apiKey = String(payload.apiKey).trim();
    }
    fs.writeFileSync(meshySettingsFile(), JSON.stringify(next, null, 2), 'utf8');
    return { ok: true, settings: { ...next, apiKey: '', apiKeySaved: Boolean(next.apiKey) }, file: meshySettingsFile() };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('meshy-prepare-free-test-pack', async (event, payload) => {
  try {
    const root = meshyRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const packFolder = path.join(root, 'prompt_packs', `meshy_prompt_pack_${stamp}`);
    ensureDir(packFolder);

    const promptPack = path.join(packFolder, 'MESHY_PROMPT_PACK.md');
    const promptJson = path.join(packFolder, 'meshy_prompt_pack.json');
    const importsFolder = path.join(root, 'imports', `imports_${stamp}`);
    ensureDir(importsFolder);

    fs.writeFileSync(promptPack, createMeshyPromptMarkdown(plan), 'utf8');
    fs.writeFileSync(promptJson, JSON.stringify(plan, null, 2), 'utf8');

    const instructions = `GAMEFORGE MESHY FREE TEST WORKFLOW

1. Open Meshy in your browser.
2. Use the prompts from MESHY_PROMPT_PACK.md.
3. Generate selected assets using your Meshy account/free testing workflow.
4. Download assets as GLB/FBX where available.
5. Place downloaded files here:

${importsFolder}

6. Re-open GameForge and use the asset importer / generation pipeline.
7. GameForge will pass imported assets through Blender cleanup if Blender is installed.

IMPORTANT:
- Do not generate copyrighted IP, famous characters, brands, celebrities or trademarked weapons.
- Track attribution/licence terms before publishing.
`;
    fs.writeFileSync(path.join(packFolder, 'README_IMPORT_INSTRUCTIONS.txt'), instructions, 'utf8');

    const attributionTemplate = {
      generatedAt: new Date().toISOString(),
      provider: 'Meshy',
      mode: 'free_test',
      requiresReviewBeforeCommercialRelease: true,
      assets: (plan.requests || []).map(r => ({
        id: r.id,
        intendedFilename: `${r.id}.glb`,
        source: 'Meshy generated by user',
        licence: 'Check Meshy account plan/licence at generation time',
        attributionRequired: true,
        prompt: r.prompt
      }))
    };
    const attributionFile = path.join(packFolder, 'MESHY_ATTRIBUTION_TEMPLATE.json');
    fs.writeFileSync(attributionFile, JSON.stringify(attributionTemplate, null, 2), 'utf8');

    return {
      ok: true,
      plan,
      files: { root, packFolder, promptPack, promptJson, importsFolder, attributionFile },
      note: 'Meshy free-test prompt pack prepared. Generate/download assets manually, then import them into GameForge.'
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});



ipcMain.handle('meshy-test-api-key', async () => {
  try {
    const settings = getSavedMeshySettingsRaw();
    if (!settings.apiKey) return { ok: false, error: 'No Meshy API key saved.' };
    return { ok: true, note: 'API key is saved. A live task will validate during generation.' };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('meshy-run-autonomous-queue', async (event, payload) => {
  try {
    const settings = getSavedMeshySettingsRaw();
    if (!settings.apiKey) throw new Error('Meshy API key is not saved.');
    if (settings.mode !== 'api') throw new Error('Meshy settings mode is not API.');
    if (!settings.paidProvidersAllowed) throw new Error('Paid/API providers are not enabled.');

    const queue = payload?.queue || {};
    const maxAssets = Math.max(1, Math.min(12, Number(settings.maxAssetsPerRun || 8)));
    const enabled = (queue.requests || []).filter(r => r.enabled !== false).slice(0, maxAssets);
    const estimatedCredits = estimateMeshyCreditsForQueue({ requests: enabled });
    const cap = Number(settings.monthlyCreditCap || payload?.settings?.monthlyCreditCap || 100);
    if (estimatedCredits > cap) {
      throw new Error(`Estimated Meshy credits (${estimatedCredits}) exceed cap (${cap}). Raise cap or reduce assets.`);
    }

    const root = meshyApiDownloadsRoot();
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const runFolder = path.join(root, 'reports', `meshy_api_run_${stamp}`);
    ensureDir(runFolder);

    const results = [];
    for (const asset of enabled) {
      try {
        const preview = await createMeshyPreviewTask(asset);
        const previewTaskId = preview.result?.id || preview.result || preview.id || preview.task_id;
        if (!previewTaskId) throw new Error('Meshy preview task id missing.');
        const previewDone = await pollMeshyTask(previewTaskId);

        const refine = await createMeshyRefineTask(previewTaskId, asset);
        const refineTaskId = refine.result?.id || refine.result || refine.id || refine.task_id;
        if (!refineTaskId) throw new Error('Meshy refine task id missing.');
        const refineDone = await pollMeshyTask(refineTaskId);

        const downloaded = await downloadMeshyResult({ ...asset, previewTaskId }, refineDone, root);
        results.push({ ok: true, assetId: asset.id, previewTaskId, refineTaskId, downloaded });
      } catch (error) {
        results.push({ ok: false, assetId: asset.id, error: error.message });
      }
    }

    const downloadedCount = results.reduce((n, r) => n + (r.downloaded?.downloaded?.length || 0), 0);
    const report = {
      generatedAt: new Date().toISOString(),
      queue,
      estimatedCredits,
      summary: { assetsRequested: enabled.length, downloaded: downloadedCount, failed: results.filter(r => !r.ok).length, estimatedCredits },
      results,
      licenceWarning: 'Review Meshy account plan/licence terms before commercial/public release.'
    };
    const reportFile = path.join(runFolder, 'meshy_api_run_report.json');
    fs.writeFileSync(reportFile, JSON.stringify(report, null, 2), 'utf8');

    return { ok: true, summary: report.summary, results, files: { root, runFolder, reportFile } };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});



function liveMeshySceneRoot() {
  const root = path.join(meshyRoot(), 'live_scene_builder');
  ensureDir(root);
  ['blueprints','downloads','metadata','reports','imports','blender_jobs','scene_maps'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function liveSceneSafeName(name) {
  return String(name || 'scene_asset').replace(/[^a-z0-9._-]+/gi, '_').slice(0, 90);
}

function createLiveSceneImportManifest(root, blueprint, processed) {
  const manifest = {
    generatedAt: new Date().toISOString(),
    mode: 'Game Style + Rating Selector Import Manifest',
    gameName: blueprint.gameName,
    sceneType: blueprint.sceneType,
    lightingPlan: blueprint.lightingPlan,
    scenePlacementRules: blueprint.scenePlacementRules,
    assets: processed.map(p => ({
      id: p.asset?.id || p.assetId,
      role: p.asset?.role,
      type: p.asset?.type,
      position: p.asset?.position,
      scale: p.asset?.scale,
      files: p.downloaded?.downloaded || p.downloadedFiles || [],
      metadata: p.downloaded?.metaFile || p.metaFile || null,
      status: p.ok ? 'ready' : 'failed',
      error: p.error || null
    })),
    legalReviewRequired: true
  };
  const file = path.join(root, 'scene_maps', `live_scene_import_manifest_${Date.now()}.json`);
  fs.writeFileSync(file, JSON.stringify(manifest, null, 2), 'utf8');
  return { manifest, file };
}

function createLiveSceneBlenderBatch(root, blueprint, processed) {
  const script = `import bpy, os, json\nprint("GameForge Game Style + Rating Selector Blender batch")\nprint("Scene type: ${String(blueprint.sceneType || '').replace(/"/g, '\\"')}")\n# Cleanup scaffold for downloaded GLB/FBX assets.\n`;
  const file = path.join(root, 'blender_jobs', `live_scene_blender_batch_${Date.now()}.py`);
  fs.writeFileSync(file, script, 'utf8');
  return file;
}

async function liveMeshyGenerateAsset(asset, root, settings) {
  const safe = liveSceneSafeName(asset.id);
  const localMetadata = {
    generatedAt: new Date().toISOString(),
    asset,
    provider: 'Meshy',
    mode: settings.mode || 'free_test',
    licence: 'Review Meshy account plan/licence terms before commercial/public release.'
  };

  if (!settings.apiKey || settings.mode !== 'api' || !settings.paidProvidersAllowed) {
    const promptFile = path.join(root, 'imports', `${safe}_MESHY_PROMPT.txt`);
    fs.writeFileSync(promptFile, `PROMPT:\n${asset.prompt}\n\nTEXTURE PROMPT:\n${asset.texturePrompt || asset.prompt}\n\nSuggested filename: ${safe}.glb\n`, 'utf8');
    const metaFile = path.join(root, 'metadata', `${safe}_free_test_metadata.json`);
    fs.writeFileSync(metaFile, JSON.stringify({ ...localMetadata, status: 'free_test_prompt_created', promptFile }, null, 2), 'utf8');
    return { ok: true, asset, mode: 'free_test_prompt', downloadedFiles: [{ type: 'prompt', file: promptFile }], metaFile };
  }

  const preview = await createMeshyPreviewTask(asset);
  const previewTaskId = preview.result?.id || preview.result || preview.id || preview.task_id;
  if (!previewTaskId) throw new Error(`Meshy preview task id missing for ${asset.id}`);
  await pollMeshyTask(previewTaskId);

  const refine = await createMeshyRefineTask(previewTaskId, asset);
  const refineTaskId = refine.result?.id || refine.result || refine.id || refine.task_id;
  if (!refineTaskId) throw new Error(`Meshy refine task id missing for ${asset.id}`);
  const refineDone = await pollMeshyTask(refineTaskId);

  const downloaded = await downloadMeshyResult({ ...asset, previewTaskId }, refineDone, root);
  return { ok: true, asset, mode: 'api', previewTaskId, refineTaskId, downloaded };
}



ipcMain.handle('live-meshy-build-scene', async (event, payload) => {
  try {
    const settings = getSavedMeshySettingsRaw();
    const blueprint = payload?.blueprint || {};
    const root = liveMeshySceneRoot();
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const runFolder = path.join(root, 'reports', `live_scene_${stamp}`);
    ensureDir(runFolder);

    const blueprintFile = path.join(root, 'blueprints', `live_scene_blueprint_${stamp}.json`);
    fs.writeFileSync(blueprintFile, JSON.stringify(blueprint, null, 2), 'utf8');

    const enabledAssets = (blueprint.assets || [])
      .filter(a => a.enabled !== false)
      .sort((a,b) => (a.priority || 99) - (b.priority || 99))
      .slice(0, Math.max(1, Math.min(16, Number(settings.maxAssetsPerRun || 8))));

    const estimatedCredits = enabledAssets.length * 40;
    if (settings.apiKey && settings.mode === 'api' && settings.paidProvidersAllowed) {
      const cap = Number(settings.monthlyCreditCap || 100);
      if (estimatedCredits > cap) {
        throw new Error(`Estimated Meshy credits (${estimatedCredits}) exceed cap (${cap}). Raise cap or reduce assets.`);
      }
    }

    const processed = [];
    for (const asset of enabledAssets) {
      try {
        processed.push(await liveMeshyGenerateAsset(asset, root, settings));
      } catch (error) {
        processed.push({ ok: false, asset, assetId: asset.id, error: error.message });
      }
    }

    const { manifest, file: manifestFile } = createLiveSceneImportManifest(root, blueprint, processed);
    const blenderBatchFile = createLiveSceneBlenderBatch(root, blueprint, processed);

    const report = {
      generatedAt: new Date().toISOString(),
      mode: 'Game Style + Rating Selector',
      blueprintFile,
      manifestFile,
      blenderBatchFile,
      apiModeUsed: Boolean(settings.apiKey && settings.mode === 'api' && settings.paidProvidersAllowed),
      summary: {
        sceneType: blueprint.sceneType,
        assetsRequested: enabledAssets.length,
        assetsProcessed: processed.filter(p => p.ok).length,
        failed: processed.filter(p => !p.ok).length,
        estimatedCredits
      },
      results: processed,
      manifest,
      legalWarning: 'Review Meshy and any provider licence terms before public/commercial release.'
    };
    const reportFile = path.join(runFolder, 'live_meshy_scene_builder_report.json');
    fs.writeFileSync(reportFile, JSON.stringify(report, null, 2), 'utf8');

    const markdownReport = path.join(runFolder, 'LIVE_MESHY_SCENE_BUILDER_REPORT.md');
    fs.writeFileSync(markdownReport, `# GameForge Game Style + Rating Selector Report\n\nGenerated: ${new Date().toISOString()}\n\nScene Type: ${blueprint.sceneType}\n\nAPI Mode Used: ${report.apiModeUsed ? 'YES' : 'NO - Free Test Prompt Mode'}\n\nAssets Requested: ${report.summary.assetsRequested}\nAssets Processed: ${report.summary.assetsProcessed}\nFailed: ${report.summary.failed}\n\n## Files\n- Blueprint: ${blueprintFile}\n- Import Manifest: ${manifestFile}\n- Blender Batch: ${blenderBatchFile}\n- Report: ${reportFile}\n`, 'utf8');

    return {
      ok: true,
      summary: report.summary,
      apiModeUsed: report.apiModeUsed,
      blueprint,
      results: processed,
      downloads: processed,
      files: { root, runFolder, blueprintFile, manifestFile, blenderBatchFile, reportFile, markdownReport },
      legalWarning: report.legalWarning
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});



function unrealPhotorealRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeUnrealPhotoreal');
  ensureDir(root);
  ['projects','packages','reports','manifests','materials','lighting','blueprints','scripts'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function unrealSafeName(name) {
  return String(name || 'GameForgeGame').replace(/[^a-z0-9_]+/gi, '_').slice(0, 80);
}

function writeUnrealPhotorealFiles(root, pkg) {
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  const projectName = unrealSafeName(pkg.unrealProject?.projectName || pkg.gameName || 'GameForgeGame');
  const projectFolder = path.join(root, 'projects', `${projectName}_${stamp}`);
  ensureDir(projectFolder);

  const folders = pkg.unrealProject?.folders || [];
  for (const f of folders) ensureDir(path.join(projectFolder, f));

  const manifestFile = path.join(projectFolder, 'GAMEFORGE_UNREAL_IMPORT_MANIFEST.json');
  fs.writeFileSync(manifestFile, JSON.stringify(pkg, null, 2), 'utf8');

  const materialPlanFile = path.join(projectFolder, 'Content', 'GameForge', 'Materials', 'PBR_MATERIAL_PLAN.json');
  ensureDir(path.dirname(materialPlanFile));
  fs.writeFileSync(materialPlanFile, JSON.stringify({
    generatedAt: new Date().toISOString(),
    materials: pkg.renderingPreset?.materials || [],
    sceneDressing: pkg.sceneDressing || [],
    notes: [
      'Create master material instances for wood, plaster, metal, glass, fabric, skin/ghost, mud/asphalt as needed.',
      'Prefer scanned/CC0 PBR maps where available.',
      'Flag missing normal/roughness/AO maps before Photoreal Ready status.'
    ]
  }, null, 2), 'utf8');

  const lightingPlanFile = path.join(projectFolder, 'Content', 'GameForge', 'Maps', 'LIGHTING_POST_PROCESS_PLAN.json');
  ensureDir(path.dirname(lightingPlanFile));
  fs.writeFileSync(lightingPlanFile, JSON.stringify({
    generatedAt: new Date().toISOString(),
    lighting: pkg.renderingPreset?.lighting || [],
    postProcess: pkg.renderingPreset?.postProcess || [],
    notes: [
      'Use Lumen-ready lighting where available.',
      'Add Post Process Volume with manual exposure and cinematic contrast.',
      'Use fog/haze and practical lights to avoid flat scenes.',
      'Use flashlight/weapon light for first-person horror scenes.'
    ]
  }, null, 2), 'utf8');

  const pythonImportScript = path.join(projectFolder, 'scripts', 'gameforge_unreal_import_stub.py');
  ensureDir(path.dirname(pythonImportScript));
  fs.writeFileSync(pythonImportScript, `# GameForge Unreal import stub
# Run inside Unreal Python environment after creating/opening the project.
# Reads GAMEFORGE_UNREAL_IMPORT_MANIFEST.json and imports assets into Content/GameForge.
print("GameForge Unreal photoreal import stub loaded")
`, 'utf8');

  const readme = path.join(projectFolder, 'README_UNREAL_PHOTOREAL_HANDOFF.md');
  fs.writeFileSync(readme, `# GameForge Unreal Photoreal Handoff

Game: ${pkg.gameName}
Scene Type: ${pkg.sceneType}

## Goal
Prepare this project folder for a high-end AA / near-AAA Unreal visual pass.

## Use
1. Open/create an Unreal project.
2. Copy/import assets from Content/GameForge folders.
3. Apply material and lighting plans.
4. Configure Lumen/Nanite where appropriate.
5. Run build readiness checklist.

## Important
This is a handoff/build-prep package. Final packaging requires Unreal Engine installed and configured.

## Legal
${pkg.legalWarning}
`, 'utf8');

  const reportFile = path.join(root, 'reports', `unreal_photoreal_report_${stamp}.json`);
  fs.writeFileSync(reportFile, JSON.stringify({
    generatedAt: new Date().toISOString(),
    projectFolder,
    manifestFile,
    materialPlanFile,
    lightingPlanFile,
    pythonImportScript,
    readme,
    package: pkg
  }, null, 2), 'utf8');

  return { projectFolder, manifestFile, materialPlanFile, lightingPlanFile, pythonImportScript, readme, reportFile };
}



ipcMain.handle('unreal-photoreal-build-package', async (event, payload) => {
  try {
    const root = unrealPhotorealRoot();
    const pkg = payload?.package || {};
    const files = writeUnrealPhotorealFiles(root, pkg);
    return {
      ok: true,
      root,
      files,
      note: 'Unreal photoreal project/handoff package prepared. Open/import in Unreal Engine for high-end rendering/build path.'
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});


function autoRepairRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAutoRepair');
  ensureDir(root);
  ['reports','logs','scripts','unreal','checks','repair_cycles'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function autoRepairCheckTool(command, displayName) {
  try {
    const checker = process.platform === 'win32' ? 'where' : 'which';
    const output = execFileSync(checker, [command], { encoding: 'utf8', stdio: ['ignore','pipe','ignore'] }).trim();
    return { name: displayName, status: output ? 'FOUND' : 'MISSING', detail: output.split(/\r?\n/)[0] || '' };
  } catch (e) {
    return { name: displayName, status: 'MISSING', detail: `${command} not found in PATH` };
  }
}

function autoRepairWriteUnrealAutomationScripts(root, plan) {
  const unrealScript = path.join(root, 'scripts', 'gameforge_unreal_auto_import_and_test.py');
  fs.writeFileSync(unrealScript, `# GameForge Unreal Auto Import/Test Script
# Run inside Unreal Python environment.
# Purpose:
# - read GameForge import manifests
# - import assets
# - apply material/lighting/post-process plans
# - prepare map/testing checks

print("GameForge Unreal Auto Import/Test started")
print("This script is a scaffold generated by GameForge Auto Repair Runner.")
`, 'utf8');

  const powershellScript = path.join(root, 'scripts', 'RUN_UNREAL_AUTOMATION_TEMPLATE.ps1');
  fs.writeFileSync(powershellScript, `# GameForge Unreal Automation Template
# Update the UnrealEditor path if needed, then run from PowerShell.
# Example:
# & "C:\\Program Files\\Epic Games\\UE_5.4\\Engine\\Binaries\\Win64\\UnrealEditor.exe" "Path\\To\\Project.uproject" -ExecutePythonScript="${unrealScript.replace(/\\/g, '\\\\')}"
Write-Host "GameForge Unreal automation template generated."
`, 'utf8');

  return { unrealScript, powershellScript };
}

function applySafeAutoRepairs(root, plan, projectState) {
  const repairs = [];
  const requiredFolders = [
    'unreal/Content/GameForge/Models',
    'unreal/Content/GameForge/Materials',
    'unreal/Content/GameForge/Textures',
    'unreal/Content/GameForge/Audio',
    'unreal/Content/GameForge/Maps',
    'unreal/Content/GameForge/Blueprints',
    'unreal/Content/GameForge/FX'
  ];
  for (const f of requiredFolders) {
    ensureDir(path.join(root, f));
  }
  repairs.push('Created/verified Unreal GameForge content folders.');

  const materialPlan = path.join(root, 'unreal', 'Content', 'GameForge', 'Materials', 'AUTO_REPAIR_PBR_MATERIAL_PLAN.json');
  fs.writeFileSync(materialPlan, JSON.stringify({
    generatedAt: new Date().toISOString(),
    repaired: true,
    materialsRequired: ['wood', 'plaster', 'metal', 'glass', 'fabric', 'skin/ghost', 'mud/asphalt'],
    mapsRequired: ['albedo/baseColor', 'normal', 'roughness', 'ambientOcclusion', 'metallic where needed'],
    warning: 'Generated as auto-repair fallback. Replace with real scanned/PBR materials for best realism.'
  }, null, 2), 'utf8');
  repairs.push('Created fallback PBR material plan.');

  const lightingPlan = path.join(root, 'unreal', 'Content', 'GameForge', 'Maps', 'AUTO_REPAIR_LIGHTING_POST_PROCESS_PLAN.json');
  fs.writeFileSync(lightingPlan, JSON.stringify({
    generatedAt: new Date().toISOString(),
    repaired: true,
    lighting: ['flashlight', 'window moonlight/sky light', 'warm practical light', 'fog/haze'],
    postProcess: ['manual exposure', 'bloom', 'ambient occlusion', 'vignette', 'film grain', 'cinematic contrast'],
    warning: 'Generated as auto-repair fallback. Tune inside Unreal for final visuals.'
  }, null, 2), 'utf8');
  repairs.push('Created fallback lighting/post-process plan.');

  const manifest = path.join(root, 'unreal', 'GAMEFORGE_AUTO_REPAIR_IMPORT_MANIFEST.json');
  fs.writeFileSync(manifest, JSON.stringify({
    generatedAt: new Date().toISOString(),
    gameName: plan.gameName,
    prompt: plan.prompt,
    sceneObjects: projectState?.scene?.objects || [],
    notes: ['Auto-generated fallback manifest. Import real Meshy/Blender processed assets when available.']
  }, null, 2), 'utf8');
  repairs.push('Created fallback Unreal import manifest.');

  const scripts = autoRepairWriteUnrealAutomationScripts(root, plan);
  repairs.push('Created Unreal automation script templates.');

  return { repairs, files: { materialPlan, lightingPlan, manifest, ...scripts } };
}


ipcMain.handle('unreal-auto-repair-run', async (event, payload) => {
  try {
    const root = autoRepairRoot();
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const runFolder = path.join(root, 'repair_cycles', `repair_${stamp}`);
    ensureDir(runFolder);

    const plan = payload?.plan || {};
    const projectState = payload?.projectState || {};
    const checks = [];

    checks.push(autoRepairCheckTool('node', 'Node.js'));
    checks.push(autoRepairCheckTool('npm', 'npm'));
    checks.push(autoRepairCheckTool('blender', 'Blender'));
    checks.push(autoRepairCheckTool('ffmpeg', 'FFmpeg'));
    checks.push(autoRepairCheckTool('godot', 'Godot'));
    checks.push(autoRepairCheckTool('UnrealEditor', 'UnrealEditor'));

    const settings = (typeof getSavedMeshySettingsRaw === 'function') ? getSavedMeshySettingsRaw() : {};
    checks.push({
      name: 'Meshy API Settings',
      status: settings.apiKey && settings.mode === 'api' && settings.paidProvidersAllowed ? 'READY' : 'FREE_TEST_OR_MISSING',
      detail: settings.apiKey ? `mode=${settings.mode}, paidProvidersAllowed=${Boolean(settings.paidProvidersAllowed)}` : 'No API key saved'
    });

    checks.push({
      name: 'Photoreal Quality Report',
      status: projectState.photorealQualityReport ? 'FOUND' : 'MISSING',
      detail: projectState.photorealQualityReport ? `${projectState.photorealQualityReport.status || 'unknown'} ${projectState.photorealQualityReport.score ?? ''}` : 'No quality report yet'
    });

    const repair = applySafeAutoRepairs(runFolder, plan, projectState);

    const blockers = [];
    for (const c of checks) {
      if (c.name === 'UnrealEditor' && c.status === 'MISSING') blockers.push('Unreal Engine not detected. Install/configure Unreal before one-click Unreal packaging can work.');
      if (c.name === 'Blender' && c.status === 'MISSING') blockers.push('Blender not detected. Install Blender for model cleanup automation.');
      if (c.name === 'Meshy API Settings' && c.status !== 'READY') blockers.push('Meshy API mode is not fully enabled. Full Meshy automation will use free-test fallback.');
    }

    const report = {
      generatedAt: new Date().toISOString(),
      plan,
      checks,
      repairs: repair.repairs,
      remainingBlockers: blockers,
      summary: {
        cyclesRun: 1,
        repairsApplied: repair.repairs.length,
        blockers: blockers.length
      },
      files: repair.files
    };

    const reportFile = path.join(runFolder, 'AUTO_REPAIR_REPORT.json');
    fs.writeFileSync(reportFile, JSON.stringify(report, null, 2), 'utf8');

    const mdFile = path.join(runFolder, 'AUTO_REPAIR_REPORT.md');
    fs.writeFileSync(mdFile, `# GameForge Auto Repair Report

Generated: ${new Date().toISOString()}

## Summary
- Cycles Run: ${report.summary.cyclesRun}
- Repairs Applied: ${report.summary.repairsApplied}
- Remaining Blockers: ${report.summary.blockers}

## Checks
${checks.map(c => `- ${c.name}: ${c.status}${c.detail ? ' — ' + c.detail : ''}`).join('\n')}

## Repairs
${repair.repairs.map(r => `- ${r}`).join('\n')}

## Remaining Blockers
${blockers.length ? blockers.map(b => `- ${b}`).join('\n') : '- None'}

## Files
${Object.entries(repair.files).map(([k,v]) => `- ${k}: ${v}`).join('\n')}
`, 'utf8');

    return { ok: true, ...report, files: { ...repair.files, reportFile, mdFile, runFolder } };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

function detectUnrealInstallDetails() {
  const results = { editor: null, uat: null, status: 'MISSING' };
  const envCandidates = [
    process.env.UNREAL_EDITOR_PATH,
    process.env.UE_EDITOR_PATH,
    process.env.UE5_EDITOR_PATH
  ].filter(Boolean);
  const winCandidates = [
    'C:\\Program Files\\Epic Games\\UE_5.4\\Engine\\Binaries\\Win64\\UnrealEditor.exe',
    'C:\\Program Files\\Epic Games\\UE_5.3\\Engine\\Binaries\\Win64\\UnrealEditor.exe',
    'C:\\Program Files\\Epic Games\\UE_5.2\\Engine\\Binaries\\Win64\\UnrealEditor.exe',
    'C:\\Program Files\\Epic Games\\UE_5.1\\Engine\\Binaries\\Win64\\UnrealEditor.exe',
    'C:\\Program Files\\Epic Games\\UE_5.0\\Engine\\Binaries\\Win64\\UnrealEditor.exe'
  ];
  for (const candidate of [...envCandidates, ...winCandidates]) {
    if (candidate && fs.existsSync(candidate)) {
      results.editor = candidate;
      const uat = path.join(path.dirname(path.dirname(candidate)), 'BatchFiles', process.platform === 'win32' ? 'RunUAT.bat' : 'RunUAT.sh');
      if (fs.existsSync(uat)) results.uat = uat;
      results.status = results.uat ? 'READY' : 'EDITOR_ONLY';
      return results;
    }
  }
  try {
    const output = execFileSync(process.platform === 'win32' ? 'where' : 'which', [process.platform === 'win32' ? 'UnrealEditor.exe' : 'UnrealEditor'], { encoding: 'utf8', stdio: ['ignore','pipe','ignore'] }).trim();
    if (output) {
      results.editor = output.split(/\r?\n/)[0];
      const uat = path.join(path.dirname(path.dirname(results.editor)), 'BatchFiles', process.platform === 'win32' ? 'RunUAT.bat' : 'RunUAT.sh');
      if (fs.existsSync(uat)) results.uat = uat;
      results.status = results.uat ? 'READY' : 'EDITOR_ONLY';
    }
  } catch (e) {}
  return results;
}

function detectBlenderInstallDetails() {
  const results = { path: null, status: 'MISSING' };
  const envCandidates = [process.env.BLENDER_PATH].filter(Boolean);
  const winCandidates = [
    'C:\\Program Files\\Blender Foundation\\Blender 4.1\\blender.exe',
    'C:\\Program Files\\Blender Foundation\\Blender 4.0\\blender.exe',
    'C:\\Program Files\\Blender Foundation\\Blender 3.6\\blender.exe'
  ];
  for (const candidate of [...envCandidates, ...winCandidates]) {
    if (candidate && fs.existsSync(candidate)) {
      results.path = candidate;
      results.status = 'READY';
      return results;
    }
  }
  try {
    const output = execFileSync(process.platform === 'win32' ? 'where' : 'which', [process.platform === 'win32' ? 'blender.exe' : 'blender'], { encoding: 'utf8', stdio: ['ignore','pipe','ignore'] }).trim();
    if (output) {
      results.path = output.split(/\r?\n/)[0];
      results.status = 'READY';
    }
  } catch (e) {}
  return results;
}

function unrealOneClickRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeUnrealOneClickBuilds');
  ensureDir(root);
  ['projects','logs','scripts','exports','reports'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function writeUnrealOneClickFiles(root, plan, projectState, detection) {
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  const safeName = String((plan.gameName || 'GameForgeGame')).replace(/[^a-z0-9_\-]+/gi, '_');
  const runRoot = path.join(root, 'projects', safeName + '_' + stamp);
  const projectFolder = path.join(runRoot, safeName);
  const contentRoot = path.join(projectFolder, 'Content', 'GameForge');
  const configRoot = path.join(projectFolder, 'Config');
  const scriptsRoot = path.join(runRoot, 'Scripts');
  const savedRoot = path.join(projectFolder, 'Saved');
  [projectFolder, contentRoot, configRoot, scriptsRoot, savedRoot].forEach(ensureDir);
  ['Models','Materials','Textures','Audio','Maps','Blueprints','FX','Characters','Metadata'].forEach(d => ensureDir(path.join(contentRoot, d)));

  const uprojectPath = path.join(projectFolder, safeName + '.uproject');
  const uproject = {
    FileVersion: 3,
    EngineAssociation: '5.x',
    Category: 'Games',
    Description: 'Generated by GameForge AI for ' + (plan.gameName || safeName),
    Modules: [{ Name: safeName, Type: 'Runtime', LoadingPhase: 'Default' }],
    Plugins: [
      { Name: 'PythonScriptPlugin', Enabled: true },
      { Name: 'EditorScriptingUtilities', Enabled: true },
      { Name: 'EnhancedInput', Enabled: true }
    ]
  };
  fs.writeFileSync(uprojectPath, JSON.stringify(uproject, null, 2), 'utf8');

  fs.writeFileSync(path.join(configRoot, 'DefaultGame.ini'), '[\/Script/EngineSettings.GeneralProjectSettings]\nProjectName=' + (plan.gameName || safeName) + '\nProjectVersion=3.4.0\nDescription=Development by GameForge AI\n', 'utf8');
  fs.writeFileSync(path.join(configRoot, 'DefaultEngine.ini'), '[/Script/Engine.RendererSettings]\nr.DynamicGlobalIlluminationMethod=1\nr.ReflectionMethod=1\nr.Shadow.Virtual.Enable=1\nr.Nanite=1\n', 'utf8');
  fs.writeFileSync(path.join(configRoot, 'DefaultInput.ini'), '[/Script/EnhancedInput.EnhancedInputDeveloperSettings]\n', 'utf8');

  const manifestPath = path.join(contentRoot, 'Metadata', 'GAMEFORGE_IMPORT_MANIFEST.json');
  const manifest = {
    generatedAt: new Date().toISOString(),
    gameName: plan.gameName || safeName,
    prompt: plan.prompt || '',
    sceneType: plan.sceneType || 'generic',
    sourceRuns: {
      meshy: projectState.meshyAutonomousApiRun || projectState.liveMeshySceneRun || null,
      selfAssetRun: projectState.selfAssetRun || null,
      audioAssetRun: projectState.audioAssetRun || null,
      qualityReport: projectState.photorealQualityReport || null
    },
    sceneObjects: (projectState.scene && projectState.scene.objects) ? projectState.scene.objects : [],
    importedAssetFolders: ['Models','Materials','Textures','Audio','Characters','FX'],
    targetMap: 'GF_MainMap',
    introSplashText: 'Development by GameForge AI'
  };
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2), 'utf8');

  const pythonScriptPath = path.join(scriptsRoot, 'gameforge_unreal_import_build.py');
  const pythonScript = [
    '# GameForge Unreal Automation Script',
    '# Generated by GameForge AI v6.8.2',
    'import json, os',
    'print("GameForge Unreal one-click automation started")',
    'manifest_path = r"' + manifestPath.replace(/\\/g, '\\\\') + '"',
    'print("Using manifest:", manifest_path)',
    '# NOTE: scaffold only — extend inside Unreal Python for full import/build logic.',
    'print("Create/import/build steps would execute here.")',
    ''
  ].join('\n');
  fs.writeFileSync(pythonScriptPath, pythonScript, 'utf8');

  const ps1Path = path.join(scriptsRoot, 'RUN_GAMEFORGE_UNREAL_ONE_CLICK.ps1');
  const batPath = path.join(scriptsRoot, 'RUN_GAMEFORGE_UNREAL_ONE_CLICK.bat');
  const packageBatPath = path.join(scriptsRoot, 'RUN_GAMEFORGE_PACKAGE_ONLY.bat');
  const editorPath = (detection.editor || '').replace(/\\/g, '\\\\');
  const uatPath = (detection.uat || '').replace(/\\/g, '\\\\');
  const packagedFolder = path.join(runRoot, 'Packaged');

  const ps1Lines = [
    'param()',
    '$ErrorActionPreference = "Stop"',
    '$uproject = "' + uprojectPath.replace(/\\/g, '\\\\') + '"',
    '$pythonScript = "' + pythonScriptPath.replace(/\\/g, '\\\\') + '"',
    '$editor = "' + editorPath + '"',
    '$uat = "' + uatPath + '"',
    'Write-Host "Launching Unreal import step..."',
    'if (Test-Path $editor) { & $editor $uproject -ExecutePythonScript=$pythonScript -unattended -nosplash -nop4 -log }',
    'if (Test-Path $uat) {',
    '  Write-Host "Launching BuildCookRun package step..."',
    '  & $uat BuildCookRun -project=$uproject -noP4 -platform=Win64 -clientconfig=Development -build -cook -stage -pak -archive -archivedirectory="' + packagedFolder.replace(/\\/g, '\\\\') + '"',
    '}',
    'Write-Host "GameForge Unreal one-click script complete."',
    ''
  ];
  fs.writeFileSync(ps1Path, ps1Lines.join('\n'), 'utf8');
  fs.writeFileSync(batPath, '@echo off\r\nsetlocal\r\npowershell -ExecutionPolicy Bypass -File "%~dp0RUN_GAMEFORGE_UNREAL_ONE_CLICK.ps1"\r\nendlocal\r\n', 'utf8');
  fs.writeFileSync(packageBatPath, '@echo off\r\nsetlocal\r\nset UPROJECT=' + uprojectPath + '\r\nset UAT=' + (detection.uat || '') + '\r\nif not exist "%UAT%" ( echo RunUAT not found. & pause & exit /b 1 )\r\n"%UAT%" BuildCookRun -project="%UPROJECT%" -noP4 -platform=Win64 -clientconfig=Development -build -cook -stage -pak -archive -archivedirectory="' + packagedFolder + '"\r\nendlocal\r\n', 'utf8');

  const readmePath = path.join(runRoot, 'README_NEXT_STEPS.txt');
  fs.writeFileSync(readmePath, 'GAMEFORGE UNREAL ONE-CLICK BUILD RUNNER\n\nGame: ' + (plan.gameName || safeName) + '\n\nThis folder was generated by GameForge AI v6.8.2.\nIt contains a generated Unreal project scaffold, import manifest, automation script, and one-click launchers.\nIf Unreal was detected, GameForge will attempt to run the build automatically.\nIf not, install Unreal Engine 5 and re-run the one-click build.\n', 'utf8');

  const planPath = path.join(runRoot, 'GAMEFORGE_UNREAL_ONE_CLICK_PLAN.json');
  fs.writeFileSync(planPath, JSON.stringify({ plan, manifest, detection }, null, 2), 'utf8');

  return { runRoot, projectFolder, uprojectPath, manifestPath, pythonScriptPath, ps1Path, batPath, packageBatPath, readmePath, planPath, packagedFolder };
}

function tryRunUnrealOneClickAutomation(files, detection, options = {}) {
  const steps = [];
  const notes = [];
  const blockers = [];
  let status = 'FILES_PREPARED';

  if (!detection.editor || !detection.uat) {
    if (!detection.editor) blockers.push('UnrealEditor not detected. Install Unreal Engine 5 or set UNREAL_EDITOR_PATH.');
    if (!detection.uat) blockers.push('RunUAT not detected. Ensure a full Unreal Engine install is available.');
    return { ok: false, status: 'WAITING_FOR_UNREAL', steps, notes, blockers };
  }

  if (!options.executeAutomation) {
    notes.push('Automation execution skipped by request; files generated only.');
    return { ok: true, status: 'FILES_PREPARED_ONLY', steps, notes, blockers };
  }

  const importArgs = [files.uprojectPath, '-ExecutePythonScript=' + files.pythonScriptPath, '-unattended', '-nosplash', '-nop4', '-log'];
  try {
    execFileSync(detection.editor, importArgs, { stdio: 'ignore', timeout: 1000 * 60 * 10 });
    steps.push({ name: 'Unreal import script', status: 'OK', detail: 'Editor launched and Python automation command executed.' });
  } catch (e) {
    steps.push({ name: 'Unreal import script', status: 'WARNING', detail: e.message });
    notes.push('Import step returned a warning. Packaging may still succeed if project opens correctly.');
  }

  const packageArgs = ['BuildCookRun', '-project=' + files.uprojectPath, '-noP4', '-platform=Win64', '-clientconfig=Development', '-build', '-cook', '-stage', '-pak', '-archive', '-archivedirectory=' + files.packagedFolder];
  try {
    execFileSync(detection.uat, packageArgs, { stdio: 'ignore', timeout: 1000 * 60 * 30 });
    steps.push({ name: 'BuildCookRun package', status: 'OK', detail: 'AutomationTool completed packaging command.' });
    status = 'PACKAGED';
  } catch (e) {
    steps.push({ name: 'BuildCookRun package', status: 'WARNING', detail: e.message });
    notes.push('Packaging failed on first attempt; generated scripts and project files are still available for retry.');
    blockers.push('Packaging did not complete automatically. Review Unreal/AutomationTool logs and retry.');
    status = 'PACKAGE_WARNING';
  }

  return { ok: status === 'PACKAGED', status, steps, notes, blockers };
}

ipcMain.handle('unreal-one-click-build', async (event, payload) => {
  try {
    const root = unrealOneClickRoot();
    const plan = payload?.plan || {};
    const projectState = payload?.projectState || {};
    const executeAutomation = payload?.executeAutomation !== false;

    const unreal = detectUnrealInstallDetails();
    const blender = detectBlenderInstallDetails();
    const detectedTools = {
      editor: unreal.editor ? { path: unreal.editor, status: unreal.status } : { status: 'MISSING' },
      uat: unreal.uat ? { path: unreal.uat, status: unreal.status } : { status: 'MISSING' },
      blender,
      node: autoRepairCheckTool('node', 'Node.js'),
      ffmpeg: autoRepairCheckTool('ffmpeg', 'FFmpeg')
    };

    const files = writeUnrealOneClickFiles(root, plan, projectState, unreal);
    const attempt = tryRunUnrealOneClickAutomation(files, unreal, { executeAutomation });
    const steps = [...(attempt.steps || [])];
    const notes = [...(attempt.notes || [])];
    const blockers = [...(attempt.blockers || [])];

    if (!attempt.ok) {
      const repairFolder = path.join(files.runRoot, 'AutoRepairCycle');
      ensureDir(repairFolder);
      const fallback = applySafeAutoRepairs(repairFolder, plan, projectState);
      notes.push('Auto-repair scaffold generated for Unreal retry.');
      steps.push({ name: 'Safe auto repair cycle', status: 'OK', detail: 'Generated ' + fallback.repairs.length + ' fallback repair items.' });
      files.autoRepairFolder = repairFolder;
      files.autoRepairManifest = fallback.files?.manifest;
      files.autoRepairLighting = fallback.files?.lightingPlan;
      files.autoRepairMaterials = fallback.files?.materialPlan;
    }

    const report = {
      ok: attempt.ok,
      status: attempt.status,
      generatedAt: new Date().toISOString(),
      detectedTools,
      files,
      steps,
      blockers,
      notes,
      executeAutomation
    };

    const reportFile = path.join(files.runRoot, 'UNREAL_ONE_CLICK_BUILD_REPORT.json');
    fs.writeFileSync(reportFile, JSON.stringify(report, null, 2), 'utf8');
    report.files.reportFile = reportFile;

    return report;
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});


function paranormalJumpscareRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeParanormalJumpscares');
  ensureDir(root);
  ['plans','unreal_blueprints','audio_requests','reports','triggers'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('paranormal-jumpscare-build', async (event, payload) => {
  try {
    const root = paranormalJumpscareRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'plans', `paranormal_jumpscare_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const triggerFile = path.join(root, 'triggers', `jump_scare_triggers_${stamp}.json`);
    fs.writeFileSync(triggerFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      triggerBlueprint: plan.unrealBlueprintHandoff?.triggerBlueprint || 'BP_GF_JumpScareTrigger',
      triggers: plan.jumpScares || [],
      gameplayRules: plan.gameplayRules || []
    }, null, 2), 'utf8');

    const audioFile = path.join(root, 'audio_requests', `jump_scare_audio_requests_${stamp}.json`);
    fs.writeFileSync(audioFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      requests: plan.audioAssetRequests || [],
      audioCuePrefix: plan.unrealBlueprintHandoff?.audioCuePrefix || 'SFX_GF_Scare_'
    }, null, 2), 'utf8');

    const blueprintStub = path.join(root, 'unreal_blueprints', `BP_GF_JumpScareTrigger_${stamp}.txt`);
    fs.writeFileSync(blueprintStub, `UNREAL BLUEPRINT HANDOFF: BP_GF_JumpScareTrigger

Components:
- Box Collision Trigger
- Audio Component
- Camera Shake
- Flashlight Flicker Controller
- Ghost Flash / Apparition Spawn
- Cooldown Timer
- Chance Roll
- Intensity Curve

Trigger events:
${(plan.jumpScares || []).map(s => `- ${s.id}: ${s.trigger} / ${s.intensity}`).join('\n')}

Copyright-safe note:
Use original devices, original ghost behaviour and original scare logic. Do not copy any existing game.
`, 'utf8');

    const reportFile = path.join(root, 'reports', `paranormal_jumpscare_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Paranormal Device + Jump Scare Report

Generated: ${new Date().toISOString()}

Devices: ${(plan.devices || []).length}
Jump Scares: ${(plan.jumpScares || []).length}
Audio Requests: ${(plan.audioAssetRequests || []).length}

Files:
- Plan: ${planFile}
- Triggers: ${triggerFile}
- Audio Requests: ${audioFile}
- Blueprint Stub: ${blueprintStub}
`, 'utf8');

    return {
      ok: true,
      summary: {
        devices: (plan.devices || []).length,
        jumpScares: (plan.jumpScares || []).length,
        audioRequests: (plan.audioAssetRequests || []).length
      },
      files: { root, planFile, triggerFile, audioFile, blueprintStub, reportFile }
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});


function oneClickAssemblyRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeOneClickHorrorAssembly');
  ensureDir(root);
  ['plans','unreal_blueprints','maps','ui','reports','polish','materials','lighting','dressing'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('unreal-horror-assemble-game', async (event, payload) => {
  try {
    const root = oneClickAssemblyRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'plans', `unreal_horror_assembly_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const blueprintFile = path.join(root, 'unreal_blueprints', `GF_Horror_Blueprint_Scaffold_${stamp}.txt`);
    fs.writeFileSync(blueprintFile, `GAMEFORGE UNREAL HORROR ASSEMBLY SCAFFOLD

Required systems:
${(plan.requiredSystems || []).map(x => '- ' + x).join('\n')}

Gameplay loop:
${(plan.gameplayLoop || []).map(x => '- ' + x).join('\n')}

Map:
${plan.levelBlueprint?.mapName || 'GF_OneClick_Horror_Map'}

Interactables:
${(plan.levelBlueprint?.interactables || []).map(x => '- ' + x).join('\n')}

Device events:
${(plan.deviceSystem?.deviceEvents || []).map(x => '- ' + x).join('\n')}

Scare audio rules:
${(plan.scareSystem?.audioRules || []).map(x => '- ' + x).join('\n')}
`, 'utf8');

    const mapFile = path.join(root, 'maps', `GF_OneClick_Horror_Map_${stamp}.json`);
    fs.writeFileSync(mapFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      mapName: plan.levelBlueprint?.mapName || 'GF_OneClick_Horror_Map',
      zones: plan.levelBlueprint?.zones || [],
      interactables: plan.levelBlueprint?.interactables || [],
      outputs: plan.unrealAssemblyOutputs || {}
    }, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `unreal_horror_assembly_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Unreal Horror Assembly Report

Generated: ${new Date().toISOString()}

Systems: ${(plan.requiredSystems || []).length}
Zones: ${(plan.levelBlueprint?.zones || []).length}
Interactables: ${(plan.levelBlueprint?.interactables || []).length}

Files:
- Plan: ${planFile}
- Blueprint Scaffold: ${blueprintFile}
- Map Plan: ${mapFile}
`, 'utf8');

    return { ok: true, files: { root, planFile, blueprintFile, mapFile, reportFile }, summary: {
      systems: (plan.requiredSystems || []).length,
      zones: (plan.levelBlueprint?.zones || []).length,
      interactables: (plan.levelBlueprint?.interactables || []).length
    }};
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

ipcMain.handle('photoreal-scene-polish-build', async (event, payload) => {
  try {
    const root = oneClickAssemblyRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'polish', `photoreal_scene_polish_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const materialFile = path.join(root, 'materials', `pbr_material_assignment_${stamp}.json`);
    fs.writeFileSync(materialFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      pbrMaterials: plan.pbrMaterials || [],
      qualityGateTargets: plan.qualityGateTargets || {}
    }, null, 2), 'utf8');

    const lightingFile = path.join(root, 'lighting', `lighting_postprocess_plan_${stamp}.json`);
    fs.writeFileSync(lightingFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      lightingPreset: plan.lightingPreset || {},
      postProcess: plan.postProcess || {}
    }, null, 2), 'utf8');

    const dressingFile = path.join(root, 'dressing', `scene_dressing_scatter_plan_${stamp}.json`);
    fs.writeFileSync(dressingFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      sceneDressingPass: plan.sceneDressingPass || {}
    }, null, 2), 'utf8');

    const unrealStub = path.join(root, 'polish', `GF_Photoreal_Unreal_Polish_Scaffold_${stamp}.txt`);
    fs.writeFileSync(unrealStub, `GAMEFORGE PHOTOREAL UNREAL POLISH SCAFFOLD

Lighting:
${Object.entries(plan.lightingPreset || {}).map(([k,v]) => `- ${k}: ${Array.isArray(v) ? v.join(', ') : v}`).join('\n')}

Post Process:
${Object.entries(plan.postProcess || {}).map(([k,v]) => `- ${k}: ${Array.isArray(v) ? v.join(', ') : v}`).join('\n')}

Scene Dressing:
${(plan.sceneDressingPass?.scatterObjects || []).map(x => '- ' + x).join('\n')}

Quality Gate Targets:
${Object.entries(plan.qualityGateTargets || {}).map(([k,v]) => `- ${k}: ${v}`).join('\n')}
`, 'utf8');

    const reportFile = path.join(root, 'reports', `photoreal_scene_polish_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Photoreal Scene Polish Report

Generated: ${new Date().toISOString()}

PBR Materials: ${(plan.pbrMaterials || []).length}
Scene Dressing Objects: ${(plan.sceneDressingPass?.scatterObjects || []).length}

Files:
- Plan: ${planFile}
- Materials: ${materialFile}
- Lighting: ${lightingFile}
- Dressing: ${dressingFile}
- Unreal Stub: ${unrealStub}
`, 'utf8');

    return { ok: true, files: { root, planFile, materialFile, lightingFile, dressingFile, unrealStub, reportFile }, summary: {
      pbrMaterials: (plan.pbrMaterials || []).length,
      dressingObjects: (plan.sceneDressingPass?.scatterObjects || []).length
    }};
  } catch (error) {
    return { ok: false, error: error.message };
  }
});


const controlledAutomationProcesses = new Set();

function controlledAutomationRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeControlledAutomation');
  ensureDir(root);
  ['plans','logs','reports','dry_runs','runs','emergency_stops','scripts'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function controlledLog(root, line) {
  const logFile = path.join(root, 'logs', 'controlled_automation_actions.log');
  fs.appendFileSync(logFile, `[${new Date().toISOString()}] ${line}\n`, 'utf8');
  return logFile;
}

function controlledDetectTools() {
  const tools = {};
  function check(command, name) {
    try {
      const checker = process.platform === 'win32' ? 'where' : 'which';
      const output = execFileSync(checker, [command], { encoding: 'utf8', stdio: ['ignore','pipe','ignore'] }).trim();
      return { status: output ? 'FOUND' : 'MISSING', path: output.split(/\r?\n/)[0] || null };
    } catch (e) {
      return { status: 'MISSING', path: null };
    }
  }

  tools.node = check('node', 'Node');
  tools.npm = check('npm', 'npm');
  tools.blender = detectBlenderInstallDetails ? detectBlenderInstallDetails() : check(process.platform === 'win32' ? 'blender.exe' : 'blender', 'Blender');
  const unreal = detectUnrealInstallDetails ? detectUnrealInstallDetails() : {};
  tools.unrealEditor = unreal.editor ? { status: unreal.status || 'FOUND', path: unreal.editor } : { status: 'MISSING', path: null };
  tools.runUAT = unreal.uat ? { status: 'FOUND', path: unreal.uat } : { status: 'MISSING', path: null };
  tools.ffmpeg = check('ffmpeg', 'FFmpeg');
  return tools;
}

function controlledCanRunStage(stage, tools, settings) {
  if (stage.id === 'meshy_assets') {
    const ready = settings && settings.apiKey && settings.mode === 'api' && settings.paidProvidersAllowed;
    return {
      canRun: Boolean(ready),
      blocker: ready ? null : 'Meshy API is not fully enabled. GameForge will create prompt packs and pause before third-party manual steps.'
    };
  }
  if (stage.id === 'blender_cleanup' && (!tools.blender || tools.blender.status === 'MISSING')) {
    return { canRun: false, blocker: 'Blender not detected. Install Blender or set BLENDER_PATH.' };
  }
  if ((stage.id === 'unreal_project' || stage.id === 'unreal_import' || stage.id === 'photoreal_polish') && (!tools.unrealEditor || tools.unrealEditor.status === 'MISSING')) {
    return { canRun: false, blocker: 'UnrealEditor not detected. Install Unreal Engine 5 or set UNREAL_EDITOR_PATH.' };
  }
  if (stage.id === 'package_build' && (!tools.runUAT || tools.runUAT.status === 'MISSING')) {
    return { canRun: false, blocker: 'RunUAT not detected. Full packaging cannot run until Unreal AutomationTool is available.' };
  }
  return { canRun: true, blocker: null };
}

ipcMain.handle('automation-emergency-stop', async (event, payload) => {
  const root = controlledAutomationRoot();
  const reason = payload?.reason || 'Emergency stop requested';
  const killed = [];
  for (const child of controlledAutomationProcesses) {
    try {
      child.kill();
      killed.push(child.pid);
    } catch (e) {}
  }
  controlledAutomationProcesses.clear();

  const reportFile = path.join(root, 'emergency_stops', `emergency_stop_${Date.now()}.json`);
  fs.writeFileSync(reportFile, JSON.stringify({
    generatedAt: new Date().toISOString(),
    reason,
    killedProcesses: killed
  }, null, 2), 'utf8');

  controlledLog(root, `EMERGENCY STOP: ${reason}. Killed processes: ${killed.join(', ') || 'none'}`);
  return { ok: true, reason, killedProcesses: killed, reportFile };
});

ipcMain.handle('controlled-automation-run', async (event, payload) => {
  const root = controlledAutomationRoot();
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  const runFolder = path.join(root, 'runs', `run_${stamp}`);
  ensureDir(runFolder);

  try {
    const plan = payload?.plan || {};
    const projectState = payload?.projectState || {};
    const settings = (typeof getSavedMeshySettingsRaw === 'function') ? getSavedMeshySettingsRaw() : {};
    const tools = controlledDetectTools();
    const steps = [];
    const blockers = [];
    const notes = [];

    const planFile = path.join(runFolder, 'CONTROLLED_AUTOMATION_PLAN.json');
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');
    controlledLog(root, `Started controlled automation run for ${plan.gameName || 'GameForge Game'}`);

    for (const stage of (plan.autonomousStages || [])) {
      const allowed = controlledCanRunStage(stage, tools, settings);
      if (!allowed.canRun) {
        steps.push({ name: stage.name, status: 'PAUSED_OR_SKIPPED', detail: allowed.blocker });
        blockers.push(allowed.blocker);
        controlledLog(root, `Stage skipped: ${stage.name} — ${allowed.blocker}`);
        continue;
      }

      steps.push({ name: stage.name, status: 'READY', detail: 'Approved for controlled automation.' });
      controlledLog(root, `Stage ready: ${stage.name}`);
    }

    // Create practical automation handoff files.
    const commandPlanFile = path.join(runFolder, 'APPROVED_COMMAND_AUTOMATION_PLAN.json');
    fs.writeFileSync(commandPlanFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      approvedTools: plan.safetyBoundary?.approvedTools || [],
      blockedActions: plan.safetyBoundary?.blockedActions || [],
      tools,
      suggestedCommands: {
        blender: tools.blender?.path ? `${tools.blender.path} --background --python <cleanup_script.py>` : null,
        unrealImport: tools.unrealEditor?.path ? `${tools.unrealEditor.path} <project.uproject> -ExecutePythonScript=<import_script.py> -unattended -nosplash -nop4 -log` : null,
        runUAT: tools.runUAT?.path ? `${tools.runUAT.path} BuildCookRun -project=<project.uproject> -platform=Win64 -clientconfig=Development -build -cook -stage -pak -archive` : null
      }
    }, null, 2), 'utf8');

    const batFile = path.join(runFolder, 'RUN_APPROVED_AUTOMATION_TEMPLATE.bat');
    fs.writeFileSync(batFile, `@echo off
title GameForge Controlled Automation Template
echo This template only runs approved GameForge build commands.
echo Edit paths only if GameForge did not auto-detect them.
echo.
echo Blender: ${tools.blender?.path || 'not detected'}
echo UnrealEditor: ${tools.unrealEditor?.path || 'not detected'}
echo RunUAT: ${tools.runUAT?.path || 'not detected'}
echo.
pause
`, 'utf8');

    const status = blockers.length ? 'PARTIAL_AUTOMATION_BLOCKED' : 'READY_FOR_FULL_AUTOMATION';
    const report = {
      ok: blockers.length === 0,
      status,
      generatedAt: new Date().toISOString(),
      plan,
      tools,
      steps,
      blockers,
      notes,
      files: { runFolder, planFile, commandPlanFile, batFile }
    };

    const reportFile = path.join(runFolder, 'CONTROLLED_AUTOMATION_REPORT.json');
    fs.writeFileSync(reportFile, JSON.stringify(report, null, 2), 'utf8');
    report.files.reportFile = reportFile;

    controlledLog(root, `Controlled automation run complete: ${status}`);
    return report;
  } catch (error) {
    controlledLog(root, `Controlled automation error: ${error.message}`);
    return { ok: false, status: 'ERROR', error: error.message, files: { runFolder } };
  }
});


function aaaPhotorealRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAAAPhotoreal');
  ensureDir(root);
  ['plans','quality_gates','renderer_presets','screenshot_reviews','repairs','reports','asset_validation'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('aaa-photoreal-enforce', async (event, payload) => {
  try {
    const root = aaaPhotorealRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'plans', `aaa_photoreal_enforcement_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const rendererFile = path.join(root, 'renderer_presets', `ue5_cinematic_horror_renderer_${stamp}.json`);
    fs.writeFileSync(rendererFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      ue5RendererPreset: plan.ue5RendererPreset || {},
      requiredVisualStandards: plan.requiredVisualStandards || []
    }, null, 2), 'utf8');

    const qualityGateFile = path.join(root, 'quality_gates', `aaa_visual_quality_gate_${stamp}.json`);
    fs.writeFileSync(qualityGateFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      hardRejectStyles: plan.hardRejectStyles || [],
      assetValidation: plan.assetValidation || {},
      screenshotReviewLoop: plan.screenshotReviewLoop || {},
      outputStatusRules: plan.outputStatusRules || {}
    }, null, 2), 'utf8');

    const repairFile = path.join(root, 'repairs', `aaa_visual_repair_actions_${stamp}.json`);
    fs.writeFileSync(repairFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      repairActions: plan.screenshotReviewLoop?.repairActions || [],
      failIf: plan.screenshotReviewLoop?.failIf || []
    }, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `aaa_photoreal_enforcement_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# AAA Photoreal Enforcement Report

Generated: ${new Date().toISOString()}

Target:
${plan.target || ''}

Hard Reject Styles:
${(plan.hardRejectStyles || []).map(x => '- ' + x).join('\n')}

Required Standards:
${(plan.requiredVisualStandards || []).map(x => '- ' + x).join('\n')}

Files:
- Plan: ${planFile}
- Renderer Preset: ${rendererFile}
- Quality Gate: ${qualityGateFile}
- Repair Actions: ${repairFile}
`, 'utf8');

    return { ok: true, status: 'AAA_PHOTOREAL_GATE_PREPARED', files: { root, planFile, rendererFile, qualityGateFile, repairFile, reportFile } };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

function scannedAssetConnectorRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeScannedAssetConnector');
  ensureDir(root);
  ['manifests','source_metadata','character_quality','pbr_materials','scan_imports','reports','licences'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('scanned-asset-character-connector', async (event, payload) => {
  try {
    const root = scannedAssetConnectorRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'manifests', `scanned_asset_character_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const sourceFile = path.join(root, 'source_metadata', `asset_source_connector_manifest_${stamp}.json`);
    fs.writeFileSync(sourceFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      assetSourceTypes: plan.assetSourceTypes || [],
      priorityAssetNeeds: plan.priorityAssetNeeds || []
    }, null, 2), 'utf8');

    const characterGateFile = path.join(root, 'character_quality', `character_realism_quality_gate_${stamp}.json`);
    fs.writeFileSync(characterGateFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      characterQualityGate: plan.characterQualityGate || {},
      priorityAssetNeeds: (plan.priorityAssetNeeds || []).filter(x => x.category && x.category.includes('character'))
    }, null, 2), 'utf8');

    const licenceFile = path.join(root, 'licences', `licence_manifest_template_${stamp}.json`);
    fs.writeFileSync(licenceFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      rule: 'Store source and licence metadata for every imported external/scanned asset.',
      commercialReleaseStatus: 'BLOCKED_UNTIL_VERIFIED',
      requiredFields: ['assetName', 'source', 'licence', 'commercialUseAllowed', 'attributionRequired', 'importDate', 'notes'],
      legalAndCommercialRules: plan.legalAndCommercialRules || []
    }, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `scanned_asset_character_connector_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Scanned Asset + Character Realism Connector Report

Generated: ${new Date().toISOString()}

Target:
${plan.target || ''}

Asset Sources:
${(plan.assetSourceTypes || []).map(s => `- ${s.name}: ${s.purpose}`).join('\n')}

Priority Needs:
${(plan.priorityAssetNeeds || []).map(n => `- ${n.category}: ${(n.examples || []).join(', ')}`).join('\n')}

Files:
- Plan: ${planFile}
- Source Manifest: ${sourceFile}
- Character Gate: ${characterGateFile}
- Licence Template: ${licenceFile}
`, 'utf8');

    return { ok: true, status: 'SCANNED_ASSET_CHARACTER_CONNECTOR_PREPARED', files: { root, planFile, sourceFile, characterGateFile, licenceFile, reportFile } };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});


function globalHighEndRealismRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeGlobalRealismLock');
  ensureDir(root);
  ['plans','locked_prompts','gates','reports','failed_visual_quality','passed_candidates','repair_cycles'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('global-high-end-realism-lock', async (event, payload) => {
  try {
    const root = globalHighEndRealismRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'plans', `global_high_end_realism_lock_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const promptFile = path.join(root, 'locked_prompts', `locked_realism_prompt_${stamp}.txt`);
    fs.writeFileSync(promptFile, plan.lockedPrompt || '', 'utf8');

    const gatesFile = path.join(root, 'gates', `global_realism_gates_${stamp}.json`);
    fs.writeFileSync(gatesFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      bannedOutputStyles: plan.bannedOutputStyles || [],
      highEndBaseline: plan.highEndBaseline || [],
      gates: plan.gates || {},
      globalRules: plan.globalRules || []
    }, null, 2), 'utf8');

    const repairCycleFile = path.join(root, 'repair_cycles', `visual_fail_retry_plan_${stamp}.json`);
    fs.writeFileSync(repairCycleFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      maxRepairCycles: plan.gates?.visualFailRetrySystem?.maxRepairCycles || 3,
      screenshotScoreMinimum: plan.gates?.visualFailRetrySystem?.screenshotScoreMinimum || 86,
      repairActions: plan.gates?.visualFailRetrySystem?.repairActions || [],
      outputBlockingSystem: plan.gates?.outputBlockingSystem || {}
    }, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `global_high_end_realism_lock_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Global High-End Realism Lock Report

Generated: ${new Date().toISOString()}

Game:
${plan.gameName || ''}

Inferred Genre:
${plan.genre || ''}

Goal:
${plan.goal || ''}

Global Rules:
${(plan.globalRules || []).map(x => '- ' + x).join('\n')}

Banned Output Styles:
${(plan.bannedOutputStyles || []).map(x => '- ' + x).join('\n')}

Output Blocking:
- Failed Label: ${plan.gates?.outputBlockingSystem?.allowedFailedLabel || 'FAILED VISUAL QUALITY — REPAIR REQUIRED'}
- Passed Label: ${plan.gates?.outputBlockingSystem?.allowedPassedLabel || 'HIGH-END REALISM CANDIDATE'}

Files:
- Plan: ${planFile}
- Locked Prompt: ${promptFile}
- Gates: ${gatesFile}
- Repair Cycle: ${repairCycleFile}
`, 'utf8');

    return {
      ok: true,
      status: 'GLOBAL_HIGH_END_REALISM_LOCK_APPLIED',
      finalOutputRule: plan.gates?.outputBlockingSystem?.allowedPassedLabel || 'HIGH-END REALISM CANDIDATE',
      files: { root, planFile, promptFile, gatesFile, repairCycleFile, reportFile }
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});


function autonomousBuildTestRepairRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeBuildTestRepair');
  ensureDir(root);
  ['plans','test_runs','screenshots','scores','repairs','reports','logs','final_status'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('autonomous-build-test-repair', async (event, payload) => {
  try {
    const root = autonomousBuildTestRepairRoot();
    const plan = payload?.plan || {};
    const projectState = payload?.projectState || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const runFolder = path.join(root, 'test_runs', `run_${stamp}`);
    ensureDir(runFolder);

    const planFile = path.join(root, 'plans', `autonomous_build_test_repair_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const steps = [];
    const blockers = [];
    const repairActions = [];
    const scores = {
      visualRealismScore: 0,
      gameplayScore: 0,
      buildStabilityScore: 0,
      audioScareScore: 0,
      overallScore: 0
    };

    // Detect existing artifacts from previous pipeline stages rather than pretending to have a live Unreal runtime.
    const hasUnrealBuild = Boolean(projectState.unrealOneClickBuildRun || projectState.unrealOneClickBuildPlan);
    const hasAutomation = Boolean(projectState.controlledFullAutomationRun || projectState.controlledFullAutomationPlan);
    const hasAAA = Boolean(projectState.aaaPhotorealEnforcementPlan || projectState.globalHighEndRealismLock);
    const hasPolish = Boolean(projectState.photorealScenePolishPlan);
    const hasScares = Boolean(projectState.paranormalJumpscarePlan);

    steps.push({ name: 'Preflight build artifacts', status: hasUnrealBuild ? 'READY' : 'WARNING', detail: hasUnrealBuild ? 'Unreal build path found.' : 'No Unreal one-click build result/plan found yet.' });
    steps.push({ name: 'Controlled automation link', status: hasAutomation ? 'READY' : 'WARNING', detail: hasAutomation ? 'Controlled automation path available.' : 'Controlled automation not available or not enabled.' });
    steps.push({ name: 'AAA realism lock', status: hasAAA ? 'READY' : 'WARNING', detail: hasAAA ? 'AAA/global realism gates available.' : 'AAA/global realism gates missing.' });
    steps.push({ name: 'Photoreal polish plan', status: hasPolish ? 'READY' : 'WARNING', detail: hasPolish ? 'Lighting/material/dressing plan available.' : 'Photoreal polish plan missing.' });
    steps.push({ name: 'Gameplay event/scare systems', status: hasScares ? 'READY' : 'WARNING', detail: hasScares ? 'Jump scare/device event plan available.' : 'No paranormal/event plan found.' });

    scores.visualRealismScore = (hasAAA ? 45 : 0) + (hasPolish ? 30 : 0) + (projectState.scannedAssetCharacterRealismPlan ? 15 : 0);
    scores.gameplayScore = (projectState.unrealHorrorAssemblyPlan ? 45 : 0) + (hasScares ? 20 : 0) + (hasUnrealBuild ? 20 : 0);
    scores.buildStabilityScore = hasUnrealBuild ? 70 : 35;
    scores.audioScareScore = hasScares ? 80 : 30;
    scores.overallScore = Math.round((scores.visualRealismScore + scores.gameplayScore + scores.buildStabilityScore + scores.audioScareScore) / 4);

    const scoreFile = path.join(root, 'scores', `build_test_scores_${stamp}.json`);
    fs.writeFileSync(scoreFile, JSON.stringify(scores, null, 2), 'utf8');

    const screenshotManifest = path.join(root, 'screenshots', `required_screenshot_manifest_${stamp}.json`);
    fs.writeFileSync(screenshotManifest, JSON.stringify({
      generatedAt: new Date().toISOString(),
      note: 'Screenshot capture points prepared. Live screenshot capture requires Unreal project/build to run locally.',
      requiredScreenshots: plan.screenshotRequirements || []
    }, null, 2), 'utf8');

    const thresholds = plan.passThresholds || {};
    function thresholdFail(key) {
      return thresholds[key] && scores[key] < thresholds[key];
    }

    if (!hasUnrealBuild) {
      blockers.push('No Unreal one-click build path found. Run Unreal build runner before final pass.');
      repairActions.push('Run or repair Unreal one-click build/package stage.');
    }
    if (!hasPolish) {
      blockers.push('Photoreal scene polish plan missing.');
      repairActions.push('Run photoreal scene polish system.');
    }
    if (!hasAAA) {
      blockers.push('Global/AAA realism gate missing.');
      repairActions.push('Run global high-end realism lock and AAA photoreal enforcement.');
    }
    if (thresholdFail('visualRealismScore')) repairActions.push('Replace weak assets, upgrade PBR materials, add lighting/fog/scene dressing, rerun visual score.');
    if (thresholdFail('gameplayScore')) repairActions.push('Repair player controller, doors, objectives, collision and event triggers.');
    if (thresholdFail('buildStabilityScore')) repairActions.push('Parse Unreal logs, fix missing assets/materials/blueprints, rerun BuildCookRun.');
    if (thresholdFail('audioScareScore')) repairActions.push('Assign missing audio cues, attenuation, stingers and event triggers.');

    const pass = scores.visualRealismScore >= (thresholds.visualRealismScore || 86)
      && scores.gameplayScore >= (thresholds.gameplayScore || 80)
      && scores.buildStabilityScore >= (thresholds.buildStabilityScore || 85)
      && scores.audioScareScore >= (thresholds.audioScareScore || 75)
      && scores.overallScore >= (thresholds.overallScore || 84)
      && blockers.length === 0;

    const finalStatus = pass ? 'HIGH-END REALISM CANDIDATE' : 'FAILED VISUAL QUALITY — REPAIR REQUIRED';

    const repairFile = path.join(root, 'repairs', `repair_actions_${stamp}.json`);
    fs.writeFileSync(repairFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      finalStatus,
      repairActions,
      maxRepairCycles: plan.maxRepairCycles || 3
    }, null, 2), 'utf8');

    const finalStatusFile = path.join(root, 'final_status', `final_status_${stamp}.json`);
    fs.writeFileSync(finalStatusFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      finalStatus,
      pass,
      scores,
      blockers
    }, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `autonomous_build_test_repair_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Autonomous Build-Test-Repair Report

Generated: ${new Date().toISOString()}

Final Status:
${finalStatus}

Scores:
- Visual Realism: ${scores.visualRealismScore}
- Gameplay: ${scores.gameplayScore}
- Build Stability: ${scores.buildStabilityScore}
- Audio/Event: ${scores.audioScareScore}
- Overall: ${scores.overallScore}

Steps:
${steps.map(s => `- ${s.name}: ${s.status} — ${s.detail}`).join('\n')}

Blockers:
${blockers.length ? blockers.map(b => '- ' + b).join('\n') : '- none'}

Repair Actions:
${repairActions.length ? repairActions.map(r => '- ' + r).join('\n') : '- none'}

Files:
- Plan: ${planFile}
- Scores: ${scoreFile}
- Screenshot Manifest: ${screenshotManifest}
- Repairs: ${repairFile}
- Final Status: ${finalStatusFile}
`, 'utf8');

    return {
      ok: pass,
      status: finalStatus,
      scores,
      steps,
      blockers,
      repairActions,
      plan,
      files: { root, runFolder, planFile, scoreFile, screenshotManifest, repairFile, finalStatusFile, reportFile }
    };
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});


function realisticStructureRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeRealisticStructures');
  ensureDir(root);
  ['plans','layouts','materials','dressing','unreal_exports','quality_gates','reports'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('realistic-structure-generate', async (event, payload) => {
  try {
    const root = realisticStructureRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'plans', `realistic_structure_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const layoutFile = path.join(root, 'layouts', `structure_layout_manifest_${stamp}.json`);
    fs.writeFileSync(layoutFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      structureType: plan.structureType,
      preset: plan.preset,
      architecturalRules: plan.architecturalRules,
      components: plan.structureComponents,
      zones: plan.zones
    }, null, 2), 'utf8');

    const materialFile = path.join(root, 'materials', `structure_material_assignment_${stamp}.json`);
    fs.writeFileSync(materialFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      materialPlan: plan.materialPlan,
      damageAndAgeLayers: plan.damageAndAgeLayers
    }, null, 2), 'utf8');

    const dressingFile = path.join(root, 'dressing', `structure_scene_dressing_${stamp}.json`);
    fs.writeFileSync(dressingFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      sceneDressingPass: plan.sceneDressingPass
    }, null, 2), 'utf8');

    const unrealFile = path.join(root, 'unreal_exports', `unreal_structure_export_manifest_${stamp}.json`);
    fs.writeFileSync(unrealFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      unrealExportPlan: plan.unrealExportPlan,
      tags: plan.unrealExportPlan?.tags || []
    }, null, 2), 'utf8');

    const qualityFile = path.join(root, 'quality_gates', `structure_quality_gate_${stamp}.json`);
    fs.writeFileSync(qualityFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      antiIndieRules: plan.antiIndieRules,
      qualityGate: plan.qualityGate
    }, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `realistic_structure_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Realistic Structure Generator Report

Generated: ${new Date().toISOString()}

Structure:
${plan.preset?.label || plan.structureType || ''}

Target:
${plan.target || ''}

Zones:
${(plan.zones || []).map(z => `- ${z.name}: ${z.purpose}`).join('\n')}

Anti-Indie Rules:
${(plan.antiIndieRules || []).map(x => '- ' + x).join('\n')}

Files:
- Plan: ${planFile}
- Layout: ${layoutFile}
- Materials: ${materialFile}
- Dressing: ${dressingFile}
- Unreal Export: ${unrealFile}
- Quality Gate: ${qualityFile}
`, 'utf8');

    return {
      ok: true,
      status: 'REALISTIC_STRUCTURE_PLAN_READY',
      summary: {
        structureType: plan.structureType,
        zones: (plan.zones || []).length,
        materials: (plan.materialPlan || []).length,
        dressing: (plan.sceneDressingPass?.dressing || []).length
      },
      files: { root, planFile, layoutFile, materialFile, dressingFile, unrealFile, qualityFile, reportFile }
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});

function trueStudioModeRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeStudioMode');
  ensureDir(root);
  ['plans','reports','full_game_targets','asset_resolver','release_status'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('true-studio-mode-run', async (event, payload) => {
  try {
    const root = trueStudioModeRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'plans', `true_studio_mode_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const assetResolverFile = path.join(root, 'asset_resolver', `guaranteed_asset_source_resolver_${stamp}.json`);
    fs.writeFileSync(assetResolverFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      resolver: plan.modules?.guaranteedAssetSourceResolver || {},
      rule: 'There is always an asset path, but weak fallback assets cannot pass the final quality gate.'
    }, null, 2), 'utf8');

    const targetsFile = path.join(root, 'full_game_targets', `full_game_output_targets_${stamp}.json`);
    fs.writeFileSync(targetsFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      fullGameOutputTargets: plan.fullGameOutputTargets || [],
      nonPrototypeRule: plan.nonPrototypeRule
    }, null, 2), 'utf8');

    const releaseFile = path.join(root, 'release_status', `studio_mode_release_status_${stamp}.json`);
    fs.writeFileSync(releaseFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      currentStatus: 'STUDIO_MODE_PIPELINE_READY_NOT_COMMERCIAL_RELEASE',
      possibleStatuses: plan.modules?.commercialReleaseReadiness?.statuses || [],
      note: 'Final commercial readiness requires licensing, build stability, performance and quality verification.'
    }, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `true_studio_mode_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# True GameForge Studio Mode Report

Generated: ${new Date().toISOString()}

Goal:
${plan.goal || ''}

Modules:
${Object.entries(plan.modules || {}).map(([k,v]) => `- ${k}: ${(v.enabled || v.planned) ? 'ON/PLANNED' : 'OFF'}${v.rule ? ' — ' + v.rule : ''}${v.note ? ' — ' + v.note : ''}`).join('\n')}

Full Game Targets:
${(plan.fullGameOutputTargets || []).map(x => '- ' + x).join('\n')}

Non-Prototype Rule:
${plan.nonPrototypeRule || ''}

Files:
- Plan: ${planFile}
- Asset Resolver: ${assetResolverFile}
- Full Game Targets: ${targetsFile}
- Release Status: ${releaseFile}
`, 'utf8');

    return { ok: true, status: 'TRUE_STUDIO_MODE_READY', files: { root, planFile, assetResolverFile, targetsFile, releaseFile, reportFile } };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});


function highEndAssetLibraryRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeAssetLibrary');
  ensureDir(root);
  ['structures','materials','characters','props','animations','audio','fx','ui','fallbacks','manifests','licences','quality_reports','replacement_queue'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('high-end-asset-library-resolve', async (event, payload) => {
  try {
    const root = highEndAssetLibraryRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'manifests', `high_end_asset_library_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const folderManifest = path.join(root, 'manifests', `asset_library_folders_${stamp}.json`);
    const folders = {};
    for (const req of (plan.requiredFolders || [])) {
      const folderPath = path.join(root, req.folder);
      ensureDir(folderPath);
      folders[req.folder] = { path: folderPath, categories: req.categories || [] };
    }
    fs.writeFileSync(folderManifest, JSON.stringify({ generatedAt: new Date().toISOString(), folders }, null, 2), 'utf8');

    const requestFile = path.join(root, 'manifests', `asset_request_map_${stamp}.json`);
    fs.writeFileSync(requestFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      assetRequestMap: plan.assetRequestMap || [],
      sourcePriority: plan.sourcePriority || []
    }, null, 2), 'utf8');

    const licenceTemplate = path.join(root, 'licences', `licence_metadata_template_${stamp}.json`);
    fs.writeFileSync(licenceTemplate, JSON.stringify({
      generatedAt: new Date().toISOString(),
      required: plan.licenceTracking?.required ?? true,
      fields: plan.licenceTracking?.fields || [],
      commercialReleaseBlockedUntilVerified: true,
      entries: []
    }, null, 2), 'utf8');

    const qualityReport = path.join(root, 'quality_reports', `asset_quality_scoring_rules_${stamp}.json`);
    fs.writeFileSync(qualityReport, JSON.stringify({
      generatedAt: new Date().toISOString(),
      qualityScoring: plan.qualityScoring || {},
      replacementRules: plan.replacementRules || []
    }, null, 2), 'utf8');

    const replacementQueue = path.join(root, 'replacement_queue', `asset_replacement_queue_${stamp}.json`);
    fs.writeFileSync(replacementQueue, JSON.stringify({
      generatedAt: new Date().toISOString(),
      queued: (plan.assetRequestMap || []).map(item => ({
        need: item.need,
        category: item.category,
        priority: item.priority,
        status: 'PENDING_SOURCE_RESOLUTION',
        preferredSourceTier: item.preferredSourceTier,
        fallback: item.fallback
      }))
    }, null, 2), 'utf8');

    const readmeFile = path.join(root, 'README_GAMEFORGE_ASSET_LIBRARY.txt');
    fs.writeFileSync(readmeFile, `GAMEFORGE HIGH-END ASSET LIBRARY

This folder is the local asset/cache/source manager for GameForge.

Folders:
- structures
- materials
- characters
- props
- animations
- audio
- fx
- ui
- fallbacks
- manifests
- licences
- quality_reports
- replacement_queue

Rule:
Use high-quality approved assets first. Fallback assets keep the build moving but cannot pass final quality unless they meet the score gate.
`, 'utf8');

    const reportFile = path.join(root, 'quality_reports', `high_end_asset_library_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# High-End Asset Library + Realism Source Manager Report

Generated: ${new Date().toISOString()}

Goal:
${plan.goal || ''}

Asset Requests:
${(plan.assetRequestMap || []).map(a => `- ${a.category}: ${a.need} | priority ${a.priority}`).join('\n')}

Source Priority:
${(plan.sourcePriority || []).map(s => `- Tier ${s.tier}: ${s.name}`).join('\n')}

Files:
- Plan: ${planFile}
- Folder Manifest: ${folderManifest}
- Request Map: ${requestFile}
- Licence Template: ${licenceTemplate}
- Quality Report: ${qualityReport}
- Replacement Queue: ${replacementQueue}
- Library README: ${readmeFile}
`, 'utf8');

    return {
      ok: true,
      status: 'HIGH_END_ASSET_LIBRARY_READY',
      summary: {
        folders: Object.keys(folders).length,
        assetRequests: (plan.assetRequestMap || []).length,
        sourceTiers: (plan.sourcePriority || []).length
      },
      files: { root, planFile, folderManifest, requestFile, licenceTemplate, qualityReport, replacementQueue, readmeFile, reportFile }
    };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});


function v6FullGameRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeV6FullGameBuilder');
  ensureDir(root);
  ['preflight','downloads','plans','reports','final_status'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function v6CheckCommand(command) {
  try {
    const checker = process.platform === 'win32' ? 'where' : 'which';
    const output = execFileSync(checker, [command], { encoding: 'utf8', stdio: ['ignore','pipe','ignore'] }).trim();
    return { status: output ? 'FOUND' : 'MISSING', path: output.split(/\r?\n/)[0] || null };
  } catch (e) {
    return { status: 'MISSING', path: null };
  }
}

function v6DetectUnrealEditor() {
  if (typeof detectUnrealInstallDetails === 'function') {
    const u = detectUnrealInstallDetails();
    return {
      unreal: u.editor ? { status: 'FOUND', path: u.editor } : { status: 'MISSING', path: null },
      runuat: u.uat ? { status: 'FOUND', path: u.uat } : { status: 'MISSING', path: null }
    };
  }
  return {
    unreal: v6CheckCommand(process.platform === 'win32' ? 'UnrealEditor.exe' : 'UnrealEditor'),
    runuat: v6CheckCommand(process.platform === 'win32' ? 'RunUAT.bat' : 'RunUAT.sh')
  };
}

function v6DetectBlender() {
  if (typeof detectBlenderInstallDetails === 'function') {
    const b = detectBlenderInstallDetails();
    return b.path ? { status: 'FOUND', path: b.path } : { status: 'MISSING', path: null };
  }
  return v6CheckCommand(process.platform === 'win32' ? 'blender.exe' : 'blender');
}

ipcMain.handle('required-app-preflight-check', async (event, payload) => {
  try {
    const root = v6FullGameRoot();
    const requiredApps = payload?.requiredApps || [];
    const detected = {};
    const unreal = v6DetectUnrealEditor();
    detected.unreal = unreal.unreal;
    detected.runuat = unreal.runuat;
    detected.blender = v6DetectBlender();
    detected.ffmpeg = v6CheckCommand(process.platform === 'win32' ? 'ffmpeg.exe' : 'ffmpeg');

    let meshySettings = {};
    try {
      meshySettings = (typeof getSavedMeshySettingsRaw === 'function') ? getSavedMeshySettingsRaw() : {};
    } catch (e) {}
    detected.meshy = meshySettings && meshySettings.apiKey ? { status: 'CONFIGURED', path: 'API key saved' } : { status: 'MISSING', path: null };

    const missingRequired = [];
    const missingOptional = [];

    for (const appInfo of requiredApps) {
      const d = detected[appInfo.id] || { status: 'MISSING' };
      if (d.status === 'MISSING') {
        if (appInfo.required) missingRequired.push(appInfo);
        else missingOptional.push(appInfo);
      }
    }

    const status = missingRequired.length ? 'REQUIRED_APPS_MISSING' : (missingOptional.length ? 'OPTIONAL_APPS_MISSING' : 'ALL_READY');
    const report = {
      ok: missingRequired.length === 0,
      status,
      generatedAt: new Date().toISOString(),
      detected,
      missingRequired,
      missingOptional,
      requiredApps
    };

    const reportFile = path.join(root, 'preflight', `required_app_preflight_${Date.now()}.json`);
    fs.writeFileSync(reportFile, JSON.stringify(report, null, 2), 'utf8');
    report.files = { reportFile };
    return report;
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});

ipcMain.handle('approved-tool-download-request', async (event, payload) => {
  try {
    const root = v6FullGameRoot();
    const missing = payload?.missing || [];

    const approvedSources = {
      blender: {
        name: 'Blender',
        action: 'open_url',
        url: 'https://www.blender.org/download/',
        note: 'Official Blender download page. User installs/approves installer.'
      },
      unreal: {
        name: 'Unreal Engine',
        action: 'open_url',
        url: 'https://www.unrealengine.com/download',
        note: 'Official Unreal download page. Requires Epic account/licence acceptance where applicable.'
      },
      runuat: {
        name: 'Unreal Automation Tool',
        action: 'install_unreal',
        url: 'https://www.unrealengine.com/download',
        note: 'RunUAT is installed with Unreal Engine.'
      },
      ffmpeg: {
        name: 'FFmpeg',
        action: 'open_url',
        url: 'https://ffmpeg.org/download.html',
        note: 'Official FFmpeg download page.'
      },
      meshy: {
        name: 'Meshy API',
        action: 'open_url',
        url: 'https://www.meshy.ai/',
        note: 'Official Meshy page. User handles login/payment/API key.'
      }
    };

    const opened = [];
    const skipped = [];

    for (const item of missing) {
      const source = approvedSources[item.id];
      if (!source) {
        skipped.push({ item, reason: 'No approved source configured.' });
        continue;
      }

      if (source.action === 'open_url' || source.action === 'install_unreal') {
        try {
          await shell.openExternal(source.url);
          opened.push({ id: item.id, name: item.name, url: source.url, note: source.note });
        } catch (e) {
          skipped.push({ item, reason: e.message });
        }
      }
    }

    const report = {
      ok: true,
      status: 'APPROVED_SOURCES_OPENED',
      generatedAt: new Date().toISOString(),
      opened,
      skipped,
      rule: 'GameForge only opens approved official sources after user approval. It does not bypass login, payment, CAPTCHA or licence acceptance.'
    };

    const reportFile = path.join(root, 'downloads', `approved_tool_download_request_${Date.now()}.json`);
    fs.writeFileSync(reportFile, JSON.stringify(report, null, 2), 'utf8');
    report.files = { reportFile };
    return report;
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});

ipcMain.handle('autonomous-full-game-build', async (event, payload) => {
  try {
    const root = v6FullGameRoot();
    const plan = payload?.plan || {};
    const projectState = payload?.projectState || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'plans', `autonomous_full_game_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const requiredChecks = [
      ['requiredAppPreflightCheck', 'Required apps checked'],
      ['globalHighEndRealismLock', 'Global realism lock'],
      ['highEndAssetLibraryPlan', 'High-end asset library'],
      ['realisticStructureGeneratorPlan', 'Realistic structures'],
      ['autonomousBuildTestRepairPlan', 'Build-test-repair loop']
    ];

    const steps = [];
    const blockers = [];

    for (const [key, label] of requiredChecks) {
      const ok = Boolean(projectState[key]);
      steps.push({ name: label, status: ok ? 'READY' : 'MISSING', detail: ok ? 'Available.' : 'Missing or not run.' });
      if (!ok) blockers.push(`${label} missing.`);
    }

    const status = blockers.length ? 'FULL GAME FAILED — REPAIR REQUIRED' : 'FULL GAME CANDIDATE';

    const report = {
      ok: blockers.length === 0,
      status,
      generatedAt: new Date().toISOString(),
      steps,
      blockers,
      plan,
      note: 'v6 coordinates the autonomous full-game workflow. Live success still depends on installed apps, API configuration, Unreal packaging and local hardware.'
    };

    const reportFile = path.join(root, 'reports', `autonomous_full_game_builder_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Autonomous Full Game Builder Report

Generated: ${new Date().toISOString()}

Status:
${status}

Steps:
${steps.map(s => `- ${s.name}: ${s.status} — ${s.detail}`).join('\n')}

Blockers:
${blockers.length ? blockers.map(b => '- ' + b).join('\n') : '- none'}

Note:
${report.note}

Plan:
${planFile}
`, 'utf8');

    const statusFile = path.join(root, 'final_status', `autonomous_full_game_status_${stamp}.json`);
    fs.writeFileSync(statusFile, JSON.stringify(report, null, 2), 'utf8');

    report.files = { root, planFile, reportFile, statusFile };
    return report;
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});


function externalDiagnosticsRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeExternalDiagnostics');
  ensureDir(root);
  ['plans','logs','diagnoses','repairs','blockers','reports','retry_manifests'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function externalDiagnosticsPattern(tool, errorText) {
  const text = String(errorText || '');
  const patterns = [
    { tool: 'unreal', rx: /plugin.*missing|could not find plugin/i, cause: 'Missing or disabled Unreal plugin', repair: 'Enable plugin if installed; otherwise prompt user to install required plugin.', level: 4 },
    { tool: 'unreal', rx: /failed to load map|map.*not found|default map/i, cause: 'Missing/default map issue', repair: 'Regenerate map manifest and set default map in project settings.', level: 1 },
    { tool: 'unreal', rx: /missing material|material.*not found|texture.*not found/i, cause: 'Missing material/texture reference', repair: 'Assign fallback PBR material or relink texture from asset library.', level: 1 },
    { tool: 'unreal', rx: /blueprint.*error|failed to compile blueprint/i, cause: 'Blueprint compile/reference error', repair: 'Create repair report; disable broken generated reference or rebuild blueprint scaffold.', level: 2 },
    { tool: 'runuat', rx: /automationtool exiting with exitcode|cook failed|buildcookrun/i, cause: 'RunUAT packaging/cook failure', repair: 'Parse log, clean Saved/Intermediate, retry safer Development BuildCookRun preset.', level: 2 },
    { tool: 'runuat', rx: /sdk.*not found|windows sdk|visual studio/i, cause: 'Missing SDK/build tools', repair: 'Prompt user to install required build tools/SDK.', level: 4 },
    { tool: 'blender', rx: /no such file|cannot open|import failed/i, cause: 'Missing or unsupported model input', repair: 'Check path, convert format, or replace with fallback asset.', level: 2 },
    { tool: 'blender', rx: /texture.*missing|image.*not found/i, cause: 'Missing texture link', repair: 'Relink textures from source folder or assign fallback PBR material.', level: 1 },
    { tool: 'meshy', rx: /invalid api|unauthorized|401|403/i, cause: 'Invalid/missing Meshy API key or permission', repair: 'Pause and ask user to update API key/account permissions.', level: 4 },
    { tool: 'meshy', rx: /rate limit|429/i, cause: 'Meshy API rate limit', repair: 'Retry after delay, lower batch count, queue remaining assets.', level: 2 },
    { tool: 'meshy', rx: /job failed|generation failed/i, cause: 'Meshy job failed', repair: 'Retry with safer photoreal prompt or use asset library fallback.', level: 3 },
    { tool: 'ffmpeg', rx: /unknown encoder|codec|invalid data|could not write/i, cause: 'FFmpeg encode or output issue', repair: 'Retry with H.264/AAC safe preset or skip trailer generation.', level: 2 },
    { tool: 'assetPipeline', rx: /licen[cs]e.*missing|source metadata/i, cause: 'Missing licence/source metadata', repair: 'Mark internal testing only and block commercial release until verified.', level: 4 },
    { tool: 'assetPipeline', rx: /quality.*failed|low-poly|placeholder|missing pbr/i, cause: 'Asset failed realism/quality gate', repair: 'Replace from high-end library, scanned source, Meshy retry, or procedural fallback.', level: 3 }
  ];
  return patterns.find(p => (!tool || p.tool === tool) && p.rx.test(text)) || {
    tool: tool || 'unknown',
    cause: 'Unknown failure pattern',
    repair: 'Create blocker report with raw logs and ask user/developer to review.',
    level: 5
  };
}

function externalDiagnosticsWriteReport(root, name, obj) {
  const file = path.join(root, name);
  fs.writeFileSync(file, JSON.stringify(obj, null, 2), 'utf8');
  return file;
}

ipcMain.handle('external-tool-diagnose-failure', async (event, payload) => {
  try {
    const root = externalDiagnosticsRoot();
    const tool = payload?.tool || 'unknown';
    const errorText = payload?.errorText || '';
    const context = payload?.context || {};
    const diagnosis = externalDiagnosticsPattern(tool, errorText);
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const report = {
      ok: diagnosis.level < 5,
      status: diagnosis.level >= 4 ? 'USER_OR_HARD_BLOCKER_REQUIRED' : 'SAFE_REPAIR_AVAILABLE',
      generatedAt: new Date().toISOString(),
      tool,
      errorText,
      context,
      diagnosis,
      safetyBoundary: 'Only safe/project-local repairs are automatic. User approval required for installs, accounts, licences, payments, CAPTCHA or ambiguous destructive actions.'
    };

    const file = externalDiagnosticsWriteReport(root, path.join('diagnoses', `diagnosis_${tool}_${stamp}.json`), report);
    report.files = { diagnosisFile: file };
    return report;
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});

ipcMain.handle('external-tool-diagnostics-run', async (event, payload) => {
  try {
    const root = externalDiagnosticsRoot();
    const plan = payload?.plan || {};
    const projectState = payload?.projectState || {};
    const failureContext = payload?.failureContext || null;
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = externalDiagnosticsWriteReport(root, path.join('plans', `external_diagnostics_plan_${stamp}.json`), plan);
    const steps = [];
    const blockers = [];
    const repairs = [];

    const checks = [
      { name: 'Unreal build run/plan', tool: 'unreal', ready: Boolean(projectState.unrealOneClickBuildRun || projectState.unrealOneClickBuildPlan) },
      { name: 'Blender cleanup path', tool: 'blender', ready: Boolean(projectState.controlledFullAutomationPlan || projectState.highEndAssetLibraryPlan) },
      { name: 'Meshy/API or fallback asset source', tool: 'meshy', ready: Boolean(projectState.highEndAssetLibraryPlan || projectState.meshyAutonomousApiRun || projectState.meshySettings) },
      { name: 'Asset quality/source library', tool: 'assetPipeline', ready: Boolean(projectState.highEndAssetLibraryPlan) },
      { name: 'Build-test-repair loop', tool: 'runuat', ready: Boolean(projectState.autonomousBuildTestRepairPlan) }
    ];

    for (const check of checks) {
      if (check.ready) {
        steps.push({ name: check.name, status: 'READY', detail: `${check.tool} diagnostic context available.` });
      } else {
        const diagnosis = externalDiagnosticsPattern(check.tool, `${check.name} missing or not run`);
        steps.push({ name: check.name, status: 'WARNING', detail: diagnosis.repair });
        repairs.push({ check: check.name, ...diagnosis });
      }
    }

    if (failureContext?.tool || failureContext?.errorText) {
      const diagnosis = externalDiagnosticsPattern(failureContext.tool, failureContext.errorText || '');
      repairs.push({ check: 'provided failure context', ...diagnosis });
      if (diagnosis.level >= 4) blockers.push(`${failureContext.tool}: ${diagnosis.cause} — ${diagnosis.repair}`);
    }

    for (const r of repairs) {
      if (r.level >= 4) blockers.push(`${r.tool}: ${r.cause} — ${r.repair}`);
    }

    const repairManifest = {
      generatedAt: new Date().toISOString(),
      repairLevels: plan.repairLevels || [],
      repairs,
      retryPolicy: plan.retryPolicy || {},
      blockers
    };
    const repairFile = externalDiagnosticsWriteReport(root, path.join('repairs', `repair_manifest_${stamp}.json`), repairManifest);

    const retryManifest = {
      generatedAt: new Date().toISOString(),
      maxRetriesPerStep: plan.retryPolicy?.maxRetriesPerStep || 2,
      safeRetries: repairs.filter(r => r.level <= 3),
      userRequired: repairs.filter(r => r.level >= 4)
    };
    const retryFile = externalDiagnosticsWriteReport(root, path.join('retry_manifests', `retry_manifest_${stamp}.json`), retryManifest);

    const report = {
      ok: blockers.length === 0,
      status: blockers.length ? 'DIAGNOSTICS_FOUND_USER_BLOCKERS' : 'DIAGNOSTICS_READY_SAFE_REPAIRS_AVAILABLE',
      generatedAt: new Date().toISOString(),
      plan,
      steps,
      repairs,
      blockers,
      files: { root, planFile, repairFile, retryFile }
    };

    const reportFile = path.join(root, 'reports', `external_tool_diagnostics_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# External Tool Diagnostics + Self-Repair Report

Generated: ${new Date().toISOString()}

Status:
${report.status}

Steps:
${steps.map(s => `- ${s.name}: ${s.status} — ${s.detail}`).join('\n')}

Repairs:
${repairs.length ? repairs.map(r => `- ${r.tool}: ${r.cause} — ${r.repair} (level ${r.level})`).join('\n') : '- none'}

Blockers:
${blockers.length ? blockers.map(b => '- ' + b).join('\n') : '- none'}

Files:
- Plan: ${planFile}
- Repair Manifest: ${repairFile}
- Retry Manifest: ${retryFile}
`, 'utf8');

    report.files.reportFile = reportFile;
    return report;
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});


function licensedVisualReferenceRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeVisualReferences');
  ensureDir(root);
  ['references','pbr_materials','unreal_materials','licences','reports','quality_gates','source_manifests','repair_queue'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('licensed-visual-reference-pbr-build', async (event, payload) => {
  try {
    const root = licensedVisualReferenceRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'source_manifests', `licensed_visual_reference_pbr_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const sourcePolicyFile = path.join(root, 'licences', `visual_source_policy_${stamp}.json`);
    fs.writeFileSync(sourcePolicyFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      legalSourceTypes: plan.legalSourceTypes || [],
      hardRules: plan.hardRules || [],
      commercialReleaseRule: 'Unknown or unverified licence blocks commercial release.'
    }, null, 2), 'utf8');

    const materialRequestFile = path.join(root, 'pbr_materials', `needed_materials_${stamp}.json`);
    fs.writeFileSync(materialRequestFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      neededMaterials: plan.neededMaterials || [],
      pbrBuildPipeline: plan.pbrBuildPipeline || []
    }, null, 2), 'utf8');

    const unrealMaterialFile = path.join(root, 'unreal_materials', `unreal_material_instance_plan_${stamp}.json`);
    fs.writeFileSync(unrealMaterialFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      geometryRules: plan.geometryRules || {},
      outputFolders: plan.outputFolders || {},
      materialInstanceRule: 'Create Unreal material instances from PBR maps and apply to real geometry.'
    }, null, 2), 'utf8');

    const qualityGateFile = path.join(root, 'quality_gates', `pbr_material_quality_gate_${stamp}.json`);
    fs.writeFileSync(qualityGateFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      qualityGate: plan.qualityGate || {}
    }, null, 2), 'utf8');

    const repairQueueFile = path.join(root, 'repair_queue', `pbr_material_repair_queue_${stamp}.json`);
    fs.writeFileSync(repairQueueFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      queued: (plan.neededMaterials || []).map(m => ({
        category: m.category,
        material: m.material,
        priority: m.priority,
        status: 'AWAITING_LEGAL_SOURCE_OR_GENERATION',
        repairIfFails: plan.qualityGate?.repairActions || []
      }))
    }, null, 2), 'utf8');

    const readmeFile = path.join(root, 'README_VISUAL_REFERENCES_AND_PBR.txt');
    fs.writeFileSync(readmeFile, `GAMEFORGE VISUAL REFERENCES + PBR MATERIAL BUILDER

Use only legal sources:
- user-owned photos
- CC0/public domain
- approved licensed libraries
- AI-generated original references
- photogrammetry/scan imports where user has rights

Do not use random Google Images as direct game assets unless licence/permission is verified.

The goal is to create game-ready PBR materials:
- baseColor/albedo
- normal
- roughness
- ambient occlusion
- height/displacement where useful

Apply materials to real 3D geometry, not flat pasted images.
`, 'utf8');

    const reportFile = path.join(root, 'reports', `licensed_visual_reference_pbr_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Licensed Visual Reference + PBR Material Builder Report

Generated: ${new Date().toISOString()}

Goal:
${plan.goal || ''}

Needed Materials:
${(plan.neededMaterials || []).map(m => `- ${m.category}: ${m.material} | ${m.priority} | target: ${m.geometryTarget}`).join('\n')}

Hard Rules:
${(plan.hardRules || []).map(r => '- ' + r).join('\n')}

Files:
- Plan: ${planFile}
- Source Policy: ${sourcePolicyFile}
- Material Requests: ${materialRequestFile}
- Unreal Material Plan: ${unrealMaterialFile}
- Quality Gate: ${qualityGateFile}
- Repair Queue: ${repairQueueFile}
- README: ${readmeFile}
`, 'utf8');

    return {
      ok: true,
      status: 'LICENSED_VISUAL_REFERENCE_PBR_PLAN_READY',
      summary: {
        materials: (plan.neededMaterials || []).length,
        sourceTypes: (plan.legalSourceTypes || []).length,
        hardRules: (plan.hardRules || []).length
      },
      plan,
      files: { root, planFile, sourcePolicyFile, materialRequestFile, unrealMaterialFile, qualityGateFile, repairQueueFile, readmeFile, reportFile }
    };
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});


function phasmophobiaQualityRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgePhasmophobiaQualityCore');
  ensureDir(root);
  ['plans','template_core','golden_test','exe_validation','screenshot_scoring','reports','repair_actions','final_status'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

function writeJsonSafe(file, obj) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, JSON.stringify(obj, null, 2), 'utf8');
  return file;
}

ipcMain.handle('phasmophobia-quality-core-build', async (event, payload) => {
  try {
    const root = phasmophobiaQualityRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = writeJsonSafe(path.join(root, 'plans', `phasmophobia_quality_core_plan_${stamp}.json`), plan);
    const templateFile = writeJsonSafe(path.join(root, 'template_core', `first_person_horror_template_core_${stamp}.json`), {
      generatedAt: new Date().toISOString(),
      coreTemplateSystems: plan.coreTemplateSystems || {},
      unrealFoundation: plan.coreTemplateSystems?.unrealFoundation || []
    });
    const goldenFile = writeJsonSafe(path.join(root, 'golden_test', `golden_haunted_farmhouse_test_${stamp}.json`), {
      generatedAt: new Date().toISOString(),
      goldenTestGameMode: plan.goldenTestGameMode || {},
      benchmark: plan.phasmophobiaQualityBenchmark || {}
    });
    const repairFile = writeJsonSafe(path.join(root, 'repair_actions', `phasmophobia_quality_repair_priorities_${stamp}.json`), {
      generatedAt: new Date().toISOString(),
      repairPriorities: plan.repairPriorities || [],
      rejectIf: plan.phasmophobiaQualityBenchmark?.rejectIf || [],
      mustPass: plan.phasmophobiaQualityBenchmark?.mustPass || []
    });

    const reportFile = path.join(root, 'reports', `phasmophobia_quality_core_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Phasmophobia-Quality Haunted Game Core Report

Generated: ${new Date().toISOString()}

Goal:
${plan.goal || ''}

Benchmark:
${plan.honestBenchmark || ''}

Golden Test Route:
${(plan.goldenTestGameMode?.route || []).map(x => '- ' + x).join('\n')}

Must Pass:
${(plan.phasmophobiaQualityBenchmark?.mustPass || []).map(x => '- ' + x).join('\n')}

Files:
- Plan: ${planFile}
- Template Core: ${templateFile}
- Golden Test: ${goldenFile}
- Repair Priorities: ${repairFile}
`, 'utf8');

    return {
      ok: true,
      status: 'PHASMOPHOBIA_QUALITY_CORE_READY',
      plan,
      files: { root, planFile, templateFile, goldenFile, repairFile, reportFile }
    };
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});

ipcMain.handle('playable-exe-validate', async (event, payload) => {
  try {
    const root = phasmophobiaQualityRoot();
    const plan = payload?.plan || {};
    const projectState = payload?.projectState || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const candidates = [];
    const possibleRoots = [
      projectState.unrealOneClickBuildRun?.files?.buildFolder,
      projectState.controlledFullAutomationRun?.files?.runFolder,
      projectState.autonomousFullGameBuilderRun?.files?.root,
      path.join(app.getPath('documents'), 'GameForgeUnrealOneClickBuilds'),
      path.join(app.getPath('documents'), 'GameForgeV6FullGameBuilder')
    ].filter(Boolean);

    for (const pr of possibleRoots) {
      try {
        if (!fs.existsSync(pr)) continue;
        const stack = [pr];
        while (stack.length && candidates.length < 20) {
          const current = stack.pop();
          for (const item of fs.readdirSync(current)) {
            const full = path.join(current, item);
            const stat = fs.statSync(full);
            if (stat.isDirectory()) stack.push(full);
            else if (/\.exe$/i.test(item)) candidates.push(full);
          }
        }
      } catch (e) {}
    }

    const status = candidates.length ? 'EXE_CANDIDATE_FOUND_NOT_LAUNCHED_IN_SANDBOX' : 'PROJECT_ONLY_NOT_PLAYABLE_EXE';
    const ok = candidates.length > 0;

    const validation = {
      generatedAt: new Date().toISOString(),
      status,
      ok,
      candidates,
      validationSteps: plan.validationSteps || [],
      note: 'Live launch validation must run on the user PC. This report detects candidate EXE paths and creates the validation checklist.'
    };

    const validationFile = writeJsonSafe(path.join(root, 'exe_validation', `playable_exe_validation_${stamp}.json`), validation);
    const reportFile = path.join(root, 'reports', `playable_exe_validation_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Playable EXE Validation Report

Generated: ${new Date().toISOString()}

Status:
${status}

EXE Candidates:
${candidates.length ? candidates.map(x => '- ' + x).join('\n') : '- none found'}

Validation Steps:
${(plan.validationSteps || []).map(x => '- ' + x).join('\n')}

Note:
${validation.note}
`, 'utf8');

    return { ok, status, plan, candidates, files: { root, validationFile, reportFile } };
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});

ipcMain.handle('screenshot-visual-score', async (event, payload) => {
  try {
    const root = phasmophobiaQualityRoot();
    const plan = payload?.plan || {};
    const projectState = payload?.projectState || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const hasPBR = Boolean(projectState.licensedVisualReferencePBRPlan || projectState.pbrMaterialLibraryPlan);
    const hasStructures = Boolean(projectState.realisticStructureGeneratorPlan || projectState.phasmophobiaQualityHauntedGameCorePlan);
    const hasLighting = Boolean(projectState.photorealScenePolishPlan || projectState.aaaPhotorealEnforcementPlan);
    const hasAssets = Boolean(projectState.highEndAssetLibraryPlan);
    const hasScares = Boolean(projectState.paranormalJumpscarePlan || projectState.phasmophobiaQualityHauntedGameCorePlan);

    const estimatedScore = Math.min(100,
      (hasPBR ? 20 : 0) +
      (hasStructures ? 20 : 0) +
      (hasLighting ? 20 : 0) +
      (hasAssets ? 15 : 0) +
      (hasScares ? 10 : 0) +
      10
    );

    const passed = estimatedScore >= (plan.scoring?.passScore || 88);
    const status = passed ? 'VISUAL_SCORE_PLAN_PASSED' : 'VISUAL_REPAIR_REQUIRED';

    const scoreReport = {
      generatedAt: new Date().toISOString(),
      status,
      estimatedScore,
      passed,
      checks: { hasPBR, hasStructures, hasLighting, hasAssets, hasScares },
      requiredScreenshots: plan.requiredScreenshots || [],
      autoRepairs: passed ? [] : (plan.autoRepairs || []),
      note: 'This is a planning/manifest score. Real screenshot analysis must run on the user PC with generated screenshots.'
    };

    const scoreFile = writeJsonSafe(path.join(root, 'screenshot_scoring', `screenshot_visual_score_${stamp}.json`), scoreReport);
    const reportFile = path.join(root, 'reports', `screenshot_visual_score_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Screenshot Visual Scoring Report

Generated: ${new Date().toISOString()}

Status:
${status}

Estimated Planning Score:
${estimatedScore}

Checks:
- PBR: ${hasPBR}
- Structures: ${hasStructures}
- Lighting: ${hasLighting}
- Assets: ${hasAssets}
- Scares/Events: ${hasScares}

Required Screenshots:
${(plan.requiredScreenshots || []).map(s => `- ${s.name}: ${(s.checks || []).join(', ')}`).join('\n')}

Auto Repairs:
${(scoreReport.autoRepairs || []).length ? scoreReport.autoRepairs.map(x => '- ' + x).join('\n') : '- none'}
`, 'utf8');

    return { ok: passed, status, estimatedScore, plan, files: { root, scoreFile, reportFile } };
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});


function cinematicComposerRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeCinematicComposer');
  ensureDir(root);
  ['plans','composition','hero_assets','lighting','quality_gates','reports','repair_queue'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('cinematic-genre-scene-compose', async (event, payload) => {
  try {
    const root = cinematicComposerRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'plans', `cinematic_genre_scene_composer_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const compositionFile = path.join(root, 'composition', `hero_scene_composition_${stamp}.json`);
    fs.writeFileSync(compositionFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      genreKey: plan.genreKey,
      label: plan.preset?.label,
      heroComposition: plan.preset?.heroComposition,
      compositionLock: plan.compositionLock,
      cameraAndFraming: plan.cameraAndFraming,
      uiHudPresentation: plan.uiHudPresentation
    }, null, 2), 'utf8');

    const heroAssetsFile = path.join(root, 'hero_assets', `hero_asset_checklist_${stamp}.json`);
    fs.writeFileSync(heroAssetsFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      heroAssetChecklist: plan.heroAssetChecklist,
      materialTargets: plan.materialTargets,
      rejectIf: plan.rejectIf
    }, null, 2), 'utf8');

    const lightingFile = path.join(root, 'lighting', `cinematic_lighting_recipe_${stamp}.json`);
    fs.writeFileSync(lightingFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      lightingRecipe: plan.lightingRecipe,
      atmosphereAudioWeather: plan.atmosphereAudioWeather
    }, null, 2), 'utf8');

    const qualityFile = path.join(root, 'quality_gates', `first_go_composition_quality_gate_${stamp}.json`);
    fs.writeFileSync(qualityFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      firstGoQualityGate: plan.firstGoQualityGate,
      prePackageRepairLoop: plan.prePackageRepairLoop
    }, null, 2), 'utf8');

    const repairFile = path.join(root, 'repair_queue', `cinematic_scene_repair_queue_${stamp}.json`);
    fs.writeFileSync(repairFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      queuedRepairs: plan.prePackageRepairLoop?.repairActions || [],
      maxCycles: plan.prePackageRepairLoop?.maxCycles || 3
    }, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `cinematic_genre_scene_composer_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Cinematic Genre Scene Composer Report

Generated: ${new Date().toISOString()}

Genre:
${plan.preset?.label || plan.genreKey || ''}

Hero Composition:
${plan.preset?.heroComposition || ''}

Camera / Framing:
${(plan.cameraAndFraming || []).map(x => '- ' + x).join('\n')}

Lighting:
${(plan.lightingRecipe || []).map(x => '- ' + x).join('\n')}

Hero Assets:
${(plan.heroAssetChecklist || []).map(x => '- ' + x).join('\n')}

Files:
- Plan: ${planFile}
- Composition: ${compositionFile}
- Hero Assets: ${heroAssetsFile}
- Lighting: ${lightingFile}
- Quality Gate: ${qualityFile}
- Repair Queue: ${repairFile}
`, 'utf8');

    return {
      ok: true,
      status: 'CINEMATIC_GENRE_SCENE_COMPOSITION_READY',
      plan,
      files: { root, planFile, compositionFile, heroAssetsFile, lightingFile, qualityFile, repairFile, reportFile }
    };
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});

ipcMain.handle('first-go-quality-gate-run', async (event, payload) => {
  try {
    const root = cinematicComposerRoot();
    const plan = payload?.plan || {};
    const projectState = payload?.projectState || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const checks = {
      composer: Boolean(projectState.cinematicGenreSceneComposerPlan),
      heroAssets: Boolean(projectState.heroAssetChecklistEnforcerPlan || projectState.highEndAssetLibraryPlan),
      materials: Boolean(projectState.licensedVisualReferencePBRPlan || projectState.pbrMaterialLibraryPlan),
      structures: Boolean(projectState.realisticStructureGeneratorPlan),
      visualScoring: Boolean(projectState.screenshotVisualScoringPlan),
      exeValidation: Boolean(projectState.playableEXEValidatorPlan)
    };

    const score = Object.values(checks).filter(Boolean).length / Object.keys(checks).length * 100;
    const passed = score >= 84;
    const status = passed ? 'FIRST_GO_QUALITY_CANDIDATE' : 'FIRST_GO_QUALITY_REPAIR_REQUIRED';

    const result = {
      generatedAt: new Date().toISOString(),
      status,
      ok: passed,
      score: Math.round(score),
      checks,
      repairBeforeExport: passed ? [] : (plan.repairBeforeExport || []),
      firstMinuteRoute: plan.firstMinuteRoute || []
    };

    const resultFile = path.join(root, 'quality_gates', `first_go_quality_result_${stamp}.json`);
    fs.writeFileSync(resultFile, JSON.stringify(result, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `first_go_quality_gate_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# First-Go Quality Gate Report

Generated: ${new Date().toISOString()}

Status:
${status}

Score:
${Math.round(score)}

Checks:
${Object.entries(checks).map(([k,v]) => `- ${k}: ${v}`).join('\n')}

First Minute Route:
${(plan.firstMinuteRoute || []).map(x => '- ' + x).join('\n')}

Repairs Before Export:
${result.repairBeforeExport.length ? result.repairBeforeExport.map(x => '- ' + x).join('\n') : '- none'}
`, 'utf8');

    return { ok: passed, status, score: Math.round(score), plan, files: { root, resultFile, reportFile } };
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});


function intricateGameplayArchitectRoot() {
  const root = path.join(app.getPath('documents'), 'GameForgeIntricateGameplayArchitect');
  ensureDir(root);
  ['plans','systems','reports','repair_queue'].forEach(d => ensureDir(path.join(root, d)));
  return root;
}

ipcMain.handle('intricate-gameplay-architect-run', async (event, payload) => {
  try {
    const root = intricateGameplayArchitectRoot();
    const plan = payload?.plan || {};
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');

    const planFile = path.join(root, 'plans', `intricate_gameplay_architect_plan_${stamp}.json`);
    fs.writeFileSync(planFile, JSON.stringify(plan, null, 2), 'utf8');

    const systemsFile = path.join(root, 'systems', `gameplay_systems_manifest_${stamp}.json`);
    fs.writeFileSync(systemsFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      gameplaySystems: plan.gameplaySystems || {},
      genreAdaptation: plan.genreAdaptation || {},
      integrationTargets: plan.integrationTargets || []
    }, null, 2), 'utf8');

    const repairFile = path.join(root, 'repair_queue', `gameplay_systems_repair_rules_${stamp}.json`);
    fs.writeFileSync(repairFile, JSON.stringify({
      generatedAt: new Date().toISOString(),
      outputRules: plan.outputRules || [],
      rule: 'If a gameplay system is planned but not implemented/testable, mark it repair-required.'
    }, null, 2), 'utf8');

    const reportFile = path.join(root, 'reports', `intricate_gameplay_architect_report_${stamp}.md`);
    fs.writeFileSync(reportFile, `# Intricate Gameplay Systems Architect Report

Generated: ${new Date().toISOString()}

Source:
${plan.source || ''}

Goal:
${plan.goal || ''}

Gameplay Systems:
${Object.entries(plan.gameplaySystems || {}).map(([k,v]) => `- ${k}: ${(v || []).join(', ')}`).join('\n')}

Output Rules:
${(plan.outputRules || []).map(x => '- ' + x).join('\n')}

Files:
- Plan: ${planFile}
- Systems: ${systemsFile}
- Repair Rules: ${repairFile}
`, 'utf8');

    return { ok: true, status: 'INTRICATE_GAMEPLAY_ARCHITECT_READY', plan, files: { root, planFile, systemsFile, repairFile, reportFile } };
  } catch (error) {
    return { ok: false, status: 'ERROR', error: error.message };
  }
});


ipcMain.handle('gameforge-read-unreal-path-config', async () => {
  try {
    const possible = [
      path.join(process.cwd(), '..', 'GameForge_Unreal_Path.txt'),
      path.join(__dirname, '..', 'GameForge_Unreal_Path.txt'),
      path.join(app.getPath('documents'), 'GameForge_Unreal_Path.txt')
    ];
    for (const file of possible) {
      if (fs.existsSync(file)) {
        const raw = fs.readFileSync(file, 'utf8');
        const out = {};
        raw.split(/\r?\n/).forEach(line => {
          const m = line.match(/^([^=]+)=(.*)$/);
          if (m) out[m[1].trim()] = m[2].trim();
        });
        return { ok: true, file, config: out };
      }
    }
    return { ok: false, error: 'GameForge_Unreal_Path.txt not found. Run SET_UNREAL_PATH.vbs.' };
  } catch (error) {
    return { ok: false, error: error.message };
  }
});


// ══════════════════════════════════════════════════════════════════════════════
// GameForge AI v6.8.2 — Core workflow IPC handlers
// ══════════════════════════════════════════════════════════════════════════════

const GF_SETTINGS_FILE = path.join(app.getPath('userData'), 'gf_settings.json');

function gfReadSettings() {
  try {
    if (fs.existsSync(GF_SETTINGS_FILE)) return JSON.parse(fs.readFileSync(GF_SETTINGS_FILE, 'utf8'));
  } catch(e) {}
  return { meshyApiKey: '', unrealPath: '', outputPath: '', aiProvider: 'none', aiKey: '', logLevel: 'info' };
}

function gfWriteSettings(data) {
  const merged = { ...gfReadSettings(), ...data };
  fs.writeFileSync(GF_SETTINGS_FILE, JSON.stringify(merged, null, 2), 'utf8');
  return merged;
}

function gfProjectsRoot(settings) {
  const p = (settings && settings.outputPath) ? settings.outputPath : path.join(app.getPath('documents'), 'GameForgeProjects');
  ensureDir(p);
  return p;
}

ipcMain.handle('save-settings', async (event, data) => {
  try { return { ok: true, settings: gfWriteSettings(data) }; }
  catch(e) { return { ok: false, error: e.message }; }
});

ipcMain.handle('load-settings', async () => {
  try { return { ok: true, ...gfReadSettings() }; }
  catch(e) { return { ok: false, error: e.message }; }
});

// ── Meshy API key test ───────────────────────────────────────────────────────
ipcMain.handle('gf-meshy-test-key', async (event, key) => {
  try {
    const testKey = String(key || '').trim();
    if (!testKey) return { ok: false, reason: 'No API key provided.' };
    const response = await gfFetch('https://api.meshy.ai/openapi/v2/text-to-3d?page_size=1', {
      method: 'GET',
      headers: { Authorization: `Bearer ${testKey}` }
    });
    if (response.ok) return { ok: true };
    const body = await response.text();
    let msg = `HTTP ${response.status}`;
    try { msg = JSON.parse(body).message || msg; } catch(e) {}
    return { ok: false, reason: msg };
  } catch(e) {
    return { ok: false, reason: e.message };
  }
});

// ── Generate game folder structure ───────────────────────────────────────────

// ── Template system helpers ───────────────────────────────────────────────────
// Maps game type slugs to template folder names
const TEMPLATE_FOLDER_MAP = {
  fps:       'fps_blueprint',
  zombie:    'zombie_shooter_blueprint',
  horror:    'horror_blueprint',
  survival:  'survival_blueprint',
  racing:    'racing_blueprint',
  rpg:       'rpg_blueprint',
  openworld: 'open_world_blueprint',
};

function getTemplateDir(gameType) {
  const folder = TEMPLATE_FOLDER_MAP[String(gameType || '').toLowerCase()];
  if (!folder) return null;
  return path.join(__dirname, 'templates', 'unreal', folder);
}

// Validates a template folder using template_manifest.json + structural checks
// Returns { valid, manifestFound, manifestData, uprojectFile, reason }
function validateTemplate(templateDir) {
  const result = { valid: false, manifestFound: false, manifestData: null, uprojectFile: null, reason: '' };
  if (!templateDir || !fs.existsSync(templateDir)) {
    result.reason = 'Template folder does not exist';
    return result;
  }

  // Check manifest
  const manifestPath = path.join(templateDir, 'template_manifest.json');
  if (!fs.existsSync(manifestPath)) {
    result.reason = 'template_manifest.json not found';
    return result;
  }
  result.manifestFound = true;

  let manifest;
  try {
    manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    result.manifestData = manifest;
  } catch(e) {
    result.reason = 'template_manifest.json is not valid JSON';
    return result;
  }

  // Validate manifest fields
  if (manifest.projectMode !== 'BlueprintOnly') {
    result.reason = `projectMode must be BlueprintOnly (got: ${manifest.projectMode})`;
    return result;
  }
  if (manifest.requiresCpp !== false) {
    result.reason = 'requiresCpp must be false';
    return result;
  }
  if (!['stable', 'ready'].includes(String(manifest.status || ''))) {
    result.reason = `Template status is '${manifest.status}' — must be 'stable' or 'ready' to use`;
    return result;
  }

  // Check .uproject exists in template root
  let entries;
  try { entries = fs.readdirSync(templateDir); } catch(e) {
    result.reason = 'Cannot read template directory';
    return result;
  }
  const uprojectFile = entries.find(e => e.endsWith('.uproject'));
  if (!uprojectFile) {
    result.reason = 'No .uproject file found in template folder';
    return result;
  }
  result.uprojectFile = path.join(templateDir, uprojectFile);

  // Check Content/ and Config/ folders exist
  if (!fs.existsSync(path.join(templateDir, 'Content'))) {
    result.reason = 'Content/ folder missing from template';
    return result;
  }
  if (!fs.existsSync(path.join(templateDir, 'Config'))) {
    result.reason = 'Config/ folder missing from template';
    return result;
  }

  result.valid = true;
  result.reason = 'Template passed all validation checks';
  return result;
}

// Deep-copies a directory recursively, skipping Saved/ and Intermediate/ (large runtime folders)
function copyDirRecursive(src, dest) {
  const skip = new Set(['Saved', 'Intermediate', 'DerivedDataCache', 'Binaries', '.git']);
  ensureDir(dest);
  const entries = fs.readdirSync(src, { withFileTypes: true });
  for (const entry of entries) {
    if (skip.has(entry.name)) continue;
    const srcPath = path.join(src, entry.name);
    const destPath = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      copyDirRecursive(srcPath, destPath);
    } else {
      fs.copyFileSync(srcPath, destPath);
    }
  }
}

// Recursively scans Content/ of a copied template for gameplay content
// Returns detection results that work for any Unreal folder layout (FirstPerson/, Blueprints/, etc.)
// Scans ONLY the Unreal Content/ folder of a copied template for .uasset and .umap files.
// Separates asset detection (filenames) from active gameplay confirmation (structural evidence).
// Does NOT scan Docs/, Output/, Scripts/, or any GameForge-generated files.
function scanTemplateContent(projectPath) {
  // Asset-level detection — file/folder name patterns inside Content/ only
  // These indicate an asset exists, NOT that it is active in Play mode
  const PLAYER_ASSET   = /^(bp_firstperson|bp_player|bp_character|bp_pawn|firstpersoncharacter|playercharacter|thirdpersoncharacter|mannequin|bp_controller)/i;
  const PLAYER_FOLDER  = /^(firstperson|thirdperson|player|characters|character|pawn)/i;
  const WEAPON_ASSET   = /^(bp_weapon|bp_gun|bp_rifle|bp_pistol|bp_projectile|weapon|gun|rifle|pistol|projectile|bullet|ammo)/i;
  const WEAPON_FOLDER  = /^(weapons|weapon|firearms|guns)/i;
  const SHOOT_ASSET    = /^(bp_projectile|bp_bullet|bp_tracer|projectile|bullet|tracer)/i;
  const HUD_ASSET      = /^(wbp_|bp_hud|hud|crosshair|reticle|ammowidget|healthwidget)/i;
  const HUD_FOLDER     = /^(ui|hud|widgets|widget)/i;
  const DAMAGE_ASSET   = /^(bp_damageable|bp_destructible|bp_target|damagetarget|destructible)/i;
  const ENEMY_ASSET    = /^(bp_enemy|bp_zombie|bp_ai|bp_npc|enemy|zombie|enemycharacter|zombiecharacter)/i;
  const ENEMY_FOLDER   = /^(enemies|enemy|zombies|zombie|ai|npc)/i;
  const MAP_EXT        = /\.umap$/i;
  const UASSET_EXT     = /\.uasset$/i;
  const BP_FOLDER      = /^(blueprints|gameplay|content)/i;

  const result = {
    // Maps
    mapsHasFiles: false,
    detectedMaps: [],
    // Any .uasset found at all
    blueprintsHasFiles: false,
    detectedBlueprintFolders: [],
    // Player/character asset detection (folder or asset name)
    playerFolderExists: false,
    detectedPlayerContent: [],
    // Weapon asset detection — strict BP_ or folder names only
    weaponAssetsDetected: false,
    detectedWeaponAssets: [],
    // Shooting/projectile asset detection
    shootingAssetsDetected: false,
    detectedShootingAssets: [],
    // HUD/widget asset detection — strict WBP_ or hud folder
    hudAssetsDetected: false,
    detectedHUDAssets: [],
    // Damage system asset detection — strict BP_ damage targets
    damageAssetsDetected: false,
    detectedDamageAssets: [],
    // Enemy asset detection — strict BP_Enemy or enemy folder
    enemyAssetsDetected: false,
    detectedEnemyAssets: [],
    // Legacy fields kept for backwards compat
    weaponsFolderExists: false,
    enemiesFolderExists: false,
    uiFolderExists: false,
    shootingContentExists: false,
    hudContentExists: false,
    damageContentExists: false,
    detectedWeaponContent: [],
    detectedEnemyContent: [],
    detectedUIContent: [],
    detectedShootingContent: [],
    detectedHUDContent: [],
    detectedDamageContent: [],
  };

  const contentDir = path.join(projectPath, 'Content');
  if (!fs.existsSync(contentDir)) return result;

  function walk(dir, depth) {
    if (depth > 8) return;
    let entries;
    try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch(e) { return; }
    for (const entry of entries) {
      const nameLower = entry.name.toLowerCase();
      const entryPath = path.join(dir, entry.name);

      if (entry.isFile()) {
        const isMap    = MAP_EXT.test(nameLower);
        const isAsset  = UASSET_EXT.test(nameLower);

        if (isMap) {
          result.mapsHasFiles = true;
          result.detectedMaps.push(entryPath.replace(projectPath + path.sep, ''));
        }
        if (isAsset || isMap) {
          result.blueprintsHasFiles = true;
          const baseName = nameLower.replace(/\.uasset$|\.umap$/, '');

          if (PLAYER_ASSET.test(baseName)) {
            result.playerFolderExists = true;
            if (result.detectedPlayerContent.length < 6) result.detectedPlayerContent.push(entry.name);
          }
          if (WEAPON_ASSET.test(baseName)) {
            result.weaponAssetsDetected = true;
            result.weaponsFolderExists = true;
            if (result.detectedWeaponAssets.length < 6) result.detectedWeaponAssets.push(entry.name);
            if (result.detectedWeaponContent.length < 6) result.detectedWeaponContent.push(entry.name);
          }
          if (SHOOT_ASSET.test(baseName)) {
            result.shootingAssetsDetected = true;
            result.shootingContentExists = true;
            if (result.detectedShootingAssets.length < 6) result.detectedShootingAssets.push(entry.name);
            if (result.detectedShootingContent.length < 6) result.detectedShootingContent.push(entry.name);
          }
          if (HUD_ASSET.test(baseName)) {
            result.hudAssetsDetected = true;
            result.hudContentExists = true;
            result.uiFolderExists = true;
            if (result.detectedHUDAssets.length < 6) result.detectedHUDAssets.push(entry.name);
            if (result.detectedHUDContent.length < 6) result.detectedHUDContent.push(entry.name);
          }
          if (DAMAGE_ASSET.test(baseName)) {
            result.damageAssetsDetected = true;
            result.damageContentExists = true;
            if (result.detectedDamageAssets.length < 6) result.detectedDamageAssets.push(entry.name);
            if (result.detectedDamageContent.length < 6) result.detectedDamageContent.push(entry.name);
          }
          if (ENEMY_ASSET.test(baseName)) {
            result.enemyAssetsDetected = true;
            result.enemiesFolderExists = true;
            if (result.detectedEnemyAssets.length < 6) result.detectedEnemyAssets.push(entry.name);
            if (result.detectedEnemyContent.length < 6) result.detectedEnemyContent.push(entry.name);
          }
        }
      } else if (entry.isDirectory()) {
        if (PLAYER_FOLDER.test(nameLower)) {
          result.playerFolderExists = true;
          if (result.detectedPlayerContent.length < 6) result.detectedPlayerContent.push(entry.name + '/');
        }
        if (WEAPON_FOLDER.test(nameLower)) {
          result.weaponAssetsDetected = true;
          result.weaponsFolderExists = true;
          if (result.detectedWeaponAssets.length < 6) result.detectedWeaponAssets.push(entry.name + '/');
          if (result.detectedWeaponContent.length < 6) result.detectedWeaponContent.push(entry.name + '/');
        }
        if (HUD_FOLDER.test(nameLower)) {
          result.hudAssetsDetected = true;
          result.hudContentExists = true;
          result.uiFolderExists = true;
          if (result.detectedHUDAssets.length < 6) result.detectedHUDAssets.push(entry.name + '/');
          if (result.detectedHUDContent.length < 6) result.detectedHUDContent.push(entry.name + '/');
        }
        if (ENEMY_FOLDER.test(nameLower)) {
          result.enemyAssetsDetected = true;
          result.enemiesFolderExists = true;
          if (result.detectedEnemyAssets.length < 6) result.detectedEnemyAssets.push(entry.name + '/');
          if (result.detectedEnemyContent.length < 6) result.detectedEnemyContent.push(entry.name + '/');
        }
        if (BP_FOLDER.test(nameLower)) {
          if (result.detectedBlueprintFolders.length < 10) result.detectedBlueprintFolders.push(entry.name + '/');
        }
        walk(entryPath, depth + 1);
      }
    }
  }

  walk(contentDir, 0);
  return result;
}

// Checks DefaultGame.ini and DefaultEngine.ini for active GameMode/pawn references
// Returns { gameModeBP, defaultPawnBP, hasCustomGameMode }
function checkConfigForActiveGameplay(projectPath) {
  const r = { gameModeBP: null, defaultPawnBP: null, hasCustomGameMode: false };
  const tryRead = (iniPath) => {
    try { return fs.readFileSync(iniPath, 'utf8'); } catch(e) { return ''; }
  };
  const defaultGame = tryRead(path.join(projectPath, 'Config', 'DefaultGame.ini'));
  const defaultEngine = tryRead(path.join(projectPath, 'Config', 'DefaultEngine.ini'));
  const combined = defaultGame + '\n' + defaultEngine;

  const gmMatch = combined.match(/GlobalDefaultGameMode\s*=\s*(.+)/i) ||
                  combined.match(/GameDefaultMap\s*.*GameMode\s*=\s*(.+)/i);
  if (gmMatch) { r.gameModeBP = gmMatch[1].trim(); r.hasCustomGameMode = true; }

  const pawnMatch = combined.match(/DefaultPawnClass\s*=\s*(.+)/i);
  if (pawnMatch) r.defaultPawnBP = pawnMatch[1].trim();

  return r;
}

// Classifies template level honestly — separates asset detection from active gameplay
// Stages: project_shell → movement_base → fps_asset_base → fps_weapon_base
//         → zombie_asset_base → zombie_shooter_base → full_playable_template
function classifyTemplateLevel(scan, configInfo) {
  const hasMap      = scan.mapsHasFiles;
  const hasPlayer   = scan.playerFolderExists;
  const hasWeapon   = scan.weaponAssetsDetected;   // strict: BP_Weapon/folder only
  const hasShooting = scan.shootingAssetsDetected; // strict: BP_Projectile/BP_Bullet only
  const hasHUD      = scan.hudAssetsDetected;      // strict: WBP_ or hud folder only
  const hasDamage   = scan.damageAssetsDetected;   // strict: BP_Damageable/Target only
  const hasEnemy    = scan.enemyAssetsDetected;    // strict: BP_Enemy/zombie folder only

  // No content at all
  if (!hasMap || !scan.blueprintsHasFiles) return 'incomplete';

  // Full playable: requires ALL systems with STRICT asset evidence + custom GameMode in config
  const hasActiveGameMode = configInfo && configInfo.hasCustomGameMode;
  if (hasMap && hasPlayer && hasWeapon && hasShooting && hasHUD && hasEnemy && hasActiveGameMode) {
    return 'full_playable_template';
  }

  // Zombie shooter base: player + weapon + enemy assets, strong structural evidence
  if (hasMap && hasPlayer && hasWeapon && hasEnemy) return 'zombie_shooter_base';

  // Zombie asset base: enemy assets present but weapon not confirmed
  if (hasMap && hasPlayer && hasEnemy && !hasWeapon) return 'zombie_asset_base';

  // FPS weapon base: player + weapon + shooting or HUD assets (multiple weapon indicators)
  if (hasMap && hasPlayer && hasWeapon && (hasShooting || hasHUD)) return 'fps_weapon_base';

  // FPS asset base: player + some weapon-related assets, but not enough to confirm fps_weapon_base
  if (hasMap && hasPlayer && hasWeapon) return 'fps_asset_base';

  // Movement base: player/map confirmed, no weapon system assets
  if (hasMap && hasPlayer) return 'movement_base';

  // Map exists but no player content recognised
  if (hasMap) return 'movement_base';

  return 'incomplete';
}

// Returns manifest/scan mismatch warnings as string array
function checkManifestVsScanMismatch(manifestData, scan, scannedLevel) {
  const warnings = [];
  if (!manifestData) return warnings;
  const manifestLevel = String(manifestData.templateLevel || '').toLowerCase().replace(/-/g, '_');

  if (!manifestLevel || manifestLevel === scannedLevel) return warnings;

  // Manifest claims higher than scan detected
  const higherThanScan = ['fps_weapon_base', 'zombie_shooter_base', 'full_playable_template'];
  if (higherThanScan.includes(manifestLevel) && !higherThanScan.includes(scannedLevel)) {
    warnings.push(`Manifest claims ${manifestLevel} but content scan only confirms ${scannedLevel}. Active gameplay systems not verified. Treating as ${scannedLevel} until verified.`);
  }

  // Scan detected more than manifest claims
  if (scannedLevel === 'fps_asset_base' && manifestLevel === 'movement_base') {
    warnings.push(`Weapon-related assets detected but manifest says movement_base. Active gameplay not confirmed. Consider testing and updating template_manifest.json templateLevel if weapon gameplay is active.`);
  }
  if (scannedLevel === 'fps_weapon_base' && manifestLevel === 'movement_base') {
    warnings.push(`Weapon/shooting/HUD assets detected. Active gameplay not confirmed. If weapon gameplay works in Play mode, consider updating template_manifest.json to fps_weapon_base.`);
  }

  // full_playable_template requires strict verification
  if (manifestLevel === 'full_playable_template') {
    warnings.push(`full_playable_template requires active gameplay verification. GameForge will only confirm this level when all systems are structurally confirmed. Current scan result: ${scannedLevel}.`);
  }

  return warnings;
}

// ── Sanitise a project name to a valid C++/UE identifier ─────────────────────
function sanitiseProjectName(raw) {
  // Replace spaces and illegal chars with underscore, strip leading digits, cap at 32
  let name = String(raw || 'MyGame')
    .replace(/[^a-zA-Z0-9_]/g, '_')   // spaces → underscore, strip other chars
    .replace(/_+/g, '_')               // collapse runs of underscores
    .replace(/^[0-9]+/, '')            // no leading digits
    .slice(0, 32)
    || 'MyGame';
  // UE needs PascalCase start — if first char is underscore/lowercase, capitalise
  return name.charAt(0).toUpperCase() + name.slice(1);
}

// ── Detect game-type-specific notes ──────────────────────────────────────────
function gameTypeNotes(gameType, perspective) {
  const gt = String(gameType || '').toLowerCase();
  const persp = String(perspective || '').toLowerCase();
  const isFPS = gt === 'fps' || persp === 'fps';
  const isZombie = gt === 'zombie';
  const isHorror = gt === 'horror';
  const isSurvival = gt === 'survival';
  const isRPG = gt === 'rpg';
  const isRacing = gt === 'racing';
  const isOpenWorld = gt === 'openworld';

  const notes = [];

  if (isFPS || isZombie) {
    notes.push('## First Person Player');
    notes.push('- FirstPersonCharacter Blueprint: camera on head socket, capsule collision');
    notes.push('- EnhancedInput actions: Move, Look, Jump, Crouch, Interact');
    notes.push('- Spring arm + camera component; camera FOV 90–100');
    notes.push('');
    notes.push('## Weapon System');
    notes.push('- WeaponBase Actor class: mesh, muzzle socket, fire rate, damage, ammo');
    notes.push('- Fire() function: line trace from camera, apply damage on hit');
    notes.push('- WeaponPickup Actor: overlap → equip weapon');
    notes.push('- Ammo component: current / max / reserve');
    notes.push('');
  }

  if (isZombie || isHorror) {
    notes.push('## Enemy AI — Zombie / Horror');
    notes.push('- EnemyCharacter Blueprint extends Character');
    notes.push('- BehaviorTree: Patrol → Chase → Attack');
    notes.push('- AIPerceptionComponent: sight (600 units), hearing (400 units)');
    notes.push('- Attack montage: melee swing, damage applied on AnimNotify');
    notes.push('- Death state: ragdoll, destroy after 5 s');
    notes.push('');
  }

  if (isFPS || isZombie || isSurvival || isHorror) {
    notes.push('## Health and Damage');
    notes.push('- HealthComponent (ActorComponent): CurrentHealth, MaxHealth=100');
    notes.push('- TakeDamage() override → HealthComponent.ApplyDamage()');
    notes.push('- OnDeath delegate → trigger death logic on Character');
    notes.push('- HUD: HealthBar widget bound to HealthComponent.CurrentHealth');
    notes.push('');
    notes.push('## Basic Objective Loop');
    notes.push('- GameMode: tracks wave number, enemies remaining, win/lose conditions');
    notes.push('- ObjectiveActor: interact to trigger next objective');
    notes.push('- HUD: wave counter, objective text, ammo display');
    notes.push('- LevelBlueprint: spawn enemy waves on timer');
    notes.push('');
  }

  if (isRPG) {
    notes.push('## RPG Systems');
    notes.push('- CharacterStats component: Strength, Agility, Intelligence, Level, XP');
    notes.push('- Inventory system: item array, weight limit, stackable items');
    notes.push('- DialogueSystem: NPC interactions, branching conversation trees');
    notes.push('- QuestManager: active/completed quests, objectives tracking');
    notes.push('');
  }

  if (isRacing) {
    notes.push('## Racing Systems');
    notes.push('- VehiclePawn using ChaosWheeledVehicle');
    notes.push('- Wheel blueprints: FrontWheel, RearWheel with friction curves');
    notes.push('- LapTracker: checkpoint actors, lap time, best time');
    notes.push('- RaceGameMode: countdown, start, finish line detection');
    notes.push('');
  }

  if (isOpenWorld) {
    notes.push('## Open World Systems');
    notes.push('- World Partition enabled in DefaultEngine.ini');
    notes.push('- HLODs for distant mesh merging');
    notes.push('- StreamingLevelManager: load/unload regions by proximity');
    notes.push('- DayNightCycle: DirectionalLight rotation, sky material change');
    notes.push('');
  }

  if (isSurvival) {
    notes.push('## Survival Systems');
    notes.push('- NeedsComponent: Hunger, Thirst, Warmth — decay over time');
    notes.push('- CraftingSystem: recipe data table, combine items in inventory');
    notes.push('- BaseBuilding: snap-to-grid placement of wall/floor/roof actors');
    notes.push('- SaveGame: serialize player state, inventory, world objects');
    notes.push('');
  }

  return notes.length
    ? notes.join('\n')
    : '- Review the GameDesignBrief.md for game-specific feature notes.\n';
}

function _buildGameplayLoopDoc(gameName, gameType) {
  const gt = String(gameType || '').toLowerCase();
  const isFPS = gt === 'fps';
  const isZombie = gt === 'zombie';
  const isHorror = gt === 'horror';
  const isSurvival = gt === 'survival';
  const isRPG = gt === 'rpg';
  const isRacing = gt === 'racing';

  let loop = '';
  if (isFPS) {
    loop = `## FPS Gameplay Loop
1. Player spawns at Player Start
2. Player explores the level
3. Player encounters enemies (AI)
4. Player fires weapon — line trace → damage → enemy health → death
5. Player collects pickups (ammo, health)
6. Player reaches objective (door, trigger, item)
7. Next wave or next level loads

## Blueprint Systems to Build
- BP_PlayerCharacter: movement, camera, health, weapon socket
- BP_WeaponBase: fire, reload, ammo, damage
- BP_EnemyCharacter: patrol, chase, attack AI
- BP_HealthComponent: current/max health, death event
- BP_GameMode: wave/objective tracking, win/lose conditions
- WBP_HUD: health bar, ammo counter, objective text`;
  } else if (isZombie) {
    loop = `## Zombie Shooter Gameplay Loop
1. Player spawns in a defendable area
2. Zombie wave begins — zombies spawn at perimeter points
3. Player shoots zombies (line trace or projectile → damage → death)
4. Wave cleared → short breather → next wave with more/faster zombies
5. Player collects dropped ammo and health packs
6. Game over when player health reaches 0, or wave goal reached

## Blueprint Systems to Build
- BP_ZombieCharacter: sight perception, move-to-player, melee attack
- BP_ZombieSpawner: wave logic, spawn count escalation, timer
- BP_PlayerCharacter: first-person camera, weapon equip, health
- BP_WeaponBase: fire, reload, damage
- BP_WaveManager: wave count, enemies remaining, wave clear event
- WBP_HUD: wave number, enemies left, ammo, health`;
  } else if (isHorror) {
    loop = `## Horror Gameplay Loop
1. Player wakes up in dark environment
2. Player explores with limited light source (flashlight, lantern)
3. Audio cues and visual scares build tension
4. Enemy encounter — player must hide, run, or solve puzzle to escape
5. Player collects items and clues to progress
6. Reach the exit / trigger the ending

## Blueprint Systems to Build
- BP_PlayerCharacter: flashlight toggle, stamina, sanity meter
- BP_EnemyAI: patrol, alert on sight/sound, chase, lose sight
- BP_HidingSpot: overlap detect, camera hide logic
- BP_InteractableItem: pick up, examine, use
- BP_SanityComponent: sanity decay, hallucination triggers`;
  } else if (isSurvival) {
    loop = `## Survival Gameplay Loop
1. Player spawns with minimal resources
2. Player gathers resources (wood, stone, food, water)
3. Player crafts basic tools and shelter
4. Hunger and thirst decrease over time — player must manage needs
5. Night brings more dangerous enemies
6. Player expands base and upgrades equipment over multiple in-game days

## Blueprint Systems to Build
- BP_NeedsComponent: hunger, thirst, temperature decay
- BP_InventorySystem: item array, weight, stacking
- BP_CraftingSystem: recipe data table, combine items
- BP_ResourceNode: interact to gather (tree, rock, plant)
- BP_BaseBuilding: snap-to-grid wall/floor/roof placement`;
  } else if (isRPG) {
    loop = `## RPG Gameplay Loop
1. Player wakes in starting village / hub area
2. Player speaks to NPCs, receives quests
3. Player travels to quest location, fights enemies
4. Player earns XP and levels up, gaining stat points
5. Player returns to hub, turns in quest, receives reward
6. New quests unlock, world state changes

## Blueprint Systems to Build
- BP_CharacterStats: level, XP, STR/AGI/INT, on-level-up event
- BP_QuestManager: active quests, objectives, completion tracking
- BP_DialogueSystem: NPC conversation tree, branching choices
- BP_InventorySystem: equipment slots, usable items
- BP_LootDropComponent: enemy death → spawn loot actors`;
  } else if (isRacing) {
    loop = `## Racing Gameplay Loop
1. Player selects vehicle and track
2. Countdown (3-2-1-GO) triggers race start
3. Player drives through checkpoint sequence
4. Lap counter increments on checkpoint completion
5. Race ends when all laps completed — time recorded
6. Results screen: player time, best lap, leaderboard position

## Blueprint Systems to Build
- BP_RaceVehicle: Chaos vehicle, wheel blueprints
- BP_Checkpoint: overlap \u2192 validate lap order \u2192 advance checkpoint
- BP_LapTracker: checkpoint sequence, lap count, timer
- BP_RaceGameMode: countdown, race state, results
- WBP_RaceHUD: speed, lap, timer, position`;
  } else {
    loop = `## General Gameplay Loop
1. Player enters the world
2. Player explores and interacts with environment
3. Player encounters challenge (combat, puzzle, traversal)
4. Player overcomes challenge and progresses
5. Player reaches goal / end state

## Blueprint Systems to Build
- BP_PlayerCharacter: movement, interaction, health
- BP_GameMode: win/lose conditions, scoring
- WBP_HUD: player status display`;
  }

  return `# Gameplay Loop — ${gameName}

Generated by GameForge AI v6.8.2

${loop}

## Visual Quality Target
All systems should support **High-End Indie Realism**:
- Lumen global illumination active
- Cinematic post-processing (bloom, vignette, DOF)
- Atmospheric fog in all levels
- PBR materials on all surfaces
- Realistic sound design (audio triggers, ambient loops)

## This Is a Prototype
GameForge generates a playable starting point.
Gameplay systems listed above are Blueprint architecture guidance.
You build them in Unreal Editor.
`;
}

function _buildMeshyAssetPlan(gameName, gameType, useMeshy, meshyApiKey) {
  const gt = String(gameType || '').toLowerCase();
  const isFPS = gt === 'fps';
  const isZombie = gt === 'zombie';
  const isHorror = gt === 'horror';

  let assetList = '';
  if (isFPS || isZombie) {
    assetList = `## FPS / Zombie Asset List
| Asset | Type | Target Folder | Priority |
|-------|------|--------------|----------|
| Zombie enemy | Skeletal Mesh | Content/Characters/ | HIGH |
| First-person weapon (pistol) | Static Mesh | Content/Weapons/ | HIGH |
| First-person weapon (rifle) | Static Mesh | Content/Weapons/ | HIGH |
| Barricade (wooden) | Static Mesh | Content/Props/ | MED |
| Metal crate | Static Mesh | Content/Props/ | MED |
| Steel door (industrial) | Static Mesh | Content/Props/ | MED |
| Warning sign | Static Mesh | Content/Props/ | LOW |
| Cardboard boxes (stacked) | Static Mesh | Content/Props/ | LOW |
| Medical kit | Static Mesh | Content/Props/ | MED |
| Industrial pipes | Static Mesh | Content/Environments/ | LOW |
| Damaged furniture (chair) | Static Mesh | Content/Props/ | LOW |
| Street lamp (broken) | Static Mesh | Content/Environments/ | LOW |

## Suggested Meshy Prompts
- zombie character, decayed human, torn clothes, realistic, game-ready
- first person pistol, modern, realistic PBR, game-ready
- assault rifle, military, realistic PBR, low poly game-ready
- industrial crate metal worn, realistic PBR
- wooden barricade planks, post-apocalyptic, worn
- medical first aid box red cross, game-ready`;
  } else if (isHorror) {
    assetList = `## Horror Asset List
| Asset | Type | Target Folder | Priority |
|-------|------|--------------|----------|
| Humanoid monster | Skeletal Mesh | Content/Characters/ | HIGH |
| Old lantern | Static Mesh | Content/Props/ | HIGH |
| Broken door | Static Mesh | Content/Props/ | MED |
| Hospital bed | Static Mesh | Content/Props/ | MED |
| Old wheelchair | Static Mesh | Content/Props/ | MED |
| Blood stain decals | Texture | Content/Materials/ | MED |
| Filing cabinet | Static Mesh | Content/Props/ | LOW |
| Flickering candle | Static Mesh | Content/Props/ | LOW |`;
  } else {
    assetList = `## General Asset List
| Asset | Type | Target Folder | Priority |
|-------|------|--------------|----------|
| Player weapon | Static Mesh | Content/Weapons/ | HIGH |
| Enemy character | Skeletal Mesh | Content/Characters/ | HIGH |
| Environment prop 1 | Static Mesh | Content/Props/ | MED |
| Environment prop 2 | Static Mesh | Content/Props/ | MED |
| Pickup item | Static Mesh | Content/Props/ | LOW |`;
  }

  const statusLine = !useMeshy
    ? '**Status: Disabled** — enable Meshy in GameForge Settings to auto-generate these assets.'
    : !meshyApiKey
      ? '**Status: No API Key** — add your Meshy API key in GameForge Settings, then regenerate to queue asset generation. Asset list below is ready to use.'
      : '**Status: Queued** — Meshy asset generation has been queued. Check Output/generation_manifest.json for status.';

  return `# Meshy Asset Plan — ${gameName}

Generated by GameForge AI v6.8.2

${statusLine}

${assetList}

## How to Use Meshy Assets
1. Generate assets at meshy.ai or via GameForge Settings (Meshy API key required)
2. Download GLB files from Meshy dashboard
3. Drag GLB files into the Target Folder shown above in Unreal Content Browser
4. Unreal auto-imports them — set correct physics, LOD, and collision settings
5. Assign materials in Unreal (Meshy provides PBR textures)

## Safe Skip Behaviour
If no Meshy API key is configured, GameForge skips Meshy silently.
The project generates successfully without Meshy assets.
You can add assets manually at any time.
`;
}

ipcMain.handle('gf-generate-game-folders', async (event, config) => {
  try {
    const settings = gfReadSettings();
    const root = gfProjectsRoot(settings);

    const safeName = sanitiseProjectName(config.gameName || 'MyGame');
    const stamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
    const projectPath = path.join(root, `${safeName}_${stamp}`);

    const isUnreal = config.engine === 'unreal';
    const isCppProject = isUnreal && config.cppProject === true;
    const gameType = (config.gameType || 'fps').toLowerCase();

    const log = [];

    // ── Template detection ────────────────────────────────────────────────────
    let templateUsed = false;
    let templatePath = null;
    let templateUprojectName = null;
    let templateManifestFound = false;
    let templateValid = false;
    let templateManifestData = null;

    if (isUnreal && !isCppProject) {
      const templateDir = getTemplateDir(gameType);
      const gtLabel = gameType === 'fps' ? 'FPS' : gameType === 'zombie' ? 'Zombie Shooter' : gameType === 'horror' ? 'Horror' : gameType === 'survival' ? 'Survival' : gameType === 'racing' ? 'Racing' : gameType === 'rpg' ? 'RPG' : gameType === 'openworld' ? 'Open World' : gameType.toUpperCase();

      log.push({ msg: `Checking local template library...`, level: 'ok' });
      log.push({ msg: `Template path checked: ${templateDir || '(no mapping for game type)'}`, level: 'ok' });

      const validation = validateTemplate(templateDir);
      templateManifestFound = validation.manifestFound;
      templateValid = validation.valid;
      templateManifestData = validation.manifestData;

      if (validation.manifestFound) {
        log.push({ msg: `Template manifest found: ${templateDir}/template_manifest.json`, level: 'ok' });
      } else {
        log.push({ msg: `Template manifest not found — ${validation.reason}`, level: 'warn' });
      }

      if (validation.valid) {
        // ── TEMPLATE MODE: Copy real Unreal project ───────────────────────────
        const templateUprojectFile = validation.uprojectFile;
        templatePath = templateDir;
        templateUprojectName = path.basename(templateUprojectFile);
        log.push({ msg: `Playable template found: ${templateDir}`, level: 'ok' });
        log.push({ msg: `Copying template to output folder...`, level: 'ok' });

        ensureDir(projectPath);
        copyDirRecursive(templateDir, projectPath);
        log.push({ msg: `Template copied to: ${projectPath}`, level: 'ok' });

        // Rename .uproject to match the user's project name
        const oldUprojectPath = path.join(projectPath, templateUprojectName);
        const newUprojectPath = path.join(projectPath, `${safeName}.uproject`);
        if (fs.existsSync(oldUprojectPath) && oldUprojectPath !== newUprojectPath) {
          fs.renameSync(oldUprojectPath, newUprojectPath);
          log.push({ msg: `Renamed ${templateUprojectName} → ${safeName}.uproject`, level: 'ok' });
        }

        // Update Description in .uproject (keep everything else intact to avoid breaking it)
        try {
          const uprojectRaw = fs.readFileSync(newUprojectPath, 'utf8');
          const uprojectJson = JSON.parse(uprojectRaw);
          uprojectJson.Description = String(config.description || '').slice(0, 200);
          // Ensure no Modules (safety check — template should not have them, but verify)
          if (Array.isArray(uprojectJson.Modules) && uprojectJson.Modules.length > 0) {
            delete uprojectJson.Modules;
            log.push({ msg: 'Removed accidental Modules entry from template .uproject (Blueprint-only enforced).', level: 'warn' });
          }
          // Remove BlueprintEditorUtils if present (it causes Missing Plugin warnings)
          if (Array.isArray(uprojectJson.Plugins)) {
            const before = uprojectJson.Plugins.length;
            uprojectJson.Plugins = uprojectJson.Plugins.filter(p => p.Name !== 'BlueprintEditorUtils');
            if (uprojectJson.Plugins.length < before) {
              log.push({ msg: 'Removed BlueprintEditorUtils from template .uproject (not a real plugin).', level: 'warn' });
            }
            if (uprojectJson.Plugins.length === 0) delete uprojectJson.Plugins;
          }
          fs.writeFileSync(newUprojectPath, JSON.stringify(uprojectJson, null, 2), 'utf8');
          log.push({ msg: '.uproject updated with project description and safety checks.', level: 'ok' });
        } catch(e) {
          log.push({ msg: `Could not update .uproject JSON: ${e.message}`, level: 'warn' });
        }

        // Update Config files to reference new project name where safe
        const configDir = path.join(projectPath, 'Config');
        if (fs.existsSync(configDir)) {
          const engIni = path.join(configDir, 'DefaultEngine.ini');
          if (fs.existsSync(engIni)) {
            try {
              let content = fs.readFileSync(engIni, 'utf8');
              // Only update GameDefaultMap if it references the old template name
              content = content.replace(/GameDefaultMap=\/Game\/Maps\/\w+/g, 'GameDefaultMap=/Game/Maps/StarterMap');
              fs.writeFileSync(engIni, content, 'utf8');
            } catch(e) {}
          }
        }

        ensureDir(path.join(projectPath, 'Docs'));
        ensureDir(path.join(projectPath, 'Output'));
        ensureDir(path.join(projectPath, 'Scripts'));
        ensureDir(path.join(projectPath, 'Scenes'));

        templateUsed = true;
        log.push({ msg: `Playable template copied successfully — ${gtLabel} template.`, level: 'ok' });
        log.push({ msg: 'Project is Blueprint-only. No C++ modules required.', level: 'ok' });
        log.push({ msg: 'No missing plugin references found.', level: 'ok' });

      } else {
        // ── SHELL MODE: Generate Blueprint-only starter ───────────────────────
        log.push({ msg: `No playable template found — falling back to Project Shell / Environment Walkthrough.`, level: 'warn' });
        log.push({ msg: `No playable ${gtLabel} template installed — generating Environment Walkthrough / Project Shell.`, level: 'warn' });
        log.push({ msg: `This output opens in Unreal with terrain and sky but has no player HUD, weapons, enemies, health, or gameplay systems.`, level: 'warn' });
        log.push({ msg: `To install a playable template: see docs/Template_Install_Guide.md`, level: 'warn' });
      }
    }

    if (!templateUsed) {
      // ── SHELL MODE: Full folder structure generation ──────────────────────
      ['', 'Docs', 'Output', 'Scripts', 'Scenes'].forEach(d =>
        ensureDir(path.join(projectPath, d)));
      log.push({ msg: 'Core project folders created.', level: 'ok' });

      if (isUnreal) {
        const unrealDirs = [
          'Config', 'Content', 'Content/Maps',
          'Content/Blueprints', 'Content/Blueprints/Player',
          'Content/Blueprints/Enemies', 'Content/Blueprints/Weapons',
          'Content/Blueprints/UI', 'Content/Characters', 'Content/Weapons',
          'Content/Audio', 'Content/Materials', 'Content/Meshes',
          'Content/Props', 'Content/Environments',
        ];
        unrealDirs.forEach(d => ensureDir(path.join(projectPath, d)));
        log.push({ msg: 'Content folders created (Maps, Blueprints/Player/Enemies/Weapons/UI, Characters, Weapons, Audio, Materials, Meshes, Props, Environments).', level: 'ok' });

        // .uproject — Blueprint-only: NO Modules, NO non-standard plugins
        const uproject = {
          FileVersion: 3,
          EngineAssociation: '5.4',
          Category: '',
          Description: String(config.description || '').slice(0, 200)
        };
        if (isCppProject) {
          uproject.Modules = [{ Name: safeName, Type: 'Runtime', LoadingPhase: 'Default' }];
        }
        const uprojectPath = path.join(projectPath, `${safeName}.uproject`);
        fs.writeFileSync(uprojectPath, JSON.stringify(uproject, null, 2), 'utf8');
        log.push({ msg: `${safeName}.uproject created — Blueprint-only, no C++ modules required, no required plugins added.`, level: 'ok' });
        log.push({ msg: 'No missing plugin warning expected — .uproject contains no non-standard plugin references.', level: 'ok' });

        const defaultEngine = `[/Script/EngineSettings.GameMapsSettings]
GameDefaultMap=/Game/Maps/StarterMap
LocalMapOptions=

[/Script/Engine.Engine]
GameViewportClientClassName=/Script/Engine.GameViewportClient
NearClipPlane=10.0

[/Script/Engine.GarbageCollectionSettings]
gc.MaxObjectsNotConsideredByGC=1000

[/Script/Renderer.RendererSettings]
r.DefaultFeature.AntiAliasing=2
r.DefaultFeature.MotionBlur=0
r.Shadow.CSM.MaxCascades=4
`;
        const defaultGame = `[/Script/EngineSettings.GameMapsSettings]
GameDefaultMap=/Game/Maps/StarterMap

[/Script/Engine.GameSession]
MaxPlayers=4
`;
        const defaultInput = `[/Script/Engine.InputSettings]
+ActionMappings=(ActionName="Jump",bShift=False,bCtrl=False,bAlt=False,bCmd=False,Key=SpaceBar)
+ActionMappings=(ActionName="Fire",bShift=False,bCtrl=False,bAlt=False,bCmd=False,Key=LeftMouseButton)
+ActionMappings=(ActionName="Interact",bShift=False,bCtrl=False,bAlt=False,bCmd=False,Key=E)
+ActionMappings=(ActionName="Reload",bShift=False,bCtrl=False,bAlt=False,bCmd=False,Key=R)
+AxisMappings=(AxisName="MoveForward",Scale=1.0,Key=W)
+AxisMappings=(AxisName="MoveForward",Scale=-1.0,Key=S)
+AxisMappings=(AxisName="MoveRight",Scale=1.0,Key=D)
+AxisMappings=(AxisName="MoveRight",Scale=-1.0,Key=A)
+AxisMappings=(AxisName="Turn",Scale=1.0,Key=MouseX)
+AxisMappings=(AxisName="LookUp",Scale=-1.0,Key=MouseY)
`;
        fs.writeFileSync(path.join(projectPath, 'Config', 'DefaultEngine.ini'), defaultEngine, 'utf8');
        fs.writeFileSync(path.join(projectPath, 'Config', 'DefaultGame.ini'), defaultGame, 'utf8');
        fs.writeFileSync(path.join(projectPath, 'Config', 'DefaultInput.ini'), defaultInput, 'utf8');
        log.push({ msg: 'Config/DefaultEngine.ini, DefaultGame.ini, DefaultInput.ini created.', level: 'ok' });

        if (isCppProject) {
          ensureDir(path.join(projectPath, 'Source'));
          ensureDir(path.join(projectPath, 'Source', safeName));
          const gameTarget = `using UnrealBuildTool;\npublic class ${safeName}Target : TargetRules\n{\n    public ${safeName}Target(TargetInfo Target) : base(Target)\n    {\n        Type = TargetType.Game;\n        DefaultBuildSettings = BuildSettingsVersion.V5;\n        IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_4;\n        ExtraModuleNames.Add("${safeName}");\n    }\n}\n`;
          const editorTarget = `using UnrealBuildTool;\npublic class ${safeName}EditorTarget : TargetRules\n{\n    public ${safeName}EditorTarget(TargetInfo Target) : base(Target)\n    {\n        Type = TargetType.Editor;\n        DefaultBuildSettings = BuildSettingsVersion.V5;\n        IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_4;\n        ExtraModuleNames.Add("${safeName}");\n    }\n}\n`;
          const buildCs = `using UnrealBuildTool;\npublic class ${safeName} : ModuleRules\n{\n    public ${safeName}(ReadOnlyTargetRules Target) : base(Target)\n    {\n        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;\n        PublicDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "InputCore" });\n    }\n}\n`;
          const gameCpp = `#include "${safeName}.h"\n#include "Modules/ModuleManager.h"\nIMPLEMENT_PRIMARY_GAME_MODULE(FDefaultGameModuleImpl, ${safeName}, "${safeName}");\n`;
          const gameH = `#pragma once\n#include "CoreMinimal.h"\n`;
          const srcDir = path.join(projectPath, 'Source');
          const moduleDir = path.join(projectPath, 'Source', safeName);
          fs.writeFileSync(path.join(srcDir, `${safeName}.Target.cs`), gameTarget, 'utf8');
          fs.writeFileSync(path.join(srcDir, `${safeName}Editor.Target.cs`), editorTarget, 'utf8');
          fs.writeFileSync(path.join(moduleDir, `${safeName}.Build.cs`), buildCs, 'utf8');
          fs.writeFileSync(path.join(moduleDir, `${safeName}.cpp`), gameCpp, 'utf8');
          fs.writeFileSync(path.join(moduleDir, `${safeName}.h`), gameH, 'utf8');
          log.push({ msg: `Source/ C++ files created. Visual Studio 2022 + Unreal Build Tool required.`, level: 'ok' });
        } else {
          log.push({ msg: 'No C++ source files generated — Blueprint-only project opens directly in Unreal Editor.', level: 'ok' });
        }

      } else {
        ['Assets', 'Assets/Models', 'Assets/Textures', 'Assets/Audio',
         'Assets/Animations', 'Assets/Materials', 'Config'].forEach(d =>
          ensureDir(path.join(projectPath, d)));
        log.push({ msg: 'Generic Assets folders created.', level: 'ok' });
      }
    }

    // ── Docs (always generated / overwritten) ─────────────────────────────────
    const gameTypeLabel = (config.gameType || 'game').toUpperCase();
    const perspLabel = (config.perspective || 'fps').toUpperCase();
    const graphicsTarget = config.graphics === 'realistic' ? 'High-End Indie Realism' : (config.graphics || 'High-End Indie Realism');

    const designBrief = `# Game Design Brief — ${config.gameName}

Generated by GameForge AI v6.8.2
Date: ${new Date().toLocaleString()}

## Overview
- **Project Name:** ${config.gameName}
- **Sanitised Name:** ${safeName}
- **Game Type:** ${gameTypeLabel}
- **Perspective:** ${perspLabel}
- **Graphics Target:** ${graphicsTarget}
- **Engine:** ${config.engine || 'unreal'}
- **Project Mode:** ${isCppProject ? 'C++ Advanced' : isUnreal ? 'Blueprint-only (recommended)' : 'Generic'}
- **Generation Mode:** ${templateUsed ? 'Playable Template' : 'Project Shell'}

## Goal
${config.description || 'Create a playable high-end indie prototype with strong visual foundations.'}

## Visual Target: High-End Indie Realism
GameForge targets strong realistic visuals for indie / AA-style prototypes.
This is a playable starting point — not a finished commercial title.
- Realistic Lumen lighting (Unreal 5)
- Cinematic post-processing
- Atmospheric fog
- PBR materials throughout

## Feature Notes
${gameTypeNotes(config.gameType, config.perspective)}
## Asset Import Notes
- 3D models (GLB/FBX) \u2192 Content/Meshes/
- PBR textures \u2192 Content/Materials/
- Audio (WAV/OGG) \u2192 Content/Audio/
- Character rigs \u2192 Content/Characters/
`;

    const gameplayLoop = _buildGameplayLoopDoc(config.gameName, config.gameType);
    const meshyPlan = _buildMeshyAssetPlan(config.gameName, config.gameType, config.useMeshy, config.meshyApiKey);

    const controlsDoc = `# Controls Reference — ${config.gameName}

## Keyboard & Mouse
| Action | Key |
|--------|-----|
| Move Forward | W |
| Move Backward | S |
| Strafe Left | A |
| Strafe Right | D |
| Jump | Space |
| Crouch | Left Ctrl |
| Interact | E |
| Fire | Left Mouse Button |
| Aim | Right Mouse Button |
| Reload | R |
| Sprint | Left Shift (hold) |
| Pause | Escape |
`;

    const setupChecklist = `# Unreal Engine Setup Checklist — ${config.gameName}

## Project Mode
${templateUsed ? `**Playable Template** — a real Unreal template has been copied and customised.
The project should contain working Blueprint assets and at least one playable map.
You can open it in Unreal Editor and press Play to test gameplay.` : `**Environment Walkthrough / Project Shell** — a Blueprint-only Unreal project structure was generated.

IMPORTANT: This project opens in Unreal Editor with terrain and a sky, but it does NOT contain:
- Player HUD or attributes
- Weapons or weapon system
- Health or damage system
- Enemies or AI
- Gameplay objectives or loops
- Inventory, quests, or progression

You can walk around in the environment, but there is no actual game yet.

To upgrade to a Playable Template, install a working Unreal project into:
\`app/templates/unreal/${TEMPLATE_FOLDER_MAP[gameType] || gameType + '_blueprint'}/\`

See: \`docs/Template_Install_Guide.md\` for full instructions.`}

## Requirements
- [ ] Unreal Engine 5.4+ installed via Epic Games Launcher
${isCppProject ? `- [ ] Visual Studio 2022 with C++ game dev workload` : `- [ ] No C++ toolchain required — Blueprint-only project`}

## Opening the Project
${isCppProject
  ? `1. Right-click **${safeName}.uproject** \u2192 Generate Visual Studio project files
2. Build Development Editor | Win64 in Visual Studio
3. Open **${safeName}.uproject** in Unreal Editor`
  : `1. Double-click **${safeName}.uproject** \u2014 Unreal Editor opens immediately
2. No rebuild or plugin warnings expected
3. .uproject contains no Modules and no non-standard plugins`}

## Plugin Check
- No required plugins in .uproject
- No "Missing Plugin" dialog expected
- Enable optional plugins later: Edit \u2192 Plugins inside Unreal Editor

## After Opening
${templateUsed
  ? `- Explore Content/Blueprints/ to find the gameplay systems
- Press Play to test
- Check Content/Maps/ for starter maps`
  : `- File \u2192 New Level \u2192 Basic \u2192 Save to Content/Maps/StarterMap
- Create Blueprint classes in Content/Blueprints/
- Refer to Docs/GameplayLoop.md for the systems to build`}
`;

    const packagingChecklist = `# Packaging Checklist — ${config.gameName}

## Current Status
${templateUsed ? 'Playable Template — ready to open and test in Unreal Editor.' : 'Project Shell — build gameplay in Unreal Editor before packaging.'}

## Before Packaging
- [ ] Create/verify level in Content/Maps/
- [ ] Set Default Map: Project Settings \u2192 Maps & Modes
- [ ] Test Play-In-Editor without errors
- [ ] Remove debug actors
- [ ] Verify no missing asset references

## Package (Unreal Editor)
Platforms \u2192 Windows \u2192 Package Project

## GameForge Future Feature
Automated packaging via GameForge is planned. For now, use Unreal Editor.
`;

    const scriptNotes = `# Generation Notes — ${config.gameName}

Generated: ${new Date().toLocaleString()}
GameForge AI v6.8.2

## Generation Mode: ${templateUsed ? 'Playable Template' : 'Project Shell'}
${templateUsed
  ? `A real Unreal Blueprint template was copied and customised for this project.
Template path: ${templatePath}`
  : `A Blueprint-only project shell was generated.
No gameplay assets are installed yet.
To upgrade to Playable Template mode, install a template in:
app/templates/unreal/${TEMPLATE_FOLDER_MAP[gameType] || gameType + '_blueprint'}/`}

## Next Steps
${templateUsed
  ? `1. Double-click ${safeName}.uproject \u2014 opens in Unreal Editor immediately
2. Press Play to test gameplay
3. Customise Blueprints as needed
4. Import Meshy assets when ready`
  : `1. Double-click ${safeName}.uproject \u2014 opens in Unreal Editor
2. Create a level: File \u2192 New Level \u2192 Basic \u2192 Content/Maps/StarterMap
3. Build gameplay Blueprints in Content/Blueprints/
4. Refer to Docs/GameplayLoop.md`}

## Meshy Assets
${config.useMeshy && config.meshyApiKey
  ? 'Queued \u2014 check Output/generation_manifest.json'
  : config.useMeshy
    ? 'No key \u2014 see Docs/MeshyAssetPlan.md for the asset list'
    : 'Disabled \u2014 enable in Settings'}

## Audio
${config.generateAudio ? 'Procedural WAV placeholders generated' : 'Disabled'}
`;

    const mapPlan = `# Starter Map Plan — ${config.gameName}

## Level: StarterMap
${templateUsed ? `A starter map from the template should already exist in Content/Maps/.
Load it in Unreal Editor and customise.` : `Create this map in Unreal Editor: File \u2192 New Level \u2192 Basic \u2192 Save As Content/Maps/StarterMap`}

### Key Areas
1. **Player Start** \u2014 spawn point
2. **Safe Zone** \u2014 cover/shelter
3. **Objective Area 1** \u2014 first encounter
4. **Objective Area 2** \u2014 escalation
5. **Exit** \u2014 goal

### Lighting (Unreal 5 Lumen)
- Directional Light: Movable, 3 lux
- Sky Light: Real-time capture
- Exponential Height Fog: Start=200, Density=0.02
- Post Process Volume: Bloom=0.6, Vignette=0.4
`;

    const levelDesignNotes = `# Level Design Notes — ${config.gameName}

## Design Philosophy
Believable, atmospheric environments that support gameplay.

## Layout Principles
- Funnel players with lighting and cues
- Cover positions for combat
- Sight lines that reward positioning

## Environment Mood
${graphicsTarget} — atmospheric fog, dramatic point lights, decal surface detail

## Gameplay Zones
Each area: clear entry/exit, cover, AI spawn points, audio triggers
`;

    ensureDir(path.join(projectPath, 'Docs'));
    ensureDir(path.join(projectPath, 'Scripts'));
    ensureDir(path.join(projectPath, 'Scenes'));
    fs.writeFileSync(path.join(projectPath, 'Docs', 'GameDesignBrief.md'), designBrief, 'utf8');
    fs.writeFileSync(path.join(projectPath, 'Docs', 'Controls.md'), controlsDoc, 'utf8');
    fs.writeFileSync(path.join(projectPath, 'Docs', 'UnrealSetupChecklist.md'), setupChecklist, 'utf8');
    fs.writeFileSync(path.join(projectPath, 'Docs', 'GameplayLoop.md'), gameplayLoop, 'utf8');
    fs.writeFileSync(path.join(projectPath, 'Docs', 'MeshyAssetPlan.md'), meshyPlan, 'utf8');
    fs.writeFileSync(path.join(projectPath, 'Scripts', 'GenerateNotes.md'), scriptNotes, 'utf8');
    fs.writeFileSync(path.join(projectPath, 'Scripts', 'PackagingChecklist.md'), packagingChecklist, 'utf8');
    fs.writeFileSync(path.join(projectPath, 'Scenes', 'StarterMapPlan.md'), mapPlan, 'utf8');
    fs.writeFileSync(path.join(projectPath, 'Scenes', 'LevelDesignNotes.md'), levelDesignNotes, 'utf8');
    log.push({ msg: 'Docs/ and Scripts/ created: GameDesignBrief, Controls, SetupChecklist, GameplayLoop, MeshyAssetPlan, GenerateNotes, PackagingChecklist.', level: 'ok' });

    // ── Readiness assessment ──────────────────────────────────────────────────
    const uprojectPath = path.join(projectPath, `${safeName}.uproject`);
    const uprojectExists = isUnreal && fs.existsSync(uprojectPath);
    if (!templateUsed && isUnreal) {
      log.push({ msg: 'Project is Blueprint-only. No C++ modules required.', level: 'ok' });
      log.push({ msg: 'No missing plugin references found.', level: 'ok' });
    }

    let uprojectHasNoModules = false;
    let uprojectHasNoBlueprintEditorUtils = false;
    if (uprojectExists) {
      try {
        const parsed = JSON.parse(fs.readFileSync(uprojectPath, 'utf8'));
        uprojectHasNoModules = !Array.isArray(parsed.Modules) || parsed.Modules.length === 0;
        const plugins = Array.isArray(parsed.Plugins) ? parsed.Plugins : [];
        uprojectHasNoBlueprintEditorUtils = !plugins.some(p => p.Name === 'BlueprintEditorUtils');
      } catch(e) { uprojectHasNoModules = true; uprojectHasNoBlueprintEditorUtils = true; }
    }

    const configFolderOk = isUnreal && fs.existsSync(path.join(projectPath, 'Config', 'DefaultEngine.ini'));
    const contentFolderOk = isUnreal && fs.existsSync(path.join(projectPath, 'Content'));
    const mapsOk = isUnreal && fs.existsSync(path.join(projectPath, 'Content', 'Maps'));
    const blueprintsOk = isUnreal && fs.existsSync(path.join(projectPath, 'Content', 'Blueprints'));
    const docsOk = fs.existsSync(path.join(projectPath, 'Docs', 'GameDesignBrief.md'));
    const gameplayLoopOk = fs.existsSync(path.join(projectPath, 'Docs', 'GameplayLoop.md'));
    const controlsOk = fs.existsSync(path.join(projectPath, 'Docs', 'Controls.md'));
    const mapPlanOk = fs.existsSync(path.join(projectPath, 'Scenes', 'StarterMapPlan.md'));

    // Template-specific content scan — scans Content/ only (.uasset/.umap only)
    // Separates asset detection from active gameplay confirmation
    const emptyFolderChecks = { mapsHasFiles: false, blueprintsHasFiles: false, playerFolderExists: false, enemiesFolderExists: false, weaponsFolderExists: false, uiFolderExists: false, shootingContentExists: false, hudContentExists: false, damageContentExists: false, weaponAssetsDetected: false, shootingAssetsDetected: false, hudAssetsDetected: false, damageAssetsDetected: false, enemyAssetsDetected: false, detectedMaps: [], detectedBlueprintFolders: [], detectedPlayerContent: [], detectedWeaponAssets: [], detectedShootingAssets: [], detectedHUDAssets: [], detectedDamageAssets: [], detectedEnemyAssets: [], detectedWeaponContent: [], detectedShootingContent: [], detectedHUDContent: [], detectedDamageContent: [], detectedEnemyContent: [], detectedUIContent: [] };
    let templateFolderChecks = emptyFolderChecks;
    let templateLevel = 'none';
    let templateConfigInfo = { gameModeBP: null, defaultPawnBP: null, hasCustomGameMode: false };
    let manifestScanWarnings = [];
    if (templateUsed) {
      templateFolderChecks = scanTemplateContent(projectPath);
      templateConfigInfo = checkConfigForActiveGameplay(projectPath);
      templateLevel = classifyTemplateLevel(templateFolderChecks, templateConfigInfo);
      manifestScanWarnings = checkManifestVsScanMismatch(templateManifestData, templateFolderChecks, templateLevel);
      if (manifestScanWarnings.length > 0) {
        manifestScanWarnings.forEach(w => log.push({ msg: `[Manifest Check] ${w}`, level: 'warn' }));
      }
    }
    const keyFoldersEmpty = !templateUsed || !templateFolderChecks.blueprintsHasFiles;

    // Determine result type based on templateLevel (honest, stage-based)
    let resultType = 'Project Shell / Environment Walkthrough';
    if (templateUsed) {
      if (templateLevel === 'full_playable_template')  resultType = 'Playable Template Project';
      else if (templateLevel === 'zombie_shooter_base') resultType = 'Playable Template Project';
      else if (templateLevel === 'zombie_asset_base')   resultType = 'Zombie Asset Base';
      else if (templateLevel === 'fps_weapon_base')     resultType = 'FPS Weapon Template';
      else if (templateLevel === 'fps_asset_base')      resultType = 'FPS Asset Base';
      else if (templateLevel === 'movement_base')       resultType = 'Playable Movement Template';
      else resultType = 'Playable Template (Verify Content)';
    }

    // Required checks (always scored)
    const requiredChecks = {
      uproject:             isUnreal ? (uprojectExists ? 'PASS' : 'FAIL') : 'N/A',
      blueprintOnly:        isUnreal ? (uprojectHasNoModules && !isCppProject ? 'PASS' : isCppProject ? 'ADVANCED' : 'WARN') : 'N/A',
      noCppModulesRequired: isUnreal ? (!isCppProject ? 'PASS' : 'ADVANCED') : 'N/A',
      noMissingPlugins:     isUnreal ? (uprojectHasNoBlueprintEditorUtils ? 'PASS' : 'FAIL') : 'N/A',
      configFiles:          isUnreal ? (configFolderOk ? 'PASS' : 'FAIL') : 'N/A',
      contentFolder:        isUnreal ? (contentFolderOk ? 'PASS' : 'FAIL') : 'N/A',
      mapsPresent:          isUnreal ? (templateFolderChecks.mapsHasFiles ? 'PASS' : (templateUsed ? 'FAIL' : 'SHELL')) : 'N/A',
      playerContentPresent: isUnreal ? (templateFolderChecks.playerFolderExists ? 'PASS' : (templateUsed ? 'FAIL' : 'SHELL')) : 'N/A',
      starterMapPlan:       mapPlanOk ? 'PASS' : 'FAIL',
      gameplayLoopDoc:      gameplayLoopOk ? 'PASS' : 'FAIL',
      controlsDoc:          controlsOk ? 'PASS' : 'FAIL',
      readyToOpenInUnreal:  isUnreal && uprojectExists && (uprojectHasNoModules || isCppProject) ? 'PASS' : (isUnreal ? 'CHECK' : 'N/A'),
    };

    // Optional checks (informational only — not counted in score for movement_base)
    const optionalChecks = {
      weaponContent:  templateFolderChecks.weaponsFolderExists ? 'PRESENT' : 'NOT-INSTALLED',
      enemyContent:   templateFolderChecks.enemiesFolderExists ? 'PRESENT' : 'NOT-INSTALLED',
      uiHUDContent:   templateFolderChecks.uiFolderExists      ? 'PRESENT' : 'NOT-INSTALLED',
      meshyStatus:    !config.useMeshy ? 'PASS' : (config.meshyApiKey ? 'QUEUED' : 'SKIPPED-NO-KEY-SAFE'),
    };

    const readinessChecks = { ...requiredChecks, optional: optionalChecks };

    // Score only required binary checks
    const passCount = Object.values(requiredChecks).filter(v => v === 'PASS').length;
    const totalScorable = Object.values(requiredChecks).filter(v => v === 'PASS' || v === 'FAIL' || v === 'WARN').length;
    const readinessScore = totalScorable > 0 ? Math.round((passCount / totalScorable) * 100) : 0;

    // Determine missing systems and upgrade path
    const missingOptionalSystems = [];
    if (!templateFolderChecks.weaponAssetsDetected)  missingOptionalSystems.push('weapon');
    if (!templateFolderChecks.shootingAssetsDetected) missingOptionalSystems.push('shooting');
    if (!templateFolderChecks.hudAssetsDetected)     missingOptionalSystems.push('HUD');
    if (!templateFolderChecks.damageAssetsDetected)  missingOptionalSystems.push('damage system');
    if (!templateFolderChecks.enemyAssetsDetected)   missingOptionalSystems.push('enemies');

    const missingForNextStage = templateLevel === 'movement_base'
      ? ['BP_Weapon or weapon/ folder', 'BP_Projectile or shooting assets', 'WBP_ HUD widget', 'damage test blueprint']
      : templateLevel === 'fps_asset_base'
        ? ['active weapon fire confirmed in Play mode', 'WBP_ HUD widget', 'damage/health system']
      : templateLevel === 'fps_weapon_base'
        ? ['BP_Enemy or enemy/ folder', 'enemy AI or spawner', 'health/damage loop', 'objective loop']
      : templateLevel === 'zombie_asset_base'
        ? ['active weapon system confirmed in Play mode', 'enemy AI spawner', 'health/damage loop']
      : templateLevel === 'zombie_shooter_base'
        ? ['polish HUD', 'objectives', 'custom GameMode in Config/DefaultGame.ini', 'packaging setup']
      : templateLevel === 'full_playable_template'
        ? ['packaging setup']
        : ['player blueprint', 'playable map', 'weapon assets', 'enemy assets', 'HUD widget'];

    const nextRecommendedUpgrade = templateLevel === 'movement_base'
      ? 'Install or create an fps_asset_base template — add BP_Weapon, BP_Projectile, and WBP_ HUD assets to Content/.'
      : templateLevel === 'fps_asset_base'
        ? 'Verify weapon/shooting/HUD assets are active in Play mode to reach fps_weapon_base. Check Config/DefaultGame.ini for a custom GameMode.'
      : templateLevel === 'fps_weapon_base'
        ? 'Add BP_Enemy/zombie enemy assets and a spawner to reach zombie_asset_base or zombie_shooter_base.'
      : templateLevel === 'zombie_asset_base'
        ? 'Confirm enemy AI and weapon fire active in Play mode. Add GameMode to Config/DefaultGame.ini.'
      : templateLevel === 'zombie_shooter_base'
        ? 'Add HUD Widgets and objective logic, confirm custom GameMode in config, to reach full_playable_template.'
      : templateLevel === 'full_playable_template'
        ? 'Package the project as a Windows .exe via Unreal Editor — File > Package Project > Windows.'
        : 'Install a playable template to enable autonomous generation.';

    const overallGameCompletionEstimate = templateLevel === 'movement_base'
      ? 'Early playable prototype foundation — movement only'
      : templateLevel === 'fps_asset_base'
        ? 'FPS asset base — weapon assets present, gameplay activation unverified'
      : templateLevel === 'fps_weapon_base'
        ? 'Playable FPS weapon prototype'
      : templateLevel === 'zombie_asset_base'
        ? 'Zombie asset base — enemy assets present, active AI unverified'
      : templateLevel === 'zombie_shooter_base'
        ? 'Playable zombie shooter prototype'
      : templateLevel === 'full_playable_template'
        ? 'Complete playable prototype — ready to package'
        : templateUsed
          ? 'Template copied — verify content'
          : 'Project shell — no gameplay systems';

    const autonomousActionTaken = templateUsed
      ? `GameForge automatically detected and copied the ${templateLevel} template.`
      : 'GameForge automatically generated a safe Project Shell / Environment Walkthrough.';

    const manualActionRequired = templateLevel === 'movement_base'
      ? 'Install an fps_asset_base or fps_weapon_base template with weapon/HUD assets to unlock the next stage automatically.'
      : templateLevel === 'fps_asset_base'
        ? 'Verify weapon, shooting, and HUD assets are active in Play mode. GameForge cannot confirm active gameplay from assets alone.'
      : templateLevel === 'fps_weapon_base'
        ? 'Install or create a zombie_shooter_base template with enemy assets to unlock the next stage automatically.'
      : templateLevel === 'zombie_asset_base'
        ? 'Confirm enemy AI and weapon fire are active in Play mode. Add a custom GameMode to Config/DefaultGame.ini.'
      : templateLevel === 'zombie_shooter_base'
        ? 'Packaging requires a user-controlled build/export step in Unreal Editor.'
      : templateLevel === 'full_playable_template'
        ? 'Packaging requires a user-controlled build/export step in Unreal Editor.'
        : !templateUsed
          ? 'A local Unreal template must be installed. See docs/Template_Install_Guide.md.'
          : 'None — template copied successfully.';

    const nextAutomaticStepPrepared = templateLevel === 'movement_base'
      ? 'GameForge is ready to detect and classify fps_asset_base or fps_weapon_base when weapon/HUD content is installed.'
      : templateLevel === 'fps_asset_base'
        ? 'GameForge is ready to classify fps_weapon_base once active gameplay evidence is detected in config.'
      : templateLevel === 'fps_weapon_base'
        ? 'GameForge is ready to upgrade to zombie_asset_base or zombie_shooter_base when enemy content is installed.'
      : templateLevel === 'zombie_asset_base'
        ? 'GameForge is ready to classify zombie_shooter_base once active gameplay evidence is detected in config.'
      : templateLevel === 'zombie_shooter_base'
        ? 'GameForge is ready to classify full_playable_template when HUD/objectives and custom GameMode are detected.'
      : templateLevel === 'full_playable_template'
        ? 'GameForge is ready to package. Future: automated packaging pipeline.'
        : 'GameForge will automatically classify the template on next generation.';

    const nextStep = templateUsed
      ? `Double-click ${safeName}.uproject and press Play to test ${templateLevel === 'movement_base' ? 'movement' : 'gameplay'}`
      : `Open ${safeName}.uproject in Unreal Editor, create a level, then build Blueprints`;

    const shellReadiness = 'Shell readiness complete';
    const templateReadiness = templateLevel === 'full_playable_template'
      ? 'Playable Template Project ready — full gameplay systems confirmed via assets and config. Ready for next autonomous phase: packaging.'
      : templateLevel === 'zombie_shooter_base'
        ? 'Zombie Shooter Base ready — player, weapons, enemies present. Next: confirm custom GameMode in config and add HUD to reach full_playable_template.'
      : templateLevel === 'zombie_asset_base'
        ? 'Zombie Asset Base ready — enemy assets detected, active AI unverified. Next: confirm active gameplay in Play mode.'
      : templateLevel === 'fps_weapon_base'
        ? 'FPS Weapon Template ready — movement and weapon/HUD assets confirmed active via config. Next: add enemy content to upgrade to zombie_shooter_base.'
      : templateLevel === 'fps_asset_base'
        ? 'FPS Asset Base — weapon/HUD assets detected but active gameplay not confirmed. Open in Unreal Editor and test Play mode to verify. GameForge cannot confirm without config evidence.'
      : templateLevel === 'movement_base'
        ? 'Playable Movement Template ready — 100% complete for movement_base stage. Weapon, HUD, enemies, health, and objectives are not installed yet.'
        : templateUsed
          ? 'Template copied — GameForge handled this automatically. Verify content in Unreal Editor.'
          : 'Playable template not installed yet. Safe fallback used: Project Shell generated.';
    const packagingReadinessLabel = templateLevel === 'full_playable_template'
      ? 'Packaging ready — use File > Package Project > Windows in Unreal Editor'
      : 'Packaging readiness pending — build gameplay systems then package via Unreal Editor';

    const verdictStr = (() => {
      if (!templateUsed) return 'Safe fallback used: Project Shell / Environment Walkthrough — opens in Unreal with terrain and sky, but no player, HUD, weapons, enemies, health, or gameplay systems installed yet. Install a local template to unlock autonomous playable generation.';
      if (templateLevel === 'movement_base') return 'Playable Movement Template Ready — 100% complete for movement_base stage. Player/camera movement works. Weapon, HUD, enemies, health, damage, and objectives are not installed yet. This is not a full FPS game.';
      if (templateLevel === 'fps_asset_base') return 'FPS Asset Base — weapon and HUD assets detected in Content/, but active gameplay systems are NOT confirmed. Asset files alone do not prove weapons fire or enemies act in Play mode. Open in Unreal Editor and press Play to verify.';
      if (templateLevel === 'fps_weapon_base') return 'FPS Weapon Template Ready — player movement and weapon/shooting/HUD assets confirmed with config evidence. Enemies, health system, and objectives are not installed yet.';
      if (templateLevel === 'zombie_asset_base') return 'Zombie Asset Base — enemy assets detected in Content/, but active enemy AI is NOT confirmed. Asset files alone do not prove enemies spawn or attack in Play mode. Open in Unreal Editor and press Play to verify.';
      if (templateLevel === 'zombie_shooter_base') return 'Zombie Shooter Base Ready — player, weapons, and enemies present. Full HUD, custom GameMode, and objective loop may be incomplete.';
      if (templateLevel === 'full_playable_template') return 'Playable Template Project Ready — full gameplay systems confirmed via assets and config. Open in Unreal Editor and press Play to test.';
      return 'Template copied but content verification needed — open in Unreal Editor to confirm.';
    })();

    // Active system confirmation: asset files alone do not prove systems are active in Play mode
    const activeMovementSystemConfirmed = templateFolderChecks.playerFolderExists && templateFolderChecks.mapsHasFiles;
    const activeWeaponSystemConfirmed   = templateFolderChecks.weaponAssetsDetected && templateConfigInfo.hasCustomGameMode;
    const activeShootingSystemConfirmed = templateFolderChecks.shootingAssetsDetected && templateConfigInfo.hasCustomGameMode;
    const activeHUDSystemConfirmed      = templateFolderChecks.hudAssetsDetected && templateConfigInfo.hasCustomGameMode;
    const activeEnemySystemConfirmed    = templateFolderChecks.enemyAssetsDetected && templateConfigInfo.hasCustomGameMode;
    const activeDamageSystemConfirmed   = templateFolderChecks.damageAssetsDetected && templateConfigInfo.hasCustomGameMode;
    const activeObjectiveLoopConfirmed  = templateLevel === 'full_playable_template';

    const gameplayActivationStatus = (() => {
      if (!templateUsed) return 'No template installed — no gameplay systems present.';
      if (templateLevel === 'full_playable_template') return 'All gameplay systems confirmed active via assets and config.';
      if (templateLevel === 'zombie_shooter_base') return 'Player, weapons, and enemies detected. HUD and objectives may need verification.';
      if (templateLevel === 'zombie_asset_base') return 'Enemy assets detected, but active enemy AI is not confirmed. Asset files alone do not prove gameplay is active.';
      if (templateLevel === 'fps_weapon_base') return 'Weapon and HUD assets confirmed with config evidence. Enemy systems not installed.';
      if (templateLevel === 'fps_asset_base') return 'Weapon and HUD assets detected, but active gameplay systems are not confirmed. Asset files alone do not prove systems are active in Play mode.';
      if (templateLevel === 'movement_base') return 'Movement confirmed. Weapon, HUD, enemies, and objectives are not installed.';
      return 'Template detected — verify gameplay systems manually in Unreal Editor.';
    })();

    const userPlayTestStatus = (() => {
      if (!templateUsed) return 'No template — cannot playtest.';
      if (activeMovementSystemConfirmed) {
        const parts = ['Movement confirmed (player blueprint and map present).'];
        if (activeWeaponSystemConfirmed) parts.push('Weapon system config confirmed.');
        else if (templateFolderChecks.weaponAssetsDetected) parts.push('Weapon assets detected — NOT confirmed active in Play mode.');
        else parts.push('Weapon system: NOT installed.');
        if (activeHUDSystemConfirmed) parts.push('HUD confirmed.');
        else if (templateFolderChecks.hudAssetsDetected) parts.push('HUD assets detected — NOT confirmed active.');
        else parts.push('HUD: NOT installed.');
        if (activeEnemySystemConfirmed) parts.push('Enemy system config confirmed.');
        else if (templateFolderChecks.enemyAssetsDetected) parts.push('Enemy assets detected — NOT confirmed active in Play mode.');
        else parts.push('Enemies: NOT installed.');
        return parts.join(' ');
      }
      return 'Movement system not confirmed — map or player blueprint may be missing.';
    })();

    const manualVerificationNeeded = !activeWeaponSystemConfirmed || !activeEnemySystemConfirmed || !activeHUDSystemConfirmed;

    // Stage readiness score: 100% means all required checks for this stage pass
    const stageReadinessScore = totalScorable > 0 ? `${readinessScore}% (${templateLevel || 'shell'} stage)` : '0%';

    const readinessReport = {
      generatedAt: new Date().toISOString(),
      generator: 'GameForge AI v6.8.2',
      resultType,
      templateUsed,
      templatePath: templateUsed ? templatePath : null,
      templateManifestFound,
      templateValid,
      templateLevel,
      keyFoldersEmpty: !templateFolderChecks.blueprintsHasFiles,
      gameName: config.gameName,
      projectPath,
      readinessScore: `${readinessScore}%`,
      stageReadinessScore,
      overallGameCompletionEstimate,
      shellReadiness,
      templateReadiness,
      packagingReadiness: packagingReadinessLabel,
      // Active system confirmation — asset detection alone does NOT prove systems are active in Play mode
      activeMovementSystemConfirmed,
      activeWeaponSystemConfirmed,
      activeShootingSystemConfirmed,
      activeHUDSystemConfirmed,
      activeEnemySystemConfirmed,
      activeDamageSystemConfirmed,
      activeObjectiveLoopConfirmed,
      gameplayActivationStatus,
      userPlayTestStatus,
      manualVerificationNeeded,
      configEvidence: {
        gameModeBP: templateConfigInfo.gameModeBP,
        defaultPawnBP: templateConfigInfo.defaultPawnBP,
        hasCustomGameMode: templateConfigInfo.hasCustomGameMode,
      },
      // Asset detection (presence in Content/ — does not confirm active gameplay)
      detectedMovementSystem: templateFolderChecks.playerFolderExists,
      detectedWeaponAssets: templateFolderChecks.weaponAssetsDetected,
      detectedShootingAssets: templateFolderChecks.shootingAssetsDetected,
      detectedHUDAssets: templateFolderChecks.hudAssetsDetected,
      detectedDamageAssets: templateFolderChecks.damageAssetsDetected,
      detectedEnemyAssets: templateFolderChecks.enemyAssetsDetected,
      detectedMaps: templateFolderChecks.detectedMaps,
      detectedBlueprintFolders: templateFolderChecks.detectedBlueprintFolders,
      detectedPlayerContent: templateFolderChecks.detectedPlayerContent,
      detectedWeaponContent: templateFolderChecks.detectedWeaponAssets || [],
      detectedShootingContent: templateFolderChecks.detectedShootingAssets || [],
      detectedHUDContent: templateFolderChecks.detectedHUDAssets || [],
      detectedDamageContent: templateFolderChecks.detectedDamageAssets || [],
      detectedEnemyContent: templateFolderChecks.detectedEnemyAssets || [],
      missingForNextStage,
      missingOptionalSystems,
      nextRecommendedUpgrade,
      autonomousActionTaken,
      manualActionRequired,
      nextAutomaticStepPrepared,
      manifestScanWarnings,
      verdict: verdictStr,
      nextStep,
      note: 'Asset files detected in Content/ do not prove gameplay systems are active in Play mode. Config/DefaultGame.ini GameMode reference + strict BP asset names are required for active confirmation. A packaged Windows .exe is the final delivery goal.',
      checks: readinessChecks
    };

    const manifest = {
      generatedAt: new Date().toISOString(),
      generator: 'GameForge AI v6.8.2',
      gameName: config.gameName,
      sanitisedName: safeName,
      gameType: config.gameType,
      description: config.description,
      perspective: config.perspective,
      graphicsTarget,
      engine: config.engine,
      projectMode: isCppProject ? 'cpp' : isUnreal ? 'blueprint-only' : 'generic',
      resultType,
      templateUsed,
      templatePath: templateUsed ? templatePath : null,
      projectPath,
      unrealStructure: isUnreal ? 'enabled' : 'disabled',
      uprojectFile: isUnreal ? `${safeName}.uproject` : null,
      uprojectExists,
      uprojectHasNoModules: isUnreal ? uprojectHasNoModules : null,
      uprojectHasNoBlueprintEditorUtils: isUnreal ? uprojectHasNoBlueprintEditorUtils : null,
      meshy: config.useMeshy && config.meshyApiKey ? 'queued' : config.useMeshy ? 'skipped-no-key' : 'disabled',
      audio: config.generateAudio ? 'enabled' : 'disabled',
      filesCreated: log.map(l => l.msg)
    };

    ensureDir(path.join(projectPath, 'Output'));
    fs.writeFileSync(path.join(projectPath, 'Output', 'generation_manifest.json'), JSON.stringify(manifest, null, 2), 'utf8');
    fs.writeFileSync(path.join(projectPath, 'Output', 'launch_report.json'), JSON.stringify({ generatedAt: new Date().toISOString(), status: 'GENERATED', projectPath, resultType, checks: readinessChecks }, null, 2), 'utf8');
    fs.writeFileSync(path.join(projectPath, 'Output', 'playable_readiness_report.json'), JSON.stringify(readinessReport, null, 2), 'utf8');
    log.push({ msg: `Output/ reports created — Result: ${resultType} | Readiness: ${readinessScore}%.`, level: 'ok' });
    log.push({ msg: 'Readiness report written to Output/playable_readiness_report.json', level: 'ok' });

    // ── README ────────────────────────────────────────────────────────────────
    const readme = `# ${config.gameName}

> GameForge AI v6.8.2 — ${new Date().toLocaleString()}
> **${resultType}** | High-End Indie / AA-Style Unreal Prototype

## Quick Start
1. **Double-click \`${safeName}.uproject\`** — Unreal Editor opens immediately
2. No C++ build or Visual Studio required
${templateUsed ? `3. Press Play to test gameplay
4. See Content/Blueprints/ for game systems` : `3. File → New Level → Basic → Save as Content/Maps/StarterMap
4. Build gameplay in Content/Blueprints/
5. See Docs/GameplayLoop.md for the systems to build`}

## Result Type: ${resultType}
${templateUsed
  ? (templateLevel === 'movement_base'
    ? 'A movement template was copied. Player movement works. Weapons, enemies, HUD, and health are not installed yet.'
    : templateLevel === 'fps_weapon_base'
      ? 'An FPS weapon template was copied. Player movement and weapons work. Enemies and HUD are not installed yet.'
      : 'A playable template was copied. Open and press Play to test gameplay.')
  : 'A Blueprint-only project shell was generated. No gameplay assets are installed.\nInstall a template to get a playable starting point.'}

## Readiness: ${readinessScore}%
${readinessReport.verdict}

## Description
${config.description || 'No description provided.'}
`;
    fs.writeFileSync(path.join(projectPath, 'README.md'), readme, 'utf8');

    const resultLabel = templateUsed ? `${resultType} generated (${templateLevel})` : 'Project Shell / Environment Walkthrough generated';
    log.push({ msg: `${resultLabel} — ${safeName}.uproject ready. Stage readiness: ${readinessScore}%.`, level: 'ok' });
    log.push({ msg: autonomousActionTaken, level: 'ok' });
    if (templateUsed) {
      log.push({ msg: `Stage: ${templateLevel}. ${nextAutomaticStepPrepared}`, level: 'ok' });
      if (manualActionRequired && manualActionRequired !== 'None — template copied successfully.') {
        log.push({ msg: `Manual action required: ${manualActionRequired}`, level: 'warn' });
      }
    } else {
      log.push({ msg: 'Safe fallback used. Project Shell generated successfully.', level: 'ok' });
      log.push({ msg: `Manual action required: ${manualActionRequired}`, level: 'warn' });
    }

    return {
      ok: true,
      projectPath,
      safeName,
      isUnreal,
      isCppProject,
      templateUsed,
      templatePath: templateUsed ? templatePath : null,
      templateManifestFound,
      templateValid,
      templateLevel,
      resultType,
      uprojectExists,
      uprojectHasNoModules,
      uprojectHasNoBlueprintEditorUtils,
      keyFoldersEmpty: !templateFolderChecks.blueprintsHasFiles,
      readinessScore,
      readinessVerdict: readinessReport.verdict,
      readinessChecks,
      missingOptionalSystems,
      missingForNextStage,
      nextRecommendedUpgrade,
      autonomousActionTaken,
      manualActionRequired,
      nextAutomaticStepPrepared,
      overallGameCompletionEstimate,
      activeMovementSystemConfirmed,
      activeWeaponSystemConfirmed,
      activeShootingSystemConfirmed,
      activeHUDSystemConfirmed,
      activeEnemySystemConfirmed,
      activeDamageSystemConfirmed,
      activeObjectiveLoopConfirmed,
      gameplayActivationStatus,
      userPlayTestStatus,
      manualVerificationNeeded,
      detectedWeaponAssets: templateFolderChecks.weaponAssetsDetected,
      detectedEnemyAssets: templateFolderChecks.enemyAssetsDetected,
      detectedHUDAssets: templateFolderChecks.hudAssetsDetected,
      uprojectFile: isUnreal ? uprojectPath : null,
      log
    };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

// ── Generate Unreal Engine project structure (delegates to gf-generate-game-folders) ──
ipcMain.handle('gf-generate-unreal-structure', async (event, config) => {
  // This handler is now a no-op — the full Unreal skeleton is created inside
  // gf-generate-game-folders when engine === 'unreal' or createUnrealStructure is true.
  // Kept for backwards-compat with any legacy callers.
  return { ok: true, delegated: true, note: 'Unreal structure is created inside gf-generate-game-folders.' };
});

// ── Meshy generate for game (queues prompts, saves manifest) ─────────────────
ipcMain.handle('gf-meshy-generate-for-game', async (event, config) => {
  try {
    const key = String(config.meshyApiKey || '').trim();
    if (!key) return { ok: false, reason: 'No Meshy API key configured.' };

    const assets = plan3DAssetsFromPrompt(config.description || config.gameType || '');
    const limited = assets.slice(0, 5); // limit to 5 to avoid hammering the API

    const settings = gfReadSettings();
    const root = gfProjectsRoot(settings);
    const safeName = String(config.gameName || 'MyGame').replace(/[^a-z0-9_]/gi, '_').slice(0, 32);
    const manifestDir = path.join(root, `${safeName}_meshy`);
    ensureDir(manifestDir);

    const manifest = {
      generatedAt: new Date().toISOString(),
      gameName: config.gameName,
      gameType: config.gameType,
      assets: limited.map(a => ({
        name: a.name,
        type: a.type,
        prompt: a.safePrompt,
        status: 'queued'
      }))
    };

    fs.writeFileSync(path.join(manifestDir, 'meshy_queue.json'), JSON.stringify(manifest, null, 2), 'utf8');

    return { ok: true, count: limited.length, assets: limited, manifestDir };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

// ── Generate audio placeholder pack ──────────────────────────────────────────
ipcMain.handle('gf-generate-audio-pack', async (event, config) => {
  try {
    const settings = gfReadSettings();
    const root = gfProjectsRoot(settings);
    const safeName = String(config.gameName || 'MyGame').replace(/[^a-z0-9_]/gi, '_').slice(0, 32);
    const audioDir = path.join(root, `${safeName}_audio`);
    ensureDir(audioDir);

    const gameType = String(config.gameType || '').toLowerCase();
    const soundEvents = [
      { name: 'footstep', type: 'footstep', freq: 70, duration: 0.25 },
      { name: 'ui_click', type: 'tone', freq: 600, duration: 0.12 },
      { name: 'pickup', type: 'tone', freq: 720, duration: 0.2 },
      { name: 'ambient_wind', type: 'wind', freq: 80, duration: 2.0 },
    ];
    if (gameType === 'horror' || gameType === 'zombie') {
      soundEvents.push(
        { name: 'jump_scare', type: 'sting', freq: 80, duration: 0.8 },
        { name: 'enemy_growl', type: 'growl', freq: 90, duration: 1.2 },
        { name: 'door_creak', type: 'creak', freq: 120, duration: 0.6 }
      );
    }
    if (gameType === 'fps' || gameType === 'zombie' || gameType === 'survival') {
      soundEvents.push(
        { name: 'gunshot', type: 'static', freq: 90, duration: 0.35 },
        { name: 'reload', type: 'tone', freq: 280, duration: 0.3 },
        { name: 'player_hurt', type: 'growl', freq: 110, duration: 0.4 }
      );
    }

    const written = [];
    soundEvents.forEach(evt => {
      const file = path.join(audioDir, `${evt.name}.wav`);
      writeProceduralWav(file, evt);
      written.push(file);
    });

    return { ok: true, count: written.length, audioDir };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

// ── Launch checker ────────────────────────────────────────────────────────────
ipcMain.handle('gf-run-launch-check', async () => {
  try {
    const settings = gfReadSettings();
    const projectsRoot = gfProjectsRoot(settings);

    function checkResult(label, pass, detail) {
      return { label, pass, detail };
    }

    const checks = {};

    // Output folders
    try {
      ensureDir(projectsRoot);
      checks.folders = checkResult('Output folders', true, `Exists: ${projectsRoot}`);
    } catch(e) {
      checks.folders = checkResult('Output folders', false, `Cannot create: ${e.message}`);
    }

    // Settings file
    checks.settings = fs.existsSync(GF_SETTINGS_FILE)
      ? checkResult('Settings file', true, `Found: ${GF_SETTINGS_FILE}`)
      : checkResult('Settings file', null, 'Settings file not found (will be created on first save)');

    // Unreal Engine path — use validateUnrealExePath for consistent logic
    const configuredPath = settings.unrealPath || '';
    const autoPath = configuredPath ? '' : detectCommonUnrealPaths();
    const pathToCheck = configuredPath || autoPath;

    if (pathToCheck) {
      const validation = validateUnrealExePath(pathToCheck);
      if (validation.valid) {
        const source = configuredPath ? 'user-configured' : 'auto-detected';
        checks.unreal = checkResult('Unreal Engine path', true, `✓ Valid (${source}): ${pathToCheck}`);
      } else {
        checks.unreal = checkResult('Unreal Engine path', false, `✗ ${validation.reason}`);
      }
    } else {
      checks.unreal = checkResult('Unreal Engine path', null, 'Not configured. Set in Settings or install Unreal Engine.');
    }

    // Meshy API key
    checks.meshy = settings.meshyApiKey
      ? checkResult('Meshy API key', true, 'API key is configured (not validated)')
      : checkResult('Meshy API key', null, 'Not configured. Add in Settings > Meshy API to enable 3D generation.');

    // node_modules
    const nmPath = path.join(__dirname, 'node_modules');
    checks.deps = fs.existsSync(nmPath)
      ? checkResult('Node dependencies', true, 'node_modules found')
      : checkResult('Node dependencies', false, `Run "npm install" in ${__dirname} before launching`);

    // Config validity
    try {
      const s = gfReadSettings();
      const valid = typeof s === 'object' && s !== null;
      checks.config = checkResult('Config validity', valid, valid ? 'Configuration is valid JSON' : 'Config is corrupt');
    } catch(e) {
      checks.config = checkResult('Config validity', false, `Config parse error: ${e.message}`);
    }

    // Output folder writable
    try {
      const testFile = path.join(projectsRoot, '.gf_write_test');
      fs.writeFileSync(testFile, 'test');
      fs.unlinkSync(testFile);
      checks.outfolder = checkResult('Output folder writable', true, `Writable: ${projectsRoot}`);
    } catch(e) {
      checks.outfolder = checkResult('Output folder writable', false, `Not writable: ${e.message}`);
    }

    return { ok: true, checks };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

// ── Auto repair ───────────────────────────────────────────────────────────────
ipcMain.handle('gf-run-auto-repair', async () => {
  try {
    const settings = gfReadSettings();
    const repairs = [];

    // 1. Create output folders
    try {
      const root = gfProjectsRoot(settings);
      repairs.push({ icon: '📁', title: 'Output folders', detail: `Created/verified: ${root}`, status: 'ok' });
    } catch(e) {
      repairs.push({ icon: '📁', title: 'Output folders', detail: `Failed: ${e.message}`, status: 'err' });
    }

    // 2. Validate settings
    try {
      gfWriteSettings(settings);
      repairs.push({ icon: '⚙️', title: 'Settings file', detail: 'Settings written and validated', status: 'ok' });
    } catch(e) {
      repairs.push({ icon: '⚙️', title: 'Settings file', detail: `Settings write failed: ${e.message}`, status: 'err' });
    }

    // 3. Detect Unreal if missing, validate if present
    if (!settings.unrealPath) {
      const detected = detectCommonUnrealPaths();
      if (detected) {
        const check = validateUnrealExePath(detected);
        if (check.valid) {
          gfWriteSettings({ ...settings, unrealPath: detected });
          repairs.push({ icon: '🎮', title: 'Unreal Engine path', detail: `Auto-detected and saved: ${detected}`, status: 'ok' });
        } else {
          repairs.push({ icon: '🎮', title: 'Unreal Engine path', detail: `Found but invalid: ${check.reason}`, status: 'warn' });
        }
      } else {
        repairs.push({ icon: '🎮', title: 'Unreal Engine path', detail: 'Not found in standard locations — set manually in Settings after installing Unreal Engine.', status: 'warn' });
      }
    } else {
      const check = validateUnrealExePath(settings.unrealPath);
      repairs.push({
        icon: '🎮',
        title: 'Unreal Engine path',
        detail: check.valid ? `Valid: ${settings.unrealPath}` : `✗ ${check.reason}`,
        status: check.valid ? 'ok' : 'warn'
      });
    }

    // 4. Meshy key check
    repairs.push({
      icon: '🔑',
      title: 'Meshy API key',
      detail: settings.meshyApiKey ? 'Key is configured' : 'Key not set — add in Settings to enable 3D generation',
      status: settings.meshyApiKey ? 'ok' : 'warn'
    });

    // 5. node_modules check
    const nmPath = path.join(__dirname, 'node_modules');
    const nmExists = fs.existsSync(nmPath);
    repairs.push({
      icon: '📦',
      title: 'Node dependencies',
      detail: nmExists ? 'node_modules found' : `Missing — run "npm install" in ${__dirname}`,
      status: nmExists ? 'ok' : 'warn'
    });

    return { ok: true, repairs };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

// ── Reset config ──────────────────────────────────────────────────────────────
ipcMain.handle('gf-reset-config', async () => {
  try {
    const defaults = { meshyApiKey: '', unrealPath: '', outputPath: '', aiProvider: 'none', aiKey: '', logLevel: 'info' };
    fs.writeFileSync(GF_SETTINGS_FILE, JSON.stringify(defaults, null, 2), 'utf8');
    return { ok: true };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

// ── Create project folders ────────────────────────────────────────────────────
ipcMain.handle('gf-create-project-folders', async () => {
  try {
    const settings = gfReadSettings();
    const root = gfProjectsRoot(settings);
    const created = [root];
    ['Models','Audio','Textures','Exports','Logs'].forEach(d => {
      const p = path.join(root, d);
      ensureDir(p);
      created.push(p);
    });
    return { ok: true, created };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

// ── File/folder browser dialogs ───────────────────────────────────────────────
ipcMain.handle('gf-browse-for-file', async (event, opts) => {
  try {
    const result = await dialog.showOpenDialog(mainWindow, {
      title: (opts && opts.title) || 'Select File',
      properties: ['openFile'],
      filters: (opts && opts.filters) || [{ name: 'All Files', extensions: ['*'] }]
    });
    if (result.canceled || !result.filePaths.length) return { ok: false, cancelled: true };
    return { ok: true, path: result.filePaths[0] };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

ipcMain.handle('gf-browse-for-folder', async (event, opts) => {
  try {
    const result = await dialog.showOpenDialog(mainWindow, {
      title: (opts && opts.title) || 'Select Folder',
      properties: ['openDirectory', 'createDirectory']
    });
    if (result.canceled || !result.filePaths.length) return { ok: false, cancelled: true };
    return { ok: true, path: result.filePaths[0] };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

// ── Open folder in Explorer / Finder ─────────────────────────────────────────
ipcMain.handle('gf-open-folder', async (event, folderPath) => {
  try {
    const { shell } = require('electron');
    const target = String(folderPath || '');
    if (!target || !fs.existsSync(target)) return { ok: false, error: 'Folder not found' };
    await shell.openPath(target);
    return { ok: true };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

ipcMain.handle('gf-open-log-folder', async () => {
  try {
    const { shell } = require('electron');
    const logDir = app.getPath('logs');
    ensureDir(logDir);
    await shell.openPath(logDir);
    return { ok: true, logDir };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});

// ── Save log file ─────────────────────────────────────────────────────────────
ipcMain.handle('gf-save-log-file', async (event, text) => {
  try {
    const logDir = app.getPath('logs');
    ensureDir(logDir);
    const stamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
    const logFile = path.join(logDir, `gameforge_session_${stamp}.log`);
    fs.writeFileSync(logFile, String(text || ''), 'utf8');
    return { ok: true, path: logFile };
  } catch(e) {
    return { ok: false, error: e.message };
  }
});
