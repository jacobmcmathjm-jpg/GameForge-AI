// Run with Node 22+: node tools/test-indie-packaged.cjs <app.exe> <artifact-directory>
// Exercises the shipped executable through its own renderer and IPC bridge.
const fs = require('fs');
const path = require('path');
const {spawn} = require('child_process');
const net = require('net');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));

(async () => {
  const executable = path.resolve(process.argv[2]);
  const output = path.resolve(process.argv[3]);
  fs.mkdirSync(output, {recursive:true});
  const server = net.createServer();
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const port = server.address().port;
  await new Promise(resolve => server.close(resolve));
  const child = spawn(executable, [`--remote-debugging-port=${port}`, `--user-data-dir=${path.join(output,'profile')}`], {windowsHide:true, stdio:'pipe'});
  let log = '', socket, closeApp, sequence = 0;
  child.stdout.on('data', data => {log += data;});
  child.stderr.on('data', data => {log += data;});
  const pending = new Map();
  const errors = [];
  let result;
  try {
    let page;
    for (let attempt = 0; attempt < 150; attempt++) {
      try {
        const pages = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
        page = pages.find(value => value.type === 'page' && value.url.includes('index.html'));
        if (page) break;
      } catch (_) {}
      await pause(200);
    }
    if (!page) throw Error('Packaged app did not open its studio');
    socket = new WebSocket(page.webSocketDebuggerUrl);
    await new Promise((resolve,reject) => {socket.onopen=resolve; socket.onerror=reject;});
    socket.onmessage = event => {
      const message = JSON.parse(event.data);
      if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails.text);
      const handler = pending.get(message.id);
      if (handler) {pending.delete(message.id); message.error ? handler.reject(Error(JSON.stringify(message.error))) : handler.resolve(message.result);}
    };
    const send = (method, params = {}) => new Promise((resolve,reject) => {
      const id = ++sequence;
      pending.set(id,{resolve,reject});
      socket.send(JSON.stringify({id,method,params}));
    });
    await send('Runtime.enable');
    closeApp = () => send('Browser.close');
    const evaluate = async expression => {
      const response = await send('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});
      if(response.exceptionDetails) throw Error(JSON.stringify(response.exceptionDetails));
      return response.result.value;
    };
    for(let attempt=0; attempt<100; attempt++) {
      if(await evaluate('document.readyState === "complete" && document.getElementById("indieSetup").textContent.includes("Python ready")')) break;
      await pause(100);
    }
    await evaluate(`window.gameforgeAPI.saveSettings(${JSON.stringify({outputPath:path.join(output,'games')})})`);
    const setup = await evaluate('window.gameforgeAPI.indieCheck()');
    if (!setup.ok) throw Error('Packaged tools missing: '+JSON.stringify(setup));
    for(let attempt=0; attempt<50; attempt++) {
      if(await evaluate('!document.getElementById("indieBuildBtn").disabled')) break;
      await pause(200);
    }
    if(await evaluate('document.getElementById("indieBuildBtn").disabled')) throw Error('Build button stayed disabled');
    await evaluate('document.getElementById("indieBuildBtn").click()');
    if(!await evaluate('document.getElementById("indieBuildBtn").textContent.includes("Building")')) throw Error('Build click did not start generation');
    let build;
    for(let attempt=0; attempt<150; attempt++) {
      await pause(1000);
      const building = await evaluate('document.getElementById("indieBuildBtn").textContent.includes("Building")');
      if(!building) {build=await evaluate('window.gameforgeAPI.indieLastBuild()'); break;}
    }
    if(!build?.ok || !fs.existsSync(build.executable)) throw Error('Packaged build failed: '+JSON.stringify(build));
    const playEnabled = await evaluate('!document.getElementById("indiePlayBtn").disabled');
    const screenshot = await send('Page.captureScreenshot',{format:'png'});
    fs.writeFileSync(path.join(output,'packaged-studio.png'),Buffer.from(screenshot.data,'base64'));
    result = {ok:playEnabled && errors.length===0,playEnabled,errors,build};
  } catch(error) {result={ok:false,error:error.stack,errors};}
  finally {
    if(closeApp) await Promise.race([closeApp().catch(()=>{}),pause(2000)]);
    if(socket) socket.close();
    child.kill();
    fs.writeFileSync(path.join(output,'packaged-test.json'),JSON.stringify(result,null,2));
    fs.writeFileSync(path.join(output,'packaged-test.log'),log);
    console.log(JSON.stringify({ok:result.ok,error:result.error,report:path.join(output,'packaged-test.json')}));
    process.exitCode = result.ok ? 0 : 1;
  }
})();
