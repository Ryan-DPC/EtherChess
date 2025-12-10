using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace EtherChess.ViewModels;

public partial class MenuViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    private string _welcomeMessage;

    public MenuViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        WelcomeMessage = "Welcome to Ether Chess";
    }

    [RelayCommand]
    private void PlayVsAI()
    {
        _mainViewModel.CurrentView = new GameViewModel(_mainViewModel.Username, isVsAI: true);
    }

    [RelayCommand]
    private void PlayMultiplayer()
    {
        MessageBox.Show("Multiplayer Lobby coming soon!", "Ether Chess");
    }
}
