namespace PokeDock.Core.Models;

public enum PokemonRarity
{
    Common,
    Uncommon,
    Rare,
    Special
}

public static class PokemonRarityRules
{
    public static double BaseCaptureChance(this PokemonRarity rarity) => rarity switch
    {
        PokemonRarity.Common => 0.72,
        PokemonRarity.Uncommon => 0.50,
        PokemonRarity.Rare => 0.30,
        PokemonRarity.Special => 0.16,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity))
    };

    public static double BaseFleeChance(this PokemonRarity rarity) => rarity switch
    {
        PokemonRarity.Common => 0.08,
        PokemonRarity.Uncommon => 0.16,
        PokemonRarity.Rare => 0.28,
        PokemonRarity.Special => 0.42,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity))
    };
}

