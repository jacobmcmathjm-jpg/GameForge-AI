const GameForgeAnimationImporter = {
  async createPlan(payload) { return { ok: true, fallback: true }; },
  async runAnimationImportPass(engine) { return { ok: true, fallback: true }; }
};
window.GameForgeAnimationImporter = GameForgeAnimationImporter;
