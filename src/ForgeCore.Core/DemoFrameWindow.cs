using System;
namespace ForgeCore.Demo
{
    [Serializable]public sealed class DemoFrameSummary
    {
        public int totalFrames,windowFrames,over33,over50,over100;
        public double measuredMilliseconds,p50,p95,p99,maximum;
    }
    // Percentiles describe only the latest bounded window; totals/max describe the whole unpaused sample.
    public sealed class DemoFrameWindow
    {
        public const int Capacity=4096;
        private readonly double[] frames=new double[Capacity];private int count,index,total,over33,over50,over100;
        private double sum,maximum;
        public void Add(double ms) {
            if(double.IsNaN(ms)||double.IsInfinity(ms)||ms<=0||ms>120000)throw new ArgumentOutOfRangeException(nameof(ms));
            frames[index]=ms;index=(index+1)%Capacity;count=Math.Min(Capacity,count+1);total++;sum+=ms;maximum=Math.Max(maximum,ms);
            if(ms>33.334)over33++;if(ms>50)over50++;if(ms>100)over100++;
        }
        public DemoFrameSummary Snapshot() {
            var sorted=new double[count];Array.Copy(frames,sorted,count);Array.Sort(sorted);
            return new DemoFrameSummary{totalFrames=total,windowFrames=count,over33=over33,over50=over50,over100=over100,
                measuredMilliseconds=sum,maximum=maximum,p50=Percentile(sorted,.50),p95=Percentile(sorted,.95),p99=Percentile(sorted,.99)};
        }
        private static double Percentile(double[] sorted,double q)=>sorted.Length==0?0:sorted[Math.Max(0,(int)Math.Ceiling(q*sorted.Length)-1)];
    }
}
