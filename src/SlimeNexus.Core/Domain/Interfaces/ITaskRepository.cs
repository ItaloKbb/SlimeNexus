using SlimeNexus.Core.Domain.Entities;

namespace SlimeNexus.Core.Domain.Interfaces;

/// <summary>
/// Repository contract for daily task management.
/// </summary>
public interface ITaskRepository
{
    Task<IReadOnlyList<DailyTask>> GetTodayTasksAsync(CancellationToken cancellationToken = default);
    Task<DailyTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(DailyTask task, CancellationToken cancellationToken = default);
    Task UpdateAsync(DailyTask task, CancellationToken cancellationToken = default);
}
