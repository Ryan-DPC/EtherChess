namespace EtherChess.Models;

public class GameHistoryItem
{
    public string Opponent { get; set; } = "";
    public int Rating { get; set; }
    public string Result { get; set; } = "";
    public int Moves { get; set; }
    public string Date { get; set; } = "";
    public bool PlayerWon { get; set; }
    public bool IsDraw { get; set; }
    public bool IsVsBot { get; set; }
    public bool CountsForRating { get; set; }
}
