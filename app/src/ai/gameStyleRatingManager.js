const GameForgeGameStyleRatingManager = {
  preset: null,
  getPreset() { return this.preset; },
  setPreset(p) { this.preset = p; },
  isPromptBlocked(prompt) { return false; }
};
window.GameForgeGameStyleRatingManager = GameForgeGameStyleRatingManager;
