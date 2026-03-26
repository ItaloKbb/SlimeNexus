using CommunityToolkit.Mvvm.ComponentModel;

namespace SlimeNexus.UI.ViewModels;

/// <summary>
/// Base class for all ViewModels in the application.
/// Provides common functionality like property change notifications.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string? _busyMessage;

    /// <summary>
    /// Gets or sets whether the ViewModel is busy performing an operation.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// Gets or sets the message to display while busy.
    /// </summary>
    public string? BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    /// <summary>
    /// Sets the busy state with an optional message.
    /// </summary>
    protected void SetBusy(bool isBusy, string? message = null)
    {
        IsBusy = isBusy;
        BusyMessage = message;
    }
}
