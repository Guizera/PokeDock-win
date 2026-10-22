using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PokeDock.Core.Game;
using Windows.ApplicationModel;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PokeDock.Windows;

public sealed partial class DockSettingsWindow : Window
{
    private readonly DockPreferences _preferences = DockPreferences.Current;
    private readonly WidgetStateStore _widgetState = WidgetStateStore.Current;
    private readonly GameSession _session = App.Current.Session;
    private bool _initialized;

    public DockSettingsWindow()
    {
        InitializeComponent();
        Title = Localization.Text("CustomizeDockTitle");
        _preferences.Changed += Preferences_Changed;
        Closed += DockSettingsWindow_Closed;
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        _initialized = false;
        NativeWindow.ResizeAndCenter(this, 860, 790);
        AnimationsToggle.IsOn = _preferences.AnimationsEnabled;
        RunningAppsToggle.IsOn = _preferences.ShowRunningApps;
        AllDisplaysToggle.IsOn = _preferences.ShowOnAllDisplays;
        EncounterNotificationsToggle.IsOn = _preferences.EncounterNotificationsEnabled;
        NotificationBadgesToggle.IsOn = _preferences.NotificationBadgesEnabled;
        var startupState = await StartupService.GetStateAsync();
        ApplyStartupState(startupState);
        DataStatusText.Text = Localization.Format("Version", UpdateService.CurrentVersion);
        BuildFrequencyOptions();
        PauseEncountersToggle.IsOn = _session.Game.Snapshot.IsPaused;
        _initialized = true;
        BuildAddWidgetMenu();
        BuildThemeCards();
        BuildDockItemsEditor();
        RefreshPreview();
    }

