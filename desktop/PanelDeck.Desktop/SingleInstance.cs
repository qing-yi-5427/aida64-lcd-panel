using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PanelDeck;

internal static class SingleInstance
{
    private const string Name = "PanelDeck.Desktop.Commands";
    public static bool Send(string command)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", Name, PipeDirection.InOut);
            pipe.Connect(2500);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            writer.WriteLine(command);
            return true;
        }
        catch { return false; }
    }
    public static async Task Listen(Control dispatcher, Action<string> commandHandler, CancellationToken ct)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = NamedPipeServerStreamAcl.Create(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                using var reader = new StreamReader(pipe);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(2000);
                var command = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                if (!ct.IsCancellationRequested && command is "show" or "off" or "auto" or "quit") dispatcher.BeginInvoke(() => commandHandler(command));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { if (ct.IsCancellationRequested) break; await Task.Delay(250).ConfigureAwait(false); }
        }
    }
}
