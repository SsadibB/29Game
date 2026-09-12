namespace Game29
{
    /// <summary>
    /// Supported trump selection types in 29:
    ///   • Suit: Standard chosen suit (Hearts, Diamonds, Clubs, Spades).
    ///   • SeventhCard: Blind trump determined by the bidder's 7th dealt card (revealed on demand).
    ///   • Joker: No-Trump mode (highest card of led suit wins; no trump suit).
    /// </summary>
    public enum TrumpMode
    {
        Suit        = 0,
        SeventhCard = 1,
        Joker       = 2
    }
}
