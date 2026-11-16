namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string DemoTelemetrySource="""
using System;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using ForgeCore.Demo;
namespace ForgeCoreGameTemplates
{
    public sealed class DemoRuntimeTelemetry:MonoBehaviour
    {
        [Serializable]public sealed class Spike {public int frame,gc0,gc1,gc2;public double startMilliseconds,elapsedMilliseconds;public DemoWorkSample[] overlappingWork;}
        public static DemoRuntimeTelemetry Instance {get;private set;}
        private DemoWorkJournal work=new DemoWorkJournal();private readonly List<Spike> spikes=new List<Spike>(128);
        private int priorGc0,priorGc1,priorGc2;
        private DemoFrameWindow samples=new DemoFrameWindow();private readonly Stopwatch timer=new Stopwatch();
        private double previous,warmup;private bool shown,active;private SecurityCombatSession session;
        private DemoTimingTrace timing;private string timingWindow="gameplay";private double timingPrevious;
        public double ReadyMilliseconds {get;private set;}
        public long AllocatedBytes=>Profiler.GetTotalAllocatedMemoryLong();
        public long ReservedBytes=>Profiler.GetTotalReservedMemoryLong();
        public DemoFrameSummary Snapshot()=>samples.Snapshot();
        public SecurityCombatSession Session=>session;
        private void Awake(){timer.Start();Instance=this;session=GetComponentInChildren<SecurityCombatSession>();
            if(!Application.isEditor&&Array.IndexOf(Environment.GetCommandLineArgs(),"-forgecore-timing-probe")>=0)
                timing=new DemoTimingTrace(Time.realtimeSinceStartupAsDouble*1000,TimingContext());}
        private void OnDestroy(){if(Instance==this)Instance=null;}
        public DemoWorkSnapshot WorkSnapshot()=>work.Snapshot();
        public Spike[] Spikes()=>spikes.ToArray();
        public static WorkScope Measure(DemoWorkKind kind)=>new WorkScope(Instance,kind);
        public static Action<DemoWorkKind,double> BackgroundRecorder() {
            var owner=Instance;if(owner==null)return null;
            var journal=owner.work;var clock=owner.timer;var frame=Time.frameCount;
            // This closure captures only managed objects and the main-thread frame number; no Unity API on the worker.
            return (kind,ms)=>{var end=clock.Elapsed.TotalMilliseconds;journal.Add(kind,frame,Math.Max(0,end-ms),ms);};
        }
        public struct WorkScope:IDisposable {
            private readonly DemoRuntimeTelemetry owner;private readonly DemoWorkKind kind;private readonly int frame;private readonly double start;
            public WorkScope(DemoRuntimeTelemetry owner,DemoWorkKind kind){this.owner=owner;this.kind=kind;frame=owner!=null?Time.frameCount:0;start=owner!=null?owner.timer.Elapsed.TotalMilliseconds:0;}
            public void Dispose(){if(owner!=null)owner.work.Add(kind,frame,start,Math.Max(0,owner.timer.Elapsed.TotalMilliseconds-start));}
        }
        private void Start(){ReadyMilliseconds=Time.realtimeSinceStartupAsDouble*1000;if(timing!=null)timing.Ready(ReadyMilliseconds);}
        public DemoTimingReport TimingSnapshot()=>timing==null?null:timing.Snapshot();
        public void ResetMeasurement(string kind="gameplay"){
            if(kind!="gameplay"&&kind!="idle")throw new ArgumentException("Unknown timing window.");timingWindow=kind;
            if(timing!=null)timing.BeginWindow(timer.Elapsed.TotalMilliseconds,kind);
            samples=new DemoFrameWindow();work=new DemoWorkJournal();spikes.Clear();active=false;warmup=0;ReadGc();}
        private static DemoTimingContext TimingContext()=>new DemoTimingContext{gc0=GC.CollectionCount(0),
            gc1=GC.MaxGeneration>=1?GC.CollectionCount(1):0,gc2=GC.MaxGeneration>=2?GC.CollectionCount(2):0,
            managedBytes=GC.GetTotalMemory(false),allocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),focused=Application.isFocused};
        private void RecordTiming(double now,string phase,bool boundary){
            if(timing==null)return;var unity=Time.unscaledDeltaTime*1000d;
            var overlap=timing.WillRetain(now,unity,phase)?work.Overlapping(timingPrevious,now):null;
            timing.Observe(now,unity,phase,boundary,TimingContext(),overlap);timingPrevious=now;}
        private void ReadGc(){priorGc0=GC.CollectionCount(0);priorGc1=GC.CollectionCount(1);priorGc2=GC.CollectionCount(2);}
        private void Update() {
            if(Input.GetKeyDown(KeyCode.F6))shown=!shown;
            var now=timer.Elapsed.TotalMilliseconds;
            var running=session!=null&&!session.Paused&&!session.Finished;
            if(!running){RecordTiming(now,ReadyMilliseconds<=0?"startup":"paused",false);active=false;previous=now;ReadGc();return;}
            if(!active){RecordTiming(now,"warmup",false);active=true;previous=now;ReadGc();return;}
            var began=previous;
            var elapsed=now-previous;previous=now;var priorWarmup=warmup;warmup+=elapsed;
            RecordTiming(now,warmup>=2000?timingWindow:"warmup",priorWarmup<2000&&warmup>=2000);
            if(warmup>=2000&&elapsed>0)samples.Add(elapsed);
            if(warmup>=2000&&elapsed>33.334&&spikes.Count<128)spikes.Add(new Spike{frame=Time.frameCount,startMilliseconds=began,elapsedMilliseconds=elapsed,
                gc0=GC.CollectionCount(0)-priorGc0,gc1=GC.CollectionCount(1)-priorGc1,gc2=GC.CollectionCount(2)-priorGc2,overlappingWork=work.Overlapping(began,now)});
            ReadGc();
        }
        private void OnGUI(){if(!shown)return;var s=Snapshot();GUI.Box(new Rect(14,112,470,96),"Local demo metrics — latest "+s.windowFrames+" frames");
            GUI.Label(new Rect(24,137,450,24),"Frame ms p50 / p95 / p99: "+s.p50.ToString("0.0")+" / "+s.p95.ToString("0.0")+" / "+s.p99.ToString("0.0"));
            GUI.Label(new Rect(24,160,450,24),"Whole sample max "+s.maximum.ToString("0.0")+"ms · >100ms "+s.over100+" · allocated "+(AllocatedBytes/1048576)+"MiB");
            GUI.Label(new Rect(24,183,450,24),"Paused/menu intervals excluded · small demo, not a full-game benchmark");}
    }
}
""";
}
