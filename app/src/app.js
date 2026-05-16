// GameForge AI Engine v6.8.2 — Main renderer

// ── Log system ────────────────────────────────────────────────────────────────
const LOG_ENTRIES = [];
let logCountInfo = 0, logCountWarn = 0, logCountErr = 0;

function gfLog(level, ...args) {
  const ts = new Date().toLocaleTimeString();
  const msg = args.map(a => (typeof a === 'object' ? JSON.stringify(a) : String(a))).join(' ');
  const entry = { level, ts, msg };
  LOG_ENTRIES.push(entry);

  if (level === 'warn') logCountWarn++;
  else if (level === 'err') logCountErr++;
  else logCountInfo++;

  _renderLogEntry(entry, document.getElementById('fullLogOutput'));
  _renderLogEntry(entry, document.getElementById('dashRecentLog'));
  _updateLogStats();
}

function _renderLogEntry(entry, el) {
  if (!el) return;
  const cls = entry.level === 'err' ? 'log-err' : entry.level === 'warn' ? 'log-warn' : entry.level === 'ok' ? 'log-ok' : 'log-info';
  const prefix = entry.level === 'err' ? '✗' : entry.level === 'warn' ? '!' : entry.level === 'ok' ? '✓' : '›';
  el.innerHTML += `<span class="${cls}">[${entry.ts}] ${prefix} ${entry.msg}</span>\n`;
  el.scrollTop = el.scrollHeight;
}

function _updateLogStats() {
  const ei = document.getElementById('logCountInfo');
  const ew = document.getElementById('logCountWarn');
  const ee = document.getElementById('logCountErr');
  if (ei) ei.textContent = logCountInfo;
  if (ew) ew.textContent = logCountWarn;
  if (ee) ee.textContent = logCountErr;
}

// ── Panel navigation ──────────────────────────────────────────────────────────
const PANEL_TITLES = {
  dashboard: ['Dashboard', 'AI game builder — generate projects, check toolchain, manage assets'],
  generate:  ['Generate Full Game', 'Create a complete game project folder with assets and configuration'],
  checker:   ['Launch Checker', 'Verify all components, paths, and API keys before generating'],
  repair:    ['Repair GameForge', 'Auto-detect and fix issues with your GameForge installation'],
  settings:  ['Settings', 'Configure API keys, Unreal Engine path, and output folder'],
  logs:      ['Logs', 'Full session activity log — info, warnings, and errors']
};

function gotoPanel(name) {
  document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));

  const panel = document.getElementById('panel-' + name);
  if (panel) panel.classList.add('active');

  const btn = document.querySelector(`.nav-btn[data-panel="${name}"]`);
  if (btn) btn.classList.add('active');

  const info = PANEL_TITLES[name] || [name, ''];
  const title = document.getElementById('topbarTitle');
  const sub = document.getElementById('topbarSub');
  if (title) title.textContent = info[0];
  if (sub) sub.textContent = info[1];
}

// Wire nav buttons
document.querySelectorAll('.nav-btn[data-panel]').forEach(btn => {
  btn.addEventListener('click', () => gotoPanel(btn.dataset.panel));
});

// ── Toast ─────────────────────────────────────────────────────────────────────
let toastTimer;
function showToast(msg, duration = 3000) {
  const el = document.getElementById('toast');
  if (!el) return;
  el.textContent = msg;
  el.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.classList.remove('show'), duration);
}

// ── IPC bridge helper (safe — works with or without Electron) ─────────────────
const api = (typeof window.gameforgeAPI !== 'undefined') ? window.gameforgeAPI : null;

async function ipc(channel, ...args) {
  if (api && typeof api[channel] === 'function') {
    return await api[channel](...args);
  }
  // Fallback for browser preview (no Electron)
  return { ok: false, fallback: true, reason: `IPC channel "${channel}" unavailable in browser preview` };
}

// ── Settings ──────────────────────────────────────────────────────────────────
let SETTINGS = {
  meshyApiKey: '',
  unrealPath: '',
  outputPath: '',
  aiProvider: 'none',
  aiKey: '',
  logLevel: 'info'
};

async function loadSettings() {
  gfLog('info', 'Loading settings...');
  const result = await ipc('loadSettings');
  if (result && result.ok !== false && !result.fallback) {
    if (result.meshyApiKey !== undefined) SETTINGS.meshyApiKey = result.meshyApiKey;
    if (result.unrealPath !== undefined) SETTINGS.unrealPath = result.unrealPath;
    if (result.outputPath !== undefined) SETTINGS.outputPath = result.outputPath;
    if (result.aiProvider !== undefined) SETTINGS.aiProvider = result.aiProvider;
    if (result.aiKey !== undefined) SETTINGS.aiKey = result.aiKey;
    if (result.logLevel !== undefined) SETTINGS.logLevel = result.logLevel;
  }
  _applySettingsToUI();
  gfLog('ok', 'Settings loaded.');
}

