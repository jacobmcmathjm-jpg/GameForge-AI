const GameForgeAICostEstimator = {
  lastEstimate: null,
  estimate(payload) {
    return { ok: true, estimatedCredits: 0, estimatedCostUSD: '$0', note: 'No AI API configured yet.' };
  }
};
window.GameForgeAICostEstimator = GameForgeAICostEstimator;
