using PokeDock.Core.Game;

namespace PokeDock.Application.Persistence;

public interface IGameStateStore
{
    Task<GameState?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(GameState state, CancellationToken cancellationToken = default);
}

