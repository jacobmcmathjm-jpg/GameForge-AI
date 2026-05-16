const GameForgePerformanceOptimisationHardwareScaling = {
  lastPlan: null,
  createPlan(projectState) {
    this.lastPlan = { preset: 'Balanced PC', fallback: true };
    projectState && (projectState.performanceOptimisationPlan = this.lastPlan);
    return this.lastPlan;
  },
  async run(projectState) { return { ok: true, fallback: true }; }
};
window.GameForgePerformanceOptimisationHardwareScaling = GameForgePerformanceOptimisationHardwareScaling;
