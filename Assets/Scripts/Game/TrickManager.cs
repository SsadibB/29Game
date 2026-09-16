using System;
using System.Collections.Generic;

namespace Game29
{
    /// <summary>
    /// Manages all 8 tricks in a round.
    ///
    /// Responsibilities:
    ///   • Owns the active Trick object.
    ///   • Validates each card play (follow-suit rule).
    ///   • Resolves trick winners.
    ///   • Accumulates per-team card-points.
    ///   • Fires events for each play, each trick win, and round completion.
    /// </summary>
    public class TrickManager
    {
        // ── Dependencies ────────────────────────────────────────────────────────
        private readonly TrumpManager _trump;

        // ── State ───────────────────────────────────────────────────────────────
        private Trick _currentTrick;
        private int _tricksCompleted;
        private int[] _teamPoints = new int[2];
        private int[] _tricksTaken = new int[4]; // per seat
        private bool _isSinglePlay;
        private PlayerSeat? _disabledSeat;

        public Trick CurrentTrick => _currentTrick;
        public Trick LastCompletedTrick { get; private set; }
        public int TricksCompleted => _tricksCompleted;
        public bool RoundComplete => _tricksCompleted >= GameRules.TricksPerRound;
        public bool IsSinglePlay => _isSinglePlay;
        public PlayerSeat? DisabledSeat => _disabledSeat;
        public int RequiredPlaysPerTrick => _isSinglePlay ? 3 : 4;

        // ── Events ──────────────────────────────────────────────────────────────
        /// <summary>Fired immediately after each card is played to the trick.</summary>
        public event Action<PlayerSeat, Card> OnCardPlayed;

        /// <summary>Fired when a trick is won. Carries winner seat and points taken.</summary>
        public event Action<PlayerSeat, int> OnTrickWon;

        /// <summary>Fired once all 8 tricks are complete.</summary>
        public event Action OnRoundComplete;

        // ── Constructor ─────────────────────────────────────────────────────────
        public TrickManager(TrumpManager trumpManager)
        {
            _trump = trumpManager;
        }

        // ── API ─────────────────────────────────────────────────────────────────

        /// <summary>Resets accumulated card points to 0–0 at the start of a new board/deal.</summary>
        public void ResetPoints()
        {
            _teamPoints = new int[2];
            _tricksCompleted = 0;
            _tricksTaken = new int[4];
            _currentTrick = null;
            LastCompletedTrick = null;
        }

        /// <summary>Resets state and starts the first trick led by <paramref name="firstLeader"/>.</summary>
        public void StartRound(PlayerSeat firstLeader)
        {
            ResetPoints();
            BeginTrick(firstLeader);
        }

        /// <summary>Configures Single Play mode and the disabled partner seat for this round.</summary>
        public void SetSinglePlay(bool isSinglePlay, PlayerSeat? disabledSeat)
        {
            _isSinglePlay = isSinglePlay;
            _disabledSeat = isSinglePlay ? disabledSeat : null;
        }

        /// <summary>
        /// Attempts to play a card for <paramref name="player"/>.
        /// Returns false if it's not that player's turn or the card is illegal.
        /// </summary>
        public bool PlayCard(PlayerSeat player, Card card, Hand playerHand)
        {
            if (RoundComplete || _currentTrick == null || _currentTrick.IsComplete)
                return false;

            if (GetCurrentPlayer() != player) return false;

            // Validate against follow-suit rule.
            List<Card> validPlays = playerHand.GetValidPlays(_currentTrick);
            if (!validPlays.Contains(card)) return false;

            // Playing a trump-suit card does not reveal hidden trump.
            _trump.NotifyCardPlayed(card);

            playerHand.RemoveCard(card);
            _currentTrick.AddPlay(player, card);
            OnCardPlayed?.Invoke(player, card);

            if (_currentTrick.IsComplete)
                ResolveTrick();

            return true;
        }

        /// <summary>
        /// Returns whose turn it is to play in the current trick.
        /// Seat order follows clockwise from the trick leader.
        /// </summary>
        public PlayerSeat GetCurrentPlayer()
        {
            if (_currentTrick == null) return PlayerSeat.South;
            PlayerSeat seat = _currentTrick.Leader;
            for (int i = 0; i < _currentTrick.PlayCount; i++)
            {
                seat = GameRules.NextPlayer(seat);
                if (_isSinglePlay && _disabledSeat.HasValue && seat == _disabledSeat.Value)
                    seat = GameRules.NextPlayer(seat);
            }
            return seat;
        }

        /// <summary>
        /// Ends the round immediately without playing out the remaining
        /// tricks — backs the "Skip" button once the round's outcome is
        /// already decided. Legal at any point, including mid-trick: if the
        /// current trick has some cards already played, that partial trick
        /// is simply discarded unresolved (no team gets credit for it).
        /// Whatever card-points earlier, fully-resolved tricks already won
        /// stand as-is.
        /// </summary>
        public bool SkipRemaining()
        {
            if (RoundComplete) return false;

            _tricksCompleted = GameRules.TricksPerRound;
            _currentTrick = null;
            OnRoundComplete?.Invoke();
            return true;
        }

        /// <summary>Immediately terminates round without firing OnRoundComplete (e.g. Single Hand early failure).</summary>
        public void TerminateRoundEarly()
        {
            _tricksCompleted = GameRules.TricksPerRound;
            _currentTrick = null;
        }

        // ── Accessors ───────────────────────────────────────────────────────────

        /// <summary>Returns a copy of per-team card-point totals.</summary>
        public int[] GetTeamPoints() => (int[])_teamPoints.Clone();

        /// <summary>Returns a copy of tricks-taken counts per seat.</summary>
        public int[] GetTricksTaken() => (int[])_tricksTaken.Clone();

        public int GetTeamPoints(int team) => _teamPoints[team];

        // ── Private ─────────────────────────────────────────────────────────────

        private void BeginTrick(PlayerSeat leader)
        {
            _currentTrick = new Trick(leader, RequiredPlaysPerTrick);
        }

        private void ResolveTrick()
        {
            // Hidden trump does not count until it has been revealed.
            Suit? effectiveTrump = _trump.TrumpRevealed ? _trump.TrumpSuit : null;
            PlayerSeat winner = _currentTrick.DetermineWinner(effectiveTrump, _trump.Mode);
            int points = _currentTrick.TotalPoints();

            // The team that wins the 8th (final) trick scores +1, for 29 total points.
            bool isFinalTrick = _tricksCompleted == GameRules.TricksPerRound - 1;
            if (isFinalTrick)
                points += GameRules.FinalTrickBonus;

            _tricksTaken[(int)winner]++;
            _teamPoints[GameRules.GetTeam(winner)] += points;
            _tricksCompleted++;
            LastCompletedTrick = _currentTrick;

            OnTrickWon?.Invoke(winner, points);

            if (RoundComplete)
                OnRoundComplete?.Invoke();
            else
                BeginTrick(winner);
        }
    }
}