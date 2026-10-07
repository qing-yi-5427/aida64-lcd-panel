using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PanelDeck;

// The MIT-licensed PresentMon analysis core runs inside the existing authorized collector.
internal sealed class DisplayFrameTrace : IDisposable
{
    private readonly TraceHandle handle;
    private readonly DisplayFrameCounter counter = new(Stopwatch.Frequency);
    private readonly NativeFrame[] frames = new NativeFrame[65536];
    private uint eventsLost, buffersLost, overflows;
    private long invalidUntil;
    internal bool Failed { get; private set; }
    internal NativeInfo Diagnostics { get; private set; }
    internal DisplayFrameStats Statistics { get; private set; }
    internal DisplayFrameTrace(uint pid, IntPtr window)
    {
        if (PdAbiVersion() != 1) throw new InvalidOperationException("显示帧率采集组件版本不匹配");
        uint error = PdStart(pid, unchecked((ulong)window), out var raw);
        if (error != 0) throw new Win32Exception((int)error);
        handle = new(raw);
    }
    internal float? Read()
    {
        Statistics = default;
        uint error = PdRead(handle, frames, (uint)frames.Length, out var info);
        Diagnostics = info;
        long now = Stopwatch.GetTimestamp();
        if (error != 0 || info.Failed != 0 || info.Count > frames.Length) {
            Failed = true; counter.Reset(); return null;
        }
        bool lost = info.EventsLost != eventsLost || info.BuffersLost != buffersLost || info.Overflows != overflows || info.LostPresents != 0;
        eventsLost = info.EventsLost; buffersLost = info.BuffersLost; overflows = info.Overflows;
        if (lost) { counter.Reset(); invalidUntil = now + Stopwatch.Frequency * 3; }
        // Keep draining while recovering, so the native completion ring cannot accumulate.
        if (lost || now < invalidUntil) return null;
        for (int i = 0; i < info.Count; i++) counter.Add(frames[i].Chain, (long)frames[i].Time);
        Statistics = counter.ReadStats(now);
        return Statistics.Fps;
    }
    public void Dispose() => handle.Dispose();
    [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.AssemblyDirectory)]
    [DllImport("PanelDeck.PresentMon.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint PdAbiVersion();
    [StructLayout(LayoutKind.Sequential)] internal struct NativeFrame { public ulong Chain, Time; }
    [StructLayout(LayoutKind.Sequential)] internal struct NativeInfo {
        public uint Count, EventsLost, BuffersLost, Overflows, LostPresents, Discarded, Failed, Reserved;
    }
    private sealed class TraceHandle : SafeHandleZeroOrMinusOneIsInvalid {
        internal TraceHandle(IntPtr value) : base(true) => SetHandle(value);
        protected override bool ReleaseHandle() { PdStop(handle); return true; }
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.AssemblyDirectory)]
    [DllImport("PanelDeck.PresentMon.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint PdStart(uint pid, ulong window, out IntPtr capture);
    [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.AssemblyDirectory)]
    [DllImport("PanelDeck.PresentMon.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint PdRead(TraceHandle capture, [Out] NativeFrame[] frames, uint capacity, out NativeInfo info);
    [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.AssemblyDirectory)]
    [DllImport("PanelDeck.PresentMon.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void PdStop(IntPtr capture);
}
