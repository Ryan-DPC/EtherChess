using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    public string? AuthToken { get; private set; }

    public MainViewModel()
    {
        // Start with Dashboard
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
        
        // Navigate to Dashboard after initialization to refresh data
        NavigateToDashboard();
    }
}
