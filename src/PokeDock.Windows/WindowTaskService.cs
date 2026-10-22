using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace PokeDock.Windows;

internal sealed record WindowTask(int ProcessId, string Name, string ExecutablePath, IntPtr WindowHandle);

/// <summary>
/// Produces one task button per desktop application that owns a visible top-level
/// window. Explorer and PokeDock are excluded because they already have fixed items.
/// </summary>
internal sealed class WindowTaskService : IDisposable
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const uint GwOwner = 4;
    private const int DwmwaCloaked = 14;

    private readonly object _gate = new();
    private System.Threading.Timer? _timer;
    private IReadOnlyList<WindowTask> _tasks = [];

    public event EventHandler? Changed;

    public IReadOnlyList<WindowTask> Tasks
    {
        get { lock (_gate) return _tasks; }
    }

    public void Start()
    {
        Refresh();
        _timer = new System.Threading.Timer(_ => Refresh(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public static byte[]? ExtractIconPng(string executablePath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null) return null;
            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private void Refresh()
    {
        var next = EnumerateTasks();
        lock (_gate)
        {
            if (_tasks.SequenceEqual(next)) return;
            _tasks = next;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<WindowTask> EnumerateTasks()
    {
        var windows = new List<(IntPtr Handle, uint ProcessId, string Title)>();
        _ = EnumWindows((windowHandle, parameter) =>
        {
            if (!IsTaskWindow(windowHandle)) return true;
            _ = GetWindowThreadProcessId(windowHandle, out var processId);
            if (processId == 0 || processId == Environment.ProcessId) return true;
            var titleLength = GetWindowTextLength(windowHandle);
            if (titleLength <= 0) return true;
            var title = new StringBuilder(titleLength + 1);
            _ = GetWindowText(windowHandle, title, title.Capacity);
            if (title.Length > 0) windows.Add((windowHandle, processId, title.ToString()));
            return true;
        }, IntPtr.Zero);

        var results = new List<WindowTask>();
        foreach (var window in windows)
        {
            try
            {
                using var process = Process.GetProcessById((int)window.ProcessId);
                var processName = process.ProcessName;
                if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase)) continue;
                var executablePath = process.MainModule?.FileName ?? string.Empty;
                var name = TryGetDisplayName(executablePath) ?? processName;
                results.Add(new WindowTask((int)window.ProcessId, name, executablePath, window.Handle));
            }
            catch
            {
                results.Add(new WindowTask((int)window.ProcessId, window.Title, string.Empty, window.Handle));
            }
        }

        return results
            .DistinctBy(task => task.WindowHandle)
            .OrderBy(task => task.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static bool IsTaskWindow(IntPtr windowHandle)
    {
        if (!IsWindowVisible(windowHandle)) return false;
        if (DwmGetWindowAttribute(windowHandle, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        var owner = GetWindow(windowHandle, GwOwner);
        var style = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
        if ((style & WsExToolWindow) != 0 && (style & WsExAppWindow) == 0) return false;
        return owner == IntPtr.Zero || (style & WsExAppWindow) != 0;
    }

    private static string? TryGetDisplayName(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;
        try
        {
            var description = FileVersionInfo.GetVersionInfo(executablePath).FileDescription;
            return string.IsNullOrWhiteSpace(description)
                ? Path.GetFileNameWithoutExtension(executablePath)
                : description.Trim();
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(executablePath);
        }
    }

    private delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr windowHandle, uint command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maximumCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr windowHandle, int attribute, out int value, int valueSize);
}
