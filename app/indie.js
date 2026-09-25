const fs = require('fs');
const path = require('path');
const { spawn, spawnSync } = require('child_process');

function pythonRuntime(explicit) {
  const candidates = explicit ? [[explicit, []]] : [
    [path.join(process.env.USERPROFILE || '', '.local', 'bin', 'python.exe'), []],
    ['python', []], ['python3', []], ['py', ['-3']]
  ];
  for (const [command, prefix] of candidates) {
    try {
      const result = spawnSync(command, [...prefix, '-c', 'import sys; print(sys.version.split()[0]); sys.exit(0 if sys.version_info >= (3, 10) else 1)'], {encoding:'utf8', timeout:5000, windowsHide:true});
      if (result.status === 0) return {command, prefix, version:result.stdout.trim()};
    } catch (_) {}
  }
  return null;
}

function registerIndie({app, ipcMain, shell, readSettings}) {
  let activeJob = null;
  const historyFile = () => path.join(app.getPath('userData'), 'indie_last_build.json');
  const framework = () => app.isPackaged ? path.join(process.resourcesPath, 'jarvis') : path.join(__dirname, '..', 'jarvis');
  const readLast = () => {try {return JSON.parse(fs.readFileSync(historyFile(), 'utf8'));} catch (_) {return null;}};
  const saveLast = value => {
    fs.mkdirSync(path.dirname(historyFile()), {recursive:true});
    const temp = historyFile() + '.tmp';
    fs.writeFileSync(temp, JSON.stringify(value, null, 2));
    fs.renameSync(temp, historyFile());
  };
  function check() {
    const settings = readSettings();
    const python = pythonRuntime(settings.indiePythonPath);
    let godot = null;
    if (python) {
      const result = spawnSync(python.command, [...python.prefix, '-c', 'import sys; sys.path.insert(0, sys.argv[1]); from indie_builder import find_godot; print(find_godot(sys.argv[2] or None) or "")', framework(), settings.indieGodotPath || ''], {encoding:'utf8', timeout:10000, windowsHide:true});
      if (result.status === 0) godot = result.stdout.trim() || null;
    }
    let goal = null;
    try {goal = JSON.parse(fs.readFileSync(path.join(framework(), 'indie_goal.json'), 'utf8'));} catch (_) {}
    return {ok:!!(python && godot && goal), python, godot, goal, busy:!!activeJob, last:readLast()};
  }
  ipcMain.handle('indie-check', async () => check());
  ipcMain.handle('indie-last-build', async () => {
    const last = readLast();
    if (last && last.report && fs.existsSync(last.report)) {
      try {last.quality = JSON.parse(fs.readFileSync(last.report, 'utf8'));} catch (_) {}
    }
    return last;
  });
  ipcMain.handle('indie-build', async (event, options = {}) => {
    if (activeJob) return {ok:false, error:'A game is already building.'};
    const status = check();
    if (!status.ok) return {ok:false, error:'Python 3.10+ and Godot 4 are required. Use Check setup to locate them.'};
    const title = String(options.title || 'Lanternfall').trim().slice(0, 40);
    const seed = Number(options.seed ?? status.goal.seed);
    if (!Number.isSafeInteger(seed) || Math.abs(seed) > 2147483647) return {ok:false, error:'Use a whole-number seed between -2147483647 and 2147483647.'};
    const settings = readSettings();
    const root = settings.outputPath || path.join(app.getPath('documents'), 'GameForgeProjects');
    const slug = title.replace(/[^a-zA-Z0-9_-]/g, '_').slice(0, 40) || 'IndieGame';
    const output = path.join(root, slug + '_' + Date.now());
    const args = [...status.python.prefix, path.join(framework(), 'main.py'), '--build-indie', '--export', '--output', output,
      '--state-file', path.join(app.getPath('userData'), 'jarvis_indie_state.json'), '--godot', status.godot, '--seed', String(seed), '--title', title];
    if (options.useOllama === true) args.push('--use-ollama');
    return new Promise(resolve => {
      const child = spawn(status.python.command, args, {cwd:framework(), windowsHide:true, env:{...process.env, PYTHONIOENCODING:'utf-8'}});
      activeJob = child;
      let outputText = '';
      let settled = false;
      const finish = value => {if (settled) return; settled=true; clearTimeout(timeout); activeJob=null; resolve(value);};
      const capture = chunk => {
        const message = chunk.toString('utf8');
        outputText = (outputText + message).slice(-60000);
        if (!event.sender.isDestroyed()) event.sender.send('indie-progress', message);
      };
      child.stdout.on('data', capture);
      child.stderr.on('data', capture);
      const timeout = setTimeout(() => {child.kill(); finish({ok:false, error:'Build exceeded ten minutes. See the build log before trying again.'});}, 600000);
      child.on('error', error => finish({ok:false, error:error.message}));
      child.on('close', code => {
        const line = outputText.split(/\r?\n/).findLast(line => line.startsWith('GAMEFORGE_RESULT='));
        let result;
        try {result = line ? JSON.parse(line.slice('GAMEFORGE_RESULT='.length)) : null;} catch (_) {}
        result = result || {ok:false, error:'Build did not complete. ' + outputText.slice(-1800), output};
        result.ok = result.ok === true && code === 0;
        if (result.report && fs.existsSync(result.report)) {
          try {result.quality = JSON.parse(fs.readFileSync(result.report, 'utf8'));} catch (_) {}
        }
        try {saveLast(result);} catch (error) {result.historyWarning = error.message;}
        finish(result);
      });
    });
  });
  ipcMain.handle('indie-play', async () => {
    const last = readLast();
    if (!last || !last.ok || !last.executable || !fs.existsSync(last.executable)) return {ok:false, error:'Build a game that passes its checks first.'};
    const error = await shell.openPath(last.executable);
    return error ? {ok:false, error} : {ok:true};
  });
  ipcMain.handle('indie-open-output', async () => {
    const last = readLast();
    if (!last || !last.output || !fs.existsSync(last.output)) return {ok:false, error:'No generated game folder yet.'};
    const error = await shell.openPath(last.output);
    return error ? {ok:false, error} : {ok:true};
  });
  return {check};
}

module.exports = {registerIndie, pythonRuntime};
