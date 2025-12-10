using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EtherChess.Models;
using EtherChess.Engine;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EtherChess.ViewModels;

public partial class GameViewModel : ObservableObject
{
    private readonly Board _board;
    private readonly ChessAI _ai;
    private readonly bool _isVsAI;

    [ObservableProperty]
    private ObservableCollection<SquareViewModel> _squares;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isWhiteTurn;

    private SquareViewModel? _selectedSquare;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private ChessAI.Difficulty _difficulty = ChessAI.Difficulty.Medium;

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
                square.Command = new RelayCommand<SquareViewModel>(OnSquareClick);
                Squares.Add(square);
            }
        }
    }

    private async void OnSquareClick(SquareViewModel clickedSquare)
    {
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
                var move = new Move(_selectedSquare.Row, _selectedSquare.Col, clickedSquare.Row, clickedSquare.Col);
                var validMoves = MoveGenerator.GenerateLegalMoves(_board);
                
                var validMove = validMoves.FirstOrDefault(m => m.FromRow == move.FromRow && m.FromCol == move.FromCol && m.ToRow == move.ToRow && m.ToCol == move.ToCol);

                if (validMove.FromRow != 0 || validMove.ToRow != 0 || validMove.FromCol != 0 || validMove.ToCol != 0) // Struct default check
                {
                    // Execute Move
                    _board.MakeMove(validMove);
                    RefreshBoard();
                    Deselect();
                    UpdateStatus();

                    if (_isVsAI && _board.Turn == PieceColor.Black)
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
        var moves = MoveGenerator.GenerateLegalMoves(_board);
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
        foreach (var square in Squares)
        {
            square.Piece = _board.GetPiece(square.Row, square.Col);
        }
    }

    private void UpdateStatus()
    {
        IsWhiteTurn = _board.Turn == PieceColor.White;
        StatusMessage = IsWhiteTurn ? "White's Turn" : "Black's Turn";
    }

    private async Task PerformAIMove()
    {
        StatusMessage = "AI Thinking...";
        await Task.Delay(100); // UI Refresh
        
        await Task.Run(() =>
        {
            var bestMove = _ai.GetBestMove(_board, Difficulty);
            Application.Current.Dispatcher.Invoke(() =>
            {
                _board.MakeMove(bestMove);
                RefreshBoard();
                UpdateStatus();
            });
        });
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

    public RelayCommand<SquareViewModel> Command { get; set; }

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
