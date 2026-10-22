namespace PokeDock.Core.Models;

public sealed record PokemonSpecies(
    int Id,
    string Name,
    PokemonRarity Rarity,
    IReadOnlyList<PokemonType> Types,
    double EncounterWeight = 1);

public sealed record PokemonProgress(
    int SeenCount = 0,
    int CaughtCount = 0,
    DateTimeOffset? LastSeen = null);

