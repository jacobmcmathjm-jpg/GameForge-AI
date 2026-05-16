const GameForgeAI3DAssetPipeline = {
  settings: {},
  async loadSettings() { return { ok: true, settings: this.settings }; },
  async saveSettings(s) { this.settings = s; return { ok: true }; },
  async createAssetPlan(payload) { return { ok: true, fallback: true }; },
  async createProviderJob(payload) { return { ok: true, fallback: true }; },
  async importModelUrl(payload) { return { ok: true, fallback: true }; },
  async sanitisePrompt(payload) { return { ok: true, prompt: payload.prompt }; }
};
window.GameForgeAI3DAssetPipeline = GameForgeAI3DAssetPipeline;
