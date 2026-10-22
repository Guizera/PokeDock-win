using Windows.ApplicationModel;

namespace PokeDock.Windows;

internal static class StartupService
{
    private const string TaskId = "PokeDockStartup";

    public static async Task<StartupTaskState?> GetStateAsync()
    {
        try
        {
            return (await StartupTask.GetAsync(TaskId)).State;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<StartupTaskState?> SetEnabledAsync(bool enabled)
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (!enabled)
            {
                task.Disable();
                return task.State;
            }

            return task.State == StartupTaskState.Disabled
                ? await task.RequestEnableAsync()
                : task.State;
        }
        catch
        {
            return null;
        }
    }
}
