namespace Game29
{
    /// <summary>
    /// Stateless repository of 29 game constants and utility lookups.
    /// All game-rule logic that doesn't belong to a specific manager lives here.
    /// </summary>
    public static class GameRules
    {
        // ── Bidding ────────────────────────────────────────────────────────────
        /// <summary>Lowest legal bid (half of 28 card-points + some).</summary>
        public const int MinBid = 16;

        /// <summary>Highest legal bid (all card-points in the deck).</summary>
        public const int MaxBid = 28;

        // ── Round structure ────────────────────────────────────────────────────
        public const int TricksPerRound   = 8;
        public const int CardsPerPlayer   = 8;
        public const int TotalCardPoints  = 28; // 4×(J3+92+A1+101) = 28

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

        // ── String helpers ─────────────────────────────────────────────────────

        public static string TeamName(int team)
            => team == 0 ? "South & North" : "East & West";
    }
}
