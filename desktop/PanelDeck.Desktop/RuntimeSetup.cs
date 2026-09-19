using System.Diagnostics;
using PanelDeck.RuntimeSetup;

namespace PanelDeck;

internal static class RuntimeSetupUi
{
    internal static string StatusText()
    {
        var status = RuntimePrerequisites.Inspect(AppContext.BaseDirectory);
        return (status.SelfContained ? "自包含版 · 已附带运行环境" : "精简版 · 复用系统运行环境") +
            Environment.NewLine + status.Summary;
    }

    internal static async Task Open()
    {
        string launcher = Path.Combine(AppContext.BaseDirectory, "PanelDeck.Launcher.exe");
        if (!File.Exists(launcher)) throw new InvalidOperationException("找不到运行环境助手，请使用完整发行包。");
        using var process = Process.Start(new ProcessStartInfo(launcher) { UseShellExecute = false, Arguments = "--setup" })
            ?? throw new InvalidOperationException("无法打开运行环境助手。");
        await process.WaitForExitAsync();
    }
}
