using System.Text.Json;
using System.Text.Json.Serialization;
using PokeDock.Core.Game;
using PokeDock.Core.Models;

namespace PokeDock.Application.Persistence;

public sealed class JsonGameStateStore : IGameStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private readonly string _filePath;
    private readonly IReadOnlyList<string> _backupPaths;

    public JsonGameStateStore(string filePath, params string[] durableBackupPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _backupPaths = [filePath + ".bak", .. durableBackupPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Where(path => !string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<GameState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = new List<GameState>();
        foreach (var path in new[] { _filePath }.Concat(_backupPaths))
        {
            var snapshot = await TryLoadAsync(path, cancellationToken);
            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots.Count == 0 ? null : MergeSnapshots(snapshots);
    }

    public async Task SaveAsync(GameState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        await WriteAtomicallyAsync(_filePath, state, cancellationToken);
        foreach (var backupPath in _backupPaths)
        {
            try
            {
                await WriteAtomicallyAsync(backupPath, state, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The primary snapshot is authoritative. A restricted roaming
                // profile must not prevent the game from saving locally.
            }
        }
    }

    private static async Task<GameState?> TryLoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<GameState>(stream, SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static GameState MergeSnapshots(IReadOnlyList<GameState> snapshots)
    {
        var result = snapshots[0];
        var progress = new Dictionary<int, PokemonProgress>();
        foreach (var snapshot in snapshots)
        {
            foreach (var (speciesId, candidate) in snapshot.Progress ?? [])
            {
                if (!progress.TryGetValue(speciesId, out var current))
                {
                    progress[speciesId] = candidate;
                    continue;
                }

                progress[speciesId] = new PokemonProgress(
                    Math.Max(current.SeenCount, candidate.SeenCount),
                    Math.Max(current.CaughtCount, candidate.CaughtCount),
                    Latest(current.LastSeen, candidate.LastSeen));
            }
        }

        return result with { Progress = progress };
    }

    private static DateTimeOffset? Latest(DateTimeOffset? first, DateTimeOffset? second) =>
        first is null ? second : second is null ? first : first > second ? first : second;

    private static async Task WriteAtomicallyAsync(
        string path,
        GameState state,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The state path must include a directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

