using System.Runtime.InteropServices;

namespace PanelDeck;

public static class ProcessPolicy
{
    // This GDI dashboard/collector needs no legacy global window-hook plugins.
    // Apply before initializing WinForms to avoid loading game overlays into each
    // process. This changes only the calling process, never games or RTSS settings.
    // Modern TSF input methods continue to work; opt out for legacy IME/hook tools.
    public static bool Initialize()
    {
        if (Environment.GetEnvironmentVariable("PANELDECK_ALLOW_LEGACY_HOOKS") == "1") return false;
        uint flags = 1;
        return SetProcessMitigationPolicy(6, ref flags, sizeof(uint));
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessMitigationPolicy(int policy, ref uint flags, nuint length);
}
