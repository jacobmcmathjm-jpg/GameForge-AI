const GameForgeAutoAssetImporter = {
  async scanModels() { return { ok: true, models: [] }; },
  async createPlan(payload) { return { ok: true, fallback: true }; },
  async runAutoImportPass(engine) { return { ok: true, fallback: true, scanCount: 0, placement: { ok: true, placed: [], fallbacks: [] } }; }
};
window.GameForgeAutoAssetImporter = GameForgeAutoAssetImporter;
