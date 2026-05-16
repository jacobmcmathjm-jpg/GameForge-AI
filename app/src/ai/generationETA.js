const GameForgeGenerationETA = {
  currentStage: null,
  setStage(stageId, label) {
    this.currentStage = { stageId, label, updatedAt: new Date().toISOString() };
    document.dispatchEvent(new CustomEvent('gf-eta-stage', { detail: this.currentStage }));
  },
  reset() { this.currentStage = null; }
};
window.GameForgeGenerationETA = GameForgeGenerationETA;
