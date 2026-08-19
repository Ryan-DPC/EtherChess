namespace EtherChess.Models;

public enum PieceType { None, Pawn, Knight, Bishop, Rook, Queen, King }
public enum PieceColor { None, White, Black }

public struct Piece
{
    public PieceType Type { get; set; }
    public PieceColor Color { get; set; }

    public Piece(PieceType type, PieceColor color)
    {
        Type = type;
        Color = color;
    }

    public static Piece None => new Piece(PieceType.None, PieceColor.None);
    
    public bool IsWhite => Color == PieceColor.White;
    public bool IsBlack => Color == PieceColor.Black;
    public bool IsEmpty => Type == PieceType.None;

    public char GetFenChar()
    {
        char c = Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => ' '
        };
        return IsWhite ? char.ToUpper(c) : c;
    }
}
