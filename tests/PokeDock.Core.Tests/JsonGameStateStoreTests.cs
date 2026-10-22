using PokeDock.Application.Persistence;
using PokeDock.Core.Game;
using PokeDock.Core.Models;

namespace PokeDock.Core.Tests;

[TestClass]
public sealed class JsonGameStateStoreTests
{
    [TestMethod]
    public async Task SaveAndLoadPreserveProgress()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PokeDock.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "state.json");
        var store = new JsonGameStateStore(path);
        var state = new GameState
        {
            Pokeballs = 7,
            Frequency = EncounterFrequency.Lively,
            Progress = new Dictionary<int, PokemonProgress>
            {
                [25] = new PokemonProgress(SeenCount: 3, CaughtCount: 1)
            }
        };

        await store.SaveAsync(state);
        var restored = await store.LoadAsync();

        Assert.IsNotNull(restored);
        Assert.AreEqual(7, restored.Pokeballs);
        Assert.AreEqual(EncounterFrequency.Lively, restored.Frequency);
        Assert.AreEqual(3, restored.Progress[25].SeenCount);
        Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public async Task LoadRecoversFromBackupWhenPrimaryFileIsCorrupted()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PokeDock.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "state.json");
        var store = new JsonGameStateStore(path);
        await store.SaveAsync(StateWithProgress(25, seen: 4, caught: 2));

        await File.WriteAllTextAsync(path, "{ not valid json");
        var restored = await new JsonGameStateStore(path).LoadAsync();

        Assert.IsNotNull(restored);
        Assert.AreEqual(4, restored.Progress[25].SeenCount);
        Assert.AreEqual(2, restored.Progress[25].CaughtCount);
        Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public async Task DurableBackupPreservesProgressWhenPrimaryStoragePathChanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PokeDock.Tests", Guid.NewGuid().ToString("N"));
        var firstPrimary = Path.Combine(directory, "version-one", "state.json");
        var updatedPrimary = Path.Combine(directory, "version-two", "state.json");
        var durableBackup = Path.Combine(directory, "user-profile", "state.json");
        await new JsonGameStateStore(firstPrimary, durableBackup)
            .SaveAsync(StateWithProgress(133, seen: 3, caught: 1));

        var restored = await new JsonGameStateStore(updatedPrimary, durableBackup).LoadAsync();

        Assert.IsNotNull(restored);
        Assert.AreEqual(3, restored.Progress[133].SeenCount);
        Assert.AreEqual(1, restored.Progress[133].CaughtCount);
        Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public async Task LoadMergesProgressWithoutRegressingCaughtPokemon()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PokeDock.Tests", Guid.NewGuid().ToString("N"));
        var primary = Path.Combine(directory, "local", "state.json");
        var durableBackup = Path.Combine(directory, "roaming", "state.json");
        await new JsonGameStateStore(primary).SaveAsync(StateWithProgress(7, seen: 5, caught: 2));
        await new JsonGameStateStore(durableBackup).SaveAsync(StateWithProgress(7, seen: 6, caught: 1));

        var restored = await new JsonGameStateStore(primary, durableBackup).LoadAsync();

        Assert.IsNotNull(restored);
        Assert.AreEqual(6, restored.Progress[7].SeenCount);
        Assert.AreEqual(2, restored.Progress[7].CaughtCount);
        Directory.Delete(directory, recursive: true);
    }

    private static GameState StateWithProgress(int speciesId, int seen, int caught) => new()
    {
        Progress = new Dictionary<int, PokemonProgress>
        {
            [speciesId] = new PokemonProgress(seen, caught, DateTimeOffset.UtcNow)
        }
    };
}