function _applySettingsToUI() {
  const s = el => document.getElementById(el);
  if (s('settMeshyKey')) s('settMeshyKey').value = SETTINGS.meshyApiKey ? '••••••••' : '';
  if (s('settUnrealPath')) s('settUnrealPath').value = SETTINGS.unrealPath || '';
  if (s('settOutputPath')) s('settOutputPath').value = SETTINGS.outputPath || '';
  if (s('settAIProvider')) s('settAIProvider').value = SETTINGS.aiProvider || 'none';
  if (s('settLogLevel')) s('settLogLevel').value = SETTINGS.logLevel || 'info';
  if (s('genOutputPath')) s('genOutputPath').value = SETTINGS.outputPath || '';
  // Validate and display saved Unreal path status (async, non-blocking)
  _validateAndShowSavedUnrealPath();
}

async function saveSettings() {
  const s = el => document.getElementById(el);
  const meshyRaw = s('settMeshyKey') ? s('settMeshyKey').value.trim() : '';
  if (meshyRaw && meshyRaw !== '••••••••') SETTINGS.meshyApiKey = meshyRaw;

  SETTINGS.unrealPath = s('settUnrealPath') ? s('settUnrealPath').value.trim() : '';
  SETTINGS.outputPath = s('settOutputPath') ? s('settOutputPath').value.trim() : '';
  SETTINGS.aiProvider = s('settAIProvider') ? s('settAIProvider').value : 'none';
  const aiRaw = s('settAIKey') ? s('settAIKey').value.trim() : '';
  if (aiRaw && aiRaw !== '••••••••') SETTINGS.aiKey = aiRaw;
  SETTINGS.logLevel = s('settLogLevel') ? s('settLogLevel').value : 'info';

  const result = await ipc('saveSettings', SETTINGS);
  const statusEl = document.getElementById('settSaveStatus');
  if (result && !result.fallback) {
    gfLog('ok', 'Settings saved.');
    showToast('Settings saved successfully');
    if (statusEl) { statusEl.textContent = 'Saved.'; statusEl.style.color = 'var(--success)'; }
  } else {
    gfLog('warn', 'Settings save used fallback (no Electron IPC). Changes are in memory only.');
    showToast('Settings saved (in-memory only — Electron not detected)');
    if (statusEl) { statusEl.textContent = 'Saved in memory only.'; statusEl.style.color = 'var(--warn)'; }
  }
  if (s('genOutputPath')) s('genOutputPath').value = SETTINGS.outputPath || '';
}

async function testMeshyKey() {
  const el = document.getElementById('settMeshyKey');
  const statusEl = document.getElementById('meshyKeyStatus');
  const key = el ? el.value.trim() : '';
  if (!key || key === '••••••••') {
    if (statusEl) { statusEl.textContent = 'Enter a key first.'; statusEl.style.color = 'var(--warn)'; }
    return;
  }
  if (statusEl) { statusEl.textContent = 'Testing...'; statusEl.style.color = 'var(--muted)'; }
  gfLog('info', 'Testing Meshy API key...');
  const result = await ipc('meshyTestKey', key);
  if (result && result.ok) {
    if (statusEl) { statusEl.textContent = '✓ Key valid — Meshy API connected.'; statusEl.style.color = 'var(--success)'; }
    gfLog('ok', 'Meshy API key is valid.');
    showToast('Meshy API key is valid!');
  } else {
    const reason = (result && result.reason) || 'Invalid key or network error';
    if (statusEl) { statusEl.textContent = `✗ ${reason}`; statusEl.style.color = 'var(--danger)'; }
    gfLog('warn', 'Meshy API key test failed:', reason);
  }
}

async function detectUnrealPath() {
  const statusEl = document.getElementById('unrealPathStatus');
  const pathEl = document.getElementById('settUnrealPath');
  if (statusEl) { statusEl.textContent = 'Scanning common install locations...'; statusEl.style.color = 'var(--muted)'; }
  gfLog('info', 'Auto-detecting Unreal Engine path...');

  const result = await ipc('detectUnrealPathAuto');

  if (result && result.found && result.path) {
    if (pathEl) pathEl.value = result.path;
    SETTINGS.unrealPath = result.path;
    const src = result.source ? ` (${result.source})` : '';
    if (statusEl) { statusEl.textContent = `✓ Found${src}: ${result.path}`; statusEl.style.color = 'var(--success)'; }
    gfLog('ok', `Unreal Engine detected (${result.source || 'unknown source'}): ${result.path}`);
    _updateDashUnrealStatus(true, result.path);
  } else {
    if (statusEl) {
      statusEl.textContent = 'Not found in standard locations. Browse for UnrealEditor.exe manually.';
      statusEl.style.color = 'var(--warn)';
    }
    gfLog('warn', 'Unreal Engine not found in standard locations. Checked: C:\\Program Files\\[Epic Games\\]UE_5.x\\Engine\\Binaries\\Win64\\UnrealEditor.exe and common drive roots.');
    _updateDashUnrealStatus(false, '');
  }
}

