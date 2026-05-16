
const GameForgeFlawlessGenerationProcessController = {
  currentRun: null,
  steps: [
    ["prompt","Reading game idea","GameForge"],
    ["preflight","Checking required tools","Tool Detector"],
    ["api","Checking API keys","API Manager"],
    ["design","Creating game design","GameForge AI"],
    ["composition","Composing cinematic hero scene","Scene Composer"],
    ["assets","Generating/sourcing assets","Asset Forge + Meshy"],
    ["materials","Building PBR materials","PBR Builder"],
    ["blender","Cleaning/converting assets","Blender"],
    ["unreal","Creating Unreal project/scene","Unreal Engine"],
    ["gameplay","Installing gameplay systems","Gameplay Architect"],
    ["lighting","Applying photoreal lighting","Photoreal Gate"],
    ["diagnostics","Diagnosing/repairing issues","Self-Repair"],
    ["package","Packaging playable EXE","RunUAT"],
    ["validate","Validating playable build","EXE Validator"],
    ["complete","Final report","GameForge"]
  ],
  createRun(projectState = window.projectState || {}) {
    const run = {
      id: "GF-" + Date.now(),
      startedAt: new Date().toISOString(),
      status: "RUNNING",
      currentStep: "prompt",
      prompt: projectState.prompt || projectState.description || "",
      logs: [],
      needsAttention: [],
      steps: this.steps.map((s,i)=>({id:s[0],label:s[1],tool:s[2],status:i===0?"running":"queued",notes:[]}))
    };
    this.currentRun = run;
    projectState.flawlessGenerationRun = run;
    this.log("Generation started. Live watcher active.", "info");
    this.render();
    return run;
  },
  ensureRun(){ return this.currentRun || this.createRun(window.projectState || {}); },
  setStep(id,status="running",note=""){
    const run=this.ensureRun();
    run.currentStep=id;
    for(const step of run.steps){
      if(step.id===id){ step.status=status; if(note) step.notes.push(note); }
      else if(status==="running" && step.status==="running"){ step.status="done"; }
    }
    this.log(`${id}: ${status}${note ? " — " + note : ""}`, status==="failed"||status==="blocked"?"error":"info");
    this.render();
  },
  log(message, level="info"){
    const run=this.ensureRun();
    run.logs.push({at:new Date().toISOString(), level, message});
    if(run.logs.length>300) run.logs.shift();
    window.dispatchEvent(new CustomEvent("gameforge-live-log",{detail:{level,message}}));
    this.render();
  },
  attention(title,detail){
    const run=this.ensureRun();
    run.needsAttention.push({at:new Date().toISOString(), title, detail});
    this.log("Needs attention: "+title, "warn");
    this.render();
  },
  complete(summary=""){
    const run=this.ensureRun();
    run.status="COMPLETE";
    this.setStep("complete","done",summary);
  },
  render(){ if(window.GameForgeLiveBuildWatcherOverlay) window.GameForgeLiveBuildWatcherOverlay.render(this.currentRun); },
  contextForHybridAI(){ return "Flawless Generation Process Controller active: visible solo pipeline, tool handoff status, needs-attention prompts and live logs."; }
};
window.GameForgeFlawlessGenerationProcessController = GameForgeFlawlessGenerationProcessController;
