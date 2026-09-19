namespace PanelDeck;

public static class DiagnosticLog
{
    private static readonly object Gate = new();
    public static void Write(string context, Exception error) {
        lock (Gate) try {
            Directory.CreateDirectory(Settings.DirectoryPath);
            var file = Path.Combine(Settings.DirectoryPath, "diagnostics.log");
            if (File.Exists(file) && new FileInfo(file).Length > 128 * 1024) File.Move(file, file + ".previous", true);
            // Do not include response bodies, settings, URLs or pairing tokens in diagnostics.
            File.AppendAllText(file, $"{DateTimeOffset.Now:O} {context}: {error.GetType().Name}\n{error.StackTrace}\n");
        } catch { }
    }
}
