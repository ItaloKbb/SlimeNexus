using SlimeNexus.Core.Domain.Entities;

namespace SlimeNexus.Core.Domain.Interfaces;

/// <summary>
/// Repository contract for persisting and querying Slime state.
/// Implemented in SlimeNexus.Infrastructure (e.g., SQLite or JSON file-based store).
/// </summary>
public interface ISlimeRepository
{
    Task<Slime?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Slime> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(Slime slime, CancellationToken cancellationToken = default);
}
