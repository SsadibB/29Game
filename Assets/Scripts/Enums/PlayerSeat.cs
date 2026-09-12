namespace Game29
{
    /// <summary>
    /// The four seating positions at the table.
    /// Clockwise order: South(0) → West(1) → North(2) → East(3) → South.
    ///
    /// Teams:
    ///   Team 0 = South + North  (human team)
    ///   Team 1 = East  + West   (AI team)
    ///
    /// South is ALWAYS the human player.
    /// </summary>
    public enum PlayerSeat
    {
        South = 0,  // Human player
        West  = 1,  // AI
        North = 2,  // AI — human's partner
        East  = 3   // AI
    }
}
