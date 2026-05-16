const GameForgeToolchainOrchestrator = {
  async detect() { return { ok: true, tools: {}, fallback: true }; },
  async runHighEndPipeline(payload) { return { ok: true, fallback: true }; }
};
window.GameForgeToolchainOrchestrator = GameForgeToolchainOrchestrator;
