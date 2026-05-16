const GameForgeCuratedDownloader = {
  async createPlan(payload) { return { ok: true, fallback: true }; },
  async downloadAsset(payload) { return { ok: false, error: 'No URL provided.' }; }
};
window.GameForgeCuratedDownloader = GameForgeCuratedDownloader;
