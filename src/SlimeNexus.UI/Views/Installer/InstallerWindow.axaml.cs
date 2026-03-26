using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SlimeNexus.UI.ViewModels.Installer;

namespace SlimeNexus.UI.Views.Installer;

public partial class InstallerWindow : Window
{
    public InstallerWindow()
    {
        InitializeComponent();

        if (App.Services is not null)
        {
            var viewModel = App.Services.GetRequiredService<InstallerWizardViewModel>();
            DataContext = viewModel;

            viewModel.OnCancelRequested += () => Close(false);
            viewModel.OnFinishRequested += () => Close(true);
        }
    }

    /// <summary>
    /// Gets the result of the installer wizard.
    /// True if installation completed successfully, False if cancelled.
    /// </summary>
    public bool? InstallationResult { get; private set; }

    private void Close(bool result)
    {
        InstallationResult = result;
        Close();
    }
}
