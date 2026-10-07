using PanelDeck;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class DisplayFrameTests
{
    internal static void Run(Action<bool, string> check)
    {
        const long frequency = 60000;
        var c = new DisplayFrameCounter(frequency);
        void Feed(ulong chain, int fps, long start) {
            for (int i = 0; i <= fps * 2; i++) c.Add(chain, start + i * frequency / fps);
        }
        check(c.Read(frequency) == null, "No display events never fall back to submitted FPS");
        Feed(1, 60, frequency);
        check(c.Read(frequency * 3) == 60, "Displayed FPS uses screen QPC timestamps");
        // A delayed batch of generated frames arrives between already recorded application frames.
        for (int i = 0; i < 120; i++) c.Add(1, frequency + 500 + i * 1000);
        check(c.Read(frequency * 3) == 120, "Out-of-order generated display frames count exactly once");
        Feed(1, 60, frequency);
        check(c.Read(frequency * 3) == 120, "Repeated screen timestamps do not inflate FPS");
        Feed(2, 30, frequency);
        check(c.Read(frequency * 3) == 120, "Independent swap chains are never summed");
        Feed(2, 30, frequency * 5);
        check(c.Read(frequency * 7) == 30, "A stale formerly busy chain cannot mask the current chain");
        check(c.Read(frequency * 10) == null, "Display readings expire without new frames");
        c.Reset(); c.Add(1, 0); c.Add(1, -10);
        check(c.Read(1) == null, "Invalid screen timestamps never become readings");
        Feed(1, 60, frequency * 10);
        c.Add(1, 10);
        check(c.Read(frequency * 12) == 60, "Late events outside the window cannot contaminate current FPS");
        c.Reset(); c.Add(1, frequency); c.Add(1, frequency + 100);
        check(c.Read(frequency + 100) == null, "Too little display history stays unavailable");
        check(Marshal.SizeOf<DisplayFrameTrace.NativeFrame>() == 16 && Marshal.SizeOf<DisplayFrameTrace.NativeInfo>() == 32,
            "PresentMon native ABI field sizes match C++");
        check(DisplayFrameTrace.PdAbiVersion() == 1, "Packaged x64 PresentMon library loads without a service or VC runtime installer");
        Statistics(check);
    }
    private static void Statistics(Action<bool, string> check)
    {
        const long hz = 60000;
        var c = new DisplayFrameCounter(hz);
        for (int i = 0; i <= 1740; i++) c.Add(1, hz + i * 1000);
        var s = c.ReadStats(30 * hz);
        check(s.Fps == 60 && Math.Abs(s.FrameTimeMs!.Value - 1000f / 60) < .001 && s.Low1Percent == null,
            "Frame time uses the same display window; 1% Low waits for a full 30 seconds");
        for (int i = 1741; i <= 1800; i++) c.Add(1, hz + i * 1000);
        check(c.ReadStats(31 * hz).Low1Percent == 60, "Constant 60 FPS yields 60 FPS 1% Low");
        c.Reset();
        long t = hz; c.Add(1, t);
        // 2,880 ten-ms frames and thirty forty-ms frames fill exactly 30 seconds.
        for (int i = 0; i < 2880; i++) c.Add(1, t += 600);
        for (int i = 0; i < 30; i++) c.Add(1, t += 2400);
        check(c.ReadStats(t).Low1Percent == 25, "1% Low is reciprocal mean of the slowest ceil(1%) intervals, not a percentile reciprocal");
        for (int i = 0; i < 3100; i++) c.Add(1, t += 600);
        check(c.ReadStats(t).Low1Percent == 100, "Old stalls expire from the rolling 30-second statistic");
        for (int i = 0; i <= 120; i++) c.Add(2, t - 2 * hz + i * 1000);
        check(c.ReadStats(t).Low1Percent == 100, "A secondary chain never dilutes the active chain's low statistic");
        check(c.ReadStats(t + 3 * hz) == default(DisplayFrameStats), "All frame statistics expire together");
        for (int i = 0; i <= 120; i++) c.Add(1, t + 4 * hz + i * 1000);
        check(c.ReadStats(t + 6 * hz).Low1Percent == null, "Resume after a display gap needs a fresh low window");
        c.Reset();
        for (int i = 0; i <= 2400; i++) c.Add(1, hz + i * 1000);
        for (int i = 600; i < 2400; i++) c.Add(1, hz + 500 + i * 1000);
        check(c.ReadStats(41 * hz).Low1Percent == 120, "Late generated display frames split intervals without inflating 1% Low");
        c.Reset();
        for (int i = 0; i <= 50000; i++) c.Add(1, hz + i * 50);
        s = c.ReadStats(hz + 50000 * 50);
        check(s.Fps == 1200 && s.Low1Percent == null, "Bounded storage still reports FPS but refuses an incomplete 30-second low window");
        c.Reset();
        for (int i = 0; i <= 40000; i++) c.Add(1, hz + i * 100);
        s = c.ReadStats(hz + 40000 * 100);
        check(s.Fps == 600 && s.Low1Percent == 600, "Circular storage preserves full-window statistics after wrapping");
    }
    // Explicit diagnostic mode only. Does not run during ordinary tests or app startup.
    internal static async Task Probe(string path, int seconds)
    {
        var samples = new List<object>();
        using var process = Process.GetCurrentProcess();
        var start = process.TotalProcessorTime; var timer = Stopwatch.StartNew();
        DisplayFrameTrace? trace = null; FullscreenTarget? previous = null;
        try {
            for (int i = 0; i < seconds; i++) {
                var target = FrameRateMonitor.FindFullscreen();
                if (target != previous) { trace?.Dispose(); trace = null; previous = target; }
                if (target != null && trace == null) trace = new(target.Pid, target.Window);
                var value = trace?.Read(); var info = trace?.Diagnostics;
                samples.Add(new { elapsed = timer.Elapsed.TotalSeconds, target = target?.Name, value,
                    frames = info?.Count, lost = info?.EventsLost, buffersLost = info?.BuffersLost,
                    overflow = info?.Overflows, lostPresents = info?.LostPresents, discarded = info?.Discarded,
                    failed = trace?.Failed });
                await Task.Delay(1000);
            }
        } finally {
            trace?.Dispose(); process.Refresh();
            File.WriteAllText(path, JsonSerializer.Serialize(new { seconds = timer.Elapsed.TotalSeconds,
                cpuPercent = (process.TotalProcessorTime - start).TotalSeconds / timer.Elapsed.TotalSeconds / Environment.ProcessorCount * 100,
                workingSet = process.WorkingSet64, samples }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
