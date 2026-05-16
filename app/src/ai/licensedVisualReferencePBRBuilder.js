const GameForgeLicensedVisualReferencePBRBuilder = {
  lastPlan: null,
  async createPlan(projectState) {
    this.lastPlan = { materials: [], fallback: true };
    return { ok: true, plan: this.lastPlan };
  },
  async run(projectState) { return { ok: true, fallback: true }; }
};
window.GameForgeLicensedVisualReferencePBRBuilder = GameForgeLicensedVisualReferencePBRBuilder;
