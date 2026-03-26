using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.Api.Services;

/// <summary>
/// Background service that processes tasks from the queue.
/// </summary>
public sealed class TaskProcessorService : BackgroundService
{
    private readonly TaskQueue _taskQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaskProcessorService> _logger;

    private readonly TimeSpan _pollingInterval = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(10);

    public TaskProcessorService(
        TaskQueue taskQueue,
        IServiceProvider serviceProvider,
        ILogger<TaskProcessorService> logger)
    {
        _taskQueue = taskQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task Processor Service started");

        var lastCleanup = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Process queued tasks
                if (_taskQueue.TryDequeue(out var task) && task is not null)
                {
                    await ProcessTaskAsync(task, stoppingToken);
                }
                else
                {
                    // No tasks, wait before polling again
                    await Task.Delay(_pollingInterval, stoppingToken);
                }

                // Periodic cleanup
                if (DateTime.UtcNow - lastCleanup > _cleanupInterval)
                {
                    var removed = _taskQueue.Cleanup();
                    if (removed > 0)
                    {
                        _logger.LogDebug("Cleaned up {Count} old tasks", removed);
                    }
                    lastCleanup = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in task processor loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Task Processor Service stopped");
    }

    private async Task ProcessTaskAsync(QueuedTask task, CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Processing queued task {RequestId}: {TaskType}",
            task.Metadata.RequestId, task.Metadata.TaskType);

        try
        {
            // Create a scoped service provider to get fresh instances
            using var scope = _serviceProvider.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IAiOrchestrator>();

            // Create a linked cancellation token with task timeout
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(task.Metadata.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);

            // Update progress periodically (simulate for now)
            var progressTask = UpdateProgressAsync(task.Metadata.RequestId, linkedCts.Token);

            // Process the task
            var result = await orchestrator.ProcessTaskAsync(task.Metadata, linkedCts.Token);

            // Complete the task
            _taskQueue.Complete(task.Metadata.RequestId, result);

            _logger.LogInformation(
                "Task {RequestId} completed: Success={Success}, XP={XP}",
                task.Metadata.RequestId, result.IsSuccess, result.XpEarned);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Task {RequestId} was cancelled", task.Metadata.RequestId);
            
            _taskQueue.Complete(task.Metadata.RequestId, Core.Domain.ValueObjects.ValidationResult.Failure(
                task.Metadata.RequestId,
                "Task was cancelled or timed out",
                Core.Domain.Enums.ErrorCodes.TaskCancelled));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {RequestId} failed", task.Metadata.RequestId);
            
            _taskQueue.Complete(task.Metadata.RequestId, Core.Domain.ValueObjects.ValidationResult.Failure(
                task.Metadata.RequestId,
                $"Task failed: {ex.Message}",
                Core.Domain.Enums.ErrorCodes.ExecutionFailed,
                ex.ToString()));
        }
    }

    private async Task UpdateProgressAsync(Guid requestId, CancellationToken cancellationToken)
    {
        // Simulate progress updates
        var progress = 0;
        while (progress < 90 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
            progress += 10;
            _taskQueue.UpdateProgress(requestId, progress);
        }
    }
}
