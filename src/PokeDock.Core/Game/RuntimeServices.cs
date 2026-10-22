namespace PokeDock.Core.Game;

public interface IClock
{
    DateTimeOffset Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}

public interface IRandomSource
{
    double NextDouble();
}

public sealed class SystemRandomSource : IRandomSource
{
    public double NextDouble() => Random.Shared.NextDouble();
}

