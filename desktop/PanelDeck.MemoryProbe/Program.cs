using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using PanelDeck;

// Isolated, opt-in measurement process: no settings reads/writes, production ports,
// tray icons, displayed windows, elevation, service install or forced collections.
internal static class Program
{
    static void Main(string[] args)
    {
        bool policy = ProcessPolicy.Initialize();
        if (args.FirstOrDefault() is "--basic-collector" or "--session-collector") { SessionCollector.Run(args).GetAwaiter().GetResult(); return; }
        File.WriteAllText(args[0] + ".startup", "Policy=" + policy + " Hook=" + Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Any(m => m.ModuleName == "RTSSHooks64.dll"));
        var thread = new Thread(() => MainOnSta(args));
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    }
    static void MainOnSta(string[] args)
    {
        var output = Path.GetFullPath(args[0]);
        try { Run(output, args[1], int.Parse(args[2])); }
        catch (Exception ex) { File.WriteAllText(output + ".error", ex.ToString()); Environment.ExitCode = 1; }
    }
    static HardwareFrame Frame() => new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "TEST CPU", "TEST GPU",
        Enumerable.Range(0, 14).ToDictionary(i => i == 0 ? "cpuLoad" : "metric" + i, i => new Metric("Test", "%", 42, "synthetic")),
        Enumerable.Range(0, 150).Select(i => new Reading("/test/" + i, "TEST", "Cpu", "Sensor " + i, "Load", 42)).ToArray(), null);
    static void Run(string output, string role, int seconds)
    {
        if (role is "main" or "tray") ApplicationConfiguration.Initialize();
        using var stop = new CancellationTokenSource();
        using var process = Process.GetCurrentProcess();
        using var hardware = role == "hardware" ? new HardwareMonitor(false) : null;
        Controller? controller = null; MainForm? form = null; HttpClient? client = null;
        Task? serving = null, consuming = null, heartbeat = null;
        long samples = 0; var latencies = new List<double>();
        HardwareFrame Sample() { var w = Stopwatch.StartNew(); var f = hardware == null ? Frame() : new HardwareFrame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), hardware.Cpu, hardware.Gpu, hardware.Sample(""), hardware.Readings.ToArray(), hardware.Warning); latencies.Add(w.Elapsed.TotalMilliseconds); Interlocked.Increment(ref samples); return f; }
        if (role is "collector" or "hardware") {
            string pipe = "PanelDeck.MemoryProbe." + Guid.NewGuid().ToString("N");
            serving = Task.Run(() => CollectorService.Serve(pipe, stop.Token, sample: Sample));
            consuming = Task.Run(async () => { while (!stop.IsCancellationRequested) { var f = await CollectorService.Read(stop.Token, pipe); if (f == null && !stop.IsCancellationRequested) throw new Exception("Pipe sample missing"); await Task.Delay(2000, stop.Token); } });
        } else {
            var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); int port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
            var settings = new Settings { ListenAddress = "127.0.0.1", Port = port, UseCollectorService = false };
            controller = new Controller(settings, role == "basic" ? null : (_, _) => Task.FromResult(Sample()));
            if (role == "main") { form = new MainForm(controller, preview: true); _ = form.Handle; }
            controller.Start().GetAwaiter().GetResult();
            client = new HttpClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", settings.Token);
            consuming = Task.Run(async () => {
                using var response = await client.GetAsync($"http://127.0.0.1:{port}/api/phone/events", HttpCompletionOption.ResponseHeadersRead, stop.Token);
                response.EnsureSuccessStatusCode(); using var stream = await response.Content.ReadAsStreamAsync(stop.Token);
                var buffer = new byte[8192]; while (await stream.ReadAsync(buffer, stop.Token) > 0) { }
            });
            heartbeat = Task.Run(async () => { while (!stop.IsCancellationRequested) { using var response = await client.PostAsync($"http://127.0.0.1:{port}/api/phone/status", null, stop.Token); response.EnsureSuccessStatusCode(); await Task.Delay(10000, stop.Token); } });
        }
        var rows = new List<object>(); var clock = Stopwatch.StartNew();
        long allocatedStart = GC.GetTotalAllocatedBytes(); double cpuStart = process.TotalProcessorTime.TotalSeconds;
        using var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        using var context = new ApplicationContext();
        timer.Tick += (_, _) => {
            process.Refresh();
            rows.Add(new { seconds = clock.Elapsed.TotalSeconds, privateBytes = process.PrivateMemorySize64, workingSet = process.WorkingSet64, managedBytes = GC.GetTotalMemory(false), gcCommitted = GC.GetGCMemoryInfo().TotalCommittedBytes, allocatedBytes = GC.GetTotalAllocatedBytes() - allocatedStart, cpuSeconds = process.TotalProcessorTime.TotalSeconds - cpuStart, handles = process.HandleCount, threads = process.Threads.Count, samples = Interlocked.Read(ref samples) });
            if (clock.Elapsed.TotalSeconds >= seconds) context.ExitThread();
        };
        timer.Start(); Application.Run(context); timer.Stop(); stop.Cancel();
        // Capture before teardown allocations.
        var finalSnapshot = controller?.Current;
        if (role == "basic" && (finalSnapshot == null || finalSnapshot.Metrics.Count != 16 || controller!.Readings.Count == 0 || DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - finalSnapshot.Timestamp > 6000)) throw new Exception("Live hardware and network data did not reach the phone endpoint");
        File.WriteAllText(output, JsonSerializer.Serialize(new { role, serverGc = GCSettings.IsServerGC, cpuCount = Environment.ProcessorCount, latencies, rows,
            metrics = finalSnapshot?.Metrics, readingCount = controller?.Readings.Count, controllerStatus = controller?.CollectorStatus,
            regions = Regions(), modules = process.Modules.Cast<ProcessModule>().Select(m => new { m.ModuleName, m.ModuleMemorySize }) }, new JsonSerializerOptions { WriteIndented = true }));
        try { Task.WhenAll(new[] { serving, consuming, heartbeat }.OfType<Task>()).Wait(3000); } catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException)) { }
        form?.Dispose(); controller?.Dispose(); client?.Dispose();
    }
    [StructLayout(LayoutKind.Sequential)]
    struct MemoryInfo { public nint BaseAddress, AllocationBase; public uint AllocationProtect; public ushort PartitionId; public nuint RegionSize; public uint State, Protect, Type; }
    [DllImport("kernel32.dll")] static extern nuint VirtualQuery(nint address, out MemoryInfo info, nuint length);
    static object[] Regions() {
        var regions = new List<object>(); nint address = 0;
        while (VirtualQuery(address, out var info, (nuint)Marshal.SizeOf<MemoryInfo>()) != 0) {
            if (info.State == 0x1000 && info.RegionSize >= 1048576) regions.Add(new { address = info.BaseAddress.ToString("x"), allocation = info.AllocationBase.ToString("x"), bytes = (long)info.RegionSize, info.Type, info.Protect });
            nint next = info.BaseAddress + (nint)info.RegionSize; if (next <= address) break; address = next;
        }
        return regions.ToArray();
    }
}
