using System.Collections.ObjectModel;

namespace EtherChess.Models;

public class UserProfile
{
    public int Elo { get; set; } = 1200;
    public int TotalGames { get; set; }
    public int RatedGames { get; set; }
    public int RatedWins { get; set; }
    public string WinRate { get; set; } = "—";
    public ObservableCollection<GameHistoryItem> RecentGames { get; } = new();
}
