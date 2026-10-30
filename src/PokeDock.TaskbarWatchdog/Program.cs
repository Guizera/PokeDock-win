using System.Globalization;
using System.Runtime.InteropServices;

namespace PokeDock.TaskbarWatchdog;

internal static class Program
{
    private const uint Synchronize = 0x00100000;
    private const uint Infinite = 0xFFFFFFFF;
    private const uint WaitObject0 = 0;
    private const int ErrorInvalidParameter = 87;
    private const int SwShow = 5;

    private static int Main(string[] args)
    {
        if (args.Length != 1 ||
            !uint.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var processId) ||
            processId == 0)
        {
            return 2;
        }

        try
        {
            var processHandle = OpenProcess(Synchronize, false, processId);
            if (processHandle == IntPtr.Zero)
            {
                if (Marshal.GetLastWin32Error() != ErrorInvalidParameter)
                {
                    return 1;
                }
            }
            else
            {
                uint waitResult;
                try
                {
                    waitResult = WaitForSingleObject(processHandle, Infinite);
                }
                finally
                {
                    _ = CloseHandle(processHandle);
                }

                if (waitResult != WaitObject0)
                {
                    return 1;
                }
            }

            RestoreTaskbars();
            return 0;
        }
        catch
        {
            return 1;
        }
    }

    private static void RestoreTaskbars()
    {
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != IntPtr.Zero)
        {
            _ = ShowWindow(primary, SwShow);
        }

        var current = IntPtr.Zero;
        while (true)
        {
            current = FindWindowEx(IntPtr.Zero, current, "Shell_SecondaryTrayWnd", null);
            if (current == IntPtr.Zero)
            {
                break;
            }

            _ = ShowWindow(current, SwShow);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);
}
