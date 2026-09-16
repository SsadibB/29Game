using System;

namespace Game29
{
    /// <summary>
    /// Tracks game-level scores across multiple rounds.
    ///
    /// Scoring:
    ///   • Only the bidding team’s game score changes.
    ///   • Bidding team meets or exceeds their <see cref="EffectiveTarget"/>
    ///     (the bid, shifted ±4 by a declared Marriage) → their score +1,
    ///     or +2 if they swept all 28 card points that board.
    ///   • Bidding team falls short of the target → their score −1.
    ///   • The defending team’s score is unchanged either way.
    ///   • First team to <see cref="GameRules.GamePointsToWin"/> game points wins.
    ///
    /// Teams: 0 = South+North (human), 1 = East+West.
    /// </summary>
    public class ScoreManager
    {
        // ── State ───────────────────────────────────────────────────────────────
        public int[] GamePoints { get; private set; } = new int[2];

        public PlayerSeat BidWinner { get; private set; }
        public int CurrentBid { get; private set; }
        public int BiddingTeam { get; private set; }

        /// <summary>
        /// The card-point total the calling team actually needs to hit this
        /// round. Starts equal to <see cref="CurrentBid"/> but can shift ±4
        /// (clamped to [<see cref="GameRules.MinBid"/>, <see cref="GameRules.MaxBid"/>])
        /// if a Marriage is declared — see <see cref="DeclareMarriage"/>.
        /// </summary>
        public int EffectiveTarget { get; private set; }

        /// <summary>True once a Marriage has been declared for this round (one per round).</summary>
        public bool MarriageDeclared { get; private set; }

        /// <summary>The game-point delta applied by the most recent <see cref="ScoreRound"/> call.</summary>
        public int LastRoundDelta { get; private set; }

        /// <summary>True if the most recent round was won by sweeping all 28 card points.</summary>
        public bool WasAllPointsSweep { get; private set; }

        /// <summary>Current round's Double/Re-Double status.</summary>
        public DoubleStatus CurrentDoubleStatus { get; private set; } = DoubleStatus.None;
        public PlayerSeat? Doubler { get; private set; }
        public PlayerSeat? ReDoubler { get; private set; }
        public DoubleStatus LastDoubleStatus { get; private set; } = DoubleStatus.None;

        public const int SingleHandDelta = 3;
        public bool LastRoundWasSingleHand { get; private set; }
        public bool LastSingleHandSuccess { get; private set; }
        public int LastSingleHandTeam { get; private set; }

        // ── Events ──────────────────────────────────────────────────────────────
        /// <summary>Fired after each round is scored. Params: biddingTeam, bid, didBiddingTeamWin.</summary>
        public event Action<int, int, bool> OnRoundScored;

        /// <summary>Fired when a team reaches the winning threshold. Param: winning team index.</summary>
        public event Action<int> OnGameOver;

        /// <summary>Fired when a Marriage is declared. Params: declaringTeam, new EffectiveTarget.</summary>
        public event Action<int, int> OnMarriageDeclared;

        // ── API ─────────────────────────────────────────────────────────────────

        /// <summary>Register the winning bidder and their bid before trick-play begins.</summary>
        public void RegisterBid(PlayerSeat winner, int bid)
        {
            BidWinner = winner;
            CurrentBid = bid;
            BiddingTeam = GameRules.GetTeam(winner);
            EffectiveTarget = bid;
            MarriageDeclared = false;
            CurrentDoubleStatus = DoubleStatus.None;
            Doubler = null;
            ReDoubler = null;
        }

        /// <summary>Applies Double by <paramref name="doubler"/> for the current round.</summary>
        public void SetDouble(PlayerSeat doubler)
        {
            CurrentDoubleStatus = DoubleStatus.Double;
            Doubler = doubler;
        }

        /// <summary>Applies Re-Double by <paramref name="reDoubler"/> for the current round.</summary>
        public void SetReDouble(PlayerSeat reDoubler)
        {
            CurrentDoubleStatus = DoubleStatus.ReDouble;
            ReDoubler = reDoubler;
        }

