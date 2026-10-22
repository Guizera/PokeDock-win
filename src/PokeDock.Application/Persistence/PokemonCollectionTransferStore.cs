using System.Text.Json;
using PokeDock.Core.Models;

namespace PokeDock.Application.Persistence;

public sealed record PokemonCollectionTransfer(
    int SchemaVersion,
    DateTimeOffset ExportedAt,
    Dictionary<int, PokemonProgress> Progress)
{
    public const int CurrentSchemaVersion = 1;
}

public static class PokemonCollectionTransferStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task ExportAsync(
        string path,
        IReadOnlyDictionary<int, PokemonProgress> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(progress);
        var transfer = new PokemonCollectionTransfer(
            PokemonCollectionTransfer.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new Dictionary<int, PokemonProgress>(progress));

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, transfer, SerializerOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<IReadOnlyDictionary<int, PokemonProgress>> ImportAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var stream = File.OpenRead(path);
        var transfer = await JsonSerializer.DeserializeAsync<PokemonCollectionTransfer>(
            stream,
            SerializerOptions,
            cancellationToken);
        if (transfer is null || transfer.SchemaVersion is < 1 or > PokemonCollectionTransfer.CurrentSchemaVersion)
        {
            throw new InvalidDataException("Unsupported PokeDock collection file.");
        }

        return transfer.Progress ?? [];
    }
}
