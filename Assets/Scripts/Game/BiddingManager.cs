using System;

namespace Game29
{
    /// <summary>
    /// Manages the bidding phase of a round of 29.
    ///
    /// Flow:
    ///   StartBidding() → players call PlaceBid() or Pass() in turn
    ///   → BiddingComplete fires OnBiddingComplete when only one bidder remains.
    ///
    /// Rules:
    ///   • Minimum opening bid = 16.
    ///   • Each raise must exceed the current high bid by at least 1.
    ///   • Once a player passes they cannot re-enter.
    ///   • Bidding ends when 3 players have passed (last remaining player wins).
    ///   • If somehow all pass with no bid, the minimum bid (16) is forced.
    /// </summary>
    public class BiddingManager
    {
        // ── State ───────────────────────────────────────────────────────────────
        public int        CurrentHighBid     { get; private set; }
        public PlayerSeat CurrentHighBidder  { get; private set; }
        public PlayerSeat CurrentBidder      { get; private set; }
        public bool       BiddingComplete    { get; private set; }

        private readonly bool[] _hasPassed = new bool[4];

        // ── Events ──────────────────────────────────────────────────────────────
        /// <summary>Fired each time a valid bid is placed.</summary>
        public event Action<PlayerSeat, int> OnBidPlaced;

        /// <summary>Fired each time a player passes.</summary>
        public event Action<PlayerSeat> OnPlayerPassed;

        /// <summary>Fired exactly once when bidding is resolved — carries the winner and final bid.</summary>
        public event Action<PlayerSeat, int> OnBiddingComplete;

        // ── API ─────────────────────────────────────────────────────────────────

        /// <summary>Initialises a new bidding round starting from <paramref name="firstBidder"/>.</summary>
        public void StartBidding(PlayerSeat firstBidder)
        {
            CurrentHighBid    = GameRules.MinBid - 1; // sentinel — no valid bid yet
            CurrentHighBidder = firstBidder;          // default to first player
            CurrentBidder     = firstBidder;
            BiddingComplete   = false;

            for (int i = 0; i < 4; i++)
                _hasPassed[i] = false;
        }

        /// <summary>
        /// Attempt to place a bid. Returns true if accepted.
        /// Only the player whose turn it is may bid.
        /// </summary>
        public bool PlaceBid(PlayerSeat player, int bid)
        {
            if (BiddingComplete || player != CurrentBidder) return false;
            if (!GameRules.IsValidBid(bid, CurrentHighBid))  return false;

            CurrentHighBid    = bid;
            CurrentHighBidder = player;

            OnBidPlaced?.Invoke(player, bid);

            CheckCompletion();
            if (!BiddingComplete) AdvanceToNextActive();

            return true;
        }

        /// <summary>
        /// Current player passes their turn. Returns true if accepted.
        /// </summary>
        public bool Pass(PlayerSeat player)
        {
            if (BiddingComplete || player != CurrentBidder) return false;

            _hasPassed[(int)player] = true;
            OnPlayerPassed?.Invoke(player);

            CheckCompletion();
            if (!BiddingComplete) AdvanceToNextActive();

            return true;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        public bool HasPassed(PlayerSeat player) => _hasPassed[(int)player];

        /// <summary>Lowest bid a player must make to take the lead.</summary>
        public int MinimumRaiseBid() => CurrentHighBid + 1;

        // ── Private ─────────────────────────────────────────────────────────────

        private void CheckCompletion()
        {
            int passedCount = 0;
            for (int i = 0; i < 4; i++)
                if (_hasPassed[i]) passedCount++;

            if (passedCount >= 3)
            {
                BiddingComplete = true;
                // Edge case: nobody ever made a valid bid — force minimum.
                if (CurrentHighBid < GameRules.MinBid)
                    CurrentHighBid = GameRules.MinBid;

                OnBiddingComplete?.Invoke(CurrentHighBidder, CurrentHighBid);
            }
        }

        private void AdvanceToNextActive()
        {
            // Walk clockwise until we find a player who hasn't passed.
            for (int i = 0; i < 4; i++)
            {
                CurrentBidder = GameRules.NextPlayer(CurrentBidder);
                if (!_hasPassed[(int)CurrentBidder]) return;
            }
        }
    }
}