        /// <summary>
        /// Declares a Marriage for <paramref name="declaringTeam"/>, shifting the
        /// calling team's target by <see cref="GameRules.MarriageTargetShift"/>:
        /// down if the calling team itself declares it, up if the opposing team
        /// does. Only one Marriage may be declared per round. Caller (GameManager)
        /// is responsible for validating trump-revealed / King+Queen-in-hand /
        /// team-has-won-a-trick before invoking this.
        /// </summary>
        public bool DeclareMarriage(int declaringTeam)
        {
            if (MarriageDeclared) return false;

            int shift = declaringTeam == BiddingTeam
                ? -GameRules.MarriageTargetShift
                : GameRules.MarriageTargetShift;

            EffectiveTarget = Math.Max(GameRules.MinBid, Math.Min(GameRules.MaxBid, EffectiveTarget + shift));
            MarriageDeclared = true;

            OnMarriageDeclared?.Invoke(declaringTeam, EffectiveTarget);
            return true;
        }

        /// <summary>
        /// Called after all tricks are played (or the round is skipped early
        /// once the target is already met).
        /// <paramref name="teamPoints"/>[0] = card-points won by South+North,
        /// <paramref name="teamPoints"/>[1] = card-points won by East+West.
        /// </summary>
        public void ScoreRound(int[] teamPoints)
        {
            bool biddingTeamWon = teamPoints[BiddingTeam] >= EffectiveTarget;
            bool allPointsSweep = teamPoints[BiddingTeam] == GameRules.TotalCardPoints;

            int delta;
            if (CurrentDoubleStatus == DoubleStatus.ReDouble)
            {
                delta = biddingTeamWon ? GameRules.ReDoubleWinBonus : GameRules.ReDoubleLossPenalty;
            }
            else if (CurrentDoubleStatus == DoubleStatus.Double)
            {
                delta = biddingTeamWon ? GameRules.DoubleWinBonus : GameRules.DoubleLossPenalty;
            }
            else
            {
                if (biddingTeamWon)
                    delta = allPointsSweep ? GameRules.AllPointsBonus : GameRules.RoundWinBonus;
                else
                    delta = GameRules.RoundLossPenalty;
            }

            GamePoints[BiddingTeam] += delta;
            GamePoints[BiddingTeam] = Math.Max(-GameRules.GamePointsToWin, Math.Min(GameRules.GamePointsToWin, GamePoints[BiddingTeam]));

            LastRoundDelta = delta;
            LastDoubleStatus = CurrentDoubleStatus;
            WasAllPointsSweep = biddingTeamWon && allPointsSweep;
            LastRoundWasSingleHand = false;

            OnRoundScored?.Invoke(BiddingTeam, CurrentBid, biddingTeamWon);

            // Check for game-over (+6 wins, -6 loses to opposing team).
            int winningTeam = GetWinningTeam();
            if (winningTeam >= 0)
            {
                OnGameOver?.Invoke(winningTeam);
            }
        }

        /// <summary>
        /// Scores a Single Hand round with a fixed +3 / -3 Set Point delta,
        /// independent of Double/Re-Double or normal bid targets. Clamped to [-6, +6].
        /// </summary>
        public void ScoreSingleHand(int singleTeam, bool success)
        {
            int delta = success ? SingleHandDelta : -SingleHandDelta;
            GamePoints[singleTeam] += delta;
            GamePoints[singleTeam] = Math.Max(-GameRules.GamePointsToWin, Math.Min(GameRules.GamePointsToWin, GamePoints[singleTeam]));

            LastRoundDelta = delta;
            LastDoubleStatus = DoubleStatus.None;
            WasAllPointsSweep = false;
            LastRoundWasSingleHand = true;
            LastSingleHandSuccess = success;
            LastSingleHandTeam = singleTeam;

            OnRoundScored?.Invoke(singleTeam, CurrentBid, success);

            int winningTeam = GetWinningTeam();
            if (winningTeam >= 0)
            {
                OnGameOver?.Invoke(winningTeam);
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
            {
                if (GamePoints[t] >= GameRules.GamePointsToWin) return t;
                if (GamePoints[t] <= -GameRules.GamePointsToWin) return 1 - t;
            }
            return -1;
        }

        public bool IsGameOver() => GetWinningTeam() >= 0;

        /// <summary>Formatted score string for logging / debug overlay.</summary>
        public string GetScoreString()
            => $"{GameRules.TeamName(0)}: {GamePoints[0]}  |  {GameRules.TeamName(1)}: {GamePoints[1]}";
    }
}