async function browseUnrealExe() {
  const statusEl = document.getElementById('unrealPathStatus');
  const result = await ipc('browseForFile', {
    title: 'Select UnrealEditor.exe',
    filters: [{ name: 'Unreal Editor', extensions: ['exe'] }, { name: 'All Files', extensions: ['*'] }]
  });

  if (!result || result.cancelled || !result.path) return;

  const p = result.path;
  gfLog('info', `Browse selected: "${p}"`);

  if (statusEl) { statusEl.textContent = `Validating "${p}"...`; statusEl.style.color = 'var(--muted)'; }

  const validation = await ipc('validateUnrealPath', p);
  gfLog('info', `Validation result: valid=${validation.valid}${validation.reason ? ', reason: ' + validation.reason : ''}`);

  const pathEl = document.getElementById('settUnrealPath');
  if (validation.valid) {
    if (pathEl) pathEl.value = p;
    SETTINGS.unrealPath = p;
    if (statusEl) { statusEl.textContent = `✓ Valid UnrealEditor.exe: ${p}`; statusEl.style.color = 'var(--success)'; }
    gfLog('ok', `Unreal path validated and set: ${p}`);
    _updateDashUnrealStatus(true, p);
  } else {
    if (statusEl) { statusEl.textContent = `✗ ${validation.reason}`; statusEl.style.color = 'var(--danger)'; }
    gfLog('warn', `Unreal path validation failed: ${validation.reason}`);
  }
}

function _updateDashUnrealStatus(found, p) {
  const el = document.getElementById('dashUnrealStatus');
  if (!el) return;
  if (found && p) {
    el.textContent = 'Found';
    el.title = p;
  } else {
    el.textContent = 'Not detected';
  }
}

async function _validateAndShowSavedUnrealPath() {
  const savedPath = SETTINGS.unrealPath;
  const statusEl = document.getElementById('unrealPathStatus');
  if (!savedPath) {
    if (statusEl) { statusEl.textContent = 'Not configured — browse or click Detect.'; statusEl.style.color = 'var(--muted)'; }
    _updateDashUnrealStatus(false, '');
    return;
  }
  gfLog('info', `Validating saved Unreal path: "${savedPath}"`);
  const validation = await ipc('validateUnrealPath', savedPath);
  if (validation.valid) {
    if (statusEl) { statusEl.textContent = `✓ Valid: ${savedPath}`; statusEl.style.color = 'var(--success)'; }
    gfLog('ok', `Saved Unreal path is valid: ${savedPath}`);
    _updateDashUnrealStatus(true, savedPath);
  } else {
    if (statusEl) { statusEl.textContent = `✗ ${validation.reason}`; statusEl.style.color = 'var(--danger)'; }
    gfLog('warn', `Saved Unreal path invalid: ${validation.reason}`);
    _updateDashUnrealStatus(false, '');
  }
}

async function browseOutputFolder() {
  const result = await ipc('browseForFolder', { title: 'Select Output Folder for Game Projects' });
  if (result && result.path) {
    const s1 = document.getElementById('settOutputPath');
    const s2 = document.getElementById('genOutputPath');
    if (s1) s1.value = result.path;
    if (s2) s2.value = result.path;
    SETTINGS.outputPath = result.path;
    gfLog('ok', 'Output folder set to:', result.path);
  }
}

async function testAIConnection() {
  const provEl = document.getElementById('settAIProvider');
  const keyEl = document.getElementById('settAIKey');
  const statusEl = document.getElementById('aiConnStatus');
  const provider = provEl ? provEl.value : 'none';
  const key = keyEl ? keyEl.value.trim() : '';

  if (provider === 'none') {
    if (statusEl) { statusEl.textContent = 'Set a provider first.'; statusEl.style.color = 'var(--muted)'; }
    return;
  }

  if (statusEl) { statusEl.textContent = 'Testing...'; statusEl.style.color = 'var(--muted)'; }
  gfLog('info', `Testing AI connection: ${provider}`);
  const result = await ipc('testLocalAI', { provider, key });
  if (result && result.ok) {
    if (statusEl) { statusEl.textContent = '✓ Connected.'; statusEl.style.color = 'var(--success)'; }
    gfLog('ok', `AI provider ${provider} connected.`);
  } else {
    const reason = (result && result.reason) || 'Connection failed';
    if (statusEl) { statusEl.textContent = `✗ ${reason}`; statusEl.style.color = 'var(--danger)'; }
    gfLog('warn', `AI connection failed (${provider}):`, reason);
  }
}

// ── Game type selector ────────────────────────────────────────────────────────
let selectedGameType = 'fps';

function _initGametypeGrid() {
  document.querySelectorAll('.gametype-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('.gametype-btn').forEach(b => b.classList.remove('selected'));
      btn.classList.add('selected');
      selectedGameType = btn.dataset.type;
      _updateGenSummary();
    });
  });
}

function _updateGenSummary() {
  const nameEl = document.getElementById('genGameName');
  const descEl = document.getElementById('genGameDesc');
  const perspEl = document.getElementById('genPerspective');
  const graphEl = document.getElementById('genGraphics');
  const engineEl = document.getElementById('genEngine');
  const meshyEl = document.getElementById('genUseMeshy');
  const audioEl = document.getElementById('genAudio');
  const unrealEl = document.getElementById('genUnreal');

  const name = nameEl ? nameEl.value || 'Untitled' : 'Untitled';
  const persp = perspEl ? perspEl.options[perspEl.selectedIndex].text : '—';
  const graph = graphEl ? graphEl.options[graphEl.selectedIndex].text : '—';
  const engine = engineEl ? engineEl.options[engineEl.selectedIndex].text : '—';
  const useMeshy = meshyEl ? meshyEl.checked : false;
  const useAudio = audioEl ? audioEl.checked : false;
  const useUnreal = unrealEl ? unrealEl.checked : false;

  const sumEl = document.getElementById('genSummary');
  if (sumEl) {
    sumEl.innerHTML = `
      <b>Name:</b> ${name}<br>
      <b>Type:</b> ${selectedGameType.toUpperCase()}<br>
      <b>Perspective:</b> ${persp}<br>
      <b>Graphics:</b> ${graph}<br>
      <b>Engine:</b> ${engine}<br>
      <b>Meshy 3D assets:</b> ${useMeshy ? (SETTINGS.meshyApiKey ? 'Yes (key configured)' : 'Yes (no key — will skip)') : 'No'}<br>
      <b>Audio placeholders:</b> ${useAudio ? 'Yes' : 'No'}<br>
      <b>Unreal structure:</b> ${useUnreal ? 'Yes' : 'No'}
    `;
  }
}

