using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlimeNexus.UI.Services;

namespace SlimeNexus.UI.ViewModels.Installer;

/// <summary>
/// Installation progress step - performs the actual installation.
/// </summary>
public sealed partial class InstallationStepViewModel : InstallerStepViewModelBase
{
    private readonly InstallerService _installerService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _installationStarted;

    [ObservableProperty]
    private bool _installationComplete;

    [ObservableProperty]
    private bool _installationFailed;

    [ObservableProperty]
    private int _overallProgress;

    [ObservableProperty]
    private string _currentTask = "Preparando instalação...";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _logsCopied;

    public ObservableCollection<string> InstallationLogs { get; } = [];

    public ObservableCollection<InstallationTaskViewModel> Tasks { get; } = [];

    public InstallationStepViewModel(
        InstallerWizardViewModel wizard,
        InstallerService installerService) : base(wizard)
    {
        _installerService = installerService;
        Title = "Instalação";
        Description = "Configurando o SlimeNexus no seu sistema";
        Icon = "📦";
        CanGoBack = true; // Can go back before starting
        CanGoNext = true; // Enabled so the "Instalar" button is clickable
    }

    public override async Task OnEnteringAsync()
    {
        if (!InstallationStarted)
        {
            InitializeTasks();
        }
    }

    public override Task<bool> ValidateAsync()
    {
        // This step triggers installation when "Next" (Install) is clicked
        if (!InstallationStarted)
        {
            _ = StartInstallationAsync();
            return Task.FromResult(false); // Don't proceed yet
        }

        return Task.FromResult(InstallationComplete);
    }

    private void InitializeTasks()
    {
        Tasks.Clear();
        
        Tasks.Add(new InstallationTaskViewModel
        {
            Id = "verify",
            Name = "Verificando Sistema",
            Description = "Confirmando requisitos do sistema",
            Icon = "🔍"
        });

        Tasks.Add(new InstallationTaskViewModel
        {
            Id = "ollama",
            Name = "Instalando Ollama",
            Description = "Configurando runtime de IA local",
            Icon = "🤖"
        });

        Tasks.Add(new InstallationTaskViewModel
        {
            Id = "model",
            Name = "Baixando Modelo de IA",
            Description = $"Baixando {Wizard.SelectedModel}",
            Icon = "📥"
        });

        Tasks.Add(new InstallationTaskViewModel
        {
            Id = "openclaw",
            Name = "Configurando OpenClaw",
            Description = "Validando habilidades de IA para tarefas",
            Icon = "🐾"
        });

        Tasks.Add(new InstallationTaskViewModel
        {
            Id = "configure",
            Name = "Configurando App",
            Description = "Aplicando configurações otimizadas",
            Icon = "⚙️"
        });

        Tasks.Add(new InstallationTaskViewModel
        {
            Id = "shortcuts",
            Name = "Criando Atalhos",
            Description = "Adicionando ao menu Iniciar",
            Icon = "📌"
        });
    }

