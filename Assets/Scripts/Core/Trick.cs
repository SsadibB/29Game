using System.Collections.Generic;
using System.Linq;

namespace Game29
{
    /// <summary>
    /// Tracks a single trick: who led, what cards were played in order,
    /// and determines the trick winner given a trump suit.
    ///
    /// Trick-winning rules:
    ///   1. Trump beats all non-trump.
    ///   2. Among same-suit cards, 29 rank wins: J &gt; 9 &gt; A &gt; 10 &gt; K &gt; Q &gt; 8 &gt; 7.
    ///   3. An off-suit, non-trump card never beats the led suit or a trump.
    /// </summary>
    public class Trick
    {
        private List<(PlayerSeat Player, Card Card)> _plays = new List<(PlayerSeat, Card)>(4);

        /// <summary>The player who leads this trick.</summary>
        public PlayerSeat Leader { get; private set; }

        public bool IsEmpty  => _plays.Count == 0;
        public bool IsComplete => _plays.Count == 4;
        public int  PlayCount  => _plays.Count;

        /// <summary>The suit of the first card played (establishes the led suit).</summary>
        public Suit? LedSuit => _plays.Count > 0 ? _plays[0].Card.Suit : (Suit?)null;

        public IReadOnlyList<(PlayerSeat Player, Card Card)> Plays => _plays.AsReadOnly();

        public Trick(PlayerSeat leader)
        {
            Leader = leader;
        }

        /// <summary>Adds a card play to this trick.</summary>
        public void AddPlay(PlayerSeat player, Card card)
        {
            _plays.Add((player, card));
        }

        /// <summary>Sum of point values of all cards in this trick.</summary>
        public int TotalPoints() => _plays.Sum(p => p.Card.PointValue);

        /// <summary>
        /// Determines which player wins this trick.
        /// Pass <c>null</c> if trump has not been revealed (treated as no trump).
        /// </summary>
        public PlayerSeat DetermineWinner(Suit? trumpSuit, TrumpMode mode = TrumpMode.Suit)
        {
            if (_plays.Count == 0) return Leader;

            var winner = _plays[0];
            for (int i = 1; i < _plays.Count; i++)
            {
                if (GameRules.Beats(_plays[i].Card, winner.Card, LedSuit, trumpSuit, mode))
                    winner = _plays[i];
            }
            return winner.Player;
        }
    }
}
