using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EtherChess.Models;
using EtherChess.Services;
using System.Collections.ObjectModel;

namespace EtherChess.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly UserProfileService _profileService = new();

    [ObservableProperty]
    private object _currentView = null!;

    [ObservableProperty]
    private string _username = "Guest";

    [ObservableProperty]
    private int _elo = 1200;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _winRate = "—";

    public UserProfileService ProfileService => _profileService;
    public UserProfile Profile => _profileService.Profile;
    public ObservableCollection<GameHistoryItem> RecentGames => Profile.RecentGames;

    public string? AuthToken { get; private set; }

    public MainViewModel()
    {
        SyncFromProfile();
        NavigateToDashboard();
    }

    [RelayCommand]
    public void NavigateToDashboard()
    {
        if (CurrentView is GameViewModel game)
        {
            game.Dispose();
        }
        else if (CurrentView is DashboardViewModel dashboard)
        {
            dashboard.DisposeLobby();
        }

        SyncFromProfile();
        CurrentView = new DashboardViewModel(this);
        if (CurrentView is DashboardViewModel dashboard)
        {
            dashboard.RefreshStats();
        }
    }

    public void RecordGame(GameHistoryItem item)
    {
        var eloDelta = 0;
        if (item.CountsForRating)
        {
            if (item.IsDraw)
            {
                eloDelta = 0;
            }
            else if (item.PlayerWon)
            {
                eloDelta = 15;
            }
            else
            {
                eloDelta = -15;
            }
        }

        _profileService.RecordGame(
            item.PlayerWon,
            item.IsDraw,
            item.IsVsBot,
            item.Moves,
            item.Opponent,
            item.Result,
            eloDelta);

        SyncFromProfile();
    }

    public void SyncFromProfile()
    {
        Elo = Profile.Elo;
        WinRate = Profile.WinRate;
        OnPropertyChanged(nameof(RecentGames));
    }

    public void Initialize(string userJson, string token)
    {
        AuthToken = token;
        IsAuthenticated = !string.IsNullOrWhiteSpace(token);

        try
        {
            using (var doc = System.Text.Json.JsonDocument.Parse(userJson))
            {
                if (doc.RootElement.TryGetProperty("username", out var usernameProp))
                {
                    Username = usernameProp.GetString() ?? "Unknown";
                }
                else
                {
                    Username = "No Username";
                }

                if (doc.RootElement.TryGetProperty("elo", out var eloProp))
                {
                    Profile.Elo = eloProp.GetInt32();
                }
            }
        }
        catch (Exception ex)
        {
            Username = "Parse Error";
            EtherChess.App.Log($"JSON Parse Error: {ex.Message}");
        }

        SyncFromProfile();
        EtherChess.App.Log($"Initialized with user: {Username}, Elo: {Elo}");

        NavigateToDashboard();
    }
}
