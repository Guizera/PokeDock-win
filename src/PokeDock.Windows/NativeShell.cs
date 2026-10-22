using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PokeDock.Windows;

internal static class NativeShell
{
    private const byte VkLeftWindows = 0x5B;
    private const byte VkA = 0x41;
    private const byte VkN = 0x4E;
    private const byte VkS = 0x53;
    private const byte VkTab = 0x09;
    private const uint KeyEventKeyUp = 0x0002;

    public static void OpenSearch() => SendWindowsShortcut(VkS);

    public static async Task SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        var encoded = Uri.EscapeDataString(query.Trim());
        try
        {
            if (await global::Windows.System.Launcher.LaunchUriAsync(new Uri($"search:query={encoded}")))
            {
                return;
            }
        }
        catch
        {
            // Fall back to the Windows Search index protocol below.
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo($"search-ms:query={encoded}") { UseShellExecute = true });
        }
        catch
        {
            OpenSearch();
        }
    }
    public static void OpenQuickSettings() => SendWindowsShortcut(VkA);
    public static void OpenNotificationsAndCalendar() => SendWindowsShortcut(VkN);
    public static void OpenTaskView() => SendWindowsShortcut(VkTab);

    public static void OpenSettings(string uri)
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
            // Windows Settings may be disabled by policy.
        }
    }

    private static void SendWindowsShortcut(byte key)
    {
        KeybdEvent(VkLeftWindows, 0, 0, UIntPtr.Zero);
        KeybdEvent(key, 0, 0, UIntPtr.Zero);
        KeybdEvent(key, 0, KeyEventKeyUp, UIntPtr.Zero);
        KeybdEvent(VkLeftWindows, 0, KeyEventKeyUp, UIntPtr.Zero);
    }

    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void KeybdEvent(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}
