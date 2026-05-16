const GameForgeNativeExporter = {
  async createPackage(payload) { return { ok: true, fallback: true }; },
  async exportPackage(payload) { return { ok: true, fallback: true }; }
};
window.GameForgeNativeExporter = GameForgeNativeExporter;
