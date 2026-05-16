const OfflineGenerator = {
  forgeStages: [
    "Read game description",
    "Generate design document",
    "Choose standalone engine systems",
    "Create scene layout",
    "Generate terrain preset",
    "Place town/forest/world objects",
    "Create player spawn and camera plan",
    "Generate enemy AI component data",
    "Generate weapon/inventory/crafting logic",
    "Generate skill tree and quests",
    "Create asset plan",
    "Run offline test pass",
    "Prepare playable draft export",
    "Create playable survival loop",
    "Enable shooting, loot and enemy attacks",
    "Attach placeholder audio triggers"
  ],

  hypergames: {
    "Zombie Wave Shooter": "Fast arena survival game with player spawn, enemy waves, weapon logic, scoring, health and ammo pickups.",
    "Horror Escape Room": "Small indoor horror game with locked doors, keys, jump scares, objective triggers and a final exit.",
    "Forest Survival": "Outdoor survival game with trees, scavenging, campfire crafting, stamina, weather and night threats.",
    "Dungeon Crawler": "Room-based combat prototype with enemies, loot crates, locked doors and simple progression.",
    "Parkour Runner": "Fast movement prototype with platforms, checkpoints, hazards and score timer.",
    "Abandoned Town Survival": "Survival horror prototype with abandoned buildings, enemies, loot crates, crafting and escape objective."
  },

  designDoc(state) {
    return `# ${state.name}

## GameForge AI Engine v0.4 Offline Design Document

## Description
${state.prompt}

## Build Settings
- Mode: ${state.mode}
- Size: ${state.size}
- Graphics Target: ${state.graphics}
- Perspective: ${state.perspective}
- Engine: GameForge AI Engine v0.4 standalone
- AI Mode: Offline prototype generator

## Core Promise
The user describes the game, then GameForge AI creates a playable draft inside its own editor.

## Core Gameplay Loop
Explore → Scavenge → Fight → Craft → Build → Upgrade → Survive → Escape.

## Player Systems
- ${state.perspective} camera
- WASD movement
- Sprint and stamina
- Health
- Interaction
- Inventory
- Weapon handling

## World Systems
- Editable 3D scene
- Town block generation
- Forest generation
- Horror survival terrain preset
- Lighting and atmosphere controls
- Placeholder structures and props

## Enemy Systems
- Enemy component data
- Health and damage
- Patrol/chase/attack design
- Night aggression multiplier

## Crafting and Building
- Scavenge materials
- Craft bandages, ammo packs and campfire
- Build basic walls and base objects
- Use crafting stations

## Skill Tree
- Survival
- Weapons
- Crafting
- Stealth
- Endurance

## Win Condition Draft
Survive long enough to repair/find the escape vehicle or reach the extraction point before the final storm.

## Prototype Notes
v0.4 is offline and template-driven. v0.5 should connect a real AI API to convert prompts into custom scene JSON and system data.
`;
  },

  logic(system = "full") {
    const blocks = {
      player: `component PlayerController {
  perspective: first_person;
  movement: WASD;
  sprint_key: Shift;
  jump_key: Space;
  stamina: 100;
  camera_sway: prototype;
}`,
      weapons: `component WeaponSystem {
  slots: primary, secondary, melee;
  fire: left_mouse;
  aim: right_mouse;
  reload: R;
  ammo_capacity: 30;
  base_damage: 25;
}`,
      enemies: `component EnemyAI {
  states: idle, patrol, investigate, chase, attack;
  detection_range: 18;
  attack_range: 1.8;
  night_aggression_multiplier: 1.5;
}`,
      inventory: `component Inventory {
  slots: 24;
  hotbar: 6;
  categories: weapons, materials, food, tools, quest_items;
  drag_drop: planned;
}`,
      crafting: `component Crafting {
  recipes: bandage, campfire, ammo_pack, wooden_wall, repair_tool;
  material_types: cloth, wood, scrap, fuel;
}`,
      skills: `component SkillTree {
  branches: survival, weapons, crafting, stealth, endurance;
  xp_sources: combat, crafting, exploration, survival_time;
}`,
      building: `component BaseBuilding {
  pieces: wooden_wall, door_frame, campfire, storage_crate, barricade;
  placement_mode: grid_snap;
  material_costs: enabled;
}`,
      weather: `component WeatherSystem {
  states: clear, fog, rain, storm;
  day_night_cycle: enabled;
  final_storm_timer: enabled;
}`,
      quests: `component QuestSystem {
  main_objective: escape_town;
  objectives: find_weapon, craft_bandage, build_shelter, repair_radio, reach_extraction;
  trigger_type: proximity_and_interaction;
}`
    };
    if (system === "full") return Object.values(blocks).join("\n\n");
    return blocks[system] || `component ${system} { status: planned; }`;
  },

  assetPlan(state) {
    return `# Asset Forge Plan

## Game
${state.name}

## Graphics Target
${state.graphics}

## Characters
- Player survivor
- Infected enemy base
- Strong infected variant
- Optional trader/survivor NPC

## Weapons
- Axe
- Pistol
- Shotgun
- Rifle
- Torch/flashlight
- Traps

## Environment
- Abandoned Australian town buildings
- Broken road pieces
- Bushland trees
- Sheds and fences
- Loot crates
- Fuel cans
- Scrap piles
- Medical kits
- Crafting bench
- Storm/extraction zone

## Materials
- Weathered timber
- Rusted metal
- Dirty concrete
- Wet asphalt
- Gum tree bark
- Dusty ground
- Old paint
- Broken glass

## Audio
- Wind
- Distant infected sounds
- Footsteps
- Weapon shots
- Reloads
- Inventory clicks
- Storm ambience
- Heartbeat danger layer

## Future Real AI Asset Pipeline
1. Generate concept art
2. Generate mesh/texture set
3. Validate format
4. Create LODs
5. Import to GameForge library
6. Run performance checks
`;
  },

  materialPlan() {
    return `# Material Plan

v0.4 includes simple prototype materials:
- Concrete
- Old Wood
- Rusted Metal
- Forest Green
- Enemy Red
- Cold Blue
- Road
- Skin Tone
- Grass
- Rock
- Bark
- Cloth

Future versions:
- PBR material editor
- Texture import
- AI texture generation
- Decal system
- Weather/wetness controls
- Day/night material changes
`;
  },

  realismAssetPlan(state) {
    return `# Offline Realism Pack Plan

## Goal
Create an everyday-life realistic look without aiming for full AAA production.

## What offline mode can do now
- Generate procedural-looking grass patches
- Generate tree placeholders with trunks and crowns
- Generate rocks with uneven shapes
- Apply realistic daylight, fog and atmosphere
- Create terrain presets
- Create human placeholders for scale and planning
- Generate asset requirements for future model replacement

## What offline mode cannot do by itself yet
- Create true photorealistic human faces
- Create high-end skeletal animation
- Create scanned-quality foliage
- Create cinematic AAA lighting and shaders
- Create production-ready character rigs

## Best future upgrade
Add a local/offline asset library containing licensed realistic:
- humans
- trees
- grass
- rocks
- terrain materials
- buildings
- animations

Then GameForge AI can place and configure them offline without needing Unreal, Unity or Godot.`;
  },

  audioPlan() {
    return `# Offline Audio Plan

## v0.4.3 Placeholder Audio Included
- gunshot.wav
- reload.wav
- pickup.wav
- enemy_hit.wav
- player_hurt.wav
- footstep.wav
- ui_click.wav
- objective_complete.wav
- enemy_growl.wav
- ambient_wind.wav

## Connected Gameplay Triggers
- Shooting plays gunshot
- Reloading plays reload
- Picking up loot plays pickup
- Hitting enemies plays enemy hit
- Player damage can play hurt sound
- Moving in Play Mode triggers footsteps
- Objective completion plays objective complete
- Ambient wind can loop during Play Mode

## Important Notes
These sounds are simple generated placeholders. They are useful for testing game feel, but they should be replaced later with proper licensed, recorded, or AI-generated audio.

## Future Audio Upgrades
- 3D positional sound
- Surface-based footsteps
- Enemy idle/chase/attack sounds
- Weather loops
- Interior/exterior ambience
- Music layers
- Audio mixer
- Volume settings
`;
  },

  playableSystemNotes() {
    return `# Playable Prototype Systems

## Included in v0.4.2
- Play Mode HUD
- Health
- Stamina
- Ammo and reserve ammo
- Click-to-shoot raycast weapon
- Reload with R
- Enemy chase/attack behaviour
- Loot crates
- Weapon/ammo pickups
- Medkit pickups
- Interaction with E
- Supplies objective
- Score
- Basic fail state
- Enemy wave spawning
- Placeholder audio triggers
- Ambient wind loop
- Footstep, gunshot, reload, pickup and hit sounds

## What this means
The prototype is no longer only a walk-around scene. You can generate a survival map, press Play, shoot enemies, collect loot, reload, take damage, and complete a simple supply objective.

## Still prototype-level
- No polished animations yet
- No finished weapon models
- No proper inventory UI yet
- No advanced enemy pathfinding yet
- No final game packaging yet
`;
  },

  forgeReport(state) {
    return `# Forge Report

## Project
${state.name}

## Offline Forge Output
- Design document generated
- Scene generated in internal Babylon.js viewport
- Survival map tools available
- Component-style object data added
- Gameplay logic generated as gfscript
- Asset plan generated
- Test Agent available
- Save/export pipeline added

## Included From Earlier Planning
- Electron desktop program base
- Babylon.js internal runtime/viewport
- AI-ready settings structure
- Hypergame Mode
- Studio Mode
- Forge Mode
- Offline generator templates
- Future OpenAI API connection path
- Standalone engine direction, not a plugin

## Current Limits
- Offline templates simulate AI behaviour.
- Real API-based generation is planned for v0.5.
- Hyper-realistic art is a target, not produced in this build.
- Play Mode is a camera/runtime prototype.
- Export Draft produces project data, not a packaged .exe yet.

## Recommended Next Build
v0.5 should add a real AI service layer:
- prompt-to-scene JSON
- prompt-to-component data
- prompt-to-gameplay logic
- validation and repair prompts
- generation cost tracking
`;
  },

  test(scene, logic, components) {
    const objects = scene.objects || [];
    const hasPlayer = objects.some(o => o.type === "player_spawn");
    const enemies = objects.filter(o => o.type === "enemy").length;
    const buildings = objects.filter(o => o.type === "building").length;
    const trees = objects.filter(o => o.type && o.type.includes("tree")).length;
    const hasWeapons = logic.includes("WeaponSystem");
    const hasInventory = logic.includes("Inventory");
    const hasCrafting = logic.includes("Crafting");
    const hasSkills = logic.includes("SkillTree");
    const compCount = Object.keys(components || {}).length;

    return [
      ["Engine Launch", "Passed", "GameForge v0.4 runtime is loaded."],
      ["Scene Load", objects.length > 0 ? "Passed" : "Warning", `${objects.length} scene objects found.`],
      ["Player Spawn", hasPlayer ? "Passed" : "Warning", hasPlayer ? "Player spawn exists." : "No player spawn exists."],
      ["Enemy Setup", enemies > 0 ? "Passed" : "Warning", `${enemies} enemies found.`],
      ["World Layout", buildings > 0 || trees > 0 ? "Passed" : "Warning", `${buildings} buildings and ${trees} tree objects found.`],
      ["Weapon Logic", hasWeapons ? "Passed" : "Warning", hasWeapons ? "Weapon system generated." : "Weapon system missing."],
      ["Inventory Logic", hasInventory ? "Passed" : "Warning", hasInventory ? "Inventory generated." : "Inventory missing."],
      ["Crafting Logic", hasCrafting ? "Passed" : "Warning", hasCrafting ? "Crafting generated." : "Crafting missing."],
      ["Skill Tree", hasSkills ? "Passed" : "Warning", hasSkills ? "Skill tree generated." : "Skill tree missing."],
      ["Component Data", compCount > 0 ? "Passed" : "Warning", `${compCount} object component records found.`],
      ["Performance", objects.length < 250 ? "Passed" : "Warning", objects.length < 250 ? "Object count is safe for prototype." : "Scene may need optimisation."]
    ];
  }
};

window.OfflineGenerator = OfflineGenerator;