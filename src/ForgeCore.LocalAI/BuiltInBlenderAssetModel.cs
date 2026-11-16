using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeCore.Core;

namespace ForgeCore.LocalAI;

/// <summary>Explicit bounded offline recipes. Unsupported Blender creation never falls through to cloud.</summary>
public sealed partial class BuiltInBlenderAssetModel(ILocalModel inner, string projectPath) : ILocalModel, IAsyncDisposable
{
    private static readonly Regex Prompt = new(@"^\s*(?:create|make|generate)\s+(?:an?\s+)?(?:local\s+)?(?:weathered\s+)?barrel\s+(?:in|using|with)\s+blender(?:\s+and\s+import\s+it)?(?:[.;]\s*|\s+)name\s+it\s+([a-z][a-z0-9_-]{1,47})[.!]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CharacterPrompt = new(@"^\s*(?:create|make|generate)\s+(?:an?\s+)?(?:original\s+)?(?<animated>animated\s+)?(?:rigged\s+mannequin|(?<security>industrial\s+security\s+character)|(?<combat>security\s+combat\s+lab)|(?<mission>industrial\s+infiltration\s+mission))\s+(?:in|using|with)\s+blender(?:\s+and\s+import\s+it)?(?:[.;]\s*|\s+)name\s+it\s+(?<name>[a-z][a-z0-9_-]{1,47})[.!]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex IndustrialPropPrompt=new(@"^\s*(?:create|make|generate)\s+(?:an?\s+)?(?<refined>refined\s+)?(?<prop>industrial\s+barrel|wooden\s+crate|concrete\s+barrier|industrial\s+fence|shipping\s+container)\s+(?:in|using|with)\s+blender(?:[.;]\s*|\s+)name\s+it\s+(?<name>[a-z][a-z0-9_-]{1,47})[.!]?\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    public ModelStatus Status => inner.Status;
    private static readonly Regex LibraryPrompt=new(@"^\s*(?:search|find)\s+local\s+blender\s+assets\s+for\s+(?<query>[a-z0-9][a-z0-9 _-]{1,119})[.!]?\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var courtyard=Regex.Match(request.UserMessage,@"^\s*(?:please\s+)?(?:create|make|generate)\s+(?:me\s+)?(?:an?\s+)?(?:(?:detailed|indie(?:-style)?|(?<retro>retro|n64-style|goldeneye(?:\s+007)?-style))\s+)?industrial\s+courtyard(?:\s+(?:named|called)\s+(?<name>[a-z][a-z0-9_-]{1,35}))?[.!]?\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        var retroStyle=courtyard.Success&&courtyard.Groups["retro"].Success;
        if(courtyard.Success)
            request=request with{UserMessage="Create an industrial infiltration mission in Blender. Name it "+(courtyard.Groups["name"].Success?courtyard.Groups["name"].Value.ToLowerInvariant():retroStyle?"retro-courtyard":"detailed-courtyard")+"."};
        else if(Regex.IsMatch(request.UserMessage,@"\b(?:create|make|generate)\b.*\bindustrial\s+courtyard\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
            return Task.FromResult(new AgentPlan("Use: Create a detailed industrial courtyard named graphics-yard. Or: Create a retro industrial courtyard named classic-yard. These are bounded FPS candidates, not arbitrary game generation; unsupported requests never reach inference.",[]));
        var simpleQuality = SimpleQualityRoute(request); if (simpleQuality is not null) return Task.FromResult(simpleQuality);
        var experienceGuided = ExperienceGuidedRoute(request, cancellationToken); if (experienceGuided is not null) return experienceGuided;
        var learning = LearningRoute(request); if (learning is not null) return Task.FromResult(learning);
        var targetReview=IndustrialTargetReviewRoute(request);if(targetReview is not null)return Task.FromResult(targetReview);
        var industrialPlan=IndustrialAssetRoute(request);if(industrialPlan is not null)return Task.FromResult(industrialPlan);
        var promotion=LocalPromotionRoute(request);if(promotion is not null)return Task.FromResult(promotion);
        var sourceReview=System.Text.RegularExpressions.Regex.Match(request.UserMessage,@"^\s*review\s+local\s+blender\s+asset\s+(?<hash>[a-f0-9]{64})\s+(?<kind>visually|rights)[.!]?\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        if(sourceReview.Success)return Task.FromResult(LocalReviewPlan(request,sourceReview.Groups["hash"].Value.ToLowerInvariant(),sourceReview.Groups["kind"].Value.Equals("rights",StringComparison.OrdinalIgnoreCase)?"rights":"visual"));
        if(Regex.IsMatch(request.UserMessage,@"\breview\s+local\s+blender\s+asset\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
            return Task.FromResult(new AgentPlan("Use: Review local Blender asset <exact 64-character bundle hash> visually. Or replace visually with rights. Each exact source-only decision requires your live review; unsupported syntax is not forwarded to inference.",[]));
        var library=LibraryPrompt.Match(request.UserMessage);
        if(library.Success)
        {
            var query=library.Groups["query"].Value.Trim().ToLowerInvariant();
            var found=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.SearchLocalBlenderAssets&&o.Arguments.TryGetValue("query",out var q)&&q==query);
            if(found is null)
            {
                var call=ToolCall.Create(ToolKind.SearchLocalBlenderAssets,PermissionKind.Read,"Search immutable local Blender candidates offline; no creation/import or approval.",("query",query),("max_results","10"));
                _=LocalBlenderSearchSpec.From(call);
                return Task.FromResult(new AgentPlan("I will search the local Blender source library only. Matches remain unreviewed candidates, not approved game assets.",[call]));
            }
            if(!found.Success)return Task.FromResult(new AgentPlan("Local Blender library search stopped; no assets were created or imported.",[]));
            try
            {
                using var result=JsonDocument.Parse(found.Detail);var root=result.RootElement;
                if(root.GetProperty("schemaVersion").GetInt32()!=1||root.GetProperty("library").GetString()!="local_blender_candidates")throw new JsonException();
                var count=root.GetProperty("results").GetArrayLength();if(count>10)throw new JsonException();
                foreach(var entry in root.GetProperty("results").EnumerateArray())
                    if(entry.GetProperty("reviewState").GetString()!="unreviewed_local_recipe"||entry.GetProperty("visualReviewed").GetBoolean()||entry.GetProperty("rightsReviewed").GetBoolean()||entry.GetProperty("placementApproved").GetBoolean())throw new JsonException();
                var partial=root.GetProperty("truncated").GetBoolean()?" Search limits were reached; this is not an exhaustive library result.":"";
                return Task.FromResult(new AgentPlan($"Found {count} hash-verified local Blender candidate(s)."+partial+" No source was changed or editor launched. Editable source is retained; separate exact import, target Unity inspection and human visual/rights review remain required. No asset was approved or automatically placed.",[]));
            }
            catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException)
            {return Task.FromResult(new AgentPlan("Local Blender search returned invalid evidence; no follow-up action will run.",[]));}
        }
        if(Regex.IsMatch(request.UserMessage,@"\b(?:search|find)\s+local\s+blender\s+assets\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
            return Task.FromResult(new AgentPlan("Use: Search local Blender assets for barrel. Queries accept bounded letters, numbers, spaces, hyphens and underscores only. No search was run and this unsupported request will not be forwarded to an inference model or provider.",[]));
        var match = Prompt.Match(request.UserMessage);
        var characterMatch = CharacterPrompt.Match(request.UserMessage);
        var propMatch=IndustrialPropPrompt.Match(request.UserMessage);
        IndustrialPropRecipe? prop=null;
        if(propMatch.Success) {
            var label=Regex.Replace(propMatch.Groups["prop"].Value.ToLowerInvariant(),@"\s+"," ");
            prop=IndustrialPropRecipes.Find(label switch{"industrial barrel"=>"industrial_barrel_v2","wooden crate"=>"wooden_crate_v1",
                "concrete barrier"=>"concrete_barrier_v1","industrial fence"=>"industrial_fence_v1","shipping container"=>"shipping_container_v1",_=>""});
            if(prop!=null&&propMatch.Groups["refined"].Success)prop=IndustrialPropRecipes.Refined.Single(r=>r.FamilyId==prop.Id);
            match=propMatch;
        }
        var character = characterMatch.Success;
        if(character) match=characterMatch;
        if (!match.Success)
        {
            if (Regex.IsMatch(request.UserMessage, @"\bblender\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                Regex.IsMatch(request.UserMessage, @"\b(create|make|generate)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return Task.FromResult(new AgentPlan("Use one fixed offline recipe: Create a weathered barrel in Blender. Name it yard-barrel. Or: Create a rigged mannequin in Blender. Name it training-enemy. Also supported: Create an industrial security character in Blender. Name it security-guard. Or: Create a security combat lab in Blender. Name it security-combat. These are bounded original procedural recipes and a technical training arena, not arbitrary realistic game generation or human quality approval. Arbitrary scripts and other recipes are not implemented; this request will not be sent to a cloud generator.", []));
            return inner.CreatePlanAsync(request, cancellationToken);
        }
        var mission=character&&characterMatch.Groups["mission"].Success;
        var combat=character && (characterMatch.Groups["combat"].Success||mission);
        var security=character && (characterMatch.Groups["security"].Success || combat);
        var animated=character && (characterMatch.Groups["animated"].Success || security);
        var name = (character||prop!=null?match.Groups["name"].Value:match.Groups[1].Value).ToLowerInvariant();
        var imported = request.Observations.LastOrDefault(x => x.Kind == ToolKind.ImportBlenderAsset);
        if (imported is not null)
        {
            if (!imported.Success) return Task.FromResult(new AgentPlan("The local Blender import stopped: " + imported.Detail, []));
            try { return Task.FromResult(InspectImported(request, imported, name, character, animated, combat,prop!=null,mission,courtyard.Success,retroStyle)); }
            catch (ArgumentException) { return Task.FromResult(new AgentPlan("The local import identity is invalid; Unity preparation stopped.", [])); }
        }
        var created = request.Observations.LastOrDefault(x => x.Kind == ToolKind.CreateBlenderAsset);
        if (created is null)
        {
            var fields = new List<(string Key,string Value)> {("project_path",projectPath),("asset_name",name),
                ("recipe",prop?.Id??(security?BlenderAssetSpec.SecurityCharacterRecipe:character?BlenderAssetSpec.CharacterRecipe:BlenderAssetSpec.SupportedRecipe)),("seed","41"),("height_mm",prop?.DefaultHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)??(character?"1800":"1100"))};
            if(!character&&(prop==null||prop.IsBarrel))fields.Add(("radius_mm","340"));
            var call = ToolCall.Create(ToolKind.CreateBlenderAsset, PermissionKind.Execute,
                prop!=null?$"Create or reuse the exact original {prop.Id} source, 1K PBR maps and two LOD companions; human acceptance remains pending.":security?"Create or reuse the original layered industrial-security character with 1K cloth PBR/normal maps; visual acceptance remains pending.":character?"Create or reuse the original local skinned technical mannequin; no motion clips or external assets.":
                "Create or reuse the exact original local weathered-barrel recipe; no paid generation or downloads.",fields.ToArray());
            _ = BlenderAssetSpec.From(call);
            return Task.FromResult(new AgentPlan("ForgeCore will run the selected bounded original recipe in installed offline Blender, or reuse the exact verified local source. Creation, import and Unity work use separate scoped approvals; human review remains pending.", [call]));
        }
        if (!created.Success) return Task.FromResult(new AgentPlan("Local Blender creation stopped; no import will run: " + created.Detail, []));
        try
        {
            using var receipt = JsonDocument.Parse(created.Detail);
            if(prop!=null&&(receipt.RootElement.GetProperty("recipe").GetString()!=prop.Id||receipt.RootElement.GetProperty("assetName").GetString()!=name))
                throw new ArgumentException("Industrial source identity mismatch.");
            var call = ToolCall.Create(ToolKind.ImportBlenderAsset, PermissionKind.Write,
                "Import the exact locally generated GLB and provenance without changing its reusable source.",
                ("project_path", projectPath), ("asset_name", name),
                ("bundle_sha256", receipt.RootElement.GetProperty("bundleSha256").GetString()!),
                ("expected_asset_sha256", receipt.RootElement.GetProperty("assetSha256").GetString()!));
            _ = BlenderAssetImportSpec.From(call);
            return Task.FromResult(new AgentPlan("The local source bundle is ready. ForgeCore will re-verify all bundle hashes and import only this exact GLB to a fresh Unity folder. No visual or rights approval is implied.", [call]));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or ArgumentException or InvalidOperationException)
        {
            return Task.FromResult(new AgentPlan("Blender returned no valid exact-hash receipt; no import will be proposed.", []));
        }
    }
    private AgentPlan InspectImported(ModelRequest request, ToolObservation imported, string name, bool character, bool animated, bool combat,bool industrial=false,bool mission=false,bool courtyardBenchmark=false,bool retroStyle=false)
    {
        var spec = BlenderAssetImportSpec.From(new(imported.ToolCallId, imported.Kind, imported.Arguments, PermissionKind.Write, imported.Description));
        if (spec.ProjectPath != projectPath || spec.AssetName != name) throw new ArgumentException("Import scope mismatch.");
        AgentPlan Step(ToolKind kind, PermissionKind permission, string description, params (string Key, string Value)[] fields) =>
            new(description, [ToolCall.Create(kind, permission, description, new[] { ("project_path", projectPath) }.Concat(fields).ToArray())]);
        var install = request.Observations.LastOrDefault(x => x.Kind == ToolKind.InstallUnityBridge);
        if (install is null) return Step(ToolKind.InstallUnityBridge, PermissionKind.Write, "Install or safely upgrade the hash-verified Unity inspection bridge; preserve unknown files.");
        if (!install.Success) return new("Local source and import are retained, but Unity bridge installation stopped: " + install.Detail, []);
        if(animated||industrial)
        {
            var kit=request.Observations.LastOrDefault(x=>x.Kind==ToolKind.ScaffoldUnityGameTemplate);
            if(kit is null)return Step(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Install the exact verified animation/template kit with protected supported-version backup; no unknown file overwrite.",("template_id","first_person_action"));
            if(!kit.Success)return new("Animation kit installation stopped: "+kit.Detail,[]);
        }
        ToolObservation? Find(string operation) => request.Observations.LastOrDefault(x => x.Kind == ToolKind.UnityEditorAction &&
            x.Arguments.TryGetValue("operation", out var value) && value == operation);
        var compile = Find("compile");
        if (compile is null) return Step(ToolKind.UnityEditorAction, PermissionKind.Execute, "Compile the trusted bridge and synchronously import the local GLB in Unity.", ("operation", "compile"));
        if (!compile.Success) return new("Local source and import are retained, but Unity compilation stopped: " + compile.Detail, []);
        if(industrial) {
            var prepared=Find("prepare_industrial_prop");
            if(prepared is null)return Step(ToolKind.UnityEditorAction,PermissionKind.Execute,"Prepare a fresh industrial prop review prefab with measured LOD/PBR/bounds/box collision; no human acceptance.",
                ("operation","prepare_industrial_prop"),("asset_path",spec.DestinationPath),("expected_sha256",spec.ExpectedAssetSha256),("asset_set_id",name),
                ("scene_path",$"Assets/Scenes/IndustrialProps/{name}-{spec.ExpectedAssetSha256[..12]}.unity"),("output_path",$"Previews/IndustrialProps/{name}-{spec.ExpectedAssetSha256[..12]}.png"));
            return new(prepared.Success?"Industrial Blender source and exact Unity 3-LOD/collision/PBR prefab/review scene retained. Source/target human reviews, suitable placement and game acceptance remain open.":"Industrial target preparation stopped: "+prepared.Detail,[]);
        }
        if(character)
        {
            var rig=Find("inspect_character_rig");
            if(rig is null)return Step(ToolKind.UnityEditorAction,PermissionKind.Execute,
                "Verify actual Unity skin weights/bind pose and shoulder/elbow/hip/knee deformation; create a fresh diagnostic scene and four-pose sheet, not a gameplay enemy.",
                ("operation","inspect_character_rig"),("asset_path",spec.DestinationPath),("expected_sha256",spec.ExpectedAssetSha256),
                ("scene_path",$"Assets/Scenes/CharacterRigs/{name}-{spec.ExpectedAssetSha256[..12]}.unity"),
                ("output_path",$"Previews/CharacterRigs/{name}-{spec.ExpectedAssetSha256[..12]}.png"));
            if(animated && rig.Success)
            {
                var operation=mission?"create_industrial_mission":combat?"create_security_combat_lab":"prepare_character_animation";
                var motion=Find(operation);
                if(motion!=null&&((motion.Arguments.GetValueOrDefault("style_family")==RetroCourtyardProfile.Id)!=retroStyle))return new("Mission style evidence does not match the requested profile. No default/retro substitution or follow-up export is allowed.",[]);
                if(courtyardBenchmark&&motion?.Success==true)return CourtyardDeliveryPlan(request,name,spec.ExpectedAssetSha256,retroStyle);
                if(mission&&motion?.Success==true)return new("Created a fresh bounded industrial mission and reusable courtyard graphics benchmark: nine 1K surface maps, industrial dressing and three fixed before/after views (left baseline, right candidate). Collision/resource/render sanity checks passed. The comparison is static editor evidence, not runtime animation, performance or human art acceptance. Greybox prop slots still await reviewed-source integration; this is not a finished realistic game.",[]);
                if(motion is null&&retroStyle)return Step(ToolKind.UnityEditorAction,PermissionKind.Execute,"Generate the opt-in original N64-era-inspired industrial candidate: low-poly rig derivative, small original textures, simplified lighting and corrected cover placement. Not a licensed game remake or reviewed library placement.",
                    ("operation",operation),("asset_path",spec.DestinationPath),("expected_sha256",spec.ExpectedAssetSha256),("asset_set_id",name+"-motion"),("style_family",RetroCourtyardProfile.Id),
                    ("scene_path",$"Assets/Scenes/CharacterAnimations/{name}-{spec.ExpectedAssetSha256[..12]}.unity"),("output_path",$"Previews/CharacterAnimations/{name}-{spec.ExpectedAssetSha256[..12]}.png"));
                if(motion is null)return Step(ToolKind.UnityEditorAction,PermissionKind.Execute,
                    combat?"Create a fresh bounded security combat lab with original Blender guard, extracted skinned arms, Unity-authored training rifle, three animated guards and static-grid navigation. Human visual/rights, play-feel and performance acceptance stay open.":
                    "Generate original Unity skeletal animation curves, blended controller and a fresh technical animation lab; Blender creates the base, Unity creates the clips. Human visual/rights and gameplay quality acceptance stay open.",
                    ("operation",operation),("asset_path",spec.DestinationPath),("expected_sha256",spec.ExpectedAssetSha256),("asset_set_id",name+"-motion"),
                    ("scene_path",$"Assets/Scenes/CharacterAnimations/{name}-{spec.ExpectedAssetSha256[..12]}.unity"),("output_path",$"Previews/CharacterAnimations/{name}-{spec.ExpectedAssetSha256[..12]}.png"));
                return new(motion.Success?(mission?"The authored industrial mission prototype has three ordered power/manifest/extraction objectives, gated encounters, cover, navigation and failure/reset. Its prop slots are explicitly greybox placeholders pending approved-source and target integration; this is not completed S09/S10 or a realistic finished game.":combat?"The original guard source, extracted skinned first-person arms, original Unity-authored training rifle and three-guard combat lab are retained. This bounded technical fixture is not an accepted detailed game or arbitrary game generator. Human visual/rights, play-feel and performance review remains pending.":"The original Blender character and eight original Unity-authored skeletal clips, blend tree, exclusive action driver and animation lab are retained. This is a technical animation foundation, not polished motion or tactical gameplay acceptance. Human visual/rights review remains pending."):"Animation generation stopped: "+motion.Detail,[]);
            }
            return rig.Success
                ? new("Original skinned mannequin source, exact GLB import and Unity skin/bind/pose diagnostics are retained, with a fresh review scene and four-pose sheet. Human visual/rights review remains pending. No detailed character art, Humanoid avatar/retargeting, locomotion/combat clips or existing-game enemy replacement is complete.",[])
                : new("The character source/import are retained, but rig diagnostics stopped: "+rig.Detail,[]);
        }
        var inspection = Find("inspect_game_art_asset");
        if (inspection is null) return Step(ToolKind.UnityEditorAction, PermissionKind.Execute, "Measure the exact local barrel's Unity geometry, PBR resources and canonical transforms; no approval is implied.",
            ("operation", "inspect_game_art_asset"), ("asset_path", spec.DestinationPath), ("expected_sha256", spec.ExpectedAssetSha256),
            ("asset_set_id", name), ("asset_role", "environment_props"), ("style_family", "realistic_pbr"));
        if (!inspection.Success || !inspection.Detail.Contains("structural_ready=true", StringComparison.OrdinalIgnoreCase))
            return new("Unity structural readiness did not pass; no scene placement or quality approval was made. " + inspection.Detail, []);
        var preview = Find("capture_game_art_preview");
        if (preview is null) return Step(ToolKind.UnityEditorAction, PermissionKind.Execute, "Render a fresh four-view Unity review sheet of the exact imported barrel.",
            ("operation", "capture_game_art_preview"), ("asset_path", spec.DestinationPath), ("expected_sha256", spec.ExpectedAssetSha256),
            ("asset_set_id", name), ("output_path", $"Previews/Blender/{name}-{spec.ExpectedAssetSha256[..12]}.png"));
        return preview.Success
            ? new("The original local Blender barrel was created or exact-source reused, retained as editable source, hash-verified and imported. Unity structural inspection and a four-view preview passed. Human visual/rights approval, collision, LOD, game-specific placement and runtime performance acceptance remain pending. No scene or existing game was changed by this asset request.", [])
            : new("The barrel source and Unity inspection are retained, but its review render failed: " + preview.Detail, []);
    }
    public async ValueTask DisposeAsync()
    {
        if (inner is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else if (inner is IDisposable disposable) disposable.Dispose();
    }
}
