using System.Text.Json.Serialization;

namespace ForgeCore.Core;

public sealed record PlayerFrameSpike(
    [property: JsonPropertyName("frame")] int Frame,
    [property: JsonPropertyName("elapsedMilliseconds")] double ElapsedMilliseconds,
    [property: JsonPropertyName("stopwatchMilliseconds")] double StopwatchMilliseconds,
    [property: JsonPropertyName("unityMilliseconds")] double UnityMilliseconds,
    [property: JsonPropertyName("focused")] bool Focused,
    [property: JsonPropertyName("gc0")] int Gc0,
    [property: JsonPropertyName("gc1")] int Gc1,
    [property: JsonPropertyName("gc2")] int Gc2);

public sealed record PlayerFrameDiagnostics(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("clock")] string Clock,
    [property: JsonPropertyName("awakeSinceStartupMilliseconds")] double AwakeSinceStartupMilliseconds,
    [property: JsonPropertyName("themeMilliseconds")] double ThemeMilliseconds,
    [property: JsonPropertyName("setupMilliseconds")] double SetupMilliseconds,
    [property: JsonPropertyName("readySinceStartupMilliseconds")] double ReadySinceStartupMilliseconds,
    [property: JsonPropertyName("interactiveSinceStartupMilliseconds")] double InteractiveSinceStartupMilliseconds,
    [property: JsonPropertyName("warmupMilliseconds")] double WarmupMilliseconds,
    [property: JsonPropertyName("warmupFrames")] int WarmupFrames,
    [property: JsonPropertyName("maximumWarmupFrameMilliseconds")] double MaximumWarmupFrameMilliseconds,
    [property: JsonPropertyName("measuredWallMilliseconds")] double MeasuredWallMilliseconds,
    [property: JsonPropertyName("sampledFrames")] int SampledFrames,
    [property: JsonPropertyName("maximumStopwatchFrameMilliseconds")] double MaximumStopwatchFrameMilliseconds,
    [property: JsonPropertyName("framesOver33Milliseconds")] int FramesOver33Milliseconds,
    [property: JsonPropertyName("framesOver50Milliseconds")] int FramesOver50Milliseconds,
    [property: JsonPropertyName("framesOver100Milliseconds")] int FramesOver100Milliseconds,
    [property: JsonPropertyName("focusChanges")] int FocusChanges,
    [property: JsonPropertyName("pauseChanges")] int PauseChanges,
    [property: JsonPropertyName("focusedAtStart")] bool FocusedAtStart,
    [property: JsonPropertyName("runInBackground")] bool RunInBackground,
    [property: JsonPropertyName("frameSpikes")] PlayerFrameSpike[] FrameSpikes)
{
    public const double MaximumFrameBudgetMilliseconds = 100;
    public const double StartupBudgetMilliseconds = 15000;
    public void Validate(int frames, int durationSeconds, double maximumUnityFrameMilliseconds)
    {
        static bool Metric(double n, double max = 120000) => double.IsFinite(n) && n >= 0 && n <= max;
        if (SchemaVersion != 1 || Clock != "stopwatch_update_intervals_v1" || SampledFrames != frames ||
            !Metric(AwakeSinceStartupMilliseconds) || !Metric(ThemeMilliseconds) || !Metric(SetupMilliseconds) ||
            !Metric(ReadySinceStartupMilliseconds) || ReadySinceStartupMilliseconds < AwakeSinceStartupMilliseconds ||
            !Metric(InteractiveSinceStartupMilliseconds) || InteractiveSinceStartupMilliseconds < ReadySinceStartupMilliseconds ||
            WarmupFrames is < 1 or > 1000000 || !Metric(WarmupMilliseconds) || WarmupMilliseconds < 1000 ||
            !Metric(MaximumWarmupFrameMilliseconds) || !Metric(MeasuredWallMilliseconds) ||
            MeasuredWallMilliseconds < durationSeconds*1000 || MeasuredWallMilliseconds > durationSeconds*1000+10000 ||
            !Metric(MaximumStopwatchFrameMilliseconds,10000) || MaximumStopwatchFrameMilliseconds <= 0 ||
            FramesOver33Milliseconds < 0 || FramesOver33Milliseconds > frames || FramesOver50Milliseconds < 0 ||
            FramesOver50Milliseconds > FramesOver33Milliseconds || FramesOver100Milliseconds < 0 ||
            FramesOver100Milliseconds > FramesOver50Milliseconds || FocusChanges is < 0 or > 1000000 || PauseChanges is < 0 or > 1000000 ||
            FrameSpikes is null || FrameSpikes.Length != Math.Min(32,FramesOver33Milliseconds))
            throw new ArgumentException("Player frame-phase diagnostics are missing, inconsistent, or outside bounded limits.");
        var worst = Math.Max(maximumUnityFrameMilliseconds,MaximumStopwatchFrameMilliseconds);
        if ((worst > 33.334) != (FramesOver33Milliseconds > 0) || (worst > 50) != (FramesOver50Milliseconds > 0) ||
            (worst > 100) != (FramesOver100Milliseconds > 0)) throw new ArgumentException("Long-frame counts contradict the reported maximum.");
        var previous = 0;
        foreach(var spike in FrameSpikes)
        {
            if (spike is null || spike.Frame <= previous || spike.Frame > frames || !Metric(spike.ElapsedMilliseconds) ||
                spike.ElapsedMilliseconds > MeasuredWallMilliseconds || !Metric(spike.StopwatchMilliseconds,10000) ||
                !Metric(spike.UnityMilliseconds,10000) || Math.Max(spike.StopwatchMilliseconds,spike.UnityMilliseconds) <= 33.334 ||
                spike.StopwatchMilliseconds > MaximumStopwatchFrameMilliseconds+.01 || spike.UnityMilliseconds > maximumUnityFrameMilliseconds+.01 ||
                spike.Gc0 < 0 || spike.Gc1 < 0 || spike.Gc2 < 0)
                throw new ArgumentException("Player long-frame diagnostic sample is inconsistent.");
            previous = spike.Frame;
        }
    }
    public bool SmoothnessPassed(double maximumUnityFrameMilliseconds) =>
        Math.Max(maximumUnityFrameMilliseconds, MaximumStopwatchFrameMilliseconds) <= MaximumFrameBudgetMilliseconds && FramesOver100Milliseconds == 0;
    [JsonIgnore]
    public bool StartupPassed => InteractiveSinceStartupMilliseconds <= StartupBudgetMilliseconds;
}
