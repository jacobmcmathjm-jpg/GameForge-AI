namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private static string ReadProfileTimingSource()
    {
        using var stream = typeof(UnityGameTemplateKit).Assembly.GetManifestResourceStream("ForgeCore.ProfileTimingDiagnostics.cs")
            ?? throw new InvalidOperationException("Shared timing source resource missing.");
        using var reader = new StreamReader(stream); return reader.ReadToEnd().Replace("\r\n", "\n").Replace("\r", "\n");
    }
    private const string ProfileTimingSource = """
using System;
using ForgeCore.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
namespace ForgeCoreGameTemplates
{
    public sealed partial class PrototypeGame
    {
        private static bool profileRuntimeHookObserved;
        private static double profileBeforeSceneLoadSeconds;
        private static ProfileTimingContext profileBeforeSceneContext;
        private ProfileTimingCollector playerProfileTiming;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ObserveProfileRuntimeStart()
        {
            profileRuntimeHookObserved = false;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-forgecoreProfileRequest") < 0) return;
            profileBeforeSceneLoadSeconds = Time.realtimeSinceStartupAsDouble;
            profileBeforeSceneContext = CaptureProfileTimingContext(); profileRuntimeHookObserved = true;
        }
        private static ProfileTimingContext CaptureProfileTimingContext() => new ProfileTimingContext
        {
            gc0 = GC.CollectionCount(0), gc1 = GC.MaxGeneration >= 1 ? GC.CollectionCount(1) : 0,
            gc2 = GC.MaxGeneration >= 2 ? GC.CollectionCount(2) : 0,
            managedBytes = GC.GetTotalMemory(false), allocatedBytes = Profiler.GetTotalAllocatedMemoryLong()
        };
    }
}
""";
}
