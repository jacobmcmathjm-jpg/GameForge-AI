
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const checks = [];
function add(name, ok, detail) {
  checks.push({ name, ok, detail });
  console.log(`${ok ? "PASS" : "FAIL"} - ${name}: ${detail || ""}`);
}

add("package.json", fs.existsSync(path.join(root, "package.json")), path.join(root, "package.json"));
add("main.js", fs.existsSync(path.join(root, "main.js")), path.join(root, "main.js"));
add("preload.js", fs.existsSync(path.join(root, "preload.js")), path.join(root, "preload.js"));
add("src/index.html", fs.existsSync(path.join(root, "src", "index.html")), path.join(root, "src", "index.html"));
add("node_modules", fs.existsSync(path.join(root, "node_modules")), path.join(root, "node_modules"));
add("electron", fs.existsSync(path.join(root, "node_modules", "electron")), path.join(root, "node_modules", "electron"));

const ok = checks.every(c => c.ok);
const report = {
  generatedAt: new Date().toISOString(),
  status: ok ? "READY" : "REPAIR_REQUIRED",
  root,
  checks
};

fs.writeFileSync(path.join(root, "..", "GameForge_Launch_Health.json"), JSON.stringify(report, null, 2), "utf8");

if (!ok) {
  console.error("\nLaunch repair required. Run npm install from the app folder or use REPAIR_GAMEFORGE.bat.");
  process.exit(1);
}
console.log("\nGameForge launch health: READY");
