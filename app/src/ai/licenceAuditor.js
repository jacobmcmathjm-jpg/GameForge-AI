const GameForgeLicenceAuditor = {
  async audit(payload) { return { ok: true, fallback: true, report: [] }; }
};
window.GameForgeLicenceAuditor = GameForgeLicenceAuditor;
