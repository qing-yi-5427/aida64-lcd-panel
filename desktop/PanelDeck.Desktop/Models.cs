using System.Security.Cryptography;
using System.Text.Json;

namespace PanelDeck;

public sealed record Reading(string Id, string Hardware, string Kind, string Name, string Type, float? Value);
public sealed record Metric(string Label, string Unit, float? Value, string? Source);
public sealed record ScreenDecision(string Mode, string Reason);
public sealed record PcState(bool Suspended, bool ManualOff);
public sealed record Snapshot(long Timestamp, string Computer, ScreenDecision Screen,
    string Cpu, string Gpu, Dictionary<string, Metric> Metrics, string? Warning,
    PcState? Pc = null, long SampleAgeMs = 0, int ProtocolVersion = 2);

public sealed class Settings
{
    private static readonly object SaveLock = new();
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8080;
    public bool StartMinimized { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool UseCollectorService { get; set; } = true;
    public int SampleSeconds { get; set; } = 2;
    public int SleepSampleSeconds { get; set; } = 15;
    public string CpuFanId { get; set; } = "";
    public string Token { get; set; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    [System.Text.Json.Serialization.JsonIgnore] public string? LoadNotice { get; set; }
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PanelDeck");
    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public static Settings Load()
    {
        try { var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); s.Validate(); return s; }
        catch (FileNotFoundException) { return new(); }
        catch (DirectoryNotFoundException) { return new(); }
        catch (Exception ex) {
            DiagnosticLog.Write("settings-load", ex);
            try { File.Copy(FilePath, FilePath + ".recovery", true); } catch { }
            return new() { LoadNotice = "原设置无法读取，已恢复默认值；请检查连接设置并重新配对。" };
        }
    }
    public void Validate()
    {
        CpuFanId ??= "";
        Port = Math.Clamp(Port, 1024, 65535);
        SampleSeconds = Math.Clamp(SampleSeconds, 1, 10);
        SleepSampleSeconds = Math.Clamp(SleepSampleSeconds, 5, 60);
        if (!System.Net.IPAddress.TryParse(ListenAddress, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) ListenAddress = "0.0.0.0";
        if (Token == null || Token.Length != 64 || !Token.All(Uri.IsHexDigit)) Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
    public Settings Copy() => (Settings)MemberwiseClone();
    public void Save()
    {
        lock (SaveLock) {
        Validate(); Directory.CreateDirectory(DirectoryPath);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
        }
    }
}

public static class ScreenPolicy
{
    public static ScreenDecision Decide(bool suspended, bool manualOff)
    {
        if (suspended) return new("off", "电脑正在休眠");
        if (manualOff) return new("off", "已手动息屏，点击自动恢复");
        return new("on", "电脑已开机，保持亮屏");
    }
}
