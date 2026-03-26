using Microsoft.AspNetCore.Mvc;
using SlimeNexus.Api.Contracts;
using SlimeNexus.Api.Services;
using SlimeNexus.Core.Domain.Enums;
using SlimeNexus.Core.Domain.Interfaces;
using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.Api.Endpoints;

/// <summary>
/// Endpoints for task validation and management.
/// </summary>
public static class TaskEndpoints
{
    /// <summary>
    /// Maps all task-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks")
            .WithTags("Tasks");

        group.MapPost("/validate", ValidateTaskAsync)
            .WithName("ValidateTask")
            .WithSummary("Validates a task and returns XP rewards")
            .Produces<ValidationResultResponse>(StatusCodes.Status200OK)
            .Produces<TaskAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/validate/async", QueueTaskAsync)
            .WithName("QueueTask")
            .WithSummary("Queues a task for async processing")
            .Produces<TaskAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/status/{requestId:guid}", GetTaskStatusAsync)
            .WithName("GetTaskStatus")
            .WithSummary("Gets the status of a queued task")
            .Produces<TaskStatusResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/types", GetSupportedTaskTypes)
            .WithName("GetTaskTypes")
            .WithSummary("Lists all supported task types")
            .Produces<TaskTypesResponse>(StatusCodes.Status200OK);

