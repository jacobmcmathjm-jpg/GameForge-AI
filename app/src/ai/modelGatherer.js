const GameForgeModelGatherer = {
  lastManifestPath: null,
  async createManifestFromRequirements() {
    return { ok: true, fallback: true, manifestPath: 'fallback_manifest' };
  },
  async runGatherer(manifestPath) {
    return { ok: true, fallback: true, report: { downloaded: 0, skipped: 0 } };
  }
};
window.GameForgeModelGatherer = GameForgeModelGatherer;
