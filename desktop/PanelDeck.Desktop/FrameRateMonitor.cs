using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PanelDeck;

internal sealed record FullscreenTarget(uint Pid, long Started, string Name, IntPtr Window);
internal sealed record FrameRateSample(string Label, string Unit, float? Value, string? Source,
    float? FrameTimeMs = null, float? Low1Percent = null);

internal sealed class FrameRateMonitor : IDisposable
{
    private DisplayFrameTrace? trace;
    private FullscreenTarget? target;
    private long retryAt;
    private string? failure;
    internal static FrameRateSample Missing(string reason) => new("显示帧率", "FPS", null, reason);
    internal FrameRateSample Sample(bool enabled)
    {
        var next = enabled ? FindFullscreen() : null;
        if (next != target) { Dispose(); target = next; retryAt = 0; failure = null; }
        if (next == null) return Missing(enabled ? "等待全屏游戏" : "帧率采集已暂停");
        if (trace?.Failed == true) { Dispose(); failure = "帧率采集已中断"; retryAt = Environment.TickCount64 + 30000; }
        if (trace == null && Environment.TickCount64 >= retryAt) {
            try { trace = new DisplayFrameTrace(next.Pid, next.Window); failure = null; }
            catch (Exception ex) { failure = "帧率采集不可用"; retryAt = Environment.TickCount64 + 30000; DiagnosticLog.Write("fps-start", ex); }
        }
        var value = trace?.Read();
        return new("显示帧率", "FPS", value, next.Name + " · " + (failure ?? (value == null ? "显示帧率暂不可用" : "显示 FPS")),
            trace?.Statistics.FrameTimeMs, trace?.Statistics.Low1Percent);
    }
    internal static FullscreenTarget? FindFullscreen()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero || IsIconic(window) || !GetClientRect(window, out var client)) return null;
        var origin = new Point();
        if (!ClientToScreen(window, ref origin)) return null;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref info)) return null;
        if (!CoversMonitor(origin.X, origin.Y, origin.X + client.Right, origin.Y + client.Bottom,
            info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom)) return null;
        GetWindowThreadProcessId(window, out uint pid);
        if (pid == 0 || pid == Environment.ProcessId) return null;
        try {
            using var process = Process.GetProcessById((int)pid);
            string name = process.ProcessName;
            if (name.Equals("explorer", StringComparison.OrdinalIgnoreCase) || name.Equals("dwm", StringComparison.OrdinalIgnoreCase) || name.Equals("PanelDeck", StringComparison.OrdinalIgnoreCase)) return null;
            return new(pid, process.StartTime.ToUniversalTime().Ticks, name, window);
        } catch { return null; }
    }
    internal static bool CoversMonitor(int left, int top, int right, int bottom, int ml, int mt, int mr, int mb) =>
        left <= ml + 2 && top <= mt + 2 && right >= mr - 2 && bottom >= mb - 2 && right > left && bottom > top;
    public void Dispose() { trace?.Dispose(); trace = null; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
