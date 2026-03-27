using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SlimeNexus.UI.ViewModels;

namespace SlimeNexus.UI.Views;

public partial class TrayIconWindow : Window
{
    /// <summary>
    /// When true, the window will actually close instead of hiding to tray.
    /// Set this before calling Close() during app shutdown.
    /// </summary>
    public bool ForceClose { get; set; }

    public TrayIconWindow()
    {
        InitializeComponent();
        DataContext = App.GetService<TrayIconViewModel>();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!ForceClose)
        {
            // Hide to tray instead of closing
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
