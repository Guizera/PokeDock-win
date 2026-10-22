using System.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System;

namespace PokeDock.Windows;

internal sealed record WindowTaskGroup(
    string Name,
    string ExecutablePath,
    IReadOnlyList<WindowTask> Windows);

public sealed partial class DockWindow : Window
{
    private const int FixedDockWidth = 408;
    private const int DockHeight = 70;
    private const int RunningAppWidth = 50;
    private const int PinnedAppWidth = 50;
    private const int WidgetSpacing = 6;
    private const int WidgetSeparatorsWidth = 18;
    private const int ScreenEdgeMargin = 16;
    private const int CollapsedSearchWidth = 42;
    private readonly GameSession _session;
    private readonly DisplayArea? _targetDisplayArea;
    private readonly DockPreferences _preferences = DockPreferences.Current;
    private readonly WidgetStateStore _widgetState = WidgetStateStore.Current;
    private readonly MediaSessionService _media = new();
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _effectTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly WindowTaskService _windowTasks = new();
    private readonly Dictionary<string, byte[]?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<DockWidgetKind, TextBlock> _widgetDetails = [];
    private int _pinnedAppsBuildVersion;
    private double _effectTime;
    private bool _searchExpanded;
    private bool _rootLoaded;

    public DockWindow(GameSession session, DisplayArea? targetDisplayArea = null)
    {
        _session = session;
        _targetDisplayArea = targetDisplayArea;
        InitializeComponent();
        _session.Changed += Session_Changed;
        _preferences.Changed += Preferences_Changed;
        _widgetState.Changed += WidgetState_Changed;
        _media.Changed += Media_Changed;
        App.Current.NotificationBadges.Changed += NotificationBadges_Changed;
        _clockTimer.Tick += ClockTimer_Tick;
        _effectTimer.Tick += EffectTimer_Tick;
        _windowTasks.Changed += WindowTasks_Changed;
        Closed += DockWindow_Closed;
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        _rootLoaded = true;
        NativeWindow.ConfigureToolWindow(this, alwaysOnTop: true, borderless: true);
        BuildWidgets();
        _ = BuildPinnedAppsAsync();
        ResizeAndPositionDock();
        App.Current.EnableTaskbarReplacement();
        _windowTasks.Start();
        _clockTimer.Start();
        _effectTimer.Start();
        ApplyTheme();
        Refresh();
        RefreshSystemStatus();
        SearchPlaceholderText.Text = Localization.Text("SearchPlaceholder");
        _ = _media.InitializeAsync();
        if (_preferences.Widgets.Contains(DockWidgetKind.Dollar))
        {
            _ = _widgetState.RefreshDollarAsync();
        }
    }

    private void Session_Changed(object? sender, EventArgs e) => Refresh();

    private void ClockTimer_Tick(object? sender, object e)
    {
        RefreshWidgetValues();
        RefreshSystemStatus();
    }

    private void EffectTimer_Tick(object? sender, object e)
    {
        _effectTime += _preferences.AnimationsEnabled ? 0.05 : 0;
        DrawThemeEffect();
    }

    private void Preferences_Changed(object? sender, EventArgs e)
    {
        ApplyTheme();
        BuildWidgets();
        _ = BuildPinnedAppsAsync();
        ResizeAndPositionDock();
        _ = RefreshRunningAppsSafelyAsync();
        if (_preferences.Widgets.Contains(DockWidgetKind.Dollar))
        {
            _ = _widgetState.RefreshDollarAsync();
        }
    }

    private void WidgetState_Changed(object? sender, EventArgs e) =>
        _ = DispatcherQueue.TryEnqueue(RefreshWidgetValues);

    private void Media_Changed(object? sender, EventArgs e) =>
        _ = DispatcherQueue.TryEnqueue(RefreshWidgetValues);

