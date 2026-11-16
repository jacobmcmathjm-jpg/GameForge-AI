#nullable disable
using System;
using System.Collections.Generic;
namespace ForgeCore.Demo
{
    [Serializable]public struct DemoTimingContext {public int gc0,gc1,gc2;public long managedBytes,allocatedBytes;public bool focused;}
    [Serializable]public sealed class DemoPhaseSummary {public string phase="";public int frames,longFrames,over100,supplementaryUnityOver100;public double wallMilliseconds,maximumWallMilliseconds,maximumUnityMilliseconds;}
    [Serializable]public sealed class DemoTimingWindow {public int id,frames;public string kind="";public double startMilliseconds,endMilliseconds,maximumWallMilliseconds;}
    [Serializable]public sealed class DemoTimingEvent {
        public int frame,window,gc0,gc1,gc2,droppedWorkSamples;public string phase="";
        public double startMilliseconds,endMilliseconds,wallMilliseconds,unityMilliseconds;
        public bool warmupBoundary,focused;public long managedBeforeBytes,managedAfterBytes,allocatedAfterBytes;
        public DemoWorkSample[] overlappingWork=Array.Empty<DemoWorkSample>();
    }
    [Serializable]public sealed class DemoTimingReport {
        public int schemaVersion=2,maximumEvents=64,maximumWindows=16,observedFrames,longFrames,droppedLongEvents,droppedFrames,droppedWindows;
        public string supportMapVariant="cached_bone_roles";
        public DemoWorkSnapshot scopedWork;
        public string clock="stopwatch_update_intervals",unityClock="supplementary_unscaled_delta_not_same_interval";
        public double unityAwakeMilliseconds,unityReadyMilliseconds,warmupMilliseconds=2000,provisionalGameplayMaximumMilliseconds=100;
        public bool truncated,measuredGameplayMaximumPassed,wholeSessionSmoothnessAccepted=false;
        public DemoPhaseSummary[] phases=Array.Empty<DemoPhaseSummary>();public DemoTimingWindow[] windows=Array.Empty<DemoTimingWindow>();
        public DemoTimingEvent[] events=Array.Empty<DemoTimingEvent>(),worstByPhase=Array.Empty<DemoTimingEvent>();
    }
    // Additional opt-in diagnostics; does not control input, clocks, audio, saves, warmup or acceptance of the game.
    public sealed class DemoTimingTrace
    {
        public const int MaximumEvents=64,MaximumWindows=16,MaximumFrames=1000000,MaximumWorkSamples=8;
        public const double WarmupMilliseconds=2000,LongFrameMilliseconds=33.334;
        private readonly DemoTimingReport report=new DemoTimingReport();
        private readonly DemoPhaseSummary[] phases=new[]{Phase("startup"),Phase("warmup"),Phase("gameplay"),Phase("idle"),Phase("paused")};
        private readonly List<DemoTimingEvent> events=new List<DemoTimingEvent>(MaximumEvents);
        private readonly DemoTimingEvent[] worst=new DemoTimingEvent[5];private readonly List<DemoTimingWindow> windows=new List<DemoTimingWindow>(MaximumWindows);
        private double previous;private DemoTimingContext context;private DemoTimingWindow window;
        public DemoTimingTrace(double unityAwakeMilliseconds,DemoTimingContext initial) {
            if(!Finite(unityAwakeMilliseconds)||!Valid(initial))throw new ArgumentOutOfRangeException();report.unityAwakeMilliseconds=unityAwakeMilliseconds;context=initial;
        }
        private static DemoPhaseSummary Phase(string name)=>new DemoPhaseSummary{phase=name};
        public void Ready(double unityMilliseconds){if(!Finite(unityMilliseconds)||unityMilliseconds<report.unityAwakeMilliseconds)throw new ArgumentOutOfRangeException();report.unityReadyMilliseconds=unityMilliseconds;}
        public void BeginWindow(double now,string kind) {
            if(!Finite(now)||now<previous||(kind!="gameplay"&&kind!="idle"))throw new ArgumentOutOfRangeException();
            if(window!=null)window.endMilliseconds=now;
            if(windows.Count==MaximumWindows){report.droppedWindows++;report.truncated=true;window=null;return;}
            window=new DemoTimingWindow{id=windows.Count+1,kind=kind,startMilliseconds=now,endMilliseconds=now};windows.Add(window);
        }
        public bool WillRetain(double now,double unityMilliseconds,string phase) {
            var i=Index(phase);var wall=now-previous;var severity=Math.Max(wall,unityMilliseconds);
            return severity>LongFrameMilliseconds&&(events.Count<MaximumEvents||worst[i]==null||wall>worst[i].wallMilliseconds);
        }
        public void Observe(double now,double unityMilliseconds,string phase,bool boundary,DemoTimingContext next,DemoWorkSample[] work=null) {
            var i=Index(phase);
            if(!Finite(now)||now<previous||now>1000000||!Finite(unityMilliseconds)||unityMilliseconds>120000||!Valid(next))throw new ArgumentOutOfRangeException();
            if(report.observedFrames==MaximumFrames){report.truncated=true;report.droppedFrames++;previous=now;context=next;return;}
            var wall=now-previous;var p=phases[i];p.frames++;report.observedFrames++;p.wallMilliseconds+=wall;
            p.maximumWallMilliseconds=Math.Max(p.maximumWallMilliseconds,wall);p.maximumUnityMilliseconds=Math.Max(p.maximumUnityMilliseconds,unityMilliseconds);
            if(phase=="gameplay"||phase=="idle")if(window!=null){window.frames++;window.maximumWallMilliseconds=Math.Max(window.maximumWallMilliseconds,wall);window.endMilliseconds=now;}
            var severity=Math.Max(wall,unityMilliseconds);
            if(wall>100)p.over100++;if(unityMilliseconds>100)p.supplementaryUnityOver100++;
            if(severity>LongFrameMilliseconds) {
                report.longFrames++;p.longFrames++;var keep=events.Count<MaximumEvents;
                if(keep||worst[i]==null||wall>worst[i].wallMilliseconds) {
                    var count=work==null?0:Math.Min(work.Length,MaximumWorkSamples);var retained=new DemoWorkSample[count];if(count>0)Array.Copy(work,retained,count);
                    var e=new DemoTimingEvent{frame=report.observedFrames,window=window==null?0:window.id,phase=phase,startMilliseconds=previous,endMilliseconds=now,
                        wallMilliseconds=wall,unityMilliseconds=unityMilliseconds,warmupBoundary=boundary,focused=next.focused,
                        gc0=Math.Max(0,next.gc0-context.gc0),gc1=Math.Max(0,next.gc1-context.gc1),gc2=Math.Max(0,next.gc2-context.gc2),
                        managedBeforeBytes=context.managedBytes,managedAfterBytes=next.managedBytes,allocatedAfterBytes=next.allocatedBytes,
                        overlappingWork=retained,droppedWorkSamples=work==null?0:work.Length-count};
                    if(keep)events.Add(e);
                    if(worst[i]==null||wall>worst[i].wallMilliseconds)worst[i]=e;
                }
                if(!keep)report.droppedLongEvents++;
            }
            previous=now;context=next;
        }
        public DemoTimingReport Snapshot() {
            var worstEvents=new List<DemoTimingEvent>(5);foreach(var e in worst)if(e!=null)worstEvents.Add(e);
            report.events=events.ToArray();report.worstByPhase=worstEvents.ToArray();report.phases=phases;report.windows=windows.ToArray();
            var gameplayWindows=0;var passed=!report.truncated;
            foreach(var w in windows)if(w.kind=="gameplay"){gameplayWindows++;passed&=w.frames>0&&w.maximumWallMilliseconds<=100;}
            report.measuredGameplayMaximumPassed=gameplayWindows>0&&passed;return report;
        }
        private static int Index(string phase){switch(phase){case "startup":return 0;case "warmup":return 1;case "gameplay":return 2;case "idle":return 3;case "paused":return 4;default:throw new ArgumentException("Unknown timing phase.");}}
        private static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0;
        private static bool Valid(DemoTimingContext c)=>c.gc0>=0&&c.gc1>=0&&c.gc2>=0&&c.managedBytes>=0&&c.allocatedBytes>=0&&c.managedBytes<=(1L<<50)&&c.allocatedBytes<=(1L<<50);
    }
}
