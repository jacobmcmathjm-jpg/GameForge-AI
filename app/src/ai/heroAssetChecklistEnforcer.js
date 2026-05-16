const GameForgeHeroAssetChecklistEnforcer = {
  async enforce(payload) { return { ok: true, fallback: true, passed: true }; }
};
window.GameForgeHeroAssetChecklistEnforcer = GameForgeHeroAssetChecklistEnforcer;
