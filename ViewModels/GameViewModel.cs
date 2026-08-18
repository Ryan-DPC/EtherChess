using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EtherChess.Engine;
using EtherChess.Models;
using EtherChess.Network;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EtherChess.ViewModels;

public partial class GameViewModel : ObservableObject, IDisposable
{
    private readonly Board _board;
    private readonly ChessAI _ai;
    private readonly bool _isVsAI;
    private readonly PeerSession? _peer;
    private readonly object _boardLock = new();
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<SquareViewModel> _squares;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isWhiteTurn;

    private SquareViewModel? _selectedSquare;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _opponentName = "AI";

    [ObservableProperty]
    private string _colorLabel = "White";

    [ObservableProperty]
    private ChessAI.Difficulty _difficulty = ChessAI.Difficulty.Medium;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isGameOver;

    [ObservableProperty]
    private string _latencyLabel = "";

    [ObservableProperty]
    private string _whiteName = "White";

    [ObservableProperty]
    private string _blackName = "Black";

    [ObservableProperty]
    private ObservableCollection<string> _moveHistory = new();

    public PieceColor LocalColor { get; }

    public GameViewModel(
        string username,
        bool isVsAI = true,
        ChessAI.Difficulty difficulty = ChessAI.Difficulty.Medium,
        PeerSession? peer = null)
    {
        Username = username;
        _peer = peer;
        _isVsAI = isVsAI && peer is null;
        Difficulty = difficulty;
        LocalColor = peer?.LocalColor ?? PieceColor.White;
        ColorLabel = LocalColor == PieceColor.White ? "White" : "Black";
        OpponentName = peer?.OpponentUsername ?? (_isVsAI ? $"AI ({difficulty})" : "Opponent");
        WhiteName = LocalColor == PieceColor.White ? Username : OpponentName;
        BlackName = LocalColor == PieceColor.Black ? Username : OpponentName;
        _board = new Board();
        _ai = new ChessAI();
        _squares = new ObservableCollection<SquareViewModel>();
        InitializeBoard();
        SubscribePeer();
        UpdateStatus();
    }

    private void SubscribePeer()
    {
        if (_peer is null)
        {
            return;
        }

        _peer.MoveReceived += OnPeerMove;
        _peer.GameEnded += OnPeerGameEnded;
        _peer.Disconnected += OnPeerDisconnected;
        _peer.MoveAcknowledgedMs += OnPeerLatency;
        _ = _peer.SendPingAsync();
    }

