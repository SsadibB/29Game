using System;

namespace Game29
{
    /// <summary>
    /// Tracks game-level scores across multiple rounds.
    ///
    /// Scoring:
    ///   • Bidding team meets or exceeds bid → bidding team gains +1 game point.
    ///   • Bidding team falls short of bid   → defending team gains +1 game point.
    ///   • First team to <see cref="GameRules.GamePointsToWin"/> game points wins.
    ///
    /// Teams: 0 = South+North (human), 1 = East+West.
    /// </summary>
    public class ScoreManager
    {
        // ── State ───────────────────────────────────────────────────────────────
        public int[] GamePoints { get; private set; } = new int[2];

        public PlayerSeat BidWinner    { get; private set; }
        public int        CurrentBid   { get; private set; }
        public int        BiddingTeam  { get; private set; }

        // ── Events ──────────────────────────────────────────────────────────────
        /// <summary>Fired after each round is scored. Params: biddingTeam, bid, didBiddingTeamWin.</summary>
        public event Action<int, int, bool> OnRoundScored;

        /// <summary>Fired when a team reaches the winning threshold. Param: winning team index.</summary>
        public event Action<int> OnGameOver;

        // ── API ─────────────────────────────────────────────────────────────────

        /// <summary>Register the winning bidder and their bid before trick-play begins.</summary>
        public void RegisterBid(PlayerSeat winner, int bid)
        {
            BidWinner   = winner;
            CurrentBid  = bid;
            BiddingTeam = GameRules.GetTeam(winner);
        }

        /// <summary>
        /// Called after all tricks are played.
        /// <paramref name="teamPoints"/>[0] = card-points won by South+North,
        /// <paramref name="teamPoints"/>[1] = card-points won by East+West.
        /// </summary>
        public void ScoreRound(int[] teamPoints)
        {
            bool biddingTeamWon = teamPoints[BiddingTeam] >= CurrentBid;

            if (biddingTeamWon)
                GamePoints[BiddingTeam]++;
            else
                GamePoints[1 - BiddingTeam]++;

            OnRoundScored?.Invoke(BiddingTeam, CurrentBid, biddingTeamWon);

            // Check for game-over.
            for (int t = 0; t < 2; t++)
            {
                if (GamePoints[t] >= GameRules.GamePointsToWin)
                {
                    OnGameOver?.Invoke(t);
                    return;
                }
            }
        }

        /// <summary>Fully resets scores for a new game.</summary>
        public void ResetGame()
        {
            GamePoints = new int[2];
        }

        // ── Queries ─────────────────────────────────────────────────────────────

        /// <summary>Returns the winning team index, or -1 if the game is still in progress.</summary>
        public int GetWinningTeam()
        {
            for (int t = 0; t < 2; t++)
                if (GamePoints[t] >= GameRules.GamePointsToWin) return t;
            return -1;
        }

        public bool IsGameOver() => GetWinningTeam() >= 0;

        /// <summary>Formatted score string for logging / debug overlay.</summary>
        public string GetScoreString()
            => $"{GameRules.TeamName(0)}: {GamePoints[0]}  |  {GameRules.TeamName(1)}: {GamePoints[1]}";
    }
}
