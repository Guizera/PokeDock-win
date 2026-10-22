param(
    [switch]$TestHide
)

$ErrorActionPreference = "Stop"

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class PokeDockTaskbarProbe
{
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const uint AbmGetState = 0x00000004;

    public sealed class WindowRow
    {
        public IntPtr Handle { get; set; }
        public string ClassName { get; set; }
        public string ProcessName { get; set; }
        public uint ProcessId { get; set; }
        public bool Visible { get; set; }
        public string Rectangle { get; set; }
    }

    public sealed class HideResult
    {
        public IntPtr Handle { get; set; }
        public string ClassName { get; set; }
        public bool Before { get; set; }
        public bool After100Ms { get; set; }
        public bool After1500Ms { get; set; }
    }

    public static List<WindowRow> Scan()
    {
        var rows = new List<WindowRow>();
        EnumWindows((handle, parameter) =>
        {
            var className = new StringBuilder(256);
            GetClassName(handle, className, className.Capacity);
            uint processId;
            GetWindowThreadProcessId(handle, out processId);
            var processName = "";
            try { processName = Process.GetProcessById((int)processId).ProcessName; } catch { }

            var name = className.ToString();
            if (!processName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                name.IndexOf("TrayWnd", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("Taskbar", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return true;
            }

            NativeRect rectangle;
            GetWindowRect(handle, out rectangle);
            rows.Add(new WindowRow
            {
                Handle = handle,
                ClassName = name,
                ProcessName = processName,
                ProcessId = processId,
                Visible = IsWindowVisible(handle),
                Rectangle = string.Format("{0},{1} - {2},{3}", rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom)
            });
            return true;
        }, IntPtr.Zero);
        return rows;
    }

    public static List<HideResult> TestHideAndRestore()
    {
        var targets = Scan().FindAll(row =>
            row.ClassName.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
            row.ClassName.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase));
        var results = new List<HideResult>();
        foreach (var target in targets)
        {
            var result = new HideResult
            {
                Handle = target.Handle,
                ClassName = target.ClassName,
                Before = IsWindowVisible(target.Handle)
            };
            ShowWindow(target.Handle, SwHide);
            Thread.Sleep(100);
            result.After100Ms = IsWindowVisible(target.Handle);
            Thread.Sleep(1400);
            result.After1500Ms = IsWindowVisible(target.Handle);
            ShowWindow(target.Handle, SwShow);
            results.Add(result);
        }
        return results;
    }

    public static uint GetAppBarState()
    {
        var data = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>() };
        return (uint)SHAppBarMessage(AbmGetState, ref data).ToUInt64();
    }

    private delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr windowHandle, StringBuilder className, int maximumCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

    [DllImport("shell32.dll")]
    private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint Size;
        public IntPtr WindowHandle;
        public uint CallbackMessage;
        public uint Edge;
        public NativeRect Rectangle;
        public IntPtr Parameter;
    }
}
'@

Write-Host "Windows: $([Environment]::OSVersion.VersionString)" -ForegroundColor Cyan
Write-Host "Process architecture: $([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)" -ForegroundColor Cyan
$appBarState = [PokeDockTaskbarProbe]::GetAppBarState()
Write-Host "AppBar state: $appBarState (auto-hide=$([bool]($appBarState -band 1)), always-on-top=$([bool]($appBarState -band 2)))" -ForegroundColor Cyan

Write-Host "`nExplorer/taskbar top-level windows:" -ForegroundColor Yellow
[PokeDockTaskbarProbe]::Scan() |
    Select-Object @{Name="Handle";Expression={"0x{0:X}" -f $_.Handle.ToInt64()}}, ClassName, ProcessName, ProcessId, Visible, Rectangle |
    Format-Table -AutoSize

if ($TestHide) {
    Write-Host "`nTesting SW_HIDE for 1.5 seconds, then restoring:" -ForegroundColor Yellow
    [PokeDockTaskbarProbe]::TestHideAndRestore() |
        Select-Object @{Name="Handle";Expression={"0x{0:X}" -f $_.Handle.ToInt64()}}, ClassName, Before, After100Ms, After1500Ms |
        Format-Table -AutoSize
}
