using System.Text.Json;

namespace PokeDock.Windows;

public enum DockTheme
{
    Meadow,
    Electric,
    Night,
    Ocean,
    Volcanic,
    Psychic
}

public enum DockWidgetKind
{
    Clock,
    Pokedex,
    Encounter,
    Spotify,
    Music,
    Dollar,
    Budget,
    Focus,
    Tasks,
    Notes,
    Arcade
}

public sealed record DockWidgetDefinition(
    DockWidgetKind Kind,
    string Title,
    string Subtitle,
    string Glyph,
    int Width);

public sealed record DockThemeDefinition(
    DockTheme Theme,
    string Name,
    string Subtitle,
    string StartColor,
    string EndColor,
    int PokemonId);

public sealed record DockAppDefinition(string Path, string Name);

public sealed class DockPreferences
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PokeDock",
        "Dock",
        "dock-settings.json");

    public static DockPreferences Current { get; } = Load();

    public static IReadOnlyList<DockThemeDefinition> Themes { get; } =
    [
        new(DockTheme.Meadow, Localization.Text("ThemeMeadowName"), Localization.Text("ThemeMeadowSubtitle"), "#1A5940", "#61AD57", 1),
        new(DockTheme.Electric, Localization.Text("ThemeElectricName"), Localization.Text("ThemeElectricSubtitle"), "#6E3812", "#F2A81C", 25),
        new(DockTheme.Night, Localization.Text("ThemeNightName"), Localization.Text("ThemeNightSubtitle"), "#121433", "#4D306E", 94),
        new(DockTheme.Ocean, Localization.Text("ThemeOceanName"), Localization.Text("ThemeOceanSubtitle"), "#0A456E", "#1AA1BD", 7),
        new(DockTheme.Volcanic, Localization.Text("ThemeVolcanicName"), Localization.Text("ThemeVolcanicSubtitle"), "#4A141A", "#C7451A", 4),
        new(DockTheme.Psychic, Localization.Text("ThemePsychicName"), Localization.Text("ThemePsychicSubtitle"), "#401F6B", "#9954B3", 151)
    ];

    public static IReadOnlyList<DockWidgetDefinition> AvailableWidgets { get; } =
    [
        new(DockWidgetKind.Clock, Localization.Text("WidgetClockTitle"), Localization.Text("WidgetClockSubtitle"), "\uE823", 108),
        new(DockWidgetKind.Pokedex, "POKÉDEX", Localization.Text("WidgetPokedexSubtitle"), "\uECA5", 120),
        new(DockWidgetKind.Encounter, Localization.Text("WidgetEncounterTitle"), Localization.Text("WidgetEncounterSubtitle"), "\uE734", 154),
        new(DockWidgetKind.Spotify, "SPOTIFY", Localization.Text("WidgetSpotifySubtitle"), "\uE8D6", 178),
        new(DockWidgetKind.Music, Localization.Text("WidgetMusicTitle"), Localization.Text("WidgetMusicSubtitle"), "\uE8D6", 178),
        new(DockWidgetKind.Dollar, "USD/BRL", Localization.Text("WidgetDollarSubtitle"), "\uEAFD", 146),
        new(DockWidgetKind.Budget, Localization.Text("WidgetBudgetTitle"), Localization.Text("WidgetBudgetSubtitle"), "\uE9D2", 164),
        new(DockWidgetKind.Focus, Localization.Text("WidgetFocusTitle"), Localization.Text("WidgetFocusSubtitle"), "\uE916", 132),
        new(DockWidgetKind.Tasks, Localization.Text("WidgetTasksTitle"), Localization.Text("WidgetTasksSubtitle"), "\uE9D5", 138),
        new(DockWidgetKind.Notes, Localization.Text("WidgetNotesTitle"), Localization.Text("WidgetNotesSubtitle"), "\uE70B", 154),
        new(DockWidgetKind.Arcade, "ARCADE", Localization.Text("WidgetArcadeSubtitle"), "\uE7FC", 84)
    ];

    private DockTheme _theme = DockTheme.Meadow;
    private bool _animationsEnabled = true;
    private bool _showRunningApps = true;
    private bool _encounterNotificationsEnabled = true;
    private bool _showOnAllDisplays;
    private bool _notificationBadgesEnabled;
    private List<DockWidgetKind> _widgets = [DockWidgetKind.Clock, DockWidgetKind.Encounter];
    private List<DockAppDefinition> _apps = [];

    public event EventHandler? Changed;

    public DockTheme Theme
    {
        get => _theme;
        set
        {
            if (_theme == value) return;
            _theme = value;
            Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            if (_animationsEnabled == value) return;
            _animationsEnabled = value;
            Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<DockWidgetKind> Widgets => _widgets;
    public IReadOnlyList<DockAppDefinition> Apps => _apps;

    public bool ShowRunningApps
    {
        get => _showRunningApps;
        set
        {
            if (_showRunningApps == value) return;
            _showRunningApps = value;
            PersistAndNotify();
        }
    }

    public bool EncounterNotificationsEnabled
    {
        get => _encounterNotificationsEnabled;
        set
        {
            if (_encounterNotificationsEnabled == value) return;
            _encounterNotificationsEnabled = value;
            PersistAndNotify();
        }
    }

    public bool ShowOnAllDisplays
    {
        get => _showOnAllDisplays;
        set
        {
            if (_showOnAllDisplays == value) return;
            _showOnAllDisplays = value;
            PersistAndNotify();
        }
    }

    public bool NotificationBadgesEnabled
    {
        get => _notificationBadgesEnabled;
        set
        {
            if (_notificationBadgesEnabled == value) return;
            _notificationBadgesEnabled = value;
            PersistAndNotify();
        }
    }

    public void AddWidget(DockWidgetKind widget)
    {
        if (!Enum.IsDefined(widget) || _widgets.Contains(widget)) return;
        _widgets.Add(widget);
        PersistAndNotify();
    }

    public void RemoveWidget(DockWidgetKind widget)
    {
        if (!_widgets.Remove(widget)) return;
        PersistAndNotify();
    }

    public void MoveWidget(DockWidgetKind widget, int offset)
    {
        var currentIndex = _widgets.IndexOf(widget);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _widgets.Count) return;
        (_widgets[currentIndex], _widgets[targetIndex]) = (_widgets[targetIndex], _widgets[currentIndex]);
        PersistAndNotify();
    }

    public void AddApp(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        if (_apps.Any(app => string.Equals(app.Path, path, StringComparison.OrdinalIgnoreCase))) return;
        _apps.Add(new DockAppDefinition(path, AppDisplayName(path)));
        PersistAndNotify();
    }

    public void RemoveApp(DockAppDefinition app)
    {
        var removed = _apps.RemoveAll(candidate =>
            string.Equals(candidate.Path, app.Path, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) PersistAndNotify();
    }

    public void MoveApp(DockAppDefinition app, int offset)
    {
        var currentIndex = _apps.FindIndex(candidate =>
            string.Equals(candidate.Path, app.Path, StringComparison.OrdinalIgnoreCase));
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _apps.Count) return;
        (_apps[currentIndex], _apps[targetIndex]) = (_apps[targetIndex], _apps[currentIndex]);
        PersistAndNotify();
    }

    public DockThemeDefinition SelectedTheme => Themes.First(item => item.Theme == Theme);

    public static DockWidgetDefinition Widget(DockWidgetKind kind) =>
        AvailableWidgets.First(item => item.Kind == kind);

    private void PersistAndNotify()
    {
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new StoredSettings(
            Theme,
            AnimationsEnabled,
            _widgets,
            ShowRunningApps,
            _apps,
            EncounterNotificationsEnabled,
            ShowOnAllDisplays,
            NotificationBadgesEnabled)));
    }

    private static DockPreferences Load()
    {
        var preferences = new DockPreferences();
        try
        {
            if (!File.Exists(SettingsPath)) return preferences;
            var stored = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(SettingsPath));
            if (stored is null || !Enum.IsDefined(stored.Theme)) return preferences;
            preferences._theme = stored.Theme;
            preferences._animationsEnabled = stored.AnimationsEnabled;
            preferences._showRunningApps = stored.ShowRunningApps;
            preferences._encounterNotificationsEnabled = stored.EncounterNotificationsEnabled;
            preferences._showOnAllDisplays = stored.ShowOnAllDisplays;
            preferences._notificationBadgesEnabled = stored.NotificationBadgesEnabled;
            if (stored.Widgets is not null)
            {
                preferences._widgets = stored.Widgets
                    .Where(Enum.IsDefined)
                    .Distinct()
                    .ToList();
            }
            if (stored.Apps is not null)
            {
                preferences._apps = stored.Apps
                    .Where(app => !string.IsNullOrWhiteSpace(app.Path) && File.Exists(app.Path))
                    .DistinctBy(app => app.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // A configuração inválida é substituída pelos padrões no próximo salvamento.
        }
        catch (IOException)
        {
            // A Dock continua disponível com o tema padrão quando o arquivo está indisponível.
        }
        return preferences;
    }

    private static string AppDisplayName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        try
        {
            var description = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileDescription;
            return string.IsNullOrWhiteSpace(description) ? name : description.Trim();
        }
        catch
        {
            return name;
        }
    }

    private sealed record StoredSettings(
        DockTheme Theme,
        bool AnimationsEnabled,
        List<DockWidgetKind>? Widgets = null,
        bool ShowRunningApps = true,
        List<DockAppDefinition>? Apps = null,
        bool EncounterNotificationsEnabled = true,
        bool ShowOnAllDisplays = false,
        bool NotificationBadgesEnabled = false);
}
