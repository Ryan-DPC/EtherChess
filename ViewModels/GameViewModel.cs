using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EtherChess.Models;
using EtherChess.Engine;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EtherChess.ViewModels;

public partial class GameViewModel : ObservableObject
{
    private readonly Board _board;
    private readonly ChessAI _ai;
    private readonly bool _isVsAI;
    private readonly object _boardLock = new();

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
    private ChessAI.Difficulty _difficulty = ChessAI.Difficulty.Medium;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isGameOver;

    public GameViewModel(string username, bool isVsAI = true, ChessAI.Difficulty difficulty = ChessAI.Difficulty.Medium)
    {
        Username = username;
        _isVsAI = isVsAI;
        Difficulty = difficulty;
        _board = new Board();
        _ai = new ChessAI();
        _squares = new ObservableCollection<SquareViewModel>();
        InitializeBoard();
        UpdateStatus();
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

        if (_selectedSquare == null)
        {
            // Select piece
            if (!clickedSquare.Piece.IsEmpty && clickedSquare.Piece.Color == _board.Turn)
            {
                _selectedSquare = clickedSquare;
                _selectedSquare.IsSelected = true;
                HighlightLegalMoves(_selectedSquare);
            }
        }
        else
        {
            // Move or Deselect
            if (clickedSquare == _selectedSquare)
            {
                Deselect();
            }
            else
            {
                // Try move
                var candidateMove = new Move(_selectedSquare.Row, _selectedSquare.Col, clickedSquare.Row, clickedSquare.Col);
                List<Move> matchingMoves;

                lock (_boardLock)
                {
                    matchingMoves = MoveGenerator
                        .GenerateLegalMoves(_board)
                        .Where(m =>
                            m.FromRow == candidateMove.FromRow &&
                            m.FromCol == candidateMove.FromCol &&
                            m.ToRow == candidateMove.ToRow &&
                            m.ToCol == candidateMove.ToCol)
                        .ToList();
                }

                if (matchingMoves.Count > 0)
                {
                    // Prefer queen promotion until a promotion picker is added.
                    var selectedMove = matchingMoves.FirstOrDefault(m => m.Promotion == PieceType.Queen, matchingMoves[0]);
                    var resolvedMove = selectedMove.Promotion == PieceType.None
                        ? selectedMove
                        : new Move(
                            selectedMove.FromRow,
                            selectedMove.FromCol,
                            selectedMove.ToRow,
                            selectedMove.ToCol,
                            PieceType.Queen);

                    lock (_boardLock)
                    {
                        _board.MakeMove(resolvedMove);
                    }

                    RefreshBoard();
                    Deselect();
                    UpdateStatus();

                    if (_isVsAI && !IsGameOver && IsBlackTurn())
                    {
                        await PerformAIMove();
                    }
                }
                else
                {
                    // Invalid move, select new piece if friendly
                    Deselect();
                    if (!clickedSquare.Piece.IsEmpty && clickedSquare.Piece.Color == _board.Turn)
                    {
                        OnSquareClick(clickedSquare);
                    }
                }
            }
        }
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
            if (inCheck)
            {
                StatusMessage = IsWhiteTurn ? "Checkmate - Black wins" : "Checkmate - White wins";
            }
            else
            {
                StatusMessage = "Stalemate";
            }

            return;
        }

        StatusMessage = inCheck
            ? (IsWhiteTurn ? "White to move - Check" : "Black to move - Check")
            : (IsWhiteTurn ? "White's Turn" : "Black's Turn");
    }

    private async Task PerformAIMove()
    {
        IsBusy = true;
        StatusMessage = "AI Thinking...";
        await Task.Delay(100); // UI Refresh

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
        UpdateStatus();
        IsBusy = false;
    }

    private bool IsBlackTurn()
    {
        lock (_boardLock)
        {
            return _board.Turn == PieceColor.Black;
        }
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

    public bool IsDarkSquare => (Row + Col) % 2 != 0;

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
