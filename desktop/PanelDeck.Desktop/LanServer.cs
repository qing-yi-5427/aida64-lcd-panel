using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PanelDeck;

public sealed class PairingWindow
{
    private readonly object sync = new();
    private string code = "";
    private readonly Func<long> clock;
    private long expires;
    private int attempts;
    public PairingWindow(Func<long>? clock = null) { this.clock = clock ?? (() => Environment.TickCount64); }
    public int RemainingSeconds { get { lock (sync) return code.Length == 0 || attempts >= 20 ? 0 : (int)Math.Max(0, (expires - clock() + 999) / 1000); } }
    public bool IsActive => RemainingSeconds > 0;
    public string Open() { lock (sync) { code = RandomNumberGenerator.GetInt32(10000000, 100000000).ToString(); expires = clock() + 300000; attempts = 0; return code; } }
    public void Close() { lock (sync) code = ""; }
    public bool Redeem(string candidate) {
        lock (sync) {
            if (code.Length == 0 || clock() >= expires || ++attempts > 20) return false;
            if (candidate != code) return false;
            code = ""; return true;
        }
    }
}

public sealed class LanServer : IAsyncDisposable
{
    private readonly WebApplication server;
    private readonly CancellationTokenSource stopped = new();
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private int streams;
    public string Address => server.Urls.First();
    public LanServer(string address, int port, Func<Snapshot> snapshot, Func<string> token,
        PairingWindow pairing, Action<int, bool> seen, Action hold)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        // Retain a loopback endpoint for the local preview when bound to a particular NIC.
        builder.WebHost.UseUrls(address is "0.0.0.0" or "127.0.0.1" ? [$"http://{address}:{port}"] : [$"http://{address}:{port}", $"http://127.0.0.1:{port}"]);
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024);
        server = builder.Build();
        server.Use(async (context, next) => {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            var remote = context.Connection.RemoteIpAddress;
            if (remote == null || !IsLocalNetwork(remote)) { context.Response.StatusCode = 403; return; }
            if (context.Request.Path.StartsWithSegments("/api/phone")) {
                var supplied = context.Request.Headers.Authorization.ToString();
                var expected = "Bearer " + token();
                if (supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(supplied), System.Text.Encoding.UTF8.GetBytes(expected))) { context.Response.StatusCode = 401; return; }
            }
            if (context.Request.Path == "/api/preview" && !IPAddress.IsLoopback(remote)) { context.Response.StatusCode = 403; return; }
            await next(context);
        });
        server.MapPost("/api/pair", async (HttpContext c) => {
            if (!c.Request.HasJsonContentType()) return Results.BadRequest();
            PairRequest? request;
            try { request = await c.Request.ReadFromJsonAsync<PairRequest>(c.RequestAborted); }
            catch (JsonException) { return Results.BadRequest(); }
            catch (BadHttpRequestException ex) { return Results.StatusCode(ex.StatusCode); }
            if (request == null || !pairing.Redeem(request.Code)) return Results.Unauthorized();
            return Results.Json(new { token = token(), computer = Environment.MachineName, protocolVersion = 2 });
        });
        void PhoneSeen(HttpContext c) {
            int.TryParse(c.Request.Query["battery"], out var battery);
            seen(Math.Clamp(battery, 0, 100), c.Request.Query["charging"] == "1");
        }
        Snapshot Fresh() { var s = snapshot(); return s with { SampleAgeMs = s.Timestamp <= 0 ? long.MaxValue : Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - s.Timestamp) }; }
        server.MapGet("/api/phone/snapshot", (HttpContext c) => { PhoneSeen(c); return Results.Json(Fresh()); });
        server.MapPost("/api/phone/status", (HttpContext c) => { PhoneSeen(c); return Results.Ok(); });
        server.MapPost("/api/phone/hold", () => { hold(); return Results.Ok(); });
        // SSE is a normal HTTP connection. State changes arrive without USB or repeated polling.
        server.MapGet("/api/phone/events", async (HttpContext c) => {
            if (Interlocked.Increment(ref streams) > 4) { Interlocked.Decrement(ref streams); c.Response.StatusCode = 429; return; }
            PhoneSeen(c);
            c.Response.ContentType = "text/event-stream";
            c.Response.Headers["X-Accel-Buffering"] = "no";
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted, stopped.Token);
            ScreenDecision? lastScreen = null; PcState? lastPc = null; long lastSample = -1; long lastSend = 0;
            var connectedToken = token();
            try {
                while (!cancel.IsCancellationRequested && token() == connectedToken) {
                    var s = snapshot(); var now = Environment.TickCount64;
                    // Keep the stream alive even when the hardware sampling interval is longer.
                    if (s.Screen != lastScreen || s.Pc != lastPc || s.Timestamp != lastSample || now - lastSend >= 5000) {
                        var fresh = s with { SampleAgeMs = s.Timestamp <= 0 ? long.MaxValue : Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - s.Timestamp) };
                        await c.Response.WriteAsync("data: " + JsonSerializer.Serialize(fresh, json) + "\n\n", cancel.Token);
                        await c.Response.Body.FlushAsync(cancel.Token);
                        lastScreen = s.Screen; lastPc = s.Pc; lastSample = s.Timestamp; lastSend = now;
                    }
                    await Task.Delay(250, cancel.Token);
                }
            } catch (OperationCanceledException) { } catch (IOException) { }
            finally { Interlocked.Decrement(ref streams); }
        });
        server.MapGet("/api/preview", () => Results.Json(Fresh()));
        foreach (var (route, file, mime) in new[] { ("/", "index.html", "text/html"), ("/panel.js", "panel.js", "text/javascript"), ("/panel.css", "panel.css", "text/css"), ("/network.css", "network.css", "text/css") }) {
            server.MapGet(route, () => Results.File(Path.Combine(AppContext.BaseDirectory, "panel", file), mime));
        }
        foreach (var theme in new[] { "editorial", "ambient", "telemetry", "studio" }) {
            var file = "layout-" + theme + ".css";
            server.MapGet("/" + file, () => Results.File(Path.Combine(AppContext.BaseDirectory, "panel", file), "text/css"));
        }
    }
    public Task Start() => server.StartAsync(stopped.Token);
    public async ValueTask DisposeAsync() {
        stopped.Cancel();
        using var timeout = new CancellationTokenSource(3000);
        try { await server.StopAsync(timeout.Token); } catch (OperationCanceledException) { }
        await server.DisposeAsync(); stopped.Dispose();
    }
    public static bool IsLocalNetwork(IPAddress address) {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;
        var b = address.GetAddressBytes();
        return b.Length == 4 ? b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254)
            : address.IsIPv6LinkLocal || (b[0] & 0xfe) == 0xfc;
    }
    private sealed record PairRequest(string Code);
}
