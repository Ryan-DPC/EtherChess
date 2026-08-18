using System.Diagnostics;
using System.Net;
using System.Threading.Channels;
using EtherChess.Engine;
using EtherChess.Models;
using EtherChess.Network;

var latencies = new List<double>();
var opening = new (int FromRow, int FromCol, int ToRow, int ToCol)[]
{
    (6, 4, 4, 4), // e4
    (1, 4, 3, 4), // e5
    (7, 6, 5, 5), // Nf3
    (0, 1, 2, 2), // Nc6
    (7, 5, 4, 2), // Bc4
    (0, 5, 3, 2), // Bc5
    (6, 2, 5, 2), // c3
    (0, 6, 2, 5), // Nf6
    (6, 3, 4, 3), // d4
    (3, 4, 4, 3), // exd4
    (5, 2, 4, 3), // cxd4
    (3, 2, 4, 1), // Bb4
    (7, 1, 5, 2), // Nc3
    (2, 5, 4, 4), // Nxe4
    (7, 4, 7, 6), // O-O
    (4, 1, 5, 2)  // Bxc3
};

var scholarsMate = new (int FromRow, int FromCol, int ToRow, int ToCol)[]
{
    (6, 4, 4, 4), // e4
    (1, 4, 3, 4), // e5
    (7, 3, 3, 7), // Qh5
    (0, 1, 2, 2), // Nc6
    (7, 5, 4, 2), // Bc4
    (0, 6, 2, 5), // Nf6
    (3, 7, 1, 5)  // Qxf7#
};

Console.WriteLine("=== EtherChess P2P live match ===");
await RunScriptedMatch("Italian opening + castling", opening, expectCheckmate: false, latencies);
await RunScriptedMatch("Scholar's mate", scholarsMate, expectCheckmate: true, latencies);

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

static async Task RunScriptedMatch(
    string title,
    IReadOnlyList<(int FromRow, int FromCol, int ToRow, int ToCol)> script,
    bool expectCheckmate,
    List<double> latencies)
{
    Console.WriteLine();
    Console.WriteLine($"-- {title} --");

    using var host = new PeerSession();
    using var guest = new PeerSession();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    var hostInbox = Channel.CreateUnbounded<Move>();
    var guestInbox = Channel.CreateUnbounded<Move>();
    string? hostEnd = null;
    string? guestEnd = null;

    host.MoveReceived += move => hostInbox.Writer.TryWrite(move);
    guest.MoveReceived += move => guestInbox.Writer.TryWrite(move);
    host.GameEnded += reason => hostEnd = reason;
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

    await host.SendPingAsync();
    await guest.SendPingAsync();

    var hostBoard = new Board();
    var guestBoard = new Board();
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

        Console.WriteLine($"  ply {i + 1,2}: {Describe(localMove)}  one-way {plyWatch.Elapsed.TotalMilliseconds:0.00} ms  key={hostKey[..16]}...");
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
    }
    else if (hostBoard.GetPiece(7, 6).Type != PieceType.King || hostBoard.GetPiece(7, 5).Type != PieceType.Rook)
    {
        throw new InvalidOperationException("Le roque n'a pas été appliqué après la séquence d'ouverture.");
    }

    Console.WriteLine($"  {script.Count} plies in {sw.Elapsed.TotalMilliseconds:0.00} ms, both boards identical");
}

static Move ApplyLegal(Board board, int fromRow, int fromCol, int toRow, int toCol, PieceType promotion = PieceType.None)
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

static string Describe(Move move)
{
    static char file(int col) => (char)('a' + col);
    static int rank(int row) => 8 - row;
    var promo = move.Promotion == PieceType.None ? "" : $"={move.Promotion}";
    return $"{file(move.FromCol)}{rank(move.FromRow)}{file(move.ToCol)}{rank(move.ToRow)}{promo}";
}

static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
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
