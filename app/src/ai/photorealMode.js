const GameForgePhotorealMode = {
  lastPlan: null,
  async createPlan(projectState) {
    this.lastPlan = { mode: 'Photoreal Mode stub', fallback: true };
    return { ok: true, plan: this.lastPlan };
  },
  applyCinematicLook(engine) { return { ok: true, fallback: true }; },
  async createPBRSurfacePack() { return { ok: true, fallback: true, materials: [] }; },
  applySceneDetailPass(engine) { return { ok: true, fallback: true }; }
};
window.GameForgePhotorealMode = GameForgePhotorealMode;
