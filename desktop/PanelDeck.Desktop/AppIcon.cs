namespace PanelDeck;

public static class AppIcon
{
    public static Icon Value { get; } = Load();
    private static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("PanelDeck.AppIcon")!;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
