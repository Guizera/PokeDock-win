using Microsoft.UI.Xaml.Media;

namespace PokeDock.Windows;

internal static class Iconography
{
    public static FontFamily Font =>
        (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["DockIconFont"];
}
