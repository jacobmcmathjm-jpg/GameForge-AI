const GameForgeSelfAssetGenerator = {
  async generate(payload) { return { ok: true, fallback: true, generated: 0 }; }
};
window.GameForgeSelfAssetGenerator = GameForgeSelfAssetGenerator;
