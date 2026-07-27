"""
installer_manager.py — Two-tier approval & silent system installs.

Pre-Flight Approval Queue at script startup:
  1. LLM / heuristics identify required external tools.
  2. List them all in the terminal under the Jarvis banner.
  3. Block on input() until the user types 'Y' for blanket master approval.
  4. Download .exe packages (urllib/requests) and execute silently via
     subprocess.Popen with /S or /quiet flags.
"""

from __future__ import annotations

import os
import platform
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence

from state_engine import StateEngine, FRAMEWORK_ROOT, _console

INSTALLERS_DIR = FRAMEWORK_ROOT / "installers"
DOWNLOADS_DIR = FRAMEWORK_ROOT / "res" / "downloads"


@dataclass
class RequiredTool:
    """A third-party tool the daemon may need for unattended operation."""

    name: str
    description: str
    check_command: str  # e.g. "7z" or "godot"
    download_url: str
    installer_filename: str
    silent_args: Sequence[str] = field(default_factory=lambda: ["/S"])
    winget_id: Optional[str] = None
    optional: bool = False
    windows_only: bool = True

    def is_present(self) -> bool:
        """Return True if the tool appears to already be on PATH / installed."""
        if shutil.which(self.check_command):
            return True
        # Extra Windows heuristics
        if platform.system() == "Windows":
            candidates = [
                Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / self.name,
                Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) / self.name,
                Path(os.environ.get("LOCALAPPDATA", "")) / self.name,
            ]
            for c in candidates:
                if c and c.exists():
                    return True
        return False


# Known installers the framework may request for Godot game generation.
KNOWN_TOOLS: Dict[str, RequiredTool] = {
    "7zip": RequiredTool(
        name="7-Zip",
        description="Command-line archive utility for unpacking asset packs",
        check_command="7z",
        download_url="https://www.7-zip.org/a/7z2408-x64.exe",
        installer_filename="7z2408-x64.exe",
        silent_args=["/S"],
        winget_id="7zip.7zip",
    ),
    "git": RequiredTool(
        name="Git",
        description="Version control CLI for cloning open-source game resources",
        check_command="git",
        download_url="https://github.com/git-for-windows/git/releases/download/v2.47.0.windows.2/Git-2.47.0.2-64-bit.exe",
        installer_filename="Git-64-bit.exe",
        silent_args=["/VERYSILENT", "/NORESTART", "/NOCANCEL"],
        winget_id="Git.Git",
    ),
    "ffmpeg": RequiredTool(
        name="FFmpeg",
        description="Audio/video conversion for game asset pipelines",
        check_command="ffmpeg",
        download_url="https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
        installer_filename="ffmpeg-release-essentials.zip",
        silent_args=[],  # zip extract only
        winget_id="Gyan.FFmpeg",
        optional=True,
    ),
    "godot": RequiredTool(
        name="Godot 4",
        description="Godot 4 engine binary for headless script validation",
        check_command="godot",
        download_url="https://github.com/godotengine/godot/releases/download/4.3-stable/Godot_v4.3-stable_win64.exe.zip",
        installer_filename="Godot_v4.3-stable_win64.exe.zip",
        silent_args=[],  # portable zip
        optional=True,
    ),
}


