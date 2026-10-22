using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PokeDock.Core.Models;

namespace PokeDock.Windows;

public sealed partial class MainWindow : Window
{
    private readonly GameSession _session;
    private CollectionFilter _filter = CollectionFilter.All;
    private bool _detailDialogOpen;
    private bool _cardsInitialized;
    private Dictionary<int, PokemonProgress> _lastProgress = [];

    public MainWindow(GameSession session)
    {
        _session = session;
        InitializeComponent();
        _session.Changed += Session_Changed;
        Closed += MainWindow_Closed;
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        NativeWindow.ResizeAndCenter(this, 960, 720);
        Refresh();
    }

    private void Session_Changed(object? sender, EventArgs e) => Refresh();

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _session.Changed -= Session_Changed;
        _ = _session.SaveAsync();
    }

    private void SimulateEncounter_Click(object sender, RoutedEventArgs e) => _session.SimulateEncounter();
    private void DockSettings_Click(object sender, RoutedEventArgs e) => App.Current.ShowDockSettings();
    private void AllFilter_Click(object sender, RoutedEventArgs e) => SetFilter(CollectionFilter.All);
    private void CaughtFilter_Click(object sender, RoutedEventArgs e) => SetFilter(CollectionFilter.Caught);
    private void SeenFilter_Click(object sender, RoutedEventArgs e) => SetFilter(CollectionFilter.Seen);
    private void UnknownFilter_Click(object sender, RoutedEventArgs e) => SetFilter(CollectionFilter.Unknown);
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshCards();

    private void PokemonGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_detailDialogOpen || e.ClickedItem is not PokemonCardViewModel { CaughtCount: > 0 } card) return;
        var species = _session.Catalog.FirstOrDefault(item => item.Id == card.SpeciesId);
        if (species is null) return;

        _detailDialogOpen = true;
        ShowPokemonDetail(species, _session.Game.ProgressFor(species));
    }

    private void ClosePokemonDetail_Click(object sender, RoutedEventArgs e)
    {
        PokemonDetailOverlay.Visibility = Visibility.Collapsed;
        _detailDialogOpen = false;
    }

    private void ShowPokemonDetail(PokemonSpecies species, PokemonProgress progress)
    {
        DetailPokemonImage.Source = $"ms-appx:///Assets/Pokemon/{species.Id}.png";
        DetailPokemonNameText.Text = $"#{species.Id:000} {species.Name.ToUpperInvariant()}";
        DetailTypesPanel.Children.Clear();
        foreach (var type in species.Types)
        {
            DetailTypesPanel.Children.Add(new Border
            {
                Padding = new Thickness(10, 5, 10, 5),
                Background = TypeBrush(type),
                CornerRadius = new CornerRadius(13),
                Child = new TextBlock
                {
                    Text = Localization.Text($"Type{type}"),
                    FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyFont"],
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.Black,
                    Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 255, 255, 255))
                }
            });
        }

        DetailRarityText.Text = $"✦  {RarityName(species.Rarity)}";
        DetailRarityText.Foreground = RarityBrush(species.Rarity);
        DetailCaptureChanceText.Text = Localization.Format(
            "BaseCaptureChance",
            Math.Round(species.Rarity.BaseCaptureChance() * 100));
        DetailCaughtCountText.Text = progress.CaughtCount.ToString(Localization.Culture);
        DetailSeenCountText.Text = progress.SeenCount.ToString(Localization.Culture);
        DetailDuplicateCountText.Text = Math.Max(0, progress.CaughtCount - 1).ToString(Localization.Culture);
        DetailLastEncounterText.Text = progress.LastSeen?.ToLocalTime().ToString("f", Localization.Culture) ?? "—";
        PokemonDetailOverlay.Visibility = Visibility.Visible;
        ClosePokemonDetailButton.Focus(FocusState.Programmatic);
    }

    private void PokemonGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (PokemonGrid.ItemsPanelRoot is not ItemsWrapGrid panel || e.NewSize.Width <= 0) return;
        const double minimumCardWidth = 148;
        var availableWidth = Math.Max(minimumCardWidth, e.NewSize.Width - 12);
        var columns = Math.Max(1, (int)Math.Floor(availableWidth / minimumCardWidth));
        panel.ItemWidth = Math.Floor(availableWidth / columns);
    }

    private void SetFilter(CollectionFilter filter)
    {
        _filter = filter;
        UpdateFilterButtons();
        RefreshCards();
    }

    private void Refresh()
    {
        var game = _session.Game;
        var snapshot = game.Snapshot;
        CaughtCountText.Text = $"{game.CaughtCount}/151";
        SeenCountText.Text = $"{game.SeenCount}/151";
        DuplicateCountText.Text = game.DuplicateCount.ToString();
        BallCountText.Text = Localization.Format("Pokeballs", snapshot.Pokeballs, 10);
        SimulateEncounterButton.IsEnabled = !snapshot.IsPaused;
        StatusText.Text = snapshot.IsPaused ? Localization.Text("Paused") : game.CurrentEncounter is null ? Localization.Text("Exploring") : Localization.Text("EncounterAvailable");
        NextEncounterText.Text = game.CurrentEncounter is not null ? Localization.Text("PokemonOnDock") : FormatNextEncounter(snapshot.NextEncounterAt);
        if (!_cardsInitialized || ProgressChanged(snapshot.Progress))
        {
            _cardsInitialized = true;
            _lastProgress = new Dictionary<int, PokemonProgress>(snapshot.Progress);
            RefreshCards();
        }
    }

    private void RefreshCards()
    {
        if (PokemonGrid is null)
        {
            return;
        }

        var scrollViewer = FindDescendantScrollViewer(PokemonGrid);
        var verticalOffset = scrollViewer?.VerticalOffset ?? 0;
        var search = SearchBox?.Text?.Trim() ?? string.Empty;
        PokemonGrid.ItemsSource = _session.Catalog
            .Where(species => MatchesFilter(species) && MatchesSearch(species, search))
            .Select(species => new PokemonCardViewModel(species, _session.Game.ProgressFor(species)))
            .ToArray();
        if (scrollViewer is not null && verticalOffset > 0)
        {
            DispatcherQueue.TryEnqueue(() => scrollViewer.ChangeView(null, verticalOffset, null, true));
        }
    }

    private bool ProgressChanged(IReadOnlyDictionary<int, PokemonProgress> progress)
    {
        if (progress.Count != _lastProgress.Count) return true;
        return progress.Any(pair => !_lastProgress.TryGetValue(pair.Key, out var current) || current != pair.Value);
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject parent)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is ScrollViewer scrollViewer) return scrollViewer;
            var descendant = FindDescendantScrollViewer(child);
            if (descendant is not null) return descendant;
        }
        return null;
    }

    private bool MatchesFilter(PokemonSpecies species)
    {
        var progress = _session.Game.ProgressFor(species);
        return _filter switch
        {
            CollectionFilter.Caught => progress.CaughtCount > 0,
            CollectionFilter.Seen => progress.SeenCount > 0 && progress.CaughtCount == 0,
            CollectionFilter.Unknown => progress.SeenCount == 0,
            _ => true
        };
    }

    private static bool MatchesSearch(PokemonSpecies species, string search) =>
        string.IsNullOrWhiteSpace(search)
        || species.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || species.Id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);

    private void UpdateFilterButtons()
    {
        SetFilterButtonStyle(AllFilterButton, _filter == CollectionFilter.All);
        SetFilterButtonStyle(CaughtFilterButton, _filter == CollectionFilter.Caught);
        SetFilterButtonStyle(SeenFilterButton, _filter == CollectionFilter.Seen);
        SetFilterButtonStyle(UnknownFilterButton, _filter == CollectionFilter.Unknown);
    }

    private static void SetFilterButtonStyle(Button button, bool selected)
    {
        button.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[selected ? "GameBoyActionBrush" : "GameBoyLightBrush"];
        button.Foreground = selected ? new SolidColorBrush(Microsoft.UI.Colors.White) : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyDarkBrush"];
    }

    private static string FormatNextEncounter(DateTimeOffset? next)
    {
        if (next is null) return Localization.Text("NoEncounterScheduled");
        var remaining = next.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero) return Localization.Text("EncounterAnyMoment");
        return remaining.TotalMinutes < 60
            ? Localization.Format("NextInMinutes", Math.Max(1, Math.Ceiling(remaining.TotalMinutes)))
            : Localization.Format("NextInHours", Math.Ceiling(remaining.TotalHours));
    }

    private static string RarityName(PokemonRarity rarity) => rarity switch
    {
        PokemonRarity.Common => Localization.Text("RarityCommon"),
        PokemonRarity.Uncommon => Localization.Text("RarityUncommon"),
        PokemonRarity.Rare => Localization.Text("RarityRare"),
        PokemonRarity.Special => Localization.Text("RaritySpecial"),
        _ => rarity.ToString().ToUpperInvariant()
    };

    private static Brush RarityBrush(PokemonRarity rarity) => rarity switch
    {
        PokemonRarity.Common => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 55, 125, 66)),
        PokemonRarity.Uncommon => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 0, 161, 203)),
        PokemonRarity.Rare => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 116, 73, 170)),
        PokemonRarity.Special => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 190, 38, 91)),
        _ => (Brush)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyMidBrush"]
    };

    private static Brush TypeBrush(PokemonType type) => new SolidColorBrush(type switch
    {
        PokemonType.Normal => global::Windows.UI.Color.FromArgb(255, 120, 120, 105),
        PokemonType.Fire => global::Windows.UI.Color.FromArgb(255, 226, 74, 42),
        PokemonType.Water => global::Windows.UI.Color.FromArgb(255, 13, 145, 230),
        PokemonType.Electric => global::Windows.UI.Color.FromArgb(255, 221, 164, 0),
        PokemonType.Grass => global::Windows.UI.Color.FromArgb(255, 62, 153, 74),
        PokemonType.Ice => global::Windows.UI.Color.FromArgb(255, 31, 164, 181),
        PokemonType.Fighting => global::Windows.UI.Color.FromArgb(255, 177, 55, 50),
        PokemonType.Poison => global::Windows.UI.Color.FromArgb(255, 143, 62, 158),
        PokemonType.Ground => global::Windows.UI.Color.FromArgb(255, 174, 123, 58),
        PokemonType.Flying => global::Windows.UI.Color.FromArgb(255, 95, 119, 190),
        PokemonType.Psychic => global::Windows.UI.Color.FromArgb(255, 205, 56, 116),
        PokemonType.Bug => global::Windows.UI.Color.FromArgb(255, 112, 140, 36),
        PokemonType.Rock => global::Windows.UI.Color.FromArgb(255, 137, 113, 52),
        PokemonType.Ghost => global::Windows.UI.Color.FromArgb(255, 91, 70, 137),
        PokemonType.Dragon => global::Windows.UI.Color.FromArgb(255, 92, 62, 190),
        PokemonType.Steel => global::Windows.UI.Color.FromArgb(255, 102, 126, 137),
        PokemonType.Fairy => global::Windows.UI.Color.FromArgb(255, 200, 96, 148),
        _ => global::Windows.UI.Color.FromArgb(255, 80, 80, 80)
    });

    private enum CollectionFilter { All, Caught, Seen, Unknown }
}

