"""
crawler_agent.py — Unattended Playwright asset & dependency harvesting.

- Launches Chromium with headless=False so the user can watch on a monitor.
- Traverses paginated open-source asset platforms (itch.io, OpenGameArt).
- When MASTER_APPROVAL is True: auto-accept downloads, track paths,
  unpack with zipfile into /res/assets/, no runtime prompts.
"""

from __future__ import annotations

import hashlib
import re
import time
import zipfile
from pathlib import Path
from typing import Any, Dict, List, Optional
from urllib.parse import urljoin, urlparse

from state_engine import StateEngine, FRAMEWORK_ROOT, _console

ASSETS_DIR = FRAMEWORK_ROOT / "res" / "assets"
DOWNLOADS_DIR = FRAMEWORK_ROOT / "res" / "downloads"

# Platforms the agent knows how to paginate
PLATFORM_CONFIG = {
    "opengameart": {
        "name": "OpenGameArt",
        "search_url": "https://opengameart.org/art-search-advanced?keys={query}&field_art_type_tid%5B%5D=9&sort_by=count&sort_order=DESC&items_per_page=24&page={page}",
        "base": "https://opengameart.org",
        "item_selector": "div.view-art-search div.views-row a",
        "next_page_param": "page",
    },
    "itchio": {
        "name": "itch.io",
        "search_url": "https://itch.io/games/free/tag-assets?q={query}&page={page}",
        "base": "https://itch.io",
        "item_selector": "div.game_cell a.game_link, a.title",
        "next_page_param": "page",
    },
}


