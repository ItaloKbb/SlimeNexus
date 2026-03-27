using System.Text.Json.Serialization;

namespace SlimeNexus.Core.Domain.ValueObjects;

/// <summary>
/// Defines an agent profile that shapes how the AI interprets and routes tasks.
/// Each profile has its own system prompt overlay, supported task types, and behavior hints.
/// </summary>
public sealed record AgentProfile
{
    /// <summary>Unique identifier for the profile.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name shown in the UI selector.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Short description of the agent's purpose.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>Emoji icon for UI display.</summary>
    [JsonPropertyName("icon")]
    public string Icon { get; init; } = "🤖";

    /// <summary>
    /// Additional system prompt text appended to the base system prompt.
    /// Gives the AI personality/focus for this agent type.
    /// </summary>
    [JsonPropertyName("systemPromptOverlay")]
    public string SystemPromptOverlay { get; init; } = string.Empty;

    /// <summary>
    /// Default task type this agent should prefer (e.g., "code_review", "custom").
    /// Null means the AI decides based on user input.
    /// </summary>
    [JsonPropertyName("defaultTaskType")]
    public string? DefaultTaskType { get; init; }

    /// <summary>
    /// File extensions this agent focuses on (e.g., ["*.prisma", "*.ts"]).
    /// Empty means all extensions.
    /// </summary>
    [JsonPropertyName("focusExtensions")]
    public List<string> FocusExtensions { get; init; } = [];

    /// <summary>
    /// Extra keywords automatically injected into every request with this agent.
    /// </summary>
    [JsonPropertyName("defaultKeywords")]
    public List<string> DefaultKeywords { get; init; } = [];

    /// <summary>Whether this is a built-in profile (cannot be deleted).</summary>
    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; init; }

    /// <summary>Display text combining icon and name.</summary>
    [JsonIgnore]
    public string DisplayName => $"{Icon} {Name}";
}

/// <summary>
/// Root object for the agents.json configuration file.
/// </summary>
public sealed record AgentProfileCollection
{
    [JsonPropertyName("agents")]
    public List<AgentProfile> Agents { get; init; } = [];
}
