const GameForgeHybridAI = {
  settings: {},
  async loadSettings() { return { ok: true, settings: this.settings }; },
  async saveSettings(s) { this.settings = s; return { ok: true }; },
  async testConnection() { return { ok: false, error: 'No API key configured.' }; },
  async generate(payload) { return { ok: true, fallback: true, data: null }; }
};
window.GameForgeHybridAI = GameForgeHybridAI;
