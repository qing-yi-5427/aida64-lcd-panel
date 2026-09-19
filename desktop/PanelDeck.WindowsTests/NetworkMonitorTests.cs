using PanelDeck;
using System.Diagnostics;
using System.Net.NetworkInformation;

internal static class NetworkMonitorTests
{
    internal static void Run(Action<bool, string> check)
    {
        long time = 1000;
        IReadOnlyList<NetworkCounter> rows = [new(1, 10, "fixture", 1000, 100)];
        bool fail = false;
        var monitor = new NetworkMonitor(() => fail ? throw new System.ComponentModel.Win32Exception(31) : rows, () => time, 1000);
        check(monitor.Sample().Download == null, "Network first sample is unknown, not zero");
        rows = [new(1, 10, "fixture", 5000, 1100)]; time += 2000;
        var rate = monitor.Sample();
        check(rate.Download == 2000 && rate.Upload == 500 && rate.Source!.Contains("fixture"), "Network uses elapsed monotonic time and byte deltas");
        time += 2000;
        check(monitor.Sample().Download == 0, "A valid idle adapter reports zero throughput");
        rows = [new(1, 10, "fixture", 100, 10)]; time += 2000;
        check(monitor.Sample().Download == null, "Counter rollback is rebaselined without a spike");
        rows = [new(1, 11, "fixture", 4000, 1000)]; time += 2000;
        check(monitor.Sample().Download == null, "Adapter index change is rebaselined");
        rows = [new(1, 11, "fixture", 6000, 2000), new(2, 20, "wifi", 9000, 3000)]; time += 2000;
        check(monitor.Sample().Download == null, "Adapter addition does not import lifetime bytes");
        rows = [new(1, 11, "fixture", 8000, 4000), new(2, 20, "wifi", 13000, 5000)]; time += 2000;
        rate = monitor.Sample();
        check(rate.Download == 3000 && rate.Upload == 2000, "Two physical adapters sum independent rates");
        rows = [new(2, 20, "wifi", 15000, 6000)]; time += 2000;
        check(monitor.Sample().Download == null, "Adapter removal does not mix totals");
        time += 120000;
        check(monitor.Sample().Download == null, "Abnormally long sample interval is rebaselined");
        time += 100;
        check(monitor.Sample().Download == null, "Too-short sample interval cannot amplify tiny deltas");
        time -= 2000;
        check(monitor.Sample().Download == null, "Invalid monotonic time is rejected");
        time += 2000; monitor.Reset();
        check(monitor.Sample().Download == null, "Sleep/resume explicitly discards the baseline");
        rows = []; time += 2000;
        check(monitor.Sample().Download == null && monitor.Sample().Source == null, "No eligible interface remains unavailable");
        rows = [new(1, 10, "fixture", ulong.MaxValue - 5000, ulong.MaxValue - 5000)]; time += 2000; monitor.Sample();
        rows = [new(1, 10, "fixture", ulong.MaxValue - 1000, ulong.MaxValue - 2000)]; time += 2000;
        check(monitor.Sample().Download == 2000, "Unsigned 64-bit counters retain precision before converting rates");
        fail = true; time += 2000;
        check(monitor.Sample().Download == null, "Native counter failure leaves hardware metrics available");
        fail = false; time += 2000;
        check(monitor.Sample().Download == null, "Recovery after a native error establishes a new baseline");
        check(WindowsNetworkCounters.Eligible(6, 5, 1) && WindowsNetworkCounters.Eligible(71, 1, 1), "Hardware Ethernet and Wi-Fi are eligible");
        check(!WindowsNetworkCounters.Eligible(6, 0, 1) && !WindowsNetworkCounters.Eligible(71, 0, 1) &&
            !WindowsNetworkCounters.Eligible(24, 1, 1) && !WindowsNetworkCounters.Eligible(131, 1, 1), "Virtual Ethernet, VPN and loopback do not double-count physical traffic");
        check(!WindowsNetworkCounters.Eligible(6, 7, 1) && !WindowsNetworkCounters.Eligible(6, 0x81, 1) &&
            !WindowsNetworkCounters.Eligible(6, 0x11, 1) && !WindowsNetworkCounters.Eligible(6, 5, 2), "Filter, endpoint, disconnected and down interfaces are excluded");

        // Read-only native smoke/performance probe. No traffic generation, process,
        // UAC, adapter configuration, address or credential output is involved.
        var actual = WindowsNetworkCounters.Read();
        var managed = NetworkInterface.GetAllNetworkInterfaces();
        check(actual.All(r => managed.Any(n => n.Name == r.Name &&
            n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)), "Native structure layout agrees with .NET adapter names/types");
        foreach (var row in actual)
        {
            var stats = managed.First(n => n.Name == row.Name).GetIPStatistics();
            check(row.Received <= (ulong)stats.BytesReceived && row.Sent <= (ulong)stats.BytesSent,
                "Native 64-bit counters agree with the .NET API");
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) WindowsNetworkCounters.Read();
        watch.Stop();
        Console.WriteLine($"NETWORK PROBE: eligible={actual.Count}, 100 native reads={watch.Elapsed.TotalMilliseconds:F2} ms, allocations={(GC.GetAllocatedBytesForCurrentThread() - allocated) / 100} bytes/read");
    }
}
