const GameForgeAutonomousRealismPipeline = {
  lastPlan: null,
  async createPlan(projectState) {
    this.lastPlan = { steps: [], fallback: true };
    projectState && (projectState.autonomousRealismPlan = this.lastPlan);
    return { ok: true, plan: this.lastPlan };
  },
  async run(projectState) { return { ok: true, fallback: true }; }
};
window.GameForgeAutonomousRealismPipeline = GameForgeAutonomousRealismPipeline;
