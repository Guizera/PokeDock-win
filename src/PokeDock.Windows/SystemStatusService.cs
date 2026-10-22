using Windows.Networking.Connectivity;
using Windows.System.Power;

namespace PokeDock.Windows;

internal sealed record SystemStatusSnapshot(int? BatteryPercent, bool IsOnline);

internal static class SystemStatusService
{
    public static SystemStatusSnapshot Read()
    {
        int? battery = null;
        try
        {
            var value = PowerManager.RemainingChargePercent;
            if (value is >= 0 and <= 100) battery = value;
        }
        catch
        {
            // Desktops without a battery do not expose a percentage.
        }

        var online = false;
        try
        {
            online = NetworkInformation.GetInternetConnectionProfile()?.GetNetworkConnectivityLevel()
                == NetworkConnectivityLevel.InternetAccess;
        }
        catch
        {
            // Network information can be restricted by policy.
        }

        return new SystemStatusSnapshot(battery, online);
    }
}