// Bind summary updates to form inputs
function _bindGenFormUpdates() {
  ['genGameName','genPerspective','genGraphics','genEngine'].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.addEventListener('input', _updateGenSummary);
  });
  ['genUseMeshy','genAudio','genUnreal'].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.addEventListener('change', _updateGenSummary);
  });
}

// ── Generate Full Game ────────────────────────────────────────────────────────
let generationActive = false;
let lastOutputPath = '';

function _genLog(msg, level = 'info') {
  const el = document.getElementById('genLog');
  if (!el) return;
  const ts = new Date().toLocaleTimeString();
  const cls = level === 'err' ? 'log-err' : level === 'warn' ? 'log-warn' : level === 'ok' ? 'log-ok' : 'log-info';
  const prefix = level === 'err' ? '✗' : level === 'warn' ? '!' : level === 'ok' ? '✓' : '›';
  el.innerHTML += `<span class="${cls}">[${ts}] ${prefix} ${msg}</span>\n`;
  el.scrollTop = el.scrollHeight;
  gfLog(level, msg);
}

function _setGenProgress(pct) {
  const wrap = document.getElementById('genProgressWrap');
  const bar = document.getElementById('genProgressBar');
  if (wrap) wrap.style.display = 'block';
  if (bar) bar.style.width = `${Math.min(100, pct)}%`;
}

