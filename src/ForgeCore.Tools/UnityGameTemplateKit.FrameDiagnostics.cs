namespace ForgeCore.Tools;

internal static partial class UnityGameTemplateKit
{
    private const string FrameDiagnosticsSource = """
using System;
using System.Collections.Generic;

namespace ForgeCoreGameTemplates
{
    [Serializable]
    public sealed class PrototypeFrameSpike
    {
        public int frame; public double elapsedMilliseconds, stopwatchMilliseconds, unityMilliseconds;
        public bool focused; public int gc0, gc1, gc2;
    }
    [Serializable]
    public sealed class PrototypeFrameSnapshot
    {
        public int schemaVersion = 1;
        public string clock = "stopwatch_update_intervals_v1";
        public double awakeSinceStartupMilliseconds, themeMilliseconds, setupMilliseconds, readySinceStartupMilliseconds;
        public double interactiveSinceStartupMilliseconds, warmupMilliseconds, maximumWarmupFrameMilliseconds;
        public int warmupFrames, sampledFrames;
        public double measuredWallMilliseconds, maximumStopwatchFrameMilliseconds;
        public int framesOver33Milliseconds, framesOver50Milliseconds, framesOver100Milliseconds, focusChanges, pauseChanges;
        public bool focusedAtStart, runInBackground;
        public PrototypeFrameSpike[] frameSpikes;
    }
    // Input timestamps are monotonic milliseconds, not scaled simulation time.
    // The boundary interval remains in warmup diagnostics; no measured interval is silently dropped.
    public sealed class PrototypeFrameDiagnostics
    {
        private readonly PrototypeFrameSnapshot snapshot = new PrototypeFrameSnapshot();
        private readonly List<PrototypeFrameSpike> spikes = new List<PrototypeFrameSpike>(32);
        private double began, previous, measurementBegan;
        private int gc0, gc1, gc2;
        public bool Measuring { get; private set; }
        public double ElapsedMilliseconds => snapshot.measuredWallMilliseconds;
        public void Begin(double now, double startup, double awake, double theme, double setup, bool focused, bool background)
        {
            began = previous = now;
            snapshot.awakeSinceStartupMilliseconds = awake; snapshot.themeMilliseconds = theme;
            snapshot.setupMilliseconds = setup; snapshot.readySinceStartupMilliseconds = startup;
            snapshot.focusedAtStart = focused; snapshot.runInBackground = background;
            gc0 = GC.CollectionCount(0); gc1 = GC.CollectionCount(1); gc2 = GC.CollectionCount(2);
        }
        public void FocusChanged() { snapshot.focusChanges++; }
        public void PauseChanged() { snapshot.pauseChanges++; }
        public bool Observe(double now, double startup, double unityMilliseconds, bool focused)
        {
            var interval = Math.Max(0, now-previous); previous = now;
            if (!Measuring)
            {
                snapshot.warmupFrames++;
                snapshot.maximumWarmupFrameMilliseconds = Math.Max(snapshot.maximumWarmupFrameMilliseconds, Math.Max(interval, unityMilliseconds));
                if (now-began >= 1000)
                {
                    snapshot.warmupMilliseconds = now-began;
                    snapshot.interactiveSinceStartupMilliseconds = startup;
                    measurementBegan = now; Measuring = true;
                    gc0 = GC.CollectionCount(0); gc1 = GC.CollectionCount(1); gc2 = GC.CollectionCount(2);
                }
                return false;
            }
            snapshot.sampledFrames++;
            snapshot.measuredWallMilliseconds = now-measurementBegan;
            snapshot.maximumStopwatchFrameMilliseconds = Math.Max(snapshot.maximumStopwatchFrameMilliseconds, interval);
            var worst = Math.Max(interval, unityMilliseconds);
            if (worst > 33.334) snapshot.framesOver33Milliseconds++;
            if (worst > 50) snapshot.framesOver50Milliseconds++;
            if (worst > 100) snapshot.framesOver100Milliseconds++;
            var g0 = GC.CollectionCount(0); var g1 = GC.CollectionCount(1); var g2 = GC.CollectionCount(2);
            if (worst > 33.334 && spikes.Count < 32)
                spikes.Add(new PrototypeFrameSpike { frame = snapshot.sampledFrames,
                    elapsedMilliseconds = snapshot.measuredWallMilliseconds, stopwatchMilliseconds = interval,
                    unityMilliseconds = unityMilliseconds, focused = focused,
                    gc0 = Math.Max(0,g0-gc0), gc1 = Math.Max(0,g1-gc1), gc2 = Math.Max(0,g2-gc2) });
            gc0 = g0; gc1 = g1; gc2 = g2;
            return true;
        }
        public PrototypeFrameSnapshot Snapshot() { snapshot.frameSpikes = spikes.ToArray(); return snapshot; }
    }
}
""";

    private const string FrameTimingTestsSource = """
using NUnit.Framework;

namespace ForgeCoreGameTemplates.Tests
{
    public sealed class PrototypeFrameTimingTests
    {
        [Test]
        public void LoadingBoundary_IsReportedSeparatelyWithoutHidingLaterGameplayHitch()
        {
            var tracker = new PrototypeFrameDiagnostics(); tracker.Begin(0,1000,800,20,2,true,false);
            Assert.That(tracker.Observe(1700,2700,1700,true), Is.False);
            Assert.That(tracker.Observe(1716,2716,16,true), Is.True);
            Assert.That(tracker.Observe(3216,4216,1500,true), Is.True);
            var value = tracker.Snapshot();
            Assert.That(value.maximumWarmupFrameMilliseconds, Is.EqualTo(1700));
            Assert.That(value.warmupMilliseconds, Is.EqualTo(1700));
            Assert.That(value.sampledFrames, Is.EqualTo(2));
            Assert.That(value.maximumStopwatchFrameMilliseconds, Is.EqualTo(1500));
            Assert.That(value.framesOver100Milliseconds, Is.EqualTo(1));
            Assert.That(value.frameSpikes[0].frame, Is.EqualTo(2));
        }
        [Test]
        public void LongFrameRecords_AreBoundedAndCaptureFocusWithoutDiscardingIt()
        {
            var tracker = new PrototypeFrameDiagnostics(); tracker.Begin(0,1000,800,20,2,true,false);
            tracker.Observe(1000,2000,16,true); tracker.FocusChanged(); tracker.PauseChanged();
            for (var i=1; i<=100; i++) tracker.Observe(1000+i*200,2000+i*200,200,false);
            var value = tracker.Snapshot();
            Assert.That(value.frameSpikes.Length, Is.EqualTo(32));
            Assert.That(value.framesOver100Milliseconds, Is.EqualTo(100));
            Assert.That(value.focusChanges, Is.EqualTo(1)); Assert.That(value.pauseChanges, Is.EqualTo(1));
            Assert.That(value.frameSpikes[0].focused, Is.False);
        }
    }
}
""";
}
