using PanelDeck;
// Phone transport fixture, never connects to real hardware or the running desktop program.
// The phone reaches this loopback-only port via a temporary ADB reverse during testing.
var path = Path.GetFullPath(args[0]);
string Mode() { try { return File.ReadAllText(path).Trim(); } catch { return "off"; } }
long started = Environment.TickCount64;
Snapshot Frame() {
    bool off = Mode() == "off";
    return new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "SYNTHETIC TEST", ScreenPolicy.Decide(off, false),
        "TEST CPU · 合成数据", "TEST GPU · 合成数据", new() {
            ["cpuLoad"] = new("CPU 使用率", "%", 23, "test"), ["cpuTemp"] = new("温度", "°C", 42, "test"),
            ["gpuLoad"] = new("GPU 使用率", "%", 35, "test"), ["gpuTemp"] = new("温度", "°C", 46, "test")
        }, null, new(off, false));
}
await using var server = new LanServer("127.0.0.1", 19864, Frame, () => new string('a', 64), new(), (_, _) => {
    Console.WriteLine($"Phone heartbeat after {(Environment.TickCount64 - started) / 1000}s");
}, () => { });
await server.Start(); Console.WriteLine("PHONE_FIXTURE_READY");
await Task.Delay(Timeout.Infinite);