    private void AnimationsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initialized) _preferences.AnimationsEnabled = AnimationsToggle.IsOn;
    }

    private void RunningAppsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initialized) _preferences.ShowRunningApps = RunningAppsToggle.IsOn;
    }

    private void AllDisplaysToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        _preferences.ShowOnAllDisplays = AllDisplaysToggle.IsOn;
        App.Current.RefreshDockDisplays();
    }

    private void EncounterNotificationsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initialized) _preferences.EncounterNotificationsEnabled = EncounterNotificationsToggle.IsOn;
    }

    private async void NotificationBadgesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        if (!NotificationBadgesToggle.IsOn)
        {
            _preferences.NotificationBadgesEnabled = false;
            App.Current.NotificationBadges.Disable();
            NotificationStatusText.Text = string.Empty;
            return;
        }

        var allowed = await App.Current.NotificationBadges.EnableAsync();
        _preferences.NotificationBadgesEnabled = allowed;
        if (!allowed)
        {
            _initialized = false;
            NotificationBadgesToggle.IsOn = false;
            _initialized = true;
            NotificationStatusText.Text = Localization.Text("NotificationAccessDenied");
        }
        else
        {
            NotificationStatusText.Text = string.Empty;
        }
    }

    private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        var requestedEnabled = StartupToggle.IsOn;
        _initialized = false;
        var state = await StartupService.SetEnabledAsync(requestedEnabled);
        ApplyStartupState(state);
        _initialized = true;

        if (requestedEnabled && state == StartupTaskState.DisabledByUser)
        {
            NativeShell.OpenSettings("ms-settings:startupapps");
        }
    }

    private void ApplyStartupState(StartupTaskState? state)
    {
        StartupToggle.IsOn = state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        StartupToggle.IsEnabled = state is not null and not StartupTaskState.DisabledByPolicy;
        StartupStatusText.Text = state switch
        {
            null => Localization.Text("StartupUnavailable"),
            StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy
                => Localization.Text("StartupManaged"),
            _ => string.Empty
        };
    }

    private async void ExportCollection_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = $"PokeDock-collection-{DateTime.Now:yyyy-MM-dd}"
        };
        picker.FileTypeChoices.Add("PokeDock", new List<string> { ".pokedock" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            await _session.ExportCollectionAsync(file.Path);
            DataStatusText.Text = Localization.Text("CollectionExported");
        }
        catch (Exception exception)
        {
            DataStatusText.Text = Localization.Format("CollectionTransferFailed", exception.Message);
        }
    }

    private async void ImportCollection_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add(".pokedock");
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            var imported = await _session.ImportCollectionAsync(file.Path);
            DataStatusText.Text = Localization.Format("CollectionImported", imported);
        }
        catch (Exception exception)
        {
            DataStatusText.Text = Localization.Format("CollectionTransferFailed", exception.Message);
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        DataStatusText.Text = Localization.Text("CheckingUpdates");

        try
        {
            var result = await UpdateService.CheckAsync();
            if (result.Availability is PackageUpdateAvailability.Available or PackageUpdateAvailability.Required)
            {
                DataStatusText.Text = Localization.Text("InstallingUpdate");
                await UpdateService.InstallAsync();
                return;
            }

            DataStatusText.Text = result.Availability switch
            {
                PackageUpdateAvailability.NoUpdates => Localization.Text("NoUpdates"),
                _ => Localization.Text("UpdateUnavailable")
            };
        }
        catch (Exception exception)
        {
            DataStatusText.Text = Localization.Format("UpdateFailed", exception.Message);
        }
    }

    private void BuildFrequencyOptions()
    {
        FrequencyCombo.Items.Clear();
        foreach (var frequency in Enum.GetValues<EncounterFrequency>())
        {
            FrequencyCombo.Items.Add(new ComboBoxItem
            {
                Content = FrequencyName(frequency),
                Tag = frequency
            });
        }

        var selected = _session.Game.Snapshot.Frequency;
        FrequencyCombo.SelectedIndex = Array.IndexOf(Enum.GetValues<EncounterFrequency>(), selected);
        FrequencyDescriptionText.Text = FrequencyDescription(selected);
    }

    private void FrequencyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FrequencyCombo.SelectedItem is not ComboBoxItem item || item.Tag is not EncounterFrequency frequency) return;
        FrequencyDescriptionText.Text = FrequencyDescription(frequency);
        if (_initialized) _session.SetEncounterFrequency(frequency);
    }

    private void PauseEncountersToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initialized) _session.SetEncountersPaused(PauseEncountersToggle.IsOn);
    }

    private static string FrequencyName(EncounterFrequency frequency) => frequency switch
    {
        EncounterFrequency.Calm => Localization.Text("FrequencyCalm"),
        EncounterFrequency.Normal => Localization.Text("FrequencyNormal"),
        EncounterFrequency.Lively => Localization.Text("FrequencyLively"),
        _ => frequency.ToString().ToUpperInvariant()
    };

    private static string FrequencyDescription(EncounterFrequency frequency) => frequency switch
    {
        EncounterFrequency.Calm => Localization.Text("FrequencyCalmDescription"),
        EncounterFrequency.Normal => Localization.Text("FrequencyNormalDescription"),
        EncounterFrequency.Lively => Localization.Text("FrequencyLivelyDescription"),
        _ => string.Empty
    };

    private void Preferences_Changed(object? sender, EventArgs e)
    {
        BuildThemeCards();
        BuildAddWidgetMenu();
        BuildDockItemsEditor();
        RefreshPreview();
    }

    private void DockSettingsWindow_Closed(object sender, WindowEventArgs args) =>
        _preferences.Changed -= Preferences_Changed;

    private void BuildThemeCards()
    {
        ThemeGrid.Children.Clear();
        for (var index = 0; index < DockPreferences.Themes.Count; index++)
        {
            var definition = DockPreferences.Themes[index];
            var selected = definition.Theme == _preferences.Theme;
            var content = new Grid { Padding = new Thickness(10), ColumnSpacing = 9 };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var image = new PixelArtImage
            {
                Width = 46,
                Height = 46,
                Source = $"ms-appx:///Assets/Pokemon/{definition.PokemonId}.png"
            };
            var labels = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(new TextBlock { Text = definition.Name, FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyFont"], FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Black, Foreground = new SolidColorBrush(Colors.White) });
            labels.Children.Add(new TextBlock { Text = definition.Subtitle, FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyFont"], FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White), Opacity = 0.9 });
            Grid.SetColumn(labels, 1);
            content.Children.Add(image);
            content.Children.Add(labels);
            if (selected)
            {
                var check = new TextBlock { Text = "✓", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Black, Foreground = new SolidColorBrush(Colors.White), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(check, 2);
                content.Children.Add(check);
            }

            var button = new Button
            {
                Content = content,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = CreateGradient(definition),
                BorderBrush = selected ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyActionBrush"] : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyDarkBrush"],
                BorderThickness = new Thickness(selected ? 4 : 2),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(0)
            };
            var theme = definition.Theme;
            button.Click += (_, _) => _preferences.Theme = theme;
            Grid.SetColumn(button, index % 3);
            Grid.SetRow(button, index / 3);
            ThemeGrid.Children.Add(button);
        }
    }

    private void BuildAddWidgetMenu()
    {
        var menu = new MenuFlyout();
        foreach (var definition in DockPreferences.AvailableWidgets)
        {
            var item = new MenuFlyoutItem
            {
                Text = definition.Title,
                IsEnabled = !_preferences.Widgets.Contains(definition.Kind),
                Icon = new FontIcon
                {
                    Glyph = definition.Glyph,
                    FontFamily = new FontFamily("Segoe Fluent Icons")
                }
            };
            var kind = definition.Kind;
            item.Click += (_, _) => _preferences.AddWidget(kind);
            menu.Items.Add(item);
        }
        AddWidgetButton.Flyout = menu;
    }

    private void BuildDockItemsEditor()
    {
        DockItemsPanel.Children.Clear();
        DockItemsPanel.Children.Add(CreateFixedItemRow("EXPLORER", Localization.Text("FixedApplication"), "\uE8B7"));

        for (var index = 0; index < _preferences.Apps.Count; index++)
        {
            var app = _preferences.Apps[index];
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            actions.Children.Add(CreateWidgetActionButton(
                "\uE76B",
                Localization.Text("MoveLeft"),
                () => _preferences.MoveApp(app, -1),
                index > 0));
            actions.Children.Add(CreateWidgetActionButton(
                "\uE76C",
                Localization.Text("MoveRight"),
                () => _preferences.MoveApp(app, 1),
                index < _preferences.Apps.Count - 1));
            actions.Children.Add(CreateWidgetActionButton(
                "\uE74D",
                Localization.Text("RemoveApp"),
                () => _preferences.RemoveApp(app)));
            DockItemsPanel.Children.Add(CreateItemRow(
                app.Name.ToUpperInvariant(),
                app.Path,
                "\uECAA",
                actions));
        }

        for (var index = 0; index < _preferences.Widgets.Count; index++)
        {
            var kind = _preferences.Widgets[index];
            var definition = DockPreferences.Widget(kind);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            actions.Children.Add(CreateWidgetActionButton(
                "\uE76B",
                Localization.Text("MoveLeft"),
                () => _preferences.MoveWidget(kind, -1),
                index > 0));
            actions.Children.Add(CreateWidgetActionButton(
                "\uE76C",
                Localization.Text("MoveRight"),
                () => _preferences.MoveWidget(kind, 1),
                index < _preferences.Widgets.Count - 1));
            actions.Children.Add(CreateWidgetActionButton(
                "\uE74D",
                Localization.Text("RemoveWidget"),
                () => _preferences.RemoveWidget(kind)));
            DockItemsPanel.Children.Add(CreateItemRow(
                definition.Title,
                Localization.Text("Widget"),
                definition.Glyph,
                actions));
        }

        DockItemsPanel.Children.Add(CreateFixedItemRow(Localization.Text("Power"), Localization.Text("PowerDescription"), "\uE7E8"));
    }

    private async void AddAppButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop
        };
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".lnk");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file is not null) _preferences.AddApp(file.Path);
    }

    private static Border CreateFixedItemRow(string title, string subtitle, string glyph) =>
        CreateItemRow(title, subtitle, glyph, new TextBlock
        {
            Text = Localization.Text("Fixed"),
            FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyFont"],
            FontSize = 8,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyMidBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });

    private static Border CreateItemRow(
        string title,
        string subtitle,
        string glyph,
        FrameworkElement actions)
    {
        var row = new Grid { Padding = new Thickness(10), ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 20,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyDarkBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });

        var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyFont"],
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyDarkBrush"]
        });
        labels.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyFont"],
            FontSize = 8,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyMidBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        });
        Grid.SetColumn(labels, 1);
        row.Children.Add(labels);
        Grid.SetColumn(actions, 2);
        row.Children.Add(actions);

        return new Border
        {
            Child = row,
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyLightBrush"],
            BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyDarkBrush"],
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5)
        };
    }

    private static Button CreateWidgetActionButton(
        string glyph,
        string tooltip,
        Action action,
        bool enabled = true)
    {
        var button = new Button
        {
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            IsEnabled = enabled,
            Content = new FontIcon
            {
                Glyph = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 12
            }
        };
        button.Click += (_, _) => action();
        button.SetValue(ToolTipService.ToolTipProperty, tooltip);
        return button;
    }

    private void RefreshPreview()
    {
        var selected = _preferences.SelectedTheme;
        PreviewSurface.Background = CreateGradient(selected);
        PreviewPokemon.Source = $"ms-appx:///Assets/Pokemon/{selected.PokemonId}.png";
        BuildPreviewWidgets();
    }

    private void BuildPreviewWidgets()
    {
        PreviewWidgetsPanel.Children.Clear();
        foreach (var kind in _preferences.Widgets)
        {
            var definition = DockPreferences.Widget(kind);
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(new FontIcon
            {
                Glyph = definition.Glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 17,
                Foreground = new SolidColorBrush(Colors.White)
            });

            var labels = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(new TextBlock
            {
                Text = definition.Title,
                FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["DockTextFont"],
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                CharacterSpacing = 20,
                Foreground = new SolidColorBrush(Colors.White),
                Opacity = 0.88,
                MaxLines = 1,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            labels.Children.Add(new TextBlock
            {
                Text = PreviewDetail(kind),
                FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["DockTextFont"],
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                MaxLines = 1,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            content.Children.Add(labels);

            PreviewWidgetsPanel.Children.Add(new Border
            {
                Width = definition.Width,
                Height = 48,
                Padding = new Thickness(7, 0, 7, 0),
                Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(34, 255, 255, 255)),
                CornerRadius = new CornerRadius(12),
                Child = content
            });
        }

        var visibility = _preferences.Widgets.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        PreviewRightDivider.Visibility = visibility;
    }

    private string PreviewDetail(DockWidgetKind kind) => kind switch
    {
        DockWidgetKind.Clock => DateTime.Now.ToString("HH:mm"),
        DockWidgetKind.Pokedex => $"{App.Current.Session.Game.CaughtCount}/151",
        DockWidgetKind.Encounter => App.Current.Session.Game.CurrentEncounter is not null ? Localization.Text("Now") : Localization.Text("ComingSoon"),
        DockWidgetKind.Spotify => Localization.Text("OpenApp"),
        DockWidgetKind.Music => Localization.Text("Media"),
        DockWidgetKind.Dollar => _widgetState.DollarRate is { } rate ? $"R$ {rate:0.0000}" : Localization.Text("Update"),
        DockWidgetKind.Budget => _widgetState.MonthlyBudgetCents == 0 ? Localization.Text("SetLimit") : WidgetStateStore.Money(_widgetState.RemainingBudgetCents),
        DockWidgetKind.Focus => $"{_widgetState.FocusRemainingSeconds / 60:00}:{_widgetState.FocusRemainingSeconds % 60:00}",
        DockWidgetKind.Tasks => Localization.Format("PendingCount", _widgetState.OpenTaskCount),
        DockWidgetKind.Notes => string.IsNullOrWhiteSpace(_widgetState.Note) ? Localization.Text("WriteNote") : Localization.Text("OpenNote"),
        DockWidgetKind.Arcade => Localization.Text("Play"),
        _ => string.Empty
    };

    internal static LinearGradientBrush CreateGradient(DockThemeDefinition definition)
    {
        var brush = new LinearGradientBrush { StartPoint = new global::Windows.Foundation.Point(0, 0), EndPoint = new global::Windows.Foundation.Point(1, 1) };
        brush.GradientStops.Add(new GradientStop { Color = ParseColor(definition.StartColor), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = ParseColor(definition.EndColor), Offset = 1 });
        return brush;
    }

    private static global::Windows.UI.Color ParseColor(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return global::Windows.UI.Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }
}
