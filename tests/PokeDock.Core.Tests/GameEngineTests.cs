using PokeDock.Core.Game;
using PokeDock.Core.Models;

namespace PokeDock.Core.Tests;

[TestClass]
public sealed class GameEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void NewGameSchedulesFirstEncounterBetweenTwoAndFourMinutes()
    {
        var clock = new TestClock(Start);
        var engine = CreateEngine(clock, new SequenceRandom(0.5));

        Assert.IsNotNull(engine.Snapshot.NextEncounterAt);
        Assert.IsTrue(engine.Snapshot.NextEncounterAt >= Start.AddMinutes(2));
        Assert.IsTrue(engine.Snapshot.NextEncounterAt <= Start.AddMinutes(4));
        Assert.AreEqual(GameEngine.MaximumPokeballs, engine.Snapshot.Pokeballs);
    }

    [TestMethod]
    public void FirstFailedThrowNeverLetsPokemonFlee()
    {
        var engine = CreateEngine(new TestClock(Start), new SequenceRandom(0.5, 0, 0, 0.99));
        engine.SimulateEncounter();

        var result = engine.ThrowPokeball();

        Assert.AreEqual(ThrowResult.BrokeFree, result);
        Assert.IsNotNull(engine.CurrentEncounter);
        Assert.AreEqual(1, engine.Snapshot.EncounterAttempts);
        Assert.AreEqual(9, engine.Snapshot.Pokeballs);
    }

    [TestMethod]
    public void SecondFailedThrowMayLetPokemonFlee()
    {
        var engine = CreateEngine(new TestClock(Start), new SequenceRandom(0.5, 0, 0, 0.99, 0.99, 0));
        engine.SimulateEncounter();
        Assert.AreEqual(ThrowResult.BrokeFree, engine.ThrowPokeball());

        var result = engine.ThrowPokeball();

        Assert.AreEqual(ThrowResult.Fled, result);
        Assert.IsNull(engine.CurrentEncounter);
        Assert.AreEqual(EncounterEnding.Fled, engine.Snapshot.LastEncounter?.Ending);
    }

    [TestMethod]
    public void CaptureUpdatesCollectionAndSchedulesNextEncounter()
    {
        var engine = CreateEngine(new TestClock(Start), new SequenceRandom(0.5, 0, 0, 0));
        engine.SimulateEncounter();
        engine.MarkCurrentEncounterSeen();
        var speciesId = engine.CurrentEncounter!.Id;

        var result = engine.ThrowPokeball();

        Assert.AreEqual(ThrowResult.Caught, result);
        Assert.AreEqual(1, engine.Snapshot.Progress[speciesId].SeenCount);
        Assert.AreEqual(1, engine.Snapshot.Progress[speciesId].CaughtCount);
        Assert.IsNotNull(engine.Snapshot.NextEncounterAt);
    }

    [TestMethod]
    public void PokeballsRefillTwoHoursAfterFirstUse()
    {
        var clock = new TestClock(Start);
        var engine = CreateEngine(clock, new SequenceRandom(0.5, 0, 0, 0.99));
        engine.SimulateEncounter();
        engine.ThrowPokeball();

        clock.Now = Start.AddHours(2);
        engine.Tick();

        Assert.AreEqual(GameEngine.MaximumPokeballs, engine.Snapshot.Pokeballs);
        Assert.IsNull(engine.Snapshot.PokeballsRefillAt);
    }

    [TestMethod]
    public void PausingExpiresActiveEncounterAndClearsSchedule()
    {
        var engine = CreateEngine(new TestClock(Start), new SequenceRandom(0.5, 0));
        engine.SimulateEncounter();

        engine.SetPaused(true);

        Assert.IsNull(engine.CurrentEncounter);
        Assert.IsNull(engine.Snapshot.NextEncounterAt);
        Assert.AreEqual(EncounterEnding.Expired, engine.Snapshot.LastEncounter?.Ending);
    }

    [TestMethod]
    public void ChangingFrequencyReschedulesTheNextEncounter()
    {
        var clock = new TestClock(Start);
        var engine = CreateEngine(clock, new SequenceRandom(0.5, 0.5));

        engine.SetFrequency(EncounterFrequency.Calm);

        Assert.AreEqual(EncounterFrequency.Calm, engine.Snapshot.Frequency);
        Assert.IsNotNull(engine.Snapshot.NextEncounterAt);
        Assert.IsTrue(engine.Snapshot.NextEncounterAt >= Start.AddMinutes(45));
        Assert.IsTrue(engine.Snapshot.NextEncounterAt <= Start.AddMinutes(70));
    }

    [TestMethod]
    public void RestoreRepairsIncompletePokeballState()
    {
        var state = new GameState { Pokeballs = 3, PokeballsRefillAt = null, IsPaused = true };
        var catalog = new[]
        {
            new PokemonSpecies(1, "Bulbasaur", PokemonRarity.Common, [PokemonType.Grass, PokemonType.Poison])
        };

        var engine = new GameEngine(catalog, new TestClock(Start), new SequenceRandom(), state);

        Assert.AreEqual(GameEngine.MaximumPokeballs, engine.Snapshot.Pokeballs);
    }

    [TestMethod]
    public void ImportedCollectionMergesWithoutRegressingExistingProgress()
    {
        var catalog = new[]
        {
            new PokemonSpecies(1, "Bulbasaur", PokemonRarity.Common, [PokemonType.Grass]),
            new PokemonSpecies(25, "Pikachu", PokemonRarity.Uncommon, [PokemonType.Electric])
        };
        var restored = new GameState
        {
            IsPaused = true,
            Progress = new Dictionary<int, PokemonProgress>
            {
                [1] = new PokemonProgress(SeenCount: 5, CaughtCount: 2, LastSeen: Start)
            }
        };
        var engine = new GameEngine(catalog, new TestClock(Start), new SequenceRandom(), restored);

        engine.MergeProgress(new Dictionary<int, PokemonProgress>
        {
            [1] = new PokemonProgress(SeenCount: 4, CaughtCount: 1, LastSeen: Start.AddDays(1)),
            [25] = new PokemonProgress(SeenCount: 1, CaughtCount: 1, LastSeen: Start),
            [999] = new PokemonProgress(SeenCount: 9, CaughtCount: 9)
        });

        Assert.AreEqual(5, engine.Snapshot.Progress[1].SeenCount);
        Assert.AreEqual(2, engine.Snapshot.Progress[1].CaughtCount);
        Assert.AreEqual(Start.AddDays(1), engine.Snapshot.Progress[1].LastSeen);
        Assert.AreEqual(1, engine.Snapshot.Progress[25].CaughtCount);
        Assert.IsFalse(engine.Snapshot.Progress.ContainsKey(999));
    }

    private static GameEngine CreateEngine(TestClock clock, SequenceRandom random)
    {
        var catalog = new[]
        {
            new PokemonSpecies(1, "Bulbasaur", PokemonRarity.Common, [PokemonType.Grass, PokemonType.Poison])
        };
        return new GameEngine(catalog, clock, random);
    }

    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now { get; set; } = now;
    }

    private sealed class SequenceRandom(params double[] values) : IRandomSource
    {
        private readonly Queue<double> _values = new(values);

        public double NextDouble() => _values.Count > 0 ? _values.Dequeue() : 0.5;
    }
}

