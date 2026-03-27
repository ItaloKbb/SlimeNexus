using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Enums;
using SlimeNexus.Core.Domain.Interfaces;
using SlimeNexus.Core.Domain.ValueObjects;
using SlimeNexus.UI.Services;

namespace SlimeNexus.UI.ViewModels;

/// <summary>
/// Represents a single chat message.
/// </summary>
public sealed class ChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public AgentSession? AgentSession { get; init; }
    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public bool IsSystem => Role == "system";
    public bool IsAgentTask => AgentSession is not null;
}

/// <summary>
/// ViewModel for the AI Chat view.
/// Acts as the bridge between the user, Ollama (AI interpretation), and OpenClaw (task execution).
/// Flow: User message → Ollama (returns JSON with taskType + message) → OpenClaw (executes task) → Result.
/// </summary>
public partial class ChatViewModel : ViewModelBase
{
    private readonly IAiProvider _aiProvider;
    private readonly ITaskExecutor _taskExecutor;
    private readonly ILogger<ChatViewModel> _logger;
    private readonly AgentProfileStore _agentStore;
    private readonly PromptTemplateStore _promptStore;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// System prompt that instructs Ollama to return structured JSON responses.
    /// </summary>
    private const string SystemPrompt = """
        Você é o SlimeNexus, um assistente de desenvolvimento que age como ponte entre o usuário e ferramentas de automação.

        SEMPRE responda em JSON válido com este formato exato:
        {
          "taskType": "<tipo_da_task_ou_null>",
          "message": "<mensagem em markdown para o usuário>",
          "targetFolder": "<pasta alvo se mencionada, ou null>",
          "executionPlan": "<instruções extras para a task, ou null>",
          "userKeywords": ["keyword1", "keyword2"]
        }

        Tipos de task disponíveis (use exatamente estes valores):
        - "code_review" → Revisão de código (qualquer linguagem: C#, TypeScript, JavaScript, Python, Prisma, etc.)
        - "file_cleanup" → Limpar bin, obj, node_modules
        - "git_commit" → Validar formato do commit (Conventional Commits)
        - "test_run" → Executar testes unitários
        - "build_project" → Compilar projeto e verificar erros
        - "generate_docs" → Escanear membros públicos sem documentação
        - "refactor" → Analisar e aplicar refatoração de código
        - "security_scan" → Verificar pacotes vulneráveis e desatualizados
        - "custom" → Tarefa personalizada definida pelo usuário

        Regras:
        1. Se o usuário pedir algo que corresponde a uma task, GERE A TASK IMEDIATAMENTE. Não peça confirmação. Se ele pediu "analise", "revise", "limpe", etc., é uma task.
        2. Se for apenas conversa/pergunta sem ação, defina "taskType" como null.
        3. A "message" SEMPRE deve usar formatação Markdown (negrito, listas, blocos de código, etc.) e ser em português. Deve ser breve e informar que a tarefa está sendo executada.
        4. Se o usuário mencionar um caminho de ARQUIVO (ex: "C:/Git/projeto/src/file.cs"), use a PASTA PAI como "targetFolder" (ex: "C:/Git/projeto/src"). Se mencionar uma pasta, use diretamente.
        5. Tasks como code_review, file_cleanup, generate_docs EXIGEM "targetFolder". Se o usuário não informar nenhum caminho, peça na "message".
        6. NUNCA retorne nada fora do JSON. O JSON é a única saída.
        7. "userKeywords" deve conter as palavras-chave do pedido do usuário que refinam a tarefa (ex: "performance", "segurança", "testes", "bugs", "schema", "prisma"). Sempre extraia ao menos 1 keyword quando houver task.
        8. IMPORTANTE: Em "targetFolder", SEMPRE use barras normais (/) nos caminhos. Exemplo: "C:/Users/nome/projeto", NUNCA "C:\Users\nome\projeto". Isso evita problemas com escape de JSON.
        9. Palavras como "analise", "revise", "verifique", "veja", "olhe" o código/arquivos = code_review. NUNCA peça confirmação quando o usuário já informou o caminho.
        """;

    [ObservableProperty]
    private string _userMessage = string.Empty;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _isAiConnected;

    [ObservableProperty]
    private string _aiStatusText = "Verificando...";

    [ObservableProperty]
    private string _modelName = "...";

    [ObservableProperty]
    private string _targetFolder = string.Empty;

