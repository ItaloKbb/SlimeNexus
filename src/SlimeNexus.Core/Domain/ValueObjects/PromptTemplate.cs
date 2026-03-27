using System.Text.Json.Serialization;

namespace SlimeNexus.Core.Domain.ValueObjects;

/// <summary>
/// Defines a reusable prompt template that provides context-specific instructions
/// for AI analysis. Each template targets specific analysis scenarios.
/// </summary>
public sealed record PromptTemplate
{
    /// <summary>Unique identifier for the template.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name shown in the UI selector.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Short description of what this prompt focuses on.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>Emoji icon for UI display.</summary>
    [JsonPropertyName("icon")]
    public string Icon { get; init; } = "📄";

    /// <summary>
    /// The prompt text injected into the AI context when this template is active.
    /// Can reference {targetFolder} and {userMessage} as placeholders.
    /// </summary>
    [JsonPropertyName("promptText")]
    public string PromptText { get; init; } = string.Empty;

    /// <summary>
    /// Task types this prompt is compatible with.
    /// Empty means compatible with all task types.
    /// </summary>
    [JsonPropertyName("compatibleTaskTypes")]
    public List<string> CompatibleTaskTypes { get; init; } = [];

    /// <summary>
    /// Keywords automatically added when this prompt is used.
    /// </summary>
    [JsonPropertyName("defaultKeywords")]
    public List<string> DefaultKeywords { get; init; } = [];

    /// <summary>Whether this is a built-in template (cannot be deleted).</summary>
    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; init; }

    /// <summary>Display text combining icon and name.</summary>
    [JsonIgnore]
    public string DisplayName => $"{Icon} {Name}";
}

/// <summary>
/// Root object for the prompts.json configuration file.
/// </summary>
public sealed record PromptTemplateCollection
{
    [JsonPropertyName("prompts")]
    public List<PromptTemplate> Prompts { get; init; } = [];
}
