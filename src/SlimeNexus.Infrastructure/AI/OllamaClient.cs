using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.Infrastructure.AI;

/// <summary>
/// HTTP client for the Ollama local AI inference server.
/// Communicates with Ollama REST API on localhost.
/// </summary>
public sealed class OllamaClient : IAiProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaClient> _logger;
    private readonly OllamaOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaClient(
        HttpClient httpClient,
        ILogger<OllamaClient> logger,
        OllamaOptions? options = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options ?? new OllamaOptions();

        // Configure base address if not already set
        _httpClient.BaseAddress ??= new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <inheritdoc />
    public string ProviderName => $"ollama/{_options.DefaultModel}";

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if Ollama is running by hitting the tags endpoint
            var response = await _httpClient.GetAsync("api/tags", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Ollama service is available at {BaseUrl}", _options.BaseUrl);
                return true;
            }

            _logger.LogWarning("Ollama returned status {StatusCode}", response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama service is not reachable at {BaseUrl}", _options.BaseUrl);
            return false;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Ollama availability check timed out");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return await GenerateAsync(prompt, _options.DefaultModel, cancellationToken);
    }

    /// <summary>
    /// Generates a response using a specific model.
    /// </summary>
    public async Task<string> GenerateAsync(
        string prompt,
        string model,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _logger.LogDebug("Sending prompt to Ollama model {Model}: {Prompt}", 
            model, prompt[..Math.Min(100, prompt.Length)]);

        var request = new OllamaGenerateRequest
        {
            Model = model,
            Prompt = prompt,
            Stream = false,
            Options = new OllamaModelOptions
            {
                Temperature = _options.Temperature,
                NumCtx = _options.ContextLength
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/generate",
                request,
                JsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                JsonOptions, cancellationToken);

            if (result is null)
            {
                throw new InvalidOperationException("Ollama returned null response");
            }

            _logger.LogDebug(
                "Ollama response received: {ResponseLength} chars, {TotalDuration}ms",
                result.Response?.Length ?? 0,
                result.TotalDuration / 1_000_000); // nanoseconds to ms

            return result.Response ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to communicate with Ollama");
            throw new OllamaException("Failed to generate response from Ollama", ex);
        }
    }

    /// <summary>
    /// Generates a streaming response from Ollama.
    /// </summary>
    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        string? model = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        model ??= _options.DefaultModel;

        var request = new OllamaGenerateRequest
        {
            Model = model,
            Prompt = prompt,
            Stream = true,
            Options = new OllamaModelOptions
            {
                Temperature = _options.Temperature,
                NumCtx = _options.ContextLength
            }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/generate")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var chunk = JsonSerializer.Deserialize<OllamaGenerateResponse>(line, JsonOptions);
            if (chunk?.Response is not null)
            {
                yield return chunk.Response;
            }

            if (chunk?.Done == true) break;
        }
    }

    /// <summary>
    /// Lists all available models in Ollama.
    /// </summary>
    public async Task<IReadOnlyList<OllamaModelInfo>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>(
                "api/tags", JsonOptions, cancellationToken);

            return response?.Models ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Ollama models");
            return [];
        }
    }

    /// <summary>
    /// Pulls (downloads) a model from the Ollama registry.
    /// </summary>
    public async Task<bool> PullModelAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Pulling Ollama model: {Model}", modelName);

        try
        {
            var request = new { name = modelName };
            var response = await _httpClient.PostAsJsonAsync(
                "api/pull", request, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pull model {Model}", modelName);
            return false;
        }
    }

    /// <summary>
    /// Checks if a specific model is available locally.
    /// </summary>
    public async Task<bool> HasModelAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        var models = await ListModelsAsync(cancellationToken);
        return models.Any(m => 
            m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
            m.Name.StartsWith(modelName + ":", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        // HttpClient is managed by HttpClientFactory, don't dispose
    }
}

#region Ollama DTOs

/// <summary>
/// Configuration options for the Ollama client.
/// </summary>
public sealed record OllamaOptions
{
    /// <summary>Base URL for the Ollama API (default: http://localhost:11434)</summary>
    public string BaseUrl { get; init; } = "http://localhost:11434";

    /// <summary>Default model to use for generation (default: llama3:8b-instruct-q4_K_M)</summary>
    public string DefaultModel { get; init; } = "llama3:8b-instruct-q4_K_M";

    /// <summary>Request timeout in seconds (default: 300)</summary>
    public int TimeoutSeconds { get; init; } = 300;

    /// <summary>Temperature for generation (0.0 = deterministic, 1.0 = creative)</summary>
    public float Temperature { get; init; } = 0.7f;

    /// <summary>Context length in tokens (default: 4096)</summary>
    public int ContextLength { get; init; } = 4096;
}

internal sealed record OllamaGenerateRequest
{
    public required string Model { get; init; }
    public required string Prompt { get; init; }
    public bool Stream { get; init; }
    public string? System { get; init; }
    public OllamaModelOptions? Options { get; init; }
}

internal sealed record OllamaModelOptions
{
    public float Temperature { get; init; }
    public int NumCtx { get; init; }
    public int? NumGpu { get; init; }
    public int? NumThread { get; init; }
}

internal sealed record OllamaGenerateResponse
{
    public string? Model { get; init; }
    public string? Response { get; init; }
    public bool Done { get; init; }
    public long TotalDuration { get; init; }
    public long LoadDuration { get; init; }
    public int PromptEvalCount { get; init; }
    public int EvalCount { get; init; }
}

internal sealed record OllamaTagsResponse
{
    public List<OllamaModelInfo>? Models { get; init; }
}

/// <summary>
/// Information about an Ollama model.
/// </summary>
public sealed record OllamaModelInfo
{
    public required string Name { get; init; }
    public DateTime ModifiedAt { get; init; }
    public long Size { get; init; }
    public string? Digest { get; init; }
}

/// <summary>
/// Exception thrown when Ollama communication fails.
/// </summary>
public sealed class OllamaException : Exception
{
    public OllamaException(string message) : base(message) { }
    public OllamaException(string message, Exception innerException) 
        : base(message, innerException) { }
}

#endregion
