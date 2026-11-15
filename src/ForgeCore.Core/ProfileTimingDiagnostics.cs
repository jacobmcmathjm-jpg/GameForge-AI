#nullable disable
using System;
using System.Collections.Generic;
using System.IO;

namespace ForgeCore.Diagnostics
{
    // This exact C#9 source is also embedded in the generated Unity runtime. No Unity or JSON dependency.
    [Serializable] public sealed class ProfileTimingReport
    {
        public int schemaVersion = 1, maximumLongFrameEvents = 64, observedFrames, longFrameCount, droppedLongFrameEvents;
        public string clock = "unity_realtime_since_startup", ownership = "frame_end_including_boundary_crossing";
        public string timestampMethod = "end_realtime_minus_unscaled_delta";
        public double runtimeBeforeSceneLoadSeconds, profileInitializedSeconds, warmupSeconds = 1, gameplaySeconds;
        public double longFrameThresholdMilliseconds = 33.334;
        public ProfilePhaseTiming[] phases;
        public List<ProfileLongFrame> longFrames = new List<ProfileLongFrame>(64);
        public ProfileLongFrame worstLongFrame;
    }
    [Serializable] public sealed class ProfilePhaseTiming
    {
        public string phase;
        public double startSeconds, plannedEndSeconds, lastObservedSeconds, contextObservedFromSeconds;
        public int sampledFrames, longFrameCount, over100Milliseconds, over250Milliseconds, over1000Milliseconds;
        public double averageFrameMilliseconds, maximumFrameMilliseconds;
        public int gc0Collections, gc1Collections, gc2Collections;
        public long managedMemoryStartBytes, managedMemoryEndBytes, peakAllocatedMemoryBytes;
    }
    [Serializable] public sealed class ProfileLongFrame
    {
        public string phase;
        public int frameIndex, gc0Collections, gc1Collections, gc2Collections;
        public double startSeconds, endSeconds, frameMilliseconds;
        public bool crossesStartupBoundary, crossesWarmupBoundary;
        public long managedMemoryBeforeBytes, managedMemoryAfterBytes, allocatedMemoryAfterBytes;
    }
    public struct ProfileTimingContext
    {
        public int gc0, gc1, gc2;
        public long managedBytes, allocatedBytes;
    }
    public sealed class ProfileTimingCollector
    {
        public const int MaximumEvents = 64, MaximumFrames = 1000000;
        public const double WarmupSeconds = 1, LongFrameMilliseconds = 33.334;
        private readonly ProfileTimingReport report;
        private ProfileTimingContext previous;
        private double previousEnd;
        public ProfileTimingCollector(double beforeSceneLoadSeconds, double initializedSeconds, int gameplaySeconds,
            ProfileTimingContext beforeScene, ProfileTimingContext initialized)
        {
            if (!Finite(beforeSceneLoadSeconds) || !Finite(initializedSeconds) || initializedSeconds < beforeSceneLoadSeconds || initializedSeconds > 300 ||
                gameplaySeconds < 5 || gameplaySeconds > 30 || !ContextValid(beforeScene) || !ContextValid(initialized))
                throw new ArgumentException("Invalid timing origin/context.");
            report = new ProfileTimingReport { runtimeBeforeSceneLoadSeconds = beforeSceneLoadSeconds,
                profileInitializedSeconds = initializedSeconds, gameplaySeconds = gameplaySeconds,
                phases = new[] { Phase("startup", 0, initializedSeconds, beforeSceneLoadSeconds, beforeScene),
                    Phase("warmup", initializedSeconds, initializedSeconds + WarmupSeconds, initializedSeconds, initialized),
                    Phase("gameplay", initializedSeconds + WarmupSeconds, initializedSeconds + WarmupSeconds + gameplaySeconds,
                        initializedSeconds, initialized) } };
            UpdateContext(report.phases[0], beforeScene, initialized);
            report.phases[0].lastObservedSeconds = initializedSeconds;
            previous = initialized; previousEnd = initializedSeconds;
        }
        private static ProfilePhaseTiming Phase(string name, double start, double end, double contextStart, ProfileTimingContext c) =>
            new ProfilePhaseTiming { phase = name, startSeconds = start, plannedEndSeconds = end,
                lastObservedSeconds = start, contextObservedFromSeconds = contextStart,
                managedMemoryStartBytes = c.managedBytes, managedMemoryEndBytes = c.managedBytes, peakAllocatedMemoryBytes = c.allocatedBytes };
        public void Record(double endSeconds, double frameMilliseconds, ProfileTimingContext context)
        {
            if (!Finite(endSeconds) || endSeconds < previousEnd || endSeconds > 420 || !Finite(frameMilliseconds) ||
                frameMilliseconds <= 0 || frameMilliseconds > endSeconds * 1000 + .001 || !ContextValid(context) || report.observedFrames >= MaximumFrames)
                throw new InvalidDataException("Timing sample outside bounded monotonic contract.");
            var gameplay = endSeconds >= report.profileInitializedSeconds + WarmupSeconds;
            var phase = report.phases[gameplay ? 2 : 1];
            if (phase.sampledFrames == 0) { phase.contextObservedFromSeconds = previousEnd; phase.managedMemoryStartBytes = previous.managedBytes; }
            phase.sampledFrames++; report.observedFrames++;
            phase.averageFrameMilliseconds += (frameMilliseconds - phase.averageFrameMilliseconds) / phase.sampledFrames;
            phase.maximumFrameMilliseconds = Math.Max(phase.maximumFrameMilliseconds, frameMilliseconds);
            phase.lastObservedSeconds = endSeconds; UpdateContext(phase, previous, context);
            if (frameMilliseconds > 100) phase.over100Milliseconds++;
            if (frameMilliseconds > 250) phase.over250Milliseconds++;
            if (frameMilliseconds > 1000) phase.over1000Milliseconds++;
            if (frameMilliseconds > LongFrameMilliseconds)
            {
                phase.longFrameCount++; report.longFrameCount++;
                var keep = report.longFrames.Count < MaximumEvents;
                var worst = report.worstLongFrame == null || frameMilliseconds > report.worstLongFrame.frameMilliseconds;
                if (keep || worst)
                {
                    var start = Math.Max(0, endSeconds - frameMilliseconds / 1000);
                    var entry = new ProfileLongFrame { phase = phase.phase, frameIndex = report.observedFrames,
                        startSeconds = start, endSeconds = endSeconds, frameMilliseconds = frameMilliseconds,
                        crossesStartupBoundary = start < report.profileInitializedSeconds && endSeconds >= report.profileInitializedSeconds,
                        crossesWarmupBoundary = start < report.profileInitializedSeconds + WarmupSeconds && gameplay,
                        gc0Collections = Math.Max(0, context.gc0 - previous.gc0), gc1Collections = Math.Max(0, context.gc1 - previous.gc1),
                        gc2Collections = Math.Max(0, context.gc2 - previous.gc2), managedMemoryBeforeBytes = previous.managedBytes,
                        managedMemoryAfterBytes = context.managedBytes, allocatedMemoryAfterBytes = context.allocatedBytes };
                    if (keep) report.longFrames.Add(entry);
                    if (worst) report.worstLongFrame = entry;
                }
                if (!keep) report.droppedLongFrameEvents++;
            }
            previous = context; previousEnd = endSeconds;
        }
        private static void UpdateContext(ProfilePhaseTiming p, ProfileTimingContext a, ProfileTimingContext b)
        {
            p.gc0Collections += Math.Max(0, b.gc0 - a.gc0); p.gc1Collections += Math.Max(0, b.gc1 - a.gc1); p.gc2Collections += Math.Max(0, b.gc2 - a.gc2);
            p.managedMemoryEndBytes = b.managedBytes; p.peakAllocatedMemoryBytes = Math.Max(p.peakAllocatedMemoryBytes, b.allocatedBytes);
        }
        public ProfileTimingReport Snapshot() => report; // Only serialize after sampling stops; no frame-path JSON/log/file I/O.
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        private static bool Memory(long value) => value >= 0 && value <= 137438953472L;
        private static bool ContextValid(ProfileTimingContext c) => c.gc0 >= 0 && c.gc1 >= 0 && c.gc2 >= 0 && Memory(c.managedBytes) && Memory(c.allocatedBytes);

