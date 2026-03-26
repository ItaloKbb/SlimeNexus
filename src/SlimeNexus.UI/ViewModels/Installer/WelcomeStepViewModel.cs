using CommunityToolkit.Mvvm.ComponentModel;
using SlimeNexus.UI.Services;

namespace SlimeNexus.UI.ViewModels.Installer;

/// <summary>
/// Welcome screen - First step of the installer wizard.
/// </summary>
public sealed partial class WelcomeStepViewModel : InstallerStepViewModelBase
{
    [ObservableProperty]
    private string _appVersion = InstallerService.AppVersion;

    public WelcomeStepViewModel(InstallerWizardViewModel wizard) : base(wizard)
    {
        Title = "Bem-vindo ao SlimeNexus!";
        Description = "Seu companheiro de IA pessoal para desktop";
        Icon = "🎮";
        CanGoBack = false; // First step - can't go back
    }

    public string[] Features { get; } =
    [
        "🤖 IA Local - Processamento privado sem enviar dados para nuvem",
        "📊 Monitoramento de Hardware - Acompanhe CPU, GPU, RAM em tempo real",
        "🎯 Tarefas Automatizadas - Execute comandos e scripts com IA",
        "🐾 Tamagotchi Virtual - Cuide do seu pet digital que evolui com você",
        "🔒 Privacidade Total - Seus dados ficam no seu computador"
    ];
}
