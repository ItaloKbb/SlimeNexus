using System.Text.Json;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.UI.Services;

/// <summary>
/// Loads, saves, and manages prompt templates from a JSON configuration file.
/// Templates are stored in the app's data directory alongside the executable.
/// </summary>
public sealed class PromptTemplateStore
{
    private readonly ILogger<PromptTemplateStore> _logger;
    private readonly string _filePath;
    private List<PromptTemplate> _templates = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public PromptTemplateStore(ILogger<PromptTemplateStore> logger)
    {
        _logger = logger;
        var appDir = AppContext.BaseDirectory;
        _filePath = Path.Combine(appDir, "prompts.json");
    }

    /// <summary>All loaded templates, including the "Nenhum" default.</summary>
    public IReadOnlyList<PromptTemplate> Templates => _templates;

    /// <summary>
    /// Loads templates from disk. If the file doesn't exist, creates it with default templates.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath);
                var collection = JsonSerializer.Deserialize<PromptTemplateCollection>(json, JsonOptions);
                if (collection?.Prompts is { Count: > 0 })
                {
                    _templates = collection.Prompts;
                    _logger.LogInformation("Loaded {Count} prompt templates from {Path}", _templates.Count, _filePath);
                    EnsureNoneTemplate();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load prompt templates from {Path}, using defaults", _filePath);
        }

        _templates = GetDefaultTemplates();
        await SaveAsync();
    }

    /// <summary>Saves current templates to disk.</summary>
    public async Task SaveAsync()
    {
        try
        {
            var collection = new PromptTemplateCollection { Prompts = _templates };
            var json = JsonSerializer.Serialize(collection, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
            _logger.LogDebug("Saved {Count} prompt templates to {Path}", _templates.Count, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save prompt templates to {Path}", _filePath);
        }
    }

    /// <summary>Adds a new template and saves.</summary>
    public async Task AddAsync(PromptTemplate template)
    {
        _templates.Add(template);
        await SaveAsync();
    }

    /// <summary>Removes a template by ID (built-in templates cannot be removed).</summary>
    public async Task<bool> RemoveAsync(string id)
    {
        var template = _templates.FirstOrDefault(t => t.Id == id);
        if (template is null || template.IsBuiltIn) return false;
        _templates.Remove(template);
        await SaveAsync();
        return true;
    }

    private void EnsureNoneTemplate()
    {
        if (_templates.All(t => t.Id != "none"))
        {
            _templates.Insert(0, CreateNoneTemplate());
        }
    }

    private static PromptTemplate CreateNoneTemplate() => new()
    {
        Id = "none",
        Name = "Nenhum",
        Description = "Sem prompt adicional — a IA decide livremente",
        Icon = "➖",
        PromptText = "",
        IsBuiltIn = true
    };

    private static List<PromptTemplate> GetDefaultTemplates() =>
    [
        CreateNoneTemplate(),
        new()
        {
            Id = "db-schema",
            Name = "Schema de Banco de Dados",
            Description = "Analisa modelos, relações, índices e migrations",
            Icon = "🗄️",
            PromptText = "Analise o schema de banco de dados com foco em: normalização, relações entre entidades, tipos de dados adequados, índices necessários, constraints (FK, unique), e possíveis problemas de performance em queries. Se encontrar arquivos Prisma, analise models, relations e @@index.",
            CompatibleTaskTypes = ["code_review", "custom"],
            DefaultKeywords = ["schema", "banco de dados", "modelos", "relações", "índices"],
            IsBuiltIn = true
        },
        new()
        {
            Id = "frontend",
            Name = "Análise de Frontend",
            Description = "Foca em componentes, acessibilidade, responsividade e UX",
            Icon = "🎨",
            PromptText = "Analise o código frontend com foco em: componentização, acessibilidade (ARIA, semântica HTML), responsividade, performance de renderização, gerenciamento de estado, e boas práticas de CSS/SCSS. Verifique se há código duplicado entre componentes.",
            CompatibleTaskTypes = ["code_review", "refactor"],
            DefaultKeywords = ["frontend", "componentes", "acessibilidade", "responsivo", "UX"],
            IsBuiltIn = true
        },
        new()
        {
            Id = "api-rest",
            Name = "Análise de API REST",
            Description = "Verifica endpoints, validações, autenticação e padrões REST",
            Icon = "🔌",
            PromptText = "Analise a API REST com foco em: padrões RESTful (verbos HTTP, status codes), validação de entrada, autenticação/autorização, tratamento de erros, paginação, versionamento e documentação de endpoints.",
            CompatibleTaskTypes = ["code_review", "security_scan"],
            DefaultKeywords = ["api", "rest", "endpoints", "autenticação", "validação"],
            IsBuiltIn = true
        },
        new()
        {
            Id = "testing",
            Name = "Cobertura de Testes",
            Description = "Analisa qualidade e cobertura dos testes existentes",
            Icon = "🧪",
            PromptText = "Analise os testes do projeto com foco em: cobertura de cenários (happy path e edge cases), qualidade dos asserts, uso de mocks, nomenclatura descritiva, padrão AAA (Arrange-Act-Assert) e testes de integração vs unitários.",
            CompatibleTaskTypes = ["code_review", "test_run"],
            DefaultKeywords = ["testes", "cobertura", "unit test", "mock", "assert"],
            IsBuiltIn = true
        },
        new()
        {
            Id = "performance",
            Name = "Análise de Performance",
            Description = "Identifica gargalos, alocações desnecessárias e otimizações",
            Icon = "⚡",
            PromptText = "Analise o código com foco em performance: alocações de memória desnecessárias, operações O(n²), chamadas síncronas bloqueantes, queries N+1, cache ausente, e oportunidades de usar Span<T>, ValueTask, ou pooling.",
            CompatibleTaskTypes = ["code_review", "refactor"],
            DefaultKeywords = ["performance", "memória", "otimização", "gargalo", "cache"],
            IsBuiltIn = true
        },
        new()
        {
            Id = "devops",
            Name = "DevOps & CI/CD",
            Description = "Analisa pipelines, Docker, configurações e deploy",
            Icon = "🚀",
            PromptText = "Analise os arquivos de infraestrutura e DevOps: Dockerfiles, docker-compose, CI/CD pipelines (GitHub Actions, Azure DevOps), variáveis de ambiente, secrets, configurações de deploy, e health checks.",
            CompatibleTaskTypes = ["code_review", "security_scan", "custom"],
            DefaultKeywords = ["docker", "CI/CD", "pipeline", "deploy", "infraestrutura"],
            IsBuiltIn = true
        }
    ];
}
