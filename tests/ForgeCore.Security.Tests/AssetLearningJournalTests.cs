using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;

namespace ForgeCore.Security.Tests;

public sealed partial class SecurityTests
{
    [Fact] public async Task LearningRecordsValidatedRigAndMotionAndSeparatelyRemembersAFailedAttempt()
    {
        var blender = await BlenderFixtureExecutable();
        using var sourceTools = BlenderExecutor(blender, (e, a, w, t, c) => CharacterRunner(e, a, w, t, c));
        var made = await sourceTools.ExecuteAsync(CharacterCall(), default); Assert.True(made.Success, made.Error);
        var imported = await sourceTools.ExecuteAsync(ImportBarrel(made.Output, "learning-rig"), default); Assert.True(imported.Success, imported.Error);
        await File.WriteAllTextAsync(Path.Combine(_root, "Game", "Assets", "ForgeCore", "Generated", "Blender", "learning-rig", "learning-rig.glb.meta"), "synthetic Unity metadata, parser test only");
        using var receipt = JsonDocument.Parse(made.Output); var bundle = receipt.RootElement.GetProperty("bundleSha256").GetString()!;
        var hash = receipt.RootElement.GetProperty("assetSha256").GetString()!;
        var guard = new WorkspacePathGuard(_root); var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        using var installer = new ToolExecutor(guard, []);
        Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "bridge", ("project_path", "Game")), default)).Success);
        Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate, PermissionKind.Write, "kit", ("project_path", "Game"), ("template_id", "first_person_action")), default)).Success);
        UnityProcessRunner runner = async (_, arguments, _, _, token) =>
        {
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(ValueAfter(arguments, "-forgecoreRequest"), token)); var r = request.RootElement;
            var operation = r.GetProperty("operation").GetString()!;
            var scene = Path.Combine(_root, "Game", r.GetProperty("scenePath").GetString()!); Directory.CreateDirectory(Path.GetDirectoryName(scene)!);
            var png = Path.Combine(_root, "Game", r.GetProperty("outputPath").GetString()!); Directory.CreateDirectory(Path.GetDirectoryName(png)!);
            await File.WriteAllTextAsync(scene, "synthetic plumbing test, not engine quality evidence", token);
            await File.WriteAllBytesAsync(png, MinimalPng(1280, 1440), token);
            if (operation == "prepare_character_animation")
            {
                var folder = Path.Combine(_root, "Game", "Assets", "ForgeCore", "CharacterAnimations", "learning-motion"); Directory.CreateDirectory(folder);
                var clips = new[] { "Idle", "Walk", "Run", "Aim", "Fire", "Reload", "Hit", "Death" };
                foreach (var name in clips.Select(c => c + ".anim").Concat(new[] { "Mannequin.controller", "animation-report.json" }))
                {
                    await File.WriteAllTextAsync(Path.Combine(folder, name), "synthetic animation fixture", token);
                    await File.WriteAllTextAsync(Path.Combine(folder, name + ".meta"), "synthetic metadata", token);
                }
                await File.WriteAllTextAsync(Path.Combine(folder, "animation-report.json"), JsonSerializer.Serialize(new
                {
                    schemaVersion = 1, recipe = ForgeCore.Motion.GuardMotionProfile.Recipe, assetSha256 = hash,
                    clipCount = 8, rootMotionPolicy = "runtime_translation_in_place_clips", finite = true, clips,
                    maximumLoopError = .001, maximumEdgeStretch = 2.2, maximumStanceSlip = .001
                }), token);
            }
            var rig = new CharacterRigEvidence(1, hash, "generic_named_bones_v1", 22, 1000, 0, 0, 0, 0, 1.8, 0,
                [new("arms", 300, .4, 1.5, true), new("legs", 400, .5, 2, true), new("combined", 800, .6, 2.2, true)]);
            await File.WriteAllTextAsync(ValueAfter(arguments, "-forgecoreResult"), JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.23", editorVersion = "6000.0.50f1", requestId = r.GetProperty("requestId").GetString(),
                operation, success = true, summary = "synthetic measurement plumbing only", characterRig = operation == "inspect_character_rig" ? rig : null
            }), token);
            return new(0, "", "");
        };
        using var tools = new ToolExecutor(guard, [], [], 1024 * 1024, unityEditorPath: unity, unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);
        ToolCall Action(string op, string scene) => ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "measurement fixture",
            ("project_path", "Game"), ("operation", op), ("asset_path", "Assets/ForgeCore/Generated/Blender/learning-rig/learning-rig.glb"),
            ("expected_sha256", hash), ("scene_path", "Assets/Scenes/" + scene + ".unity"), ("output_path", "Previews/" + scene + ".png"));
        var rigCall = Action("inspect_character_rig", "LearningRig");
        var rigResult = await tools.ExecuteAsync(rigCall, default); Assert.True(rigResult.Success, rigResult.Error);
        var motionCall = Action("prepare_character_animation", "LearningMotion");
        motionCall = motionCall with { Arguments = new Dictionary<string, string>(motionCall.Arguments) { ["asset_set_id"] = "learning-motion" } };
        var motion = await tools.ExecuteAsync(motionCall, default); Assert.True(motion.Success, motion.Error);
        var failed = await tools.ExecuteAsync(motionCall with { Id = Guid.NewGuid() }, default); Assert.False(failed.Success); Assert.Equal("recorded_advisory_only", failed.LearningStatus);
        var inspected = await tools.ExecuteAsync(InspectLearning(bundle), default); Assert.True(inspected.Success, inspected.Error);
        using var plan = JsonDocument.Parse(inspected.Output); var stages = plan.RootElement.GetProperty("learningPlan").GetProperty("stages");
        Assert.Equal(1, stages[1].GetProperty("recordedAttempts").GetInt32());
        Assert.Equal(2, stages[2].GetProperty("recordedAttempts").GetInt32());
        Assert.Equal(1, stages[2].GetProperty("failedConfigurations").GetInt32());
        Assert.Single(stages[2].GetProperty("technicalFrontier").EnumerateArray());
        // Changing the original proof invalidates the advisory event, never restores it from a claimed success flag.
        var proof = Directory.GetFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "character-animation-evidence")).Single();
        await File.AppendAllTextAsync(proof, "stale");
        var stale = await tools.ExecuteAsync(InspectLearning(bundle), default); Assert.True(stale.Success, stale.Error);
        using var stalePlan = JsonDocument.Parse(stale.Output);
        Assert.Equal(1, stalePlan.RootElement.GetProperty("learningPlan").GetProperty("rejectedRecords").GetInt32());
    }

    private static ToolCall InspectLearning(string bundle) => ToolCall.Create(ToolKind.InspectLocalBlenderAsset, PermissionKind.Read,
        "Read source and advisory experience only", ("bundle_sha256", bundle));

    [Fact] public async Task GenerationAutomaticallyRetainsGeneralExperienceAndCacheReusesDoNotBecomeNewModels()
    {
        var exe = await BlenderFixtureExecutable(); var launches = 0;
        using var tools = BlenderExecutor(exe, async (e, a, w, t, c) => { launches++; return await BlenderFixtureRunner(e, a, w, t, c); });
        var first = await tools.ExecuteAsync(BarrelCall(), default); Assert.True(first.Success, first.Error);
        Assert.Equal("recorded_advisory_only", first.LearningStatus);
        var second = await tools.ExecuteAsync(BarrelCall(), default); Assert.True(second.Success, second.Error); Assert.Equal(1, launches);
        using var receipt = JsonDocument.Parse(first.Output); var bundle = receipt.RootElement.GetProperty("bundleSha256").GetString()!;
        var inspected = await tools.ExecuteAsync(InspectLearning(bundle), default); Assert.True(inspected.Success, inspected.Error);
        using var packet = JsonDocument.Parse(inspected.Output); var plan = packet.RootElement.GetProperty("learningPlan");
        var stage = plan.GetProperty("stages")[0];
        Assert.Equal(2, stage.GetProperty("recordedAttempts").GetInt32());
        Assert.Equal(1, stage.GetProperty("distinctSuccessfulCandidates").GetInt32());
        Assert.Equal(1, stage.GetProperty("cacheReuses").GetInt32());
        Assert.False(plan.GetProperty("neuralWeightsTrained").GetBoolean()); Assert.False(plan.GetProperty("grantsApproval").GetBoolean());
        Assert.Equal("pending", plan.GetProperty("sourceVisualReview").GetString());
        var before = Directory.GetFiles(Path.Combine(_root, ".forgecore", "blender-library", "experience", "v1"));
        Assert.True((await tools.ExecuteAsync(InspectLearning(bundle), default)).Success);
        Assert.Equal(before, Directory.GetFiles(Path.Combine(_root, ".forgecore", "blender-library", "experience", "v1")));
    }

    [Fact] public async Task FailedRunIsRememberedWithoutRawErrorsAndDoesNotPublishAFalseCandidate()
    {
        var exe = await BlenderFixtureExecutable(); var fail = true;
        using var tools = BlenderExecutor(exe, async (e, a, w, t, c) => fail
            ? throw new InvalidOperationException("simulated private detail DO_NOT_PERSIST") : await BlenderFixtureRunner(e, a, w, t, c));
        var failed = await tools.ExecuteAsync(BarrelCall(), default); Assert.False(failed.Success);
        Assert.Equal("recorded_advisory_only", failed.LearningStatus); fail = false;
        var made = await tools.ExecuteAsync(BarrelCall(), default); Assert.True(made.Success, made.Error);
        using var receipt = JsonDocument.Parse(made.Output);
        var result = await tools.ExecuteAsync(InspectLearning(receipt.RootElement.GetProperty("bundleSha256").GetString()!), default);
        Assert.True(result.Success, result.Error); using var packet = JsonDocument.Parse(result.Output);
        var stage = packet.RootElement.GetProperty("learningPlan").GetProperty("stages")[0];
        Assert.Equal(1, stage.GetProperty("failedConfigurations").GetInt32());
        Assert.Equal(1, stage.GetProperty("distinctConfigurations").GetInt32());
        foreach (var file in Directory.GetFiles(Path.Combine(_root, ".forgecore", "blender-library", "experience", "v1")))
            Assert.DoesNotContain("DO_NOT_PERSIST", await File.ReadAllTextAsync(file));
    }

    [Theory]
    [InlineData("bytes")][InlineData("approval")][InlineData("secret_path")][InlineData("measurements")]
    public async Task TamperedJournalCannotBecomeApprovalOrModelInstructions(string defect)
    {
        var exe = await BlenderFixtureExecutable(); using var tools = BlenderExecutor(exe, BlenderFixtureRunner);
        var first = await tools.ExecuteAsync(BarrelCall(), default); Assert.True(first.Success, first.Error);
        using var receipt = JsonDocument.Parse(first.Output); var bundle = receipt.RootElement.GetProperty("bundleSha256").GetString()!;
        var folder = Path.Combine(_root, ".forgecore", "blender-library", "experience", "v1"); var file = Directory.GetFiles(folder).Single();
        if (defect == "bytes") await File.AppendAllTextAsync(file, "tamper");
        else
        {
            var node = JsonNode.Parse(await File.ReadAllTextAsync(file))!;
            if (defect == "approval") node["approve_everything"] = true;
            if (defect == "secret_path") node["evidencePath"] = "appsettings.json";
            if (defect == "measurements") node["measurements"]!["triangles"] = 1;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(node); var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await File.WriteAllBytesAsync(Path.Combine(folder, hash + ".json"), bytes);
        }
        var inspected = await tools.ExecuteAsync(InspectLearning(bundle), default); Assert.True(inspected.Success, inspected.Error);
        using var packet = JsonDocument.Parse(inspected.Output); var plan = packet.RootElement.GetProperty("learningPlan");
        Assert.Equal(1, plan.GetProperty("rejectedRecords").GetInt32()); Assert.False(plan.GetProperty("grantsApproval").GetBoolean());
        Assert.False(Directory.Exists(Path.Combine(_root, ".forgecore", "blender-library", "reviews")));
    }

    [Fact] public async Task UnwritableJournalNeverTurnsCompletedGenerationIntoRetryableFailure()
    {
        var exe = await BlenderFixtureExecutable();
        var parent = Path.Combine(_root, ".forgecore", "blender-library", "experience"); Directory.CreateDirectory(parent);
        await File.WriteAllTextAsync(Path.Combine(parent, "v1"), "occupied, preserve me");
        using var tools = BlenderExecutor(exe, BlenderFixtureRunner);
        var made = await tools.ExecuteAsync(BarrelCall(), default);
        Assert.True(made.Success, made.Error); Assert.Equal("not_recorded_execution_result_unchanged", made.LearningStatus);
        Assert.Equal("occupied, preserve me", await File.ReadAllTextAsync(Path.Combine(parent, "v1")));
    }
}
