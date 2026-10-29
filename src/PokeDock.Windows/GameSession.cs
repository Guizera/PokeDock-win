using Microsoft.UI.Xaml;
using PokeDock.Application.Persistence;
using PokeDock.Core.Catalog;
using PokeDock.Core.Game;
using PokeDock.Core.Models;

namespace PokeDock.Windows;

public sealed class GameSession
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private JsonGameStateStore? _store;
    private int? _knownEncounterId;

    public IReadOnlyList<PokemonSpecies> Catalog { get; private set; } = [];
    public GameEngine Game { get; private set; } = null!;
    public bool IsInitialized { get; private set; }

    public event EventHandler? Changed;
    public event EventHandler<PokemonEncounterEventArgs>? EncounterAppeared;

    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        var catalogPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Data", "pokemon.json");
        await using var catalogStream = File.OpenRead(catalogPath);
        Catalog = PokemonCatalog.Load(catalogStream);

        var statePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PokeDock",
            "Game",
            "game-state.json");
        var durableBackupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PokeDock",
            "Game",
            "game-state.json");
        _store = new JsonGameStateStore(statePath, durableBackupPath);
        var state = await _store.LoadAsync();
        Game = new GameEngine(Catalog, restoredState: state);
        _knownEncounterId = Game.CurrentEncounter?.Id;
        IsInitialized = true;

        // Rewrites a recovered or older snapshot to every durable location. The
        // files live in the user profile and are not replaced by an app update.
        await SaveAsync();

        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    public void SetEncounterFrequency(EncounterFrequency frequency) =>
        Mutate(() => Game.SetFrequency(frequency));

    public void SetEncountersPaused(bool paused) =>
        Mutate(() => Game.SetPaused(paused));

    public void MarkCurrentEncounterSeen() => Mutate(Game.MarkCurrentEncounterSeen);

    public ThrowResult ThrowPokeball()
    {
        var result = Game.ThrowPokeball();
        PublishChange();
        return result;
    }

    public void RunFromEncounter() => Mutate(Game.RunFromEncounter);

    public void DismissEncounterResult() => Mutate(Game.DismissEncounterResult);

    public async Task SaveAsync()
    {
        var store = _store;
        if (!IsInitialized || store is null)
        {
            return;
        }

        await _saveGate.WaitAsync();
        try
        {
            await store.SaveAsync(Game.Snapshot);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public Task ExportCollectionAsync(string path) =>
        PokemonCollectionTransferStore.ExportAsync(path, Game.Snapshot.Progress);

    public async Task<int> ImportCollectionAsync(string path)
    {
        var imported = await PokemonCollectionTransferStore.ImportAsync(path);
        var before = Game.CaughtCount;
        Game.MergeProgress(imported);
        PublishChange();
        await SaveAsync();
        return Math.Max(0, Game.CaughtCount - before);
    }

    private void Timer_Tick(object? sender, object args)
    {
        var before = Game.Snapshot;
        Game.Tick();
        var after = Game.Snapshot;
        if (before != after)
        {
            PublishChange();
        }
        else
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Mutate(Action mutation)
    {
        mutation();
        PublishChange();
    }

    private void PublishChange()
    {
        var encounter = Game.CurrentEncounter;
        if (encounter is not null && encounter.Id != _knownEncounterId)
        {
            EncounterAppeared?.Invoke(this, new PokemonEncounterEventArgs(encounter));
        }
        _knownEncounterId = encounter?.Id;
        Changed?.Invoke(this, EventArgs.Empty);
        _ = SaveAsync();
    }
}

public sealed class PokemonEncounterEventArgs(PokemonSpecies species) : EventArgs
{
    public PokemonSpecies Species { get; } = species;
}
