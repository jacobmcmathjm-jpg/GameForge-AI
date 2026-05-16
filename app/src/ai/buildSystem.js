const GameForgeBuildSystem = {
  async createBuild(payload) { return { ok: true, fallback: true }; },
  async validateBuild(payload) { return { ok: true, fallback: true }; }
};
window.GameForgeBuildSystem = GameForgeBuildSystem;
