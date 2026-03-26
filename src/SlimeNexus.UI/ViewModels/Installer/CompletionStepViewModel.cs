using CommunityToolkit.Mvvm.ComponentModel;

namespace SlimeNexus.UI.ViewModels.Installer;

/// <summary>
/// Completion step - final screen after successful installation.
/// </summary>
public sealed partial class CompletionStepViewModel : InstallerStepViewModelBase
{
    [ObservableProperty]
    private bool _launchOnClose = true;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _selectedModelDisplay = string.Empty;

    [ObservableProperty]
    private string _performanceTierDisplay = string.Empty;

    public CompletionStepViewModel(InstallerWizardViewModel wizard) : base(wizard)
    {
        Title = "Instalação Concluída!";
        Description = "O SlimeNexus está pronto para uso";
        Icon = "🎉";
        CanGoBack = false; // No going back after completion
    }

    public override Task OnEnteringAsync()
    {
        BuildSummary();
        return Task.CompletedTask;
    }

    private void BuildSummary()
    {
        SelectedModelDisplay = Wizard.SelectedModel;
        PerformanceTierDisplay = Wizard.BenchmarkResults?.PerformanceTier ?? "Não avaliado";

        var sysReq = Wizard.SystemRequirements;
        var benchmark = Wizard.BenchmarkResults;

        SummaryText = $"""
            ✅ Instalação realizada com sucesso!
            
            📋 Resumo da Configuração:
            
            🖥️ Sistema: {sysReq?.CpuName ?? "N/A"}
            🎮 GPU: {sysReq?.GpuName ?? "N/A"}
            💾 RAM: {(sysReq?.TotalRamMb ?? 0) / 1024.0:F0} GB
            🧠 VRAM: {(sysReq?.TotalVramMb ?? 0) / 1024.0:F1} GB
            
            🤖 Modelo instalado: {Wizard.SelectedModel}
            ⚡ Performance: {benchmark?.PerformanceTier ?? "N/A"}
            📊 Score: {benchmark?.OverallScore ?? 0:F0}/100
            
            🎯 Próximos passos:
            • Seu pet Tamagotchi está te esperando!
            • O app ficará na bandeja do sistema
            • Acesse o dashboard via navegador
            """;
    }

    public string[] Tips { get; } =
    [
        "💡 O SlimeNexus fica na bandeja do sistema (área de notificação)",
        "🌐 Acesse o Tamagotchi web em: http://localhost:5000",
        "⚙️ Clique com botão direito no ícone da bandeja para opções",
        "📊 Veja o dashboard de hardware a qualquer momento",
        "🔄 Atualizações são baixadas automaticamente"
    ];
}
