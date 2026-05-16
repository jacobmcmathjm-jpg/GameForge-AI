const GameForgeCopyrightSafeVisualIdentityGuard = {
  checkPrompt(prompt) { return { ok: true, safe: true, cleaned: prompt }; },
  createPlan(projectState) { return { ok: true, fallback: true }; }
};
window.GameForgeCopyrightSafeVisualIdentityGuard = GameForgeCopyrightSafeVisualIdentityGuard;
