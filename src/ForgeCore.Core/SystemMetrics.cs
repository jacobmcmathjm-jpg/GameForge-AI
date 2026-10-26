using System.Diagnostics;

namespace ForgeCore.Core;

public sealed class ProcessSystemMetrics : ISystemMetrics
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _lastCpu; private long _lastTimestamp = Stopwatch.GetTimestamp();

    public ValueTask<SystemSnapshot> SampleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); _process.Refresh();
        var now = Stopwatch.GetTimestamp(); var cpu = _process.TotalProcessorTime;
        var elapsedSeconds = (now - _lastTimestamp) / (double)Stopwatch.Frequency;
        var cpuSeconds = (cpu - _lastCpu).TotalSeconds;
        var percent = elapsedSeconds <= 0 ? 0 : Math.Clamp(cpuSeconds / elapsedSeconds / Environment.ProcessorCount * 100, 0, 100);
        _lastTimestamp = now; _lastCpu = cpu;
        return ValueTask.FromResult(new SystemSnapshot(percent, _process.WorkingSet64, "status placeholder"));
    }
}
