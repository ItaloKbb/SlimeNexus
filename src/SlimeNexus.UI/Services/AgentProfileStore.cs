using System.Text.Json;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.UI.Services;

/// <summary>
/// Loads, saves, and manages agent profiles from a JSON configuration file.
/// Profiles are stored in the app's data directory alongside the executable.
/// </summary>
public sealed class AgentProfileStore
{
    private readonly ILogger<AgentProfileStore> _logger;
    private readonly string _filePath;
    private List<AgentProfile> _profiles = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AgentProfileStore(ILogger<AgentProfileStore> logger)
    {
        _logger = logger;
        var appDir = AppContext.BaseDirectory;
        _filePath = Path.Combine(appDir, "agents.json");
    }

    /// <summary>All loaded profiles, including the "Automático" default.</summary>
    public IReadOnlyList<AgentProfile> Profiles => _profiles;

    /// <summary>
    /// Loads profiles from disk. If the file doesn't exist, creates it with default profiles.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath);
                var collection = JsonSerializer.Deserialize<AgentProfileCollection>(json, JsonOptions);
                if (collection?.Agents is { Count: > 0 })
                {
                    _profiles = collection.Agents;
                    _logger.LogInformation("Loaded {Count} agent profiles from {Path}", _profiles.Count, _filePath);
                    EnsureAutoProfile();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load agent profiles from {Path}, using defaults", _filePath);
        }

        _profiles = GetDefaultProfiles();
        await SaveAsync();
    }

    /// <summary>Saves current profiles to disk.</summary>
    public async Task SaveAsync()
    {
        try
        {
            var collection = new AgentProfileCollection { Agents = _profiles };
            var json = JsonSerializer.Serialize(collection, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
            _logger.LogDebug("Saved {Count} agent profiles to {Path}", _profiles.Count, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save agent profiles to {Path}", _filePath);
        }
    }

    /// <summary>Adds a new profile and saves.</summary>
    public async Task AddAsync(AgentProfile profile)
    {
        _profiles.Add(profile);
        await SaveAsync();
    }

    /// <summary>Removes a profile by ID (built-in profiles cannot be removed).</summary>
    public async Task<bool> RemoveAsync(string id)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == id);
        if (profile is null || profile.IsBuiltIn) return false;
        _profiles.Remove(profile);
        await SaveAsync();
        return true;
    }

    private void EnsureAutoProfile()
    {
        if (_profiles.All(p => p.Id != "auto"))
        {
            _profiles.Insert(0, CreateAutoProfile());
        }
    }

    private static AgentProfile CreateAutoProfile() => new()
    {
        Id = "auto",
        Name = "Automático",
        Description = "A IA decide o tipo de tarefa com base na sua mensagem",
        Icon = "🤖",
        SystemPromptOverlay = "",
        IsBuiltIn = true
    };

    private static List<AgentProfile> GetDefaultProfiles() =>
    [
        CreateAutoProfile(),
        new()
        {
            Id = "analyst",
            Name = "Análise de Código",
            Description = "Revisor sênior que foca em bugs, segurança e boas práticas",
            Icon = "🔍",
            SystemPromptOverlay = "Você é um revisor de código sênior. Foque em: bugs potenciais, vulnerabilidades de segurança, problemas de performance e violações de boas práticas. Seja detalhado e objetivo.",
            DefaultTaskType = "code_review",
            DefaultKeywords = ["bugs", "segurança", "performance", "boas práticas"],
            IsBuiltIn = true
        },
        new()
        {
            Id = "improver",
            Name = "Melhoria de Código",
            Description = "Sugere refatorações, padrões modernos e otimizações",
            Icon = "✨",
            SystemPromptOverlay = "Você é um especialista em refatoração e modernização de código. Sugira melhorias usando padrões modernos, SOLID, clean code e otimizações de performance.",
            DefaultTaskType = "refactor",
            DefaultKeywords = ["refatoração", "clean code", "SOLID", "otimização"],
            IsBuiltIn = true
        },
        new()
        {
            Id = "planner",
            Name = "Planejamento",
            Description = "Planeja arquitetura, features e sprints",
            Icon = "📋",
            SystemPromptOverlay = "Você é um arquiteto de software e tech lead. Ajude a planejar: arquitetura de sistemas, features, sprints, e decisões técnicas. Use diagramas textuais quando possível.",
            DefaultTaskType = "custom",
            DefaultKeywords = ["arquitetura", "planejamento", "feature"],
            IsBuiltIn = true
        },
        new()
        {
            Id = "nodejs",
            Name = "Desenvolvimento Node.js",
            Description = "Especialista em Node.js, Express, Prisma e TypeScript",
            Icon = "🟢",
            SystemPromptOverlay = "Você é um especialista em Node.js/TypeScript. Foque em: Express, Prisma ORM, NestJS, padrões assíncronos, gerenciamento de pacotes npm e boas práticas de backend JavaScript/TypeScript.",
            DefaultTaskType = "code_review",
            FocusExtensions = ["*.ts", "*.tsx", "*.js", "*.jsx", "*.prisma", "*.json"],
            DefaultKeywords = ["node.js", "typescript", "prisma", "express"],
            IsBuiltIn = true
        },
        new()
        {
            Id = "static-page",
            Name = "Página Estática",
            Description = "Criação e análise de páginas HTML/CSS/JS estáticas",
            Icon = "🌐",
            SystemPromptOverlay = "Você é um especialista em desenvolvimento frontend estático. Foque em: HTML5 semântico, CSS moderno (Flexbox, Grid), JavaScript vanilla, acessibilidade (WCAG), SEO e performance de carregamento.",
            DefaultTaskType = "code_review",
            FocusExtensions = ["*.html", "*.css", "*.scss", "*.js"],
            DefaultKeywords = ["html", "css", "frontend", "acessibilidade"],
            IsBuiltIn = true
        },
        new()
        {
            Id = "security",
            Name = "Analista de Segurança",
            Description = "Foca em vulnerabilidades, pacotes inseguros e OWASP",
            Icon = "🛡️",
            SystemPromptOverlay = "Você é um analista de segurança de aplicações. Foque em: OWASP Top 10, injeção SQL/XSS, dependências vulneráveis, secrets expostos, autenticação/autorização e hardening de configurações.",
            DefaultTaskType = "security_scan",
            DefaultKeywords = ["segurança", "vulnerabilidades", "OWASP", "CVE"],
            IsBuiltIn = true
        }
    ];
}
