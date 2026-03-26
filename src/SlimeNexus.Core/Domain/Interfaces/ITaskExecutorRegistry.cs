namespace SlimeNexus.Core.Domain.Interfaces;

/// <summary>
/// Registry for managing multiple task executors.
/// Implements the Strategy pattern to route tasks to appropriate executors.
/// </summary>
public interface ITaskExecutorRegistry
{
    /// <summary>
    /// Registers a task executor for dependency injection.
    /// </summary>
    /// <param name="executor">The executor instance to register.</param>
    void Register(ITaskExecutor executor);

    /// <summary>
    /// Gets the appropriate executor for a given task type.
    /// </summary>
    /// <param name="taskType">The task type identifier.</param>
    /// <returns>The executor, or null if no executor supports this task type.</returns>
    ITaskExecutor? GetExecutor(string taskType);

    /// <summary>
    /// Gets all registered executors.
    /// </summary>
    IReadOnlyList<ITaskExecutor> GetAll();

    /// <summary>
    /// Gets all supported task types across all registered executors.
    /// </summary>
    IReadOnlyList<string> GetSupportedTaskTypes();
}
