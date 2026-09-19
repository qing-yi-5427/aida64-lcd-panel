using Microsoft.Win32;

namespace PanelDeck;

// The tray owns the application lifetime. Create the dashboard only when requested,
// and release its controls/images when closed without stopping phone connectivity.
internal sealed class DesktopApplicationContext : ApplicationContext
{
    private readonly Controller controller;
    private readonly Control dispatcher = new();
    private readonly CancellationTokenSource stop = new();
    private readonly NotifyIcon? tray;
    private MainForm? dashboard;
    private bool disposed;
    public DesktopApplicationContext(Settings settings, bool background, bool integrateDesktop = true)
    {
        controller = new Controller(settings);
        _ = dispatcher.Handle;
        if (integrateDesktop) {
            tray = new NotifyIcon { Icon = AppIcon.Value, Text = "曜屏 · 硬件与手机屏幕", Visible = true, ContextMenuStrip = new ContextMenuStrip() };
            tray.DoubleClick += (_, _) => ShowDashboard();
            tray.ContextMenuStrip.Items.Add("打开曜屏", null, (_, _) => ShowDashboard());
            tray.ContextMenuStrip.Items.Add("自动亮灭屏", null, (_, _) => controller.Auto());
            tray.ContextMenuStrip.Items.Add("退出曜屏", null, (_, _) => ExitThread());
            SystemEvents.PowerModeChanged += PowerChanged;
            _ = SingleInstance.Listen(dispatcher, Command, stop.Token);
        }
        dispatcher.BeginInvoke(async () => {
            try { await controller.Start(); }
            catch (Exception ex) { DiagnosticLog.Write("controller-start", ex); }
            if (!background && !stop.IsCancellationRequested) ShowDashboard();
        });
    }
    private void ShowDashboard()
    {
        if (dashboard == null || dashboard.IsDisposed) {
            dashboard = new MainForm(controller);
            dashboard.FormClosed += (_, _) => { controller.DashboardVisible = false; dashboard = null; };
        }
        dashboard.ShowWindow();
    }
    private void Command(string command)
    {
        switch (command) {
            case "show": ShowDashboard(); break;
            case "auto": controller.Auto(); break;
            case "off": controller.Off(); break;
            case "quit": ExitThread(); break;
        }
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs args)
    {
        if (args.Mode == PowerModes.Suspend) controller.PowerChanged(true);
        else if (args.Mode == PowerModes.Resume) controller.PowerChanged(false);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed) {
            disposed = true;
            stop.Cancel();
            if (tray != null) { SystemEvents.PowerModeChanged -= PowerChanged; tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
            dashboard?.Dispose(); dispatcher.Dispose(); controller.Dispose(); stop.Dispose();
        }
        base.Dispose(disposing);
    }
}
