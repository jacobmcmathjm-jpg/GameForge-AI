const GameForgeVisualTarget = {
  lastRecord: null,
  createRecord(projectState, options) {
    this.lastRecord = { style: options && options.style || 'realistic', prompt: projectState && projectState.prompt || '' };
    return this.lastRecord;
  },
  contextForHybridAI() { return 'Visual Target stub.'; }
};
window.GameForgeVisualTarget = GameForgeVisualTarget;
