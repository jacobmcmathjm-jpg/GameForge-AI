using System.Runtime.CompilerServices;
using ForgeCore.Core;
using ForgeCore.LocalAI;

namespace ForgeCore.Core.Tests;

public sealed class LocalModelTests
{
    [Fact]
    public void Parser_DerivesPermissionsFromToolKind()
    {
        var json = """{"Response":"I will inspect and update the note after approval.","Actions":[{"Kind":"read_text_file","Description":"Read","Arguments":{"path":"notes.txt"}},{"Kind":"write_text_file","Description":"Write","Arguments":{"path":"notes.txt","content":"updated"}}]}""";
        var plan = StructuredPlanParser.Parse(json, 4);
        Assert.Equal(PermissionKind.Read, plan.Actions[0].RequiredPermission);
        Assert.Equal(PermissionKind.Write, plan.Actions[1].RequiredPermission);
    }

    [Fact]
    public void Parser_AcceptsOnlyKnownProjectScaffolds()
    {
        var json = """{"Response":"I can create that console project after approval.","Actions":[{"Kind":"scaffold_project","Description":"Create project","Arguments":{"path":"apps/Demo","template":"dotnet-console","name":"Demo"}}]}""";
        var plan = StructuredPlanParser.Parse(json, 4);
        Assert.Equal(ToolKind.ScaffoldProject, plan.Actions.Single().Kind);
        Assert.Equal(PermissionKind.Write, plan.Actions.Single().RequiredPermission);
        var invalid = json.Replace("dotnet-console", "powershell-project", StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(invalid, 4));
    }

    [Fact]
    public void Parser_DerivesNetworkPermissionAndRejectsActiveDownloads()
    {
        var json = """{"Response":"I found a CC0 model and can stage it after approval.","Actions":[{"Kind":"stage_web_asset","Description":"Stage tree model","Arguments":{"project_path":"Game","file_name":"tree.glb","url":"https://assets.example/tree.glb","source_page":"https://assets.example/tree","license":"CC0-1.0"}}]}""";
        var plan = StructuredPlanParser.Parse(json, 4);
        Assert.Equal(PermissionKind.Network, plan.Actions.Single().RequiredPermission);
        var executable = json.Replace("tree.glb", "installer.exe", StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(executable, 4));
    }

