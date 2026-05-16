const GameForgeAnimationAssetGatherer = {
  lastManifestPath: null,
  async createManifestFromRequirements() {
    return { ok: true, fallback: true, manifestPath: 'fallback_animation_manifest' };
  },
  async runGatherer(manifestPath) {
    return { ok: true, fallback: true, report: { downloaded: 0, skipped: 0 } };
  },
  async generateMissingReport(payload) { return { ok: true, fallback: true }; }
};
window.GameForgeAnimationAssetGatherer = GameForgeAnimationAssetGatherer;
