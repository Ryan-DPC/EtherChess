using System;
using System.Collections.Generic;
using EtherChess.Models;

namespace EtherChess.Engine;

public static class MoveGenerator
{
    public static List<Move> GenerateLegalMoves(Board board)
    {
        var pseudoLegalMoves = GenerateAllMoves(board);
        var legalMoves = new List<Move>();

        foreach (var move in pseudoLegalMoves)
        {
            var nextBoard = board.Clone();
            nextBoard.MakeMove(move);
            
            // Check if King is in check after move
            // Note: MakeMove flips the turn, so we check if the *previous* player (who just moved) is in check.
            // But IsInCheck takes a color.
            // If White moved, it's now Black's turn. We need to check if White is in check.
            var moverColor = board.Turn; // The color moving now
            if (!nextBoard.IsInCheck(moverColor))
            {
                legalMoves.Add(move);
            }
        }
        return legalMoves;
    }

    public static List<Move> GenerateAllMoves(Board board)
    {
        var moves = new List<Move>();
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var piece = board.GetPiece(r, c);
                if (piece.Color == board.Turn)
                {
                    GenerateMovesForPiece(board, r, c, piece, moves);
                }
            }
        }
        return moves;
    }

    private static void GenerateMovesForPiece(Board board, int r, int c, Piece piece, List<Move> moves)
    {
        switch (piece.Type)
        {
            case PieceType.Pawn: GeneratePawnMoves(board, r, c, piece, moves); break;
            case PieceType.Knight: GenerateKnightMoves(board, r, c, piece, moves); break;
            case PieceType.Bishop: GenerateSlidingMoves(board, r, c, piece, moves, new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) }); break;
            case PieceType.Rook: GenerateSlidingMoves(board, r, c, piece, moves, new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }); break;
            case PieceType.Queen: GenerateSlidingMoves(board, r, c, piece, moves, new[] { (1, 1), (1, -1), (-1, 1), (-1, -1), (1, 0), (-1, 0), (0, 1), (0, -1) }); break;
            case PieceType.King: GenerateKingMoves(board, r, c, piece, moves); break;
        }
    }

    private static void GeneratePawnMoves(Board board, int r, int c, Piece piece, List<Move> moves)
    {
        int direction = piece.IsWhite ? -1 : 1;
        int startRow = piece.IsWhite ? 6 : 1;
        int promotionRow = piece.IsWhite ? 0 : 7;

        // Forward 1
        int r1 = r + direction;
        if (IsValid(r1, c) && board.GetPiece(r1, c).IsEmpty)
        {
            AddPawnMove(moves, r, c, r1, c, r1 == promotionRow);
            
            // Forward 2
            int r2 = r + 2 * direction;
            if (r == startRow && IsValid(r2, c) && board.GetPiece(r2, c).IsEmpty)
            {
                moves.Add(new Move(r, c, r2, c));
            }
        }

        // Captures
        foreach (int dc in new[] { -1, 1 })
        {
            int tc = c + dc;
            if (IsValid(r1, tc))
            {
                var target = board.GetPiece(r1, tc);
                if (!target.IsEmpty && target.Color != piece.Color)
                {
                    AddPawnMove(moves, r, c, r1, tc, r1 == promotionRow);
                }
                // En Passant
                if (board.EnPassantTarget.HasValue && board.EnPassantTarget.Value.Row == r1 && board.EnPassantTarget.Value.Col == tc)
                {
                    moves.Add(new Move(r, c, r1, tc));
                }
            }
        }
    }

    private static void AddPawnMove(List<Move> moves, int r, int c, int tr, int tc, bool promotion)
    {
        if (promotion)
        {
            moves.Add(new Move(r, c, tr, tc, PieceType.Queen));
            moves.Add(new Move(r, c, tr, tc, PieceType.Rook));
            moves.Add(new Move(r, c, tr, tc, PieceType.Bishop));
            moves.Add(new Move(r, c, tr, tc, PieceType.Knight));
        }
        else
        {
            moves.Add(new Move(r, c, tr, tc));
        }
    }

    private static void GenerateKnightMoves(Board board, int r, int c, Piece piece, List<Move> moves)
    {
        var offsets = new[] { (2, 1), (2, -1), (-2, 1), (-2, -1), (1, 2), (1, -2), (-1, 2), (-1, -2) };
        foreach (var (dr, dc) in offsets)
        {
            int tr = r + dr, tc = c + dc;
            if (IsValid(tr, tc))
            {
                var target = board.GetPiece(tr, tc);
                if (target.IsEmpty || target.Color != piece.Color)
                {
                    moves.Add(new Move(r, c, tr, tc));
                }
            }
        }
    }

    private static void GenerateSlidingMoves(Board board, int r, int c, Piece piece, List<Move> moves, (int, int)[] directions)
    {
        foreach (var (dr, dc) in directions)
        {
            int tr = r + dr, tc = c + dc;
            while (IsValid(tr, tc))
            {
                var target = board.GetPiece(tr, tc);
                if (target.IsEmpty)
                {
                    moves.Add(new Move(r, c, tr, tc));
                }
                else
                {
                    if (target.Color != piece.Color)
                    {
                        moves.Add(new Move(r, c, tr, tc));
                    }
                    break; // Blocked
                }
                tr += dr;
                tc += dc;
            }
        }
    }

    private static void GenerateKingMoves(Board board, int r, int c, Piece piece, List<Move> moves)
    {
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int tr = r + dr, tc = c + dc;
                if (IsValid(tr, tc))
                {
                    var target = board.GetPiece(tr, tc);
                    if (target.IsEmpty || target.Color != piece.Color)
                    {
                        moves.Add(new Move(r, c, tr, tc));
                    }
                }
            }
        }

        // Castling (Basic check, needs safety check later)
        // This is pseudo-legal generation, validation happens later
    }

    private static bool IsValid(int r, int c) => r >= 0 && r < 8 && c >= 0 && c < 8;
}
