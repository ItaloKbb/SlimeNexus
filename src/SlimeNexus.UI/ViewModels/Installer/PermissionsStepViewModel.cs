using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SlimeNexus.UI.ViewModels.Installer;

/// <summary>
/// Permissions confirmation step.
/// </summary>
public sealed partial class PermissionsStepViewModel : InstallerStepViewModelBase
{
    [ObservableProperty]
    private bool _acceptHardwareMonitoring = true;

    [ObservableProperty]
    private bool _acceptAutoStart = true;

    [ObservableProperty]
    private bool _acceptNetworkAccess = true;

    [ObservableProperty]
    private bool _acceptDataStorage = true;

    [ObservableProperty]
    private bool _allPermissionsAccepted;

    public ObservableCollection<PermissionItemViewModel> Permissions { get; } = [];

    public PermissionsStepViewModel(InstallerWizardViewModel wizard) : base(wizard)
    {
        Title = "Permissões e Privacidade";
        Description = "Revise as permissões necessárias para o funcionamento do app";
        Icon = "🔒";

        InitializePermissions();
    }

    private void InitializePermissions()
    {
        Permissions.Add(new PermissionItemViewModel
        {
            Icon = "📊",
            Title = "Monitoramento de Hardware",
            Description = "Acesso a informações de CPU, GPU, RAM e disco para otimização de IA e exibição do dashboard.",
            IsRequired = true,
            IsAccepted = true
        });

        Permissions.Add(new PermissionItemViewModel
        {
            Icon = "🚀",
            Title = "Iniciar com Windows",
            Description = "Permite que o SlimeNexus inicie automaticamente quando você ligar o computador.",
            IsRequired = false,
            IsAccepted = true
        });

        Permissions.Add(new PermissionItemViewModel
        {
            Icon = "🌐",
            Title = "Acesso à Rede Local",
            Description = "API local para integração com o Tamagotchi web. Comunicação apenas na rede local (localhost).",
            IsRequired = true,
            IsAccepted = true
        });

        Permissions.Add(new PermissionItemViewModel
        {
            Icon = "💾",
            Title = "Armazenamento de Dados",
            Description = "Salvar configurações e cache localmente. Nenhum dado é enviado para servidores externos.",
            IsRequired = true,
            IsAccepted = true
        });

        Permissions.Add(new PermissionItemViewModel
        {
            Icon = "🔄",
            Title = "Atualizações Automáticas",
            Description = "Verificar e baixar atualizações automaticamente para manter o app seguro e atualizado.",
            IsRequired = false,
            IsAccepted = true
        });

        // Subscribe to changes
        foreach (var permission in Permissions)
        {
            permission.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PermissionItemViewModel.IsAccepted))
                {
                    ValidatePermissions();
                }
            };
        }

        ValidatePermissions();
    }

    private void ValidatePermissions()
    {
        // All required permissions must be accepted
        AllPermissionsAccepted = Permissions
            .Where(p => p.IsRequired)
            .All(p => p.IsAccepted);

        CanGoNext = AllPermissionsAccepted;
        UpdateWizardNavigation();
    }

    public string PrivacyNote => """
        🔐 Compromisso de Privacidade
        
        • Todos os dados ficam no seu computador
        • IA processa localmente, sem enviar para nuvem
        • Código aberto e auditável
        • Você pode revogar permissões a qualquer momento
        """;
}

/// <summary>
/// ViewModel for a single permission item.
/// </summary>
public sealed partial class PermissionItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _icon = "✅";

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isRequired;

    [ObservableProperty]
    private bool _isAccepted;
}
