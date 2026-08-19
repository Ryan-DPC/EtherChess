using EtherChess.Models;

namespace EtherChess.Network;

public static class PeerMessageType
{
    public const string Hello = "hello";
    public const string Welcome = "welcome";
    public const string Move = "move";
    public const string Ack = "ack";
    public const string GameEnd = "game_end";
    public const string Resign = "resign";
    public const string Ping = "ping";
    public const string Pong = "pong";
}

public sealed class PeerMessage
{
    public string Type { get; set; } = "";
    public string Username { get; set; } = "";
    public int Elo { get; set; }
    public string Color { get; set; } = "";
    public int FromRow { get; set; }
    public int FromCol { get; set; }
    public int ToRow { get; set; }
    public int ToCol { get; set; }
    public string Promotion { get; set; } = nameof(PieceType.None);
    public string Reason { get; set; } = "";
    public long Timestamp { get; set; }
    public int Sequence { get; set; }

    public Move ToMove()
    {
        var promotion = Enum.TryParse<PieceType>(Promotion, true, out var parsed)
            ? parsed
            : PieceType.None;
        return new Move(FromRow, FromCol, ToRow, ToCol, promotion);
    }

    public static PeerMessage FromMove(Move move, int sequence, long timestamp)
    {
        return new PeerMessage
        {
            Type = PeerMessageType.Move,
            FromRow = move.FromRow,
            FromCol = move.FromCol,
            ToRow = move.ToRow,
            ToCol = move.ToCol,
            Promotion = move.Promotion.ToString(),
            Sequence = sequence,
            Timestamp = timestamp
        };
    }
}