        return app;
    }

    /// <summary>
    /// POST /api/tasks/validate
    /// Validates a task immediately or queues it if expected to be long-running.
    /// </summary>
    private static async Task<IResult> ValidateTaskAsync(
        [FromBody] TaskMetadataRequest request,
        [FromServices] IAiOrchestrator orchestrator,
        [FromServices] TaskQueue taskQueue,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Validate request
        var validationErrors = ValidateRequest(request);
        if (validationErrors.Count > 0)
        {
            return Results.Problem(
                title: "Validation Failed",
                detail: string.Join("; ", validationErrors),
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Check if orchestrator is ready
        if (!await orchestrator.IsReadyAsync(cancellationToken))
        {
            return Results.Problem(
                title: "Service Unavailable",
                detail: "AI orchestrator is not ready. Ensure Ollama is running.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        // Convert request to domain model
        var metadata = request.ToDomain();

        // Check if task should be processed async (estimated > 30 seconds)
        var estimatedTime = EstimateTaskDuration(metadata.TaskType);
        if (estimatedTime > TimeSpan.FromSeconds(30))
        {
            // Queue for async processing
            var taskId = taskQueue.Enqueue(metadata);
            
            logger.LogInformation(
                "Task {RequestId} queued for async processing (estimated {Duration}s)",
                metadata.RequestId, estimatedTime.TotalSeconds);

            return Results.Accepted(
                uri: $"/api/tasks/status/{metadata.RequestId}",
                value: new TaskAcceptedResponse
                {
                    RequestId = metadata.RequestId,
                    Status = "queued",
                    EstimatedDurationSeconds = (int)estimatedTime.TotalSeconds,
                    StatusUrl = $"/api/tasks/status/{metadata.RequestId}"
                });
        }

        // Process immediately
        logger.LogInformation("Processing task {RequestId} synchronously", metadata.RequestId);

        try
        {
            var result = await orchestrator.ProcessTaskAsync(metadata, cancellationToken);
            return Results.Ok(ValidationResultResponse.FromDomain(result));
        }
        catch (OperationCanceledException)
        {
            return Results.Problem(
                title: "Request Cancelled",
                detail: "The task was cancelled",
                statusCode: StatusCodes.Status499ClientClosedRequest);
        }
    }

    /// <summary>
    /// POST /api/tasks/validate/async
    /// Always queues the task for async processing.
    /// </summary>
    private static IResult QueueTaskAsync(
        [FromBody] TaskMetadataRequest request,
        [FromServices] TaskQueue taskQueue,
        [FromServices] ILogger<Program> logger)
    {
        var validationErrors = ValidateRequest(request);
        if (validationErrors.Count > 0)
        {
            return Results.Problem(
                title: "Validation Failed",
                detail: string.Join("; ", validationErrors),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var metadata = request.ToDomain();
        taskQueue.Enqueue(metadata);

        logger.LogInformation("Task {RequestId} queued for async processing", metadata.RequestId);

        return Results.Accepted(
            uri: $"/api/tasks/status/{metadata.RequestId}",
            value: new TaskAcceptedResponse
            {
                RequestId = metadata.RequestId,
                Status = "queued",
                EstimatedDurationSeconds = (int)EstimateTaskDuration(metadata.TaskType).TotalSeconds,
                StatusUrl = $"/api/tasks/status/{metadata.RequestId}"
            });
    }

    /// <summary>
    /// GET /api/tasks/status/{requestId}
    /// Gets the current status of a queued/processing task.
    /// </summary>
    private static IResult GetTaskStatusAsync(
        Guid requestId,
        [FromServices] TaskQueue taskQueue)
    {
        var status = taskQueue.GetStatus(requestId);
        
        if (status is null)
        {
            return Results.Problem(
                title: "Not Found",
                detail: $"Task {requestId} not found",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(status);
    }

    /// <summary>
    /// GET /api/tasks/types
    /// Lists all supported task types.
    /// </summary>
    private static IResult GetSupportedTaskTypes(
        [FromServices] ITaskExecutorRegistry registry)
    {
        var types = registry.GetSupportedTaskTypes();
        
        return Results.Ok(new TaskTypesResponse
        {
            Types = types.Select(t => new TaskTypeInfo
            {
                Type = t,
                Description = GetTaskTypeDescription(t),
                EstimatedDurationSeconds = (int)EstimateTaskDuration(t).TotalSeconds
            }).ToList()
        });
    }

    #region Helpers

    private static List<string> ValidateRequest(TaskMetadataRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.TaskType))
            errors.Add("task_type is required");

        if (string.IsNullOrWhiteSpace(request.PetState))
            errors.Add("pet_state is required");

        if (!TaskTypes.IsValid(request.TaskType ?? ""))
            errors.Add($"Invalid task_type: {request.TaskType}");

        if (!PetStates.CanPerformTasks(request.PetState ?? ""))
            errors.Add($"Pet cannot perform tasks while {request.PetState}");

        if (request.TimeoutSeconds is < 1 or > 600)
            errors.Add("timeout_seconds must be between 1 and 600");

        return errors;
    }

    private static TimeSpan EstimateTaskDuration(string taskType) => taskType switch
    {
        TaskTypes.CodeReview => TimeSpan.FromSeconds(60),
        TaskTypes.FileCleanup => TimeSpan.FromSeconds(15),
        TaskTypes.GitCommit => TimeSpan.FromSeconds(5),
        TaskTypes.TestRun => TimeSpan.FromSeconds(120),
        TaskTypes.BuildProject => TimeSpan.FromSeconds(90),
        TaskTypes.GenerateDocs => TimeSpan.FromSeconds(45),
        TaskTypes.Refactor => TimeSpan.FromSeconds(90),
        TaskTypes.SecurityScan => TimeSpan.FromSeconds(60),
        TaskTypes.Custom => TimeSpan.FromSeconds(60),
        _ => TimeSpan.FromSeconds(30)
    };

    private static string GetTaskTypeDescription(string taskType) => taskType switch
    {
        TaskTypes.CodeReview => "AI-assisted code review",
        TaskTypes.FileCleanup => "Clean up build artifacts and caches",
        TaskTypes.GitCommit => "Validate Git commit format",
        TaskTypes.TestRun => "Run and validate unit tests",
        TaskTypes.BuildProject => "Build project and check for errors",
        TaskTypes.GenerateDocs => "Generate code documentation",
        TaskTypes.Refactor => "AI-assisted code refactoring",
        TaskTypes.SecurityScan => "Scan dependencies for vulnerabilities",
        TaskTypes.Custom => "Custom AI-executed task",
        _ => "Unknown task type"
    };

    #endregion
}