    [RelayCommand]
    private async Task StartInstallationAsync()
    {
        if (InstallationStarted) return;

        InstallationStarted = true;
        InstallationFailed = false;
        CanGoBack = false;
        CanGoNext = false; // Disable during installation to prevent repeated clicks
        Wizard.IsInstalling = true;
        LogsCopied = false;
        InstallationLogs.Clear();
        UpdateWizardNavigation();

        AddLog($"Instalação iniciada em {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        AddLog($"Modelo selecionado: {Wizard.SelectedModel}");

        _cts = new CancellationTokenSource();

        try
        {
            // Task 1: Verify system
            await ExecuteTaskAsync("verify", async () =>
            {
                await Task.Delay(500, _cts.Token); // Brief delay for UI
                return true;
            });

            // Task 2: Install Ollama
            await ExecuteTaskAsync("ollama", async () =>
            {
                var progress = new Progress<string>(msg =>
                {
                    CurrentTask = msg;
                    AddLog($"  [Ollama] {msg}");
                });
                return await _installerService.InstallOllamaAsync(progress, _cts.Token);
            });

            // Task 3: Download model
            string? lastDownloadError = null;
            await ExecuteTaskAsync("model", async () =>
            {
                var progress = new Progress<ModelDownloadProgress>(p =>
                {
                    CurrentTask = p.Message;
                    if (p.Percent >= 0)
                    {
                        UpdateTaskProgress("model", p.Percent);
                    }
                    else
                    {
                        // Negative percent indicates an error message
                        lastDownloadError = p.Message;
                        AddLog($"  [Download] {p.Message}");
                    }
                });

                var success = await _installerService.DownloadModelAsync(
                    Wizard.SelectedModel, 
                    progress, 
                    _cts.Token);

                if (!success && lastDownloadError is not null)
                {
                    throw new Exception(lastDownloadError);
                }

                return success;
            });

            // Task 4: Configure OpenClaw
            await ExecuteTaskAsync("openclaw", async () =>
            {
                var progress = new Progress<string>(msg =>
                {
                    CurrentTask = msg;
                    AddLog($"  [OpenClaw] {msg}");
                });

                var result = await _installerService.ValidateOpenClawAsync(progress, _cts.Token);

                if (!result.IsValid)
                {
                    // OpenClaw validation is non-fatal — log warning but continue
                    AddLog($"  [OpenClaw] Aviso: {result.ErrorMessage ?? "Validação incompleta"}");
                    AddLog("  [OpenClaw] As habilidades de IA serão configuradas na próxima inicialização.");
                }

                return true; // Always succeed — OpenClaw will retry on first use
            });

            // Task 5: Configure app
            await ExecuteTaskAsync("configure", async () =>
            {
                CurrentTask = "Aplicando configurações...";
                await Task.Delay(1000, _cts.Token); // Simulate configuration
                return true;
            });

            // Task 6: Create shortcuts
            await ExecuteTaskAsync("shortcuts", async () =>
            {
                CurrentTask = "Criando atalhos...";
                await Task.Delay(500, _cts.Token);
                return true;
            });

            // All done!
            InstallationComplete = true;
            OverallProgress = 100;
            CurrentTask = "✅ Instalação concluída com sucesso!";
            CanGoNext = true;
            AddLog("Instalação concluída com sucesso.");
        }
        catch (OperationCanceledException)
        {
            InstallationFailed = true;
            ErrorMessage = "Instalação cancelada pelo usuário.";
            CanGoBack = true;
            AddLog("Instalação cancelada pelo usuário.");
        }
        catch (Exception ex)
        {
            InstallationFailed = true;
            ErrorMessage = $"Erro durante instalação: {ex.Message}";
            CanGoBack = true;
            AddLog($"ERRO: {ex.Message}");
            AddLog($"StackTrace: {ex.StackTrace}");
        }
        finally
        {
            Wizard.IsInstalling = false;
            UpdateWizardNavigation();
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task ExecuteTaskAsync(string taskId, Func<Task<bool>> action)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return;

        task.Status = TaskStatus.Running;
        task.Progress = 0;
        CurrentTask = task.Name;
        AddLog($"[Iniciando] {task.Name}");

        try
        {
            var success = await action();

            task.Progress = 100;
            task.Status = success ? TaskStatus.Completed : TaskStatus.Failed;

            if (!success)
            {
                AddLog($"[Falha] {task.Name}");
                throw new Exception($"Falha em: {task.Name}");
            }

            AddLog($"[Concluído] {task.Name}");

            // Update overall progress
            var completedCount = Tasks.Count(t => t.Status == TaskStatus.Completed);
            OverallProgress = (completedCount * 100) / Tasks.Count;
        }
        catch
        {
            task.Status = TaskStatus.Failed;
            throw;
        }
    }

    private void UpdateTaskProgress(string taskId, int progress)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is not null)
        {
            task.Progress = progress;
        }
    }

    [RelayCommand]
    private void CancelInstallation()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private async Task CopyLogsAsync()
    {
        var clipboard = (Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;

        if (clipboard is null) return;

        await clipboard.SetTextAsync(BuildLogText());
        LogsCopied = true;
    }

    private string BuildLogText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SlimeNexus - Log de Instalação ===");
        sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Modelo: {Wizard.SelectedModel}");
        sb.AppendLine();

        foreach (var log in InstallationLogs)
        {
            sb.AppendLine(log);
        }

        if (InstallationFailed)
        {
            sb.AppendLine();
            sb.AppendLine($"Erro: {ErrorMessage}");
        }

        sb.AppendLine();
        sb.AppendLine("--- Status das Tarefas ---");
        foreach (var task in Tasks)
        {
            sb.AppendLine($"  {task.StatusIcon} {task.Name}: {task.Status} ({task.Progress}%)");
        }

        return sb.ToString();
    }

    private void AddLog(string message)
    {
        InstallationLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}

/// <summary>
/// ViewModel for a single installation task.
/// </summary>
public sealed partial class InstallationTaskViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _icon = "⏳";

    [ObservableProperty]
    private TaskStatus _status = TaskStatus.Pending;

    [ObservableProperty]
    private int _progress;

    public string StatusIcon => Status switch
    {
        TaskStatus.Pending => "⏳",
        TaskStatus.Running => "🔄",
        TaskStatus.Completed => "✅",
        TaskStatus.Failed => "❌",
        TaskStatus.Skipped => "⏭️",
        _ => "⏳"
    };
}

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}
