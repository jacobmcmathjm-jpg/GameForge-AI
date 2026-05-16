const GameForgeHighEndAssetLibraryManager = {
  lastPlan: null,
  createPlan(projectState) {
    this.lastPlan = { assets: [], fallback: true };
    projectState && (projectState.highEndAssetLibraryPlan = this.lastPlan);
    return this.lastPlan;
  },
  async run(projectState) { return { ok: true, fallback: true }; }
};
window.GameForgeHighEndAssetLibraryManager = GameForgeHighEndAssetLibraryManager;
