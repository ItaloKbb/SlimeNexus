namespace SlimeNexus.Core.Domain.Interfaces;

/// <summary>
/// Contract for AI inference providers (Ollama/Llama 3, OpenClaw, etc.).
/// Implemented in SlimeNexus.Infrastructure.AI.
/// </summary>
public interface IAiProvider
{
    /// <summary>Name of the underlying model/provider (e.g., "ollama/llama3").</summary>
    string ProviderName { get; }

    /// <summary>Returns true when the local AI service is reachable and ready.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends a prompt and returns the AI-generated response text.</summary>
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
