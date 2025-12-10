using System;
using System.Collections.Generic;
using System.Linq;
using EtherChess.Models;

namespace EtherChess.Engine;

public class ChessAI
{
    private const int Infinity = 1000000;
    private Random _random = new Random();

    public enum Difficulty { Easy, Medium, Hard }

    public Move GetBestMove(Board board, Difficulty difficulty)
    {
        var moves = MoveGenerator.GenerateLegalMoves(board);
        if (moves.Count == 0) return default; // Checkmate or Stalemate

        // Easy: Random Move
        if (difficulty == Difficulty.Easy)
        {
            return moves[_random.Next(moves.Count)];
        }

        int depth = difficulty == Difficulty.Medium ? 2 : 4;

        Move bestMove = default;
        int bestValue = -Infinity;
        int alpha = -Infinity;
        int beta = Infinity;

        // Simple move ordering: captures first
        moves = moves.OrderByDescending(m => board.GetPiece(m.ToRow, m.ToCol).Type != PieceType.None).ToList();

        foreach (var move in moves)
        {
            var nextBoard = board.Clone();
            nextBoard.MakeMove(move);

            int value = -Minimax(nextBoard, depth - 1, -beta, -alpha);

            if (value > bestValue)
            {
                bestValue = value;
                bestMove = move;
            }
            alpha = Math.Max(alpha, value);
        }

        return bestMove;
    }

    private int Minimax(Board board, int depth, int alpha, int beta)
    {
        if (depth == 0)
        {
            return Evaluate(board);
        }

        var moves = MoveGenerator.GenerateLegalMoves(board);
        if (moves.Count == 0)
        {
            // Checkmate detection
            if (board.IsInCheck(board.Turn)) return -Infinity + (100 - depth); // Prefer faster mate
            return 0; // Stalemate
        }

        int bestValue = -Infinity;

        foreach (var move in moves)
        {
            var nextBoard = board.Clone();
            nextBoard.MakeMove(move);

            int value = -Minimax(nextBoard, depth - 1, -beta, -alpha);

            bestValue = Math.Max(bestValue, value);
            alpha = Math.Max(alpha, value);

            if (alpha >= beta)
                break;
        }

        return bestValue;
    }

    private int Evaluate(Board board)
    {
        int score = 0;
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var piece = board.GetPiece(r, c);
                if (piece.IsEmpty) continue;

                int value = GetPieceValue(piece.Type);
                
                // Positional bonuses (central control)
                if (piece.Type == PieceType.Pawn || piece.Type == PieceType.Knight)
                {
                    value += GetPositionBonus(r, c);
                }

                if (piece.Color == board.Turn)
                    score += value;
                else
                    score -= value;
            }
        }
        return score;
    }

    private int GetPieceValue(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => 100,
            PieceType.Knight => 320,
            PieceType.Bishop => 330,
            PieceType.Rook => 500,
            PieceType.Queen => 900,
            PieceType.King => 20000,
            _ => 0
        };
    }

    private int GetPositionBonus(int r, int c)
    {
        // Simple center bonus
        if (r >= 3 && r <= 4 && c >= 3 && c <= 4) return 20;
        return 0;
    }


}
