using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.UI.ViewModels;

/// <summary>
/// Represents a single chat message.
/// </summary>
public sealed class ChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public bool IsSystem => Role == "system";
}

/// <summary>
/// ViewModel for the AI Chat view.
/// Allows testing conversations with the local AI and OpenClaw actions.
/// </summary>
public partial class ChatViewModel : ViewModelBase
{
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<ChatViewModel> _logger;

    [ObservableProperty]
    private string _userMessage = string.Empty;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _isAiConnected;

    [ObservableProperty]
    private string _aiStatusText = "Verificando...";

    [ObservableProperty]
    private string _modelName = "...";

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public ChatViewModel(
        IAiProvider aiProvider,
        ILogger<ChatViewModel> logger)
    {
        _aiProvider = aiProvider;
        _logger = logger;

        ModelName = _aiProvider.ProviderName;

        Messages.Add(new ChatMessage
        {
            Role = "system",
            Content = "👋 Bem-vindo ao SlimeNexus Chat! Envie uma mensagem para conversar com a IA local."
        });

        _ = CheckConnectionAsync();
    }

    private async Task CheckConnectionAsync()
    {
        try
        {
            IsAiConnected = await _aiProvider.IsAvailableAsync();
            AiStatusText = IsAiConnected ? "Conectado" : "Desconectado";
            ModelName = _aiProvider.ProviderName;
        }
        catch
        {
            IsAiConnected = false;
            AiStatusText = "Erro";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserMessage)) return;

        var userText = UserMessage.Trim();
        UserMessage = string.Empty;

        Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = userText
        });

        IsGenerating = true;

        try
        {
            _logger.LogDebug("Sending message to AI: {Message}", userText);
            var response = await _aiProvider.GenerateAsync(userText);

            Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI response");
            Messages.Add(new ChatMessage
            {
                Role = "system",
                Content = $"❌ Erro: {ex.Message}"
            });
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(UserMessage) && !IsGenerating;

    partial void OnUserMessageChanged(string value) => SendMessageCommand.NotifyCanExecuteChanged();
    partial void OnIsGeneratingChanged(bool value) => SendMessageCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        Messages.Add(new ChatMessage
        {
            Role = "system",
            Content = "🗑️ Chat limpo. Envie uma nova mensagem para começar."
        });
    }

    [RelayCommand]
    private async Task ReconnectAsync()
    {
        AiStatusText = "Reconectando...";
        await CheckConnectionAsync();
    }
}
