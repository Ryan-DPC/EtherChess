using System.Diagnostics;
using System.Net;
using System.Threading.Channels;
using EtherChess.Engine;
using EtherChess.Models;
using EtherChess.Network;

namespace EtherChess.P2PTest;

internal static class MatchRunner
{
    internal static readonly (int FromRow, int FromCol, int ToRow, int ToCol)[] Opening =
    {
        (6, 4, 4, 4),
        (1, 4, 3, 4),
        (7, 6, 5, 5),
        (0, 1, 2, 2),
        (7, 5, 4, 2),
        (0, 5, 3, 2),
        (6, 2, 5, 2),
        (0, 6, 2, 5),
        (6, 3, 4, 3),
        (3, 4, 4, 3),
        (5, 2, 4, 3),
        (3, 2, 4, 1),
        (7, 1, 5, 2),
        (2, 5, 4, 4),
        (7, 4, 7, 6),
        (4, 1, 5, 2)
    };

    internal static readonly (int FromRow, int FromCol, int ToRow, int ToCol)[] ScholarsMate =
    {
        (6, 4, 4, 4),
        (1, 4, 3, 4),
        (7, 3, 3, 7),
        (0, 1, 2, 2),
        (7, 5, 4, 2),
        (0, 6, 2, 5),
        (3, 7, 1, 5)
    };

    public static async Task RunHeadlessAsync()
    {
        var latencies = new List<double>();
        Console.WriteLine("=== EtherChess P2P live match ===");
        await RunScriptedMatch("Italian opening + castling", Opening, expectCheckmate: false, latencies, delayMs: 0, onEvent: null);
        await RunScriptedMatch("Scholar's mate", ScholarsMate, expectCheckmate: true, latencies, delayMs: 0, onEvent: null);
        PrintSummary(latencies);
    }

    public static async Task RunVisualAsync(Func<object, Task> emit)
    {
        var latencies = new List<double>();
        await emit(new { type = "hello", host = "WhiteHost", guest = "BlackGuest" });
        await Task.Delay(700);

        await RunScriptedMatch(
            "Italian opening + castling",
            Opening,
            expectCheckmate: false,
            latencies,
            delayMs: 850,
            onEvent: emit);

        await Task.Delay(1200);

        await RunScriptedMatch(
            "Scholar's mate",
            ScholarsMate,
            expectCheckmate: true,
            latencies,
            delayMs: 850,
            onEvent: emit);

        var avg = latencies.Average();
        var max = latencies.Max();
        await emit(new { type = "summary", avgMs = avg, maxMs = max, samples = latencies.Count });
        await emit(new { type = "result", message = "P2P match synced and fluid" });
        PrintSummary(latencies);
    }

