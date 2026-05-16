const GameForgePBRMaterialLibrary = {
  materials: [],
  get(name) { return this.materials.find(m => m.name === name) || null; }
};
window.GameForgePBRMaterialLibrary = GameForgePBRMaterialLibrary;