    private void NotificationBadges_Changed(object? sender, EventArgs e) =>
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _ = BuildPinnedAppsAsync();
            _ = RefreshRunningAppsSafelyAsync();
        });

    private void WindowTasks_Changed(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _ = BuildPinnedAppsAsync();
            _ = RefreshRunningAppsSafelyAsync();
        });
    }

    private void Pokedex_Click(object sender, RoutedEventArgs e) => App.Current.ShowPokedex();

    private void Pokemon_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Game.CurrentEncounter is not null)
        {
            App.Current.ShowEncounter();
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.ShowDockSettings();

    private async void Exit_Click(object sender, RoutedEventArgs e) => await App.Current.ExitPokeDockAsync();

    private void Explorer_Click(object sender, RoutedEventArgs e)
    {
        _ = Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        if (_searchExpanded) return;
        _searchExpanded = true;
        SearchCollapseStoryboard.Stop();
        SearchButton.Visibility = Visibility.Collapsed;
        SearchEditor.Visibility = Visibility.Visible;
        SearchExpandStoryboard.Begin();
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
        });
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            CollapseSearch(clearText: true);
            return;
        }

        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        var query = SearchBox.Text.Trim();
        if (query.Length == 0) return;
        CollapseSearch(clearText: true);
        await NativeShell.SearchAsync(query);
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchBox.Text)) CollapseSearch(clearText: false);
    }

    private void DockSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholderText.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SearchClear_Click(object sender, RoutedEventArgs e) => CollapseSearch(clearText: true);

    private void CollapseSearch(bool clearText)
    {
        if (clearText) SearchBox.Text = string.Empty;
        if (!_searchExpanded) return;
        _searchExpanded = false;
        SearchExpandStoryboard.Stop();
        SearchCollapseStoryboard.Begin();
    }

    private void SearchCollapseStoryboard_Completed(object? sender, object e)
    {
        if (_searchExpanded) return;
        SearchEditor.Visibility = Visibility.Collapsed;
        SearchButton.Visibility = Visibility.Visible;
    }

    private void SearchHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_rootLoaded) ResizeAndPositionDock();
    }
    private void QuickSettings_Click(object sender, RoutedEventArgs e) => NativeShell.OpenQuickSettings();
    private void Notifications_Click(object sender, RoutedEventArgs e) => NativeShell.OpenNotificationsAndCalendar();
    private void TaskView_Click(object sender, RoutedEventArgs e) => NativeShell.OpenTaskView();
    private void SoundSettings_Click(object sender, RoutedEventArgs e) => NativeShell.OpenSettings("ms-settings:sound");
    private void NetworkSettings_Click(object sender, RoutedEventArgs e) => NativeShell.OpenSettings("ms-settings:network-status");

    private void RefreshSystemStatus()
    {
        if (BatteryStatusText is null || NetworkStatusIcon is null) return;
        var status = SystemStatusService.Read();
        BatteryStatusText.Text = status.BatteryPercent is { } percent ? $"{percent}%" : DateTime.Now.ToString("HH:mm");
        NetworkStatusIcon.Glyph = status.IsOnline ? "\uE701" : "\uEB55";
        SystemStatusButton.Opacity = status.IsOnline ? 1 : 0.78;
    }

    private void Lock_Click(object sender, RoutedEventArgs e) => NativePower.Lock();

    private async void Sleep_Click(object sender, RoutedEventArgs e)
    {
        if (await ConfirmPowerActionAsync(Localization.Text("SleepTitle"), Localization.Text("SleepMessage")))
        {
            NativePower.Sleep();
        }
    }

    private async void SignOut_Click(object sender, RoutedEventArgs e) =>
        await RunPowerActionAsync(Localization.Text("SignOutTitle"), Localization.Text("SignOutMessage"), "/l");

    private async void Restart_Click(object sender, RoutedEventArgs e) =>
        await RunPowerActionAsync(Localization.Text("RestartTitle"), Localization.Text("RestartMessage"), "/r /t 0");

    private async void Shutdown_Click(object sender, RoutedEventArgs e) =>
        await RunPowerActionAsync(Localization.Text("ShutDownTitle"), Localization.Text("ShutDownMessage"), "/s /t 0");

    private async Task RunPowerActionAsync(string title, string message, string arguments)
    {
        if (!await ConfirmPowerActionAsync(title, message)) return;
        await _session.SaveAsync();
        App.Current.RestoreDesktopAfterDockClosed();
        _ = Process.Start(new ProcessStartInfo("shutdown.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private async Task<bool> ConfirmPowerActionAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = title,
            CloseButtonText = Localization.Text("Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void DockWindow_Closed(object sender, WindowEventArgs args)
    {
        _rootLoaded = false;
        App.Current.NotifyDockWindowClosed(this);
        _clockTimer.Stop();
        _effectTimer.Stop();
        _windowTasks.Dispose();
        _session.Changed -= Session_Changed;
        _preferences.Changed -= Preferences_Changed;
        App.Current.NotificationBadges.Changed -= NotificationBadges_Changed;
        _widgetState.Changed -= WidgetState_Changed;
        _media.Changed -= Media_Changed;
        _media.Dispose();
        _windowTasks.Changed -= WindowTasks_Changed;
    }

    private void Refresh()
    {
        RefreshWidgetValues();
        var encounter = _session.Game.CurrentEncounter;
        var spriteId = encounter?.Id ?? _preferences.SelectedTheme.PokemonId;
        PokemonImage.Source = $"ms-appx:///Assets/Pokemon/{spriteId}.png";
        PokemonButton.IsEnabled = encounter is not null;
        PokemonButton.Opacity = encounter is null ? 0.86 : 1;
        PokemonButton.SetValue(ToolTipService.ToolTipProperty,
            encounter is null ? Localization.Text("ExploringKanto") : Localization.Format("CatchPokemon", encounter.Name));

    }

    private void ApplyTheme()
    {
        DockSurface.Background = DockSettingsWindow.CreateGradient(_preferences.SelectedTheme);
        Root.Background = DockSettingsWindow.CreateGradient(_preferences.SelectedTheme);
        DrawThemeEffect();
        Refresh();
    }

    private async Task RefreshRunningAppsAsync()
    {
        if (RunningAppsPanel is null) return;

        RunningAppsPanel.Children.Clear();
        if (!_preferences.ShowRunningApps)
        {
            ResizeAndPositionDock();
            return;
        }
        foreach (var group in VisibleRunningGroups())
        {
            var content = new Grid();
            var image = new Image { Width = 38, Height = 38, Stretch = Stretch.Uniform };
            var iconBytes = await GetIconBytesAsync(group.ExecutablePath);
            if (iconBytes is not null)
            {
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream))
                {
                    writer.WriteBytes(iconBytes);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                    writer.DetachStream();
                }
                stream.Seek(0);
                var source = new BitmapImage();
                await source.SetSourceAsync(stream);
                image.Source = source;
                content.Children.Add(image);
            }
            else
            {
                content.Children.Add(new FontIcon
                {
                    Glyph = "\uECAA",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 26,
                    Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 255, 255, 255))
                });
            }

            var indicator = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 1)
            };
            content.Children.Add(indicator);

            var badgeCount = App.Current.NotificationBadges.CountFor(group.Name);
            if (badgeCount == 0 && group.Windows.Count > 1) badgeCount = group.Windows.Count;
            if (badgeCount > 0)
            {
                content.Children.Add(WindowCountBadge(badgeCount));
            }

            var button = new Button
            {
                Width = 48,
                Height = 48,
                Content = content,
                Style = (Style)Root.Resources["DockIconButtonStyle"]
            };
            var windowHandle = group.Windows[0].WindowHandle;
            button.Click += (_, _) => NativeWindow.ActivateWindow(windowHandle);
            button.SetValue(
                ToolTipService.ToolTipProperty,
                group.Windows.Count == 1
                    ? group.Name
                    : Localization.Format("WindowCount", group.Name, group.Windows.Count));
            button.ContextFlyout = CreateRunningAppMenu(group);
            RunningAppsPanel.Children.Add(button);
        }

        ResizeAndPositionDock();
    }

    private async Task BuildPinnedAppsAsync()
    {
        if (PinnedAppsPanel is null) return;
        var buildVersion = ++_pinnedAppsBuildVersion;
        var buttons = new List<Button>();
        foreach (var app in _preferences.Apps)
        {
            var content = new Grid();
            var iconBytes = await GetIconBytesAsync(app.Path);
            if (buildVersion != _pinnedAppsBuildVersion) return;
            if (iconBytes is not null)
            {
                content.Children.Add(new Image
                {
                    Width = 38,
                    Height = 38,
                    Stretch = Stretch.Uniform,
                    Source = await BitmapFromBytesAsync(iconBytes)
                });
            }
            else
            {
                content.Children.Add(new FontIcon
                {
                    Glyph = "\uECAA",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 26,
                    Foreground = White(1)
                });
            }

            var running = RunningTasksFor(app);
            if (running.Count > 0)
            {
                content.Children.Add(new Ellipse
                {
                    Width = 5,
                    Height = 5,
                    Fill = White(1),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 1)
                });
            }
            var badgeCount = App.Current.NotificationBadges.CountFor(app.Name);
            if (badgeCount == 0 && running.Count > 1) badgeCount = running.Count;
            if (badgeCount > 0) content.Children.Add(WindowCountBadge(badgeCount));

            var button = new Button
            {
                Width = 48,
                Height = 48,
                Content = content,
                Style = (Style)Root.Resources["DockIconButtonStyle"]
            };
            button.Click += (_, _) => OpenPinnedApp(app);
            button.SetValue(ToolTipService.ToolTipProperty, app.Name);
            button.ContextFlyout = CreatePinnedAppMenu(app, running);
            buttons.Add(button);
        }
        if (buildVersion != _pinnedAppsBuildVersion) return;
        PinnedAppsPanel.Children.Clear();
        foreach (var button in buttons) PinnedAppsPanel.Children.Add(button);
        ResizeAndPositionDock();
    }

    private void OpenPinnedApp(DockAppDefinition app)
    {
        var running = RunningTasksFor(app);
        if (running.Count > 0)
        {
            NativeWindow.ActivateWindow(running[0].WindowHandle);
            return;
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo(app.Path) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            App.Current.RecordDiagnostic($"Pinned app launch failed ({app.Path}): {exception}");
        }
    }

    private MenuFlyout CreateRunningAppMenu(WindowTaskGroup group)
    {
        var menu = new MenuFlyout();
        AddWindowItems(menu, group.Windows);

        var pin = MenuItem(Localization.Text("PinToTaskbar"), "\uE718", () => _preferences.AddApp(group.ExecutablePath));
        pin.IsEnabled = !string.IsNullOrWhiteSpace(group.ExecutablePath) && File.Exists(group.ExecutablePath);
        menu.Items.Add(pin);

        var location = MenuItem(Localization.Text("OpenFileLocation"), "\uE838", () => OpenFileLocation(group.ExecutablePath));
        location.IsEnabled = !string.IsNullOrWhiteSpace(group.ExecutablePath) && File.Exists(group.ExecutablePath);
        menu.Items.Add(location);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MenuItem(Localization.Text("CloseAllWindows"), "\uE8BB", () =>
        {
            foreach (var window in group.Windows) NativeWindow.RequestCloseWindow(window.WindowHandle);
        }));
        return menu;
    }

    private MenuFlyout CreatePinnedAppMenu(DockAppDefinition app, IReadOnlyList<WindowTask> running)
    {
        var menu = new MenuFlyout();
        menu.Items.Add(MenuItem(
            running.Count == 0 ? Localization.Text("Open") : Localization.Text("ShowWindow"),
            "\uE8A7",
            () => OpenPinnedApp(app)));
        if (running.Count > 1) AddWindowItems(menu, running);
        menu.Items.Add(MenuItem(Localization.Text("UnpinFromTaskbar"), "\uE77A", () => _preferences.RemoveApp(app)));
        menu.Items.Add(MenuItem(Localization.Text("OpenFileLocation"), "\uE838", () => OpenFileLocation(app.Path)));
        if (running.Count > 0)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(MenuItem(Localization.Text("CloseAllWindows"), "\uE8BB", () =>
            {
                foreach (var window in running) NativeWindow.RequestCloseWindow(window.WindowHandle);
            }));
        }
        return menu;
    }

    private static void AddWindowItems(MenuFlyout menu, IReadOnlyList<WindowTask> windows)
    {
        foreach (var window in windows)
        {
            var title = NativeWindow.GetWindowTitle(window.WindowHandle);
            menu.Items.Add(MenuItem(
                string.IsNullOrWhiteSpace(title) ? window.Name : title,
                "\uE8A7",
                () => NativeWindow.ActivateWindow(window.WindowHandle)));
        }
        if (windows.Count > 0) menu.Items.Add(new MenuFlyoutSeparator());
    }

    private static Border WindowCountBadge(int count) => new()
    {
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        MinWidth = 16,
        Height = 16,
        Padding = new Thickness(3, 0, 3, 0),
        Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyActionBrush"],
        CornerRadius = new CornerRadius(8),
        Child = new TextBlock
        {
            Text = count.ToString(Localization.Culture),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["DockTextFont"],
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = White(1)
        }
    };

    private static MenuFlyoutItem MenuItem(string text, string glyph, Action action)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Icon = new FontIcon
            {
                Glyph = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons")
            }
        };
        item.Click += (_, _) => action();
        return item;
    }

    private static void OpenFileLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        startInfo.ArgumentList.Add($"/select,{path}");
        _ = Process.Start(startInfo);
    }

    private IReadOnlyList<WindowTask> RunningTasksFor(DockAppDefinition app) =>
        _windowTasks.Tasks.Where(task =>
            !string.IsNullOrWhiteSpace(task.ExecutablePath)
            && string.Equals(task.ExecutablePath, app.Path, StringComparison.OrdinalIgnoreCase)).ToArray();

    private IReadOnlyList<WindowTaskGroup> VisibleRunningGroups() => _windowTasks.Tasks
        .Where(task => !_preferences.Apps.Any(app =>
            !string.IsNullOrWhiteSpace(task.ExecutablePath)
            && string.Equals(task.ExecutablePath, app.Path, StringComparison.OrdinalIgnoreCase)))
        .GroupBy(task => string.IsNullOrWhiteSpace(task.ExecutablePath)
            ? $"PID:{task.ProcessId}"
            : task.ExecutablePath,
            StringComparer.OrdinalIgnoreCase)
        .Select(group => new WindowTaskGroup(
            group.First().Name,
            group.First().ExecutablePath,
            group.ToArray()))
        .ToArray();

    private static async Task<BitmapImage> BitmapFromBytesAsync(byte[] iconBytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(iconBytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        var source = new BitmapImage();
        await source.SetSourceAsync(stream);
        return source;
    }

    private void BuildWidgets()
    {
        WidgetsPanel.Children.Clear();
        _widgetDetails.Clear();

        foreach (var kind in _preferences.Widgets)
        {
            var definition = DockPreferences.Widget(kind);
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(new FontIcon
            {
                Glyph = definition.Glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 18,
                Foreground = White(0.94)
            });

            var labels = new StackPanel
            {
                Spacing = 1,
                VerticalAlignment = VerticalAlignment.Center
            };
            labels.Children.Add(new TextBlock
            {
                Text = definition.Title,
                FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["DockTextFont"],
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                CharacterSpacing = 20,
                Foreground = White(0.88),
                MaxLines = 1,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var detail = new TextBlock
            {
                FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["DockTextFont"],
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = White(1),
                MaxLines = 1,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            labels.Children.Add(detail);
            content.Children.Add(labels);

            var button = new Button
            {
                Width = definition.Width,
                Height = 48,
                Padding = new Thickness(7, 0, 7, 0),
                Content = content,
                Style = (Style)Root.Resources["DockIconButtonStyle"]
            };
            button.SetValue(ToolTipService.ToolTipProperty, definition.Subtitle);
            var selectedKind = kind;
            button.Click += (_, _) => ActivateWidget(selectedKind);
            if (kind is DockWidgetKind.Spotify or DockWidgetKind.Music)
            {
                button.ContextFlyout = CreateMediaMenu(kind);
            }
            WidgetsPanel.Children.Add(button);
            _widgetDetails[kind] = detail;
        }

        var hasWidgets = _preferences.Widgets.Count > 0;
        WidgetsLeftDivider.Visibility = hasWidgets ? Visibility.Visible : Visibility.Collapsed;
        WidgetsRightDivider.Visibility = hasWidgets ? Visibility.Visible : Visibility.Collapsed;
        WidgetsLeftSpacerColumn.Width = new GridLength(hasWidgets ? 9 : 0);
        WidgetsRightSpacerColumn.Width = new GridLength(hasWidgets ? 9 : 0);
        RefreshWidgetValues();
    }

    private void RefreshWidgetValues()
    {
        foreach (var (kind, text) in _widgetDetails)
        {
            text.Text = kind switch
            {
                DockWidgetKind.Clock => DateTime.Now.ToString("HH:mm"),
                DockWidgetKind.Pokedex => $"{_session.Game.CaughtCount}/151",
                DockWidgetKind.Encounter => FormatEncounterWidget(_session.Game.Snapshot.NextEncounterAt),
                DockWidgetKind.Spotify => MediaDetail(DockWidgetKind.Spotify),
                DockWidgetKind.Music => MediaDetail(DockWidgetKind.Music),
                DockWidgetKind.Dollar => _widgetState.DollarRate is { } rate
                    ? $"R$ {rate:0.0000}"
                    : _widgetState.DollarError ?? Localization.Text("Loading"),
                DockWidgetKind.Budget => _widgetState.MonthlyBudgetCents == 0
                    ? Localization.Text("SetLimit")
                    : WidgetStateStore.Money(_widgetState.RemainingBudgetCents),
                DockWidgetKind.Focus => FormatFocusTime(_widgetState.FocusRemainingSeconds),
                DockWidgetKind.Tasks => _widgetState.OpenTaskCount == 1
                    ? Localization.Text("OnePending")
                    : Localization.Format("PendingCount", _widgetState.OpenTaskCount),
                DockWidgetKind.Notes => FirstNoteLine(),
                DockWidgetKind.Arcade => Localization.Text("Play"),
                _ => string.Empty
            };
        }
    }

    private void ActivateWidget(DockWidgetKind kind)
    {
        switch (kind)
        {
            case DockWidgetKind.Pokedex:
                App.Current.ShowPokedex();
                break;
            case DockWidgetKind.Encounter when _session.Game.CurrentEncounter is not null:
                App.Current.ShowEncounter();
                break;
            case DockWidgetKind.Spotify:
                if (_media.Spotify is null) OpenUri("spotify:");
                else _ = _media.TogglePlayPauseAsync(kind);
                break;
            case DockWidgetKind.Music:
                if (_media.Music is null) OpenUri("mswindowsmusic:");
                else _ = _media.TogglePlayPauseAsync(kind);
                break;
            case DockWidgetKind.Dollar:
                _ = _widgetState.RefreshDollarAsync(force: true);
                break;
            case DockWidgetKind.Focus:
                _widgetState.ToggleFocus();
                break;
            case DockWidgetKind.Budget:
            case DockWidgetKind.Tasks:
            case DockWidgetKind.Notes:
            case DockWidgetKind.Arcade:
                App.Current.ShowWidgetHub(kind);
                break;
        }
    }

    private MenuFlyout CreateMediaMenu(DockWidgetKind kind)
    {
        var menu = new MenuFlyout();
        menu.Items.Add(MenuItem(Localization.Text("PreviousTrack"), "\uE892", () => _ = _media.SkipPreviousAsync(kind)));
        menu.Items.Add(MenuItem(Localization.Text("PlayPause"), "\uE768", () => _ = _media.TogglePlayPauseAsync(kind)));
        menu.Items.Add(MenuItem(Localization.Text("NextTrack"), "\uE893", () => _ = _media.SkipNextAsync(kind)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MenuItem(Localization.Text("OpenPlayer"), "\uE8A7", () =>
            OpenUri(kind == DockWidgetKind.Spotify ? "spotify:" : "mswindowsmusic:")));
        return menu;
    }

    private static void OpenUri(string uri)
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
            // The widget remains available even when the corresponding player is not installed.
        }
    }

    private string MediaDetail(DockWidgetKind kind)
    {
        var snapshot = _media.Snapshot(kind);
        if (snapshot is null) return kind == DockWidgetKind.Spotify ? Localization.Text("OpenApp") : Localization.Text("NothingPlaying");
        if (string.IsNullOrWhiteSpace(snapshot.Track)) return snapshot.IsPlaying ? Localization.Text("Playing") : Localization.Text("Paused");
        return snapshot.Track.Length <= 18 ? snapshot.Track : $"{snapshot.Track[..17]}…";
    }

    private async Task RefreshRunningAppsSafelyAsync()
    {
        try
        {
            await RefreshRunningAppsAsync();
        }
        catch (Exception exception)
        {
            App.Current.RecordDiagnostic($"Running apps refresh failed: {exception}");
        }
    }

    private async Task<byte[]?> GetIconBytesAsync(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;
        if (_iconCache.TryGetValue(executablePath, out var cached)) return cached;
        var bytes = await Task.Run(() => WindowTaskService.ExtractIconPng(executablePath));
        _iconCache[executablePath] = bytes;
        return bytes;
    }

    private void ResizeAndPositionDock()
    {
        var searchExtraWidth = Math.Max(0, (int)Math.Ceiling((SearchHost?.ActualWidth ?? CollapsedSearchWidth) - CollapsedSearchWidth));
        var (pinnedWidth, widgetsWidth, runningWidth) = GetDynamicWidths(searchExtraWidth);
        PinnedAppsScroller.Width = pinnedWidth;
        WidgetsScroller.Width = widgetsWidth;
        RunningAppsScroller.Width = runningWidth;
        var separators = _preferences.Widgets.Count > 0 ? WidgetSeparatorsWidth : 0;
        var width = FixedDockWidth + searchExtraWidth + separators + pinnedWidth + widgetsWidth + runningWidth;
        NativeWindow.PositionAtBottomCenter(this, width, DockHeight, 2, _targetDisplayArea);
        NativeWindow.ApplyRoundedRegion(this, width, DockHeight, 20);
    }

    private (int Pinned, int Widgets, int Running) GetDynamicWidths(int searchExtraWidth = 0)
    {
        var maximumDockWidth = NativeWindow.GetMaximumLogicalWidth(this, ScreenEdgeMargin, _targetDisplayArea);
        var separators = _preferences.Widgets.Count > 0 ? WidgetSeparatorsWidth : 0;
        var availableWidth = Math.Max(0, maximumDockWidth - FixedDockWidth - searchExtraWidth - separators);
        var desiredPinned = _preferences.Apps.Count * PinnedAppWidth;
        var pinned = Math.Min(desiredPinned, availableWidth);
        availableWidth -= pinned;

        var desiredRunning = _preferences.ShowRunningApps
            ? VisibleRunningGroups().Count * RunningAppWidth
            : 0;
        var reserveRunning = desiredRunning > 0 ? Math.Min(RunningAppWidth, availableWidth) : 0;
        var desiredWidgets = _preferences.Widgets.Count == 0
            ? 0
            : _preferences.Widgets.Sum(kind => DockPreferences.Widget(kind).Width)
                + (Math.Max(0, _preferences.Widgets.Count - 1) * WidgetSpacing);
        var widgets = Math.Min(desiredWidgets, Math.Max(0, availableWidth - reserveRunning));
        availableWidth -= widgets;
        var running = Math.Min(desiredRunning, availableWidth);
        return (pinned, widgets, running);
    }

    private void DrawThemeEffect()
    {
        if (ThemeEffectCanvas is null) return;
        ThemeEffectCanvas.Children.Clear();
        var width = ThemeEffectCanvas.ActualWidth > 0 ? ThemeEffectCanvas.ActualWidth : FixedDockWidth - 30;
        var height = ThemeEffectCanvas.ActualHeight > 0 ? ThemeEffectCanvas.ActualHeight : 58;
        switch (_preferences.Theme)
        {
            case DockTheme.Meadow: DrawLeaves(width, height); break;
            case DockTheme.Electric: DrawLightning(width, height); break;
            case DockTheme.Night: DrawNight(width, height); break;
            case DockTheme.Ocean: DrawWaves(width, height); break;
            case DockTheme.Volcanic: DrawEmbers(width, height); break;
            case DockTheme.Psychic: DrawOrbs(width, height); break;
        }
    }

    private void DrawLeaves(double width, double height)
    {
        for (var index = 0; index < 10; index++)
        {
            var progress = Fraction(index * 0.173 + _effectTime * (0.035 + (index % 3) * 0.009));
            var leaf = new Ellipse { Width = 14, Height = 7, Fill = White(0.10 + 0.06 * Fraction(index * 0.29)), RenderTransform = new RotateTransform { Angle = Math.Sin(_effectTime + index) * 35 } };
            Canvas.SetLeft(leaf, (1 - progress) * (width + 30) - 15);
            Canvas.SetTop(leaf, height * Fraction(index * 0.317) + Math.Sin(_effectTime * 1.4 + index) * 7);
            ThemeEffectCanvas.Children.Add(leaf);
        }
    }

    private void DrawLightning(double width, double height)
    {
        for (var index = 0; index < 3; index++)
        {
            var phase = Fraction(_effectTime * 0.82 + index * 0.34);
            var pulse = Math.Max(0, 1 - Math.Abs(phase - 0.16) / 0.17);
            var x = width * (0.17 + index * 0.32);
            var bolt = new Polyline { Stroke = White(0.18 + 0.58 * pulse), StrokeThickness = 2.5, Points = [new Point(x - 16, -8), new Point(x + 8, height * 0.28), new Point(x - 3, height * 0.45), new Point(x + 20, height * 0.70), new Point(x + 5, height + 8)] };
            ThemeEffectCanvas.Children.Add(bolt);
        }
    }

    private void DrawNight(double width, double height)
    {
        for (var index = 0; index < 16; index++)
        {
            var star = new Ellipse { Width = 2.5, Height = 2.5, Fill = White(0.10 + 0.20 * (0.5 + 0.5 * Math.Sin(_effectTime * 1.1 + index * 2.4))) };
            Canvas.SetLeft(star, width * Fraction(index * 0.618));
            Canvas.SetTop(star, height * Fraction(index * 0.371));
            ThemeEffectCanvas.Children.Add(star);
        }
    }

    private void DrawWaves(double width, double height)
    {
        for (var index = 0; index < 3; index++)
        {
            var points = new PointCollection();
            var y = height * (0.3 + index * 0.27);
            for (var step = 0; step <= 30; step++)
            {
                var x = width * step / 30;
                points.Add(new Point(x, y + Math.Sin(x * 0.025 + _effectTime * 0.8 + index) * 7));
            }
            ThemeEffectCanvas.Children.Add(new Polyline { Points = points, Stroke = White(0.12), StrokeThickness = 2 });
        }
    }

    private void DrawEmbers(double width, double height)
    {
        for (var index = 0; index < 15; index++)
        {
            var radius = 2 + 3 * Fraction(index * 0.371);
            var ember = new Ellipse { Width = radius, Height = radius, Fill = new SolidColorBrush(global::Windows.UI.Color.FromArgb((byte)(80 + 80 * Fraction(index * 0.157)), 255, 165, 0)) };
            Canvas.SetLeft(ember, width * Fraction(index * 0.618) + Math.Sin(_effectTime + index) * 8);
            Canvas.SetTop(ember, height * (1 - Fraction(index * 0.249 + _effectTime * 0.08)));
            ThemeEffectCanvas.Children.Add(ember);
        }
    }

    private void DrawOrbs(double width, double height)
    {
        for (var index = 0; index < 9; index++)
        {
            var radius = 5 + 8 * Fraction(index * 0.273);
            var orb = new Ellipse { Width = radius, Height = radius, Fill = White(0.08), Stroke = new SolidColorBrush(global::Windows.UI.Color.FromArgb(46, 255, 105, 180)), StrokeThickness = 1 };
            Canvas.SetLeft(orb, width * Fraction(index * 0.618) + Math.Sin(_effectTime * 0.55 + index) * 12);
            Canvas.SetTop(orb, height * Fraction(index * 0.317) + Math.Cos(_effectTime * 0.7 + index) * 8);
            ThemeEffectCanvas.Children.Add(orb);
        }
    }

    private static SolidColorBrush White(double opacity) => new(global::Windows.UI.Color.FromArgb((byte)(255 * Math.Clamp(opacity, 0, 1)), 255, 255, 255));
    private static double Fraction(double value) => value - Math.Floor(value);

    private string FirstNoteLine()
    {
        if (string.IsNullOrWhiteSpace(_widgetState.Note)) return Localization.Text("WriteNote");
        var firstLine = _widgetState.Note
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? Localization.Text("OpenNote");
        return firstLine.Length <= 18 ? firstLine : $"{firstLine[..17]}…";
    }

    private static string FormatFocusTime(int seconds) =>
        $"{seconds / 60:00}:{seconds % 60:00}";

    private string FormatEncounterWidget(DateTimeOffset? next)
    {
        if (_session.Game.CurrentEncounter is not null)
        {
            return Localization.Text("Now");
        }

        if (next is null)
        {
            return Localization.Text("Paused");
        }

        var remaining = next.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            return Localization.Text("Now");
        }

        return remaining.TotalMinutes < 60
            ? Localization.Format("InMinutes", Math.Max(1, Math.Ceiling(remaining.TotalMinutes)))
            : Localization.Format("InHours", Math.Ceiling(remaining.TotalHours));
    }
}
