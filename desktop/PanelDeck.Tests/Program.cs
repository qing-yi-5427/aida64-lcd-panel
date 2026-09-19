using PanelDeck;

var settings = new Settings();
int passed = 0;
void Check(string name, string expected, ScreenDecision actual)
{
    if (actual.Mode != expected) throw new Exception($"{name}: expected {expected}, got {actual.Mode}");
    Console.WriteLine("PASS " + name); passed++;
}
Check("Awake PC keeps screen on", "on", ScreenPolicy.Decide(false, false));
Check("Sleeping PC turns screen off", "off", ScreenPolicy.Decide(true, false));
Check("Manual screen off", "off", ScreenPolicy.Decide(false, true));
Check("Sleep wins over all overrides", "off", ScreenPolicy.Decide(true, true));
settings.SampleSeconds = 900; settings.Token = "invalid"; settings.Validate();
if (settings.SampleSeconds != 10 || settings.Token.Length != 64) throw new Exception("Invalid persisted settings were accepted");
Console.WriteLine($"PASS Settings validation: {passed + 1} checks");

// Integration checks use an ephemeral loopback port and synthetic readings only.
void Assert(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
var legacy = System.Text.Json.JsonSerializer.Deserialize<Settings>("""
{"Brightness":65,"IdleMinutes":10,"DimSeconds":30,"LockSeconds":60,"Serial":"old-phone","AdbPath":"old-adb","Port":8081,"Token":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}
""")!;
legacy.Validate();
var migrated = System.Text.Json.JsonSerializer.Serialize(legacy);
Assert(legacy.Port == 8081 && legacy.Token == new string('A', 64), "Legacy config preserves paired endpoint and token");
Assert(!migrated.Contains("Brightness") && !migrated.Contains("IdleMinutes") && !migrated.Contains("AdbPath") && !migrated.Contains("Serial"), "Legacy brightness, activity and USB settings are no longer saved");
Assert(LanServer.IsLocalNetwork(System.Net.IPAddress.Parse("192.168.1.10")), "LAN address accepted");
Assert(!LanServer.IsLocalNetwork(System.Net.IPAddress.Parse("8.8.8.8")), "Public address rejected");
long fakeTime = 1000;
var timedWindow = new PairingWindow(() => fakeTime);
var expiring = timedWindow.Open(); fakeTime += 300000;
Assert(!timedWindow.Redeem(expiring) && !timedWindow.IsActive, "Pairing expires on monotonic time");
var window = new PairingWindow();
var oneTimeCode = window.Open();
Assert(!window.Redeem("00000000"), "Wrong pairing code rejected");
Assert(window.Redeem(oneTimeCode) && !window.Redeem(oneTimeCode), "Pairing code single use");
var closed = window.Open(); window.Close(); Assert(!window.Redeem(closed), "Closed pairing window rejects code");
var exhausted = window.Open(); for (int i = 0; i < 20; i++) window.Redeem("00000000");
Assert(!window.Redeem(exhausted), "Pairing attempt limit enforced");
var code = window.Open();
var token = new string('a', 64);
var frame = new Snapshot(100, "TEST", new("on", "testing"), "CPU", "GPU", new(), null, new(false, false));
int holds = 0, reports = 0;
await using var server = new LanServer("127.0.0.1", 0, () => frame, () => token, window, (_, _) => Interlocked.Increment(ref reports), () => Interlocked.Increment(ref holds));
await server.Start();
using var client = new HttpClient { BaseAddress = new Uri(server.Address), Timeout = TimeSpan.FromSeconds(5) };
foreach (var theme in new[] { "editorial", "ambient", "telemetry", "studio" }) {
    using var style = await client.GetAsync("/layout-" + theme + ".css");
    Assert(style.IsSuccessStatusCode && style.Content.Headers.ContentType?.MediaType == "text/css" &&
        (await style.Content.ReadAsStringAsync()).Contains("data-theme=\"" + theme + "\""), "Local preview serves " + theme + " layout");
}
Assert((await client.GetAsync("/api/phone/snapshot")).StatusCode == System.Net.HttpStatusCode.Unauthorized, "Snapshot requires token");
Assert((await client.PostAsync("/api/phone/hold", null)).StatusCode == System.Net.HttpStatusCode.Unauthorized, "Control requires token");
using var paired = await client.PostAsync("/api/pair", new StringContent(System.Text.Json.JsonSerializer.Serialize(new { code }), System.Text.Encoding.UTF8, "application/json"));
Assert(paired.IsSuccessStatusCode && (await paired.Content.ReadAsStringAsync()).Contains(token), "HTTP pairing exchanges code for token");
using var invalidJson = await client.PostAsync("/api/pair", new StringContent("{broken", System.Text.Encoding.UTF8, "application/json"));
Assert(invalidJson.StatusCode == System.Net.HttpStatusCode.BadRequest, "Invalid pairing JSON rejected");
using var tooBig = await client.PostAsync("/api/pair", new StringContent(new string('x', 2048), System.Text.Encoding.UTF8, "application/json"));
Assert(tooBig.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge, "Oversized pairing request rejected");
client.DefaultRequestHeaders.Authorization = new("Bearer", token);
Assert((await client.GetAsync("/api/phone/snapshot")).IsSuccessStatusCode, "Authenticated snapshot works");
int previousReports = reports;
await client.PostAsync("/api/phone/status?battery=77&charging=1", null);
Assert(reports == previousReports + 1, "Phone heartbeat is acknowledged");
using var snap = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/api/phone/snapshot"));
Assert(!snap.RootElement.TryGetProperty("brightness", out _), "PC protocol does not override phone brightness");
Assert(snap.RootElement.GetProperty("sampleAgeMs").GetInt64() > 20000, "Sample age is computed on PC, independent of phone clock");
await client.PostAsync("/api/phone/hold", null); Assert(holds == 1, "Authenticated hold request delivered once");
using var request = new HttpRequestMessage(HttpMethod.Get, "/api/phone/events");
using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
using var deadline = new CancellationTokenSource(4000);
var first = await reader.ReadLineAsync(deadline.Token);
Assert(first != null && first.StartsWith("data: ") && first.Contains("\"pc\""), "SSE carries PC power state");
frame = frame with { Screen = new("off", "testing off"), Pc = new(true, false) };
string? line;
do { line = await reader.ReadLineAsync(deadline.Token); } while (line != null && !line.Contains("testing off"));
Assert(line != null, "SSE delivers state transition without reconnect");
Assert(reports >= 2, "Authenticated requests report phone presence");
token = new string('b', 64);
Assert((await client.GetAsync("/api/phone/snapshot")).StatusCode == System.Net.HttpStatusCode.Unauthorized, "Old token rejected after rotation");
Console.WriteLine("LAN integration checks passed; no hardware or phone was accessed.");
