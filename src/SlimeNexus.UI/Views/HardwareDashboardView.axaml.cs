using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SlimeNexus.UI.ViewModels;

namespace SlimeNexus.UI.Views;

public partial class HardwareDashboardView : UserControl
{
    public HardwareDashboardView()
    {
        InitializeComponent();
        DataContext = App.GetService<HardwareDashboardViewModel>();
    }
}
