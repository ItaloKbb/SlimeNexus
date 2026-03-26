using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SlimeNexus.UI.ViewModels;

namespace SlimeNexus.UI.Views;

public partial class PetStatusView : Window
{
    public PetStatusView()
    {
        InitializeComponent();
        DataContext = App.GetService<PetStatusViewModel>();
    }
}
