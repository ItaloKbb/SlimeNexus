using System.Collections.Concurrent;
using SlimeNexus.Api.Contracts;
using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.Api.Services;

/// <summary>
/// In-memory queue for async task processing.
/// For production, consider using a persistent queue like Redis or SQL.
/// </summary>
public sealed class TaskQueue
{
    private readonly ConcurrentQueue<QueuedTask> _queue = new();
    private readonly ConcurrentDictionary<Guid, QueuedTask> _tasks = new();

    /// <summary>
    /// Enqueues a task for async processing.
    /// </summary>
    public Guid Enqueue(TaskMetadata metadata)
    {
        var task = new QueuedTask
        {
            Metadata = metadata,
            Status = TaskStatus.Queued,
            QueuedAt = DateTimeOffset.UtcNow
        };

        _tasks[metadata.RequestId] = task;
        _queue.Enqueue(task);

        return metadata.RequestId;
    }

    /// <summary>
    /// Attempts to dequeue a task for processing.
    /// </summary>
    public bool TryDequeue(out QueuedTask? task)
    {
        if (_queue.TryDequeue(out task))
        {
            task.Status = TaskStatus.Processing;
            task.StartedAt = DateTimeOffset.UtcNow;
            return true;
        }

        task = null;
        return false;
    }

    /// <summary>
    /// Gets the status of a task.
    /// </summary>
    public TaskStatusResponse? GetStatus(Guid requestId)
    {
        if (!_tasks.TryGetValue(requestId, out var task))
            return null;

        return new TaskStatusResponse
        {
            RequestId = requestId,
            Status = task.Status.ToString().ToLowerInvariant(),
            Progress = task.Progress,
            Result = task.Result is not null 
                ? ValidationResultResponse.FromDomain(task.Result) 
                : null,
            QueuedAt = task.QueuedAt,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt
        };
    }

    /// <summary>
    /// Updates task progress.
    /// </summary>
    public void UpdateProgress(Guid requestId, int progress)
    {
        if (_tasks.TryGetValue(requestId, out var task))
        {
            task.Progress = Math.Clamp(progress, 0, 100);
        }
    }

    /// <summary>
    /// Marks a task as completed.
    /// </summary>
    public void Complete(Guid requestId, ValidationResult result)
    {
        if (_tasks.TryGetValue(requestId, out var task))
        {
            task.Status = result.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed;
            task.Result = result;
            task.CompletedAt = DateTimeOffset.UtcNow;
            task.Progress = 100;
        }
    }

    /// <summary>
    /// Gets the number of queued tasks.
    /// </summary>
    public int QueuedCount => _queue.Count;

    /// <summary>
    /// Cleans up old completed tasks (older than 1 hour).
    /// </summary>
    public int Cleanup()
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        var toRemove = _tasks
            .Where(kvp => kvp.Value.CompletedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            _tasks.TryRemove(id, out _);
        }

        return toRemove.Count;
    }
}

/// <summary>
/// Represents a task in the queue.
/// </summary>
public sealed class QueuedTask
{
    public required TaskMetadata Metadata { get; init; }
    public TaskStatus Status { get; set; }
    public int Progress { get; set; }
    public ValidationResult? Result { get; set; }
    public DateTimeOffset QueuedAt { get; init; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>
/// Task processing status.
/// </summary>
public enum TaskStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}
