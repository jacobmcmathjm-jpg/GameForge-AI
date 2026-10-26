using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeCore.Core;
using ForgeCore.Security;

namespace ForgeCore.Tools;

/// <summary>Versioned local recipes, not a general Python executor or a reviewed-art vault.</summary>
internal sealed class LocalBlenderAssets(WorkspacePathGuard paths, string? executable, string version,
    int timeoutSeconds, UnityProcessRunner? runner = null)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };
    private static readonly string[] OutputNames = ["asset.glb", "model.blend", "preview.png", "basecolor.png",
        "metallic.png", "roughness.png", "metrics.json", "recipe.py", "request.json"];
    private const string Library = ".forgecore/blender-library";
    private const long MaxFileBytes = 100 * 1024 * 1024;
    private static readonly byte[] Recipe = ReadRecipe();
    private static readonly string RecipeHash = Hash(Recipe);

    public async Task<string> CreateAsync(BlenderAssetSpec spec, CancellationToken token)
    {
        RequireProject(spec.ProjectPath);
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) ||
            !string.Equals(Path.GetFileName(executable), "blender.exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(executable) || version != "5.2")
            throw new InvalidOperationException("Configure an existing absolute Blender 5.2 blender.exe path before local creation. Nothing is downloaded.");
        if (timeoutSeconds is < 30 or > 1200) throw new ArgumentException("Blender timeout must be 30-1200 seconds.");
        var binaryHash = await HashFile(executable, 1024L * 1024 * 1024, token);
        var request = new Request(1, spec.AssetName, spec.Recipe, spec.Seed, spec.HeightMillimetres,
            spec.RadiusMillimetres, RecipeHash, version, binaryHash);
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, Json);
        var recipeKey = Hash(requestBytes);
        await Gate.WaitAsync(token);
        try
        {
            var root = paths.Resolve(Library, true);
            Directory.CreateDirectory(root);
            var leasePath = paths.Resolve(Library + "/execution.lease", true);
            // Cross-process exclusion: a stale lease is an explicit stop, never silently removed.
            using var lease = new FileStream(leasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1,
                FileOptions.DeleteOnClose);
            var reference = paths.Resolve($"{Library}/recipes/{recipeKey}.json", true);
            if (File.Exists(reference))
            {
                var hash = await ReadJson<string>(reference, 100, token);
                var existing = await VerifyBundle(hash, token);
                if (existing.Request != request) throw new InvalidDataException("Recipe cache identity does not match its immutable bundle.");
                return Receipt(hash, existing, true);
            }
            RequireIdleEditors();
            var runRelative = $"{Library}/runs/{Guid.NewGuid():N}";
            var run = paths.Resolve(runRelative, true);
            Directory.CreateDirectory(run);
            await WriteNew(Path.Combine(run, "recipe.py"), Recipe, token);
            await WriteNew(Path.Combine(run, "request.json"), requestBytes, token);
            var arguments = new[] { "--background", "--factory-startup", "--disable-autoexec", "--offline-mode", "--threads", "2",
                "--python-exit-code", "1", "--python", Path.Combine(run, "recipe.py"), "--", Path.Combine(run, "request.json") };
            var result = await (runner ?? RunProcess)(executable, arguments, run, TimeSpan.FromSeconds(timeoutSeconds), token);
            await WriteNew(Path.Combine(run, "process.log"), Encoding.UTF8.GetBytes(result.StandardOutput + "\n" + result.StandardError), token);
            if (result.ExitCode != 0 || result.Truncated || !result.StandardOutput.Contains("FORGECORE_BLENDER_RECIPE_COMPLETE", StringComparison.Ordinal))
                throw new InvalidDataException($"Blender recipe failed (exit {result.ExitCode}); no bundle or import was published. Bounded local log: {runRelative}/process.log");
            if (await HashFile(Path.Combine(run, "recipe.py"), MaxFileBytes, token) != RecipeHash ||
                await HashFile(Path.Combine(run, "request.json"), MaxFileBytes, token) != recipeKey ||
                await HashFile(executable, 1024L * 1024 * 1024, token) != binaryHash)
                throw new InvalidDataException("Recipe, request, or Blender executable changed during execution.");
            var output = paths.Resolve(runRelative + "/output", false);
            File.Copy(Path.Combine(run, "recipe.py"), Path.Combine(output, "recipe.py"), false);
            File.Copy(Path.Combine(run, "request.json"), Path.Combine(output, "request.json"), false);
            var metrics = await ValidateOutputs(output, request, token);
            var files = new List<BundleFile>();
            foreach (var name in OutputNames)
            {
                var file = paths.Resolve(Path.Combine(output, name), false);
                files.Add(new(name, new FileInfo(file).Length, await HashFile(file, MaxFileBytes, token)));
            }
            var bundle = new Bundle(1, "unreviewed_local_recipe", request, metrics, files.ToArray());
            var entryBytes = JsonSerializer.SerializeToUtf8Bytes(bundle, Json);
            var bundleHash = Hash(entryBytes);
            await WriteNew(Path.Combine(output, "entry.json"), entryBytes, token);
            var destination = paths.Resolve($"{Library}/objects/{bundleHash}", true);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (Directory.Exists(destination)) throw new IOException("Bundle destination already exists; it was not overwritten.");
            token.ThrowIfCancellationRequested();
            Directory.Move(output, destination);
            // Reference is published only after all outputs and content-addressed entry exist.
            // Publication is the commit point: finish its tiny reference write even if cancellation arrives now.
            Directory.CreateDirectory(Path.GetDirectoryName(reference)!);
            await WriteNew(reference, JsonSerializer.SerializeToUtf8Bytes(bundleHash, Json), CancellationToken.None);
            return Receipt(bundleHash, bundle, false);
        }
        finally { Gate.Release(); }
    }

    public async Task<string> ImportAsync(BlenderAssetImportSpec spec, CancellationToken token)
    {
        RequireProject(spec.ProjectPath);
        var bundle = await VerifyBundle(spec.BundleSha256, token);
        var asset = bundle.Files.Single(x => x.Name == "asset.glb");
        if (asset.Sha256 != spec.ExpectedAssetSha256) throw new InvalidDataException("Bundle GLB differs from the approved import hash.");
        RequireIdleEditors();
        var destinationDirectory = Path.GetDirectoryName(paths.Resolve(Path.Combine(spec.ProjectPath, spec.DestinationPath), true))!;
        if (File.Exists(destinationDirectory) || Directory.Exists(destinationDirectory) || File.Exists(destinationDirectory + ".meta"))
            throw new IOException("Blender import destination or Unity metadata already exists; no overwrite is allowed.");
        var stageRelative = $"{Library}/imports/{Guid.NewGuid():N}";
        var stage = paths.Resolve(stageRelative, true);
        Directory.CreateDirectory(stage);
        var source = paths.Resolve($"{Library}/objects/{spec.BundleSha256}/asset.glb", false);
        var fileName = spec.AssetName + ".glb";
        var stagedAsset = Path.Combine(stage, fileName);
        File.Copy(source, stagedAsset, false);
        if (await HashFile(stagedAsset, MaxFileBytes, token) != spec.ExpectedAssetSha256)
            throw new InvalidDataException("Copied asset identity changed; import not published.");
        var now = DateTimeOffset.UtcNow;
        var provenance = new
        {
            file = fileName, destinationPath = spec.DestinationPath,
            sourcePage = $"forgecore://local-blender/{spec.BundleSha256}", downloadUrl = "",
            license = "FORGECORE_LOCAL_RECIPE_GENERATED_REVIEW_REQUIRED", sha256 = asset.Sha256,
            bytes = asset.Bytes, format = "glTF binary", inspection = "Original local Blender recipe; immutable source bundle verified; visual/rights review pending.",
            stagedUtc = now, importedUtc = now
        };
        await WriteNew(Path.Combine(stage, fileName + ".asset.json"), JsonSerializer.SerializeToUtf8Bytes(provenance, Json), token);
        // Same-volume atomic directory publication; source and all previous games remain unchanged.
        paths.Resolve(destinationDirectory, true);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationDirectory)!);
        token.ThrowIfCancellationRequested();
        Directory.Move(stage, destinationDirectory);
        return JsonSerializer.Serialize(new { assetPath = spec.DestinationPath, assetSha256 = asset.Sha256,
            bundleSha256 = spec.BundleSha256, reviewState = bundle.ReviewState, reusedSource = true }, Json);
    }

    private async Task<Bundle> VerifyBundle(string bundleHash, CancellationToken token)
    {
        if (bundleHash is null || bundleHash.Length != 64 || bundleHash.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid Blender bundle hash.");
        var root = paths.Resolve($"{Library}/objects/{bundleHash}", false);
        var entryPath = paths.Resolve(Path.Combine(root, "entry.json"), false);
        if (await HashFile(entryPath, 128 * 1024, token) != bundleHash) throw new InvalidDataException("Immutable Blender entry changed.");
        var bundle = await ReadJson<Bundle>(entryPath, 128 * 1024, token);
        if (bundle.SchemaVersion != 1 || bundle.ReviewState != "unreviewed_local_recipe" || bundle.Request is null || bundle.Files is null || bundle.Files.Any(x => x is null) ||
            bundle.Files.Length != OutputNames.Length || !bundle.Files.Select(x => x.Name).Order().SequenceEqual(OutputNames.Order()) ||
            bundle.Request.RecipeSha256 != RecipeHash || bundle.Request.Recipe != BlenderAssetSpec.SupportedRecipe ||
            bundle.Request.BlenderVersion != "5.2") throw new InvalidDataException("Unsupported or malformed Blender bundle.");
        foreach (var file in bundle.Files)
        {
            var path = paths.Resolve(Path.Combine(root, file.Name), false);
            if (new FileInfo(path).Length != file.Bytes || await HashFile(path, MaxFileBytes, token) != file.Sha256)
                throw new InvalidDataException("Blender bundle content no longer matches its immutable entry: " + file.Name);
        }
        if (await HashFile(Path.Combine(root, "recipe.py"), MaxFileBytes, token) != RecipeHash ||
            await ReadJson<Request>(Path.Combine(root, "request.json"), 4096, token) != bundle.Request)
            throw new InvalidDataException("Blender bundle source or recipe request changed.");
        var metrics = await ValidateOutputs(root, bundle.Request, token, published: true);
        if (JsonSerializer.Serialize(metrics, Json) != JsonSerializer.Serialize(bundle.Metrics, Json))
            throw new InvalidDataException("Blender measured metrics differ from entry.");
        return bundle;
    }

    private async Task<Metrics> ValidateOutputs(string root, Request req, CancellationToken token, bool published = false)
    {
        var names = Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order().ToArray();
        var expected = published ? OutputNames.Append("entry.json").Order().ToArray() : OutputNames.Order().ToArray();
        if (!names.SequenceEqual(expected)) throw new InvalidDataException("Blender output contains unexpected or missing files.");
        long total = 0;
        foreach (var name in OutputNames)
        {
            var p = paths.Resolve(Path.Combine(root, name), false);
            var length = new FileInfo(p).Length;
            if (length < 1 || length > MaxFileBytes || (total += length) > 200 * 1024 * 1024)
                throw new InvalidDataException("Blender output exceeds the bounded file budget.");
        }
        _ = await StagedAssetInspector.InspectGlbAsync(Path.Combine(root, "asset.glb"), token);
        foreach (var name in new[] { "preview.png", "basecolor.png", "metallic.png", "roughness.png" })
            _ = await StagedAssetInspector.InspectPngAsync(Path.Combine(root, name), token);
        using (var blend = File.OpenRead(Path.Combine(root, "model.blend")))
        {
            var signature = new byte[7]; blend.ReadExactly(signature);
            if (!signature.AsSpan().SequenceEqual("BLENDER"u8)) throw new InvalidDataException("Editable source is not an uncompressed Blender file.");
        }
        var m = await ReadJson<Metrics>(Path.Combine(root, "metrics.json"), 4096, token);
        if (m.Recipe != req.Recipe || m.BlenderVersion is null || !m.BlenderVersion.StartsWith("5.2.", StringComparison.Ordinal) ||
            m.Vertices is < 32 or > 20000 || m.Triangles is < 32 or > 20000 || m.Materials is < 1 or > 4 || m.Objects != 1 ||
            m.Pivot != "bottom_center" || m.Units != "metres" || m.TextureResolution != 512 || m.AnimationCount != 0 ||
            m.BoundsMin is not { Length: 3 } || m.BoundsMax is not { Length: 3 } ||
            m.BoundsMin.Concat(m.BoundsMax).Any(x => !double.IsFinite(x))) throw new InvalidDataException("Blender metrics failed the bounded static-prop gate.");
        var h = req.HeightMillimetres / 1000.0; var r = req.RadiusMillimetres / 1000.0 * 1.025;
        if (Math.Abs(m.BoundsMin[2]) > .005 || Math.Abs(m.BoundsMax[2] - h) > .005 ||
            Math.Abs(m.BoundsMin[0] + r) > .005 || Math.Abs(m.BoundsMax[0] - r) > .005 ||
            Math.Abs(m.BoundsMin[1] + r) > .005 || Math.Abs(m.BoundsMax[1] - r) > .005)
            throw new InvalidDataException("Blender bounds or grounded pivot do not match the recipe dimensions.");
        return m;
    }

    private void RequireProject(string path)
    {
        var root = paths.Resolve(path, false);
        if (!Directory.Exists(Path.Combine(root, "Assets")) || !File.Exists(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt")))
            throw new ArgumentException("project_path must identify an existing Unity project.");
    }
    private static void RequireIdleEditors()
    {
        foreach (var name in new[] { "Unity", "blender" })
        {
            var processes = Process.GetProcessesByName(name);
            try { if (processes.Length != 0) throw new InvalidOperationException("Close Unity and Blender before serialized asset preparation; no editor is terminated automatically."); }
            finally { foreach (var process in processes) process.Dispose(); }
        }
    }
    private static string Receipt(string hash, Bundle b, bool reused) => JsonSerializer.Serialize(new
    {
        bundleSha256 = hash, assetSha256 = b.Files.Single(x => x.Name == "asset.glb").Sha256,
        b.Request.AssetName, b.Request.Recipe, b.Metrics, b.ReviewState, reused,
        sourcePath = $"{Library}/objects/{hash}/model.blend", previewPath = $"{Library}/objects/{hash}/preview.png"
    }, Json);
    private static byte[] ReadRecipe()
    {
        using var stream = typeof(LocalBlenderAssets).Assembly.GetManifestResourceStream("ForgeCore.Tools.Blender.WeatheredBarrel.py")
            ?? throw new InvalidOperationException("Bundled Blender recipe is missing.");
        using var output = new MemoryStream(); stream.CopyTo(output); return output.ToArray();
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static async Task<string> HashFile(string path, long max, CancellationToken token)
    {
        if (!File.Exists(path) || new FileInfo(path).Length is < 1 || new FileInfo(path).Length > max)
            throw new InvalidDataException("Missing or oversized Blender pipeline file: " + Path.GetFileName(path));
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
    }
    private static async Task<T> ReadJson<T>(string path, int max, CancellationToken token)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > max) throw new InvalidDataException("Missing or oversized Blender metadata.");
        return JsonSerializer.Deserialize<T>(await File.ReadAllBytesAsync(path, token), Json) ?? throw new InvalidDataException("Empty Blender metadata.");
    }
    private static async Task WriteNew(string path, byte[] bytes, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes, token);
    }
    private static async Task<UnityProcessResult> RunProcess(string exe, IReadOnlyList<string> arguments, string directory,
        TimeSpan timeout, CancellationToken token)
    {
        var info = new ProcessStartInfo(exe) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        // Do not pass provider credentials or user Python configuration to Blender.
        info.Environment.Clear();
        foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP" })
            if (Environment.GetEnvironmentVariable(name) is { } value) info.Environment[name] = value;
        info.Environment["PATH"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        cancellation.CancelAfter(timeout);
        if (!process.Start()) throw new InvalidOperationException("Blender did not start.");
        var truncated = false;
        async Task<string> Drain(StreamReader reader)
        {
            var buffer = new char[4096]; var text = new StringBuilder(); int count;
            while ((count = await reader.ReadAsync(buffer, CancellationToken.None)) != 0)
            {
                var keep = Math.Min(count, 100000 - text.Length);
                if (keep > 0) text.Append(buffer, 0, keep);
                if (keep < count) truncated = true;
            }
            return text.ToString();
        }
        var stdout = Drain(process.StandardOutput); var stderr = Drain(process.StandardError);
        try { await process.WaitForExitAsync(cancellation.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Blender timed out; its owned process tree was stopped and no import was published.");
        }
        return new(process.ExitCode, await stdout, await stderr, truncated);
    }
    private sealed record Request(int SchemaVersion, string AssetName, string Recipe, int Seed, int HeightMillimetres,
        int RadiusMillimetres, string RecipeSha256, string BlenderVersion, string BlenderExecutableSha256);
    private sealed record BundleFile(string Name, long Bytes, string Sha256);
    private sealed record Bundle(int SchemaVersion, string ReviewState, Request Request, Metrics Metrics, BundleFile[] Files);
    private sealed record Metrics(string Recipe, string BlenderVersion, int Vertices, int Triangles, int Materials, int Objects,
        double[] BoundsMin, double[] BoundsMax, string Pivot, string Units, int TextureResolution, int AnimationCount);
}