    [Fact]
    public void Parser_AcceptsBoundedAssetCatalogueSearch()
    {
        var json = """{"Response":"I can search approved public asset metadata after network approval.","Actions":[{"Kind":"search_asset_catalog","Description":"Find forest textures","Arguments":{"query":"stylized forest texture","media_type":"image","license_filter":"cc0","max_results":"6"}}]}""";
        var plan = StructuredPlanParser.Parse(json, 2);
        Assert.Equal(ToolKind.SearchAssetCatalog, plan.Actions.Single().Kind);
        Assert.Equal(PermissionKind.Network, plan.Actions.Single().RequiredPermission);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"image\"", "\"archive\"", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"6\"", "\"20\"", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedTyped3DGeneration()
    {
        var json = """{"Response":"I can generate that original Unity prop after cloud approval.","Actions":[{"Kind":"generate_3d_asset","Description":"Generate original watchtower","Arguments":{"project_path":"Games/ForgeCore","provider":"seele","asset_name":"frontier-watchtower","prompt":"An original weathered timber ranger watchtower with efficient topology and realistic PBR materials.","model":"Seele02-flash"}}]}""";
        var plan = StructuredPlanParser.Parse(json, 2);
        var call = plan.Actions.Single(); var spec = Generated3DAssetSpec.From(call);
        Assert.Equal(ToolKind.Generate3DAsset, call.Kind); Assert.Equal(PermissionKind.Network, call.RequiredPermission);
        Assert.Equal("frontier-watchtower", spec.AssetName); Assert.Equal("Seele02-flash", spec.Model);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("seele", "unknown", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Seele02-flash", "unbounded-model", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("frontier-watchtower", "../escape", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("An original weathered timber ranger watchtower with efficient topology and realistic PBR materials.", "too short", StringComparison.Ordinal), 2));

        var meshyJson = json.Replace("\"provider\":\"seele\"", "\"provider\":\"meshy\"", StringComparison.Ordinal)
            .Replace("Seele02-flash", "meshy-6", StringComparison.Ordinal);
        var meshy = Generated3DAssetSpec.From(StructuredPlanParser.Parse(meshyJson, 2).Actions.Single());
        Assert.Equal("meshy", meshy.Provider); Assert.Equal("meshy-6", meshy.Model);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(meshyJson.Replace("meshy-6", "Seele02-flash", StringComparison.Ordinal), 2));
        var oversizedMeshy = meshyJson.Replace("An original weathered timber ranger watchtower with efficient topology and realistic PBR materials.", new string('x', 301), StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(oversizedMeshy, 2));
    }

    [Fact]
    public void Parser_RequiresInspectionBeforeTypedHashBoundAssetImport()
    {
        var hash = new string('a', 64);
        var json = """{"Response":"I will inspect the quarantined asset, then request separate import approval.","Actions":[{"Kind":"inspect_staged_asset","Description":"Inspect staged texture","Arguments":{"project_path":"Game","file_name":"forest.png"}},{"Kind":"import_staged_asset","Description":"Import verified texture","Arguments":{"project_path":"Game","file_name":"forest.png","destination_path":"Assets/Art/forest.png","expected_sha256":"HASH"}}]}"""
            .Replace("HASH", hash, StringComparison.Ordinal);
        var plan = StructuredPlanParser.Parse(json, 4);

        Assert.Equal([ToolKind.InspectStagedAsset, ToolKind.ImportStagedAsset], plan.Actions.Select(x => x.Kind));
        Assert.Equal(PermissionKind.Read, plan.Actions[0].RequiredPermission);
        Assert.Equal(PermissionKind.Write, plan.Actions[1].RequiredPermission);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace(hash, "bad", StringComparison.Ordinal), 4));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Assets/Art/forest.png", "Assets/Art/renamed.png", StringComparison.Ordinal), 4));
    }

    [Fact]
    public void Parser_AcceptsOnlyTypedHashBoundLocalAssetVaultWorkflow()
    {
        var assetHash = new string('a', 64); var bundleHash = new string('b', 64);
        var json = """{"Response":"I will search local reviewed assets before creating another model.","Actions":[{"Kind":"search_asset_vault","Description":"Find a reviewed watchtower","Arguments":{"query":"frontier watchtower","asset_role":"environment_props","style_family":"realistic_pbr","max_results":"5"}},{"Kind":"store_reviewed_asset","Description":"Store the exact approved source asset","Arguments":{"project_path":"Games/ForgeCore","asset_set_id":"frontier-watchtower","expected_asset_sha256":"ASSET_HASH","tags":"watchtower,frontier"}},{"Kind":"import_vault_asset","Description":"Import the exact immutable bundle","Arguments":{"project_path":"Games/NewGame","bundle_sha256":"BUNDLE_HASH","expected_asset_sha256":"ASSET_HASH","destination_directory":"Assets/ForgeCore/Generated/Vault/frontier-watchtower"}}]}"""
            .Replace("ASSET_HASH", assetHash, StringComparison.Ordinal).Replace("BUNDLE_HASH", bundleHash, StringComparison.Ordinal);
        var plan = StructuredPlanParser.Parse(json, 4);

        Assert.Equal([ToolKind.SearchAssetVault, ToolKind.StoreReviewedAsset, ToolKind.ImportVaultAsset], plan.Actions.Select(x => x.Kind));
        Assert.Equal([PermissionKind.Read, PermissionKind.Write, PermissionKind.Write], plan.Actions.Select(x => x.RequiredPermission));
        Assert.Equal("environment_props", AssetVaultSearchSpec.From(plan.Actions[0]).AssetRole);
        Assert.Equal(["frontier", "watchtower"], ReviewedAssetStoreSpec.From(plan.Actions[1]).Tags);
        Assert.Equal(bundleHash, VaultAssetImportSpec.From(plan.Actions[2]).BundleSha256);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace(bundleHash, "bad", StringComparison.Ordinal), 4));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Assets/ForgeCore/Generated/Vault/frontier-watchtower", "../Outside", StringComparison.Ordinal), 4));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("watchtower,frontier", "one-character,x", StringComparison.Ordinal), 4));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedTypedUnityActions()
    {
        var json = """{"Response":"I will install the bridge, create a scene, compile, and run tests with separate approvals.","Actions":[{"Kind":"install_unity_bridge","Description":"Install deterministic bridge","Arguments":{"project_path":"Games/ForestRun"}},{"Kind":"unity_editor_action","Description":"Create basic scene","Arguments":{"project_path":"Games/ForestRun","operation":"create_scene","scene_path":"Assets/Scenes/Main.unity","scene_template":"basic_3d"}},{"Kind":"unity_editor_action","Description":"Compile scripts","Arguments":{"project_path":"Games/ForestRun","operation":"compile"}},{"Kind":"unity_editor_action","Description":"Run EditMode tests","Arguments":{"project_path":"Games/ForestRun","operation":"run_tests","test_mode":"edit"}}]}""";
        var plan = StructuredPlanParser.Parse(json, 6);

        Assert.Equal([ToolKind.InstallUnityBridge, ToolKind.UnityEditorAction, ToolKind.UnityEditorAction, ToolKind.UnityEditorAction], plan.Actions.Select(x => x.Kind));
        Assert.Equal(PermissionKind.Write, plan.Actions[0].RequiredPermission);
        Assert.All(plan.Actions.Skip(1), x => Assert.Equal(PermissionKind.Execute, x.RequiredPermission));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Assets/Scenes/Main.unity", "../Outside.unity", StringComparison.Ordinal), 6));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("basic_3d", "arbitrary_script", StringComparison.Ordinal), 6));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"test_mode\":\"edit\"", "\"test_mode\":\"edit\",\"command\":\"powershell\"", StringComparison.Ordinal), 6));
    }

    [Fact]
    public void Parser_AcceptsOnlyHashBoundGeneratedAssetPreparation()
    {
        var hash = new string('a', 64);
        var json = """{"Response":"I can prepare the exact imported model after Unity approval.","Actions":[{"Kind":"unity_editor_action","Description":"Prepare generated watchtower","Arguments":{"project_path":"Games/ForgeCore","operation":"prepare_generated_asset","scene_path":"Assets/Scenes/FrontierValley.unity","asset_path":"Assets/ForgeCore/Generated/frontier-watchtower.glb","expected_sha256":"HASH","parent_object_name":"Forest Watchtower","object_name":"Generated Frontier Watchtower","target_height":"16","collider_mode":"mesh","replace_placeholder":"true"}}]}"""
            .Replace("HASH", hash, StringComparison.Ordinal);
        var spec = UnityEditorActionSpec.From(StructuredPlanParser.Parse(json, 2).Actions.Single());

        Assert.Equal("prepare_generated_asset", spec.Operation);
        Assert.Equal("Assets/ForgeCore/Generated/frontier-watchtower.glb", spec.AssetPath);
        Assert.Equal(16, spec.TargetHeight);
        Assert.Equal("mesh", spec.ColliderMode);
        Assert.True(spec.ReplacePlaceholder);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace(hash, "bad", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Assets/ForgeCore/Generated/frontier-watchtower.glb", "../outside.glb", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"16\"", "\"101\"", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"mesh\"", "\"script\"", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedHashBoundGameArtInspection()
    {
        var hash = new string('b', 64);
        var json = """{"Response":"I can collect local structural evidence after Unity approval.","Actions":[{"Kind":"unity_editor_action","Description":"Inspect generated watchtower","Arguments":{"project_path":"Games/ForgeCore","operation":"inspect_game_art_asset","asset_path":"Assets/ForgeCore/Generated/frontier-watchtower.glb","expected_sha256":"HASH","asset_set_id":"frontier-watchtower","asset_role":"environment_props","style_family":"realistic_pbr"}}]}"""
            .Replace("HASH", hash, StringComparison.Ordinal);
        var spec = UnityEditorActionSpec.From(StructuredPlanParser.Parse(json, 2).Actions.Single());

        Assert.Equal("inspect_game_art_asset", spec.Operation);
        Assert.Equal("frontier-watchtower", spec.AssetSetId);
        Assert.Equal("environment_props", spec.AssetRole);
        Assert.Equal("realistic_pbr", spec.StyleFamily);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace(hash, "bad", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("environment_props", "arbitrary_role", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("realistic_pbr", "unbounded_style", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"style_family\":\"realistic_pbr\"", "\"style_family\":\"realistic_pbr\",\"command\":\"powershell\"", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedHashBoundGameArtPreview()
    {
        var hash = new string('c', 64);
        var json = """{"Response":"I can create a local review sheet after Unity approval.","Actions":[{"Kind":"unity_editor_action","Description":"Capture generated watchtower preview","Arguments":{"project_path":"Games/ForgeCore","operation":"capture_game_art_preview","asset_path":"Assets/ForgeCore/Generated/frontier-watchtower.glb","expected_sha256":"HASH","asset_set_id":"frontier-watchtower","output_path":"Previews/GameArt/frontier-watchtower.png"}}]}"""
            .Replace("HASH", hash, StringComparison.Ordinal);
        var spec = UnityEditorActionSpec.From(StructuredPlanParser.Parse(json, 2).Actions.Single());

        Assert.Equal("capture_game_art_preview", spec.Operation);
        Assert.Equal("frontier-watchtower", spec.AssetSetId);
        Assert.Equal("Previews/GameArt/frontier-watchtower.png", spec.OutputPath);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace(hash, "bad", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Previews/GameArt/frontier-watchtower.png", "Builds/frontier-watchtower.png", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"output_path\":\"Previews/GameArt/frontier-watchtower.png\"", "\"output_path\":\"Previews/GameArt/frontier-watchtower.png\",\"command\":\"powershell\"", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void Parser_CannotEmitHumanGameArtAttestations()
    {
        var hash = new string('a', 64);
        var json = """{"Response":"I approve it.","Actions":[{"Kind":"review_game_art_asset","Description":"Self-certify generated art","Arguments":{"project_path":"Games/ForgeCore","review_kind":"visual","asset_set_id":"frontier-watchtower","asset_path":"Assets/ForgeCore/Generated/frontier-watchtower.glb","expected_asset_sha256":"HASH","preview_path":"Previews/GameArt/frontier-watchtower.png","expected_preview_sha256":"HASH","expected_preview_provenance_sha256":"HASH"}}]}"""
            .Replace("HASH", hash, StringComparison.Ordinal);

        var error = Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json, 2));
        Assert.Contains("Unknown tool kind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_AcceptsOnlyTypedFrontierValleyMapWorkflow()
    {
        var json = """{"Response":"I can install and create that map after separate approvals.","Actions":[{"Kind":"scaffold_unity_map_demo","Description":"Install map kit","Arguments":{"project_path":"Games/Frontier","map_template":"frontier_valley"}},{"Kind":"unity_editor_action","Description":"Create seeded map","Arguments":{"project_path":"Games/Frontier","operation":"create_map_demo","scene_path":"Assets/Scenes/FrontierValley.unity","map_template":"frontier_valley","seed":"240730"}}]}""";
        var plan = StructuredPlanParser.Parse(json, 3);
        Assert.Equal([ToolKind.ScaffoldUnityMapDemo, ToolKind.UnityEditorAction], plan.Actions.Select(x => x.Kind));
        Assert.Equal(PermissionKind.Write, plan.Actions[0].RequiredPermission);
        Assert.Equal(PermissionKind.Execute, plan.Actions[1].RequiredPermission);
        var spec = UnityEditorActionSpec.From(plan.Actions[1]);
        Assert.Equal("frontier_valley", spec.MapTemplate); Assert.Equal(240730, spec.Seed);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("frontier_valley", "arbitrary_map", StringComparison.Ordinal), 3));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("240730", "1000000", StringComparison.Ordinal), 3));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Assets/Scenes/FrontierValley.unity", "../Outside.unity", StringComparison.Ordinal), 3));
    }

    [Fact]
    public void Parser_AcceptsOnlyReviewedUnityGameTemplateWorkflow()
    {
        var json = """{"Response":"I can install and create that reviewed prototype after separate approvals.","Actions":[{"Kind":"scaffold_unity_game_template","Description":"Install prototype kit","Arguments":{"project_path":"Games/Prototype","template_id":"first_person_action"}},{"Kind":"unity_editor_action","Description":"Create prototype scene","Arguments":{"project_path":"Games/Prototype","operation":"create_game_template","scene_path":"Assets/Scenes/FpsPrototype.unity","template_id":"first_person_action","seed":"4812"}}]}""";
        var plan = StructuredPlanParser.Parse(json, 3);

        Assert.Equal([ToolKind.ScaffoldUnityGameTemplate, ToolKind.UnityEditorAction], plan.Actions.Select(x => x.Kind));
        Assert.Equal(PermissionKind.Write, plan.Actions[0].RequiredPermission);
        Assert.Equal(PermissionKind.Execute, plan.Actions[1].RequiredPermission);
        Assert.Equal("first_person_action", UnityGameTemplateSpec.From(plan.Actions[0]).TemplateId);
        var action = UnityEditorActionSpec.From(plan.Actions[1]);
        Assert.Equal("first_person_action", action.TemplateId); Assert.Equal(4812, action.Seed);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("first_person_action", "unreviewed_genre", StringComparison.Ordinal), 3));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"seed\":\"4812\"", "\"seed\":\"4812\",\"command\":\"powershell\"", StringComparison.Ordinal), 3));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Assets/Scenes/FpsPrototype.unity", "../Outside.unity", StringComparison.Ordinal), 3));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedLinkedUnityCampaignWorkflow()
    {
        var json = """{"Response":"I can create three linked reviewed levels after approval.","Actions":[{"Kind":"unity_editor_action","Description":"Create linked campaign","Arguments":{"project_path":"Games/Prototype","operation":"create_game_campaign","scene_path":"Assets/Scenes/ForgeCampaign-Level01.unity","template_id":"first_person_action","seed":"84001","level_count":"3"}}]}""";
        var action = StructuredPlanParser.Parse(json, 2).Actions.Single();
        var spec = UnityEditorActionSpec.From(action);

        Assert.Equal("create_game_campaign", spec.Operation);
        Assert.Equal("first_person_action", spec.TemplateId);
        Assert.Equal(84001, spec.Seed);
        Assert.Equal(3, spec.LevelCount);
        Assert.Equal([
            "Assets/Scenes/ForgeCampaign-Level01.unity",
            "Assets/Scenes/ForgeCampaign-Level02.unity",
            "Assets/Scenes/ForgeCampaign-Level03.unity"
        ], spec.GetCampaignScenePaths());

        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"3\"", "\"0\"", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"3\"", "\"13\"", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("ForgeCampaign-Level01.unity", "ForgeCampaign.unity", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("ForgeCampaign-Level01.unity", "../ForgeCampaign-Level01.unity", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"level_count\":\"3\"", "\"level_count\":\"3\",\"command\":\"powershell\"", StringComparison.Ordinal), 2));

        var build = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "build linked campaign",
            ("project_path", "Games/Prototype"), ("operation", "build_windows"),
            ("output_path", "Builds/Campaign/Campaign.exe"), ("development_build", "true"),
            ("scene_path", "Assets/Scenes/ForgeCampaign-Level01.unity"), ("level_count", "3"));
        var buildSpec = UnityEditorActionSpec.From(build);
        Assert.Equal(3, buildSpec.LevelCount);
        Assert.Equal(spec.GetCampaignScenePaths(), buildSpec.GetCampaignScenePaths());
        Assert.Throws<ArgumentException>(() => UnityEditorActionSpec.From(build with
        {
            Arguments = new Dictionary<string, string>(build.Arguments) { ["scene_path"] = "Assets/Scenes/ForgeCampaign.unity" }
        }));
        var withoutScene = build.Arguments.Where(pair => pair.Key != "scene_path").ToDictionary();
        Assert.Throws<ArgumentException>(() => UnityEditorActionSpec.From(build with { Arguments = withoutScene }));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedUnityPlayerSmokeTests()
    {
        var json = """{"Response":"I can launch the exact ForgeCore build for a bounded smoke test after approval.","Actions":[{"Kind":"unity_player_smoke_test","Description":"Smoke test player","Arguments":{"project_path":"Games/Prototype","output_path":"Builds/Prototype/Prototype.exe","duration_seconds":"15"}}]}""";
        var action = StructuredPlanParser.Parse(json, 2).Actions.Single();

        Assert.Equal(ToolKind.UnityPlayerSmokeTest, action.Kind);
        Assert.Equal(PermissionKind.Execute, action.RequiredPermission);
        var spec = UnityPlayerSmokeTestSpec.From(action);
        Assert.Equal("Games/Prototype", spec.ProjectPath);
        Assert.Equal("Builds/Prototype/Prototype.exe", spec.OutputPath);
        Assert.Equal(15, spec.DurationSeconds);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Builds/Prototype/Prototype.exe", "../Prototype.exe", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"15\"", "\"31\"", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"duration_seconds\":\"15\"", "\"duration_seconds\":\"15\",\"command\":\"powershell\"", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedUnityPlayerProfileTests()
    {
        var json = """{"Response":"I can profile the exact rendered ForgeCore build after approval.","Actions":[{"Kind":"unity_player_profile_test","Description":"Profile rendered player","Arguments":{"project_path":"Games/Prototype","output_path":"Builds/Prototype/Prototype.exe","duration_seconds":"15"}}]}""";
        var action = StructuredPlanParser.Parse(json, 2).Actions.Single();

        Assert.Equal(ToolKind.UnityPlayerProfileTest, action.Kind);
        Assert.Equal(PermissionKind.Execute, action.RequiredPermission);
        var spec = UnityPlayerProfileTestSpec.From(action);
        Assert.Equal("Games/Prototype", spec.ProjectPath);
        Assert.Equal("Builds/Prototype/Prototype.exe", spec.OutputPath);
        Assert.Equal(15, spec.DurationSeconds);
        Assert.Equal(1280, UnityPlayerProfileTestSpec.Width); Assert.Equal(720, UnityPlayerProfileTestSpec.Height);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Builds/Prototype/Prototype.exe", "../Prototype.exe", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"15\"", "\"31\"", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"duration_seconds\":\"15\"", "\"duration_seconds\":\"15\",\"command\":\"powershell\"", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedNonOverwritingUnityScenePreviews()
    {
        var json = """{"Response":"I can capture one local runtime preview after approval.","Actions":[{"Kind":"unity_editor_action","Description":"Capture scene preview","Arguments":{"project_path":"Games/Prototype","operation":"capture_scene_preview","scene_path":"Assets/Scenes/ForgeCampaign-Level01.unity","output_path":"Previews/ForgeCampaign-Level01.png","duration_seconds":"5"}}]}""";
        var action = StructuredPlanParser.Parse(json, 2).Actions.Single();
        var spec = UnityEditorActionSpec.From(action);

        Assert.Equal("capture_scene_preview", spec.Operation);
        Assert.Equal("Assets/Scenes/ForgeCampaign-Level01.unity", spec.ScenePath);
        Assert.Equal("Previews/ForgeCampaign-Level01.png", spec.OutputPath);
        Assert.Equal(5, spec.DurationSeconds);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Previews/ForgeCampaign-Level01.png", "Builds/ForgeCampaign.png", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("Previews/ForgeCampaign-Level01.png", "Previews/../ForgeCampaign.png", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"5\"", "\"31\"", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"duration_seconds\":\"5\"", "\"duration_seconds\":\"5\",\"command\":\"powershell\"", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedTypedRuntimeGameplayVerification()
    {
        var json = """{"Response":"I can verify the serialized runtime gameplay contract after approval.","Actions":[{"Kind":"unity_editor_action","Description":"Verify gameplay","Arguments":{"project_path":"Games/Prototype","operation":"verify_gameplay","scene_path":"Assets/Scenes/Platformer.unity","duration_seconds":"5"}}]}""";
        var action = StructuredPlanParser.Parse(json, 2).Actions.Single();
        var spec = UnityEditorActionSpec.From(action);

        Assert.Equal("verify_gameplay", spec.Operation);
        Assert.Equal("Assets/Scenes/Platformer.unity", spec.ScenePath);
        Assert.Equal(5, spec.DurationSeconds);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"5\"", "\"1\"", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"duration_seconds\":\"5\"", "\"duration_seconds\":\"5\",\"output_path\":\"Previews/X.png\"", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedTypedRuntimeSceneProfiling()
    {
        var json = """{"Response":"I can collect a repeatable local Editor baseline after approval.","Actions":[{"Kind":"unity_editor_action","Description":"Profile scene","Arguments":{"project_path":"Games/Prototype","operation":"profile_scene","scene_path":"Assets/Scenes/ForgeCampaign-Level02.unity","duration_seconds":"10"}}]}""";
        var action = StructuredPlanParser.Parse(json, 2).Actions.Single();
        var spec = UnityEditorActionSpec.From(action);

        Assert.Equal("profile_scene", spec.Operation);
        Assert.Equal("Assets/Scenes/ForgeCampaign-Level02.unity", spec.ScenePath);
        Assert.Equal(10, spec.DurationSeconds);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"10\"", "\"4\"", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"10\"", "\"31\"", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"duration_seconds\":\"10\"", "\"duration_seconds\":\"10\",\"output_path\":\"Previews/X.png\"", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void UnitySpec_RejectsUnboundedPlayBuildAndComponentArguments()
    {
        var play = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "play",
            ("project_path", "Game"), ("operation", "play_mode"), ("scene_path", "Assets/Main.unity"), ("duration_seconds", "31"));
        var build = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "build",
            ("project_path", "Game"), ("operation", "build_windows"), ("output_path", "../Game.exe"), ("development_build", "true"));
        var component = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "component",
            ("project_path", "Game"), ("operation", "add_component"), ("scene_path", "Assets/Main.unity"),
            ("object_name", "Player"), ("component_type", "System.Diagnostics.Process;Start"));

        Assert.Throws<ArgumentException>(() => UnityEditorActionSpec.From(play));
        Assert.Throws<ArgumentException>(() => UnityEditorActionSpec.From(build));
        Assert.Throws<ArgumentException>(() => UnityEditorActionSpec.From(component));
    }

    [Fact]
    public void Parser_AcceptsOnlyBoundedTypedUnityMcpActions()
    {
        var discoverJson = """{"Response":"I will discover the official Unity MCP tools after approval.","Actions":[{"Kind":"unity_mcp_action","Description":"Discover Unity tools","Arguments":{"project_path":"Games/ForestRun","operation":"discover"}}]}""";
        var discover = StructuredPlanParser.Parse(discoverJson, 2).Actions.Single();
        Assert.Equal(ToolKind.UnityMcpAction, discover.Kind);
        Assert.Equal(PermissionKind.Execute, discover.RequiredPermission);

        var callJson = """{"Response":"I can apply the approved polish action.","Actions":[{"Kind":"unity_mcp_action","Description":"Polish scene lighting","Arguments":{"project_path":"Games/ForestRun","operation":"call","tool_name":"scene_polish","arguments_json":"{\"scene\":\"Assets/Main.unity\"}"}}]}""";
        var call = StructuredPlanParser.Parse(callJson, 2).Actions.Single();
        var spec = UnityMcpActionSpec.From(call);
        Assert.Equal("scene_polish", spec.ToolName);
        Assert.Equal("{\"scene\":\"Assets/Main.unity\"}", spec.ArgumentsJson);

        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(callJson.Replace("{\\\"scene\\\":\\\"Assets/Main.unity\\\"}", "[]", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(callJson.Replace("scene_polish", "scene polish", StringComparison.Ordinal), 2));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(discoverJson.Replace("\"operation\":\"discover\"", "\"operation\":\"discover\",\"tool_name\":\"anything\"", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void Parser_AcceptsPatchAndDedicatedDotnetActions()
    {
        var json = """{"Response":"I can patch and build that after approval.","Actions":[{"Kind":"apply_text_patch","Description":"Fix greeting","Arguments":{"path":"Program.cs","old_text":"Hello","new_text":"Hello Jacob"}},{"Kind":"dotnet_build","Description":"Compile","Arguments":{"project_path":"Game.csproj","configuration":"Release"}}]}""";
        var plan = StructuredPlanParser.Parse(json, 4);
        Assert.Equal([ToolKind.ApplyTextPatch, ToolKind.DotnetBuild], plan.Actions.Select(x => x.Kind));
        Assert.Equal(PermissionKind.Write, plan.Actions[0].RequiredPermission);
        Assert.Equal(PermissionKind.Execute, plan.Actions[1].RequiredPermission);
        var invalid = json.Replace("Release", "Anything", StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(invalid, 4));
    }

    [Fact]
    public void Parser_AcceptsSnapshotRestoreOnlyWithCurrentHash()
    {
        var hash = new string('a', 64);
        var json = """{"Response":"I can restore that backup after approval.","Actions":[{"Kind":"restore_text_snapshot","Description":"Undo edit","Arguments":{"path":"Program.cs","snapshot_path":".forgecore/backups/abc/Program.cs.bak","expected_current_sha256":"HASH"}}]}"""
            .Replace("HASH", hash, StringComparison.Ordinal);
        var plan = StructuredPlanParser.Parse(json, 2);
        Assert.Equal(ToolKind.RestoreTextSnapshot, plan.Actions.Single().Kind);
        Assert.Equal(PermissionKind.Write, plan.Actions.Single().RequiredPermission);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace(hash, "bad", StringComparison.Ordinal), 2));
    }

    [Fact]
    public void Parser_AcceptsBoundedProjectIndexAndSearchActions()
    {
        var json = """{"Response":"I will index and search the project.","Actions":[{"Kind":"index_project","Description":"Index project metadata","Arguments":{"path":"Game"}},{"Kind":"search_project","Description":"Find player movement","Arguments":{"path":"Game","query":"PlayerController movement","max_results":"8"}}]}""";
        var plan = StructuredPlanParser.Parse(json, 4);
        Assert.Equal([ToolKind.IndexProject, ToolKind.SearchProject], plan.Actions.Select(x => x.Kind));
        Assert.All(plan.Actions, x => Assert.Equal(PermissionKind.Read, x.RequiredPermission));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"8\"", "\"100\"", StringComparison.Ordinal), 4));
    }

    [Theory]
    [InlineData("{\"Response\":\"x\",\"Unexpected\":1,\"Actions\":[]}")]
    [InlineData("{\"Response\":\"x\",\"Actions\":[{\"Kind\":\"list_files\",\"Description\":\"bad\",\"Permission\":\"Administrator\",\"Arguments\":{\"path\":\".\"}}]}")]
    [InlineData("{\"Response\":\"x\",\"Response\":\"y\",\"Actions\":[]}")]
    [InlineData("{\"Response\":\"x\",\"Actions\":[{\"Kind\":\"delete_file\",\"Description\":\"bad\",\"Arguments\":{\"path\":\"x\"}}]}")]
    [InlineData("```json\n{\"Response\":\"x\",\"Actions\":[]}\n```")]
    public void Parser_RejectsUnexpectedOrNonJsonOutput(string output) => Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(output, 12));

    [Fact]
    public void Parser_RejectsActionsOverLimit()
    {
        var json = """{"Response":"too many","Actions":[{"Kind":"list_files","Description":"one","Arguments":{"path":"."}},{"Kind":"list_files","Description":"two","Arguments":{"path":"."}}]}""";
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json, 1));
    }

    [Fact]
    public async Task Model_CollectsStreamingJsonIntoTypedPlan()
    {
        var backend = new FakeBackend(["{\"Response\":\"I can list those files.\",", "\"Actions\":[{\"Kind\":\"list_files\",\"Description\":\"Inspect\",\"Arguments\":{\"path\":\".\"}}]}"]);
        await using var model = new LlamaSharpLocalModel(backend, new LocalModelOptions());
        var observation = new ToolObservation(Guid.NewGuid(), ToolKind.DotnetBuild, "Build project",
            new Dictionary<string, string> { ["project_path"] = "App.csproj", ["configuration"] = "Release" },
            "Failed", false, "CS1002: ; expected");
        var plan = await model.CreatePlanAsync(new ModelRequest("show files", [new("user", "Earlier context")], [observation], 2, 7), default);
        Assert.Single(plan.Actions); Assert.Equal(ToolKind.ListFiles, plan.Actions[0].Kind); Assert.Contains("show files", backend.LastPrompt);
        Assert.Contains("Earlier context", backend.LastPrompt);
        Assert.Contains("planning pass 2", backend.LastPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CS1002", backend.LastPrompt, StringComparison.Ordinal);
        Assert.Contains("App.csproj", backend.LastPrompt, StringComparison.Ordinal);
        Assert.Contains("7 actions remaining", backend.LastPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Model_ReceivesDeterministicGenreAndSystemPackContext()
    {
        var backend = new FakeBackend(["{\"Response\":\"I can plan that game.\",\"Actions\":[]}"]);
        await using var model = new LlamaSharpLocalModel(backend, new LocalModelOptions());

        await model.CreatePlanAsync(new ModelRequest(
            "Create an online co-op kart racing game with controller support.", []), default);

        Assert.Contains("primary genre: racing", backend.LastPrompt, StringComparison.Ordinal);
        Assert.Contains("vehicle_physics", backend.LastPrompt, StringComparison.Ordinal);
        Assert.Contains("networking", backend.LastPrompt, StringComparison.Ordinal);
        Assert.Contains("controller_support", backend.LastPrompt, StringComparison.Ordinal);
        Assert.Contains("grants no tool permissions", backend.LastPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Model_EnforcesResponseCharacterLimit()
    {
        await using var model = new LlamaSharpLocalModel(new FakeBackend([new string('x', 20)]), new LocalModelOptions { MaxResponseCharacters = 10 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.CreatePlanAsync(new ModelRequest("request", []), default));
    }

    [Fact]
    public async Task Model_PropagatesStreamingCancellation()
    {
        await using var model = new LlamaSharpLocalModel(new FakeBackend(["unused"], waitForCancellation: true), new LocalModelOptions());
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.CreatePlanAsync(new ModelRequest("request", []), source.Token));
    }

    [Fact]
    public async Task Model_RejectsBlankRequestsBeforeInference()
    {
        var backend = new FakeBackend([]); await using var model = new LlamaSharpLocalModel(backend, new LocalModelOptions());
        await Assert.ThrowsAsync<ArgumentException>(() => model.CreatePlanAsync(new ModelRequest("  ", []), default)); Assert.Equal(string.Empty, backend.LastPrompt);
    }

    [Fact]
    public async Task BuiltInUnityDiagnostics_ProducesImmediateApprovedModelCheckAndCompletesFromObservation()
    {
        var fallback = new CountingModel();
        await using var model = new BuiltInUnityDiagnosticsModel(fallback, "Games/ForgeCore");
        var plan = await model.CreatePlanAsync(new ModelRequest("Check the Unity AI models", []), default);

        var call = Assert.Single(plan.Actions);
        Assert.Equal(ToolKind.UnityMcpAction, call.Kind);
        Assert.Equal(PermissionKind.Execute, call.RequiredPermission);
        Assert.Equal("Games/ForgeCore", call.Arguments["project_path"]);
        Assert.Equal("Unity_AssetGeneration_GetModels", call.Arguments["tool_name"]);
        Assert.Equal(0, fallback.Requests);

        var observation = new ToolObservation(call.Id, call.Kind, call.Description, call.Arguments,
            "Succeeded", true, "{\"Models\": []}");
        var completed = await model.CreatePlanAsync(new ModelRequest("Check the Unity AI models", [], [observation], 2, 11), default);
        Assert.Empty(completed.Actions);
        Assert.Contains("connection is working", completed.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fallback.Requests);

        var escapedObservation = observation with { Detail = "{\"content\":[{\"type\":\"text\",\"text\":\"{\\\"Models\\\": []}\"}]}" };
        var escaped = await model.CreatePlanAsync(new ModelRequest("Check the Unity AI models", [], [escapedObservation], 2, 11), default);
        Assert.Contains("no asset-generation models", escaped.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuiltInUnityDiagnostics_LeavesUnrelatedConversationToLocalModel()
    {
        var fallback = new CountingModel();
        await using var model = new BuiltInUnityDiagnosticsModel(fallback, "Games/ForgeCore");
        var plan = await model.CreatePlanAsync(new ModelRequest("Hello there", []), default);
        Assert.Equal("fallback", plan.Response);
        Assert.Equal(1, fallback.Requests);
    }

    [Fact]
    public async Task BuiltInSupervised3D_ProducesDeterministicGenerationWithoutCallingNativeModel()
    {
        var fallback = new CountingModel();
        await using var model = new BuiltInSupervised3DModel(fallback, "Games/ForgeCore");
        const string request = "Generate an original realistic 3D watchtower as a self-contained GLB. Name it frontier-watchtower.";
        var searchPlan = await model.CreatePlanAsync(new ModelRequest(request, []), default);
        var search = Assert.Single(searchPlan.Actions); Assert.Equal(ToolKind.SearchAssetVault, search.Kind);
        var searched = new ToolObservation(search.Id, search.Kind, search.Description, search.Arguments,
            "Succeeded", true, "{\"schemaVersion\":1,\"results\":[]}");
        var plan = await model.CreatePlanAsync(new ModelRequest(request, [], [searched], 2, 11), default);

        var call = Assert.Single(plan.Actions);
        Assert.Equal(ToolKind.Generate3DAsset, call.Kind);
        Assert.Equal(PermissionKind.Network, call.RequiredPermission);
        Assert.Equal("Games/ForgeCore", call.Arguments["project_path"]);
        Assert.Equal("seele", call.Arguments["provider"]);
        Assert.Equal("frontier-watchtower", call.Arguments["asset_name"]);
        Assert.Equal("Seele02-flash", call.Arguments["model"]);
        Assert.Equal(request, call.Arguments["prompt"]);
        Assert.Equal(0, fallback.Requests);
    }

    [Fact]
    public async Task BuiltInSupervised3D_UsesConfiguredMeshyProviderWithoutCallingNativeModel()
    {
        var fallback = new CountingModel();
        await using var model = new BuiltInSupervised3DModel(fallback, "Games/ForgeCore", "meshy");
        const string request = "Generate an original realistic 3D watchtower as a self-contained GLB. Name it frontier-watchtower.";
        var search = Assert.Single((await model.CreatePlanAsync(new ModelRequest(request, []), default)).Actions);
        var searched = new ToolObservation(search.Id, search.Kind, search.Description, search.Arguments,
            "Succeeded", true, "{\"schemaVersion\":1,\"results\":[]}");
        var call = Assert.Single((await model.CreatePlanAsync(new ModelRequest(request, [], [searched], 2, 11), default)).Actions);

        Assert.Equal("meshy", call.Arguments["provider"]);
        Assert.Equal("meshy-6", call.Arguments["model"]);
        Assert.Equal("frontier-watchtower", call.Arguments["asset_name"]);
        Assert.Equal(0, fallback.Requests);
    }

    [Fact]
    public async Task BuiltInSupervised3D_ReusesExactNamedVaultCandidateBeforeCloudGeneration()
    {
        var fallback = new CountingModel();
        await using var model = new BuiltInSupervised3DModel(fallback, "Games/NewGame");
        const string request = "Generate an original realistic 3D watchtower as a self-contained GLB. Name it frontier-watchtower.";
        var search = Assert.Single((await model.CreatePlanAsync(new ModelRequest(request, []), default)).Actions);
        var bundle = new string('b', 64); var assetHash = new string('a', 64);
        var searched = new ToolObservation(search.Id, search.Kind, search.Description, search.Arguments, "Succeeded", true,
            "{\"schemaVersion\":1,\"results\":[{\"bundleSha256\":\"" + bundle + "\",\"assetSetId\":\"frontier-watchtower\",\"assetSha256\":\"" + assetHash + "\"}]}");

        var import = Assert.Single((await model.CreatePlanAsync(new ModelRequest(request, [], [searched], 2, 11), default)).Actions);

        Assert.Equal(ToolKind.ImportVaultAsset, import.Kind); Assert.Equal(PermissionKind.Write, import.RequiredPermission);
        Assert.Equal(bundle, import.Arguments["bundle_sha256"]); Assert.Equal(assetHash, import.Arguments["expected_asset_sha256"]);
        Assert.DoesNotContain("provider", import.Arguments.Keys);
        Assert.Equal(0, fallback.Requests);
    }

    [Fact]
    public async Task BuiltInSupervised3D_AdvancesThroughInspectionAndExactHashImport()
    {
        var fallback = new CountingModel();
        await using var model = new BuiltInSupervised3DModel(fallback, "Games/ForgeCore");
        const string request = "Generate an original realistic 3D watchtower as a self-contained GLB. Name it frontier-watchtower.";
        var generatedCall = ToolCall.Create(ToolKind.Generate3DAsset, PermissionKind.Network, "generate");
        var generated = new ToolObservation(generatedCall.Id, generatedCall.Kind, generatedCall.Description, generatedCall.Arguments,
            "Succeeded", true, "Quarantined and structurally validated: frontier-watchtower.glb (GLB; valid; 100 bytes; SHA-256 " + new string('a', 64) + ").");
        var inspectPlan = await model.CreatePlanAsync(new ModelRequest(request, [], [generated], 2, 10), default);
        var inspect = Assert.Single(inspectPlan.Actions);
        Assert.Equal(ToolKind.InspectStagedAsset, inspect.Kind);
        Assert.Equal("frontier-watchtower.glb", inspect.Arguments["file_name"]);

        var inspected = new ToolObservation(inspect.Id, inspect.Kind, inspect.Description, inspect.Arguments,
            "Succeeded", true, "Validated GLB. SHA-256: " + new string('b', 64) + ".");
        var importPlan = await model.CreatePlanAsync(new ModelRequest(request, [], [generated, inspected], 3, 9), default);
        var import = Assert.Single(importPlan.Actions);
        Assert.Equal(ToolKind.ImportStagedAsset, import.Kind);
        Assert.Equal(new string('b', 64), import.Arguments["expected_sha256"]);
        Assert.Equal("Assets/ForgeCore/Generated/frontier-watchtower.glb", import.Arguments["destination_path"]);
        Assert.Equal(0, fallback.Requests);
    }

    [Fact]
    public async Task BuiltInSupervised3D_PreparesExistingImportedAssetThroughHashBoundWorkflow()
    {
        var fallback = new CountingModel();
        await using var model = new BuiltInSupervised3DModel(fallback, "Games/ForgeCore", "meshy");
        const string request = "Prepare imported 3D asset named frontier-watchtower.";
        var installPlan = await model.CreatePlanAsync(new ModelRequest(request, []), default);
        var install = Assert.Single(installPlan.Actions); Assert.Equal(ToolKind.InstallUnityBridge, install.Kind);

        var installed = new ToolObservation(install.Id, install.Kind, install.Description, install.Arguments, "Succeeded", true, "Bridge upgraded.");
        var readPlan = await model.CreatePlanAsync(new ModelRequest(request, [], [installed], 2, 11), default);
        var read = Assert.Single(readPlan.Actions); Assert.Equal(ToolKind.ReadTextFile, read.Kind);
        var hash = new string('c', 64);
        var readResult = new ToolObservation(read.Id, read.Kind, read.Description, read.Arguments, "Succeeded", true,
            "{\"file\":\"frontier-watchtower.glb\",\"sha256\":\"" + hash + "\"}");
        var compilePlan = await model.CreatePlanAsync(new ModelRequest(request, [], [installed, readResult], 3, 10), default);
        var compile = Assert.Single(compilePlan.Actions); var compileSpec = UnityEditorActionSpec.From(compile);
        Assert.Equal("compile", compileSpec.Operation);

        var compiled = new ToolObservation(compile.Id, compile.Kind, compile.Description, compile.Arguments, "Succeeded", true, "Compiled and imported.");
        var inspectionPlan = await model.CreatePlanAsync(new ModelRequest(request, [], [installed, readResult, compiled], 4, 9), default);
        var inspection = Assert.Single(inspectionPlan.Actions); var inspectionSpec = UnityEditorActionSpec.From(inspection);
        Assert.Equal("inspect_game_art_asset", inspectionSpec.Operation);
        Assert.Equal("frontier-watchtower", inspectionSpec.AssetSetId);
        Assert.Equal("environment_props", inspectionSpec.AssetRole);
        Assert.Equal("realistic_pbr", inspectionSpec.StyleFamily);
        Assert.Equal(hash, inspectionSpec.ExpectedSha256);

        var inspected = new ToolObservation(inspection.Id, inspection.Kind, inspection.Description, inspection.Arguments,
            "Succeeded", true, "Game-art evidence: local.json (structural_ready=true, rights_reviewed=false, visual_reviewed=false)");
        var previewPlan = await model.CreatePlanAsync(new ModelRequest(request, [], [installed, readResult, compiled, inspected], 5, 8), default);
        var preview = Assert.Single(previewPlan.Actions); var previewSpec = UnityEditorActionSpec.From(preview);
        Assert.Equal("capture_game_art_preview", previewSpec.Operation);
        Assert.Equal("Previews/GameArt/frontier-watchtower-Review1.png", previewSpec.OutputPath);
        Assert.Equal(hash, previewSpec.ExpectedSha256);

        var previewHash = new string('d', 64); var previewProvenanceHash = new string('e', 64);
        var previewed = new ToolObservation(preview.Id, preview.Kind, preview.Description, preview.Arguments,
            "Succeeded", true, $"Game-art preview evidence: local.png (1280x720, four views, 123 bytes, SHA-256 {previewHash}); provenance local.json (SHA-256 {previewProvenanceHash}); rights_reviewed=false; visual_reviewed=false");
        var visualPlan = await model.CreatePlanAsync(new ModelRequest(request, [], [installed, readResult, compiled, inspected, previewed], 6, 7), default);
        var visual = Assert.Single(visualPlan.Actions); var visualSpec = GameArtReviewSpec.FromProposal(visual);
        Assert.Equal(ToolKind.ReviewGameArtAsset, visual.Kind); Assert.Equal(GameArtReviewSpec.Visual, visualSpec.ReviewKind);
        Assert.Equal(previewHash, visualSpec.ExpectedPreviewSha256); Assert.Equal(previewProvenanceHash, visualSpec.ExpectedPreviewProvenanceSha256);
        var visualBound = GameArtReviewSpec.BindUserDecision(visual, ApprovalDecision.Approved);
        var visualReviewed = new ToolObservation(visual.Id, visual.Kind, visual.Description, visualBound.Arguments,
            "Succeeded", true, "Game-art visual review recorded: decision=approved; asset_set_id=frontier-watchtower");
        var rightsPlan = await model.CreatePlanAsync(new ModelRequest(request, [], [installed, readResult, compiled, inspected, previewed, visualReviewed], 7, 6), default);
        var rights = Assert.Single(rightsPlan.Actions); var rightsSpec = GameArtReviewSpec.FromProposal(rights);
        Assert.Equal(GameArtReviewSpec.Rights, rightsSpec.ReviewKind);
        var rightsBound = GameArtReviewSpec.BindUserDecision(rights, ApprovalDecision.Approved);
        var rightsReviewed = new ToolObservation(rights.Id, rights.Kind, rights.Description, rightsBound.Arguments,
            "Succeeded", true, "Game-art rights review recorded: decision=approved; asset_set_id=frontier-watchtower");
        var storePlan = await model.CreatePlanAsync(new ModelRequest(request, [], [installed, readResult, compiled, inspected, previewed, visualReviewed, rightsReviewed], 8, 5), default);
        var store = Assert.Single(storePlan.Actions); var storeSpec = ReviewedAssetStoreSpec.From(store);
        Assert.Equal(ToolKind.StoreReviewedAsset, store.Kind); Assert.Equal(hash, storeSpec.ExpectedAssetSha256);
        Assert.Contains("frontier-watchtower", storeSpec.Tags);
        var stored = new ToolObservation(store.Id, store.Kind, store.Description, store.Arguments,
            "Succeeded", true, "Reviewed asset stored in immutable local vault.");
        var preparePlan = await model.CreatePlanAsync(new ModelRequest(request, [], [installed, readResult, compiled, inspected, previewed, visualReviewed, rightsReviewed, stored], 9, 4), default);
        var prepare = Assert.Single(preparePlan.Actions); var spec = UnityEditorActionSpec.From(prepare);
        Assert.Equal("prepare_generated_asset", spec.Operation); Assert.Equal(hash, spec.ExpectedSha256);
        Assert.Equal("Forest Watchtower", spec.ParentObjectName); Assert.Equal(16, spec.TargetHeight);
        Assert.Equal(0, fallback.Requests);

        var prepared = new ToolObservation(prepare.Id, prepare.Kind, prepare.Description, prepare.Arguments, "Succeeded", true, "Prepared.");
        var complete = await model.CreatePlanAsync(new ModelRequest(request, [], [installed, readResult, compiled, inspected, previewed, visualReviewed, rightsReviewed, stored, prepared], 10, 3), default);
        Assert.Empty(complete.Actions); Assert.Contains("scaled to 16 metres", complete.Response, StringComparison.OrdinalIgnoreCase);

        var visualRejectedBound = GameArtReviewSpec.BindUserDecision(visual, ApprovalDecision.Rejected);
        var visualRejected = new ToolObservation(visual.Id, visual.Kind, visual.Description, visualRejectedBound.Arguments,
            "Succeeded", true, "Game-art visual review recorded: decision=rejected; asset_set_id=frontier-watchtower");
        var stopped = await model.CreatePlanAsync(new ModelRequest(request, [], [installed, readResult, compiled, inspected, previewed, visualRejected], 7, 6), default);
        Assert.Empty(stopped.Actions); Assert.Contains("rejected visual review", stopped.Response, StringComparison.OrdinalIgnoreCase);

        var notReady = inspected with { Detail = "Game-art evidence: local.json (structural_ready=false, rights_reviewed=false, visual_reviewed=false)" };
        var rejected = await model.CreatePlanAsync(new ModelRequest(request, [], [installed, readResult, compiled, notReady], 5, 8), default);
        Assert.Empty(rejected.Actions); Assert.Contains("will not place", rejected.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Factory_ConstructsBackendOnlyForValidExistingLocalGguf()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gguf"); await File.WriteAllBytesAsync(path, [0]);
        try
        {
            var called = false; var options = new LocalModelOptions { EnableLlamaSharp = true, ModelPath = path };
            await using var backend = LocalInferenceBackendFactory.Create(options, (resolved, _) => { called = true; Assert.Equal(Path.GetFullPath(path), resolved); return new FakeBackend([]); });
            Assert.True(called); Assert.True(backend.Status.IsAvailable);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Factory_FailsClosedForUnsafeOrInvalidConfiguration()
    {
        var invalid = new LocalModelOptions { EnableLlamaSharp = true, ModelPath = @"\\server\model.gguf", ContextSize = 1 };
        var backend = LocalInferenceBackendFactory.Create(invalid, (_, _) => throw new InvalidOperationException("Must not construct backend."));
        Assert.False(backend.Status.IsAvailable); Assert.Contains("ContextSize", backend.Status.Detail, StringComparison.Ordinal);
    }

    private sealed class FakeBackend(IReadOnlyList<string> chunks, bool waitForCancellation = false) : ILocalInferenceBackend
    {
        public string LastPrompt { get; private set; } = string.Empty;
        public ModelStatus Status => new("Fake", true, "Ready");
        public async IAsyncEnumerable<string> InferAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastPrompt = prompt;
            if (waitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            foreach (var chunk in chunks) { cancellationToken.ThrowIfCancellationRequested(); yield return chunk; }
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CountingModel : ILocalModel
    {
        public int Requests { get; private set; }
        public ModelStatus Status => new("Fallback", true, "Ready");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new AgentPlan("fallback", []));
        }
    }
}
