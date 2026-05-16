const GameForgeEnvironmentKitManager = {
  kits: [],
  async scan() { return { ok: true, kits: [] }; }
};
window.GameForgeEnvironmentKitManager = GameForgeEnvironmentKitManager;
