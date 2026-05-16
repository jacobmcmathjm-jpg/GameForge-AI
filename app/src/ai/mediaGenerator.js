const GameForgeMediaGenerator = {
  async generate(payload) { return { ok: true, fallback: true }; },
  contextForHybridAI() { return 'Media Generator stub.'; }
};
window.GameForgeMediaGenerator = GameForgeMediaGenerator;
