const GameForgeUnrealExportPrep = {
  lastPackage: null,
  async savePackage(projectState) {
    this.lastPackage = { gameName: (projectState && projectState.name) || 'GameForge Game', fallback: true };
    return { ok: true, package: this.lastPackage };
  }
};
window.GameForgeUnrealExportPrep = GameForgeUnrealExportPrep;
