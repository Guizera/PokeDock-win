using System.Security;
using PokeDock.Core.Models;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace PokeDock.Windows;

internal static class EncounterNotificationService
{
    public static void Show(PokemonSpecies species)
    {
        try
        {
            var title = SecurityElement.Escape(Localization.Text("WildPokemonAppeared"));
            var message = SecurityElement.Escape(Localization.Format("CatchPokemon", species.Name));
            var xml = new XmlDocument();
            xml.LoadXml($"""
                <toast>
                  <visual>
                    <binding template="ToastGeneric">
                      <image placement="appLogoOverride" hint-crop="circle" src="ms-appx:///Assets/Pokemon/{species.Id}.png" />
                      <text>{title}</text>
                      <text>{message}</text>
                    </binding>
                  </visual>
                </toast>
                """);
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(xml));
        }
        catch
        {
            // Notifications are optional and may be disabled by Windows policy.
        }
    }
}