public sealed class PokemonCardViewModel
{
    public PokemonCardViewModel(PokemonSpecies species, PokemonProgress progress)
    {
        var hasBeenSeen = progress.SeenCount > 0;
        SpeciesId = species.Id;
        CaughtCount = progress.CaughtCount;
        Number = $"#{species.Id:000}";
        DisplayName = hasBeenSeen ? species.Name.ToUpperInvariant() : "???";
        SpriteUri = $"ms-appx:///Assets/Pokemon/{species.Id}.png";
        SpriteOpacity = progress.CaughtCount > 0 ? 1d : 0.55d;
        SpriteVisibility = hasBeenSeen ? Visibility.Visible : Visibility.Collapsed;
        UnknownVisibility = hasBeenSeen ? Visibility.Collapsed : Visibility.Visible;
        Status = progress.CaughtCount > 0 ? Localization.Text("CaughtStatus") : hasBeenSeen ? Localization.Text("SeenStatus") : Localization.Text("UnknownStatus");
        CaptureBadge = progress.CaughtCount > 1 ? $"×{progress.CaughtCount}" : string.Empty;
        CaptureBadgeVisibility = progress.CaughtCount > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    public int SpeciesId { get; }
    public int CaughtCount { get; }
    public string Number { get; }
    public string DisplayName { get; }
    public string SpriteUri { get; }
    public double SpriteOpacity { get; }
    public Visibility SpriteVisibility { get; }
    public Visibility UnknownVisibility { get; }
    public string Status { get; }
    public string CaptureBadge { get; }
    public Visibility CaptureBadgeVisibility { get; }
}
