const GameForgeScannedAssetCharacterRealismConnector = {
  lastPlan: null,
  createPlan(projectState) {
    this.lastPlan = { characters: [], fallback: true };
    projectState && (projectState.scannedAssetCharacterRealismPlan = this.lastPlan);
    return this.lastPlan;
  },
  async run(projectState) { return { ok: true, fallback: true }; }
};
window.GameForgeScannedAssetCharacterRealismConnector = GameForgeScannedAssetCharacterRealismConnector;
