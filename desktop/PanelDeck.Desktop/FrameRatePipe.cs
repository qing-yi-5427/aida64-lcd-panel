using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace PanelDeck;

// Runs inside the existing explicitly authorized collector, not a new process/service.
// The only command is one byte: pause (0) or request a 15-second sampling lease (1).
internal static class FrameRatePipe
{
    internal static async Task Serve(string name, SecurityIdentifier client, CancellationToken ct,
        Func<bool, FrameRateSample>? sample = null, int leaseMs = 15000)
    {
        using var monitor = sample == null ? new FrameRateMonitor() : null;
        sample ??= monitor!.Sample;
        using var identity = WindowsIdentity.GetCurrent();
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(client, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        long leaseUntil = 0;
        FrameRateSample current = FrameRateMonitor.Missing("帧率采集已暂停");
        try {
            while (!ct.IsCancellationRequested) {
                using var pipe = NamedPipeServerStreamAcl.Create(name + ".Frames", PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous, 16, 4096, security);
                // The foreground check shares the lease watchdog; no per-frame process enumeration.
                var connection = pipe.WaitForConnectionAsync(ct);
                while (!connection.IsCompleted) {
                    if (Environment.TickCount64 >= leaseUntil) { current = sample(false); await connection; break; }
                    await Task.WhenAny(connection, Task.Delay(1000, ct));
                    ct.ThrowIfCancellationRequested();
                    current = sample(Environment.TickCount64 < leaseUntil);
                }
                await connection;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(1500);
                try {
                    var request = new byte[1];
                    await pipe.ReadExactlyAsync(request, timeout.Token);
                    if (request[0] > 1) continue;
                    leaseUntil = request[0] == 1 ? Environment.TickCount64 + leaseMs : 0;
                    current = sample(request[0] == 1);
                    await pipe.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(current), timeout.Token);
                    await pipe.FlushAsync(timeout.Token);
                } catch (IOException) { } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            }
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { sample(false); }
    }
    internal static async Task<FrameRateSample> Read(string pipeName, bool enabled, CancellationToken ct)
    {
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(1500);
            using var pipe = new NamedPipeClientStream(".", pipeName + ".Frames", PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token);
            await pipe.WriteAsync(new byte[] { enabled ? (byte)1 : (byte)0 }, timeout.Token);
            byte[] buffer = new byte[4096]; int count = 0;
            while (count < buffer.Length) {
                int n = await pipe.ReadAsync(buffer.AsMemory(count), timeout.Token);
                if (n == 0) break; count += n;
            }
            return JsonSerializer.Deserialize<FrameRateSample>(buffer.AsSpan(0, count)) ?? FrameRateMonitor.Missing("等待帧率数据");
        } catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or UnauthorizedAccessException) {
            return FrameRateMonitor.Missing("帧率采集暂不可用");
        }
    }
}
