using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace PokeDock.Windows;

/// <summary>
/// Hides the Explorer taskbars while PokeDock is active without changing the
/// Windows work area or replacing Explorer. The original visibility is restored
/// when PokeDock exits.
/// </summary>
internal sealed class TaskbarReplacementController
{
    private const int SwHide = 0;
    private const int SwShow = 5;

    private readonly object _gate = new();
    private DispatcherTimer? _watchdog;
    private bool _enabled;
    private bool _nativeTaskbarWasVisible;
    private int _hideAttempts;

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PokeDock",
        "Logs");

    internal static string LogPath => Path.Combine(LogDirectory, "taskbar.log");

    public void Enable()
    {
        lock (_gate)
        {
            if (_enabled)
            {
                WriteLog("Enable ignored because replacement is already active.");
                return;
            }

            StartLogSession();
            var primary = FindWindow("Shell_TrayWnd", null);
            _nativeTaskbarWasVisible = primary != IntPtr.Zero && IsWindowVisible(primary);
            _enabled = true;
            StartRecoveryWatchdog();
            WriteLog(
                $"Enable: pid={Environment.ProcessId}, architecture={RuntimeInformation.ProcessArchitecture}, " +
                $"handle={FormatHandle(primary)}, visible={_nativeTaskbarWasVisible}, thread={Environment.CurrentManagedThreadId}.");
            HideTaskbars();
            _watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _watchdog.Tick += Watchdog_Tick;
            _watchdog.Start();
            WriteLog("Watchdog started.");
        }
    }

    public void HideNow() => HideTaskbars();

    public void Restore(string reason = "unspecified")
    {
        lock (_gate)
        {
            if (!_enabled)
            {
                WriteLog($"Restore ignored: replacement is inactive. Reason={reason}.");
                return;
            }

            WriteLog($"Restore requested. Reason={reason}.");
            _enabled = false;
            if (_watchdog is not null)
            {
                _watchdog.Stop();
                _watchdog.Tick -= Watchdog_Tick;
            }
            _watchdog = null;
            if (_nativeTaskbarWasVisible) ApplyToTaskbars(SwShow);
            WriteLog($"Restore completed. Taskbar visible={IsPrimaryTaskbarVisible()}.");
        }
    }

    public void Record(string message) => WriteLog(message);

    private static void StartRecoveryWatchdog()
    {
        try
        {
            var watchdogPath = Path.Combine(AppContext.BaseDirectory, "PokeDock.TaskbarWatchdog.exe");
            if (!File.Exists(watchdogPath))
            {
                WriteLog($"Recovery watchdog was not found: {watchdogPath}");
                return;
            }

            var startInfo = new ProcessStartInfo(watchdogPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

            var watchdog = Process.Start(startInfo);
            WriteLog($"Native recovery watchdog started. Pid={watchdog?.Id.ToString(CultureInfo.InvariantCulture) ?? "unknown"}.");
        }
        catch (Exception exception)
        {
            WriteLog($"Recovery watchdog could not start: {exception.Message}");
        }
    }

    private void HideTaskbars()
    {
        lock (_gate)
        {
            if (!_enabled) return;

            _hideAttempts++;
            var primary = FindWindow("Shell_TrayWnd", null);
            var wasVisible = primary != IntPtr.Zero && IsWindowVisible(primary);
            ApplyToTaskbars(SwHide);
            var remainsVisible = primary != IntPtr.Zero && IsWindowVisible(primary);

            if (_hideAttempts <= 10 || remainsVisible)
            {
                WriteLog(
                    $"Hide attempt #{_hideAttempts}: handle={FormatHandle(primary)}, " +
                    $"before={wasVisible}, after={remainsVisible}, thread={Environment.CurrentManagedThreadId}.");
            }
        }
    }

    private void Watchdog_Tick(object? sender, object e) => HideTaskbars();

    private static void ApplyToTaskbars(int command)
    {
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != IntPtr.Zero) _ = ShowWindow(primary, command);

        var current = IntPtr.Zero;
        while (true)
        {
            current = FindWindowEx(IntPtr.Zero, current, "Shell_SecondaryTrayWnd", null);
            if (current == IntPtr.Zero) break;
            _ = ShowWindow(current, command);
        }
    }

    private static bool IsPrimaryTaskbarVisible()
    {
        var primary = FindWindow("Shell_TrayWnd", null);
        return primary != IntPtr.Zero && IsWindowVisible(primary);
    }

    private static string FormatHandle(IntPtr handle) => $"0x{handle.ToInt64():X}";

    private static void StartLogSession()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.WriteAllText(
                LogPath,
                $"{DateTimeOffset.Now:O} PokeDock taskbar replacement session started.{Environment.NewLine}");
        }
        catch
        {
            // Logging must never prevent the dock from replacing the taskbar.
        }
    }

    private static void WriteLog(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never affect the application lifecycle.
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);
}
