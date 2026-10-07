using PanelDeck;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

internal static class FrameRateTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        const long frequency = 60000;
        var c = new FrameRateCounter(frequency);
        void Frames(ulong chain, int fps, long start, bool d3d9 = false) {
            for (int i = 0; i <= fps * 2; i++) {
                c.Begin(d3d9, 1, chain, start + i * frequency / fps, 0, 43);
                c.End(d3d9, 1, 43, 0);
            }
        }
        check(c.Read(1) == null, "FPS empty window stays unknown");
        Frames(1, 60, frequency);
        check(Math.Abs(c.Read(frequency * 3)!.Value - 60) < .01f, "FPS uses elapsed QPC time, not callback delivery bursts");
        Frames(2, 30, frequency);
        check(c.Read(frequency * 3) == 60, "FPS does not add rates of multiple swap chains");
        check(c.Read(frequency * 6) == null, "FPS expires after frames stop");
        c.Reset();
        for (int i = 0; i < 120; i++) {
            c.Begin(false, 1, 1, frequency + i * 1000, 1, 43); c.End(false, 1, 43, 0);
            c.Begin(false, 1, 1, frequency + i * 1000, 0, 43); c.End(false, 1, 43, 0x087A0001);
            c.Begin(true, 1, 1, frequency + i * 1000, 4, 2); c.End(true, 1, 2, 0);
        }
        check(c.Read(frequency * 3) == null, "Test presents, occluded presents and D3D9 DONOTFLIP never count");
        c.Begin(false, 1, 1, 1, 0, 43); c.End(false, 1, 56, 0);
        check(c.Read(2) == null, "Unmatched start/stop events are ignored");
        Frames(1, 120, frequency, true);
        check(c.Read(frequency * 3) == 120, "D3D9 frames are counted independently");
        c.Reset(); check(c.Read(frequency * 3) == null, "Target change/lost events discard old FPS");
        Frames(1, 600, frequency); // Warm up bounded storage and JIT before measuring accounting overhead.
        long allocated = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        for (int i = 0; i < 120000; i++) { c.Begin(false, 1, 1, frequency * 4 + i * 100, 0, 43); c.End(false, 1, 43, 0); }
        watch.Stop(); allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        check(allocated < 4096, "FPS per-frame accounting remains allocation-free after warmup");
        Console.WriteLine($"FPS SYNTHETIC ACCOUNTING: 120000 frames={watch.Elapsed.TotalMilliseconds:F2} ms, allocations={allocated} bytes; excludes ETW/provider/game cost");
        check(FrameRateMonitor.CoversMonitor(-1920, 0, 0, 1080, -1920, 0, 0, 1080), "Fullscreen supports secondary monitors with negative coordinates");
        check(!FrameRateMonitor.CoversMonitor(0, 30, 1920, 1080, 0, 0, 1920, 1080), "Ordinary maximized windows do not start FPS capture");
        check(Marshal.SizeOf<PresentTrace.TraceProperties>() == 120 && Marshal.SizeOf<PresentTrace.TraceLog>() == 448, "Native ETW structures have x64 SDK sizes");
        Decode(check);
        string pipe = "PanelDeck.Fps.Test." + Guid.NewGuid().ToString("N");
        using var stop = new CancellationTokenSource();
        int enabled = 0, disabled = 0;
        var serving = FrameRatePipe.Serve(pipe, WindowsIdentity.GetCurrent().User!, stop.Token, on => {
            if (on) Interlocked.Increment(ref enabled); else Interlocked.Increment(ref disabled);
            return new("显示帧率", "FPS", on ? 60 : null, on ? "synthetic · 显示 FPS" : "paused", on ? 16.67f : null, on ? 45 : null);
        }, leaseMs: 100);
        var result = await FrameRatePipe.Read(pipe, true, CancellationToken.None);
        check(result.Value == 60, "FPS round-trips over ACL-protected local pipe");
        check(result.FrameTimeMs == 16.67f && result.Low1Percent == 45, "Frame time and 1% Low share the existing pipe snapshot");
        await Task.Delay(1250);
        check(Volatile.Read(ref disabled) >= 1, "Abandoned FPS lease automatically stops capture");
        result = await FrameRatePipe.Read(pipe, false, CancellationToken.None);
        check(result.Value == null && result.FrameTimeMs == null && result.Low1Percent == null, "Explicit pause clears all game statistics immediately");
        stop.Cancel(); await serving.WaitAsync(TimeSpan.FromSeconds(3));
        check(true, "FPS pipe and watchdog cancel cleanly");
    }
    private static void Decode(Action<bool, string> check)
    {
        IntPtr record = Marshal.AllocHGlobal(112), payload = Marshal.AllocHGlobal(64);
        try {
            foreach (bool x86 in new[] { false, true }) {
                var runtime = new FrameRateCounter(60000); var kernel = new FrameRateCounter(60000);
                Marshal.Copy(new byte[112], 0, record, 112); Marshal.Copy(new byte[64], 0, payload, 64);
                Marshal.WriteInt16(record, 4, (short)(x86 ? 0x20 : 0x40));
                Marshal.WriteInt32(record, 8, 9); Marshal.WriteInt32(record, 12, 123);
                Marshal.WriteInt16(record, 86, 32); Marshal.WriteIntPtr(record, 96, payload);
                for (int i = 0; i <= 120; i++) {
                    Marshal.WriteInt64(record, 16, 60000 + i * 1000);
                    Marshal.WriteInt16(record, 40, 42); Marshal.WriteInt64(payload, 0, 999); Marshal.WriteInt32(payload, x86 ? 4 : 8, 0);
                    PresentTrace.Decode(record, 123, 77, runtime, kernel);
                    Marshal.WriteInt16(record, 40, 43); Marshal.WriteInt32(payload, 0, 0);
                    PresentTrace.Decode(record, 123, 77, runtime, kernel);
                }
                check(runtime.Read(180000) == 60, $"Native decoder handles {(x86 ? "32" : "64")}-bit DXGI event pointer fields");
                Marshal.WriteInt16(record, 40, 184); Marshal.WriteByte(record, 42, 1);
                for (int i = 0; i <= 120; i++) {
                    Marshal.WriteInt64(record, 16, 60000 + i * 1000);
                    Marshal.WriteInt64(payload, 4, 77); Marshal.WriteInt32(payload, 16 + (x86 ? 4 : 8), 0);
                    PresentTrace.Decode(record, 123, 77, runtime, kernel);
                }
                check(kernel.Read(180000) == 60, "Kernel fallback filters and decodes the target window");
                runtime.Reset(); kernel.Reset();
                PresentTrace.Decode(record, 124, 77, runtime, kernel);
                PresentTrace.Decode(record, 123, 78, runtime, kernel);
                check(kernel.Read(180000) == null, "Wrong process/window cannot leak into game FPS");
            }
        } finally { Marshal.FreeHGlobal(record); Marshal.FreeHGlobal(payload); }
    }
}
