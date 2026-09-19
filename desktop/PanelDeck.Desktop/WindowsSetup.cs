using System.Diagnostics;

namespace PanelDeck;

// Called only by the explicit settings button. Never used by startup or auto-reconnect.
public static class WindowsSetup
{
    public static async Task Run(int port)
    {
        var script = Path.Combine(AppContext.BaseDirectory, "Setup-Windows.ps1");
        if (!File.Exists(script)) throw new InvalidOperationException("请在完整发行包中运行配置。");
        var info = new ProcessStartInfo {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden,
        };
        // Paths are arguments to -File, never inserted into executable PowerShell code.
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-Port", port.ToString() }) info.ArgumentList.Add(arg);
        try {
            using var process = Process.Start(info) ?? throw new InvalidOperationException("无法启动配置。");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new InvalidOperationException("配置未完成。查看 ProgramData\\PanelDeck\\setup.log，修复后可重试。");
        } catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) {
            throw new InvalidOperationException("已取消管理员授权，现有设置未保存。");
        }
    }
}
