const GameForgeFree3DGenerator = {
  queue: [],
  async createJob(type, name, description, style, count) {
    const job = { id: 'free3d_' + Date.now(), type, name, description, style, count, fallback: true };
    this.queue.push(job);
    return { ok: true, job };
  },
  async createAssetPack(payload) { return { ok: true, fallback: true }; },
  async runQueueIntoScene(engine) { return { ok: true, placed: [], fallback: true }; }
};
window.GameForgeFree3DGenerator = GameForgeFree3DGenerator;
