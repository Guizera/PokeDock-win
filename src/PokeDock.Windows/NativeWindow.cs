using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace PokeDock.Windows;

internal static class NativeWindow
{
    private const uint WmClose = 0x0010;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const long WsBorder = 0x00800000L;
    private const long WsDlgFrame = 0x00400000L;
    private const long WsCaption = WsBorder | WsDlgFrame;
    private const long WsThickFrame = 0x00040000L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;
    private const int SwRestore = 9;
    private const int DwmwaNcRenderingPolicy = 2;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmNcRenderingDisabled = 1;
    private const int DwmWindowCornerDoNotRound = 1;
    private const int DwmColorNone = unchecked((int)0xFFFFFFFE);

    public static AppWindow GetAppWindow(Window window)
    {
        var windowHandle = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        return AppWindow.GetFromWindowId(windowId);
    }

    public static void ConfigureToolWindow(Window window, bool alwaysOnTop, bool borderless)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = alwaysOnTop;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            if (borderless)
            {
                presenter.SetBorderAndTitleBar(false, false);
            }
        }

        var windowHandle = WindowNative.GetWindowHandle(window);
        if (borderless)
        {
            var windowStyle = GetWindowLongPtr(windowHandle, GwlStyle).ToInt64();
            windowStyle &= ~(WsCaption | WsThickFrame | WsBorder | WsDlgFrame);
            _ = SetWindowLongPtr(windowHandle, GwlStyle, new IntPtr(windowStyle));

            var nonClientRendering = DwmNcRenderingDisabled;
            _ = DwmSetWindowAttribute(
                windowHandle,
                DwmwaNcRenderingPolicy,
                ref nonClientRendering,
                sizeof(int));

            var cornerPreference = DwmWindowCornerDoNotRound;
            _ = DwmSetWindowAttribute(
                windowHandle,
                DwmwaWindowCornerPreference,
                ref cornerPreference,
                sizeof(int));

            var borderColor = DwmColorNone;
            _ = DwmSetWindowAttribute(windowHandle, DwmwaBorderColor, ref borderColor, sizeof(int));
        }
        var style = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
        style = (style | WsExToolWindow) & ~WsExAppWindow;
        _ = SetWindowLongPtr(windowHandle, GwlExStyle, new IntPtr(style));
        _ = SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);

        if (borderless)
        {
            // A frame change can make DWM recalculate the native outline, so
            // enforce the no-border color once more after applying the styles.
            var borderColor = DwmColorNone;
            _ = DwmSetWindowAttribute(windowHandle, DwmwaBorderColor, ref borderColor, sizeof(int));
        }
    }

    public static void ResizeAndCenter(Window window, int width, int height)
    {
        var appWindow = GetAppWindow(window);
        var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = area.WorkArea;
        var size = ScaleToPixels(window, width, height);
        appWindow.Resize(size);
        appWindow.Move(new PointInt32(
            workArea.X + ((workArea.Width - size.Width) / 2),
            workArea.Y + ((workArea.Height - size.Height) / 2)));
    }

    public static void PositionAtBottomCenter(
        Window window,
        int width,
        int height,
        int bottomMargin,
        DisplayArea? targetArea = null)
    {
        var appWindow = GetAppWindow(window);
        var area = targetArea ?? DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var bounds = area.OuterBounds;
        var size = ScaleToPixels(window, width, height);
        appWindow.Resize(size);
        appWindow.Move(new PointInt32(
            bounds.X + ((bounds.Width - size.Width) / 2),
            bounds.Y + bounds.Height - size.Height - ScaleToPixels(window, bottomMargin)));
    }

    public static int GetMaximumLogicalWidth(Window window, int horizontalMargin, DisplayArea? targetArea = null)
    {
        var appWindow = GetAppWindow(window);
        var area = targetArea ?? DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var availablePixels = Math.Max(
            0,
            area.OuterBounds.Width - (ScaleToPixels(window, horizontalMargin) * 2));
        var dpi = GetDpiForWindow(WindowNative.GetWindowHandle(window));
        return (int)Math.Floor(availablePixels * 96d / dpi);
    }

    public static void ApplyRoundedRegion(Window window, int width, int height, int radius)
    {
        var windowHandle = WindowNative.GetWindowHandle(window);
        var size = ScaleToPixels(window, width, height);
        var scaledRadius = ScaleToPixels(window, radius);
        var region = CreateRoundRectRgn(0, 0, size.Width + 1, size.Height + 1, scaledRadius * 2, scaledRadius * 2);
        if (region == IntPtr.Zero)
        {
            return;
        }

        if (SetWindowRgn(windowHandle, region, true) == 0)
        {
            _ = DeleteObject(region);
        }
    }

    public static void ActivateWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return;
        if (IsIconic(windowHandle)) _ = ShowWindow(windowHandle, SwRestore);
        _ = SetForegroundWindow(windowHandle);
    }

    public static void RequestCloseWindow(IntPtr windowHandle)
    {
        if (windowHandle != IntPtr.Zero) _ = PostMessage(windowHandle, WmClose, IntPtr.Zero, IntPtr.Zero);
    }

    public static string GetWindowTitle(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return string.Empty;
        var length = GetWindowTextLength(windowHandle);
        if (length <= 0) return string.Empty;
        var title = new StringBuilder(length + 1);
        _ = GetWindowText(windowHandle, title, title.Capacity);
        return title.ToString();
    }

    private static SizeInt32 ScaleToPixels(Window window, int width, int height) => new(
        ScaleToPixels(window, width),
        ScaleToPixels(window, height));

    private static int ScaleToPixels(Window window, int value)
    {
        var dpi = GetDpiForWindow(WindowNative.GetWindowHandle(window));
        return (int)Math.Round(value * dpi / 96d);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr newValue);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int ellipseWidth,
        int ellipseHeight);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr windowHandle, IntPtr region, bool redraw);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr graphicsObject);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maximumCount);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int value,
        int valueSize);
}
