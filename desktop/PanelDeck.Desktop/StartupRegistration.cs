using Microsoft.Win32;

namespace PanelDeck;
public static class StartupRegistration
{
    public static void Set(bool enabled) {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("PanelDeck", "\"" + Path.Combine(AppContext.BaseDirectory, "PanelDeck.exe") + "\" --background");
        else key.DeleteValue("PanelDeck", false);
    }
}
