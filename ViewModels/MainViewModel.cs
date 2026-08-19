using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EtherChess.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace EtherChess.ViewModels;

public partial class MainViewModel : ObservableObject
{
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

    public ObservableCollection<GameHistoryItem> RecentGames { get; } = new();

    public string? AuthToken { get; private set; }

    public MainViewModel()
    {
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

        CurrentView = new DashboardViewModel(this);
    }

    public void RecordGame(GameHistoryItem item)
    {
        RecentGames.Insert(0, item);

        while (RecentGames.Count > 20)
        {
            RecentGames.RemoveAt(RecentGames.Count - 1);
        }

        if (item.CountsForRating)
        {
            if (item.IsDraw)
            {
                // No ELO change on draw for now.
            }
            else if (item.PlayerWon)
            {
                Elo += 15;
            }
            else
            {
                Elo = Math.Max(100, Elo - 15);
            }
        }

        UpdateWinRate();
    }

    private void UpdateWinRate()
    {
        var rated = RecentGames.Where(g => g.CountsForRating).ToList();
        if (rated.Count == 0)
        {
            WinRate = "—";
            return;
        }

        var wins = rated.Count(g => g.PlayerWon);
        var pct = (int)Math.Round(100.0 * wins / rated.Count);
        WinRate = $"{pct}%";
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
                    Elo = eloProp.GetInt32();
                }
            }
        }
        catch (Exception ex)
        {
            Username = "Parse Error";
            EtherChess.App.Log($"JSON Parse Error: {ex.Message}");
        }

        EtherChess.App.Log($"Initialized with user: {Username}, Elo: {Elo}");

        NavigateToDashboard();
    }
}
