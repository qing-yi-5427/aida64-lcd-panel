using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PanelDeck;

// Minimal x64 ETW consumer: Present only, PID + event-ID filtered at the provider.
// No GPU scheduling traces, stacks, disk trace, injection or third-party capture process.
internal sealed class PresentTrace : IDisposable
{
    private static readonly Guid Dxgi = new("ca11c036-0102-4a2d-a6ad-f03cfed5d3c9");
    private static readonly Guid D3d9 = new("783aca0a-790e-4d7f-8451-aa850511c6b9");
    private static readonly Guid DxgKrnl = new("802ec45a-1e99-4b83-9920-87c98277ba9d");
    private readonly string name = "PanelDeck.Frames." + Environment.ProcessId;
    private readonly FrameRateCounter counter = new(Stopwatch.Frequency);
    private readonly FrameRateCounter kernelCounter = new(Stopwatch.Frequency);
    private readonly EventCallback callback;
    private readonly BufferCallback bufferCallback;
    private readonly uint pid;
    private readonly ulong window;
    private IntPtr properties;
    private ulong session, consumer = ulong.MaxValue;
    private Task? processing;
    private volatile bool failed;
    private uint lost;
    private long invalidUntil;
    internal PresentTrace(uint processId, IntPtr targetWindow = default)
    {
        if (IntPtr.Size != 8) throw new PlatformNotSupportedException("帧率采集需要 x64");
        pid = processId; window = unchecked((ulong)targetWindow); callback = OnEvent; bufferCallback = OnBuffer;
        try {
            int size = Marshal.SizeOf<TraceProperties>() + 2048;
            properties = Marshal.AllocHGlobal(size);
            Marshal.Copy(new byte[size], 0, properties, size);
            var p = new TraceProperties {
                Wnode = new() { BufferSize = (uint)size, Guid = Guid.NewGuid(), ClientContext = 1, Flags = 0x20000 },
                BufferSize = 16, MinimumBuffers = 4, MaximumBuffers = 16,
                // NO_PER_PROCESSOR_BUFFERING avoids one buffer for each logical CPU.
                LogFileMode = 0x100 | 0x10000000, FlushTimer = 1,
                LoggerNameOffset = (uint)Marshal.SizeOf<TraceProperties>()
            };
            Marshal.StructureToPtr(p, properties, false);
            Check(StartTrace(out session, name, properties));
            var log = new TraceLog {
                LoggerName = Marshal.StringToHGlobalUni(name),
                ProcessTraceMode = 0x100 | 0x10000000 | 0x1000,
                EventRecordCallback = Marshal.GetFunctionPointerForDelegate(callback),
                BufferCallback = Marshal.GetFunctionPointerForDelegate(bufferCallback)
            };
            try { consumer = OpenTrace(ref log); }
            finally { Marshal.FreeHGlobal(log.LoggerName); }
            if (consumer == ulong.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
            Enable(Dxgi, [42, 43, 55, 56]); Enable(D3d9, [1, 2]);
            // One kernel event supplies a best-effort Vulkan/OpenGL compatibility path.
            // Never enable the Performance keyword: it can change system GPU overhead.
            Enable(DxgKrnl, [184], 0x8000001);
            ulong processingHandle = consumer;
            processing = Task.Factory.StartNew(() => {
                uint result = ProcessTrace([processingHandle], 1, IntPtr.Zero, IntPtr.Zero);
                failed = true; // Even an unexpected successful return means data is no longer live.
                if (result != 0 && result != 1223) DiagnosticLog.Write("fps-trace", new Win32Exception((int)result));
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        } catch { Dispose(); throw; }
    }
    private void Enable(Guid provider, ushort[] events, ulong keywords = 2)
    {
        IntPtr pidFilter = Marshal.AllocHGlobal(4), eventFilter = Marshal.AllocHGlobal(4 + events.Length * 2), descriptors = Marshal.AllocHGlobal(32);
        try {
            Marshal.WriteInt32(pidFilter, unchecked((int)pid));
            Marshal.WriteInt32(eventFilter, 1 | (events.Length << 16));
            for (int i = 0; i < events.Length; i++) Marshal.WriteInt16(eventFilter, 4 + i * 2, (short)events[i]);
            Marshal.StructureToPtr(new FilterDescriptor { Pointer = (ulong)pidFilter, Size = 4, Type = 0x80000004 }, descriptors, false);
            Marshal.StructureToPtr(new FilterDescriptor { Pointer = (ulong)eventFilter, Size = (uint)(4 + events.Length * 2), Type = 0x80000200 }, descriptors + 16, false);
            var parameters = new EnableParameters { Version = 2, EnableProperty = 0x10, Filters = descriptors, FilterCount = 2 };
            // If filtering is unsupported, fail closed instead of tracing the entire system.
            Check(EnableTraceEx2(session, ref provider, 1, 5, keywords, 0, 1000, ref parameters));
        } finally { Marshal.FreeHGlobal(descriptors); Marshal.FreeHGlobal(eventFilter); Marshal.FreeHGlobal(pidFilter); }
    }
    private void OnEvent(IntPtr record)
    {
        try { Decode(record, pid, window, counter, kernelCounter); }
        catch { failed = true; } // Never unwind a managed exception through ETW's native callback.
    }
    internal static void Decode(IntPtr record, uint pid, ulong window, FrameRateCounter counter, FrameRateCounter kernelCounter)
    {
            // EVENT_RECORD / EVENT_HEADER native layout, verified for the x64-only distribution.
            if (unchecked((uint)Marshal.ReadInt32(record, 12)) != pid) return;
            ushort id = unchecked((ushort)Marshal.ReadInt16(record, 40));
            byte version = Marshal.ReadByte(record, 42);
            uint thread = unchecked((uint)Marshal.ReadInt32(record, 8));
            int length = unchecked((ushort)Marshal.ReadInt16(record, 86));
            IntPtr data = Marshal.ReadIntPtr(record, 96);
            int pointerSize = (Marshal.ReadInt16(record, 4) & 0x20) != 0 ? 4 : 8;
            if (id == 184) {
                if (version != 1 || length < 20 + pointerSize) return;
                ulong hwnd = pointerSize == 4 ? unchecked((uint)Marshal.ReadInt32(data, 4)) : unchecked((ulong)Marshal.ReadInt64(data, 4));
                uint result = unchecked((uint)Marshal.ReadInt32(data, 16 + pointerSize));
                if (hwnd != window || window == 0 || result != 0) return;
                kernelCounter.Begin(false, thread, hwnd, Marshal.ReadInt64(record, 16), 0, 184);
                kernelCounter.End(false, thread, 184, 0);
                return;
            }
            if (version != 0) return;
            bool d3d9 = id is 1 or 2;
            if (id is 42 or 55 or 1) {
                if (length < pointerSize + 4) return;
                ulong chain = pointerSize == 4 ? unchecked((uint)Marshal.ReadInt32(data)) : unchecked((ulong)Marshal.ReadInt64(data));
                counter.Begin(d3d9, thread, chain, Marshal.ReadInt64(record, 16), unchecked((uint)Marshal.ReadInt32(data, pointerSize)), (ushort)(id + 1));
            } else if (id is 43 or 56 or 2 && length >= 4) counter.End(d3d9, thread, id, unchecked((uint)Marshal.ReadInt32(data)));
    }
    private uint OnBuffer(IntPtr log)
    {
        uint currentLost = unchecked((uint)Marshal.ReadInt32(log, 416));
        if (currentLost != lost) { lost = currentLost; counter.Reset(); kernelCounter.Reset(); Interlocked.Exchange(ref invalidUntil, Stopwatch.GetTimestamp() + Stopwatch.Frequency * 3); }
        return 1;
    }
    internal float? Read() => failed || Stopwatch.GetTimestamp() < Interlocked.Read(ref invalidUntil) ? null : counter.Read(Stopwatch.GetTimestamp());
    internal (float? Value, bool Kernel) ReadFrame()
    {
        if (failed || Stopwatch.GetTimestamp() < Interlocked.Read(ref invalidUntil)) return (null, false);
        float? runtime = counter.Read(Stopwatch.GetTimestamp());
        return runtime.HasValue ? (runtime, false) : (kernelCounter.Read(Stopwatch.GetTimestamp()), true);
    }
    internal bool Failed => failed;
    private static void Check(uint error) { if (error != 0) throw new Win32Exception((int)error); }
    public void Dispose()
    {
        if (session != 0) { ControlTrace(session, name, properties, 1); session = 0; }
        if (consumer != ulong.MaxValue) { CloseTrace(consumer); consumer = ulong.MaxValue; }
        // CloseTrace cancels ProcessTrace; keep delegates alive until native delivery has ended.
        processing?.GetAwaiter().GetResult(); processing = null;
        if (properties != IntPtr.Zero) { Marshal.FreeHGlobal(properties); properties = IntPtr.Zero; }
        GC.KeepAlive(callback); GC.KeepAlive(bufferCallback);
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void EventCallback(IntPtr record);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint BufferCallback(IntPtr log);
    [StructLayout(LayoutKind.Sequential)] internal struct WnodeHeader { public uint BufferSize, ProviderId; public ulong HistoricalContext; public long TimeStamp; public Guid Guid; public uint ClientContext, Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct TraceProperties {
        public WnodeHeader Wnode;
        public uint BufferSize, MinimumBuffers, MaximumBuffers, MaximumFileSize, LogFileMode, FlushTimer, EnableFlags;
        public int AgeLimit;
        public uint NumberOfBuffers, FreeBuffers, EventsLost, BuffersWritten, LogBuffersLost, RealTimeBuffersLost;
        public IntPtr LoggerThreadId;
        public uint LogFileNameOffset, LoggerNameOffset;
    }
    [StructLayout(LayoutKind.Explicit, Size = 448)] internal struct TraceLog {
        [FieldOffset(8)] public IntPtr LoggerName;
        [FieldOffset(28)] public uint ProcessTraceMode;
        [FieldOffset(400)] public IntPtr BufferCallback;
        [FieldOffset(424)] public IntPtr EventRecordCallback;
    }
    [StructLayout(LayoutKind.Sequential)] private struct FilterDescriptor { public ulong Pointer; public uint Size, Type; }
    [StructLayout(LayoutKind.Sequential)] private struct EnableParameters { public uint Version, EnableProperty, ControlFlags; public Guid SourceId; public IntPtr Filters; public uint FilterCount; }
    [DllImport("advapi32.dll", EntryPoint = "StartTraceW", CharSet = CharSet.Unicode)] private static extern uint StartTrace(out ulong handle, string name, IntPtr properties);
    [DllImport("advapi32.dll", EntryPoint = "ControlTraceW", CharSet = CharSet.Unicode)] private static extern uint ControlTrace(ulong handle, string name, IntPtr properties, uint code);
    [DllImport("advapi32.dll")] private static extern uint EnableTraceEx2(ulong handle, ref Guid provider, uint code, byte level, ulong any, ulong all, uint timeout, ref EnableParameters parameters);
    [DllImport("advapi32.dll", EntryPoint = "OpenTraceW", SetLastError = true)] private static extern ulong OpenTrace(ref TraceLog log);
    [DllImport("advapi32.dll")] private static extern uint ProcessTrace(ulong[] handles, uint count, IntPtr start, IntPtr end);
    [DllImport("advapi32.dll")] private static extern uint CloseTrace(ulong handle);
}
