const GameForgeParanormalDeviceJumpscareSystem = {
  lastPlan: null,
  createPlan(projectState) {
    this.lastPlan = { devices: [], jumpScares: [], fallback: true };
    projectState && (projectState.paranormalJumpscarePlan = this.lastPlan);
    return this.lastPlan;
  },
  async run(projectState) { return { ok: true, fallback: true, plan: this.lastPlan }; }
};
window.GameForgeParanormalDeviceJumpscareSystem = GameForgeParanormalDeviceJumpscareSystem;
