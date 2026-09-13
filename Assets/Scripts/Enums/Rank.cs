namespace Game29
{
    /// <summary>
    /// Card ranks used in 29. The deck runs from Seven to Ace (32 cards total: 7-A of 4 suits).
    /// Enum integers are face values only. Trick-taking order is J &gt; 9 &gt; A &gt; 10 &gt; K &gt; Q &gt; 8 &gt; 7
    /// via <see cref="GameRules.GetTrickRank"/>.
    /// </summary>
    public enum Rank
    {
        Seven = 7,
        Eight = 8,
        Nine  = 9,
        Ten   = 10,
        Jack  = 11,
        Queen = 12,
        King  = 13,
        Ace   = 14
    }
}
