using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace EtherChess.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object _currentView;

    [ObservableProperty]
    private string _username = "Guest";

    [ObservableProperty]
    private int _elo = 1200;

    public MainViewModel()
    {
        // Start with Dashboard
        NavigateToDashboard();
    }

    [RelayCommand]
    public void NavigateToDashboard()
    {
        CurrentView = new DashboardViewModel(this);
    }

    public void Initialize(string userJson, string token)
    {
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
