using System;

namespace Game29
{
    /// <summary>
    /// Manages the bidding phase of a round of 29.
    ///
    /// Order:
    ///   The player clockwise from the dealer speaks first, then the table
    ///   continues clockwise (East dealer → South, West, North, East).
    ///   When a raise beats the current leader, that leader must immediately
    ///   bid higher or pass before the auction moves on. After they pass,
    ///   play resumes with the next clockwise player after the new leader.
    ///
    /// Rules:
    ///   • Minimum opening bid = 16. A bid must be strictly higher than the current high bid.
    ///   • Once a player passes they cannot bid again this round.
    ///   • The leader is not asked to act again until someone outbids them.
    ///   • Bidding ends when every player except the high bidder has passed.
    ///   • If all four pass with no bid, the first bidder is forced to the minimum (16).
    /// </summary>
    public class BiddingManager
    {
        // ── State ───────────────────────────────────────────────────────────────
        public int        CurrentHighBid     { get; private set; }
        public PlayerSeat CurrentHighBidder  { get; private set; }
        public PlayerSeat CurrentBidder      { get; private set; }
        public bool       BiddingComplete    { get; private set; }
        public bool       HasHighBid         { get; private set; }

        private readonly bool[] _hasPassed = new bool[4];
        private PlayerSeat _firstBidder;
        /// <summary>True while the player who was just outbid must answer before the table moves on.</summary>
        private bool _responsePending;

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
            CurrentHighBidder = firstBidder;
            CurrentBidder     = firstBidder;
            BiddingComplete   = false;
            HasHighBid        = false;
            _firstBidder      = firstBidder;
            _responsePending  = false;

            for (int i = 0; i < 4; i++)
                _hasPassed[i] = false;
        }

        /// <summary>
        /// Attempt to place a bid. Returns true if accepted.
        /// Only the player whose turn it is may bid, and a player who already
        /// passed cannot bid again. The bid must be strictly higher than the
        /// current high bid.
        /// </summary>
        public bool PlaceBid(PlayerSeat player, int bid)
        {
            if (BiddingComplete || player != CurrentBidder) return false;
            if (_hasPassed[(int)player]) return false;
            if (!GameRules.IsValidBid(bid, CurrentHighBid)) return false;

            PlayerSeat previousLeader = CurrentHighBidder;
            bool hadLeader = HasHighBid;
            _responsePending = false;

            CurrentHighBid    = bid;
            CurrentHighBidder = player;
            HasHighBid        = true;

            OnBidPlaced?.Invoke(player, bid);

            // The player who just lost the lead answers immediately (raise or pass)
            // before the auction continues clockwise.
            if (hadLeader && previousLeader != player && !_hasPassed[(int)previousLeader])
            {
                CurrentBidder = previousLeader;
                _responsePending = true;
                return true;
            }

            CheckCompletion();
            if (!BiddingComplete) AdvanceClockwiseFrom(player);

            return true;
        }

        /// <summary>
        /// Current player passes. Returns true if accepted.
        /// A pass removes that player from the rest of this bidding round.
        /// </summary>
        public bool Pass(PlayerSeat player)
        {
            if (BiddingComplete || player != CurrentBidder) return false;
            if (_hasPassed[(int)player]) return false;

            _hasPassed[(int)player] = true;
            bool wasResponse = _responsePending;
            _responsePending = false;
            OnPlayerPassed?.Invoke(player);

            CheckCompletion();
            if (BiddingComplete) return true;

            // After the outbid player passes, resume clockwise from the leader
            // (East dealer, West holds the bid, South passes → North is next).
            if (wasResponse && HasHighBid)
                AdvanceClockwiseFrom(CurrentHighBidder);
            else
                AdvanceClockwiseFrom(player);

            return true;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        public bool HasPassed(PlayerSeat player) => _hasPassed[(int)player];

        /// <summary>Lowest bid a player must make to take the lead.</summary>
        public int MinimumRaiseBid() => CurrentHighBid + 1;

        // ── Private ─────────────────────────────────────────────────────────────

        private int PassedCount()
        {
            int passedCount = 0;
            for (int i = 0; i < 4; i++)
                if (_hasPassed[i]) passedCount++;
            return passedCount;
        }

        private void CheckCompletion()
        {
            int passedCount = PassedCount();

            if (HasHighBid && passedCount >= 3)
            {
                CompleteBidding(CurrentHighBidder, CurrentHighBid);
                return;
            }

            // Nobody bid. The opener is stuck with the minimum.
            if (!HasHighBid && passedCount >= 4)
            {
                CompleteBidding(_firstBidder, GameRules.MinBid);
            }
        }

        private void CompleteBidding(PlayerSeat winner, int bid)
        {
            if (BiddingComplete) return;
            BiddingComplete   = true;
            CurrentHighBidder = winner;
            CurrentHighBid    = bid;
            HasHighBid        = true;
            OnBiddingComplete?.Invoke(winner, bid);
        }

        /// <summary>
        /// Next clockwise player who has not passed and is not the current leader.
        /// The leader only acts when PlaceBid hands them the response turn.
        /// </summary>
        private void AdvanceClockwiseFrom(PlayerSeat from)
        {
            PlayerSeat cursor = from;
            for (int i = 0; i < 4; i++)
            {
                cursor = GameRules.NextPlayer(cursor);
                if (_hasPassed[(int)cursor]) continue;
                if (HasHighBid && cursor == CurrentHighBidder) continue;

                CurrentBidder = cursor;
                return;
            }

            if (HasHighBid)
                CompleteBidding(CurrentHighBidder, CurrentHighBid);
        }
    }
}
