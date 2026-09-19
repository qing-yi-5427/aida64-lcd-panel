using System.Buffers;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace PanelDeck;

public sealed record HardwareFrame(long Timestamp, string Cpu, string Gpu, Dictionary<string, Metric> Metrics, Reading[] Readings, string? Warning);

// No HTTP, ADB, arbitrary commands or client-supplied paths in the privileged process.
public sealed class CollectorService : BackgroundService
{
    public const string PipeName = "PanelDeck.Hardware.ReadOnly.v1";
    private static readonly byte[] NewLine = [10];
    protected override Task ExecuteAsync(CancellationToken ct) => Serve(PipeName, ct);
    public static async Task Serve(string pipeName, CancellationToken ct, SecurityIdentifier? clientSid = null, Func<HardwareFrame>? sample = null, bool allowPrivileged = true)
    {
        HardwareFrame current = new(0, "CPU", "GPU", new(), [], "正在启动采集");
        using var hardware = sample == null ? new HardwareMonitor(allowPrivileged) : null;
        using var identity = WindowsIdentity.GetCurrent();
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(clientSid ?? new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.Read, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        try {
            while (!ct.IsCancellationRequested) {
                using var pipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 262144, security);
                await pipe.WaitForConnectionAsync(ct);
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - current.Timestamp >= 1000) {
                    try {
                        if (sample != null) current = sample();
                        else {
                            var metrics = hardware!.Sample("");
                            current = new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), hardware.Cpu, hardware.Gpu, metrics, hardware.Readings.ToArray(), hardware.Warning);
                        }
                    } catch (Exception ex) { current = current with { Warning = "采集失败：" + ex.GetType().Name }; }
                }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(1500);
                try {
                    var payload = JsonSerializer.SerializeToUtf8Bytes(current);
                    await pipe.WriteAsync(payload, timeout.Token); await pipe.WriteAsync(NewLine, timeout.Token); await pipe.FlushAsync(timeout.Token);
                } catch (IOException) { } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            }
        } catch (OperationCanceledException) { }
    }
    public static async Task<HardwareFrame?> Read(CancellationToken ct, string pipeName = PipeName) {
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(1500);
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token);
            var buffer = ArrayPool<byte>.Shared.Rent(16384);
            try {
                int count = 0;
                while (true) {
                    if (count >= 262144) return null;
                    if (count == buffer.Length) {
                        var larger = ArrayPool<byte>.Shared.Rent(Math.Min(262144, buffer.Length * 2));
                        buffer.AsSpan(0, count).CopyTo(larger); ArrayPool<byte>.Shared.Return(buffer); buffer = larger;
                    }
                    int n = await pipe.ReadAsync(buffer.AsMemory(count, Math.Min(buffer.Length - count, 262144 - count)), timeout.Token);
                    if (n == 0) break;
                    count += n; if (buffer[count - 1] == 10) break;
                }
                var result = JsonSerializer.Deserialize<HardwareFrame>(buffer.AsSpan(0, count));
                return result?.Metrics != null && result.Readings != null ? result : null;
            } finally {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        } catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or UnauthorizedAccessException) { return null; }
    }
}
