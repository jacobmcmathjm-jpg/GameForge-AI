const GameForgeAssetManager = {
  library: { root: "", assets: [] },

  async scan() {
    const result = await window.gameforgeAPI.scanAssetLibrary();
    if (result.ok) {
      this.library = { root: result.root, assets: result.assets || [] };
      document.dispatchEvent(new CustomEvent("gf-assets-updated", { detail: this.library }));
    }
    return result;
  },

  async importFiles() {
    const result = await window.gameforgeAPI.importAssetFiles();
    if (result.ok && result.scan) {
      this.library = { root: result.scan.root, assets: result.scan.assets || [] };
      document.dispatchEvent(new CustomEvent("gf-assets-updated", { detail: this.library }));
    }
    return result;
  },

  async download(url, licenseNote = "") {
    const result = await window.gameforgeAPI.downloadAssetPack({ url, licenseNote });
    if (result.ok && result.scan) {
      this.library = { root: result.scan.root, assets: result.scan.assets || [] };
      document.dispatchEvent(new CustomEvent("gf-assets-updated", { detail: this.library }));
    }
    return result;
  },

  summary() {
    const counts = {};
    for (const a of this.library.assets || []) counts[a.type] = (counts[a.type] || 0) + 1;
    return counts;
  },

  generateManifest(packName, licenseNote) {
    return {
      packName: packName || "GameForge Imported Asset Pack",
      createdAt: new Date().toISOString(),
      licenseNote: licenseNote || "User must verify asset licence before use.",
      root: this.library.root,
      counts: this.summary(),
      assets: (this.library.assets || []).map(a => ({
        name: a.name,
        type: a.type,
        relativePath: a.relativePath,
        sizeBytes: a.sizeBytes
      }))
    };
  },

  modelAssets() {
    return (this.library.assets || []).filter(a => a.type === "model" && /\.(glb|gltf)$/i.test(a.name));
  },

  getByRelativePath(relativePath) {
    return (this.library.assets || []).find(a => a.relativePath === relativePath);
  },

  modelReferenceContext(prompt) {
    const p = String(prompt || "").toLowerCase();
    const models = this.modelAssets();
    const scored = models.map(m => {
      let score = 0;
      const n = (m.name + " " + m.relativePath).toLowerCase();
      ["tree","forest","grass","rock","human","zombie","enemy","building","house","weapon","gun","car","terrain"].forEach(k => {
        if (p.includes(k) && n.includes(k)) score += 3;
        else if (n.includes(k)) score += 1;
      });
      return { ...m, score };
    }).sort((a,b) => b.score - a.score).slice(0, 40);

    if (!scored.length) return "No GLB/GLTF model assets are available yet.";
    return scored.map(m => `- model: ${m.name} | relativePath: ${m.relativePath} | fileUrl: ${m.fileUrl || "unavailable"}`).join("\\n");
  },

  selectBestAssetsForPrompt(prompt) {
    const p = String(prompt || "").toLowerCase();
    const assets = this.library.assets || [];
    const wants = [];
    if (p.includes("forest") || p.includes("tree") || p.includes("bush")) wants.push("tree", "forest", "grass", "rock", "nature");
    if (p.includes("horror") || p.includes("abandoned")) wants.push("horror", "abandoned", "building", "ambient");
    if (p.includes("gun") || p.includes("weapon") || p.includes("shoot")) wants.push("weapon", "gun", "shot", "reload");
    if (p.includes("real") || p.includes("realistic")) wants.push("realistic", "pbr", "nature", "human");
    return assets.filter(a => wants.some(w => a.name.toLowerCase().includes(w) || a.relativePath.toLowerCase().includes(w))).slice(0, 30);
  }
};

window.GameForgeAssetManager = GameForgeAssetManager;