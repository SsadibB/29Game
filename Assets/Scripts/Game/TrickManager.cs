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
        private int   _tricksCompleted;
        private int[] _teamPoints    = new int[2];
        private int[] _tricksTaken   = new int[4]; // per seat

        public Trick CurrentTrick        => _currentTrick;
        public Trick LastCompletedTrick  { get; private set; }
        public int   TricksCompleted     => _tricksCompleted;
        public bool  RoundComplete   => _tricksCompleted >= GameRules.TricksPerRound;

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

        /// <summary>Resets state and starts the first trick led by <paramref name="firstLeader"/>.</summary>
        public void StartRound(PlayerSeat firstLeader)
        {
            _tricksCompleted = 0;
            _teamPoints      = new int[2];
            _tricksTaken     = new int[4];
            BeginTrick(firstLeader);
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

            // Notify trump manager — reveals trump if a trump card is played.
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
                seat = GameRules.NextPlayer(seat);
            return seat;
        }

        // ── Accessors ───────────────────────────────────────────────────────────

        /// <summary>Returns a copy of per-team card-point totals.</summary>
        public int[] GetTeamPoints()    => (int[])_teamPoints.Clone();

        /// <summary>Returns a copy of tricks-taken counts per seat.</summary>
        public int[] GetTricksTaken()   => (int[])_tricksTaken.Clone();

        public int GetTeamPoints(int team) => _teamPoints[team];

        // ── Private ─────────────────────────────────────────────────────────────

        private void BeginTrick(PlayerSeat leader)
        {
            _currentTrick = new Trick(leader);
        }

        private void ResolveTrick()
        {
            // Use fully visible trump if revealed; otherwise the trick resolves without trump awareness.
            Suit? effectiveTrump = _trump.TrumpRevealed ? _trump.TrumpSuit : null;
            // Note: for winner determination we always use the actual trump — the
            // game will reveal it naturally through NotifyCardPlayed calls before this.
            effectiveTrump = _trump.TrumpSuit; // internal resolution uses real trump

            PlayerSeat winner = _currentTrick.DetermineWinner(effectiveTrump);
            int        points = _currentTrick.TotalPoints();

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
