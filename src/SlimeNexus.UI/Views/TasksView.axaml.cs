using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SlimeNexus.UI.ViewModels;

namespace SlimeNexus.UI.Views;

public partial class TasksView : UserControl
{
    public TasksView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = App.GetService<TasksViewModel>();
    }
}
