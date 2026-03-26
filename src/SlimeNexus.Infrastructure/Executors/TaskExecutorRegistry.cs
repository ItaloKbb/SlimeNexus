using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.Infrastructure.Executors;

/// <summary>
/// Registry for managing and routing to appropriate task executors.
/// Implements the Strategy pattern for task execution.
/// </summary>
public sealed class TaskExecutorRegistry : ITaskExecutorRegistry
{
    private readonly List<ITaskExecutor> _executors = [];
    private readonly object _lock = new();

    public TaskExecutorRegistry() { }

    public TaskExecutorRegistry(IEnumerable<ITaskExecutor> executors)
    {
        foreach (var executor in executors)
        {
            Register(executor);
        }
    }

    /// <inheritdoc />
    public void Register(ITaskExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);

        lock (_lock)
        {
            // Avoid duplicate registrations
            if (!_executors.Contains(executor))
            {
                _executors.Add(executor);
            }
        }
    }

    /// <inheritdoc />
    public ITaskExecutor? GetExecutor(string taskType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskType);

        lock (_lock)
        {
            return _executors.FirstOrDefault(e => e.CanExecute(taskType));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ITaskExecutor> GetAll()
    {
        lock (_lock)
        {
            return [.. _executors];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSupportedTaskTypes()
    {
        lock (_lock)
        {
            return _executors
                .SelectMany(e => e.SupportedTaskTypes)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order()
                .ToList();
        }
    }
}
