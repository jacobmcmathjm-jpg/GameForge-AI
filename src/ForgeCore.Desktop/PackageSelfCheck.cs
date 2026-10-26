using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using ForgeCore.Core;

namespace ForgeCore.Desktop;

/// <summary>Offline deployment probe only; not a model, UI, Unity, or game-quality acceptance test.</summary>
internal static class PackageSelfCheck
{
    public static int Run(string directory)
    {
        try
        {
            foreach (var file in new[] { "ForgeCore.Desktop.deps.json", "appsettings.json", "coreclr.dll", "hostfxr.dll", "hostpolicy.dll" })
                if (!File.Exists(Path.Combine(directory, file))) return 2;
            using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "appsettings.json")));
            if (!config.RootElement.TryGetProperty("ForgeCore", out _)) return 3;
            using var runtime = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "ForgeCore.Desktop.runtimeconfig.json")));
            if (!runtime.RootElement.GetProperty("runtimeOptions").TryGetProperty("includedFrameworks", out _)) return 4;
            foreach (var module in new[] { "ForgeCore.Core", "ForgeCore.Agent", "ForgeCore.LocalAI", "ForgeCore.Memory", "ForgeCore.Security", "ForgeCore.Tools", "LLamaSharp" })
                Assembly.Load(new AssemblyName(module));
            var tools = Assembly.Load(new AssemblyName("ForgeCore.Tools"));
            using (var recipe = tools.GetManifestResourceStream("ForgeCore.Tools.Blender.WeatheredBarrel.py"))
                if (recipe is null || recipe.Length < 1000) return 6;
            foreach (var backend in new[] { "avx", "avx2", "avx512", "noavx", "vulkan" })
                foreach (var file in new[] { "llama.dll", "ggml.dll", "ggml-base.dll" })
                    if (!File.Exists(Path.Combine(directory, "runtimes", "win-x64", "native", backend, file))) return 5;
            var sqlite = NativeLibrary.Load(Path.Combine(directory, "e_sqlite3.dll"));
            NativeLibrary.Free(sqlite);
            var plan = GameProductionPlan.Create("Create a first person shooter game with 1 level.", "Games/ForgeCore", Guid.NewGuid());
            plan.Workflow.Validate();
            return 0;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            System.Diagnostics.Trace.WriteLine("Offline package verification failed: " + error.GetType().Name);
            return 1;
        }
    }
}