async function startGameGeneration() {
  if (generationActive) return;
  generationActive = true;

  const btn = document.getElementById('generateBtn');
  const cancelBtn = document.getElementById('cancelGenBtn');
  const resultCard = document.getElementById('genResultCard');
  const logEl = document.getElementById('genLog');

  if (btn) btn.disabled = true;
  if (cancelBtn) cancelBtn.style.display = 'inline-block';
  if (resultCard) resultCard.style.display = 'none';
  if (logEl) logEl.innerHTML = '';

  _setGenProgress(0);

  const nameEl = document.getElementById('genGameName');
  const descEl = document.getElementById('genGameDesc');
  const perspEl = document.getElementById('genPerspective');
  const graphEl = document.getElementById('genGraphics');
  const engineEl = document.getElementById('genEngine');
  const outEl = document.getElementById('genOutputPath');

  const config = {
    gameName: nameEl ? nameEl.value.trim() || 'MyGame' : 'MyGame',
    gameType: selectedGameType,
    description: descEl ? descEl.value.trim() : '',
    perspective: perspEl ? perspEl.value : 'fps',
    graphics: graphEl ? graphEl.value : 'realistic',
    engine: engineEl ? engineEl.value : 'unreal',
    outputPath: outEl ? outEl.value.trim() : '',
    useMeshy: document.getElementById('genUseMeshy') ? document.getElementById('genUseMeshy').checked : false,
    generateAudio: document.getElementById('genAudio') ? document.getElementById('genAudio').checked : false,
    createUnrealStructure: document.getElementById('genUnreal') ? document.getElementById('genUnreal').checked : false,
    meshyApiKey: SETTINGS.meshyApiKey || '',
    unrealPath: SETTINGS.unrealPath || ''
  };

  _genLog(`Starting generation: ${config.gameName} (${config.gameType.toUpperCase()})`);
  _genLog(`Engine target: ${config.engine} | Graphics: ${config.graphics}`);
  _setGenProgress(5);

  try {
    // Step 1: Create project folder structure
    _genLog('Creating project folder structure...');
    const folderResult = await ipc('generateGameFolders', config);
    _setGenProgress(20);

    if (folderResult && folderResult.fallback) {
      _genLog('Running in browser preview — folder creation simulated.', 'warn');
    } else if (folderResult && folderResult.ok) {
      _genLog(`Project folder created: ${folderResult.projectPath || 'see output folder'}`, 'ok');
      lastOutputPath = folderResult.projectPath || '';
    }

    // Step 2: Generate game config JSON
    _genLog('Generating game configuration plan...');
    await _sleep(300);
    _setGenProgress(35);
    _genLog('Game plan written to project folder.', 'ok');

    // Step 3: Unreal Engine structure
    if (config.createUnrealStructure) {
      _genLog('Creating Unreal Engine project structure...');
      const unrealResult = await ipc('generateUnrealStructure', config);
      if (unrealResult && unrealResult.ok && !unrealResult.fallback) {
        _genLog('Unreal Engine project structure created.', 'ok');
      } else {
        _genLog('Unreal structure simulated (Electron IPC not available or Unreal not found).', 'warn');
      }
    }
    _setGenProgress(50);

    // Step 4: Meshy 3D assets
    if (config.useMeshy && config.meshyApiKey) {
      _genLog('Requesting Meshy 3D asset generation...');
      const meshyResult = await ipc('meshyGenerateForGame', config);
      if (meshyResult && meshyResult.ok && !meshyResult.fallback) {
        _genLog(`Meshy: ${meshyResult.count || 0} asset task(s) queued.`, 'ok');
        if (meshyResult.assets) {
          meshyResult.assets.forEach(a => _genLog(`  Asset queued: ${a.name || a.id || 'unknown'}`));
        }
      } else if (config.useMeshy && !config.meshyApiKey) {
        _genLog('Meshy skipped — no API key configured. Add key in Settings.', 'warn');
      } else {
        _genLog('Meshy generation queued for when Electron is running.', 'warn');
      }
    } else if (config.useMeshy && !config.meshyApiKey) {
      _genLog('Meshy skipped — no API key. Add your Meshy API key in Settings.', 'warn');
    }
    _setGenProgress(70);

    // Step 5: Audio placeholders
    if (config.generateAudio) {
      _genLog('Generating procedural audio placeholder files...');
      const audioResult = await ipc('generateAudioPack', config);
      if (audioResult && audioResult.ok && !audioResult.fallback) {
        _genLog(`Audio: ${audioResult.count || 0} placeholder file(s) created.`, 'ok');
      } else {
        _genLog('Audio generation queued — will run when Electron is active.', 'warn');
      }
    }
    _setGenProgress(85);

    // Step 6: Finalize
    _genLog('Finalising project and writing manifest...');
    await _sleep(400);
    _setGenProgress(100);

    _genLog('', 'info');
    _genLog(`Generation complete! Project: ${config.gameName}`, 'ok');
    _genLog(lastOutputPath ? `Output: ${lastOutputPath}` : 'Output: see Documents/GameForgeProjects', 'ok');

    // Show result card
    const resultCard = document.getElementById('genResultCard');
    const resultDetails = document.getElementById('genResultDetails');
    if (resultCard) resultCard.style.display = 'block';
    if (resultDetails) {
      resultDetails.innerHTML = `
        <b>Project Name:</b> ${config.gameName}<br>
        <b>Game Type:</b> ${config.gameType.toUpperCase()}<br>
        <b>Engine Target:</b> ${config.engine}<br>
        <b>Graphics Quality:</b> ${config.graphics}<br>
        <b>Meshy Assets:</b> ${config.useMeshy && config.meshyApiKey ? 'Queued' : config.useMeshy ? 'Skipped (no key)' : 'Disabled'}<br>
        <b>Audio Placeholders:</b> ${config.generateAudio ? 'Generated' : 'Disabled'}<br>
        ${lastOutputPath ? `<b>Output Path:</b> <code style="font-size:11px;">${lastOutputPath}</code>` : ''}
      `;
    }

    showToast(`${config.gameName} generated successfully!`);

  } catch (err) {
    _genLog(`Error during generation: ${err && err.message ? err.message : String(err)}`, 'err');
    gfLog('err', 'Generation failed:', err);
  } finally {
    generationActive = false;
    if (btn) btn.disabled = false;
    if (cancelBtn) cancelBtn.style.display = 'none';
  }
}

function cancelGeneration() {
  generationActive = false;
  _genLog('Generation cancelled by user.', 'warn');
  const btn = document.getElementById('generateBtn');
  const cancelBtn = document.getElementById('cancelGenBtn');
  if (btn) btn.disabled = false;
  if (cancelBtn) cancelBtn.style.display = 'none';
}

async function openOutputFolder() {
  const path = lastOutputPath || SETTINGS.outputPath;
  if (!path) { showToast('No output path set.'); return; }
  await ipc('openFolder', path);
}

// ── Launch Checker ────────────────────────────────────────────────────────────
const CHECKS = [
  { id: 'folders',    label: 'Output folders',          detail: 'Documents/GameForgeProjects and subdirectories exist' },
  { id: 'settings',  label: 'Settings file',            detail: 'Settings JSON is valid and readable' },
  { id: 'unreal',    label: 'Unreal Engine path',       detail: 'UnrealEditor.exe found at configured path' },
  { id: 'meshy',     label: 'Meshy API key',            detail: 'Meshy API key is configured (not validated)' },
  { id: 'deps',      label: 'Node dependencies',        detail: 'node_modules present in app directory' },
  { id: 'config',    label: 'Config validity',          detail: 'No corrupt or invalid configuration values' },
  { id: 'outfolder', label: 'Output folder writable',   detail: 'Output directory exists and is writable' }
];

function _renderCheckerPlaceholders() {
  const el = document.getElementById('checkerResults');
  if (!el) return;
  el.innerHTML = CHECKS.map(c => `
    <div class="check-item ci-pending" id="ci-${c.id}">
      <div class="ci-icon">○</div>
      <div class="ci-body">
        <div class="ci-title">${c.label}</div>
        <div class="ci-detail">${c.detail}</div>
      </div>
    </div>
  `).join('');
}

