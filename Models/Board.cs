using System;
using System.Collections.Generic;

namespace EtherChess.Models;

public class Board
{
    public Piece[,] Grid { get; private set; }
    public PieceColor Turn { get; private set; }
    public bool CanCastleWhiteKingSide { get; set; }
    public bool CanCastleWhiteQueenSide { get; set; }
    public bool CanCastleBlackKingSide { get; set; }
    public bool CanCastleBlackQueenSide { get; set; }
    public (int Row, int Col)? EnPassantTarget { get; set; }
    public int HalfMoveClock { get; set; }
    public int FullMoveNumber { get; set; }

    public Board()
    {
        Grid = new Piece[8, 8];
        SetupStartingPosition();
    }

    public void SetupStartingPosition()
    {
        // Clear board
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                Grid[r, c] = Piece.None;

        // Pawns
        for (int c = 0; c < 8; c++)
        {
            Grid[1, c] = new Piece(PieceType.Pawn, PieceColor.Black);
            Grid[6, c] = new Piece(PieceType.Pawn, PieceColor.White);
        }

        // Pieces
        SetupRank(0, PieceColor.Black);
        SetupRank(7, PieceColor.White);

        Turn = PieceColor.White;
        CanCastleWhiteKingSide = true;
        CanCastleWhiteQueenSide = true;
        CanCastleBlackKingSide = true;
        CanCastleBlackQueenSide = true;
        EnPassantTarget = null;
        HalfMoveClock = 0;
        FullMoveNumber = 1;
    }

    private void SetupRank(int row, PieceColor color)
    {
        Grid[row, 0] = new Piece(PieceType.Rook, color);
        Grid[row, 1] = new Piece(PieceType.Knight, color);
        Grid[row, 2] = new Piece(PieceType.Bishop, color);
        Grid[row, 3] = new Piece(PieceType.Queen, color);
        Grid[row, 4] = new Piece(PieceType.King, color);
        Grid[row, 5] = new Piece(PieceType.Bishop, color);
        Grid[row, 6] = new Piece(PieceType.Knight, color);
        Grid[row, 7] = new Piece(PieceType.Rook, color);
    }

    public Piece GetPiece(int row, int col)
    {
        if (row < 0 || row >= 8 || col < 0 || col >= 8) return Piece.None;
        return Grid[row, col];
    }

