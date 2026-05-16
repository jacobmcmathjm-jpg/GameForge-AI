const GameForgeCharacterCreatureRealismLibrary = {
  characters: [],
  get(name) { return this.characters.find(c => c.name === name) || null; }
};
window.GameForgeCharacterCreatureRealismLibrary = GameForgeCharacterCreatureRealismLibrary;
