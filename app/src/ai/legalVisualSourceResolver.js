const GameForgeLegalVisualSourceResolver = {
  async resolve(payload) { return { ok: true, fallback: true, sources: [] }; }
};
window.GameForgeLegalVisualSourceResolver = GameForgeLegalVisualSourceResolver;
