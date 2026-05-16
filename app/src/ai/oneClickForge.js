const GameForgeOneClickForge = {
  async forge(projectState, engine) {
    if (window.GameForgeSafePipeline) return await GameForgeSafePipeline.generate(projectState, engine);
    return { ok: true, fallback: true };
  }
};
window.GameForgeOneClickForge = GameForgeOneClickForge;
