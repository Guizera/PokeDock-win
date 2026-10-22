using Microsoft.UI.Xaml;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace PokeDock.Windows;

internal sealed class NotificationBadgeService : IDisposable
{
    private readonly UserNotificationListener _listener = UserNotificationListener.Current;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(12) };
    private IReadOnlyDictionary<string, int> _counts = new Dictionary<string, int>();
    private bool _refreshing;

    public NotificationBadgeService()
    {
        _timer.Tick += Timer_Tick;
    }

    public event EventHandler? Changed;

    public async Task<bool> EnableAsync()
    {
        try
        {
            if (await _listener.RequestAccessAsync() != UserNotificationListenerAccessStatus.Allowed)
            {
                return false;
            }

            _timer.Start();
            await RefreshAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Disable()
    {
        _timer.Stop();
        _counts = new Dictionary<string, int>();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public int CountFor(string applicationName)
    {
        var normalized = Normalize(applicationName);
        if (normalized.Length == 0) return 0;
        return _counts
            .Where(pair => pair.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            .Sum(pair => pair.Value);
    }

    public void Dispose()
    {
        Disable();
        _timer.Tick -= Timer_Tick;
    }

    private async void Timer_Tick(object? sender, object e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var notifications = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            _counts = notifications
                .Select(notification => Normalize(notification.AppInfo.DisplayInfo.DisplayName))
                .Where(name => name.Length > 0)
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Keep the last badge counts if notification access changes.
        }
        finally
        {
            _refreshing = false;
        }
    }

    private static string Normalize(string value) => new(
        value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
