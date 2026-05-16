const GameForgeCharacterModelImporter = {
  characterModels: [],
  async scanForCharacterModels() { return { ok: true, fallback: true, models: [] }; },
  async assignCharacterModels() { return { ok: true, fallback: true }; }
};
window.GameForgeCharacterModelImporter = GameForgeCharacterModelImporter;
