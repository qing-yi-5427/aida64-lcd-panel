using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PanelDeck;

// Explicit UAC only. The network-facing GUI remains unprivileged.
public sealed class SessionCollector : IDisposable
{
    private readonly object sync = new();
    private bool disposed;
    private bool fullStarting;
    private int generation;
    private volatile Process? process;
    private NamedPipeServerStream? lifetime;
    private CancellationTokenSource? lifetimeStop;
    private volatile bool elevated;
    public string? Pipe { get; private set; }
    public bool IsRunning { get { try { return process != null && !process.HasExited; } catch { return false; } } }
    public bool IsAuthorized => elevated && IsRunning;
    public void StartBasic() { if (!IsRunning) Start(false); }
    public void StopBasic() { lock (sync) if (!elevated) CloseWorker(); }
    public void Authorize()
    {
        if (IsAuthorized) return;
        var driver = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PawnIO", "PawnIOLib.dll");
        if (!File.Exists(driver)) throw new InvalidOperationException("尚未安装 PawnIO 驱动。请先通过发行包的完整采集配置安装驱动，再授权本次读取。");
        Start(true);
    }
    private void Start(bool full)
    {
        int ticket;
        lock (sync) {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (full ? IsAuthorized || fullStarting : IsRunning || fullStarting) return;
            ticket = ++generation; if (full) fullStarting = true;
        }
        try {
        using var parent = Process.GetCurrentProcess();
        using var identity = WindowsIdentity.GetCurrent();
        string nonce = Guid.NewGuid().ToString("N");
        string pipeName = PipeFor(nonce);
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        // A standard user may supply a different administrator's credentials in UAC.
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.Read, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        var nextLifetime = NamedPipeServerStreamAcl.Create(pipeName + ".Lifetime", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
        var nextStop = new CancellationTokenSource();
        // No commands are sent. Closing this parent-owned pipe gracefully ends the worker.
        _ = AcceptLifetime(nextLifetime, nextStop.Token);
        var info = new ProcessStartInfo {
            FileName = Environment.ProcessPath!, UseShellExecute = full,
            Verb = full ? "runas" : "", WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = !full
        };
        foreach (var arg in new[] { full ? "--session-collector" : "--basic-collector", parent.Id.ToString(), parent.StartTime.ToUniversalTime().Ticks.ToString(), nonce, identity.User!.Value }) info.ArgumentList.Add(arg);
        Process? next = null;
        try {
            next = Process.Start(info) ?? throw new InvalidOperationException("无法启动本次采集授权。");
            lock (sync) {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (ticket != generation) { nextStop.Cancel(); nextLifetime.Dispose(); nextStop.Dispose(); next.Dispose(); return; }
                CloseWorker();
                lifetime = nextLifetime; lifetimeStop = nextStop;
                process = next; elevated = full; Pipe = pipeName;
            }
        } catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) {
            nextStop.Cancel(); nextLifetime.Dispose(); nextStop.Dispose();
            throw new InvalidOperationException("已取消授权，继续使用基础采集。");
        } catch {
            nextStop.Cancel(); nextLifetime.Dispose(); nextStop.Dispose(); next?.Dispose(); throw;
        }
        } finally {
            if (full) lock (sync) { if (ticket == generation) fullStarting = false; }
        }
    }
    private static async Task AcceptLifetime(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try { await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException) { }
    }
    public static string PipeFor(string nonce)
    {
        if (!Guid.TryParseExact(nonce, "N", out _)) throw new ArgumentException("Invalid session identifier");
        return "PanelDeck.Hardware.Session." + nonce;
    }
    public static bool MatchesParent(Process parent, long startTicks, string expectedPath)
    {
        try {
            return !parent.HasExited && parent.StartTime.ToUniversalTime().Ticks == startTicks
                && string.Equals(parent.MainModule?.FileName, expectedPath, StringComparison.OrdinalIgnoreCase);
        } catch { return false; }
    }
    // Also used by isolated tests with synthetic collectors; no privilege or hardware needed there.
    public static async Task RunUntilParentExits(Process parent, Func<CancellationToken, Task> serve, Task? lifetimeClosed = null)
    {
        using var stop = new CancellationTokenSource();
        var serving = Task.Run(() => serve(stop.Token));
        var parentExit = parent.WaitForExitAsync(stop.Token);
        await Task.WhenAny(serving, parentExit, lifetimeClosed ?? Task.Delay(Timeout.Infinite, stop.Token));
        stop.Cancel();
        try { await serving.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) { }
        // Timeout propagates to Main and terminates this helper; never persist after the parent.
    }
    public static async Task Run(string[] args)
    {
        if (args.Length != 5 || !int.TryParse(args[1], out var pid) || !long.TryParse(args[2], out var ticks)) return;
        bool full = args[0] == "--session-collector";
        if (!full && args[0] != "--basic-collector") return;
        using var identity = WindowsIdentity.GetCurrent();
        if (full && !new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return;
        using var parent = Process.GetProcessById(pid);
        if (!MatchesParent(parent, ticks, Environment.ProcessPath!)) return;
        var sid = new SecurityIdentifier(args[4]);
        string name = PipeFor(args[3]);
        using var lifetime = new NamedPipeClientStream(".", name + ".Lifetime", PipeDirection.In, PipeOptions.Asynchronous);
        using var connectionTimeout = new CancellationTokenSource(5000);
        await lifetime.ConnectAsync(connectionTimeout.Token);
        await RunUntilParentExits(parent, ct => CollectorService.Serve(name, ct, sid, allowPrivileged: full), lifetime.ReadAsync(new byte[1]).AsTask());
    }
    private void CloseWorker() {
        lifetimeStop?.Cancel(); lifetime?.Dispose(); lifetimeStop?.Dispose(); process?.Dispose();
        lifetime = null; lifetimeStop = null; process = null; Pipe = null; elevated = false;
    }
    public void Dispose() { lock (sync) { if (disposed) return; disposed = true; CloseWorker(); } }
}
