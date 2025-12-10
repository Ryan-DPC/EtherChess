namespace EtherChess.Models;

public struct Move
{
    public int FromRow { get; set; }
    public int FromCol { get; set; }
    public int ToRow { get; set; }
    public int ToCol { get; set; }
    public PieceType Promotion { get; set; }

    public Move(int fromRow, int fromCol, int toRow, int toCol, PieceType promotion = PieceType.None)
    {
        FromRow = fromRow;
        FromCol = fromCol;
        ToRow = toRow;
        ToCol = toCol;
        Promotion = promotion;
    }
}
