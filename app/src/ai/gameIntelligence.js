const GameForgeGameIntelligence = {
  lastPlan: null,
  createPlan(projectState) {
    this.lastPlan = {
      title: (projectState && projectState.title) || 'GameForge Game',
      prompt: (projectState && projectState.prompt) || '',
      assets: [],
      materials: [],
      levels: ['Level 1'],
      objectives: ['Complete the objective']
    };
    return this.lastPlan;
  },
  async createDownstreamJobs() { return { ok: true, fallback: true }; },
  contextForHybridAI() { return 'Game Intelligence stub.'; }
};
window.GameForgeGameIntelligence = GameForgeGameIntelligence;
