using Avalonia.Controls;
using Avalonia.Input;
using SlimeNexus.UI.ViewModels.Installer;

namespace SlimeNexus.UI.Views.Installer;

public partial class ModelSelectionStepView : UserControl
{
    public ModelSelectionStepView()
    {
        InitializeComponent();
    }

    private void OnModelCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && 
            border.DataContext is InstallerModelOption model &&
            DataContext is ModelSelectionStepViewModel vm)
        {
            vm.SelectedModel = model;
        }
    }
}
