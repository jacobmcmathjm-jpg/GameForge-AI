// Run with Electron: electron tools/test-indie-desktop.cjs <artifact-directory>
const {app} = require('electron');
const fs = require('fs');
const path = require('path');
const output = path.resolve(process.argv[2] || path.join(require('os').tmpdir(), 'gameforge-desktop-test-' + Date.now()));
fs.mkdirSync(output, {recursive:true});
const profile = path.join(output, 'profile');
fs.mkdirSync(profile, {recursive:true});
app.setPath('userData', profile);
fs.writeFileSync(path.join(profile,'gf_settings.json'),JSON.stringify({outputPath:path.join(output,'games')}));
let complete = false;
const errors = [];
const deadline = setTimeout(()=>finish({ok:false,error:'Desktop test timed out',errors}),120000);
function finish(result) {
  if(complete) return;
  complete=true;
  clearTimeout(deadline);
  fs.writeFileSync(path.join(output,'desktop-test.json'),JSON.stringify(result,null,2));
  app.exit(result.ok ? 0 : 1);
}
app.on('browser-window-created', (_event, window) => {
  window.hide();
  window.webContents.on('console-message', (_event, level, message) => {if(level >= 3) errors.push(message);});
  window.webContents.once('did-finish-load', async () => {
    try {
      const run = js => window.webContents.executeJavaScript(js);
      const tools = await run('window.gameforgeAPI.indieCheck()');
      if(!tools.ok) throw Error('Tool setup failed: ' + JSON.stringify(tools));
      await new Promise(resolve=>setTimeout(resolve,500));
      fs.writeFileSync(path.join(output,'studio-before.png'),(await window.webContents.capturePage()).toPNG());
      const currentPanel = await run('document.querySelector(".panel.active").id');
      if(currentPanel !== 'panel-indie') throw Error('Indie Studio is not the opening panel');
      await run('document.getElementById("indieBuildBtn").click()');
      let last;
      for(let i=0;i<90;i++) {
        await new Promise(resolve=>setTimeout(resolve,1000));
        const building = await run('document.getElementById("indieBuildBtn").textContent.includes("Building")');
        if(!building) {last=await run('window.gameforgeAPI.indieLastBuild()'); break;}
      }
      if(!last?.ok || !last.executable) throw Error('Desktop build failed: '+JSON.stringify(last));
      const playEnabled = await run('!document.getElementById("indiePlayBtn").disabled');
      if(!playEnabled) throw Error('Verified game did not enable Play');
      fs.writeFileSync(path.join(output,'studio-after.png'),(await window.webContents.capturePage()).toPNG());
      finish({ok:errors.length===0,errors,currentPanel,playEnabled,build:last});
    } catch(error) {finish({ok:false,error:error.stack,errors});}
  });
});
require('../main.js');
