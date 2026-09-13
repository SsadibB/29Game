namespace Game29
{
    /// <summary>
    /// All distinct phases in a round of 29.
    /// GameManager transitions through these sequentially each round.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>Idle before the first game starts.</summary>
        WaitingToStart,

        /// <summary>Cards are being shuffled and distributed.</summary>
        Dealing,

        /// <summary>Players are placing bids (15–28).</summary>
        Bidding,

        /// <summary>Bid winner selects the trump suit.</summary>
        TrumpSelection,

        /// <summary>Trick-taking play phase.</summary>
        Playing,

        /// <summary>Round has ended — scores tallied, awaiting next round.</summary>
        RoundOver,

        /// <summary>A team has reached 6 game points — game is over.</summary>
        GameOver
    }
}
