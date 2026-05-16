const GameForgeLocalAI = {
  settings: {},
  async testConnection() { return { ok: false, error: 'Local AI not configured.' }; },
  async generate(payload) { return { ok: true, fallback: true, data: null }; },
  contextForHybridAI() { return 'Local AI stub.'; }
};
window.GameForgeLocalAI = GameForgeLocalAI;