    private static async Task RunScriptedMatch(
        string title,
        IReadOnlyList<(int FromRow, int FromCol, int ToRow, int ToCol)> script,
        bool expectCheckmate,
        List<double> latencies,
        int delayMs,
        Func<object, Task>? onEvent)
    {
        Console.WriteLine();
        Console.WriteLine($"-- {title} --");
        if (onEvent is not null)
        {
            await onEvent(new { type = "match", title });
        }

        using var host = new PeerSession();
        using var guest = new PeerSession();
        var timeout = delayMs > 0 ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource(timeout);

        var hostInbox = Channel.CreateUnbounded<Move>();
        var guestInbox = Channel.CreateUnbounded<Move>();
        string? guestEnd = null;

        host.MoveReceived += move => hostInbox.Writer.TryWrite(move);
        guest.MoveReceived += move => guestInbox.Writer.TryWrite(move);
        guest.GameEnded += reason => guestEnd = reason;
        host.MoveAcknowledgedMs += ms => { lock (latencies) latencies.Add(ms); };
        guest.MoveAcknowledgedMs += ms => { lock (latencies) latencies.Add(ms); };

        var hostTask = host.HostAsync(0, "WhiteHost", 1500, cts.Token, IPAddress.Loopback);
        await WaitUntil(() => host.Port > 0, TimeSpan.FromSeconds(2));
        await guest.JoinAsync("127.0.0.1", host.Port, "BlackGuest", 1480, cts.Token);
        await hostTask;

        if (!host.IsConnected || !guest.IsConnected)
        {
            throw new InvalidOperationException("Handshake P2P incomplet.");
        }

        if (host.LocalColor != PieceColor.White || guest.LocalColor != PieceColor.Black)
        {
            throw new InvalidOperationException("Les couleurs P2P n'ont pas été attribuées correctement.");
        }

        var hostBoard = new Board();
        var guestBoard = new Board();
        if (onEvent is not null)
        {
            await onEvent(new
            {
                type = "connected",
                hostSquares = BoardVisual.Squares(hostBoard),
                guestSquares = BoardVisual.Squares(guestBoard)
            });
            await Task.Delay(Math.Max(delayMs, 400));
        }

        await host.SendPingAsync();
        await guest.SendPingAsync();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < script.Count; i++)
        {
            var ply = script[i];
            bool whiteToMove = i % 2 == 0;
            var sender = whiteToMove ? host : guest;
            var senderBoard = whiteToMove ? hostBoard : guestBoard;
            var receiverBoard = whiteToMove ? guestBoard : hostBoard;
            var inbox = whiteToMove ? guestInbox : hostInbox;

            var localMove = ApplyLegal(senderBoard, ply.FromRow, ply.FromCol, ply.ToRow, ply.ToCol);
            var plyWatch = Stopwatch.StartNew();
            await sender.SendMoveAsync(localMove);
            var remoteMove = await inbox.Reader.ReadAsync(cts.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            plyWatch.Stop();
            ApplyLegal(receiverBoard, remoteMove.FromRow, remoteMove.FromCol, remoteMove.ToRow, remoteMove.ToCol, remoteMove.Promotion);

            var hostKey = hostBoard.GetPositionKey();
            var guestKey = guestBoard.GetPositionKey();
            if (hostKey != guestKey)
            {
                throw new InvalidOperationException($"Desync au coup {i + 1}: host={hostKey} guest={guestKey}");
            }

            var san = Describe(localMove);
            Console.WriteLine($"  ply {i + 1,2}: {san}  one-way {plyWatch.Elapsed.TotalMilliseconds:0.00} ms");
            if (onEvent is not null)
            {
                await onEvent(new
                {
                    type = "move",
                    ply = i + 1,
                    san,
                    side = whiteToMove ? "white" : "black",
                    from = localMove.FromRow * 8 + localMove.FromCol,
                    to = localMove.ToRow * 8 + localMove.ToCol,
                    latencyMs = plyWatch.Elapsed.TotalMilliseconds,
                    synced = true,
                    hostSquares = BoardVisual.Squares(hostBoard),
                    guestSquares = BoardVisual.Squares(guestBoard)
                });
                await Task.Delay(delayMs);
            }
        }

        sw.Stop();

        var hostMoves = MoveGenerator.GenerateLegalMoves(hostBoard);
        var guestMoves = MoveGenerator.GenerateLegalMoves(guestBoard);
        bool hostMate = hostMoves.Count == 0 && hostBoard.IsInCheck(hostBoard.Turn);
        bool guestMate = guestMoves.Count == 0 && guestBoard.IsInCheck(guestBoard.Turn);

        if (expectCheckmate)
        {
            if (!hostMate || !guestMate)
            {
                throw new InvalidOperationException("Le mat n'est pas vu des deux côtés.");
            }

            await host.SendGameEndAsync("Checkmate - White wins");
            await WaitUntil(() => guestEnd == "Checkmate - White wins", TimeSpan.FromSeconds(2));
            Console.WriteLine("  checkmate synced on both peers");
            if (onEvent is not null)
            {
                await onEvent(new { type = "result", message = "Checkmate synced on both peers" });
            }
        }
        else if (hostBoard.GetPiece(7, 6).Type != PieceType.King || hostBoard.GetPiece(7, 5).Type != PieceType.Rook)
        {
            throw new InvalidOperationException("Le roque n'a pas été appliqué après la séquence d'ouverture.");
        }

        Console.WriteLine($"  {script.Count} plies in {sw.Elapsed.TotalMilliseconds:0.00} ms, both boards identical");
    }

    private static void PrintSummary(List<double> latencies)
    {
        if (latencies.Count == 0)
        {
            throw new InvalidOperationException("No move acknowledgements were measured.");
        }

        var max = latencies.Max();
        var avg = latencies.Average();
        Console.WriteLine();
        Console.WriteLine($"RTT samples: {latencies.Count}");
        Console.WriteLine($"RTT avg: {avg:0.00} ms");
        Console.WriteLine($"RTT max: {max:0.00} ms");
        if (max > 250)
        {
            throw new InvalidOperationException($"P2P was not fluid enough: max RTT {max:0.00} ms");
        }

        Console.WriteLine("P2P match check passed: boards stayed in sync and moves were fluid.");
    }

    private static Move ApplyLegal(Board board, int fromRow, int fromCol, int toRow, int toCol, PieceType promotion = PieceType.None)
    {
        var matches = MoveGenerator.GenerateLegalMoves(board)
            .Where(move =>
                move.FromRow == fromRow &&
                move.FromCol == fromCol &&
                move.ToRow == toRow &&
                move.ToCol == toCol &&
                (promotion == PieceType.None || move.Promotion == promotion))
            .ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Coup illégal: {fromRow},{fromCol} -> {toRow},{toCol}");
        }

        var selected = matches.FirstOrDefault(move => move.Promotion == PieceType.Queen, matches[0]);
        board.MakeMove(selected);
        return selected;
    }

    private static string Describe(Move move)
    {
        static char File(int col) => (char)('a' + col);
        static int Rank(int row) => 8 - row;
        return $"{File(move.FromCol)}{Rank(move.FromRow)}{File(move.ToCol)}{Rank(move.ToRow)}";
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var start = Stopwatch.StartNew();
        while (!condition())
        {
            if (start.Elapsed > timeout)
            {
                throw new TimeoutException("Condition P2P non atteinte à temps.");
            }

            await Task.Delay(10);
        }
    }
}
