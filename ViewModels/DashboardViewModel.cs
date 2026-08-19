using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EtherChess.Engine;
using EtherChess.Models;
using EtherChess.Network;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
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
    private string _winRate = "—";

    [ObservableProperty]
    private ChessAI.Difficulty _selectedDifficulty = ChessAI.Difficulty.Medium;

    [ObservableProperty]
    private int _hostPort = 5555;

    [ObservableProperty]
    private string _joinHost = "127.0.0.1";

    [ObservableProperty]
    private int _joinPort = 5555;

    [ObservableProperty]
    private string _multiplayerStatus = "Test local : instance 1 = Host, instance 2 = Join.";

    [ObservableProperty]
    private bool _isWaitingForOpponent;

    public System.Collections.ObjectModel.ObservableCollection<GameHistoryItem> RecentGames => _mainViewModel.RecentGames;

    public ObservableCollection<ChessAI.Difficulty> Difficulties { get; } = new(Enum.GetValues<ChessAI.Difficulty>());

    public DashboardViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        Username = _mainViewModel.Username;
        Rating = _mainViewModel.Elo;
        WinRate = _mainViewModel.WinRate;
    }

    [RelayCommand]
    private void PlayVsAI()
    {
        _mainViewModel.CurrentView = new GameViewModel(
            _mainViewModel,
            Username,
            isVsAI: true,
            difficulty: SelectedDifficulty);
    }

    [RelayCommand(CanExecute = nameof(CanStartPeerAction))]
    private async Task HostGameAsync()
    {
        await ConnectPeerAsync(isHost: true);
    }

    [RelayCommand(CanExecute = nameof(CanStartPeerAction))]
    private async Task JoinGameAsync()
    {
        await ConnectPeerAsync(isHost: false);
    }

    private bool CanStartPeerAction() => !IsWaitingForOpponent;

    [RelayCommand]
    private void CancelLobby()
    {
        try { _lobbyCts?.Cancel(); } catch { }
        _pendingSession?.Dispose();
        _pendingSession = null;
        IsWaitingForOpponent = false;
        MultiplayerStatus = "Connexion P2P annulée.";
        HostGameCommand.NotifyCanExecuteChanged();
        JoinGameCommand.NotifyCanExecuteChanged();
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
        HostGameCommand.NotifyCanExecuteChanged();
        JoinGameCommand.NotifyCanExecuteChanged();
        MultiplayerStatus = isHost
            ? $"En attente d'un adversaire sur le port {HostPort}... (Blanc)"
            : $"Connexion à {JoinHost}:{JoinPort}... (Noir)";

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
            _mainViewModel.CurrentView = new GameViewModel(_mainViewModel, Username, isVsAI: false, peer: session);
        }
        catch (OperationCanceledException)
        {
            MultiplayerStatus = "Connexion P2P annulée.";
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            MultiplayerStatus =
                $"Le port {HostPort} est déjà pris. Sur la 2e instance, cliquez Join (127.0.0.1:{HostPort}), pas Host.";
            _pendingSession?.Dispose();
            _pendingSession = null;
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
            HostGameCommand.NotifyCanExecuteChanged();
            JoinGameCommand.NotifyCanExecuteChanged();
        }
    }
}
