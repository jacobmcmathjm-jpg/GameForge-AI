using System;
using System.Collections.Generic;
namespace ForgeCore.Demo
{
    public enum DemoWorkKind { StateWrite,StateRead,StateFlush,StateReplace,CheckpointRestore,NavigationRefresh,RouteSearch,CorpseSupport,ProbeCapture,GraphicsSettings,BackgroundWrite,BackgroundFlush,BackgroundReplace,FootworkInitialize,FootworkWeightMap,ActorReset,AnimatorRebind,NewGameCheckpoint }
    [Serializable]public struct DemoWorkSample {public string kind;public int frame;public double startMilliseconds,durationMilliseconds;}
    [Serializable]public struct DemoWorkStatistic {public string kind;public int count;public double totalMilliseconds,maximumMilliseconds;}
    [Serializable]public sealed class DemoWorkSnapshot {public int totalSamples;public DemoWorkSample[] recent=Array.Empty<DemoWorkSample>();public DemoWorkStatistic[] statistics=Array.Empty<DemoWorkStatistic>();}
    // Allocation-free recording, bounded retention. Nested scopes overlap: durations must NOT be added as exclusive CPU time.
    public sealed class DemoWorkJournal
    {
        public const int Capacity=256;
        private static readonly string[] Names=Enum.GetNames(typeof(DemoWorkKind));
        private readonly object gate=new object();private readonly DemoWorkSample[] samples=new DemoWorkSample[Capacity];
        private readonly int[] counts=new int[Names.Length];private readonly double[] sums=new double[Names.Length],maxima=new double[Names.Length];
        private int next,count,total;
        public void Add(DemoWorkKind kind,int frame,double start,double duration) {
            var k=(int)kind;if(k<0||k>=Names.Length||frame<0||!Finite(start)||!Finite(duration)||start<0||duration<0||duration>120000)throw new ArgumentOutOfRangeException();
            lock(gate){samples[next]=new DemoWorkSample{kind=Names[k],frame=frame,startMilliseconds=start,durationMilliseconds=duration};
                next=(next+1)%Capacity;count=Math.Min(Capacity,count+1);total++;counts[k]++;sums[k]+=duration;maxima[k]=Math.Max(maxima[k],duration);}
        }
        public DemoWorkSample[] Overlapping(double start,double end) {
            if(!Finite(start)||!Finite(end)||start<0||end<start)throw new ArgumentOutOfRangeException();
            lock(gate){var found=new List<DemoWorkSample>();for(var i=0;i<count;i++){var s=samples[(next-count+i+Capacity)%Capacity];
                if(s.startMilliseconds<end&&s.startMilliseconds+s.durationMilliseconds>start)found.Add(s);}return found.ToArray();}
        }
        public DemoWorkSnapshot Snapshot() {
            lock(gate){var recent=new DemoWorkSample[count];for(var i=0;i<count;i++)recent[i]=samples[(next-count+i+Capacity)%Capacity];
                var stats=new DemoWorkStatistic[Names.Length];for(var k=0;k<stats.Length;k++)stats[k]=new DemoWorkStatistic{kind=Names[k],count=counts[k],totalMilliseconds=sums[k],maximumMilliseconds=maxima[k]};
                return new DemoWorkSnapshot{totalSamples=total,recent=recent,statistics=stats};}
        }
        private static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
    }
}
