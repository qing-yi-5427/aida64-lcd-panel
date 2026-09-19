using LibreHardwareMonitor.Hardware;
using System.Security.Principal;
using System.Runtime.CompilerServices;

namespace PanelDeck;

public sealed class HardwareMonitor : IDisposable
{
    private readonly Computer computer;
    private readonly bool elevated;
    private readonly ConditionalWeakTable<ISensor, Reading> sensorMetadata = new();
    public IReadOnlyList<Reading> Readings { get; private set; } = Array.Empty<Reading>();
    public string Cpu { get; private set; } = "CPU";
    public string Gpu { get; private set; } = "GPU";
    public string? Warning { get; private set; }
    public HardwareMonitor(bool allowPrivileged = true) {
        using var identity = WindowsIdentity.GetCurrent();
        elevated = allowPrivileged && new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        computer = new() { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true, IsMotherboardEnabled = elevated };
        computer.Open();
    }
    public Dictionary<string, Metric> Sample(string cpuFanId)
    {
        var readings = new List<Reading>(Readings.Count);
        var errors = new List<string>();
        void Visit(IHardware h)
        {
            try { h.Update(); }
            catch (Exception ex) { errors.Add(h.Name + ": " + ex.GetType().Name); return; }
            foreach (var s in h.Sensors)
            {
                // Only the current reading is displayed. LHM otherwise retains a day of history
                // for every sensor, including sensors that are not shown on the phone.
                s.ValuesTimeWindow = TimeSpan.Zero;
                float? value = s.Value;
                if (value.HasValue && !float.IsFinite(value.Value)) value = null;
                // Unprivileged low-level CPU reads can return plausible-looking zeroes.
                if (h.HardwareType == HardwareType.Cpu && s.SensorType != SensorType.Load && !elevated) value = null;
                if (h.HardwareType == HardwareType.Cpu && (s.SensorType == SensorType.Temperature || s.SensorType == SensorType.Power || s.SensorType == SensorType.Clock) && value <= 0) value = null;
                var metadata = sensorMetadata.GetValue(s, static sensor => new(sensor.Identifier.ToString(), sensor.Hardware.Name, sensor.Hardware.HardwareType.ToString(), sensor.Name, sensor.SensorType.ToString(), null));
                readings.Add(metadata with { Value = value });
            }
            foreach (var sub in h.SubHardware) Visit(sub);
        }
        foreach (var h in computer.Hardware) Visit(h);
        Readings = readings;
        Cpu = readings.FirstOrDefault(x => x.Kind == "Cpu")?.Hardware ?? "CPU";
        var gpu = readings.FirstOrDefault(x => x.Kind == "GpuNvidia") ?? readings.FirstOrDefault(x => x.Kind.StartsWith("Gpu"));
        Gpu = gpu?.Hardware ?? "GPU";
        Reading? Pick(string kind, string type, params string[] names) => names.Select(name => readings.FirstOrDefault(x => x.Kind == kind && x.Type == type && x.Name == name && x.Value.HasValue)).FirstOrDefault(x => x != null);
        Reading? G(string type, params string[] names) => names.Select(name => readings.FirstOrDefault(x => x.Hardware == Gpu && x.Type == type && x.Name == name && x.Value.HasValue)).FirstOrDefault(x => x != null);
        Metric M(string label, string unit, Reading? r, float factor = 1) => new(label, unit, r?.Value * factor, r == null ? null : r.Hardware + " · " + r.Name);
        var physicalMemory = readings.Where(x => x.Kind == "Memory" && x.Hardware == "Total Memory").ToList();
        Reading? Ram(string name) => physicalMemory.FirstOrDefault(x => x.Name == name);
        var cpuFan = cpuFanId.Length > 0
            ? readings.FirstOrDefault(x => x.Type == "Fan" && x.Id == cpuFanId)
            : readings.FirstOrDefault(x => x.Type == "Fan" && !x.Kind.StartsWith("Gpu") && x.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase));
        bool provisionalFan = false;
        // Upstream's B650E AORUS ELITE AX ICE table maps channel 0 to CPU_FAN.
        // This SMBIOS variant includes an extra X and is not recognized upstream.
        // Keep this family-based inference visible and allow the user to override it.
        if (cpuFan == null && cpuFanId.Length == 0 && computer.Hardware.Any(h => h.HardwareType == HardwareType.Motherboard && h.Name.Contains("B650E AORUS ELITE X AX ICE", StringComparison.OrdinalIgnoreCase))) {
            cpuFan = readings.FirstOrDefault(x => x.Id == "/lpc/it8689e/0/fan/0");
            provisionalFan = cpuFan != null;
        }
        var result = new Dictionary<string, Metric>
        {
            ["cpuLoad"] = M("CPU 使用率", "%", Pick("Cpu", "Load", "CPU Total")),
            ["cpuTemp"] = M("CPU 温度", "°C", Pick("Cpu", "Temperature", "Core (Tctl/Tdie)", "Core (Tdie)", "CPU Package")),
            ["cpuPower"] = M("CPU 封装功耗", "W", Pick("Cpu", "Power", "Package", "CPU Package")),
            ["cpuClock"] = M("CPU 平均频率", "MHz", Pick("Cpu", "Clock", "Cores (Average)", "CPU Core #1")),
            ["cpuFan"] = M(provisionalFan ? "CPU 风扇（暂定）" : "CPU 风扇", "RPM", cpuFan),
            ["gpuLoad"] = M("GPU 使用率", "%", G("Load", "GPU Core")),
            ["gpuTemp"] = M("GPU 核心温度", "°C", G("Temperature", "GPU Core")),
            ["gpuPower"] = M("GPU 功耗", "W", G("Power", "GPU Package", "GPU Board")),
            ["gpuClock"] = M("GPU 核心频率", "MHz", G("Clock", "GPU Core")),
            ["gpuFan"] = M("GPU 风扇 1", "RPM", G("Fan", "GPU Fan 1", "GPU")),
            ["ramLoad"] = M("物理内存使用率", "%", Ram("Memory")),
            ["ramUsed"] = M("物理内存已用", "GB", Ram("Memory Used")),
            ["vramLoad"] = M("显存使用率", "%", G("Load", "GPU Memory")),
            ["vramUsed"] = M("显存已用", "GB", G("SmallData", "GPU Memory Used"), 1f / 1024),
        };
        Warning = errors.Count > 0 ? string.Join("；", errors) : !elevated ? "基础采集：点击“授权完整读取（UAC）”读取 CPU 温度、功耗与主板风扇。本次运行只需授权一次。" :
            result["cpuTemp"].Value == null ? "CPU 温度不可用，请检查 PawnIO 驱动。" : provisionalFan ? "CPU_FAN 暂按同系列映射使用 Fan #1，建议与 BIOS 读数核对；可手动选择接口。" : cpuFan == null ? "请选择实际的 CPU 风扇接口。" : null;
        return result;
    }
    public void Dispose() => computer.Close();
}
