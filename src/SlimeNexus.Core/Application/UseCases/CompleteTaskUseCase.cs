using SlimeNexus.Core.Domain.Entities;
using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.Core.Application.UseCases;

/// <summary>
/// Validates and completes a daily task, applying the appropriate reward to the Slime.
/// This is the core application use case orchestrating the domain objects.
/// </summary>
public sealed class CompleteTaskUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISlimeRepository _slimeRepository;

    public CompleteTaskUseCase(ITaskRepository taskRepository, ISlimeRepository slimeRepository)
    {
        _taskRepository  = taskRepository;
        _slimeRepository = slimeRepository;
    }

    /// <summary>
    /// Marks the specified task as complete and rewards the active Slime.
    /// </summary>
    /// <param name="taskId">The task to complete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated Slime entity.</returns>
    public async Task<Slime> ExecuteAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task '{taskId}' not found.");

        if (task.IsCompleted)
            throw new InvalidOperationException($"Task '{taskId}' is already completed.");

        task.Complete();
        await _taskRepository.UpdateAsync(task, cancellationToken);

        var slime = await _slimeRepository.GetCurrentAsync(cancellationToken);
        slime.ApplyTaskReward(task.HappinessReward, task.EnergyReward);
        await _slimeRepository.SaveAsync(slime, cancellationToken);

        return slime;
    }
}