    public void MakeMove(Move move)
    {
        var piece = Grid[move.FromRow, move.FromCol];
        var target = Grid[move.ToRow, move.ToCol];

        // Capture or Pawn Move resets halfmove clock
        if (piece.Type == PieceType.Pawn || !target.IsEmpty)
            HalfMoveClock = 0;
        else
            HalfMoveClock++;

        // Move piece
        Grid[move.ToRow, move.ToCol] = piece;
        Grid[move.FromRow, move.FromCol] = Piece.None;

        // Promotion
        if (move.Promotion != PieceType.None)
        {
            Grid[move.ToRow, move.ToCol] = new Piece(move.Promotion, piece.Color);
        }

        // Handle Castling (Simplified - just moving rook)
        if (piece.Type == PieceType.King && Math.Abs(move.ToCol - move.FromCol) == 2)
        {
            if (move.ToCol == 6) // King side
            {
                var rook = Grid[move.FromRow, 7];
                Grid[move.FromRow, 5] = rook;
                Grid[move.FromRow, 7] = Piece.None;
            }
            else if (move.ToCol == 2) // Queen side
            {
                var rook = Grid[move.FromRow, 0];
                Grid[move.FromRow, 3] = rook;
                Grid[move.FromRow, 0] = Piece.None;
            }
        }

        // Handle En Passant Capture
        if (piece.Type == PieceType.Pawn && EnPassantTarget.HasValue && 
            move.ToRow == EnPassantTarget.Value.Row && move.ToCol == EnPassantTarget.Value.Col)
        {
            int captureRow = move.FromRow; // The pawn being captured is on the same rank as the start
            Grid[captureRow, move.ToCol] = Piece.None;
        }

        // Set En Passant Target
        EnPassantTarget = null;
        if (piece.Type == PieceType.Pawn && Math.Abs(move.ToRow - move.FromRow) == 2)
        {
            EnPassantTarget = (move.FromRow + (move.ToRow - move.FromRow) / 2, move.FromCol);
        }

        // Update Turn
        if (Turn == PieceColor.Black)
            FullMoveNumber++;
        Turn = Turn == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
    public bool IsInCheck(PieceColor color)
    {
        var kingPos = FindKing(color);
        if (!kingPos.HasValue) return false; // Should not happen
        return IsSquareAttacked(kingPos.Value.Row, kingPos.Value.Col, color == PieceColor.White ? PieceColor.Black : PieceColor.White);
    }

    private (int Row, int Col)? FindKing(PieceColor color)
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var p = Grid[r, c];
                if (p.Type == PieceType.King && p.Color == color)
                    return (r, c);
            }
        return null;
    }

    public bool IsSquareAttacked(int r, int c, PieceColor attackerColor)
    {
        // Check for Pawn attacks
        int pawnDir = attackerColor == PieceColor.White ? -1 : 1; // White pawns attack "up" (lower row index)
        // Actually, if we are checking if *attacker* (e.g. White) attacks (r,c), we look for White pawns at (r+1, c-1) and (r+1, c+1)
        // Wait, standard logic:
        // If attacker is White, they are at (r+1, c+/-1) moving to (r, c)
        int attackRow = r - (attackerColor == PieceColor.White ? -1 : 1); 
        if (attackRow >= 0 && attackRow < 8)
        {
            if (c - 1 >= 0 && IsPiece(attackRow, c - 1, PieceType.Pawn, attackerColor)) return true;
            if (c + 1 < 8 && IsPiece(attackRow, c + 1, PieceType.Pawn, attackerColor)) return true;
        }

        // Knights
        var knightOffsets = new[] { (2, 1), (2, -1), (-2, 1), (-2, -1), (1, 2), (1, -2), (-1, 2), (-1, -2) };
        foreach (var (dr, dc) in knightOffsets)
            if (IsPiece(r + dr, c + dc, PieceType.Knight, attackerColor)) return true;

        // Kings
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
                if (IsPiece(r + dr, c + dc, PieceType.King, attackerColor)) return true;

        // Sliding pieces (Queen, Rook, Bishop)
        var dirs = new[] { (0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1) };
        foreach (var (dr, dc) in dirs)
        {
            int tr = r + dr, tc = c + dc;
            while (tr >= 0 && tr < 8 && tc >= 0 && tc < 8)
            {
                var p = Grid[tr, tc];
                if (!p.IsEmpty)
                {
                    if (p.Color == attackerColor)
                    {
                        bool isDiagonal = dr != 0 && dc != 0;
                        if (p.Type == PieceType.Queen || 
                            (isDiagonal && p.Type == PieceType.Bishop) || 
                            (!isDiagonal && p.Type == PieceType.Rook))
                        {
                            return true;
                        }
                    }
                    break; // Blocked
                }
                tr += dr;
                tc += dc;
            }
        }

        return false;
    }

    private bool IsPiece(int r, int c, PieceType type, PieceColor color)
    {
        if (r < 0 || r >= 8 || c < 0 || c >= 8) return false;
        var p = Grid[r, c];
        return !p.IsEmpty && p.Type == type && p.Color == color;
    }

    public Board Clone()
    {
        var newBoard = new Board();
        // Copy grid
        Array.Copy(Grid, newBoard.Grid, Grid.Length);
        
        // Copy state
        newBoard.Turn = Turn;
        newBoard.CanCastleWhiteKingSide = CanCastleWhiteKingSide;
        newBoard.CanCastleWhiteQueenSide = CanCastleWhiteQueenSide;
        newBoard.CanCastleBlackKingSide = CanCastleBlackKingSide;
        newBoard.CanCastleBlackQueenSide = CanCastleBlackQueenSide;
        newBoard.EnPassantTarget = EnPassantTarget;
        newBoard.HalfMoveClock = HalfMoveClock;
        newBoard.FullMoveNumber = FullMoveNumber;
        
        return newBoard;
    }
}
