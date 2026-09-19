using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PanelDeck.RuntimeSetup;

internal static class Launcher
{
    [STAThread]
    private static int Main(string[] args)
    {
        string directory = AppDomain.CurrentDomain.BaseDirectory;
        // A machine-readable, noninteractive check for packaging tests; never installs or launches.
        if ((args.Length == 2 || (args.Length == 4 && args[2] == "--runtime-root")) && args[0] == "--check") {
            RuntimeStatus check = RuntimePrerequisites.Inspect(directory, args.Length == 4 ? args[3] : "");
            File.WriteAllText(args[1], "CanLaunch=" + check.CanLaunch + "\nSelfContained=" + check.SelfContained + "\n" + check.Summary, Encoding.UTF8);
            return check.CanLaunch ? 0 : 2;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try {
            if (args.Length == 2 && args[0] == "--render-ui") {
                using (var preview = new SetupForm(directory, false, new string[0])) {
                    Prepare(preview); preview.PerformAutoScale(); Prepare(preview);
                    using (var bitmap = new Bitmap(preview.Width, preview.Height)) {
                        preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, preview.Size));
                        bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                return 0;
            }
            RuntimeStatus status = RuntimePrerequisites.Inspect(directory);
            bool setupOnly = args.Length == 1 && args[0] == "--setup";
            if (!setupOnly && status.CanLaunch) { StartApp(directory, status, args); return 0; }
            bool first;
            using (var mutex = new Mutex(true, "Local\\PanelDeck.RuntimeSetup", out first)) {
                if (!first) { MessageBox.Show("运行环境助手已经打开，请在原窗口继续。", "曜屏"); return 0; }
                try { Application.Run(new SetupForm(directory, setupOnly, args)); }
                finally { mutex.ReleaseMutex(); }
            }
            return 0;
        } catch (Exception ex) {
            if (args.Length == 2 && args[0] == "--render-ui") { File.WriteAllText(args[1] + ".error", ex.ToString()); return 1; }
            MessageBox.Show(ex.Message, "曜屏 · 启动检查", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }
    }
    private static void Prepare(Control control)
    {
        IntPtr handle = control.Handle;
        foreach (Control child in control.Controls) Prepare(child);
        control.PerformLayout();
    }
    internal static void StartApp(string directory, RuntimeStatus status, string[] args)
    {
        string executable = Path.Combine(directory, "PanelDeck.exe");
        if (!File.Exists(executable)) throw new InvalidOperationException("找不到 PanelDeck.exe，请保留完整的曜屏发行文件夹。");
        var info = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false };
        info.Arguments = string.Join(" ", args.Select(Quote));
        // Use exactly the x64 installation we checked, even when a stale environment override exists.
        if (!status.SelfContained) info.EnvironmentVariables["DOTNET_ROOT_X64"] = status.Root;
        using (var process = Process.Start(info)) { }
    }
    private static string Quote(string argument)
    {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in argument) {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); slashes = 0; result.Append(c);
        }
        result.Append('\\', slashes * 2); return result.Append('"').ToString();
    }
}

