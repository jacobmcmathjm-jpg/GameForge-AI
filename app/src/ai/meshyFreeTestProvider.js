const GameForgeMeshyFreeTestProvider = {
  inferAssetRequests(projectState) { return []; },
  async preparePromptPack(projectState) { return { ok: true, fallback: true }; }
};
window.GameForgeMeshyFreeTestProvider = GameForgeMeshyFreeTestProvider;
