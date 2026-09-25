(() => {
  const bridge = window.gameforgeAPI;
  const byId = id => document.getElementById(id);
  let building = false;
  const describe = (heading, message, failed = false) => {
    const box = byId('indieResult');
    box.replaceChildren();
    box.classList.toggle('failed', failed);
    const title = document.createElement('strong'); title.textContent = heading;
    const body = document.createElement('p'); body.textContent = message;
    box.append(title, body);
  };
  const showResult = result => {
    if (!result) return;
    const assertions = result.quality?.checks?.find(c => c.name === 'gameplay_tests')?.assertions || [];
    const count = assertions.filter(c => c.passed).length;
    if (result.ok) {
      describe('Your game is ready to playtest', `${result.title} passed engine checks and ${count} gameplay tests. The Windows build is ready. Play it and judge the controls, challenge and visual feel.`);
    } else {
      const failedChecks = result.quality?.checks?.filter(c => !c.passed).map(c => c.name).join(', ');
      describe('Build needs attention', result.error || `The quality gate blocked this build${failedChecks ? ': ' + failedChecks : ''}. Expand Build activity for details.`, true);
    }
    byId('indiePlayBtn').disabled = !result.ok || !result.executable;
    byId('indieFolderBtn').disabled = !result.output;
  };
  async function check() {
    if (!bridge?.indieCheck) {
      byId('indieSetup').textContent = 'Open the desktop app to build and play games.';
      byId('indieBuildBtn').disabled = true;
      return;
    }
    byId('indieSetup').textContent = 'Checking setup…';
    try {
      const status = await bridge.indieCheck();
      byId('indieSetup').textContent = `${status.python ? 'Python ready' : 'Python 3.10+ missing'} · ${status.godot ? 'Godot found' : 'Godot 4 missing'}. ${status.ok ? 'Original local assets. No paid services required.' : 'Set the missing tool location below.'}`;
      byId('indieBuildBtn').disabled = !status.ok || building || status.busy;
      if (!building) showResult(await bridge.indieLastBuild());
    } catch (error) {
      byId('indieSetup').textContent = error.message;
      byId('indieBuildBtn').disabled = true;
    }
  }
  byId('indieCheckBtn').addEventListener('click', check);
  byId('indieSaveTools').addEventListener('click', async () => {
    if (!bridge) return;
    await bridge.saveSettings({indiePythonPath:byId('indiePython').value.trim(), indieGodotPath:byId('indieGodot').value.trim()});
    await check();
  });
  byId('indieBuildBtn').addEventListener('click', async () => {
    if (building || !bridge) return;
    if (!byId('indieSeed').reportValidity()) return;
    building = true;
    byId('indieBuildBtn').disabled = true;
    byId('indieBuildBtn').textContent = 'Building and testing…';
    byId('indiePlayBtn').disabled = true;
    byId('indieBuildLog').textContent = '';
    describe('Building your adventure', 'Creating chapters, generating original audio, checking gameplay and exporting the result. This usually takes a minute or two.');
    try {
      showResult(await bridge.indieBuild({title:byId('indieTitle').value, seed:Number(byId('indieSeed').value), useOllama:byId('indieOllama').checked}));
    } catch (error) {
      describe('Build needs attention', error.message, true);
    } finally {
      building = false;
      byId('indieBuildBtn').textContent = 'Build & test game';
      byId('indieBuildBtn').disabled = false;
    }
  });
  if (bridge?.onIndieProgress) bridge.onIndieProgress(message => {
    const log = byId('indieBuildLog');
    log.textContent = (log.textContent + message).slice(-18000);
    log.scrollTop = log.scrollHeight;
  });
  byId('indiePlayBtn').addEventListener('click', async () => {
    const result = await bridge.indiePlay();
    if (!result.ok) describe('Could not launch game', result.error, true);
  });
  byId('indieFolderBtn').addEventListener('click', async () => {
    const result = await bridge.indieOpenOutput();
    if (!result.ok) describe('Could not open project', result.error, true);
  });
  check();
})();
