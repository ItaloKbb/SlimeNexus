using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SlimeNexus.UI.ViewModels;

namespace SlimeNexus.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.GetService<MainWindowViewModel>();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Hide instead of close (keep running in system tray)
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