    [ObservableProperty]
    private AgentProfile? _selectedAgent;

    [ObservableProperty]
    private PromptTemplate? _selectedPrompt;

    public ObservableCollection<AgentProfile> AvailableAgents { get; } = [];
    public ObservableCollection<PromptTemplate> AvailablePrompts { get; } = [];
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public ChatViewModel(
        IAiProvider aiProvider,
        ITaskExecutor taskExecutor,
        ILogger<ChatViewModel> logger,
        AgentProfileStore agentStore,
        PromptTemplateStore promptStore)
    {
        _aiProvider = aiProvider;
        _taskExecutor = taskExecutor;
        _logger = logger;
        _agentStore = agentStore;
        _promptStore = promptStore;

        ModelName = _aiProvider.ProviderName;

        Messages.Add(new ChatMessage
        {
            Role = "system",
            Content = "👋 Bem-vindo ao SlimeNexus Chat! Envie uma mensagem para conversar com a IA local.\n" +
                      "Peça tarefas como: \"revise meu código\", \"limpe os arquivos\", \"rode os testes\", etc."
        });

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _agentStore.LoadAsync();
        await _promptStore.LoadAsync();

        foreach (var agent in _agentStore.Profiles)
            AvailableAgents.Add(agent);
        foreach (var prompt in _promptStore.Templates)
            AvailablePrompts.Add(prompt);

        SelectedAgent = AvailableAgents.FirstOrDefault();
        SelectedPrompt = AvailablePrompts.FirstOrDefault();

        await CheckConnectionAsync();
    }

    private async Task CheckConnectionAsync()
    {
        try
        {
            IsAiConnected = await _aiProvider.IsAvailableAsync();
            AiStatusText = IsAiConnected ? "Conectado" : "Desconectado";
            ModelName = _aiProvider.ProviderName;
        }
        catch
        {
            IsAiConnected = false;
            AiStatusText = "Erro";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserMessage)) return;

