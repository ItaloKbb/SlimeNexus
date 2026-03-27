using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SlimeNexus.UI.ViewModels;

namespace SlimeNexus.UI.Views;

public partial class PetTabView : UserControl
{
    public PetTabView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = App.GetService<PetStatusViewModel>();
    }
}