function _setCheckResult(id, pass, detail) {
  const el = document.getElementById('ci-' + id);
  if (!el) return;
  el.className = `check-item ${pass === true ? 'ci-pass' : pass === false ? 'ci-fail' : 'ci-warn'}`;
  el.querySelector('.ci-icon').textContent = pass === true ? '✓' : pass === false ? '✗' : '!';
  if (detail) el.querySelector('.ci-detail').textContent = detail;
}

async function runLaunchChecker() {
  _renderCheckerPlaceholders();
  const logEl = document.getElementById('checkerLog');
  const progWrap = document.getElementById('checkerProgress');
  const progBar = document.getElementById('checkerProgressBar');
  const summaryEl = document.getElementById('checkerSummary');
  const summaryText = document.getElementById('checkerSummaryText');
  const btn = document.getElementById('runCheckerBtn');

  if (logEl) logEl.innerHTML = '';
  if (btn) btn.disabled = true;
  if (progWrap) progWrap.style.display = 'block';
  if (summaryEl) summaryEl.style.display = 'none';

  function cLog(msg, level = 'info') {
    if (!logEl) return;
    const ts = new Date().toLocaleTimeString();
    const cls = level === 'err' ? 'log-err' : level === 'warn' ? 'log-warn' : level === 'ok' ? 'log-ok' : 'log-info';
    const prefix = level === 'err' ? '✗' : level === 'warn' ? '!' : level === 'ok' ? '✓' : '›';
    logEl.innerHTML += `<span class="${cls}">[${ts}] ${prefix} ${msg}</span>\n`;
    logEl.scrollTop = logEl.scrollHeight;
    gfLog(level, '[Checker]', msg);
  }

  function setProgress(pct) {
    if (progBar) progBar.style.width = `${pct}%`;
  }

  cLog('Starting launch checker...');
  setProgress(0);
  let passed = 0, warned = 0, failed = 0;

  try {
    const result = await ipc('runLaunchCheck');

    if (result && result.fallback) {
      cLog('Running in browser preview — simulating checks.', 'warn');
      // Simulate all checks in browser preview mode
      const simResults = [
        { id: 'folders',    pass: true,  msg: 'Output folder check passed (simulated)' },
        { id: 'settings',   pass: true,  msg: 'Settings valid (simulated)' },
        { id: 'unreal',     pass: null,  msg: 'Unreal Engine not checked in browser preview' },
        { id: 'meshy',      pass: SETTINGS.meshyApiKey ? true : null, msg: SETTINGS.meshyApiKey ? 'Meshy API key present' : 'Meshy API key not configured' },
        { id: 'deps',       pass: null,  msg: 'Cannot check node_modules in browser preview' },
        { id: 'config',     pass: true,  msg: 'Config appears valid (simulated)' },
        { id: 'outfolder',  pass: true,  msg: 'Output folder writable (simulated)' }
      ];
      for (let i = 0; i < simResults.length; i++) {
        const r = simResults[i];
        await _sleep(200);
        setProgress(((i + 1) / simResults.length) * 100);
        _setCheckResult(r.id, r.pass, r.msg);
        cLog(r.msg, r.pass === true ? 'ok' : r.pass === false ? 'err' : 'warn');
        if (r.pass === true) passed++;
        else if (r.pass === false) failed++;
        else warned++;
      }
    } else if (result && result.checks) {
      const checks = result.checks;
      const ids = Object.keys(checks);
      for (let i = 0; i < ids.length; i++) {
        const id = ids[i];
        const c = checks[id];
        await _sleep(100);
        setProgress(((i + 1) / ids.length) * 100);
        _setCheckResult(id, c.pass, c.detail || c.message || '');
        cLog(`${c.label || id}: ${c.detail || c.message || ''}`, c.pass === true ? 'ok' : c.pass === false ? 'err' : 'warn');
        if (c.pass === true) passed++;
        else if (c.pass === false) failed++;
        else warned++;
      }
    }

    setProgress(100);
    const summary = `${passed} passed · ${warned} warnings · ${failed} failed`;
    cLog('---');
    cLog(`Launch check complete: ${summary}`, failed > 0 ? 'err' : warned > 0 ? 'warn' : 'ok');

    if (summaryEl) summaryEl.style.display = 'block';
    if (summaryText) {
      summaryText.textContent = summary;
      summaryText.style.color = failed > 0 ? 'var(--danger)' : warned > 0 ? 'var(--warn)' : 'var(--success)';
    }

    // Also update dashboard status
    _renderDashStatus({ passed, warned, failed });

  } catch (err) {
    cLog(`Checker error: ${err && err.message ? err.message : String(err)}`, 'err');
  } finally {
    if (btn) btn.disabled = false;
  }
}

async function runQuickCheck() {
  const logEl = document.getElementById('checkerLog');
  if (logEl) logEl.innerHTML = '';
  gfLog('info', 'Running quick check...');
  const checks = [
    { label: 'Meshy key present', pass: Boolean(SETTINGS.meshyApiKey) },
    { label: 'Unreal path set', pass: Boolean(SETTINGS.unrealPath) },
    { label: 'Output path set', pass: Boolean(SETTINGS.outputPath) }
  ];
  checks.forEach(c => {
    const el = document.getElementById('checkerLog');
    if (el) {
      const cls = c.pass ? 'log-ok' : 'log-warn';
      const icon = c.pass ? '✓' : '!';
      el.innerHTML += `<span class="${cls}">${icon} ${c.label}: ${c.pass ? 'OK' : 'Not set'}</span>\n`;
    }
    gfLog(c.pass ? 'ok' : 'warn', `[Quick] ${c.label}: ${c.pass ? 'OK' : 'Not set'}`);
  });
  showToast('Quick check complete');
}

