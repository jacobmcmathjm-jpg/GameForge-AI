
const GameForgeFullGameDesignDirector = {
  lastPlan: null,
  createPlan(projectState = window.projectState || {}) {
    const plan = {
      mode: "Full Game Design Director",
      generatedAt: new Date().toISOString(),
      gameName: projectState.name || "GameForge Game",
      purpose: "Plan core loop, menus, objectives, progression, pacing, win/lose conditions and completion rules.",
      prompt: projectState.promptGlobalRealismLocked || projectState.promptAAAPhotoreal || projectState.prompt || projectState.description || "",
      output: "Full Game Design Director manifest and report for the autonomous full-game pipeline."
    };
    this.lastPlan = plan;
    projectState["fullGameDesignDirectorPlan"] = plan;
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
    return "Full Game Design Director active: Plan core loop, menus, objectives, progression, pacing, win/lose conditions and completion rules.";
  }
};
window.GameForgeFullGameDesignDirector = GameForgeFullGameDesignDirector;