class InstallerManager:
    """
    Detect missing tools, run the Pre-Flight Approval Queue, then download
    and silently install approved packages.
    """

    def __init__(self, state: StateEngine) -> None:
        self.state = state
        INSTALLERS_DIR.mkdir(parents=True, exist_ok=True)
        DOWNLOADS_DIR.mkdir(parents=True, exist_ok=True)

    # ------------------------------------------------------------------
    # Detection
    # ------------------------------------------------------------------
    def detect_required_tools(
        self,
        extra_names: Optional[Sequence[str]] = None,
        goal_hints: Optional[str] = None,
    ) -> List[RequiredTool]:
        """
        Identify tools needed for unattended operation.
        Combines known catalog + optional LLM/heuristic hints from the goal.
        """
        needed: List[RequiredTool] = []
        candidates = list(KNOWN_TOOLS.keys())
        if extra_names:
            for n in extra_names:
                if n in KNOWN_TOOLS and n not in candidates:
                    candidates.append(n)

        # Heuristic: scan master goal for keywords
        goal = (goal_hints or self.state.get("Master_Goal", "")).lower()
        keyword_map = {
            "7zip": ["zip", "archive", "unpack", "7z", "7-zip"],
            "git": ["git", "clone", "github", "repo"],
            "ffmpeg": ["audio", "video", "ffmpeg", "sound", "music", "convert"],
            "godot": ["godot", "gdscript", ".tscn", ".gd"],
        }
        for key, words in keyword_map.items():
            if any(w in goal for w in words) and key not in candidates:
                candidates.append(key)

        # Always consider core tools for a game-gen run
        for key in ("7zip", "git", "godot"):
            if key not in candidates:
                candidates.append(key)

        already = set(self.state.get("installed_tools", []))
        for key in candidates:
            tool = KNOWN_TOOLS.get(key)
            if not tool:
                continue
            if tool.windows_only and platform.system() != "Windows":
                # Still report on non-Windows for planning, but skip install later
                pass
            if tool.is_present() or tool.name in already or key in already:
                _console("INSTALL", f"Tool present — skip: {tool.name}")
                continue
            needed.append(tool)

        return needed

    def identify_from_llm_list(self, names: Sequence[str]) -> List[RequiredTool]:
        """Map LLM-identified tool names onto the Known Tools catalog."""
        found: List[RequiredTool] = []
        for raw in names:
            key = raw.strip().lower().replace(" ", "").replace("-", "")
            aliases = {
                "7z": "7zip",
                "7zip": "7zip",
                "sevenzip": "7zip",
                "git": "git",
                "ffmpeg": "ffmpeg",
                "godot": "godot",
                "godot4": "godot",
            }
            mapped = aliases.get(key) or aliases.get(raw.strip().lower())
            if mapped and mapped in KNOWN_TOOLS:
                tool = KNOWN_TOOLS[mapped]
                if not tool.is_present():
                    found.append(tool)
            else:
                _console("INSTALL", f"Unknown tool from LLM (ignored): {raw}")
        return found

    # ------------------------------------------------------------------
    # Pre-Flight Approval Queue
    # ------------------------------------------------------------------
    def run_preflight_approval(
        self,
        required: Optional[List[RequiredTool]] = None,
        auto_yes: bool = False,
    ) -> bool:
        """
        List every required installer and block until the user types 'Y'.
        On approval, set MASTER_APPROVAL in project_state.json.
        Returns True if approved (or already approved with empty queue).
        """
        if required is None:
            required = self.detect_required_tools()

        # Already have blanket approval and nothing new
        if not required and self.state.master_approval():
            _console("INSTALL", "MASTER_APPROVAL already True — no new installers required.")
            return True

        if not required:
            _console("INSTALL", "No third-party installations required.")
            # Still ask for crawl/download master approval if not set
            if not self.state.master_approval():
                return self._prompt_master_approval_only(auto_yes=auto_yes)
            return True

        print()
        print("=" * 72)
        print("Jarvis requires the following third-party installations to run unattended...")
        print("=" * 72)
        for i, tool in enumerate(required, 1):
            opt = " (optional)" if tool.optional else ""
            print(f"  {i}. {tool.name}{opt}")
            print(f"     {tool.description}")
            print(f"     Source: {tool.download_url}")
            print(f"     Silent flags: {' '.join(tool.silent_args) or '(portable extract)'}")
        print("=" * 72)
        print()
        print("Typing 'Y' grants a BLANKET MASTER APPROVAL for these specific tools.")
        print("Downloads will run in the background with silent/quiet installer flags.")
        print("Asset crawlers will also accept downloads without further prompts.")
        print()

        if auto_yes:
            answer = "Y"
            _console("INSTALL", "auto_yes enabled — granting MASTER_APPROVAL without interactive prompt.")
        else:
            try:
                answer = input("Grant master approval and proceed? [Y/N]: ").strip()
            except EOFError:
                _console("INSTALL", "No TTY available — approval denied.")
                answer = "N"

        if answer.upper() != "Y":
            _console("INSTALL", "Approval DENIED. Daemon will not install packages or auto-download assets.")
            self.state.set_master_approval(False)
            return False

        approved_names = [t.name for t in required]
        with self.state._lock:
            self.state._state["approved_installers"] = approved_names
            self.state._state["MASTER_APPROVAL"] = True
            self.state.save()

        _console("INSTALL", f"MASTER_APPROVAL granted for: {', '.join(approved_names)}")
        return True

    def _prompt_master_approval_only(self, auto_yes: bool = False) -> bool:
        print()
        print("=" * 72)
        print("Jarvis requires the following third-party installations to run unattended...")
        print("  (none — tools already present)")
        print()
        print("However, MASTER_APPROVAL is still required for unattended asset downloads.")
        print("=" * 72)
        if auto_yes:
            answer = "Y"
        else:
            try:
                answer = input("Grant master approval for unattended downloads? [Y/N]: ").strip()
            except EOFError:
                answer = "N"
        if answer.upper() == "Y":
            self.state.set_master_approval(True)
            return True
        self.state.set_master_approval(False)
        return False

    # ------------------------------------------------------------------
    # Download + silent install
    # ------------------------------------------------------------------
    def install_all_approved(self, required: Optional[List[RequiredTool]] = None) -> List[str]:
        """Download and silently install every approved missing tool."""
        if not self.state.master_approval():
            _console("INSTALL", "MASTER_APPROVAL is False — refusing installs.")
            return []

        if required is None:
            required = self.detect_required_tools()

        installed: List[str] = []
        for tool in required:
            try:
                ok = self.install_tool(tool)
                if ok:
                    installed.append(tool.name)
                    self.state.record_installed_tool(tool.name)
            except Exception as exc:
                _console("INSTALL", f"FAILED installing {tool.name}: {exc}")
        return installed

    def install_tool(self, tool: RequiredTool) -> bool:
        _console("INSTALLING", f"Triggering approved system package… {tool.name}")

        if tool.is_present():
            _console("INSTALL", f"{tool.name} already available on PATH.")
            return True

        dest = INSTALLERS_DIR / tool.installer_filename
        if not dest.exists():
            self._download(tool.download_url, dest)
        else:
            _console("INSTALL", f"Installer already cached: {dest}")

        # Zip-based portable tools
        if dest.suffix.lower() == ".zip":
            return self._extract_portable(tool, dest)

        if platform.system() != "Windows":
            _console(
                "INSTALL",
                f"Non-Windows host — skipping .exe silent install for {tool.name}. "
                f"Installer saved to {dest}",
            )
            return False

        return self._run_silent_exe(tool, dest)

    def _download(self, url: str, dest: Path) -> None:
        dest.parent.mkdir(parents=True, exist_ok=True)
        _console("INSTALL", f"Downloading {url}")
        _console("INSTALL", f"          → {dest}")
        try:
            # Prefer requests if available, else urllib
            try:
                import requests  # type: ignore

                with requests.get(url, stream=True, timeout=120) as resp:
                    resp.raise_for_status()
                    total = int(resp.headers.get("content-length", 0))
                    done = 0
                    with open(dest, "wb") as fh:
                        for chunk in resp.iter_content(chunk_size=1024 * 256):
                            if chunk:
                                fh.write(chunk)
                                done += len(chunk)
                                if total:
                                    pct = done * 100 // total
                                    print(f"\r  [DOWNLOAD] {pct}% ({done}/{total} bytes)", end="", flush=True)
                    print()
            except ImportError:
                def _reporthook(block_num: int, block_size: int, total_size: int) -> None:
                    if total_size > 0:
                        done = block_num * block_size
                        pct = min(100, done * 100 // total_size)
                        print(f"\r  [DOWNLOAD] {pct}%", end="", flush=True)

                urllib.request.urlretrieve(url, str(dest), reporthook=_reporthook)
                print()
        except (urllib.error.URLError, OSError, Exception) as exc:
            if dest.exists():
                dest.unlink(missing_ok=True)  # type: ignore[arg-type]
            raise RuntimeError(f"Download failed for {url}: {exc}") from exc

        _console("INSTALL", f"Download complete: {dest} ({dest.stat().st_size} bytes)")

    def _run_silent_exe(self, tool: RequiredTool, exe_path: Path) -> bool:
        args = [str(exe_path), *list(tool.silent_args)]
        _console("INSTALLING", f"subprocess.Popen silent install: {' '.join(args)}")
        try:
            proc = subprocess.Popen(
                args,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                cwd=str(exe_path.parent),
                shell=False,
            )
            _console("INSTALL", f"Installer PID={proc.pid} — waiting (background silent)…")
            # Wait with timeout so the daemon stays responsive
            try:
                code = proc.wait(timeout=600)
            except subprocess.TimeoutExpired:
                _console("INSTALL", f"Installer still running after 600s — leaving in background.")
                return True
            if code == 0:
                _console("INSTALL", f"{tool.name} installed successfully (exit 0).")
                return True
            _console("INSTALL", f"{tool.name} installer exited with code {code}")
            return code == 0
        except OSError as exc:
            _console("INSTALL", f"Failed to launch installer: {exc}")
            return False

    def _extract_portable(self, tool: RequiredTool, zip_path: Path) -> bool:
        import zipfile

        target = INSTALLERS_DIR / tool.name.replace(" ", "_")
        target.mkdir(parents=True, exist_ok=True)
        _console("INSTALL", f"Extracting portable package → {target}")
        try:
            with zipfile.ZipFile(zip_path, "r") as zf:
                zf.extractall(target)
            # Try to surface binary onto a known path note
            _console("INSTALL", f"Portable {tool.name} extracted. Add {target} to PATH if needed.")
            self.state.record_installed_tool(tool.name)
            return True
        except zipfile.BadZipFile as exc:
            _console("INSTALL", f"Bad zip for {tool.name}: {exc}")
            return False


def run_startup_preflight(state: StateEngine, auto_yes: bool = False) -> bool:
    """Convenience entry used by main.py at daemon boot."""
    mgr = InstallerManager(state)
    required = mgr.detect_required_tools()
    approved = mgr.run_preflight_approval(required, auto_yes=auto_yes)
    if approved and required:
        mgr.install_all_approved(required)
    return approved


if __name__ == "__main__":
    from state_engine import get_engine

    engine = get_engine()
    run_startup_preflight(engine, auto_yes="--yes" in sys.argv)
