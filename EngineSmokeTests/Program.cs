using EtherChess.Engine;
using EtherChess.Models;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static Move? FindMove(Board board, int fromRow, int fromCol, int toRow, int toCol, PieceType promotion = PieceType.None)
{
    return MoveGenerator.GenerateLegalMoves(board)
        .FirstOrDefault(move =>
            move.FromRow == fromRow &&
            move.FromCol == fromCol &&
            move.ToRow == toRow &&
            move.ToCol == toCol &&
            move.Promotion == promotion);
}

static Board CreateEmptyBoard(PieceColor turn)
{
    var board = new Board();

    for (int row = 0; row < 8; row++)
    {
        for (int col = 0; col < 8; col++)
        {
            board.Grid[row, col] = Piece.None;
        }
    }

    board.CanCastleWhiteKingSide = false;
    board.CanCastleWhiteQueenSide = false;
    board.CanCastleBlackKingSide = false;
    board.CanCastleBlackQueenSide = false;
    board.EnPassantTarget = null;
    board.HalfMoveClock = 0;
    board.FullMoveNumber = 1;
    SetBoardTurn(board, turn);

    return board;
}

static void SetBoardTurn(Board board, PieceColor turn)
{
    typeof(Board).GetProperty(nameof(Board.Turn))!.SetValue(board, turn);
}

void TestWhiteKingSideCastling()
{
    var board = CreateEmptyBoard(PieceColor.White);
    board.Grid[7, 4] = new Piece(PieceType.King, PieceColor.White);
    board.Grid[7, 7] = new Piece(PieceType.Rook, PieceColor.White);
    board.Grid[0, 4] = new Piece(PieceType.King, PieceColor.Black);
    board.CanCastleWhiteKingSide = true;

    var castleMove = FindMove(board, 7, 4, 7, 6);
    Assert(castleMove.HasValue, "Expected white king-side castle to be legal.");

    board.MakeMove(castleMove.Value);

    Assert(board.GetPiece(7, 6).Type == PieceType.King, "King should end on g1 after castling.");
    Assert(board.GetPiece(7, 5).Type == PieceType.Rook, "Rook should end on f1 after castling.");
    Assert(!board.CanCastleWhiteKingSide && !board.CanCastleWhiteQueenSide, "White castling rights should be cleared after king move.");
}

void TestCastleBlockedByAttack()
{
    var board = CreateEmptyBoard(PieceColor.White);
    board.Grid[7, 4] = new Piece(PieceType.King, PieceColor.White);
    board.Grid[7, 7] = new Piece(PieceType.Rook, PieceColor.White);
    board.Grid[0, 4] = new Piece(PieceType.King, PieceColor.Black);
    board.Grid[5, 5] = new Piece(PieceType.Rook, PieceColor.Black);
    board.CanCastleWhiteKingSide = true;

    var castleMove = FindMove(board, 7, 4, 7, 6);
    Assert(!castleMove.HasValue, "Castling through an attacked square must be illegal.");
}

void TestPromotionMovesGenerated()
{
    var board = CreateEmptyBoard(PieceColor.White);
    board.Grid[7, 4] = new Piece(PieceType.King, PieceColor.White);
    board.Grid[0, 4] = new Piece(PieceType.King, PieceColor.Black);
    board.Grid[1, 0] = new Piece(PieceType.Pawn, PieceColor.White);

    var moves = MoveGenerator.GenerateLegalMoves(board)
        .Where(move => move.FromRow == 1 && move.FromCol == 0 && move.ToRow == 0 && move.ToCol == 0)
        .Select(move => move.Promotion)
        .OrderBy(piece => piece)
        .ToArray();

    Assert(moves.SequenceEqual(new[] { PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen }.OrderBy(piece => piece)),
        "Pawn promotion should generate all four promotion pieces.");
}

void TestEnPassantCapture()
{
    var board = CreateEmptyBoard(PieceColor.White);
    board.Grid[7, 4] = new Piece(PieceType.King, PieceColor.White);
    board.Grid[0, 4] = new Piece(PieceType.King, PieceColor.Black);
    board.Grid[3, 4] = new Piece(PieceType.Pawn, PieceColor.White);
    board.Grid[3, 5] = new Piece(PieceType.Pawn, PieceColor.Black);
    board.EnPassantTarget = (2, 5);

    var move = FindMove(board, 3, 4, 2, 5);
    Assert(move.HasValue, "Expected en passant move to be generated.");

    board.MakeMove(move.Value);

    Assert(board.GetPiece(2, 5).Color == PieceColor.White, "Capturing pawn should move to en passant target square.");
    Assert(board.GetPiece(3, 5).IsEmpty, "Captured pawn should be removed after en passant.");
}

var tests = new Action[]
{
    TestWhiteKingSideCastling,
    TestCastleBlockedByAttack,
    TestPromotionMovesGenerated,
    TestEnPassantCapture
};

foreach (var test in tests)
{
    test();
}

Console.WriteLine($"Engine smoke tests passed: {tests.Length}");
