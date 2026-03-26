using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Enums;

namespace SlimeNexus.UI.ViewModels;

/// <summary>
/// ViewModel for the Pet Status view.
/// </summary>
public partial class PetStatusViewModel : ViewModelBase
{
    private readonly ILogger<PetStatusViewModel> _logger;

    [ObservableProperty]
    private string _petName = "Slimey";

    [ObservableProperty]
    private string _petState = PetStates.Happy;

    [ObservableProperty]
    private string _petStateEmoji = "😊";

    [ObservableProperty]
    private int _level = 5;

    [ObservableProperty]
    private int _currentXp = 350;

    [ObservableProperty]
    private int _xpToNextLevel = 500;

    [ObservableProperty]
    private int _happinessPercent = 85;

    [ObservableProperty]
    private int _energyPercent = 70;

    [ObservableProperty]
    private int _tasksCompletedToday = 3;

    public PetStatusViewModel(ILogger<PetStatusViewModel> logger)
    {
        _logger = logger;
        UpdateStateEmoji();
    }

    public double XpProgress => (double)CurrentXp / XpToNextLevel * 100;
    
    public string XpText => $"{CurrentXp} / {XpToNextLevel}";
    
    public string LevelText => $"Level {Level}";

    partial void OnPetStateChanged(string value)
    {
        UpdateStateEmoji();
    }

    partial void OnCurrentXpChanged(int value)
    {
        OnPropertyChanged(nameof(XpProgress));
        OnPropertyChanged(nameof(XpText));
    }

    partial void OnXpToNextLevelChanged(int value)
    {
        OnPropertyChanged(nameof(XpProgress));
        OnPropertyChanged(nameof(XpText));
    }

    partial void OnLevelChanged(int value)
    {
        OnPropertyChanged(nameof(LevelText));
    }

    private void UpdateStateEmoji()
    {
        PetStateEmoji = PetState switch
        {
            PetStates.Happy => "😊",
            PetStates.Sad => "😢",
            PetStates.Hungry => "🍽️",
            PetStates.Tired => "😴",
            PetStates.Excited => "🤩",
            PetStates.Bored => "😐",
            PetStates.Sick => "🤒",
            PetStates.Sleeping => "💤",
            _ => "🐾"
        };
    }

    [RelayCommand]
    private void Feed()
    {
        _logger.LogDebug("Feeding pet");
        
        HappinessPercent = Math.Min(100, HappinessPercent + 10);
        EnergyPercent = Math.Min(100, EnergyPercent + 5);
        
        if (PetState == PetStates.Hungry)
            PetState = PetStates.Happy;
    }

    [RelayCommand]
    private void Rest()
    {
        _logger.LogDebug("Pet resting");
        
        EnergyPercent = Math.Min(100, EnergyPercent + 20);
        
        if (PetState == PetStates.Tired)
            PetState = PetStates.Happy;
    }

    [RelayCommand]
    private void Play()
    {
        _logger.LogDebug("Playing with pet");
        
        HappinessPercent = Math.Min(100, HappinessPercent + 15);
        EnergyPercent = Math.Max(0, EnergyPercent - 10);
        
        if (PetState == PetStates.Bored || PetState == PetStates.Sad)
            PetState = PetStates.Excited;
        
        if (EnergyPercent < 20)
            PetState = PetStates.Tired;
    }

    /// <summary>
    /// Adds XP to the pet, handling level-ups.
    /// </summary>
    public void AddXp(int amount)
    {
        CurrentXp += amount;
        
        while (CurrentXp >= XpToNextLevel)
        {
            CurrentXp -= XpToNextLevel;
            Level++;
            XpToNextLevel = CalculateXpForLevel(Level + 1);
            _logger.LogInformation("Pet leveled up to {Level}!", Level);
        }
    }

    private static int CalculateXpForLevel(int level) => 
        (int)(100 * Math.Pow(1.5, level - 1));
}