internal sealed class SetupForm : Form
{
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    private readonly string directory;
    private readonly bool setupOnly;
    private readonly string[] launchArgs;
    private readonly Label status = new Label { AutoSize = true };
    private readonly Label note = new Label { AutoSize = true, MaximumSize = new Size(510, 0) };
    private readonly Button install = new Button { Text = "安装缺少的运行时", AutoSize = true, Height = 38 };
    private readonly Button refresh = new Button { Text = "重新检测", AutoSize = true, Height = 38 };
    private readonly Button launch = new Button { Text = "启动曜屏", AutoSize = true, Height = 38 };
    private bool busy;
    internal SetupForm(string directory, bool setupOnly, string[] args)
    {
        this.directory = directory; this.setupOnly = setupOnly; launchArgs = args;
        Text = "曜屏 · 运行环境"; StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None; ClientSize = new Size(610, 440); MinimumSize = Size; MaximizeBox = false;
        Font = new Font("Microsoft YaHei UI", 10); BackColor = Color.FromArgb(246, 247, 250);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(28), ColumnCount = 1, RowCount = 5 };
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 18) };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var title = new Label { Text = "准备好运行曜屏", AutoSize = true, Font = new Font(Font.FontFamily, 19, FontStyle.Bold) };
        title.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xa1, new IntPtr(2), IntPtr.Zero); } };
        var close = new Button { Text = "关闭", AutoSize = true, FlatStyle = FlatStyle.Flat, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        close.FlatAppearance.BorderSize = 0; close.Click += delegate { Close(); }; heading.Controls.Add(title); heading.Controls.Add(close); grid.Controls.Add(heading);
        status.Margin = new Padding(0, 0, 0, 18); grid.Controls.Add(status);
        note.Margin = new Padding(0, 0, 0, 18); grid.Controls.Add(note);
        grid.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(510, 0), Text = "只从微软下载并校验安装包。安装时 Windows 可能请求管理员授权；不会自动重启电脑。" });
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        actions.Controls.AddRange(new Control[] { install, refresh, launch }); grid.Controls.Add(actions); Controls.Add(grid);
        launch.Visible = !setupOnly; refresh.Click += delegate { RefreshStatus(); };
        launch.Click += delegate { try { Launcher.StartApp(directory, RuntimePrerequisites.Inspect(directory), launchArgs); Close(); } catch (Exception ex) { note.Text = ex.Message; } };
        install.Click += async delegate { await Install(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) { e.Cancel = true; note.Text = "正在准备或安装。可在微软安装程序中取消；完成后即可关闭。"; } };
        RefreshStatus();
    }
    private void RefreshStatus()
    {
        RuntimeStatus current = RuntimePrerequisites.Inspect(directory);
        status.Text = current.Summary;
        note.Text = current.SelfContained ? "当前是自包含版，已附带运行环境，无需额外安装。" :
            current.SharedReady ? "所需运行时已齐全，可以正常启动。" : "精简版需要以上 x64 .NET 10 组件，缺少时主程序无法启动。";
        install.Enabled = !busy && !current.CanLaunch; refresh.Enabled = !busy; launch.Enabled = !busy && current.CanLaunch;
    }
    private async Task Install()
    {
        RuntimeStatus current = RuntimePrerequisites.Inspect(directory);
        if (current.CanLaunch) { RefreshStatus(); return; }
        // Microsoft installers target the registered global installation, not DOTNET_ROOT.
        // Never combine frameworks from separate custom/global directories into a false ready state.
        RuntimeStatus installTarget = RuntimePrerequisites.Inspect(directory, RuntimePrerequisites.SystemRoot());
        string script = Path.Combine(directory, "Install-Runtime.ps1");
        if (!File.Exists(script)) { note.Text = "缺少安装脚本，请重新解压完整发行包。"; return; }
        busy = true; RefreshStatus(); note.Text = "正在从微软获取安装包并校验，请稍候…";
        try {
            string resultFile = Path.Combine(Path.GetTempPath(), "PanelDeck-runtime-" + Guid.NewGuid().ToString("N") + ".txt");
            try {
                var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe")) {
                    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory,
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\" -ResultPath \"" + resultFile + "\"" +
                        (installTarget.NeedsDesktop ? " -Desktop" : "") + (installTarget.NeedsAspNet ? " -AspNet" : "")
                };
                int code = await Task.Run(delegate { using (var process = Process.Start(info)) { process.WaitForExit(); return process.ExitCode; } });
                string detail = File.Exists(resultFile) ? File.ReadAllText(resultFile, Encoding.UTF8) : "安装未完成，请检查网络后重试。";
                busy = false; RefreshStatus(); note.Text = detail;
                if (code == 0 && !RuntimePrerequisites.Inspect(directory).CanLaunch) note.Text += "\n组件尚未全部就绪，请重新检测或重试缺少项。";
            } finally { try { File.Delete(resultFile); } catch (IOException) { } }
        } catch (Exception ex) { busy = false; RefreshStatus(); note.Text = "安装未完成：" + ex.Message; }
    }
}
