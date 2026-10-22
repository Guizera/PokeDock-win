using PokeDock.Core.Catalog;
using PokeDock.Core.Models;

namespace PokeDock.Core.Tests;

[TestClass]
public sealed class PokemonCatalogTests
{
    [TestMethod]
    public void GeneratedCatalogContainsTheOriginal151Species()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "pokemon.json");
        using var stream = File.OpenRead(path);

        var catalog = PokemonCatalog.Load(stream);

        Assert.HasCount(151, catalog);
        Assert.AreEqual("Bulbasaur", catalog[0].Name);
        Assert.AreEqual(PokemonRarity.Uncommon, catalog[24].Rarity);
        CollectionAssert.AreEqual(
            new[] { PokemonType.Electric },
            catalog[24].Types.ToArray());
        Assert.AreEqual("Mew", catalog[^1].Name);
        Assert.AreEqual(PokemonRarity.Special, catalog[^1].Rarity);
    }
}
