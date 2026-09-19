using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PanelDeck;

// Adapter throughput, including LAN traffic. No packet capture, WMI, driver,
// elevation, background thread or separate sampling timer is needed.
internal sealed class NetworkMonitor
{
    private readonly Func<IReadOnlyList<NetworkCounter>> read;
    private readonly Func<long> timestamp;
    private readonly long frequency;
    private readonly Dictionary<ulong, NetworkCounter> previous = new();
    private long previousTime;
    private int resetRequested;

    internal NetworkMonitor(Func<IReadOnlyList<NetworkCounter>>? read = null, Func<long>? timestamp = null, long frequency = 0)
    {
        this.read = read ?? WindowsNetworkCounters.Read;
        this.timestamp = timestamp ?? Stopwatch.GetTimestamp;
        this.frequency = frequency > 0 ? frequency : Stopwatch.Frequency;
    }

    internal void Reset() => Interlocked.Exchange(ref resetRequested, 1);

    internal NetworkRates Sample()
    {
        if (Interlocked.Exchange(ref resetRequested, 0) != 0) previous.Clear();
        IReadOnlyList<NetworkCounter> current;
        try { current = read(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or System.Net.NetworkInformation.NetworkInformationException)
        {
            previous.Clear();
            return new(null, null, null);
        }
        long now = timestamp();
        double seconds = (double)(now - previousTime) / frequency;
        bool valid = current.Count > 0 && current.Count == previous.Count && seconds >= 0.25 && seconds <= 90;
        double download = 0, upload = 0;
        var seen = new HashSet<ulong>();
        foreach (var item in current)
        {
            if (!seen.Add(item.Id) || !previous.TryGetValue(item.Id, out var old) ||
                item.Index != old.Index || item.Received < old.Received || item.Sent < old.Sent)
            { valid = false; continue; }
            download += (item.Received - old.Received) / seconds;
            upload += (item.Sent - old.Sent) / seconds;
        }
        previous.Clear();
        foreach (var item in current) previous[item.Id] = item;
        previousTime = now;
        string? source = current.Count == 0 ? null : "物理网卡（含局域网） · " + string.Join(" + ", current.Select(x => x.Name));
        return valid && double.IsFinite(download) && double.IsFinite(upload)
            ? new((float)download, (float)upload, source) : new(null, null, source);
    }
}

internal readonly record struct NetworkCounter(ulong Id, uint Index, string Name, ulong Received, ulong Sent);
internal readonly record struct NetworkRates(float? Download, float? Upload, string? Source);

internal static class WindowsNetworkCounters
{
    [InlineArray(257)] private struct InterfaceName { private ushort element; }
    [InlineArray(32)] private struct PhysicalAddress { private byte element; }

    // Windows SDK MIB_IF_ROW2, default eight-byte structure alignment. Fixed
    // inline buffers keep table walking free of per-row array allocations.
    [StructLayout(LayoutKind.Sequential)]
    private struct Row
    {
        public ulong Luid;
        public uint Index;
        public Guid Guid;
        public InterfaceName Alias, Description;
        public uint PhysicalAddressLength;
        public PhysicalAddress PhysicalAddress, PermanentPhysicalAddress;
        public uint Mtu, Type, TunnelType, MediaType, PhysicalMediumType, AccessType, DirectionType;
        public byte Flags;
        public uint OperStatus, AdminStatus, MediaConnectState;
        public Guid NetworkGuid;
        public uint ConnectionType;
        public ulong TransmitLinkSpeed, ReceiveLinkSpeed;
        public ulong InOctets, InUcastPkts, InNUcastPkts, InDiscards, InErrors, InUnknownProtos, InUcastOctets, InMulticastOctets, InBroadcastOctets;
        public ulong OutOctets, OutUcastPkts, OutNUcastPkts, OutDiscards, OutErrors, OutUcastOctets, OutMulticastOctets, OutBroadcastOctets, OutQLen;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Table { public uint Count; public Row First; }
    private static readonly int RowSize = Marshal.SizeOf<Row>();
    private static readonly int TableOffset = (int)Marshal.OffsetOf<Table>(nameof(Table.First));
    private static readonly int AliasOffset = (int)Marshal.OffsetOf<Row>(nameof(Row.Alias));
    private static readonly int TypeOffset = (int)Marshal.OffsetOf<Row>(nameof(Row.Type));
    private static readonly int FlagsOffset = (int)Marshal.OffsetOf<Row>(nameof(Row.Flags));
    private static readonly int StatusOffset = (int)Marshal.OffsetOf<Row>(nameof(Row.OperStatus));
    private static readonly int InOffset = (int)Marshal.OffsetOf<Row>(nameof(Row.InOctets));
    private static readonly int OutOffset = (int)Marshal.OffsetOf<Row>(nameof(Row.OutOctets));

    [DllImport("iphlpapi.dll", ExactSpelling = true)] private static extern uint GetIfTable2(out IntPtr table);
    [DllImport("iphlpapi.dll", ExactSpelling = true)] private static extern void FreeMibTable(IntPtr table);

    internal static bool Eligible(uint type, byte flags, uint operationalStatus) =>
        type is 6 or 71 && operationalStatus == 1 && (flags & 1) != 0 && (flags & 0xfa) == 0;

    internal static IReadOnlyList<NetworkCounter> Read()
    {
        IntPtr table = IntPtr.Zero;
        try
        {
            uint error = GetIfTable2(out table);
            if (error != 0) throw new System.ComponentModel.Win32Exception((int)error);
            int count = Marshal.ReadInt32(table);
            if (count < 0 || count > 65536) throw new System.ComponentModel.Win32Exception(13);
            var result = new List<NetworkCounter>(2);
            for (int i = 0; i < count; i++)
            {
                IntPtr address = IntPtr.Add(table, checked(TableOffset + i * RowSize));
                // PtrToStructure<Row> boxes and copies the 1,352-byte row even
                // for virtual adapters. Read only needed fields in place.
                if (!Eligible((uint)Marshal.ReadInt32(address, TypeOffset), Marshal.ReadByte(address, FlagsOffset),
                    (uint)Marshal.ReadInt32(address, StatusOffset))) continue;
                string name = Marshal.PtrToStringUni(IntPtr.Add(address, AliasOffset))!;
                result.Add(new((ulong)Marshal.ReadInt64(address), (uint)Marshal.ReadInt32(address, 8), name,
                    (ulong)Marshal.ReadInt64(address, InOffset), (ulong)Marshal.ReadInt64(address, OutOffset)));
            }
            return result;
        }
        finally { if (table != IntPtr.Zero) FreeMibTable(table); }
    }
}