class CrawlerAgent:
    """Playwright-driven open-source asset harvester."""

    def __init__(self, state: StateEngine) -> None:
        self.state = state
        ASSETS_DIR.mkdir(parents=True, exist_ok=True)
        DOWNLOADS_DIR.mkdir(parents=True, exist_ok=True)
        self._browser = None
        self._playwright = None
        self._context = None
        self._page = None

    # ------------------------------------------------------------------
    # Browser lifecycle
    # ------------------------------------------------------------------
    def start_browser(self) -> None:
        _console("CRAWLER", "Launching Playwright Chromium (headless=False)…")
        try:
            from playwright.sync_api import sync_playwright
        except ImportError as exc:
            raise RuntimeError(
                "Playwright is not installed. Run: pip install playwright && playwright install chromium"
            ) from exc

        self._playwright = sync_playwright().start()
        self._browser = self._playwright.chromium.launch(
            headless=False,  # visible on an active monitor
            downloads_path=str(DOWNLOADS_DIR),
            args=["--disable-blink-features=AutomationControlled"],
        )
        self._context = self._browser.new_context(
            accept_downloads=True,
            viewport={"width": 1400, "height": 900},
            user_agent=(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                "AppleWebKit/537.36 (KHTML, like Gecko) "
                "Chrome/120.0.0.0 Safari/537.36"
            ),
        )
        self._page = self._context.new_page()
        _console("CRAWLER", "Browser ready — visible window on active monitor.")

    def stop_browser(self) -> None:
        try:
            if self._context:
                self._context.close()
            if self._browser:
                self._browser.close()
            if self._playwright:
                self._playwright.stop()
        except Exception as exc:
            _console("CRAWLER", f"Browser shutdown warning: {exc}")
        finally:
            self._page = None
            self._context = None
            self._browser = None
            self._playwright = None
            _console("CRAWLER", "Browser closed.")

    # ------------------------------------------------------------------
    # Pagination traversal
    # ------------------------------------------------------------------
    def harvest(
        self,
        query: str,
        platforms: Optional[List[str]] = None,
        max_pages: int = 2,
        max_assets: int = 5,
    ) -> List[Dict[str, Any]]:
        """
        Crawl platforms for assets matching `query`.
        Requires MASTER_APPROVAL for autonomous download/unpack.
        """
        if not self.state.master_approval():
            _console(
                "CRAWLER",
                "MASTER_APPROVAL is False — crawl is read-only; downloads disabled.",
            )

        platforms = platforms or ["opengameart", "itchio"]
        collected: List[Dict[str, Any]] = []

        started_here = False
        if self._page is None:
            self.start_browser()
            started_here = True

        try:
            for platform_key in platforms:
                if len(collected) >= max_assets:
                    break
                cfg = PLATFORM_CONFIG.get(platform_key)
                if not cfg:
                    _console("CRAWLER", f"Unknown platform '{platform_key}' — skip.")
                    continue
                _console(
                    "CRAWLER",
                    f"Paginated harvest on {cfg['name']} for query='{query}' "
                    f"(max_pages={max_pages})",
                )
                try:
                    found = self._crawl_platform(
                        cfg,
                        query=query,
                        max_pages=max_pages,
                        remaining=max_assets - len(collected),
                    )
                    collected.extend(found)
                except Exception as exc:
                    _console("CRAWLER", f"{cfg['name']} crawl error (non-fatal): {exc}")
        finally:
            if started_here:
                self.stop_browser()

        _console("CRAWLER", f"Harvest complete — {len(collected)} asset(s) processed.")
        return collected

    def _crawl_platform(
        self,
        cfg: Dict[str, Any],
        query: str,
        max_pages: int,
        remaining: int,
    ) -> List[Dict[str, Any]]:
        assert self._page is not None
        results: List[Dict[str, Any]] = []
        seen_urls: set[str] = set()

        for page_idx in range(max_pages):
            if len(results) >= remaining:
                break
            url = cfg["search_url"].format(query=query.replace(" ", "+"), page=page_idx)
            _console("CRAWLER", f"[{cfg['name']}] page {page_idx + 1}/{max_pages} → {url}")
            try:
                self._page.goto(url, wait_until="domcontentloaded", timeout=45000)
                time.sleep(1.5)
            except Exception as exc:
                _console("CRAWLER", f"Navigation failed: {exc}")
                break

            # Collect candidate links
            hrefs: List[str] = []
            try:
                anchors = self._page.query_selector_all("a[href]")
                for a in anchors:
                    href = a.get_attribute("href") or ""
                    if not href or href.startswith("#") or href.startswith("javascript:"):
                        continue
                    full = urljoin(cfg["base"], href)
                    if self._looks_like_asset_page(full, cfg["name"]):
                        if full not in seen_urls:
                            seen_urls.add(full)
                            hrefs.append(full)
            except Exception as exc:
                _console("CRAWLER", f"Link scrape error: {exc}")

            _console("CRAWLER", f"Found {len(hrefs)} candidate asset page(s) on this page.")

            for item_url in hrefs:
                if len(results) >= remaining:
                    break
                try:
                    asset = self._process_asset_page(item_url, cfg["name"])
                    if asset:
                        results.append(asset)
                        self.state.record_asset(asset)
                except Exception as exc:
                    _console("CRAWLER", f"Asset page error (non-fatal): {exc}")

            # Detect end of pagination: no new links
            if not hrefs:
                _console("CRAWLER", "No more candidates — stopping pagination.")
                break

        return results

    @staticmethod
    def _looks_like_asset_page(url: str, platform_name: str) -> bool:
        path = urlparse(url).path.lower()
        if platform_name == "OpenGameArt":
            return "/content/" in path or bool(re.search(r"/node/\d+", path))
        if platform_name == "itch.io":
            # itch game pages look like https://user.itch.io/slug
            host = urlparse(url).netloc.lower()
            return host.endswith(".itch.io") and path.strip("/") != ""
        return False

    def _process_asset_page(self, url: str, platform_name: str) -> Optional[Dict[str, Any]]:
        assert self._page is not None
        _console("CRAWLER", f"Inspecting asset page: {url}")
        self._page.goto(url, wait_until="domcontentloaded", timeout=45000)
        time.sleep(1.0)

        title = ""
        try:
            title = (self._page.title() or "").strip()
        except Exception:
            title = url

        info: Dict[str, Any] = {
            "title": title,
            "source_url": url,
            "platform": platform_name,
            "downloaded": False,
            "local_path": None,
            "unpacked_to": None,
        }

        if not self.state.master_approval():
            _console("CRAWLER", "MASTER_APPROVAL=False — cataloguing only, no download.")
            return info

        # Attempt download of zip / archive links
        download_path = self._try_autonomous_download(url, platform_name)
        if download_path:
            info["downloaded"] = True
            info["local_path"] = str(download_path)
            unpacked = self._unpack_if_archive(download_path)
            if unpacked:
                info["unpacked_to"] = str(unpacked)
            _console("CRAWLER", f"Asset secured → {download_path}")
        else:
            _console("CRAWLER", "No downloadable archive found on this page.")

        return info

    def _try_autonomous_download(self, page_url: str, platform_name: str) -> Optional[Path]:
        """
        Find a likely download button/link and accept the download without prompts
        when MASTER_APPROVAL is True.
        """
        assert self._page is not None
        assert self._context is not None

        # Prefer direct archive hrefs first
        archive_exts = (".zip", ".7z", ".tar.gz", ".tgz", ".rar")
        candidates = []
        try:
            for a in self._page.query_selector_all("a[href]"):
                href = a.get_attribute("href") or ""
                text = (a.inner_text() or "").lower()
                full = urljoin(page_url, href)
                lower = full.lower()
                if any(lower.endswith(ext) or ext in lower for ext in archive_exts):
                    candidates.append((full, a))
                elif "download" in text or "download" in lower:
                    candidates.append((full, a))
        except Exception as exc:
            _console("CRAWLER", f"Download link scan failed: {exc}")
            return None

        if not candidates:
            return None

        target_url, locator = candidates[0]
        _console("CRAWLER", f"[AUTO-DOWNLOAD] Accepting: {target_url}")

        try:
            with self._page.expect_download(timeout=60000) as download_info:
                try:
                    locator.click(timeout=5000)
                except Exception:
                    # Fallback: navigate directly
                    self._page.evaluate(f"window.location.href = '{target_url}'")
            download = download_info.value
            suggested = download.suggested_filename or f"asset_{int(time.time())}.bin"
            # Sanitize filename
            safe = re.sub(r"[^\w.\-]+", "_", suggested)[:120]
            dest = DOWNLOADS_DIR / safe
            download.save_as(str(dest))
            _console("CRAWLER", f"Download saved: {dest}")
            return dest
        except Exception as exc:
            # Fallback: urllib direct fetch if it's a direct archive URL
            _console("CRAWLER", f"Playwright download event missed ({exc}); trying direct fetch…")
            if any(target_url.lower().endswith(ext) for ext in (".zip", ".7z", ".rar")):
                return self._direct_fetch(target_url)
            return None

    def _direct_fetch(self, url: str) -> Optional[Path]:
        import urllib.request

        name = re.sub(r"[^\w.\-]+", "_", Path(urlparse(url).path).name or f"asset_{int(time.time())}.zip")
        dest = DOWNLOADS_DIR / name[:120]
        try:
            _console("CRAWLER", f"urllib fetch → {dest}")
            urllib.request.urlretrieve(url, str(dest))
            return dest
        except Exception as exc:
            _console("CRAWLER", f"Direct fetch failed: {exc}")
            return None

    def _unpack_if_archive(self, archive_path: Path) -> Optional[Path]:
        """Unpack zip archives into /res/assets/ using Python's zipfile module."""
        if archive_path.suffix.lower() != ".zip":
            _console(
                "CRAWLER",
                f"Non-zip archive kept as-is (use 7-Zip if installed): {archive_path.name}",
            )
            # Still copy into assets folder for mapping
            dest_dir = ASSETS_DIR / archive_path.stem
            dest_dir.mkdir(parents=True, exist_ok=True)
            target = dest_dir / archive_path.name
            if not target.exists():
                target.write_bytes(archive_path.read_bytes())
            return dest_dir

        digest = hashlib.sha1(archive_path.name.encode()).hexdigest()[:8]
        dest_dir = ASSETS_DIR / f"{archive_path.stem}_{digest}"
        dest_dir.mkdir(parents=True, exist_ok=True)
        _console("CRAWLER", f"zipfile unpack → {dest_dir}")
        try:
            with zipfile.ZipFile(archive_path, "r") as zf:
                # Safety: prevent path traversal
                for member in zf.namelist():
                    member_path = Path(dest_dir) / member
                    if not str(member_path.resolve()).startswith(str(dest_dir.resolve())):
                        _console("CRAWLER", f"Skipping unsafe zip member: {member}")
                        continue
                zf.extractall(dest_dir)
            file_count = sum(1 for _ in dest_dir.rglob("*") if _.is_file())
            _console("CRAWLER", f"Unpacked {file_count} file(s) into {dest_dir}")
            self._map_assets(dest_dir)
            return dest_dir
        except zipfile.BadZipFile as exc:
            _console("CRAWLER", f"Bad zipfile: {exc}")
            return None

    def _map_assets(self, root: Path) -> Dict[str, List[str]]:
        """Map harvested files by type for the project builder."""
        mapping: Dict[str, List[str]] = {
            "textures": [],
            "audio": [],
            "models": [],
            "fonts": [],
            "other": [],
        }
        texture_ext = {".png", ".jpg", ".jpeg", ".webp", ".tga", ".bmp", ".svg"}
        audio_ext = {".wav", ".ogg", ".mp3", ".flac"}
        model_ext = {".obj", ".fbx", ".gltf", ".glb", ".dae"}
        font_ext = {".ttf", ".otf", ".woff"}

        for f in root.rglob("*"):
            if not f.is_file():
                continue
            ext = f.suffix.lower()
            rel = str(f.relative_to(ASSETS_DIR))
            if ext in texture_ext:
                mapping["textures"].append(rel)
            elif ext in audio_ext:
                mapping["audio"].append(rel)
            elif ext in model_ext:
                mapping["models"].append(rel)
            elif ext in font_ext:
                mapping["fonts"].append(rel)
            else:
                mapping["other"].append(rel)

        _console(
            "CRAWLER",
            "Asset map: "
            + ", ".join(f"{k}={len(v)}" for k, v in mapping.items() if v),
        )
        # Persist mapping under state
        self.state.record_asset({"type": "asset_map", "root": str(root), "map": mapping})
        return mapping

    # ------------------------------------------------------------------
    # Task-facing entry
    # ------------------------------------------------------------------
    def execute_task(self, task: Dict[str, Any]) -> Dict[str, Any]:
        """Run a harvest task from the backlog payload."""
        payload = task.get("payload") or {}
        query = payload.get("query") or payload.get("search") or "2d pixel art sprites"
        platforms = payload.get("platforms") or ["opengameart", "itchio"]
        max_pages = int(payload.get("max_pages", 2))
        max_assets = int(payload.get("max_assets", 3))

        _console(
            "CRAWLER",
            f"Focus: harvest query='{query}' platforms={platforms} "
            f"pages={max_pages} assets={max_assets}",
        )
        assets = self.harvest(
            query=query,
            platforms=platforms,
            max_pages=max_pages,
            max_assets=max_assets,
        )
        return {"ok": True, "assets": assets, "count": len(assets)}


if __name__ == "__main__":
    from state_engine import get_engine

    engine = get_engine()
    agent = CrawlerAgent(engine)
    print("CrawlerAgent ready. MASTER_APPROVAL =", engine.master_approval())
