using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using EtherChess.Engine;
using System.Windows;

namespace EtherChess.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private int _rating;

    [ObservableProperty]
    private int _puzzlesSolved;

    [ObservableProperty]
    private string _winRate = "0%";

    [ObservableProperty]
    private ObservableCollection<GameHistoryItem> _recentGames;

    [ObservableProperty]
    private ChessAI.Difficulty _selectedDifficulty = ChessAI.Difficulty.Medium;

    public ObservableCollection<ChessAI.Difficulty> Difficulties { get; } = new ObservableCollection<ChessAI.Difficulty>(Enum.GetValues<ChessAI.Difficulty>());

    public DashboardViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        Username = _mainViewModel.Username;
        Rating = _mainViewModel.Elo;
        PuzzlesSolved = 0; // No real data yet
        RecentGames = new ObservableCollection<GameHistoryItem>(); // No real data yet
    }

    [RelayCommand]
    private void PlayVsAI()
    {
        _mainViewModel.CurrentView = new GameViewModel(_username, isVsAI: true, difficulty: SelectedDifficulty);
    }

    [RelayCommand]
    private void PlayMultiplayer()
    {
        MessageBox.Show(
            "Le mode multijoueur n'est pas encore disponible dans cette version. Utilisez la partie contre l'IA en attendant.",
            "EtherChess",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ShowPuzzlesComingSoon()
    {
        MessageBox.Show(
            "Les puzzles arriveront dans une prochaine version.",
            "EtherChess",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

public class GameHistoryItem
{
    public string Opponent { get; set; }
    public int Rating { get; set; }
    public string Result { get; set; }
    public int Moves { get; set; }
    public string Date { get; set; }
}
