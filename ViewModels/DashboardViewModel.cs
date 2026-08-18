using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EtherChess.Engine;
using EtherChess.Network;
using System.Collections.ObjectModel;
using System.Net;
using System.Windows;

namespace EtherChess.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private CancellationTokenSource? _lobbyCts;
    private PeerSession? _pendingSession;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private int _rating;

    [ObservableProperty]
    private int _puzzlesSolved;

    [ObservableProperty]
    private string _winRate = "0%";

    [ObservableProperty]
    private ObservableCollection<GameHistoryItem> _recentGames = new();

    [ObservableProperty]
    private ChessAI.Difficulty _selectedDifficulty = ChessAI.Difficulty.Medium;

    [ObservableProperty]
    private int _hostPort = 5555;

    [ObservableProperty]
    private string _joinHost = "127.0.0.1";

    [ObservableProperty]
    private int _joinPort = 5555;

    [ObservableProperty]
    private string _multiplayerStatus = "Host a local peer or join an IP:port.";

    [ObservableProperty]
    private bool _isWaitingForOpponent;

    public ObservableCollection<ChessAI.Difficulty> Difficulties { get; } = new(Enum.GetValues<ChessAI.Difficulty>());

    public DashboardViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        Username = _mainViewModel.Username;
        Rating = _mainViewModel.Elo;
    }

    [RelayCommand]
    private void PlayVsAI()
    {
        _mainViewModel.CurrentView = new GameViewModel(Username, isVsAI: true, difficulty: SelectedDifficulty);
    }

    [RelayCommand]
    private async Task HostGameAsync()
    {
        await ConnectPeerAsync(isHost: true);
    }

    [RelayCommand]
    private async Task JoinGameAsync()
    {
        await ConnectPeerAsync(isHost: false);
    }

    [RelayCommand]
    private void CancelLobby()
    {
        try { _lobbyCts?.Cancel(); } catch { }
        _pendingSession?.Dispose();
        _pendingSession = null;
        IsWaitingForOpponent = false;
        MultiplayerStatus = "Connexion P2P annulée.";
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

    public void DisposeLobby()
    {
        try { _lobbyCts?.Cancel(); } catch { }
        _pendingSession?.Dispose();
        _pendingSession = null;
        _lobbyCts?.Dispose();
        _lobbyCts = null;
    }

    private async Task ConnectPeerAsync(bool isHost)
    {
        DisposeLobby();
        _lobbyCts = new CancellationTokenSource();
        _pendingSession = new PeerSession();
        IsWaitingForOpponent = true;
        MultiplayerStatus = isHost
            ? $"En attente d'un adversaire sur le port {HostPort}..."
            : $"Connexion à {JoinHost}:{JoinPort}...";

        try
        {
            if (isHost)
            {
                await _pendingSession.HostAsync(HostPort, Username, Rating, _lobbyCts.Token, IPAddress.Any);
            }
            else
            {
                await _pendingSession.JoinAsync(JoinHost.Trim(), JoinPort, Username, Rating, _lobbyCts.Token);
            }

            var session = _pendingSession;
            _pendingSession = null;
            IsWaitingForOpponent = false;
            MultiplayerStatus = $"Connecté à {session.OpponentUsername}.";
            _mainViewModel.CurrentView = new GameViewModel(Username, isVsAI: false, peer: session);
        }
        catch (OperationCanceledException)
        {
            MultiplayerStatus = "Connexion P2P annulée.";
        }
        catch (Exception ex)
        {
            MultiplayerStatus = $"Erreur P2P: {ex.Message}";
            _pendingSession?.Dispose();
            _pendingSession = null;
        }
        finally
        {
            IsWaitingForOpponent = false;
        }
    }
}

public class GameHistoryItem
{
    public string Opponent { get; set; } = "";
    public int Rating { get; set; }
    public string Result { get; set; } = "";
    public int Moves { get; set; }
    public string Date { get; set; } = "";
}
