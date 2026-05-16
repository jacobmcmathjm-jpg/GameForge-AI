const GameForgePhasmophobiaQualityHauntedGameCore = {
  lastPlan: null,
  createPlan(projectState) {
    this.lastPlan = { devices: [], rooms: [], fallback: true };
    return this.lastPlan;
  },
  async run(projectState) { return { ok: true, fallback: true }; }
};
window.GameForgePhasmophobiaQualityHauntedGameCore = GameForgePhasmophobiaQualityHauntedGameCore;
