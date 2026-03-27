using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Enums;
using SlimeNexus.Core.Domain.Interfaces;
using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.UI.ViewModels;

/// <summary>
/// Represents a skill card for the Tasks UI.
/// </summary>
public sealed class SkillCard
{
    public required string TaskType { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Icon { get; init; }
    public required int BaseXp { get; init; }
    public bool RequiresFolder { get; init; } = true;
}

/// <summary>
/// Represents a completed task in the history.
/// </summary>
public sealed class TaskHistoryEntry
{
    public required string TaskType { get; init; }
    public required string Title { get; init; }
    public required string Icon { get; init; }
    public required bool IsSuccess { get; init; }
    public required string Message { get; init; }
    public required int XpEarned { get; init; }
    public required long ExecutionTimeMs { get; init; }
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.Now;
    public string TimeAgo => FormatTimeAgo(CompletedAt);

    private static string FormatTimeAgo(DateTimeOffset time)
    {
        var diff = DateTimeOffset.Now - time;
        if (diff.TotalSeconds < 60) return "agora";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m atrás";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h atrás";
        return $"{(int)diff.TotalDays}d atrás";
    }
}

/// <summary>
/// ViewModel for the Tasks tab. Displays skill cards and allows executing OpenClaw tasks.
/// </summary>
public partial class TasksViewModel : ViewModelBase
{
    private readonly ITaskExecutor _taskExecutor;
    private readonly ILogger<TasksViewModel> _logger;

    [ObservableProperty]
    private string _targetFolder = string.Empty;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _executionStatus = string.Empty;

    [ObservableProperty]
    private int _totalXpEarned;

    [ObservableProperty]
    private int _tasksCompleted;

    [ObservableProperty]
    private int _tasksFailed;

    [ObservableProperty]
    private SkillCard? _selectedSkill;

    [ObservableProperty]
    private string? _lastResultMessage;

    [ObservableProperty]
    private bool _lastResultSuccess;

    [ObservableProperty]
    private bool _hasLastResult;

    public ObservableCollection<SkillCard> Skills { get; } =
    [
        new SkillCard
        {
            TaskType = TaskTypes.CodeReview,
            Title = "Code Review",
            Description = "Revisão de código assistida por IA",
            Icon = "🔍",
            BaseXp = 25
        },
        new SkillCard
        {
            TaskType = TaskTypes.FileCleanup,
            Title = "File Cleanup",
            Description = "Limpar bin, obj, node_modules e mais",
            Icon = "🧹",
            BaseXp = 15
        },
        new SkillCard
        {
            TaskType = TaskTypes.GitCommit,
            Title = "Git Commit",
            Description = "Validar formato do commit (Conventional Commits)",
            Icon = "📝",
            BaseXp = 20
        },
        new SkillCard
        {
            TaskType = TaskTypes.TestRun,
            Title = "Test Run",
            Description = "Executar testes unitários do projeto",
            Icon = "🧪",
            BaseXp = 30
        },
        new SkillCard
        {
            TaskType = TaskTypes.BuildProject,
            Title = "Build Project",
            Description = "Compilar o projeto e verificar erros",
            Icon = "🔨",
            BaseXp = 15
        },
        new SkillCard
        {
            TaskType = TaskTypes.GenerateDocs,
            Title = "Generate Docs",
            Description = "Escanear membros públicos sem documentação",
            Icon = "📚",
            BaseXp = 20
        },
        new SkillCard
        {
            TaskType = TaskTypes.Refactor,
            Title = "Refactor",
            Description = "Analisar e aplicar refatoração de código",
            Icon = "♻️",
            BaseXp = 25
        },
        new SkillCard
        {
            TaskType = TaskTypes.SecurityScan,
            Title = "Security Scan",
            Description = "Verificar pacotes vulneráveis e desatualizados",
            Icon = "🛡️",
            BaseXp = 30
        }
    ];

    public ObservableCollection<TaskHistoryEntry> History { get; } = [];

    public TasksViewModel(
        ITaskExecutor taskExecutor,
        ILogger<TasksViewModel> logger)
    {
        _taskExecutor = taskExecutor;
        _logger = logger;
    }

    [RelayCommand]
    private async Task ExecuteSkillAsync(SkillCard? skill)
    {
        if (skill is null) return;

        if (skill.RequiresFolder && string.IsNullOrWhiteSpace(TargetFolder))
        {
            LastResultMessage = "Selecione uma pasta antes de executar.";
            LastResultSuccess = false;
            HasLastResult = true;
            return;
        }

        SelectedSkill = skill;
        IsExecuting = true;
        ExecutionStatus = $"Executando {skill.Title}...";
        HasLastResult = false;

        try
        {
            var metadata = new TaskMetadata
            {
                RequestId = Guid.NewGuid(),
                PetState = PetStates.Happy,
                TaskType = skill.TaskType,
                TargetFolder = string.IsNullOrWhiteSpace(TargetFolder) ? null : TargetFolder,
                TimeoutSeconds = 120,
                XpMultiplier = 1.0f
            };

            _logger.LogInformation("Executing skill {TaskType} on {Folder}",
                skill.TaskType, metadata.TargetFolder ?? "(none)");

            var result = await _taskExecutor.ExecuteAsync(metadata, string.Empty);

            History.Insert(0, new TaskHistoryEntry
            {
                TaskType = skill.TaskType,
                Title = skill.Title,
                Icon = skill.Icon,
                IsSuccess = result.IsSuccess,
                Message = result.Message,
                XpEarned = result.XpEarned,
                ExecutionTimeMs = result.ExecutionTimeMs,
                CompletedAt = result.CompletedAt
            });

            if (result.IsSuccess)
            {
                TotalXpEarned += result.XpEarned;
                TasksCompleted++;
                LastResultMessage = $"✓ {result.Message} (+{result.XpEarned} XP)";
                LastResultSuccess = true;
            }
            else
            {
                TasksFailed++;
                LastResultMessage = $"✗ {result.Message}";
                LastResultSuccess = false;
            }

            HasLastResult = true;
            ExecutionStatus = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Skill execution failed: {TaskType}", skill.TaskType);
            TasksFailed++;
            LastResultMessage = $"✗ Erro: {ex.Message}";
            LastResultSuccess = false;
            HasLastResult = true;
            ExecutionStatus = string.Empty;
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        try
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel is null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Selecione a pasta do projeto",
                    AllowMultiple = false
                });

            if (folders.Count > 0)
            {
                TargetFolder = folders[0].Path.LocalPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open folder picker");
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        History.Clear();
    }
}
