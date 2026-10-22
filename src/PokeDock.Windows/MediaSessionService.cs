using Microsoft.UI.Xaml;
using Windows.Media.Control;

namespace PokeDock.Windows;

internal sealed record MediaWidgetSnapshot(
    string Track,
    string Artist,
    bool IsPlaying,
    string SourceAppId);

internal sealed class MediaSessionService : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(6) };
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private bool _refreshing;

    public MediaSessionService()
    {
        _timer.Tick += Timer_Tick;
    }

    public event EventHandler? Changed;

    public MediaWidgetSnapshot? Spotify { get; private set; }
    public MediaWidgetSnapshot? Music { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.SessionsChanged += Manager_SessionsChanged;
            _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
            _timer.Start();
            await RefreshAsync();
        }
        catch
        {
            // Media widgets remain launchers when Windows media sessions are unavailable.
        }
    }

    public MediaWidgetSnapshot? Snapshot(DockWidgetKind kind) =>
        kind == DockWidgetKind.Spotify ? Spotify : Music;

    public async Task TogglePlayPauseAsync(DockWidgetKind kind)
    {
        var session = FindSession(kind);
        if (session is null) return;
        try
        {
            _ = await session.TryTogglePlayPauseAsync();
            await RefreshAsync();
        }
        catch
        {
            // A player can close between discovery and the command.
        }
    }

    public Task SkipPreviousAsync(DockWidgetKind kind) => SendCommandAsync(
        kind,
        session => session.TrySkipPreviousAsync());

    public Task SkipNextAsync(DockWidgetKind kind) => SendCommandAsync(
        kind,
        session => session.TrySkipNextAsync());

    private async Task SendCommandAsync(
        DockWidgetKind kind,
        Func<GlobalSystemMediaTransportControlsSession, global::Windows.Foundation.IAsyncOperation<bool>> command)
    {
        var session = FindSession(kind);
        if (session is null) return;
        try
        {
            _ = await command(session);
            await RefreshAsync();
        }
        catch
        {
            // A player can close or reject a command while its session changes.
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        if (_manager is not null)
        {
            _manager.SessionsChanged -= Manager_SessionsChanged;
            _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
        }
    }

    private async void Timer_Tick(object? sender, object e) => await RefreshAsync();

    private void Manager_SessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) => _ = RefreshAsync();

    private void Manager_CurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_manager is null || _refreshing) return;
        _refreshing = true;
        try
        {
            Spotify = await ReadAsync(FindSession(DockWidgetKind.Spotify));
            Music = await ReadAsync(FindSession(DockWidgetKind.Music));
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Keep the last valid metadata while a session changes.
        }
        finally
        {
            _refreshing = false;
        }
    }

    private GlobalSystemMediaTransportControlsSession? FindSession(DockWidgetKind kind)
    {
        if (_manager is null) return null;
        var sessions = _manager.GetSessions();
        if (kind == DockWidgetKind.Spotify)
        {
            return sessions.FirstOrDefault(session =>
                session.SourceAppUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase));
        }

        var current = _manager.GetCurrentSession();
        if (current is not null &&
            !current.SourceAppUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }
        return sessions.FirstOrDefault(session =>
            !session.SourceAppUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<MediaWidgetSnapshot?> ReadAsync(
        GlobalSystemMediaTransportControlsSession? session)
    {
        if (session is null) return null;
        var properties = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        return new MediaWidgetSnapshot(
            properties.Title ?? string.Empty,
            properties.Artist ?? string.Empty,
            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            session.SourceAppUserModelId);
    }
}
