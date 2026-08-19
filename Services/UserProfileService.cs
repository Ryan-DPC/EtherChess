using EtherChess.Models;

namespace EtherChess.Services;

public class UserProfileService
{
    public UserProfile Profile { get; } = new();

    public void RecordGame(
        bool playerWon,
        bool isDraw,
        bool isVsBot,
        int moves,
        string opponent,
        string result,
        int eloDelta)
    {
        Profile.TotalGames++;

        Profile.RecentGames.Insert(0, new GameHistoryItem
        {
            Opponent = opponent,
            Rating = isVsBot ? 0 : Profile.Elo,
            Result = result,
            Moves = moves,
            Date = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            PlayerWon = playerWon,
            IsDraw = isDraw,
            IsVsBot = isVsBot,
            CountsForRating = !isVsBot
        });

        while (Profile.RecentGames.Count > 20)
        {
            Profile.RecentGames.RemoveAt(Profile.RecentGames.Count - 1);
        }

        if (isVsBot)
        {
            UpdateWinRate();
            return;
        }

        Profile.RatedGames++;
        if (playerWon)
        {
            Profile.RatedWins++;
        }

        if (eloDelta != 0)
        {
            Profile.Elo = Math.Max(100, Profile.Elo + eloDelta);
        }

        UpdateWinRate();
    }

    private void UpdateWinRate()
    {
        if (Profile.RatedGames == 0)
        {
            Profile.WinRate = "—";
            return;
        }

        var pct = (int)Math.Round(100.0 * Profile.RatedWins / Profile.RatedGames);
        Profile.WinRate = $"{pct}%";
    }
}
