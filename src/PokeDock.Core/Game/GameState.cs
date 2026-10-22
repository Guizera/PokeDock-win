using PokeDock.Core.Models;

namespace PokeDock.Core.Game;

public enum EncounterEnding
{
    Caught,
    Fled,
    Ran,
    Expired
}

public enum ThrowResult
{
    Caught,
    BrokeFree,
    Fled,
    NoBalls,
    Unavailable
}

public sealed record EncounterSummary(int SpeciesId, EncounterEnding Ending);

public sealed record GameState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Dictionary<int, PokemonProgress> Progress { get; init; } = [];
    public int? ActiveEncounterId { get; init; }
    public DateTimeOffset? EncounterEndsAt { get; init; }
    public DateTimeOffset? NextEncounterAt { get; init; }
    public EncounterSummary? LastEncounter { get; init; }
    public int EncounterAttempts { get; init; }
    public bool ActiveEncounterSeen { get; init; }
    public int Pokeballs { get; init; } = GameEngine.MaximumPokeballs;
    public DateTimeOffset? PokeballsRefillAt { get; init; }
    public bool IsPaused { get; init; }
    public EncounterFrequency Frequency { get; init; } = EncounterFrequency.Normal;
}

