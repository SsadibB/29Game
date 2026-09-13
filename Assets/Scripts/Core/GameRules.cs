namespace Game29
{
    /// <summary>
    /// Stateless repository of 29 game constants and utility lookups.
    /// All game-rule logic that doesn't belong to a specific manager lives here.
    /// </summary>
    public static class GameRules
    {
        // ── Bidding ────────────────────────────────────────────────────────────
        /// <summary>Usual minimum opening bid (16–28).</summary>
        public const int MinBid = 16;

        /// <summary>Highest legal bid (all card-points in the deck, excluding the last-trick bonus).</summary>
        public const int MaxBid = 28;

        // ── Round structure ────────────────────────────────────────────────────
        public const int TricksPerRound   = 8;
        public const int CardsPerPlayer   = 8;
        public const int TotalCardPoints  = 28; // 4×(J3 + 9=2 + A1 + 10=1) = 28
        public const int FinalTrickBonus  = 1;  // 8th trick is worth +1, making 29 total
        public const int TotalRoundPoints = 29;

        // ── Game scoring ────────────────────────────────────────────────────────
        /// <summary>First team to reach this many game-points wins the game.</summary>
        public const int GamePointsToWin = 6;

        // ── Seat / team helpers ────────────────────────────────────────────────

        /// <summary>Returns the partner seat (North↔South, East↔West).</summary>
        public static PlayerSeat GetPartner(PlayerSeat seat)
        {
            switch (seat)
            {
                case PlayerSeat.South: return PlayerSeat.North;
                case PlayerSeat.North: return PlayerSeat.South;
                case PlayerSeat.East:  return PlayerSeat.West;
                case PlayerSeat.West:  return PlayerSeat.East;
                default:               return seat;
            }
        }

        /// <summary>Team 0 = South+North (human team). Team 1 = East+West.</summary>
        public static int GetTeam(PlayerSeat seat)
            => (seat == PlayerSeat.South || seat == PlayerSeat.North) ? 0 : 1;

        /// <summary>Next player clockwise: South→West→North→East→South.</summary>
        public static PlayerSeat NextPlayer(PlayerSeat current)
            => (PlayerSeat)(((int)current + 1) % 4);

        // ── Bid validation ─────────────────────────────────────────────────────

        /// <summary>True if <paramref name="bid"/> is a legal raise over the current high bid.</summary>
        public static bool IsValidBid(int bid, int currentHighBid)
            => bid >= MinBid && bid <= MaxBid && bid > currentHighBid;

        // ── Card ranking (29 order, not poker order) ───────────────────────────
        // J > 9 > A > 10 > K > Q > 8 > 7

        /// <summary>
        /// Hand display suit order: Spades, Clubs, Hearts, Diamonds (left to right).
        /// </summary>
        public static int GetHandSuitOrder(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spades:   return 0;
                case Suit.Clubs:    return 1;
                case Suit.Hearts:   return 2;
                case Suit.Diamonds: return 3;
                default:            return 4;
            }
        }
        public static int GetTrickRank(Rank rank)
        {
            switch (rank)
            {
                case Rank.Jack:  return 8;
                case Rank.Nine:  return 7;
                case Rank.Ace:   return 6;
                case Rank.Ten:   return 5;
                case Rank.King:  return 4;
                case Rank.Queen: return 3;
                case Rank.Eight: return 2;
                case Rank.Seven: return 1;
                default:         return 0;
            }
        }

        /// <summary>
        /// Joker-mode hierarchy for Jacks (highest to lowest):
        /// Spades &gt; Hearts &gt; Diamonds &gt; Clubs.
        /// </summary>
        public static int GetJokerRank(Suit suit) => (int)suit;

        /// <summary>In Joker trump mode, every Jack is a Joker (super-trump).</summary>
        public static bool IsJokerCard(Card card) => card != null && card.Rank == Rank.Jack;

        /// <summary>
        /// True if <paramref name="challenger"/> beats <paramref name="current"/> in a trick.
        /// Suit/7th-card: trump suit, then 29 rank, then led suit over off-suit.
        /// Joker mode: Jacks beat every non-Jack; Jacks compare by joker hierarchy.
        /// </summary>
        public static bool Beats(Card challenger, Card current, Suit? ledSuit, Suit? trumpSuit, TrumpMode mode = TrumpMode.Suit)
        {
            if (challenger == null || current == null) return false;

            if (mode == TrumpMode.Joker)
                return BeatsJoker(challenger, current, ledSuit);

            bool challengerIsTrump = trumpSuit.HasValue && challenger.Suit == trumpSuit.Value;
            bool currentIsTrump    = trumpSuit.HasValue && current.Suit    == trumpSuit.Value;

            if (challengerIsTrump && !currentIsTrump) return true;
            if (!challengerIsTrump && currentIsTrump)  return false;

            if (challenger.Suit == current.Suit)
                return GetTrickRank(challenger.Rank) > GetTrickRank(current.Rank);

            if (ledSuit.HasValue && challenger.Suit == ledSuit.Value && current.Suit != ledSuit.Value)
                return true;

            return false;
        }

        private static bool BeatsJoker(Card challenger, Card current, Suit? ledSuit)
        {
            bool challengerJoker = IsJokerCard(challenger);
            bool currentJoker    = IsJokerCard(current);

            if (challengerJoker && !currentJoker) return true;
            if (!challengerJoker && currentJoker)  return false;
            if (challengerJoker && currentJoker)
                return GetJokerRank(challenger.Suit) > GetJokerRank(current.Suit);

            if (challenger.Suit == current.Suit)
                return GetTrickRank(challenger.Rank) > GetTrickRank(current.Rank);

            if (ledSuit.HasValue && challenger.Suit == ledSuit.Value && current.Suit != ledSuit.Value)
                return true;

            return false;
        }

        // ── String helpers ─────────────────────────────────────────────────────

        public static string TeamName(int team)
            => team == 0 ? "South & North" : "East & West";
    }
}
