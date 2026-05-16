const GameForgeAdvancedAIEnemyBehaviourBuilder = {
  lastPlan: null,
  createPlan(projectState) {
    this.lastPlan = { enemies: [], fallback: true };
    projectState && (projectState.advancedAIEnemyBehaviourBuilderPlan = this.lastPlan);
    return this.lastPlan;
  },
  async run(projectState) { return { ok: true, fallback: true }; }
};
window.GameForgeAdvancedAIEnemyBehaviourBuilder = GameForgeAdvancedAIEnemyBehaviourBuilder;
