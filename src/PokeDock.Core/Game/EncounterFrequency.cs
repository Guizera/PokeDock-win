namespace PokeDock.Core.Game;

public enum EncounterFrequency
{
    Calm,
    Normal,
    Lively
}

public static class EncounterFrequencyRules
{
    public static (TimeSpan Minimum, TimeSpan Maximum) Interval(this EncounterFrequency frequency) => frequency switch
    {
        EncounterFrequency.Calm => (TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(70)),
        EncounterFrequency.Normal => (TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(35)),
        EncounterFrequency.Lively => (TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20)),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency))
    };
}

