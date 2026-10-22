using Microsoft.UI.Xaml;
using PokeDock.Core.Game;
using PokeDock.Core.Models;

namespace PokeDock.Windows;

public sealed partial class EncounterWindow : Window
{
    private readonly GameSession _session;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly int _speciesId;
    private string _battleMessage = Localization.Text("WildPokemonAppeared");

    public EncounterWindow(GameSession session)
    {
        _session = session;
        _speciesId = session.Game.CurrentEncounter?.Id ?? 1;
        InitializeComponent();
        Title = Localization.Text("EncounterWindowTitle");
        _session.Changed += Session_Changed;
        _timer.Tick += Timer_Tick;
        Closed += EncounterWindow_Closed;
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        NativeWindow.ConfigureToolWindow(this, alwaysOnTop: true, borderless: false);
        NativeWindow.ResizeAndCenter(this, 520, 620);
        _timer.Start();
        Refresh();
    }

    private void Session_Changed(object? sender, EventArgs e) => Refresh();

    private void Timer_Tick(object? sender, object e) => RefreshCountdown();

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        CaptureButton.IsEnabled = false;
        RunButton.IsEnabled = false;
        _battleMessage = Localization.Text("ThrewPokeball");
        DialogueText.Text = _battleMessage;
        await Task.Delay(750);

        var result = _session.ThrowPokeball();
        _battleMessage = result switch
        {
            ThrowResult.BrokeFree => Localization.Text("BrokeFree"),
            ThrowResult.NoBalls => Localization.Text("NoPokeballs"),
            ThrowResult.Caught => Localization.Text("CaughtMessage"),
            ThrowResult.Fled => Localization.Text("FledMessage"),
            _ => Localization.Text("EncounterUnavailable")
        };
        Refresh();
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        _battleMessage = Localization.Text("RanSafely");
        _session.RunFromEncounter();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        _session.DismissEncounterResult();
        App.Current.CloseEncounter();
    }

    private void EncounterWindow_Closed(object sender, WindowEventArgs args)
    {
        _timer.Stop();
        _session.Changed -= Session_Changed;
        _ = _session.SaveAsync();
    }

    private void Refresh()
    {
        var species = _session.Game.CurrentEncounter
            ?? _session.Catalog.First(item => item.Id == _speciesId);
        var snapshot = _session.Game.Snapshot;
        var active = _session.Game.CurrentEncounter is not null;

        EncounterNameText.Text = species.Name.ToUpperInvariant();
        EncounterDetailsText.Text = Localization.Format("PokemonDetails", species.Id, FormatRarity(species.Rarity));
        CaptureChanceText.Text = active ? $"{_session.Game.CaptureChance(species):P0}" : "—";
        EncounterImage.Source = $"ms-appx:///Assets/Pokemon/{species.Id}.png";
        EncounterImage.Opacity = snapshot.LastEncounter?.Ending == EncounterEnding.Caught || active ? 1 : 0.55;
        AttemptText.Text = active
            ? snapshot.EncounterAttempts == 0 ? Localization.Text("FirstFailureSafe") : Localization.Format("Attempt", snapshot.EncounterAttempts + 1)
            : snapshot.LastEncounter?.Ending == EncounterEnding.Caught ? Localization.Text("RegisteredPokedex") : Localization.Text("EncounterEnded");

        CaptureButton.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        RunButton.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ContinueButton.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        CaptureButton.IsEnabled = active && snapshot.Pokeballs > 0;
        RunButton.IsEnabled = active;

        DialogueText.Text = active
            ? snapshot.Pokeballs == 0 ? Localization.Text("NoPokeballsHint") : _battleMessage
            : ResultMessage(species.Name, snapshot.LastEncounter?.Ending);
        BallCountText.Text = $"◉  {Localization.Format("Pokeballs", snapshot.Pokeballs, GameEngine.MaximumPokeballs)}";
        RefillText.Text = snapshot.PokeballsRefillAt is { } refill
            ? Localization.Format("Refill", FormatRemaining(refill))
            : Localization.Text("FullStock");
        RefreshCountdown();
    }

    private void RefreshCountdown()
    {
        var endsAt = _session.Game.Snapshot.EncounterEndsAt;
        ExpiryText.Text = endsAt is null ? string.Empty : Localization.Format("WillFlee", FormatRemaining(endsAt.Value));
    }

    private static string ResultMessage(string speciesName, EncounterEnding? ending) => ending switch
    {
        EncounterEnding.Caught => Localization.Format("CaughtNamed", speciesName),
        EncounterEnding.Fled => Localization.Format("FledNamed", speciesName),
        EncounterEnding.Ran => Localization.Text("RanSafely"),
        EncounterEnding.Expired => Localization.Text("TimeExpired"),
        _ => Localization.Text("EncounterFinished")
    };

    private static string FormatRarity(PokemonRarity rarity) => rarity switch
    {
        PokemonRarity.Common => Localization.Text("RarityCommon"),
        PokemonRarity.Uncommon => Localization.Text("RarityUncommon"),
        PokemonRarity.Rare => Localization.Text("RarityRare"),
        PokemonRarity.Special => Localization.Text("RaritySpecial"),
        _ => rarity.ToString().ToUpperInvariant()
    };

    private static string FormatRemaining(DateTimeOffset target)
    {
        var remaining = target - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            return Localization.Text("Now").TrimEnd('!');
        }

        return remaining.TotalHours >= 1
            ? Localization.Format("InHours", Math.Ceiling(remaining.TotalHours))
            : Localization.Format("InMinutes", Math.Max(1, Math.Ceiling(remaining.TotalMinutes)));
    }
}
