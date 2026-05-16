const GameForgeAnimationPackManager = {
  packs: [],
  async scan() { return { ok: true, packs: [] }; }
};
window.GameForgeAnimationPackManager = GameForgeAnimationPackManager;