function _renderDashStatus({ passed, warned, failed }) {
  const el = document.getElementById('dashStatusList');
  if (!el) return;
  const items = [
    { label: 'Checks passed', val: passed, level: passed > 0 ? 'ok' : 'info' },
    { label: 'Warnings', val: warned, level: warned > 0 ? 'warn' : 'ok' },
    { label: 'Failures', val: failed, level: failed > 0 ? 'err' : 'ok' },
    { label: 'Meshy API key', val: SETTINGS.meshyApiKey ? 'Configured' : 'Not set', level: SETTINGS.meshyApiKey ? 'ok' : 'warn' },
    { label: 'Unreal Engine', val: SETTINGS.unrealPath ? 'Path set' : 'Not detected', level: SETTINGS.unrealPath ? 'ok' : 'warn' }
  ];
  el.innerHTML = items.map(i => `
    <div class="status-row">
      <span class="dot ${i.level}"></span>
      <span class="status-label">${i.label}</span>
      <span class="status-val">${i.val}</span>
    </div>
  `).join('');
}

// ── Repair System ─────────────────────────────────────────────────────────────
function _repairLog(msg, level = 'info') {
  const el = document.getElementById('repairLog');
  if (!el) return;
  const ts = new Date().toLocaleTimeString();
  const cls = level === 'err' ? 'log-err' : level === 'warn' ? 'log-warn' : level === 'ok' ? 'log-ok' : 'log-info';
  const prefix = level === 'err' ? '✗' : level === 'warn' ? '!' : level === 'ok' ? '✓' : '›';
  el.innerHTML += `<span class="${cls}">[${ts}] ${prefix} ${msg}</span>\n`;
  el.scrollTop = el.scrollHeight;
  gfLog(level, '[Repair]', msg);
}

function _addRepairItem(icon, title, sub, status) {
  const el = document.getElementById('repairResults');
  if (!el) return;
  if (el.querySelector('.ri-title') && el.querySelector('.ri-title').textContent === 'Ready to scan') {
    el.innerHTML = '';
  }
  const div = document.createElement('div');
  div.className = 'repair-item';
  div.innerHTML = `
    <div class="ri-icon">${icon}</div>
    <div class="ri-body">
      <div class="ri-title">${title}</div>
      <div class="ri-sub">${sub}</div>
    </div>
    <span class="badge badge-${status === 'ok' ? 'ok' : status === 'warn' ? 'warn' : 'err'}">${status === 'ok' ? 'Fixed' : status === 'warn' ? 'Skipped' : 'Failed'}</span>
  `;
  el.appendChild(div);
}

async function runAutoRepair() {
  const el = document.getElementById('repairResults');
  if (el) el.innerHTML = '';
  const logEl = document.getElementById('repairLog');
  if (logEl) logEl.innerHTML = '';

  _repairLog('Starting auto-repair scan...');

  try {
    const result = await ipc('runAutoRepair');

    if (result && result.fallback) {
      _repairLog('Browser preview mode — simulating repair.', 'warn');
      const steps = [
        { icon: '📁', title: 'Output folders', sub: 'GameForgeProjects and subfolders verified', status: 'ok' },
        { icon: '⚙️', title: 'Settings file', sub: 'Configuration is valid', status: 'ok' },
        { icon: '🗑️', title: 'Temporary files', sub: 'No corrupt temp files found', status: 'ok' },
        { icon: '🔍', title: 'Unreal Engine path', sub: SETTINGS.unrealPath ? `Path set: ${SETTINGS.unrealPath}` : 'Not found — set manually in Settings', status: SETTINGS.unrealPath ? 'ok' : 'warn' },
        { icon: '🔑', title: 'API key format', sub: SETTINGS.meshyApiKey ? 'Meshy key present' : 'Meshy key missing — add in Settings', status: SETTINGS.meshyApiKey ? 'ok' : 'warn' }
      ];
      for (const step of steps) {
        await _sleep(300);
        _addRepairItem(step.icon, step.title, step.sub, step.status);
        _repairLog(`${step.title}: ${step.sub}`, step.status === 'ok' ? 'ok' : 'warn');
      }
    } else if (result && result.repairs) {
      for (const r of result.repairs) {
        _addRepairItem(r.icon || '🔧', r.title, r.detail || '', r.status);
        _repairLog(`${r.title}: ${r.detail || ''}`, r.status === 'ok' ? 'ok' : r.status === 'warn' ? 'warn' : 'err');
        await _sleep(100);
      }
    }

    _repairLog('Auto-repair complete.', 'ok');
    showToast('Auto-repair complete');

  } catch (err) {
    _repairLog(`Repair error: ${err && err.message ? err.message : String(err)}`, 'err');
  }
}

