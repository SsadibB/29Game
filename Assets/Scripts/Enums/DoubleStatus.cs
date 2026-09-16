namespace Game29
{
    /// <summary>
    /// Represents the Double and Re-Double state for the current round.
    /// Resets at the start of each new deal/bidding round.
    /// </summary>
    public enum DoubleStatus
    {
        /// <summary>Standard round without Double or Re-Double.</summary>
        None = 0,

        /// <summary>Opposing team doubled the bid (+2 Set Points on win, -2 on loss).</summary>
        Double = 1,

        /// <summary>Bidding team re-doubled (+4 Set Points on win, -4 on loss).</summary>
        ReDouble = 2
    }
}
