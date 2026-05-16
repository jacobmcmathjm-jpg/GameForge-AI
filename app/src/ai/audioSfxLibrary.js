const GameForgeAudioSfxLibrary = {
  sounds: {},
  get(name) { return this.sounds[name] || null; }
};
window.GameForgeAudioSfxLibrary = GameForgeAudioSfxLibrary;
