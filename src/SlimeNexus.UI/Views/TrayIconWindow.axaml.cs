using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SlimeNexus.UI.ViewModels;

namespace SlimeNexus.UI.Views;

public partial class TrayIconWindow : Window
{
    public TrayIconWindow()
    {
        InitializeComponent();
        DataContext = App.GetService<TrayIconViewModel>();
    }
}
