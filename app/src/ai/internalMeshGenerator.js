const GameForgeInternalMeshGenerator = {
  recipes: [],
  materials: [],
  async createRecipe(type, name, style, detail) {
    const recipe = { id: 'mesh_' + Date.now(), type, name, style, detail, fallback: true };
    this.recipes.push(recipe);
    return { ok: true, recipe };
  },
  async createMaterial(preset, name) {
    const mat = { id: 'mat_' + Date.now(), preset, name, fallback: true };
    this.materials.push(mat);
    return { ok: true, material: mat };
  },
  async generatePackIntoScene(engine, packName) { return { ok: true, placed: [], fallback: true }; }
};
window.GameForgeInternalMeshGenerator = GameForgeInternalMeshGenerator;
