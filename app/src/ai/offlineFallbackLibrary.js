const GameForgeOfflineFallbackLibrary = {
  getFallbackAsset(type) { return { type, fallback: true, name: 'Procedural ' + type }; }
};
window.GameForgeOfflineFallbackLibrary = GameForgeOfflineFallbackLibrary;
