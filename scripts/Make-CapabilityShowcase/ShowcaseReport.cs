using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class ShowcaseReport
{
    public static async Task WriteAsync(string output,string project)
    {
        var games=new[]{
            ("FPS","Three-level FPS","Freshly generated industrial arenas; primitive enemies, firing/reloading, six objectives per level and linked level progression.","WASD + mouse; click/Space fire; R reload; N next level after completing objectives.",false),
            ("Platformer","Platformer","Freshly generated jumping/collectible prototype; five collectibles and a capsule placeholder character.","A/D move; Space jump.",false),
            ("TopDownRPG","Top-down combat arena","Freshly generated arena with seven combat objectives. Not a full RPG: no complete quests, inventory or dialogue system.","WASD move; Space attack.",false),
            ("Racing","Checkpoint racer","Freshly generated six-gate driving prototype with a placeholder vehicle. Not realistic vehicle simulation.","W/S throttle; A/D steer.",false),
            ("SecurityCombat","Security-combat lab","Existing v10 ForgeCore-generated lab, freshly rendered/built: original guard, skinned arms, original training rifle, three enemies, navigation and visible projectiles.","WASD + mouse; left click fire; right click aim; R reload; Escape pause; F5 restart.",true),
            ("AnimationLab","Character-animation lab","Existing v3 ForgeCore-generated lab, freshly rendered/built: technical mannequin and eight original Unity-authored motion clips, blending and action control. Not polished mocap or Humanoid retargeting.","Automatic demonstration; use the on-screen Pause, Manual, Fire, Reload, Hit, Death and Restart demo buttons.",true),
            ("FrontierValley","Frontier Valley map","Existing ForgeCore-generated terrain map preserved/reused, freshly rendered/built. Walkable scenery, landmarks, minimap and day/night foundation; not a finished designed game.","Use the on-screen map controls.",true)};
        foreach(var (id,_,_,_,_) in games)
        {
            using var startup=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output,"Evidence",id+"-startup.json")));
            if(!startup.RootElement.GetProperty("success").GetBoolean())throw new InvalidDataException("A game startup check did not pass.");
            var source=Path.Combine(project,"Builds","Showcase20261028",id);var target=Path.Combine(output,"Games",id);
            foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
                if(await HashFile(file)!=await HashFile(Path.Combine(target,Path.GetRelativePath(source,file))))throw new InvalidDataException("Game delivery copy differs.");
            var png=Path.Combine(output,"Previews",id+".png");using var proof=JsonDocument.Parse(await File.ReadAllBytesAsync(png+".forgecore-preview.json"));
            if(await HashFile(png)!=proof.RootElement.GetProperty("pngSha256").GetString())throw new InvalidDataException("Rendered preview identity differs.");
        }
        foreach(var kind in new[]{"barrel","mannequin","security"})
        {
            using var source=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output,"Assets",kind,"entry.json")));
            foreach(var file in source.RootElement.GetProperty("files").EnumerateArray())
            {
                var path=Path.Combine(output,"Assets",kind,file.GetProperty("name").GetString()!);
                if(new FileInfo(path).Length!=file.GetProperty("bytes").GetInt64()||await HashFile(path)!=file.GetProperty("sha256").GetString())throw new InvalidDataException("Source delivery copy differs.");
            }
        }
        var html=new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>ForgeCore — actual capability showcase</title><style>body{margin:0;background:#0d1620;color:#e3edf5;font:16px/1.6 system-ui}main{max-width:1240px;margin:auto;padding:36px 24px}h1,h2{line-height:1.2}h1{font-size:40px;margin-bottom:12px}.note{background:#182939;padding:20px;border-left:4px solid #52d4bd;border-radius:6px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(330px,1fr));gap:24px}article{background:#142231;border:1px solid #2b4053;border-radius:12px;overflow:hidden}article img{width:100%;display:block;background:#09121c}.copy{padding:20px}.asset img{aspect-ratio:1;object-fit:contain}a{color:#69e1cc}.tag{font-size:13px;text-transform:uppercase;letter-spacing:1px;color:#88b9d6}code{overflow-wrap:anywhere}footer{margin-top:36px;color:#a8bed0}</style><main><div class=\"tag\">28 October 2026 · actual generated outputs · offline tools</div><h1>What ForgeCore can make today</h1><p>These are real Unity runtime renders and Blender source previews, not AI concept images. The test runner used the existing ForgeCore production builders; it did not hand-author new game content.</p><div class=\"note\"><strong>Current standard: procedural prototypes, not a finished indie/AA game.</strong> Each Windows build passed a bounded headless startup check. That is not a human playthrough, smooth-performance or visual-quality acceptance. Runtime camera PNGs omit immediate-mode HUD/menu overlays. Launch a game from its whole <code>Games/&lt;name&gt;</code> folder; the four template games start at a menu—press Enter or click Play.</div><h2>Game and map examples</h2><div class=\"grid\">");
        var readme=new StringBuilder("# ForgeCore actual capability showcase — 28 October 2026\n\nOpen `START-HERE.html` for the complete image gallery. These are existing ForgeCore production builders, not concept images or newly implemented generation capabilities. The four generation/runtime assemblies match the shipped S07SourcePromotionV1 app.\n\nKeep this entire folder intact. Do not copy only an EXE. The genre templates start at a menu: press Enter/click Play.\n\n");
        foreach(var (id,title,description,controls,reused) in games)
        {
            html.Append($"<article><a href=\"Previews/{id}.png\"><img src=\"Previews/{id}.png\" alt=\"{WebUtility.HtmlEncode(title)} real Unity runtime render\"></a><div class=\"copy\"><div class=\"tag\">{(reused?"Existing scene · fresh build":"Freshly generated scenes")}</div><h3>{title}</h3><p>{description}</p><p><strong>Controls:</strong> {controls}</p><p><code>Games/{id}/{id}.exe</code></p><a href=\"Games/{id}/\">Open game folder</a></div></article>");
            readme.Append($"## {title}\n\n- EXE: `Games/{id}/{id}.exe`\n- {description}\n- Controls: {controls}\n- Fresh preview/build; existing scene reused: {reused}.\n\n");
        }
        html.Append("</div><h2>Fresh Blender source examples</h2><p>Three fixed original procedural recipes, not arbitrary character/object generation. Editable .blend source, GLB, PBR maps, metrics and recipe are retained. These new samples stop before Unity import; they were not automatically placed into the games above. Human visual/rights review is still pending. Blender source animation count is zero; the separate animation lab demonstrates Unity-authored curves.</p><div class=\"grid\">");
        foreach(var (id,title,description) in new[]{("barrel","Weathered barrel","Bounded metal prop with 512px PBR maps."),("mannequin","Rigged technical mannequin","Skinned generic humanoid base for rig/motion tests, not detailed character art."),("security","Industrial-security character","Layered procedural uniform, boots/gloves, vest/pouches and helmet/visor with 1K source maps; not accepted realistic character art.")})
        {
            html.Append($"<article class=\"asset\"><a href=\"Assets/{id}/preview.png\"><img src=\"Assets/{id}/preview.png\" alt=\"{title} actual Blender preview\"></a><div class=\"copy\"><div class=\"tag\">Fresh Blender execution · unreviewed source</div><h3>{title}</h3><p>{description}</p><a href=\"Assets/{id}/model.blend\">Editable Blender source</a> · <a href=\"Assets/{id}/asset.glb\">GLB</a> · <a href=\"Assets/{id}/metrics.json\">Metrics</a></div></article>");
            readme.Append($"## {title}\n\n{description} Files: `Assets/{id}/model.blend`, `asset.glb`, `preview.png`, PBR maps, `metrics.json`, recipe/request and content-addressed `entry.json`. Newly created source only; not imported or approved for a game.\n\n");
        }
        const string limits="Basic HUD/menu/pause/restart, linked prototype levels and synthesized feedback sounds are included where implemented. This does not add arbitrary maps/genres/assets, realistic art/animations, full RPG systems, production vehicle physics, soundtrack/voice generation, finished settings/accessibility, representative performance acceptance or reliable unattended prompt-to-finished-game generation. No paid provider, new package/model download, human art/rights attestation or GitHub upload was used. Existing scenes/assets/package/source files were rehashed against the captured baseline; scene registrations and graphics/quality setup can change through production builders with protected backups.";
        html.Append("</div><footer><h2>Limits and evidence</h2><p>"+limits+"</p><p>Exact calls/results are in Evidence/. The initial restricted Unity licensing failure is retained; a separately approved normal-profile retry succeeded without modifying licensing. Checks are technical component workflows, not full persisted app/restart acceptance.</p><p><a href=\"README.md\">Instructions</a> · <a href=\"showcase-manifest.json\">Payload integrity manifest</a></p></footer></main></html>");
        readme.Append("## Limits\n\n"+limits+"\n\nExact calls/results: `Evidence/`. The initial restricted licensing failure remains recorded; a separately approved normal-profile retry succeeded. No licence was modified or bypassed. Do not replay existing receipt paths. These are component workflows, not full persisted app/restart acceptance.\n");
        await WriteNew(Path.Combine(output,"START-HERE.html"),Encoding.UTF8.GetBytes(html.ToString()));
        await WriteNew(Path.Combine(output,"README.md"),Encoding.UTF8.GetBytes(readme.ToString()));
        var files=new List<object>();long bytes=0;
        foreach(var file in Directory.EnumerateFiles(output,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}).Order(StringComparer.Ordinal))
        {var length=new FileInfo(file).Length;bytes+=length;files.Add(new{path=Path.GetRelativePath(output,file).Replace('\\','/'),bytes=length,sha256=await HashFile(file)});}
        var manifest=new{schemaVersion=1,kind="actual_forgecore_existing_capabilities_showcase",createdUtc=DateTimeOffset.UtcNow,gameBuilds=7,newTemplateScenes=6,
            existingScenesReused=3,newBlenderSourceRecipes=3,sourceVisualReviewed=false,sourceRightsReviewed=false,
            fileCount=files.Count,totalBytes=bytes,files,qualification="Runtime previews and startup-checked development builds, not completed games, human acceptance, representative performance or full persisted autonomy."};
        await WriteNew(Path.Combine(output,"showcase-manifest.json"),JsonSerializer.SerializeToUtf8Bytes(manifest,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new{gameBuilds=7,newBlenderSourceRecipes=3,fileCount=files.Count,totalBytes=bytes,gallery=Path.Combine(output,"START-HERE.html")}));
    }
    private static async Task<string> HashFile(string path){await using var stream=File.OpenRead(path);return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();}
    private static async Task WriteNew(string path,byte[] bytes){await using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);await stream.WriteAsync(bytes);}
}