        var userText = UserMessage.Trim();
        UserMessage = string.Empty;

        Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = userText
        });

        IsGenerating = true;

        try
        {
            // Build the prompt with system instructions + agent overlay + prompt template
            var prompt = BuildFullPrompt(userText);

            _logger.LogDebug("Sending message to AI for interpretation: {Message}", userText);
            var rawResponse = await _aiProvider.GenerateAsync(prompt);

            // Try to parse the structured JSON response from Ollama
            var aiResponse = ParseAiResponse(rawResponse);

            // Show the AI's message to the user
            Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = aiResponse.Message
            });

            // If the AI identified a task, route it to OpenClaw
            if (aiResponse.HasTask)
            {
                await ExecuteTaskFromAiAsync(aiResponse);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message");
            Messages.Add(new ChatMessage
            {
                Role = "system",
                Content = $"❌ Erro: {ex.Message}"
            });
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// Parses the raw AI response into a structured <see cref="AiTaskResponse"/>.
    /// Handles common LLM issues: markdown code blocks, unescaped backslashes in file paths,
    /// and malformed JSON. Never returns raw JSON as the message.
    /// </summary>
    private AiTaskResponse ParseAiResponse(string rawResponse)
    {
        var trimmed = rawResponse.Trim();
        AiTaskResponse? result = null;

        var jsonStart = trimmed.IndexOf('{');
        var jsonEnd = trimmed.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonText = trimmed[jsonStart..(jsonEnd + 1)];

            // Attempt 1: direct deserialization
            result = TryDeserialize(jsonText);

            // Attempt 2: fix unescaped backslashes (common LLM issue with file paths like C:\Git\aula)
            if (result is null)
            {
                var sanitized = SanitizeJsonBackslashes(jsonText);
                result = TryDeserialize(sanitized);
            }

            // Attempt 3: regex field extraction as last resort
            if (result is null)
            {
                result = TryExtractFieldsViaRegex(jsonText);
            }

            if (result is null)
                _logger.LogWarning("Failed to parse AI JSON response after all recovery attempts");
        }

        // Fallback: never show raw JSON — extract message content or use cleaned text
        if (result is null)
        {
            _logger.LogDebug("AI response was not parseable JSON, treating as conversation");
            result = new AiTaskResponse { Message = ExtractMessageFromRawText(trimmed) };
        }

        // Always repair TargetFolder — JSON escapes like \t, \n, \b collide with
        // Windows path fragments (\temp, \node_modules, \bin) and produce control chars.
        return RepairTargetFolder(result);
    }

    private AiTaskResponse? TryDeserialize(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<AiTaskResponse>(json, JsonOptions);
            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Message))
            {
                _logger.LogDebug("Parsed AI response: taskType={TaskType}, message length={Length}",
                    parsed.TaskType, parsed.Message.Length);
                return parsed;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "JSON deserialization attempt failed");
        }
        return null;
    }

    /// <summary>
    /// Fixes unescaped backslashes in JSON strings produced by LLMs.
    /// Escapes lone backslashes while preserving valid JSON escape sequences (\n, \t, \", \\, etc.).
    /// Only runs when direct parsing already failed, so double-escaping is not a concern.
    /// </summary>
    private static string SanitizeJsonBackslashes(string json)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            json,
            @"\\(?![""\\\/bfnrtu])",
            @"\\\\");
    }

    /// <summary>
    /// Extracts known fields from malformed JSON using regex as a last-resort parser.
    /// </summary>
    private AiTaskResponse? TryExtractFieldsViaRegex(string json)
    {
        try
        {
            var messageMatch = System.Text.RegularExpressions.Regex.Match(
                json,
                @"""message""\s*:\s*""((?:[^""\\]|\\.)*)""",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            if (!messageMatch.Success) return null;

            var taskTypeMatch = System.Text.RegularExpressions.Regex.Match(
                json, @"""taskType""\s*:\s*""([^""]+)""");
            var folderMatch = System.Text.RegularExpressions.Regex.Match(
                json, @"""targetFolder""\s*:\s*""((?:[^""\\]|\\.)*)""");
            var planMatch = System.Text.RegularExpressions.Regex.Match(
                json, @"""executionPlan""\s*:\s*""((?:[^""\\]|\\.)*)""");

            _logger.LogDebug("Regex-extracted AI response: taskType={TaskType}",
                taskTypeMatch.Success ? taskTypeMatch.Groups[1].Value : "null");

            return new AiTaskResponse
            {
                Message = UnescapeJsonString(messageMatch.Groups[1].Value),
                TaskType = taskTypeMatch.Success ? taskTypeMatch.Groups[1].Value : null,
                TargetFolder = folderMatch.Success ? UnescapeJsonString(folderMatch.Groups[1].Value) : null,
                ExecutionPlan = planMatch.Success ? UnescapeJsonString(planMatch.Groups[1].Value) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Regex extraction failed");
            return null;
        }
    }

    /// <summary>
    /// Extracts the "message" field from raw text that may contain JSON,
    /// ensuring raw JSON is never shown to the user.
    /// </summary>
    private static string ExtractMessageFromRawText(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"""message""\s*:\s*""((?:[^""\\]|\\.)*)""",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        if (match.Success)
            return UnescapeJsonString(match.Groups[1].Value);

        if (text.TrimStart().StartsWith('{') && text.TrimEnd().EndsWith('}'))
            return "⚠ Não foi possível interpretar a resposta da IA. Tente reformular sua mensagem.";

        return text;
    }

    private static string UnescapeJsonString(string value) =>
        value.Replace("\\n", "\n")
             .Replace("\\r", "\r")
             .Replace("\\t", "\t")
             .Replace("\\\"", "\"")
             .Replace("\\\\", "\\");

    /// <summary>
    /// Repairs a file path where JSON escape sequences collided with Windows path fragments.
    /// Control characters (tab, newline, backspace, etc.) never appear in valid file paths,
    /// so their presence means a path like "C:\temp" had \t parsed as a tab character.
    /// </summary>
    private static AiTaskResponse RepairTargetFolder(AiTaskResponse response)
    {
        if (response.TargetFolder is null) return response;

        var repaired = response.TargetFolder
            .Replace("\t", "\\t")    // tab       → \t  (e.g. \temp, \test, \tabs, \tools)
            .Replace("\n", "\\n")    // newline   → \n  (e.g. \node_modules, \new, \nuget)
            .Replace("\r", "\\r")    // CR        → \r  (e.g. \resources, \release, \repos)
            .Replace("\b", "\\b")    // backspace → \b  (e.g. \bin, \build, \backup)
            .Replace("\f", "\\f");   // form feed → \f  (e.g. \files, \fonts, \framework)

        return repaired == response.TargetFolder
            ? response
            : response with { TargetFolder = repaired };
    }

    /// <summary>
    /// Executes a task identified by the AI using the agent session pipeline.
    /// Creates a visual agent card in the chat that shows step-by-step progress.
    /// Uses user keywords to enhance predefined task prompts.
    /// </summary>
    private async Task ExecuteTaskFromAiAsync(AiTaskResponse aiResponse)
    {
        var taskType = aiResponse.TaskType!;

        if (!_taskExecutor.CanExecute(taskType))
        {
            Messages.Add(new ChatMessage
            {
                Role = "system",
                Content = $"⚠️ Tipo de task desconhecido: \"{taskType}\". Tasks disponíveis: {string.Join(", ", _taskExecutor.SupportedTaskTypes)}"
            });
            return;
        }

        var targetFolder = aiResponse.TargetFolder ??
            (string.IsNullOrWhiteSpace(TargetFolder) ? null : TargetFolder);

        var (icon, title) = GetTaskInfo(taskType);
        var session = new AgentSession
        {
            TaskType = taskType,
            TaskIcon = icon,
            TaskTitle = title
        };

        session.Steps.Add(new AgentStep { Title = "Identificando tarefa", Icon = "🎯" });
        session.Steps.Add(new AgentStep { Title = "Extraindo palavras-chave", Icon = "📝" });
        session.Steps.Add(new AgentStep { Title = "Mapeando arquivos", Icon = "📁" });
        session.Steps.Add(new AgentStep { Title = "Analisando stack", Icon = "🔍" });
        session.Steps.Add(new AgentStep { Title = "Otimizando prompt", Icon = "🧠" });
        session.Steps.Add(new AgentStep { Title = "Executando via OpenClaw", Icon = "🔗" });
        session.Steps.Add(new AgentStep { Title = "Processando resultados", Icon = "📊" });

        Messages.Add(new ChatMessage
        {
            Role = "agent",
            Content = string.Empty,
            AgentSession = session
        });

        try
        {
            // Step 1: Identify task
            await RunStepAsync(session, 0, () =>
            {
                session.Steps[0].Detail = $"{title} ({taskType})";
                session.ProgressPercent = 10;
            });

            // Step 2: Extract keywords (merge AI + agent + prompt defaults)
            await RunStepAsync(session, 1, () =>
            {
                var keywords = aiResponse.UserKeywords ?? [];
                var merged = new List<string>(keywords);

                if (SelectedAgent is { DefaultKeywords.Count: > 0 } agentKw)
                    merged.AddRange(agentKw.DefaultKeywords);
                if (SelectedPrompt is { DefaultKeywords.Count: > 0 } promptKw)
                    merged.AddRange(promptKw.DefaultKeywords);

                merged = merged.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                session.UserKeywords = merged.Count > 0
                    ? string.Join(", ", merged)
                    : "(automático)";
                session.Steps[1].Detail = session.UserKeywords;
                session.ProgressPercent = 20;
            });

            // Step 3: Map affected files
            await RunStepAsync(session, 2, () =>
            {
                if (targetFolder is not null && Directory.Exists(targetFolder))
                {
                    var files = ScanAffectedFiles(taskType, targetFolder);
                    session.FileCount = files.Count;
                    foreach (var f in files.Take(8))
                        session.AffectedFiles.Add(f);
                    if (files.Count > 8)
                        session.AffectedFiles.Add($"... +{files.Count - 8} arquivo(s)");
                    session.Steps[2].Detail = $"{files.Count} arquivo(s)";
                }
                else
                {
                    session.Steps[2].Detail = targetFolder is null
                        ? "Pasta não especificada"
                        : "Pasta não encontrada";
                }
                session.ProgressPercent = 35;
            });

            // Step 4: Analyze stack
            await RunStepAsync(session, 3, () =>
            {
                session.StackAnalysis = targetFolder is not null
                    ? AnalyzeStack(targetFolder)
                    : "N/A";
                session.Steps[3].Detail = session.StackAnalysis;
                session.ProgressPercent = 45;
            });

            // Step 5: Build improved prompt using user keywords
            await RunStepAsync(session, 4, () =>
            {
                var keywords = aiResponse.UserKeywords ?? [];
                session.ImprovedPrompt = BuildImprovedPrompt(taskType, keywords, aiResponse.ExecutionPlan);
                session.Steps[4].Detail = "Prompt gerado";
                session.ProgressPercent = 55;
            });

            // Step 6: Execute via OpenClaw (real async work)
            session.Steps[5].Status = AgentStepStatus.Running;
            session.EstimatedTime = EstimateTime(taskType, session.FileCount);

            var metadata = new TaskMetadata
            {
                RequestId = session.SessionId,
                PetState = PetStates.Happy,
                TaskType = taskType,
                TargetFolder = targetFolder,
                ContextPrompt = session.ImprovedPrompt,
                TimeoutSeconds = 120,
                XpMultiplier = 1.0f
            };

            session.OpenClawRequest = $"Task: {taskType}\nFolder: {targetFolder ?? "N/A"}\nTimeout: 120s\nRequestId: {session.SessionId:N}";
            session.ProgressPercent = 65;

            var result = await _taskExecutor.ExecuteAsync(metadata, aiResponse.ExecutionPlan ?? string.Empty);

            session.Steps[5].Status = AgentStepStatus.Completed;
            session.Steps[5].Detail = $"{result.ExecutionTimeMs}ms";
            session.ProgressPercent = 85;

            // Step 7: Process results
            await RunStepAsync(session, 6, () =>
            {
                session.ProgressPercent = 100;
                if (result.IsSuccess)
                {
                    session.ResultSummary = $"✅ {result.Message}\n+{result.XpEarned} XP  ·  ⏱️ {result.ExecutionTimeMs}ms";
                    session.IsCompleted = true;
                    session.Steps[6].Detail = $"+{result.XpEarned} XP";
                }
                else
                {
                    session.ResultSummary = $"❌ {result.Message}" +
                        (result.ErrorCode is not null ? $"\nCódigo: {result.ErrorCode}" : "");
                    session.IsFailed = true;
                    session.Steps[6].Detail = result.ErrorCode ?? "Falha";
                }
                session.IsRunning = false;
            });

            if (!string.IsNullOrWhiteSpace(result.DetailedOutput))
            {
                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = result.DetailedOutput
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent session failed for {TaskType}", taskType);
            FailRemainingSteps(session, ex.Message);
            session.ResultSummary = $"❌ Erro: {ex.Message}";
            session.IsFailed = true;
            session.IsRunning = false;
        }
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(UserMessage) && !IsGenerating;

    partial void OnUserMessageChanged(string value) => SendMessageCommand.NotifyCanExecuteChanged();
    partial void OnIsGeneratingChanged(bool value) => SendMessageCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Builds the full prompt by combining the base system prompt, the selected agent overlay,
    /// the selected prompt template, and the user's message.
    /// </summary>
    private string BuildFullPrompt(string userText)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(SystemPrompt);

        // Append agent profile overlay
        if (SelectedAgent is { SystemPromptOverlay.Length: > 0 } agent)
        {
            sb.AppendLine();
            sb.AppendLine($"PERFIL DO AGENTE ({agent.Name}): {agent.SystemPromptOverlay}");

            if (agent.DefaultTaskType is not null)
                sb.AppendLine($"Tipo de task preferido para este agente: {agent.DefaultTaskType}");

            if (agent.FocusExtensions is { Count: > 0 })
                sb.AppendLine($"Extensões de foco: {string.Join(", ", agent.FocusExtensions)}");
        }

        // Append prompt template
        if (SelectedPrompt is { PromptText.Length: > 0 } prompt)
        {
            sb.AppendLine();
            sb.AppendLine($"CONTEXTO DE ANÁLISE ({prompt.Name}): {prompt.PromptText}");
        }

        sb.AppendLine();
        sb.AppendLine($"Mensagem do usuário: {userText}");

        if (!string.IsNullOrWhiteSpace(TargetFolder))
            sb.AppendLine($"Pasta de trabalho atual: {TargetFolder}");

        return sb.ToString();
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        try
        {
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow is null) return;

            var folder = await mainWindow.StorageProvider.OpenFolderPickerAsync(
                new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Selecionar pasta de trabalho",
                    AllowMultiple = false
                });

            if (folder is { Count: > 0 })
            {
                TargetFolder = folder[0].Path.LocalPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open folder picker");
        }
    }

    [RelayCommand]
    private void ClearFolder()
    {
        TargetFolder = string.Empty;
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        Messages.Add(new ChatMessage
        {
            Role = "system",
            Content = "🗑️ Chat limpo. Envie uma nova mensagem para começar."
        });
    }

    [RelayCommand]
    private async Task ReconnectAsync()
    {
        AiStatusText = "Reconectando...";
        await CheckConnectionAsync();
    }

    [RelayCommand]
    private async Task CopyMessageAsync(ChatMessage? message)
    {
        if (message is null) return;

        var text = message.IsAgentTask && message.AgentSession is { } session
            ? BuildAgentSessionText(session)
            : message.Content;

        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            var clipboard = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow?.Clipboard
                : null;

            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy message to clipboard");
        }
    }

    private static string BuildAgentSessionText(AgentSession session)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{session.TaskIcon} {session.TaskTitle} ({session.TaskType})");

        if (!string.IsNullOrWhiteSpace(session.UserKeywords))
            sb.AppendLine($"Keywords: {session.UserKeywords}");
        if (!string.IsNullOrWhiteSpace(session.StackAnalysis))
            sb.AppendLine($"Stack: {session.StackAnalysis}");
        if (!string.IsNullOrWhiteSpace(session.EstimatedTime))
            sb.AppendLine($"Estimado: {session.EstimatedTime}");

        if (session.AffectedFiles.Count > 0)
        {
            sb.AppendLine($"\nArquivos ({session.FileCount}):");
            foreach (var f in session.AffectedFiles)
                sb.AppendLine($"  {f}");
        }

        sb.AppendLine("\nEtapas:");
        foreach (var step in session.Steps)
            sb.AppendLine($"  {step.StatusIcon} {step.Icon} {step.Title}{(step.Detail is not null ? $" — {step.Detail}" : "")}");

        if (!string.IsNullOrWhiteSpace(session.ImprovedPrompt))
            sb.AppendLine($"\nPrompt: {session.ImprovedPrompt}");
        if (!string.IsNullOrWhiteSpace(session.ResultSummary))
            sb.AppendLine($"\nResultado: {session.ResultSummary}");

        return sb.ToString().TrimEnd();
    }

    #region Agent Helpers

    private static async Task RunStepAsync(AgentSession session, int stepIndex, Action action)
    {
        session.Steps[stepIndex].Status = AgentStepStatus.Running;
        await Task.Delay(120);
        try
        {
            action();
            session.Steps[stepIndex].Status = AgentStepStatus.Completed;
        }
        catch
        {
            session.Steps[stepIndex].Status = AgentStepStatus.Failed;
            throw;
        }
    }

    private static void FailRemainingSteps(AgentSession session, string reason)
    {
        foreach (var step in session.Steps)
        {
            if (step.Status is AgentStepStatus.Pending or AgentStepStatus.Running)
            {
                step.Status = AgentStepStatus.Failed;
                step.Detail = reason;
            }
        }
    }

    private static (string Icon, string Title) GetTaskInfo(string taskType) => taskType switch
    {
        TaskTypes.CodeReview => ("🔍", "Code Review"),
        TaskTypes.FileCleanup => ("🧹", "File Cleanup"),
        TaskTypes.GitCommit => ("📝", "Git Commit"),
        TaskTypes.TestRun => ("🧪", "Test Run"),
        TaskTypes.BuildProject => ("🔨", "Build Project"),
        TaskTypes.GenerateDocs => ("📚", "Generate Docs"),
        TaskTypes.Refactor => ("♻️", "Refactor"),
        TaskTypes.SecurityScan => ("🛡️", "Security Scan"),
        TaskTypes.Custom => ("🎯", "Custom Task"),
        _ => ("⚡", taskType)
    };

    private static List<string> ScanAffectedFiles(string taskType, string folder)
    {
        try
        {
            return taskType switch
            {
                TaskTypes.CodeReview or TaskTypes.GenerateDocs or TaskTypes.Refactor =>
                    new[] { "*.cs", "*.ts", "*.tsx", "*.js", "*.jsx", "*.py", "*.prisma", "*.json", "*.yaml", "*.yml", "*.sql", "*.razor", "*.vue", "*.go", "*.rs", "*.java", "*.kt" }
                        .SelectMany(ext =>
                        {
                            try { return Directory.GetFiles(folder, ext, SearchOption.AllDirectories); }
                            catch { return Array.Empty<string>(); }
                        })
                        .Select(f => Path.GetRelativePath(folder, f))
                        .Where(f => !f.Contains($"bin{Path.DirectorySeparatorChar}") &&
                                    !f.Contains($"obj{Path.DirectorySeparatorChar}") &&
                                    !f.Contains($"node_modules{Path.DirectorySeparatorChar}"))
                        .Distinct()
                        .ToList(),

                TaskTypes.FileCleanup =>
                    new[] { "bin", "obj", ".vs", "node_modules", "packages", "TestResults" }
                        .SelectMany(p =>
                        {
                            try { return Directory.GetDirectories(folder, p, SearchOption.AllDirectories); }
                            catch { return Array.Empty<string>(); }
                        })
                        .Select(d => Path.GetRelativePath(folder, d) + Path.DirectorySeparatorChar)
                        .ToList(),

                TaskTypes.BuildProject or TaskTypes.SecurityScan =>
                    Directory.GetFiles(folder, "*.csproj", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(folder, f))
                        .ToList(),

                TaskTypes.TestRun =>
                    Directory.GetFiles(folder, "*.csproj", SearchOption.AllDirectories)
                        .Where(f => f.Contains("Test", StringComparison.OrdinalIgnoreCase))
                        .Select(f => Path.GetRelativePath(folder, f))
                        .ToList(),

                TaskTypes.GitCommit =>
                    Directory.Exists(Path.Combine(folder, ".git"))
                        ? ["(repositório git detectado)"]
                        : [],

                _ => []
            };
        }
        catch
        {
            return [];
        }
    }

    private static string AnalyzeStack(string folder)
    {
        var parts = new List<string>();
        try
        {
            var csprojFiles = Directory.GetFiles(folder, "*.csproj", SearchOption.AllDirectories);
            if (csprojFiles.Length > 0)
            {
                parts.Add($".NET ({csprojFiles.Length} projeto(s))");
                var content = File.ReadAllText(csprojFiles[0]);
                var match = System.Text.RegularExpressions.Regex.Match(
                    content, @"<TargetFramework>([^<]+)</TargetFramework>");
                if (match.Success)
                    parts.Add(match.Groups[1].Value);
            }
            if (File.Exists(Path.Combine(folder, "package.json")))
                parts.Add("Node.js");
            if (Directory.GetFiles(folder, "*.sln", SearchOption.TopDirectoryOnly).Length > 0)
                parts.Add("Solution");
        }
        catch { /* best-effort stack detection */ }
        return parts.Count > 0 ? string.Join(" · ", parts) : "Não identificado";
    }

    private static string EstimateTime(string taskType, int fileCount)
    {
        var seconds = taskType switch
        {
            TaskTypes.CodeReview => Math.Max(5, fileCount * 2),
            TaskTypes.FileCleanup => 3,
            TaskTypes.GitCommit => 2,
            TaskTypes.TestRun => 30,
            TaskTypes.BuildProject => 15,
            TaskTypes.GenerateDocs => Math.Max(5, fileCount),
            TaskTypes.Refactor => 20,
            TaskTypes.SecurityScan => 10,
            TaskTypes.Custom => 15,
            _ => 10
        };
        return seconds < 60 ? $"~{seconds}s" : $"~{seconds / 60}m {seconds % 60}s";
    }

    private static string BuildImprovedPrompt(string taskType, List<string> keywords, string? executionPlan)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(taskType switch
        {
            TaskTypes.CodeReview => "Analise o código buscando: bugs, segurança, performance e boas práticas.",
            TaskTypes.FileCleanup => "Limpe diretórios de build e temporários do projeto.",
            TaskTypes.GitCommit => "Valide o formato do commit seguindo Conventional Commits.",
            TaskTypes.TestRun => "Execute os testes unitários e reporte resultados.",
            TaskTypes.BuildProject => "Compile o projeto e verifique erros de compilação.",
            TaskTypes.GenerateDocs => "Identifique membros públicos sem documentação XML.",
            TaskTypes.Refactor => "Analise e aplique refatorações de estilo e formatação.",
            TaskTypes.SecurityScan => "Verifique pacotes NuGet vulneráveis e desatualizados.",
            TaskTypes.Custom => "Execute a tarefa personalizada conforme descrito.",
            _ => "Execute a tarefa solicitada."
        });

        if (keywords.Count > 0)
            sb.AppendLine($"Foco especial em: {string.Join(", ", keywords)}.");

        if (!string.IsNullOrWhiteSpace(executionPlan))
            sb.AppendLine($"Contexto adicional: {executionPlan}");

        return sb.ToString().Trim();
    }

    #endregion
}
