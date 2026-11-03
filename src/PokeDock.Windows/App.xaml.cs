using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace PokeDock.Windows;

public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly TaskbarReplacementController _taskbar = new();
    private MainWindow? _pokedexWindow;
    private readonly List<DockWindow> _dockWindows = [];
    private EncounterWindow? _encounterWindow;
    private DockSettingsWindow? _dockSettingsWindow;
    private WidgetHubWindow? _widgetHubWindow;
    private readonly DispatcherTimer _displayRefreshTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private string _displayConfiguration = string.Empty;
    private bool _rebuildingDocks;
    private bool _exiting;

    public static new App Current => (App)Microsoft.UI.Xaml.Application.Current;
    public GameSession Session { get; } = new();
    internal NotificationBadgeService NotificationBadges { get; } = new();

    public App()
    {
        InitializeComponent();
        _displayRefreshTimer.Tick += DisplayRefreshTimer_Tick;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _taskbar.Restore("ProcessExit");
        UnhandledException += (_, args) =>
        {
            _taskbar.Record($"UnhandledException: {args.Exception}");
            _taskbar.Restore("UnhandledException");
        };
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        await Session.InitializeAsync();
        Session.EncounterAppeared += Session_EncounterAppeared;
        if (DockPreferences.Current.NotificationBadgesEnabled)
        {
            DockPreferences.Current.NotificationBadgesEnabled = await NotificationBadges.EnableAsync();
        }

        CreateDockWindows(FindDisplayAreas());
        _displayRefreshTimer.Start();
    }

    private void Session_EncounterAppeared(object? sender, PokemonEncounterEventArgs args)
    {
        if (DockPreferences.Current.EncounterNotificationsEnabled)
        {
            EncounterNotificationService.Show(args.Species);
        }
    }

    public void ShowPokedex()
    {
        if (_pokedexWindow is null)
        {
            _pokedexWindow = new MainWindow(Session);
            _pokedexWindow.Closed += (_, _) => _pokedexWindow = null;
        }

        _pokedexWindow.Activate();
    }

    public void ShowEncounter()
    {
        if (Session.Game.CurrentEncounter is null)
        {
            return;
        }

        Session.MarkCurrentEncounterSeen();
        if (_encounterWindow is null)
        {
            _encounterWindow = new EncounterWindow(Session);
            _encounterWindow.Closed += (_, _) => _encounterWindow = null;
        }

        _encounterWindow.Activate();
    }

    public void ShowDockSettings()
    {
        if (_dockSettingsWindow is null)
        {
            _dockSettingsWindow = new DockSettingsWindow();
            _dockSettingsWindow.Closed += (_, _) => _dockSettingsWindow = null;
        }

        _dockSettingsWindow.Activate();
    }

    public void ShowWidgetHub(DockWidgetKind kind)
    {
        if (_widgetHubWindow is null)
        {
            _widgetHubWindow = new WidgetHubWindow(kind);
            _widgetHubWindow.Closed += (_, _) => _widgetHubWindow = null;
        }

        _widgetHubWindow.Show(kind);
    }

    internal void RestoreDesktopAfterDockClosed()
    {
        _taskbar.Restore("DockWindow.Closed");
    }

    private void DisplayRefreshTimer_Tick(object? sender, object e)
    {
        if (_rebuildingDocks || _exiting) return;
        var displays = FindDisplayAreas();
        if (DisplayConfiguration(displays) == _displayConfiguration) return;
        RefreshDockDisplays(displays);
    }

    private void RefreshDockDisplays(IReadOnlyList<DisplayArea> displays)
    {
        _rebuildingDocks = true;
        try
        {
            foreach (var dock in _dockWindows.ToArray()) dock.Close();
            _dockWindows.Clear();
            CreateDockWindows(displays);
        }
        finally
        {
            _rebuildingDocks = false;
        }
    }

    internal void NotifyDockWindowClosed(DockWindow dockWindow)
    {
        _dockWindows.Remove(dockWindow);
        if (!_rebuildingDocks && !_exiting && _dockWindows.Count == 0)
        {
            _taskbar.Restore("Last DockWindow closed");
        }
    }

    private void CreateDockWindows(IReadOnlyList<DisplayArea> displays)
    {
        _displayConfiguration = DisplayConfiguration(displays);

        foreach (var display in displays)
        {
            var dock = new DockWindow(Session, display);
            _dockWindows.Add(dock);
            dock.Activate();
        }
    }

    private static IReadOnlyList<DisplayArea> FindDisplayAreas()
    {
        try
        {
            var displays = DisplayArea.FindAll().ToArray();
            return displays.Length > 0 ? displays : [DisplayArea.Primary];
        }
        catch
        {
            return [DisplayArea.Primary];
        }
    }

    private static string DisplayConfiguration(IEnumerable<DisplayArea> displays) => string.Join(
        "|",
        displays
            .Select(display => display.OuterBounds)
            .OrderBy(bounds => bounds.X)
            .ThenBy(bounds => bounds.Y)
            .ThenBy(bounds => bounds.Width)
            .ThenBy(bounds => bounds.Height)
            .Select(bounds => $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}"));

    internal void EnableTaskbarReplacement()
    {
        _taskbar.Enable();
        _taskbar.HideNow();
    }

    internal void RecordDiagnostic(string message)
    {
        _taskbar.Record(message);
    }

    public void CloseEncounter()
    {
        _encounterWindow?.Close();
    }

    public async Task ExitPokeDockAsync()
    {
        _exiting = true;
        _displayRefreshTimer.Stop();
        _displayRefreshTimer.Tick -= DisplayRefreshTimer_Tick;
        await Session.SaveAsync();
        _taskbar.Restore("Exit button");
        _encounterWindow?.Close();
        _dockSettingsWindow?.Close();
        _widgetHubWindow?.Close();
        _pokedexWindow?.Close();
        foreach (var dock in _dockWindows.ToArray()) dock.Close();
        _dockWindows.Clear();
        NotificationBadges.Dispose();
    }
}

