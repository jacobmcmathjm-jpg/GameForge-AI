const GameForgeCompleteGameBuilder = {
  lastPlan: null,
  async createPlan(projectState) {
    this.lastPlan = {
      title: (projectState && projectState.title) || 'GameForge Game',
      branding: { introText: 'Developed by GameForge AI' },
      modes: ['single_player'],
      levels: ['Level 1'],
      fallback: true
    };
    return { ok: true, plan: this.lastPlan };
  },
  async createExePackage(payload) { return { ok: true, fallback: true }; }
};
window.GameForgeCompleteGameBuilder = GameForgeCompleteGameBuilder;
