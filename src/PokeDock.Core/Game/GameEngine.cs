using PokeDock.Core.Models;

namespace PokeDock.Core.Game;

public sealed class GameEngine
{
    public const int MaximumPokeballs = 10;
    public static readonly TimeSpan RefillInterval = TimeSpan.FromHours(2);
    public static readonly TimeSpan EncounterDuration = TimeSpan.FromMinutes(5);

    private readonly IReadOnlyList<PokemonSpecies> _catalog;
    private readonly IReadOnlyDictionary<int, PokemonSpecies> _speciesById;
    private readonly IClock _clock;
    private readonly IRandomSource _random;
    private GameState _state;

    public GameEngine(
        IReadOnlyList<PokemonSpecies> catalog,
        IClock? clock = null,
        IRandomSource? random = null,
        GameState? restoredState = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Count == 0)
        {
            throw new ArgumentException("The Pokémon catalog cannot be empty.", nameof(catalog));
        }

        _catalog = catalog;
        _speciesById = catalog.ToDictionary(species => species.Id);
        _clock = clock ?? new SystemClock();
        _random = random ?? new SystemRandomSource();
        _state = Normalize(restoredState ?? new GameState());

        if (restoredState is null && !_state.IsPaused)
        {
            ScheduleNextEncounter(initial: true);
        }
        else
        {
            Tick();
            if (!_state.IsPaused && _state.ActiveEncounterId is null && _state.NextEncounterAt is null)
            {
                ScheduleNextEncounter(initial: false);
            }
        }
    }

    public GameState Snapshot => _state with { Progress = new Dictionary<int, PokemonProgress>(_state.Progress) };
    public PokemonSpecies? CurrentEncounter => _state.ActiveEncounterId is int id ? _speciesById.GetValueOrDefault(id) : null;
    public int SeenCount => _state.Progress.Values.Count(progress => progress.SeenCount > 0);
    public int CaughtCount => _state.Progress.Values.Count(progress => progress.CaughtCount > 0);
    public int DuplicateCount => _state.Progress.Values.Sum(progress => Math.Max(0, progress.CaughtCount - 1));

    public PokemonProgress ProgressFor(PokemonSpecies species) =>
        _state.Progress.GetValueOrDefault(species.Id) ?? new PokemonProgress();

    public double CaptureChance(PokemonSpecies species) =>
        Math.Min(0.90, species.Rarity.BaseCaptureChance() + (_state.EncounterAttempts * 0.08));

    public void Tick()
    {
        RefreshPokeballs();
        var now = _clock.Now;

        if (_state.EncounterEndsAt is { } endsAt && now >= endsAt)
        {
            FinishEncounter(EncounterEnding.Expired);
        }

        if (!_state.IsPaused && _state.ActiveEncounterId is null && _state.NextEncounterAt is { } next && now >= next)
        {
            SpawnEncounter();
        }
    }

    public void SetPaused(bool paused)
    {
        if (_state.IsPaused == paused)
        {
            return;
        }

        _state = _state with { IsPaused = paused };
        if (paused)
        {
            if (_state.ActiveEncounterId is not null)
            {
                FinishEncounter(EncounterEnding.Expired);
            }

            _state = _state with { NextEncounterAt = null };
        }
        else if (_state.ActiveEncounterId is null)
        {
            ScheduleNextEncounter(initial: false);
        }
    }

    public void SetFrequency(EncounterFrequency frequency)
    {
        _state = _state with { Frequency = frequency };
        if (!_state.IsPaused && _state.ActiveEncounterId is null)
        {
            ScheduleNextEncounter(initial: false);
        }
    }

    public void SimulateEncounter()
    {
        if (_state.IsPaused)
        {
            return;
        }

        if (_state.ActiveEncounterId is not null)
        {
            FinishEncounter(EncounterEnding.Expired, scheduleNext: false);
        }

        SpawnEncounter();
    }

    public void MarkCurrentEncounterSeen()
    {
        var species = CurrentEncounter;
        if (species is null || _state.ActiveEncounterSeen)
        {
            return;
        }

        var progress = new Dictionary<int, PokemonProgress>(_state.Progress);
        var current = progress.GetValueOrDefault(species.Id) ?? new PokemonProgress();
        progress[species.Id] = current with
        {
            SeenCount = current.SeenCount + 1,
            LastSeen = _clock.Now
        };
        _state = _state with { Progress = progress, ActiveEncounterSeen = true };
    }

    public ThrowResult ThrowPokeball()
    {
        RefreshPokeballs();
        var species = CurrentEncounter;
        if (species is null)
        {
            return ThrowResult.Unavailable;
        }

        if (_state.EncounterEndsAt is { } endsAt && _clock.Now >= endsAt)
        {
            FinishEncounter(EncounterEnding.Expired);
            return ThrowResult.Unavailable;
        }

        if (_state.Pokeballs <= 0)
        {
            return ThrowResult.NoBalls;
        }

        _state = _state with
        {
            Pokeballs = _state.Pokeballs - 1,
            PokeballsRefillAt = _state.PokeballsRefillAt ?? _clock.Now.Add(RefillInterval)
        };

        if (_random.NextDouble() < CaptureChance(species))
        {
            var progress = new Dictionary<int, PokemonProgress>(_state.Progress);
            var current = progress.GetValueOrDefault(species.Id) ?? new PokemonProgress();
            progress[species.Id] = current with { CaughtCount = current.CaughtCount + 1 };
            _state = _state with { Progress = progress };
            FinishEncounter(EncounterEnding.Caught);
            return ThrowResult.Caught;
        }

        var attempts = _state.EncounterAttempts + 1;
        _state = _state with { EncounterAttempts = attempts };
        var fleeChance = Math.Min(0.65, species.Rarity.BaseFleeChance() + (Math.Max(0, attempts - 2) * 0.05));
        if (attempts >= 2 && _random.NextDouble() < fleeChance)
        {
            FinishEncounter(EncounterEnding.Fled);
            return ThrowResult.Fled;
        }

        return ThrowResult.BrokeFree;
    }

    public void RunFromEncounter()
    {
        if (_state.ActiveEncounterId is not null)
        {
            FinishEncounter(EncounterEnding.Ran);
        }
    }

    public void DismissEncounterResult() => _state = _state with { LastEncounter = null };

    public void MergeProgress(IReadOnlyDictionary<int, PokemonProgress> importedProgress)
    {
        ArgumentNullException.ThrowIfNull(importedProgress);
        var progress = new Dictionary<int, PokemonProgress>(_state.Progress);
        foreach (var (speciesId, candidate) in importedProgress)
        {
            if (!_speciesById.ContainsKey(speciesId)) continue;
            var current = progress.GetValueOrDefault(speciesId) ?? new PokemonProgress();
            progress[speciesId] = new PokemonProgress(
                Math.Max(current.SeenCount, candidate.SeenCount),
                Math.Max(current.CaughtCount, candidate.CaughtCount),
                Latest(current.LastSeen, candidate.LastSeen));
        }

        _state = _state with { Progress = progress };
    }

    private static DateTimeOffset? Latest(DateTimeOffset? first, DateTimeOffset? second) =>
        first is null ? second : second is null ? first : first > second ? first : second;

    private GameState Normalize(GameState state)
    {
        var progress = state.Progress
            .Where(pair => _speciesById.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        int? activeId = state.ActiveEncounterId is int id
            && state.EncounterEndsAt is not null
            && _speciesById.ContainsKey(id)
                ? id
                : null;
        var balls = Math.Clamp(state.Pokeballs, 0, MaximumPokeballs);
        var refillAt = state.PokeballsRefillAt;
        if (balls < MaximumPokeballs && refillAt is null)
        {
            balls = MaximumPokeballs;
        }

        return state with
        {
            SchemaVersion = GameState.CurrentSchemaVersion,
            Progress = progress,
            ActiveEncounterId = activeId,
            EncounterEndsAt = activeId is null ? null : state.EncounterEndsAt,
            EncounterAttempts = activeId is null ? 0 : Math.Max(0, state.EncounterAttempts),
            ActiveEncounterSeen = activeId is not null && state.ActiveEncounterSeen,
            Pokeballs = balls,
            PokeballsRefillAt = balls == MaximumPokeballs ? null : refillAt
        };
    }

    private void ScheduleNextEncounter(bool initial)
    {
        if (_state.IsPaused)
        {
            return;
        }

        var interval = initial
            ? (TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4))
            : _state.Frequency.Interval();
        var seconds = interval.Item1.TotalSeconds
            + ((interval.Item2.TotalSeconds - interval.Item1.TotalSeconds) * _random.NextDouble());
        _state = _state with { NextEncounterAt = _clock.Now.AddSeconds(seconds) };
    }

    private void SpawnEncounter()
    {
        if (_state.IsPaused || _state.ActiveEncounterId is not null)
        {
            return;
        }

        var species = ChooseSpecies();
        _state = _state with
        {
            ActiveEncounterId = species.Id,
            EncounterEndsAt = _clock.Now.Add(EncounterDuration),
            NextEncounterAt = null,
            LastEncounter = null,
            EncounterAttempts = 0,
            ActiveEncounterSeen = false
        };
    }

    private PokemonSpecies ChooseSpecies()
    {
        var tierRoll = _random.NextDouble();
        var rarity = tierRoll < 0.72 ? PokemonRarity.Common
            : tierRoll < 0.92 ? PokemonRarity.Uncommon
            : tierRoll < 0.99 ? PokemonRarity.Rare
            : PokemonRarity.Special;
        var candidates = _catalog.Where(species => species.Rarity == rarity).ToArray();
        if (candidates.Length == 0)
        {
            candidates = _catalog.ToArray();
        }

        var weights = candidates
            .Select(species => species.EncounterWeight * (ProgressFor(species).CaughtCount == 0 ? 1.4 : 1))
            .ToArray();
        var roll = _random.NextDouble() * weights.Sum();
        for (var index = 0; index < candidates.Length; index++)
        {
            roll -= weights[index];
            if (roll < 0)
            {
                return candidates[index];
            }
        }

        return candidates[^1];
    }

    private void FinishEncounter(EncounterEnding ending, bool scheduleNext = true)
    {
        if (_state.ActiveEncounterId is not int speciesId)
        {
            return;
        }

        _state = _state with
        {
            ActiveEncounterId = null,
            EncounterEndsAt = null,
            EncounterAttempts = 0,
            ActiveEncounterSeen = false,
            LastEncounter = new EncounterSummary(speciesId, ending)
        };

        if (scheduleNext && !_state.IsPaused)
        {
            ScheduleNextEncounter(initial: false);
        }
        else
        {
            _state = _state with { NextEncounterAt = null };
        }
    }

    private void RefreshPokeballs()
    {
        if (_state.PokeballsRefillAt is { } refillAt && _clock.Now >= refillAt)
        {
            _state = _state with
            {
                Pokeballs = MaximumPokeballs,
                PokeballsRefillAt = null
            };
        }
    }
}

