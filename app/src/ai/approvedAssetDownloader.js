const GameForgeApprovedAssetDownloader = {
  async createRequirementPlan(payload) { return { ok: true, fallback: true }; },
  async createManifest(payload) { return { ok: true, fallback: true }; },
  async runDownloads(payload) { return { ok: true, fallback: true, downloaded: 0 }; }
};
window.GameForgeApprovedAssetDownloader = GameForgeApprovedAssetDownloader;
