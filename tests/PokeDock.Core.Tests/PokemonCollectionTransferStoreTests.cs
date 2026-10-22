using PokeDock.Application.Persistence;
using PokeDock.Core.Models;

namespace PokeDock.Core.Tests;

[TestClass]
public sealed class PokemonCollectionTransferStoreTests
{
    [TestMethod]
    public async Task ExportAndImportPreserveCollectionProgress()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PokeDock.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "collection.pokedock");
        var lastSeen = new DateTimeOffset(2026, 10, 21, 12, 30, 0, TimeSpan.Zero);

        await PokemonCollectionTransferStore.ExportAsync(path, new Dictionary<int, PokemonProgress>
        {
            [94] = new PokemonProgress(SeenCount: 7, CaughtCount: 3, LastSeen: lastSeen)
        });
        var restored = await PokemonCollectionTransferStore.ImportAsync(path);

        Assert.AreEqual(7, restored[94].SeenCount);
        Assert.AreEqual(3, restored[94].CaughtCount);
        Assert.AreEqual(lastSeen, restored[94].LastSeen);
        Directory.Delete(directory, recursive: true);
    }
}