        public static void Validate(ProfileTimingReport r, int durationSeconds, int measuredFrames, double measuredAverage, double measuredMaximum)
        {
            void Require(bool valid) { if (!valid) throw new InvalidDataException("Phase timing diagnostics failed the bounded evidence contract."); }
            bool Near(double a, double b) => Finite(a) && Finite(b) && Math.Abs(a - b) <= .0001;
            Require(r != null && r.schemaVersion == 1 && r.maximumLongFrameEvents == MaximumEvents && r.clock == "unity_realtime_since_startup" &&
                r.ownership == "frame_end_including_boundary_crossing" && r.timestampMethod == "end_realtime_minus_unscaled_delta" && Near(r.warmupSeconds, WarmupSeconds) && Near(r.gameplaySeconds, durationSeconds) &&
                Near(r.longFrameThresholdMilliseconds, LongFrameMilliseconds) && Finite(r.runtimeBeforeSceneLoadSeconds) &&
                r.runtimeBeforeSceneLoadSeconds <= r.profileInitializedSeconds && r.profileInitializedSeconds <= 300 &&
                r.phases != null && r.phases.Length == 3 && r.longFrames != null && r.longFrames.Count <= MaximumEvents &&
                r.observedFrames >= measuredFrames && r.observedFrames <= MaximumFrames && r.longFrameCount >= 0 && r.longFrameCount <= r.observedFrames &&
                r.longFrames.Count == Math.Min(MaximumEvents, r.longFrameCount) && r.droppedLongFrameEvents == r.longFrameCount - r.longFrames.Count);
            for (var i = 0; i < 3; i++)
            {
                var p = r.phases[i]; var start = i == 0 ? 0 : r.profileInitializedSeconds + (i == 2 ? WarmupSeconds : 0);
                var end = i == 0 ? r.profileInitializedSeconds : r.profileInitializedSeconds + WarmupSeconds + (i == 2 ? durationSeconds : 0);
                Require(p != null && p.phase == new[] { "startup", "warmup", "gameplay" }[i] && Near(p.startSeconds, start) && Near(p.plannedEndSeconds, end) &&
                    Finite(p.lastObservedSeconds) && p.lastObservedSeconds >= start && p.lastObservedSeconds <= 420 &&
                    Finite(p.contextObservedFromSeconds) && p.contextObservedFromSeconds <= p.lastObservedSeconds &&
                    p.sampledFrames >= 0 && p.sampledFrames <= MaximumFrames && p.longFrameCount >= 0 && p.longFrameCount <= p.sampledFrames &&
                    p.over100Milliseconds >= 0 && p.over100Milliseconds <= p.longFrameCount && p.over250Milliseconds >= 0 && p.over250Milliseconds <= p.over100Milliseconds &&
                    p.over1000Milliseconds >= 0 && p.over1000Milliseconds <= p.over250Milliseconds &&
                    Finite(p.averageFrameMilliseconds) && Finite(p.maximumFrameMilliseconds) && p.maximumFrameMilliseconds >= p.averageFrameMilliseconds &&
                    p.gc0Collections >= 0 && p.gc1Collections >= 0 && p.gc2Collections >= 0 && Memory(p.managedMemoryStartBytes) && Memory(p.managedMemoryEndBytes) && Memory(p.peakAllocatedMemoryBytes));
                if (i == 0) Require(p.sampledFrames == 0 && Near(p.lastObservedSeconds, end));
                if (i == 1) Require(p.sampledFrames == 0 || p.lastObservedSeconds < end);
                if (i == 2) Require(p.sampledFrames == measuredFrames && p.lastObservedSeconds >= end && Near(p.averageFrameMilliseconds, measuredAverage) && Near(p.maximumFrameMilliseconds, measuredMaximum));
            }
            Require(r.observedFrames == r.phases[1].sampledFrames + r.phases[2].sampledFrames &&
                r.longFrameCount == r.phases[1].longFrameCount + r.phases[2].longFrameCount);
            int previousIndex = 0; double previousTime = 0;
            void ValidateEvent(ProfileLongFrame e)
            {
                Require(e != null && e.frameIndex >= 1 && e.frameIndex <= r.observedFrames && Finite(e.startSeconds) && Finite(e.endSeconds) && e.endSeconds <= 420 &&
                    e.endSeconds >= r.profileInitializedSeconds && e.startSeconds <= e.endSeconds && e.frameMilliseconds > LongFrameMilliseconds &&
                    Near((e.endSeconds - e.startSeconds) * 1000, e.frameMilliseconds) &&
                    e.phase == (e.endSeconds >= r.profileInitializedSeconds + WarmupSeconds ? "gameplay" : "warmup") &&
                    e.crossesStartupBoundary == (e.startSeconds < r.profileInitializedSeconds && e.endSeconds >= r.profileInitializedSeconds) &&
                    e.crossesWarmupBoundary == (e.startSeconds < r.profileInitializedSeconds + WarmupSeconds && e.endSeconds >= r.profileInitializedSeconds + WarmupSeconds) &&
                    e.gc0Collections >= 0 && e.gc1Collections >= 0 && e.gc2Collections >= 0 && Memory(e.managedMemoryBeforeBytes) && Memory(e.managedMemoryAfterBytes) && Memory(e.allocatedMemoryAfterBytes));
            }
            foreach (var e in r.longFrames) { ValidateEvent(e); Require(e.frameIndex > previousIndex && e.endSeconds >= previousTime); previousIndex = e.frameIndex; previousTime = e.endSeconds; }
            if (r.longFrameCount == 0) Require(r.worstLongFrame == null);
            else
            {
                ValidateEvent(r.worstLongFrame);
                Require(Near(r.worstLongFrame.frameMilliseconds, Math.Max(r.phases[1].maximumFrameMilliseconds, r.phases[2].maximumFrameMilliseconds)));
                foreach (var e in r.longFrames) Require(e.frameMilliseconds <= r.worstLongFrame.frameMilliseconds);
            }
        }
    }
}
