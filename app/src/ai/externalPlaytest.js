const GameForgeExternalPlaytest = {
  async validateDemo(payload) { return { ok: true, fallback: true, checks: [] }; },
  async exportDemo(payload) { return { ok: true, fallback: true }; }
};
window.GameForgeExternalPlaytest = GameForgeExternalPlaytest;
