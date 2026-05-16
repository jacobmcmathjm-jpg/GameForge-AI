
const GameForgeAssetQualityScorer = {
  scoreAsset(asset = {}) {
    let score = 0;
    if (asset.hasPBR) score += 20;
    if (asset.realWorldScale) score += 15;
    if (asset.meshDetail === "high") score += 20;
    if (asset.collisionReady) score += 10;
    if (asset.unrealReady) score += 15;
    if (asset.licenceMetadata) score += 10;
    if (asset.genreFit) score += 10;
    return Math.min(100, score);
  },
  createPlan(projectState = window.projectState || {}) {
    const plan = {
      mode: "Asset Quality Scorer",
      generatedAt: new Date().toISOString(),
      minimumAcceptScore: 82,
      highEndCandidateScore: 88,
      heroAssetScore: 92,
      scoringAreas: ["PBR", "scale", "mesh detail", "collision", "Unreal readiness", "licence metadata", "genre fit"]
    };
    projectState.assetQualityScorerPlan = plan;
    return plan;
  },
  contextForHybridAI() {
    return "Asset Quality Scorer active: scores assets and blocks low-quality/indie-looking assets from final pass.";
  }
};
window.GameForgeAssetQualityScorer = GameForgeAssetQualityScorer;