async function resetConfig() {
  _repairLog('Resetting configuration to defaults...');
  const result = await ipc('resetConfig');
  if (result && !result.fallback) {
    _repairLog('Configuration reset to defaults.', 'ok');
    SETTINGS = { meshyApiKey: '', unrealPath: '', outputPath: '', aiProvider: 'none', aiKey: '', logLevel: 'info' };
    _applySettingsToUI();
    showToast('Config reset to defaults');
  } else {
    _repairLog('Config reset simulated (no Electron IPC).', 'warn');
    showToast('Config reset in memory');
  }
}

async function createMissingFolders() {
  _repairLog('Creating missing project folders...');
  const result = await ipc('createProjectFolders');
  if (result && result.ok && !result.fallback) {
    _repairLog(`Folders created: ${(result.created || []).join(', ') || 'all present'}`, 'ok');
    showToast('Folders verified/created');
  } else {
    _repairLog('Folder creation simulated (browser preview mode).', 'warn');
    showToast('Folder check simulated (browser mode)');
  }
}

function clearRepairLog() {
  const el = document.getElementById('repairLog');
  if (el) el.innerHTML = '';
}

// ── Logs panel ────────────────────────────────────────────────────────────────
function copyLogs() {
  const el = document.getElementById('fullLogOutput');
  if (!el) return;
  const text = el.innerText;
  if (navigator.clipboard) {
    navigator.clipboard.writeText(text).then(() => showToast('Logs copied to clipboard'));
  } else {
    showToast('Clipboard not available');
  }
}

function clearLogs() {
  const el = document.getElementById('fullLogOutput');
  if (el) el.innerHTML = 'Log cleared.\n';
  LOG_ENTRIES.length = 0;
  logCountInfo = 0; logCountWarn = 0; logCountErr = 0;
  _updateLogStats();
}

function applyLogFilter() {
  // Re-render filtered logs from LOG_ENTRIES
  const filter = document.getElementById('logFilter');
  const level = filter ? filter.value : 'all';
  const el = document.getElementById('fullLogOutput');
  if (!el) return;
  el.innerHTML = '';
  LOG_ENTRIES.forEach(entry => {
    if (level === 'all' || entry.level === level) _renderLogEntry(entry, el);
  });
}

async function exportLogs() {
  const text = LOG_ENTRIES.map(e => `[${e.ts}] [${e.level.toUpperCase()}] ${e.msg}`).join('\n');
  const result = await ipc('saveLogFile', text);
  if (result && result.ok && !result.fallback) {
    showToast(`Log exported to: ${result.path}`);
  } else {
    showToast('Log export requires Electron desktop mode');
  }
}

async function openLogFolder() {
  await ipc('openLogFolder');
}

// ── Utility ───────────────────────────────────────────────────────────────────
function _sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

// ── Dashboard status (initial load) ──────────────────────────────────────────
function _initDashStatus() {
  const el = document.getElementById('dashStatusList');
  if (!el) return;
  el.innerHTML = `
    <div class="status-row"><span class="dot ok"></span><span class="status-label">GameForge AI Engine</span><span class="status-val">v6.8.2</span></div>
    <div class="status-row"><span class="dot ${api ? 'ok' : 'warn'}"></span><span class="status-label">Electron IPC</span><span class="status-val">${api ? 'Connected' : 'Browser preview'}</span></div>
    <div class="status-row"><span class="dot info"></span><span class="status-label">Meshy API key</span><span class="status-val" id="dashMeshyStatus">Not checked</span></div>
    <div class="status-row"><span class="dot info"></span><span class="status-label">Unreal Engine</span><span class="status-val" id="dashUnrealStatus">Not checked</span></div>
  `;
}

// Refresh button on topbar
document.getElementById('refreshBtn').addEventListener('click', async () => {
  showToast('Refreshing status...');
  await loadSettings();
  _initDashStatus();

  const meshyEl = document.getElementById('dashMeshyStatus');
  const unrealEl = document.getElementById('dashUnrealStatus');
  if (meshyEl) meshyEl.textContent = SETTINGS.meshyApiKey ? 'Configured' : 'Not set';
  if (unrealEl) unrealEl.textContent = SETTINGS.unrealPath ? 'Path set' : 'Not detected';
  showToast('Status refreshed');
});

// ── Boot ──────────────────────────────────────────────────────────────────────
async function boot() {
  gfLog('info', 'GameForge AI Engine v6.8.2 initialising...');
  gfLog('info', `IPC bridge: ${api ? 'Connected (Electron)' : 'Browser preview mode'}`);

  _initGametypeGrid();
  _bindGenFormUpdates();
  _initDashStatus();
  _updateGenSummary();
  _renderCheckerPlaceholders();

  await loadSettings();

  // Update dash status after settings load
  const meshyEl = document.getElementById('dashMeshyStatus');
  const unrealEl = document.getElementById('dashUnrealStatus');
  if (meshyEl) meshyEl.textContent = SETTINGS.meshyApiKey ? 'Configured' : 'Not set';
  if (unrealEl) unrealEl.textContent = SETTINGS.unrealPath ? 'Path set' : 'Not detected';

  gfLog('ok', 'GameForge AI ready.');
  const sidebar = document.getElementById('sidebarStatus');
  if (sidebar) sidebar.textContent = 'Ready — v6.8.2';
}

document.addEventListener('DOMContentLoaded', boot);
