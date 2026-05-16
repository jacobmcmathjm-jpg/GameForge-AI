
const GameForgeExternalToolFailurePatternLibrary = {
  patterns: [
    { tool: "unreal", match: /plugin.*missing|could not find plugin/i, cause: "Missing or disabled Unreal plugin", repair: "Enable plugin if installed; otherwise prompt user to install required plugin.", level: 4 },
    { tool: "unreal", match: /failed to load map|map.*not found|default map/i, cause: "Missing/default map issue", repair: "Regenerate map manifest and set default map in project settings.", level: 1 },
    { tool: "unreal", match: /missing material|material.*not found|texture.*not found/i, cause: "Missing material/texture reference", repair: "Assign fallback PBR material or relink texture from asset library.", level: 1 },
    { tool: "unreal", match: /blueprint.*error|failed to compile blueprint/i, cause: "Blueprint compile/reference error", repair: "Create repair report; disable broken generated reference or rebuild blueprint scaffold.", level: 2 },
    { tool: "runuat", match: /automationtool exiting with exitcode|cook failed|buildcookrun/i, cause: "RunUAT packaging/cook failure", repair: "Parse log, clean Saved/Intermediate, retry safer Development BuildCookRun preset.", level: 2 },
    { tool: "runuat", match: /sdk.*not found|windows sdk|visual studio/i, cause: "Missing SDK/build tools", repair: "Prompt user to install required build tools/SDK.", level: 4 },
    { tool: "blender", match: /no such file|cannot open|import failed/i, cause: "Missing or unsupported model input", repair: "Check path, convert format, or replace with fallback asset.", level: 2 },
    { tool: "blender", match: /texture.*missing|image.*not found/i, cause: "Missing texture link", repair: "Relink textures from source folder or assign fallback PBR material.", level: 1 },
    { tool: "meshy", match: /invalid api|unauthorized|401|403/i, cause: "Invalid/missing Meshy API key or permission", repair: "Pause and ask user to update API key/account permissions.", level: 4 },
    { tool: "meshy", match: /rate limit|429/i, cause: "Meshy API rate limit", repair: "Retry after delay, lower batch count, queue remaining assets.", level: 2 },
    { tool: "meshy", match: /job failed|generation failed/i, cause: "Meshy job failed", repair: "Retry with safer photoreal prompt or use asset library fallback.", level: 3 },
    { tool: "ffmpeg", match: /unknown encoder|codec|invalid data|could not write/i, cause: "FFmpeg encode or output issue", repair: "Retry with H.264/AAC safe preset or skip trailer generation.", level: 2 },
    { tool: "assetPipeline", match: /licen[cs]e.*missing|source metadata/i, cause: "Missing licence/source metadata", repair: "Mark internal testing only and block commercial release until verified.", level: 4 },
    { tool: "assetPipeline", match: /quality.*failed|low-poly|placeholder|missing pbr/i, cause: "Asset failed realism/quality gate", repair: "Replace from high-end library, scanned source, Meshy retry, or procedural fallback.", level: 3 }
  ],

  diagnose(tool, errorText = "") {
    const text = String(errorText);
    const pattern = this.patterns.find(p => (!tool || p.tool === tool) && p.match.test(text));
    return pattern || {
      tool: tool || "unknown",
      cause: "Unknown failure pattern",
      repair: "Create blocker report with raw logs and ask user or developer to review.",
      level: 5
    };
  },

  contextForHybridAI() {
    return "External Tool Failure Pattern Library active: maps Unreal/RunUAT/Blender/Meshy/FFmpeg/asset errors to likely causes and safe repair levels.";
  }
};

window.GameForgeExternalToolFailurePatternLibrary = GameForgeExternalToolFailurePatternLibrary;
