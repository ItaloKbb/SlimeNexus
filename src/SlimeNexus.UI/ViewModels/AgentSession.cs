using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SlimeNexus.UI.ViewModels;

/// <summary>
/// Status of an individual agent execution step.
/// </summary>
public enum AgentStepStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

/// <summary>
/// Represents a single step in the agent's execution pipeline.
/// Each step transitions through: Pending → Running → Completed/Failed.
/// </summary>
public partial class AgentStep : ObservableObject
{
    public required string Title { get; init; }
    public required string Icon { get; init; }

    [ObservableProperty]
    private AgentStepStatus _status = AgentStepStatus.Pending;

    [ObservableProperty]
    private string? _detail;

    public string StatusIcon => Status switch
    {
        AgentStepStatus.Pending => "⬜",
        AgentStepStatus.Running => "⏳",
        AgentStepStatus.Completed => "✅",
        AgentStepStatus.Failed => "❌",
        _ => "⬜"
    };

    partial void OnStatusChanged(AgentStepStatus value)
    {
        OnPropertyChanged(nameof(StatusIcon));
    }
}

/// <summary>
/// Represents a full agent execution session displayed as a card in the chat.
/// Tracks the task lifecycle: identification → analysis → execution → results.
/// Inspired by GitHub Copilot's agent experience.
/// </summary>
public partial class AgentSession : ObservableObject
{
    public Guid SessionId { get; init; } = Guid.NewGuid();

    public required string TaskType { get; init; }
    public required string TaskIcon { get; init; }
    public required string TaskTitle { get; init; }

    [ObservableProperty]
    private string _userKeywords = string.Empty;

    [ObservableProperty]
    private string _improvedPrompt = string.Empty;

    [ObservableProperty]
    private bool _isRunning = true;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _isFailed;

    [ObservableProperty]
    private string _estimatedTime = "Calculando...";

    [ObservableProperty]
    private string? _stackAnalysis;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string? _resultSummary;

    [ObservableProperty]
    private string? _openClawRequest;

    [ObservableProperty]
    private int _fileCount;

    public ObservableCollection<string> AffectedFiles { get; } = [];
    public ObservableCollection<AgentStep> Steps { get; } = [];

    public string StatusIcon => IsCompleted ? "✅" : IsFailed ? "❌" : "⚙️";
    public string StatusText => IsCompleted ? "Concluído" : IsFailed ? "Falhou" : "Executando...";
    public bool HasAffectedFiles => FileCount > 0;

    partial void OnIsCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnIsFailedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnFileCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasAffectedFiles));
    }
}
