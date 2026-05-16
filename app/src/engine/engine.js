class GameForgeEngine {
  constructor(canvasId) {
    this.canvas = document.getElementById(canvasId);
    this.engine = null;
    this.scene = null;
    this.camera = null;
    this.selectedMesh = null;
    this.meshCounter = 0;
    this.playMode = false;
    this.objectComponents = {};
    this.materials = {};
    this.runtime = {
      health: 100,
      stamina: 100,
      ammo: 30,
      reserveAmmo: 90,
      score: 0,
      objective: "Survive and find supplies",
      enemiesDefeated: 0,
      suppliesFound: 0,
      gameOver: false,
      paused: false,
      menuState: 'gameplay',
      saveSlot: null,
      lastShot: 0,
      keys: {},
      interactTarget: null
    };
    this.runtimeObserver = null;
    this.inputHandlersReady = false;
    this.movementInput = {
      keys: {},
      enabled: false,
      moveSpeed: 8.0,
      sprintMultiplier: 1.7
    };
    this.audio = {
      enabled: false,
      sounds: {},
      ambient: null,
      lastFootstep: 0,
      basePath: "../assets/audio/"
    };
  }

  init() {
    if (!window.BABYLON) {
      console.error("Babylon.js not loaded.");
      return;
    }

    this.engine = new BABYLON.Engine(this.canvas, true);
    this.scene = new BABYLON.Scene(this.engine);
    this.scene.clearColor = new BABYLON.Color4(0.035, 0.055, 0.09, 1);
    this.setupCamera();
    this.setupMaterials();
    this.setupLighting();
    this.createGround();
    this.createPlayerSpawn();

    this.scene.onPointerObservable.add((pointerInfo) => {
      if (pointerInfo.type === BABYLON.PointerEventTypes.POINTERPICK) {
        const pick = pointerInfo.pickInfo;
        if (pick.hit && pick.pickedMesh && pick.pickedMesh.name !== "Ground") {
          this.selectMesh(pick.pickedMesh);
        }
      }
    });

    this.engine.runRenderLoop(() => this.scene.render());
    window.addEventListener("resize", () => this.engine.resize());
  }

  setupCamera() {
    this.camera = new BABYLON.ArcRotateCamera("EditorCamera", Math.PI / 4, Math.PI / 3, 26, new BABYLON.Vector3(0, 1, 0), this.scene);
    this.camera.attachControl(this.canvas, true);
    this.camera.wheelPrecision = 45;
  }

  setupLighting() {
    const hemi = new BABYLON.HemisphericLight("SkyLight", new BABYLON.Vector3(0, 1, 0), this.scene);
    hemi.intensity = 0.75;
    const sun = new BABYLON.DirectionalLight("Sun", new BABYLON.Vector3(-0.6, -1, -0.35), this.scene);
    sun.position = new BABYLON.Vector3(12, 18, 8);
    sun.intensity = 1.0;
  }

  setupMaterials() {
    const create = (name, color, spec = 0.08) => {
      const mat = new BABYLON.StandardMaterial(name, this.scene);
      mat.diffuseColor = color;
      mat.specularColor = new BABYLON.Color3(spec, spec, spec);
      this.materials[name] = mat;
      return mat;
    };
    create("concrete", new BABYLON.Color3(0.42, 0.42, 0.40));
    create("wood", new BABYLON.Color3(0.34, 0.20, 0.10));
    create("metal", new BABYLON.Color3(0.35, 0.32, 0.30), 0.22);
    create("forest", new BABYLON.Color3(0.07, 0.28, 0.12));
    create("enemy", new BABYLON.Color3(0.76, 0.16, 0.14));
    create("water", new BABYLON.Color3(0.12, 0.38, 0.62), 0.28);
    create("road", new BABYLON.Color3(0.075, 0.075, 0.085));
    create("player", new BABYLON.Color3(0.22, 0.62, 1));
    create("ground", new BABYLON.Color3(0.13, 0.20, 0.16));
  }

  createGround(kind = "ground") {
    const old = this.scene.getMeshByName("Ground");
    if (old) old.dispose();
    const ground = BABYLON.MeshBuilder.CreateGround("Ground", { width: 100, height: 100, subdivisions: 12 }, this.scene);
    ground.material = this.materials[kind] || this.materials.ground;
    ground.metadata = { type: "terrain", gameforge: true, locked: true };
    return ground;
  }

  selectMesh(mesh) {
    this.selectedMesh = mesh;
    document.dispatchEvent(new CustomEvent("gf-object-selected", { detail: this.serializeMesh(mesh) }));
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }

  createPlayerSpawn() {
    const spawn = BABYLON.MeshBuilder.CreateCapsule("PlayerSpawn", { height: 2, radius: 0.45 }, this.scene);
    spawn.position = new BABYLON.Vector3(0, 1, 0);
    spawn.material = this.materials.player;
    spawn.metadata = { type: "player_spawn", gameforge: true };
    this.objectComponents[spawn.name] = { PlayerSpawn: { perspective: "first_person" }, Health: { max: 100, current: 100 } };
    this.selectMesh(spawn);
    return spawn;
  }

  addCube(name = null, position = null, scale = null, materialName = "concrete", type = "prop") {
    const meshName = name || `Cube_${++this.meshCounter}`;
    const cube = BABYLON.MeshBuilder.CreateBox(meshName, { size: 1 }, this.scene);
    cube.position = position || new BABYLON.Vector3(Math.random() * 8 - 4, 0.5, Math.random() * 8 - 4);
    if (scale) cube.scaling = scale;
    cube.material = this.materials[materialName] || this.materials.concrete;
    cube.metadata = { type, gameforge: true, material: materialName };
    this.objectComponents[cube.name] = { Transform: this.transformData(cube) };
    this.selectMesh(cube);
    return cube;
  }


  createHumanoidEnemyMesh(name, position = new BABYLON.Vector3(0, 1, 0), scaleFactor = 1) {
    // v0.7.2.2: less blocky humanoid character placeholder.
    // This is still procedural/prototype quality, but it looks more like a character than a red capsule.
    const root = new BABYLON.TransformNode(name, this.scene);
    root.position = position.clone();

    const skinMat = this.scene.getMaterialByName("gf_skin") || new BABYLON.StandardMaterial("gf_skin", this.scene);
    skinMat.diffuseColor = new BABYLON.Color3(0.55, 0.62, 0.55);
    skinMat.specularColor = new BABYLON.Color3(0.05, 0.05, 0.05);

    const clothMat = this.scene.getMaterialByName("gf_dark_cloth") || new BABYLON.StandardMaterial("gf_dark_cloth", this.scene);
    clothMat.diffuseColor = new BABYLON.Color3(0.10, 0.13, 0.16);
    clothMat.specularColor = new BABYLON.Color3(0.02, 0.02, 0.02);

    const pantsMat = this.scene.getMaterialByName("gf_pants") || new BABYLON.StandardMaterial("gf_pants", this.scene);
    pantsMat.diffuseColor = new BABYLON.Color3(0.06, 0.08, 0.10);
    pantsMat.specularColor = new BABYLON.Color3(0.01, 0.01, 0.01);

    const make = (mesh, mat) => {
      mesh.parent = root;
      mesh.material = mat;
      mesh.metadata = { gameforge: true, type: "enemy_part" };
      return mesh;
    };

    const head = make(BABYLON.MeshBuilder.CreateSphere(name + "_Head", { diameter: 0.42 * scaleFactor, segments: 12 }, this.scene), skinMat);
    head.position.y = 1.72 * scaleFactor;

    const torso = make(BABYLON.MeshBuilder.CreateCapsule ? 
      BABYLON.MeshBuilder.CreateCapsule(name + "_Torso", { height: 0.92 * scaleFactor, radius: 0.28 * scaleFactor, tessellation: 12 }, this.scene) :
      BABYLON.MeshBuilder.CreateCylinder(name + "_Torso", { height: 0.92 * scaleFactor, diameter: 0.56 * scaleFactor, tessellation: 12 }, this.scene), clothMat);
    torso.position.y = 1.12 * scaleFactor;

    const leftArm = make(BABYLON.MeshBuilder.CreateCylinder(name + "_LeftArm", { height: 0.72 * scaleFactor, diameter: 0.14 * scaleFactor, tessellation: 10 }, this.scene), skinMat);
    leftArm.position.set(-0.38 * scaleFactor, 1.14 * scaleFactor, 0);
    leftArm.rotation.z = Math.PI * 0.08;

    const rightArm = make(BABYLON.MeshBuilder.CreateCylinder(name + "_RightArm", { height: 0.72 * scaleFactor, diameter: 0.14 * scaleFactor, tessellation: 10 }, this.scene), skinMat);
    rightArm.position.set(0.38 * scaleFactor, 1.14 * scaleFactor, 0);
    rightArm.rotation.z = -Math.PI * 0.08;

    const leftLeg = make(BABYLON.MeshBuilder.CreateCylinder(name + "_LeftLeg", { height: 0.82 * scaleFactor, diameter: 0.17 * scaleFactor, tessellation: 10 }, this.scene), pantsMat);
    leftLeg.position.set(-0.14 * scaleFactor, 0.42 * scaleFactor, 0);

    const rightLeg = make(BABYLON.MeshBuilder.CreateCylinder(name + "_RightLeg", { height: 0.82 * scaleFactor, diameter: 0.17 * scaleFactor, tessellation: 10 }, this.scene), pantsMat);
    rightLeg.position.set(0.14 * scaleFactor, 0.42 * scaleFactor, 0);

    // small glowing eyes
    const eyeMat = this.scene.getMaterialByName("gf_enemy_eye") || new BABYLON.StandardMaterial("gf_enemy_eye", this.scene);
    eyeMat.diffuseColor = new BABYLON.Color3(1, 0.12, 0.08);
    eyeMat.emissiveColor = new BABYLON.Color3(0.9, 0.05, 0.03);
    const eyeL = make(BABYLON.MeshBuilder.CreateSphere(name + "_EyeL", { diameter: 0.045 * scaleFactor, segments: 8 }, this.scene), eyeMat);
    eyeL.position.set(-0.08 * scaleFactor, 1.76 * scaleFactor, -0.19 * scaleFactor);
    const eyeR = make(BABYLON.MeshBuilder.CreateSphere(name + "_EyeR", { diameter: 0.045 * scaleFactor, segments: 8 }, this.scene), eyeMat);
    eyeR.position.set(0.08 * scaleFactor, 1.76 * scaleFactor, -0.19 * scaleFactor);

    root.metadata = { gameforge: true, type: "enemy", material: "enemy", humanoid: true };
    return root;
  }

  enableReliableMovementControls() {
    if (!this.canvas || this.movementInput.enabled) return;
    this.movementInput.enabled = true;
    this.movementInput.keys = {};

    const down = (e) => {
      const key = e.key.toLowerCase();
      if (["w","a","s","d","arrowup","arrowdown","arrowleft","arrowright","shift"].includes(key)) {
        this.movementInput.keys[key] = true;
        if (this.playMode) e.preventDefault();
      }
    };
    const up = (e) => {
      const key = e.key.toLowerCase();
      this.movementInput.keys[key] = false;
    };

    window.addEventListener("keydown", down, true);
    window.addEventListener("keyup", up, true);
    this.canvas.addEventListener("click", () => {
      try { this.canvas.focus(); } catch(e) {}
      if (this.playMode && this.canvas.requestPointerLock) {
        this.canvas.requestPointerLock();
      }
    });
    this.canvas.tabIndex = 1;
  }

  updateReliableMovement(dt) {
    if (!this.playMode || !this.camera || this.runtime.gameOver || this.runtime.paused) return;
    this.updateReliableMovement(dt);
    const keys = this.movementInput.keys || {};
    const forward = (keys["w"] || keys["arrowup"] ? 1 : 0) - (keys["s"] || keys["arrowdown"] ? 1 : 0);
    const strafe = (keys["d"] || keys["arrowright"] ? 1 : 0) - (keys["a"] || keys["arrowleft"] ? 1 : 0);
    if (!forward && !strafe) return;

    const speed = this.movementInput.moveSpeed * (keys["shift"] ? this.movementInput.sprintMultiplier : 1);
    const yaw = this.camera.rotation ? this.camera.rotation.y : 0;
    const fwd = new BABYLON.Vector3(Math.sin(yaw), 0, Math.cos(yaw));
    const right = new BABYLON.Vector3(Math.cos(yaw), 0, -Math.sin(yaw));
    const move = fwd.scale(forward).add(right.scale(strafe));
    if (move.lengthSquared() > 0) {
      move.normalize();
      this.camera.position.addInPlace(move.scale(speed * dt));
      // keep player above terrain
      if (this.camera.position.y < 1.75) this.camera.position.y = 1.75;
    }
  }


  addEnemy(position = null) {
    const enemy = BABYLON.MeshBuilder.CreateCapsule(`Enemy_${++this.meshCounter}`, { height: 2, radius: 0.5 }, this.scene);
    enemy.position = position || new BABYLON.Vector3(Math.random() * 16 - 8, 1, Math.random() * 16 - 8);
    enemy.material = this.materials.enemy;
    enemy.metadata = { type: "enemy", gameforge: true, material: "enemy" };
    this.objectComponents[enemy.name] = {
      Transform: this.transformData(enemy),
      Health: { max: 75, current: 75 },
      EnemyAI: { state: "patrol", detectionRange: 18, attackRange: 1.8, nightAggressionMultiplier: 1.5 },
      Damage: { amount: 15 }
    };
    this.selectMesh(enemy);
    return enemy;
  }

  addLight(position = null) {
    const light = new BABYLON.PointLight(`PointLight_${++this.meshCounter}`, position || new BABYLON.Vector3(2, 5, 2), this.scene);
    light.intensity = 0.9;
    light.diffuse = new BABYLON.Color3(1, 0.82, 0.58);
    const bulb = BABYLON.MeshBuilder.CreateSphere(`${light.name}_Icon`, { diameter: 0.35 }, this.scene);
    bulb.position = light.position.clone();
    const mat = new BABYLON.StandardMaterial(`${light.name}_Glow`, this.scene);
    mat.emissiveColor = new BABYLON.Color3(1, 0.82, 0.58);
    bulb.material = mat;
    bulb.metadata = { type: "light_icon", gameforge: true, linkedLight: light.name };
    this.objectComponents[bulb.name] = { Light: { intensity: light.intensity, color: "warm" } };
    this.selectMesh(bulb);
    return bulb;
  }

  generateTownBlock() {
    this.addCube("Main_Road", new BABYLON.Vector3(0, 0.03, 0), new BABYLON.Vector3(5, 0.04, 26), "road", "road");
    this.addCube("Cross_Road", new BABYLON.Vector3(0, 0.04, 0), new BABYLON.Vector3(28, 0.04, 4), "road", "road");
    const positions = [[-8,1.5,-8],[8,1.25,-8],[-8,2,7],[8,1.75,8],[-15,1.25,2],[15,1.5,-2]];
    positions.forEach((p, idx) => this.addCube(`Abandoned_Building_${idx+1}`, new BABYLON.Vector3(p[0],p[1],p[2]), new BABYLON.Vector3(3.5,p[1]*2,3.5), "concrete", "building"));
    for (let i = 0; i < 6; i++) this.addEnemy(new BABYLON.Vector3(Math.random()*30-15, 1, Math.random()*30-15));
    this.addLight(new BABYLON.Vector3(0, 6, -5));
  }

  generateForest(count = 38) {
    for (let i = 0; i < count; i++) {
      const x = Math.random()*82-41, z = Math.random()*82-41;
      if (Math.abs(x) < 6 && Math.abs(z) < 6) continue;
      this.addCube(`Tree_Trunk_${this.meshCounter}_${i}`, new BABYLON.Vector3(x,1.15,z), new BABYLON.Vector3(0.35,2.3,0.35), "wood", "tree_trunk");
      const crown = BABYLON.MeshBuilder.CreateSphere(`Tree_Crown_${this.meshCounter}_${i}`, { diameter: 2.8, segments: 8 }, this.scene);
      crown.position = new BABYLON.Vector3(x, 3.0, z);
      crown.material = this.materials.forest;
      crown.metadata = { type: "tree_crown", gameforge: true, material: "forest" };
      this.objectComponents[crown.name] = { Transform: this.transformData(crown) };
    }
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }

  generateSurvivalMap() {
    this.clearScene();
    this.createGround("ground");
    this.createPlayerSpawn();
    this.generateTownBlock();
    this.generateForest(42);
    this.addCube("Crafting_Table", new BABYLON.Vector3(3,0.5,2), new BABYLON.Vector3(1.5,1,0.8), "wood", "crafting_station");
    this.objectComponents["Crafting_Table"] = { CraftingStation: { recipes: ["bandage","campfire","wooden_wall"] }, Interactable: { prompt: "Craft" } };
    this.addCube("Safe_Base_Frame", new BABYLON.Vector3(-4,1,-2), new BABYLON.Vector3(3,2,0.25), "wood", "buildable_wall");
    this.addCube("Loot_Crate", new BABYLON.Vector3(1,0.5,4), new BABYLON.Vector3(1,1,1), "metal", "loot");
    this.objectComponents["Loot_Crate"] = { Pickup: { items: ["ammo","cloth","scrap"] }, Interactable: { prompt: "Search" } };
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }

  setTerrainPreset(preset) {
    if (preset === "flat") this.createGround("ground");
    if (preset === "forest") this.createGround("forest");
    if (preset === "town") this.createGround("concrete");
    if (preset === "horror") {
      this.createGround("ground");
      this.scene.clearColor = new BABYLON.Color4(0.02, 0.026, 0.04, 1);
    }
  }

  transformSelected(axis, delta) {
    if (!this.selectedMesh || this.selectedMesh.metadata?.locked) return;
    this.selectedMesh.position[axis] += Number(delta);
    this.updateTransformComponent(this.selectedMesh);
    this.selectMesh(this.selectedMesh);
  }

  scaleSelected(multiplier) {
    if (!this.selectedMesh || this.selectedMesh.metadata?.locked) return;
    this.selectedMesh.scaling.scaleInPlace(multiplier);
    this.updateTransformComponent(this.selectedMesh);
    this.selectMesh(this.selectedMesh);
  }

  duplicateSelected() {
    if (!this.selectedMesh || this.selectedMesh.metadata?.locked) return;
    const src = this.selectedMesh;
    const copy = src.clone(`${src.name}_Copy_${++this.meshCounter}`);
    copy.position = src.position.add(new BABYLON.Vector3(1.5, 0, 1.5));
    copy.metadata = { ...(src.metadata || {}), gameforge: true };
    this.objectComponents[copy.name] = JSON.parse(JSON.stringify(this.objectComponents[src.name] || { Transform: this.transformData(copy) }));
    this.updateTransformComponent(copy);
    this.selectMesh(copy);
  }

  deleteSelected() {
    if (!this.selectedMesh || this.selectedMesh.metadata?.locked) return;
    const name = this.selectedMesh.name;
    delete this.objectComponents[name];
    this.selectedMesh.dispose();
    this.selectedMesh = null;
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }

  applyMaterial(name) {
    if (!this.selectedMesh || this.selectedMesh.metadata?.locked) return;
    this.selectedMesh.material = this.materials[name] || this.selectedMesh.material;
    this.selectedMesh.metadata.material = name;
    this.selectMesh(this.selectedMesh);
  }

  addComponentToSelected(name) {
    if (!this.selectedMesh) return;
    if (!this.objectComponents[this.selectedMesh.name]) this.objectComponents[this.selectedMesh.name] = {};
    const defaults = {
      Health: { max: 100, current: 100 },
      Damage: { amount: 10 },
      Interactable: { prompt: "Interact" },
      EnemyAI: { state: "patrol", detectionRange: 15, attackRange: 2 },
      Pickup: { items: ["scrap"] },
      CraftingStation: { recipes: ["bandage"] }
    };
    this.objectComponents[this.selectedMesh.name][name] = defaults[name] || {};
    this.selectMesh(this.selectedMesh);
  }

  clearScene() {
    this.scene.meshes.slice().forEach(mesh => {
      if (mesh.name !== "Ground") mesh.dispose();
    });
    this.objectComponents = {};
    this.selectedMesh = null;
    this.createPlayerSpawn();
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }



  enableAudio() {
    if (!window.BABYLON || !this.scene) return;
    const soundMap = {
      gunshot: "gunshot.wav",
      reload: "reload.wav",
      pickup: "pickup.wav",
      enemyHit: "enemy_hit.wav",
      playerHurt: "player_hurt.wav",
      footstep: "footstep.wav",
      uiClick: "ui_click.wav",
      objectiveComplete: "objective_complete.wav",
      enemyGrowl: "enemy_growl.wav",
      ambientWind: "ambient_wind.wav"
    };

    Object.entries(soundMap).forEach(([key, file]) => {
      if (!this.audio.sounds[key]) {
        this.audio.sounds[key] = new BABYLON.Sound(key, this.audio.basePath + file, this.scene, null, {
          loop: key === "ambientWind",
          autoplay: false,
          volume: key === "ambientWind" ? 0.22 : 0.65
        });
      }
    });

    this.audio.enabled = true;
    if (this.audio.sounds.ambientWind && !this.audio.sounds.ambientWind.isPlaying) {
      this.audio.sounds.ambientWind.play();
    }
    document.dispatchEvent(new CustomEvent("gf-audio-updated", { detail: { enabled: true } }));
  }

  playSound(name) {
    if (!this.audio.enabled) return;
    const snd = this.audio.sounds[name];
    if (snd) {
      try {
        if (name !== "ambientWind") snd.stop();
        snd.play();
      } catch (e) {
        console.warn("Audio play failed", name, e);
      }
    }
  }


  addLootCrate(position = null, lootType = "supplies") {
    const crate = this.addCube(`Loot_${++this.meshCounter}`, position || new BABYLON.Vector3(Math.random()*24-12, 0.45, Math.random()*24-12), new BABYLON.Vector3(0.8,0.8,0.8), lootType === "medkit" ? "medkit" : "loot", "loot");
    this.objectComponents[crate.name] = {
      Transform: this.transformData(crate),
      Interactable: { prompt: lootType === "medkit" ? "Pick up medkit" : "Search supplies" },
      Pickup: { type: lootType, ammo: lootType === "ammo" ? 24 : 8, health: lootType === "medkit" ? 30 : 0, supplies: lootType === "supplies" ? 1 : 0 }
    };
    return crate;
  }

  addWeaponPickup(position = null) {
    const weapon = this.addCube(`WeaponPickup_${++this.meshCounter}`, position || new BABYLON.Vector3(Math.random()*18-9,0.35,Math.random()*18-9), new BABYLON.Vector3(1.1,0.18,0.25), "weapon", "weapon_pickup");
    this.objectComponents[weapon.name] = {
      Transform: this.transformData(weapon),
      Interactable: { prompt: "Pick up ammo and weapon parts" },
      Pickup: { type: "ammo", ammo: 36, supplies: 1 }
    };
    return weapon;
  }

  generatePlayableSurvivalPrototype() {
    this.generateRealisticEnvironment();
    for (let i = 0; i < 8; i++) this.addLootCrate(null, i % 3 === 0 ? "medkit" : "supplies");
    for (let i = 0; i < 5; i++) this.addWeaponPickup();
    for (let i = 0; i < 6; i++) {
      const angle = Math.random() * Math.PI * 2;
      const dist = 24 + Math.random() * 12;
      this.addEnemy(new BABYLON.Vector3(Math.cos(angle) * dist, 1, Math.sin(angle) * dist));
    }
    this.resetRuntime();
    this.runtime.objective = "Find 5 supplies and survive the infected";
    document.dispatchEvent(new CustomEvent("gf-runtime-updated", { detail: this.runtime }));
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }

  resetRuntime() {
    this.runtime.health = 100;
    this.runtime.stamina = 100;
    this.runtime.ammo = 30;
    this.runtime.reserveAmmo = 90;
    this.runtime.score = 0;
    this.runtime.objective = "Find 5 supplies and survive the infected";
    this.runtime.enemiesDefeated = 0;
    this.runtime.suppliesFound = 0;
    this.runtime.gameOver = false;
    this.runtime.paused = false;
    this.runtime.menuState = 'gameplay';
    this.runtime.spawnProtectionUntil = performance.now() + 3500;
    this.runtime.lastShot = 0;
    this.runtime.interactTarget = null;
    document.dispatchEvent(new CustomEvent("gf-runtime-updated", { detail: this.runtime }));
  }

  spawnEnemyWave(count = 6) {
    for (let i = 0; i < count; i++) {
      const angle = Math.random() * Math.PI * 2;
      const dist = 18 + Math.random() * 12;
      this.addEnemy(new BABYLON.Vector3(Math.cos(angle)*dist, 1, Math.sin(angle)*dist));
    }
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }

  setupRuntimeInput() {
    if (this.inputHandlersReady) return;
    this.inputHandlersReady = true;
    window.addEventListener("keydown", (e) => {
      this.runtime.keys[e.key.toLowerCase()] = true;
      if (!this.playMode) return;
      if (e.key === "Escape") { this.runtime.paused = !this.runtime.paused; this.runtime.menuState = this.runtime.paused ? 'pause' : 'gameplay'; document.dispatchEvent(new CustomEvent('gf-runtime-updated', { detail: this.runtime })); return; }
      if (e.key.toLowerCase() === "r") this.reloadWeapon();
      if (e.key.toLowerCase() === "e") this.interact();
    });
    window.addEventListener("keyup", (e) => {
      this.runtime.keys[e.key.toLowerCase()] = false;
    });
    this.canvas.addEventListener("pointerdown", (e) => {
      if (this.playMode && e.button === 0) this.shoot();
    });
  }

  shoot() {
    const now = performance.now();
    if (now - this.runtime.lastShot < 180 || this.runtime.gameOver) return;
    this.runtime.lastShot = now;
    if (this.runtime.ammo <= 0) {
      this.runtime.objective = "Out of ammo — press R to reload";
      this.playSound("uiClick");
      document.dispatchEvent(new CustomEvent("gf-runtime-updated", { detail: this.runtime }));
      return;
    }
    this.runtime.ammo -= 1;
    this.playSound("gunshot");

    const ray = this.scene.createPickingRay(this.engine.getRenderWidth()/2, this.engine.getRenderHeight()/2, BABYLON.Matrix.Identity(), this.camera);
    const hit = this.scene.pickWithRay(ray, (mesh) => mesh.metadata?.type === "enemy");
    if (hit && hit.hit && hit.pickedMesh) {
      const enemy = hit.pickedMesh;
      const comps = this.objectComponents[enemy.name] || {};
      if (!comps.Health) comps.Health = { max: 75, current: 75 };
      comps.Health.current -= 35;
      this.playSound("enemyHit");
      enemy.scaling.y = Math.max(0.35, enemy.scaling.y * 0.92);
      if (comps.Health.current <= 0) {
        this.runtime.score += 100;
        this.runtime.enemiesDefeated += 1;
        delete this.objectComponents[enemy.name];
        enemy.dispose();
        if (this.runtime.enemiesDefeated >= 5) this.runtime.objective = "Good work — keep searching for supplies";
      }
    }
    document.dispatchEvent(new CustomEvent("gf-runtime-updated", { detail: this.runtime }));
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }

  reloadWeapon() {
    if (!this.playMode || this.runtime.ammo >= 30 || this.runtime.reserveAmmo <= 0) return;
    const need = 30 - this.runtime.ammo;
    const take = Math.min(need, this.runtime.reserveAmmo);
    this.runtime.ammo += take;
    this.runtime.reserveAmmo -= take;
    this.playSound("reload");
    document.dispatchEvent(new CustomEvent("gf-runtime-updated", { detail: this.runtime }));
  }

  interact() {
    const target = this.runtime.interactTarget;
    if (!target || target.isDisposed()) return;
    const comps = this.objectComponents[target.name] || {};
    if (comps.Pickup) {
      this.runtime.reserveAmmo += comps.Pickup.ammo || 0;
      this.runtime.health = Math.min(100, this.runtime.health + (comps.Pickup.health || 0));
      this.runtime.suppliesFound += comps.Pickup.supplies || 0;
      this.runtime.score += 25;
      this.playSound("pickup");
      if (this.runtime.suppliesFound >= 5) {
        this.runtime.objective = "Supplies found — survive and reach safety";
        this.playSound("objectiveComplete");
      } else {
        this.runtime.objective = `Supplies found: ${this.runtime.suppliesFound}/5`;
      }
      delete this.objectComponents[target.name];
      target.dispose();
      this.runtime.interactTarget = null;
    }
    document.dispatchEvent(new CustomEvent("gf-runtime-updated", { detail: this.runtime }));
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }

  updateRuntime() {
    if (!this.playMode || !this.camera || this.runtime.gameOver || this.runtime.paused) return;

    const dt = this.engine.getDeltaTime() / 1000;
    if (this.runtime.keys["shift"]) {
      this.runtime.stamina = Math.max(0, this.runtime.stamina - 18 * dt);
      this.camera.speed = this.runtime.stamina > 0 ? 0.9 : 0.45;
    } else {
      this.runtime.stamina = Math.min(100, this.runtime.stamina + 12 * dt);
      this.camera.speed = 0.55;
    }

    const moving = this.runtime.keys["w"] || this.runtime.keys["a"] || this.runtime.keys["s"] || this.runtime.keys["d"];
    const now = performance.now();
    if (moving && now - this.audio.lastFootstep > (this.runtime.keys["shift"] ? 280 : 430)) {
      this.audio.lastFootstep = now;
      this.playSound("footstep");
    }

    const camPos = this.camera.position;
    let nearestInteract = null;
    let nearestDist = 999;

    this.scene.meshes.forEach(mesh => {
      if (!mesh.metadata?.gameforge || mesh.isDisposed()) return;
      const type = mesh.metadata.type;
      const dist = BABYLON.Vector3.Distance(camPos, mesh.position);

      if ((type === "loot" || type === "weapon_pickup") && dist < 3 && dist < nearestDist) {
        nearestInteract = mesh;
        nearestDist = dist;
      }

      if (type === "enemy") {
        const comps = this.objectComponents[mesh.name] || {};
        const ai = comps.EnemyAI || {};
        const detection = ai.detectionRange || 18;
        if (dist < detection) {
          const direction = camPos.subtract(mesh.position);
          direction.y = 0;
          if (direction.length() > 0.1) {
            direction.normalize();
            mesh.position.addInPlace(direction.scale(1.35 * dt));
            mesh.lookAt(new BABYLON.Vector3(camPos.x, mesh.position.y, camPos.z));
          }
          if (dist < 1.8) {
            if (!this.runtime.spawnProtectionUntil || performance.now() > this.runtime.spawnProtectionUntil) {
              this.runtime.health -= 18 * dt;
              if (Math.random() < 0.03) this.playSound("playerHurt");
            }
            this.runtime.objective = "You are being attacked!";
            if (this.runtime.health <= 0) {
              this.runtime.health = 0;
              this.runtime.gameOver = true;
              this.runtime.objective = "Prototype failed — press Stop then Play to retry";
            }
          }
        }
      }
    });

    this.runtime.interactTarget = nearestInteract;
    document.dispatchEvent(new CustomEvent("gf-runtime-updated", { detail: this.runtime }));
  }

  startRuntimeLoop() {
    this.setupRuntimeInput();
    this.enableReliableMovementControls();
    if (this.runtimeObserver) this.scene.onBeforeRenderObservable.remove(this.runtimeObserver);
    this.runtimeObserver = this.scene.onBeforeRenderObservable.add(() => this.updateRuntime());
    document.dispatchEvent(new CustomEvent("gf-runtime-updated", { detail: this.runtime }));
  }

  stopRuntimeLoop() {
    if (this.runtimeObserver) {
      this.scene.onBeforeRenderObservable.remove(this.runtimeObserver);
      this.runtimeObserver = null;
    }
  }




  async placeImportedModelAsset(asset, options = {}) {
    const name = options.name || asset.name || `ImportedModel_${++this.meshCounter}`;
    const pos = options.position || new BABYLON.Vector3(0, 0, 0);
    const scale = options.scale || new BABYLON.Vector3(1, 1, 1);
    const type = options.type || "imported_model";

    try {
      if (!asset?.fileUrl) throw new Error("Missing fileUrl for imported model.");
      const url = new URL(asset.fileUrl);
      const fileName = url.pathname.split("/").pop();
      const rootUrl = asset.fileUrl.slice(0, asset.fileUrl.length - fileName.length);
      const result = await BABYLON.SceneLoader.ImportMeshAsync("", rootUrl, fileName, this.scene);
      const root = new BABYLON.TransformNode(name, this.scene);
      result.meshes.forEach((mesh, idx) => {
        if (mesh !== root) {
          mesh.parent = root;
          mesh.metadata = { ...(mesh.metadata || {}), gameforge: true, importedAsset: asset.relativePath, type };
        }
      });
      root.position = pos;
      root.scaling = scale;
      root.metadata = { gameforge: true, importedAsset: asset.relativePath, type, material: "imported" };
      this.objectComponents[root.name] = {
        Transform: {
          position: { x: pos.x, y: pos.y, z: pos.z },
          scale: { x: scale.x, y: scale.y, z: scale.z },
          rotation: { x: 0, y: 0, z: 0 }
        },
        ImportedAsset: { relativePath: asset.relativePath, fileUrl: asset.fileUrl, type: "model" }
      };
      this.selectMesh(result.meshes.find(m => m.name && m.name !== "__root__") || root);
      document.dispatchEvent(new CustomEvent("gf-scene-updated"));
      return root;
    } catch (error) {
      console.warn("Imported model placement failed, creating proxy:", error);
      const proxy = this.addCube(name + "_Proxy", pos, scale, "metal", type);
      proxy.metadata.importedAsset = asset?.relativePath || "unknown";
      this.objectComponents[proxy.name] = {
        Transform: this.transformData(proxy),
        ImportedAsset: { relativePath: asset?.relativePath || "unknown", fileUrl: asset?.fileUrl || "", type: "model", loadError: error.message },
        Proxy: { reason: "GLB/GLTF could not be loaded. Proxy created." }
      };
      return proxy;
    }
  }


  applyGeneratedSceneData(data, assetLibrary = null) {
    this.clearScene();
    this.createGround("grass");
    this.applyRealisticLighting();
    const assetLookup = {};
    if (assetLibrary && Array.isArray(assetLibrary.assets)) {
      assetLibrary.assets.forEach(a => assetLookup[a.relativePath] = a);
    }
    const components = {};
    (data.objects || []).forEach((o) => {
      const pos = new BABYLON.Vector3(o.position.x, o.position.y, o.position.z);
      const scale = new BABYLON.Vector3(o.scale.x, o.scale.y, o.scale.z);
      let mesh = null;
      const importedRef = o.components && o.components.ImportedAsset && o.components.ImportedAsset.relativePath;
      if (importedRef && assetLookup[importedRef] && assetLookup[importedRef].type === "model") {
        this.placeImportedModelAsset(assetLookup[importedRef], { name: o.name, position: pos, scale, type: o.type || "imported_model" });
        components[o.name] = o.components || {};
        return;
      }
      if (o.type === "player_spawn") {
        mesh = this.scene.getMeshByName("PlayerSpawn") || this.createPlayerSpawn();
        mesh.position = pos; mesh.scaling = scale;
        mesh.material = this.materials[o.material] || this.materials.player;
        mesh.metadata.type = "player_spawn"; mesh.metadata.material = o.material;
      } else if (o.type === "enemy") {
        mesh = this.addEnemy(pos); mesh.name = o.name; mesh.scaling = scale;
      } else if (o.type === "tree") {
        this.addRealisticTree(pos); return;
      } else if (o.type === "rock") {
        mesh = this.addRock(pos, Math.max(scale.x, scale.y, scale.z)); mesh.name = o.name;
      } else if (o.type === "grass") {
        this.addGrassPatch(pos, 24, Math.max(2, scale.x * 4)); return;
      } else if (o.type === "loot") {
        mesh = this.addLootCrate(pos, "supplies"); mesh.name = o.name;
      } else if (o.type === "weapon_pickup") {
        mesh = this.addWeaponPickup(pos); mesh.name = o.name;
      } else if (o.type === "crafting_station") {
        mesh = this.addCube(o.name, pos, scale, o.material || "wood", "crafting_station");
      } else if (o.type === "light_icon") {
        mesh = this.addLight(pos); mesh.name = o.name;
      } else {
        mesh = this.addCube(o.name, pos, scale, o.material || "concrete", o.type || "prop");
      }
      if (mesh) {
        mesh.name = o.name || mesh.name;
        if (o.components && o.components.ImportedAsset) mesh.metadata.importedAsset = o.components.ImportedAsset;
        mesh.metadata.type = o.type; mesh.metadata.material = o.material;
        if (this.materials[o.material]) mesh.material = this.materials[o.material];
        components[mesh.name] = o.components || {};
        if (!components[mesh.name].Transform) components[mesh.name].Transform = this.transformData(mesh);
      }
    });
    this.objectComponents = { ...this.objectComponents, ...components };
    this.resetRuntime();
    if (data.objective) this.runtime.objective = data.objective;
    if (this.enableAudio) this.enableAudio();
    document.dispatchEvent(new CustomEvent("gf-runtime-updated", { detail: this.runtime }));
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }


  setPlayMode(enabled) {
    this.playMode = enabled;
    const badge = document.getElementById("modeBadge");
    if (enabled) {
      badge.textContent = "PLAY MODE";
      this.camera.detachControl();
      this.camera = new BABYLON.UniversalCamera("PlayCamera", new BABYLON.Vector3(0, 2, -6), this.scene);
      this.camera.setTarget(new BABYLON.Vector3(0, 1.6, 0));
      this.camera.attachControl(this.canvas, true);
      this.camera.speed = 0.55;
      this.camera.angularSensibility = 4500;
      this.camera.keysUp.push(87); this.camera.keysDown.push(83); this.camera.keysLeft.push(65); this.camera.keysRight.push(68);
      this.resetRuntime();
      if (this.camera) {
        this.camera.position = new BABYLON.Vector3(0, 2, -8);
        this.camera.setTarget(new BABYLON.Vector3(0, 1.5, 0));
      }
      this.startRuntimeLoop();
      if (this.audio.enabled && this.audio.sounds.ambientWind && !this.audio.sounds.ambientWind.isPlaying) this.audio.sounds.ambientWind.play();
      this.canvas.requestPointerLock?.();
    } else {
      badge.textContent = "EDIT MODE";
      this.stopRuntimeLoop();
      if (this.audio.sounds.ambientWind && this.audio.sounds.ambientWind.isPlaying) this.audio.sounds.ambientWind.stop();
      document.exitPointerLock?.();
      this.camera.detachControl();
      this.setupCamera();
    }
  }

  transformData(mesh) {
    return {
      position: { x: mesh.position.x, y: mesh.position.y, z: mesh.position.z },
      scale: { x: mesh.scaling.x, y: mesh.scaling.y, z: mesh.scaling.z },
      rotation: { x: mesh.rotation.x, y: mesh.rotation.y, z: mesh.rotation.z }
    };
  }

  updateTransformComponent(mesh) {
    if (!this.objectComponents[mesh.name]) this.objectComponents[mesh.name] = {};
    this.objectComponents[mesh.name].Transform = this.transformData(mesh);
  }

  serializeMesh(mesh) {
    return {
      name: mesh.name,
      type: mesh.metadata?.type || "mesh",
      material: mesh.metadata?.material || "default",
      position: { x: Number(mesh.position.x.toFixed(2)), y: Number(mesh.position.y.toFixed(2)), z: Number(mesh.position.z.toFixed(2)) },
      scale: { x: Number(mesh.scaling.x.toFixed(2)), y: Number(mesh.scaling.y.toFixed(2)), z: Number(mesh.scaling.z.toFixed(2)) },
      locked: !!mesh.metadata?.locked
    };
  }

  serializeScene() {
    return {
      engine: "GameForge AI Engine",
      version: "0.4",
      objects: this.scene.meshes
        .filter(m => m.metadata?.gameforge)
        .map(m => this.serializeMesh(m))
    };
  }

  loadSceneData(sceneData, components = {}) {
    this.clearScene();
    this.objectComponents = components || {};
    const objects = sceneData?.objects || [];
    objects.forEach(o => {
      if (o.name === "Ground" || o.locked) return;
      let mesh;
      const pos = new BABYLON.Vector3(o.position?.x || 0, o.position?.y || 0, o.position?.z || 0);
      const scale = new BABYLON.Vector3(o.scale?.x || 1, o.scale?.y || 1, o.scale?.z || 1);
      if (o.type === "enemy") mesh = this.addEnemy(pos);
      else if (o.type === "light_icon") mesh = this.addLight(pos);
      else mesh = this.addCube(o.name, pos, scale, o.material || "concrete", o.type || "prop");
      mesh.name = o.name;
      mesh.scaling = scale;
      mesh.metadata.type = o.type;
      mesh.metadata.material = o.material;
      if (this.materials[o.material]) mesh.material = this.materials[o.material];
    });
    document.dispatchEvent(new CustomEvent("gf-scene-updated"));
  }
}

window.GameForgeEngine = GameForgeEngine;