using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SlimeNexus.UI.ViewModels;

namespace SlimeNexus.UI.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = App.GetService<ChatViewModel>();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChatViewModel vm && vm.SendMessageCommand.CanExecute(null))
        {
            vm.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }
}
