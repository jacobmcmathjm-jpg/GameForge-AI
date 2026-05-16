
const GameForgeGameplaySystemsBuilder = {
  lastPlan: null,
  createPlan(projectState = window.projectState || {}) {
    const plan = {
      mode: "Gameplay Systems Builder",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      purpose: "Plan player controller, interaction, objectives, inventory/loadout, audio manager, event triggers, win/lose state and save/settings.",
      prompt: projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.prompt || projectState.description || "",
      output: "Gameplay Systems Builder manifest and report for the autonomous full-game pipeline."
    };
    this.lastPlan = plan;
    projectState["gameplaySystemsBuilderPlan"] = plan;
    return plan;
  },
  async run(projectState = window.projectState || {}) {
    const plan = this.createPlan(projectState);
    return { ok: true, mode: "plan_ready", plan };
  },
  formatPlan(plan = this.lastPlan) {
    return plan ? `# ${plan.mode}\n\nPurpose:\n${plan.purpose}\n\nOutput:\n${plan.output}` : "No plan yet.";
  },
  contextForHybridAI() {
    return "Gameplay Systems Builder active: Plan player controller, interaction, objectives, inventory/loadout, audio manager, event triggers, win/lose state and save/settings.";
  }
};
window.GameForgeGameplaySystemsBuilder = GameForgeGameplaySystemsBuilder;
