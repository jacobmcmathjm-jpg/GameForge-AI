const GameForgeRealisticStructureGenerator = {
  lastPlan: null,
  createPlan(projectState) {
    this.lastPlan = { structures: [], fallback: true };
    projectState && (projectState.realisticStructureGeneratorPlan = this.lastPlan);
    return this.lastPlan;
  },
  async run(projectState) { return { ok: true, fallback: true }; }
};
window.GameForgeRealisticStructureGenerator = GameForgeRealisticStructureGenerator;
