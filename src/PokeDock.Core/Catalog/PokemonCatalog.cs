using System.Text.Json;
using System.Text.Json.Serialization;
using PokeDock.Core.Models;

namespace PokeDock.Core.Catalog;

public static class PokemonCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static IReadOnlyList<PokemonSpecies> Load(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var species = JsonSerializer.Deserialize<List<PokemonSpecies>>(json, SerializerOptions)
            ?? throw new InvalidDataException("The Pokémon catalog is empty or invalid.");

        if (species.Count != 151 || species.Select(item => item.Id).Distinct().Count() != 151)
        {
            throw new InvalidDataException("The Pokémon catalog must contain 151 unique species.");
        }

        for (var id = 1; id <= 151; id++)
        {
            if (species[id - 1].Id != id)
            {
                throw new InvalidDataException("The Pokémon catalog must be ordered from ID 1 through 151.");
            }
        }

        return species;
    }
}