    private void InitializeBoard()
    {
        Squares.Clear();
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var piece = _board.GetPiece(r, c);
                var square = new SquareViewModel(r, c, piece);
                square.Command = new RelayCommand<SquareViewModel?>(OnSquareClick);
                Squares.Add(square);
            }
        }
    }

    private async void OnSquareClick(SquareViewModel? clickedSquare)
    {
        if (clickedSquare is null || IsBusy || IsGameOver)
        {
            return;
        }

        if (_peer is not null && CurrentTurn() != LocalColor)
        {
            return;
        }

        if (_selectedSquare == null)
        {
            if (!clickedSquare.Piece.IsEmpty && clickedSquare.Piece.Color == CurrentTurn())
            {
                if (_peer is not null && clickedSquare.Piece.Color != LocalColor)
                {
                    return;
                }

                _selectedSquare = clickedSquare;
                _selectedSquare.IsSelected = true;
                HighlightLegalMoves(_selectedSquare);
            }
        }
        else
        {
            if (clickedSquare == _selectedSquare)
            {
                Deselect();
            }
            else
            {
                var candidateMove = new Move(_selectedSquare.Row, _selectedSquare.Col, clickedSquare.Row, clickedSquare.Col);
                if (!TryApplyLocalMove(candidateMove, out var resolvedMove))
                {
                    Deselect();
                    if (!clickedSquare.Piece.IsEmpty && clickedSquare.Piece.Color == CurrentTurn())
                    {
                        OnSquareClick(clickedSquare);
                    }
                    return;
                }

                RefreshBoard();
                MarkLastMove(resolvedMove);
                RecordMove(resolvedMove);
                Deselect();
                UpdateStatus();

                if (_peer is not null)
                {
                    await _peer.SendMoveAsync(resolvedMove);
                    if (IsGameOver)
                    {
                        await _peer.SendGameEndAsync(StatusMessage);
                    }
                    return;
                }

                if (_isVsAI && !IsGameOver && IsBlackTurn())
                {
                    await PerformAIMove();
                }
            }
        }
    }

    private bool TryApplyLocalMove(Move candidateMove, out Move resolvedMove)
    {
        resolvedMove = default;
        List<Move> matchingMoves;
        lock (_boardLock)
        {
            matchingMoves = FindMatchingMoves(candidateMove);
            if (matchingMoves.Count == 0)
            {
                return false;
            }

            resolvedMove = PreferQueenPromotion(matchingMoves);
            _board.MakeMove(resolvedMove);
            return true;
        }
    }

    private void ApplyIncomingMove(Move incoming)
    {
        if (IsGameOver)
        {
            return;
        }

        Move resolved = default;
        lock (_boardLock)
        {
            var matchingMoves = FindMatchingMoves(incoming);
            if (matchingMoves.Count == 0)
            {
                StatusMessage = "Desync détecté: coup adverse illégal.";
                IsGameOver = true;
                return;
            }

            resolved = incoming.Promotion != PieceType.None
                ? incoming
                : PreferQueenPromotion(matchingMoves);
            _board.MakeMove(resolved);
        }

        Deselect();
        RefreshBoard();
        MarkLastMove(resolved);
        RecordMove(resolved);
        UpdateStatus();
    }

    private List<Move> FindMatchingMoves(Move candidateMove)
    {
        return MoveGenerator
            .GenerateLegalMoves(_board)
            .Where(m =>
                m.FromRow == candidateMove.FromRow &&
                m.FromCol == candidateMove.FromCol &&
                m.ToRow == candidateMove.ToRow &&
                m.ToCol == candidateMove.ToCol &&
                (candidateMove.Promotion == PieceType.None || m.Promotion == candidateMove.Promotion))
            .ToList();
    }

    private static Move PreferQueenPromotion(List<Move> matchingMoves)
    {
        var selectedMove = matchingMoves.FirstOrDefault(m => m.Promotion == PieceType.Queen, matchingMoves[0]);
        return selectedMove.Promotion == PieceType.None
            ? selectedMove
            : new Move(selectedMove.FromRow, selectedMove.FromCol, selectedMove.ToRow, selectedMove.ToCol, PieceType.Queen);
    }

    private void HighlightLegalMoves(SquareViewModel startSquare)
    {
        List<Move> moves;
        lock (_boardLock)
        {
            moves = MoveGenerator.GenerateLegalMoves(_board);
        }

        foreach (var move in moves)
        {
            if (move.FromRow == startSquare.Row && move.FromCol == startSquare.Col)
            {
                var target = Squares.FirstOrDefault(s => s.Row == move.ToRow && s.Col == move.ToCol);
                if (target != null) target.IsLegalMove = true;
            }
        }
    }

    private void Deselect()
    {
        if (_selectedSquare != null)
        {
            _selectedSquare.IsSelected = false;
            _selectedSquare = null;
        }
        foreach (var s in Squares) s.IsLegalMove = false;
    }

    private void RefreshBoard()
    {
        lock (_boardLock)
        {
            foreach (var square in Squares)
            {
                square.Piece = _board.GetPiece(square.Row, square.Col);
            }
        }
    }

    private void UpdateStatus()
    {
        PieceColor turn;
        bool inCheck;
        bool hasMoves;

        lock (_boardLock)
        {
            turn = _board.Turn;
            inCheck = _board.IsInCheck(turn);
            hasMoves = MoveGenerator.GenerateLegalMoves(_board).Count > 0;
        }

        IsWhiteTurn = turn == PieceColor.White;
        IsGameOver = !hasMoves;

        if (!hasMoves)
        {
            StatusMessage = inCheck
                ? (IsWhiteTurn ? "Checkmate - Black wins" : "Checkmate - White wins")
                : "Stalemate";
            return;
        }

        var turnLabel = inCheck
            ? (IsWhiteTurn ? "White to move - Check" : "Black to move - Check")
            : (IsWhiteTurn ? "White's Turn" : "Black's Turn");

        if (_peer is not null)
        {
            turnLabel += turn == LocalColor ? " — your move" : $" — waiting for {OpponentName}";
        }

        StatusMessage = turnLabel;
    }

    private async Task PerformAIMove()
    {
        IsBusy = true;
        StatusMessage = "AI Thinking...";
        await Task.Delay(100);

        Board boardSnapshot;
        lock (_boardLock)
        {
            boardSnapshot = _board.Clone();
        }

        var bestMove = await Task.Run(() => _ai.GetBestMove(boardSnapshot, Difficulty));

        lock (_boardLock)
        {
            if (!IsGameOver && _board.Turn == PieceColor.Black)
            {
                _board.MakeMove(bestMove);
            }
        }

        RefreshBoard();
        MarkLastMove(bestMove);
        RecordMove(bestMove);
        UpdateStatus();
        IsBusy = false;
    }

    private void MarkLastMove(Move move)
    {
        foreach (var square in Squares)
        {
            square.IsLastMove =
                (square.Row == move.FromRow && square.Col == move.FromCol) ||
                (square.Row == move.ToRow && square.Col == move.ToCol);
        }
    }

    private void RecordMove(Move move)
    {
        static char File(int col) => (char)('a' + col);
        static int Rank(int row) => 8 - row;
        var san = $"{File(move.FromCol)}{Rank(move.FromRow)}{File(move.ToCol)}{Rank(move.ToRow)}";
        if (MoveHistory.Count % 2 == 0)
        {
            MoveHistory.Add($"{MoveHistory.Count / 2 + 1}. {san}");
        }
        else
        {
            MoveHistory[^1] = $"{MoveHistory[^1]}   {san}";
        }
    }

    private PieceColor CurrentTurn()
    {
        lock (_boardLock)
        {
            return _board.Turn;
        }
    }

    private bool IsBlackTurn()
    {
        return CurrentTurn() == PieceColor.Black;
    }

    private void OnPeerMove(Move move) => RunOnUi(() => ApplyIncomingMove(move));

    private void OnPeerGameEnded(string reason) => RunOnUi(() =>
    {
        if (!IsGameOver)
        {
            IsGameOver = true;
        }
        StatusMessage = reason;
    });

    private void OnPeerDisconnected(string reason) => RunOnUi(() =>
    {
        if (IsGameOver)
        {
            return;
        }

        IsGameOver = true;
        StatusMessage = reason;
    });

    private void OnPeerLatency(double milliseconds) => RunOnUi(() =>
    {
        LatencyLabel = $"sync {milliseconds:0.0} ms";
    });

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
            return;
        }

        action();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_peer is null)
        {
            return;
        }

        _peer.MoveReceived -= OnPeerMove;
        _peer.GameEnded -= OnPeerGameEnded;
        _peer.Disconnected -= OnPeerDisconnected;
        _peer.MoveAcknowledgedMs -= OnPeerLatency;
        _peer.Dispose();
    }
}

