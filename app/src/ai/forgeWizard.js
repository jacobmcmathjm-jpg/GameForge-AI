const GameForgeForgeWizard = {
  async run(payload) { return { ok: true, fallback: true }; },
  contextForHybridAI() { return 'Forge Wizard stub.'; }
};
window.GameForgeForgeWizard = GameForgeForgeWizard;
