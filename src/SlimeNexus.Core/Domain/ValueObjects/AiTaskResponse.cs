using System.Text.Json.Serialization;

namespace SlimeNexus.Core.Domain.ValueObjects;

/// <summary>
/// Structured response from the AI model when interpreting user messages.
/// The AI returns this JSON to indicate what task (if any) should be executed
/// and what message to display to the user.
/// </summary>
public sealed record AiTaskResponse
{
    /// <summary>
    /// The task type to execute via OpenClaw (e.g., "code_review", "file_cleanup").
    /// Null or empty if no task is needed (conversation only).
    /// </summary>
    [JsonPropertyName("taskType")]
    public string? TaskType { get; init; }

    /// <summary>
    /// The message to display to the user in the chat.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional target folder for file-based tasks.
    /// </summary>
    [JsonPropertyName("targetFolder")]
    public string? TargetFolder { get; init; }

    /// <summary>
    /// Optional execution plan or additional instructions for the task executor.
    /// </summary>
    [JsonPropertyName("executionPlan")]
    public string? ExecutionPlan { get; init; }

    /// <summary>
    /// Keywords extracted from the user's request to refine task-specific prompts.
    /// Example: ["performance", "segurança", "bugs"]
    /// </summary>
    [JsonPropertyName("userKeywords")]
    public List<string>? UserKeywords { get; init; }

    /// <summary>
    /// Whether this response contains a task to execute.
    /// </summary>
    [JsonIgnore]
    public bool HasTask => !string.IsNullOrWhiteSpace(TaskType);
}