public partial class SquareViewModel : ObservableObject
{
    public int Row { get; }
    public int Col { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplaySymbol))]
    private Piece _piece;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLegalMove;

    [ObservableProperty]
    private bool _isLastMove;

    public bool IsDarkSquare => (Row + Col) % 2 != 0;
    public string FileLabel => Row == 7 ? ((char)('a' + Col)).ToString() : "";
    public string RankLabel => Col == 0 ? (8 - Row).ToString() : "";

    public RelayCommand<SquareViewModel?>? Command { get; set; }

    public string DisplaySymbol
    {
        get
        {
            if (Piece.IsEmpty) return "";
            return Piece.Type switch
            {
                PieceType.King => Piece.IsWhite ? "♔" : "♚",
                PieceType.Queen => Piece.IsWhite ? "♕" : "♛",
                PieceType.Rook => Piece.IsWhite ? "♖" : "♜",
                PieceType.Bishop => Piece.IsWhite ? "♗" : "♝",
                PieceType.Knight => Piece.IsWhite ? "♘" : "♞",
                PieceType.Pawn => Piece.IsWhite ? "♙" : "♟",
                _ => ""
            };
        }
    }

    public SquareViewModel(int row, int col, Piece piece)
    {
        Row = row;
        Col = col;
        Piece = piece;
    }
}
