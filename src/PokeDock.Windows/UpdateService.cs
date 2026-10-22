using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace PokeDock.Windows;

internal sealed record UpdateCheckResult(string Version, PackageUpdateAvailability? Availability);

internal static class UpdateService
{
    public static string CurrentVersion
    {
        get
        {
            try
            {
                var version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch
            {
                return "DEV";
            }
        }
    }

    public static async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            var packageManager = new PackageManager();
            var package = packageManager.FindPackageForUser(string.Empty, Package.Current.Id.FullName);
            var result = await package.CheckUpdateAvailabilityAsync();
            return new UpdateCheckResult(CurrentVersion, result.Availability);
        }
        catch
        {
            return new UpdateCheckResult(CurrentVersion, null);
        }
    }

    public static async Task InstallAsync()
    {
        var packageManager = new PackageManager();
        var package = packageManager.FindPackageForUser(string.Empty, Package.Current.Id.FullName);
        var appInstallerInfo = package.GetAppInstallerInfo()
            ?? throw new InvalidOperationException("This installation is not associated with an App Installer feed.");

        var result = await packageManager.RequestAddPackageByAppInstallerFileAsync(
            appInstallerInfo.Uri,
            AddPackageByAppInstallerOptions.ForceTargetAppShutdown,
            packageManager.GetDefaultPackageVolume());

        if (result.ExtendedErrorCode is not null)
        {
            throw new InvalidOperationException(result.ErrorText, result.ExtendedErrorCode);
        }
    }
}
