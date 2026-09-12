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
    ///   2. Among same-suit cards, higher Rank wins.
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
        public PlayerSeat DetermineWinner(Suit? trumpSuit)
        {
            if (_plays.Count == 0) return Leader;

            var winner = _plays[0];
            for (int i = 1; i < _plays.Count; i++)
            {
                if (Beats(_plays[i].Card, winner.Card, trumpSuit))
                    winner = _plays[i];
            }
            return winner.Player;
        }

        // Returns true if challenger beats the current leader card.
        private bool Beats(Card challenger, Card current, Suit? trumpSuit)
        {
            bool challengerIsTrump = trumpSuit.HasValue && challenger.Suit == trumpSuit.Value;
            bool currentIsTrump    = trumpSuit.HasValue && current.Suit    == trumpSuit.Value;

            // Trump always beats non-trump.
            if (challengerIsTrump && !currentIsTrump) return true;
            if (!challengerIsTrump && currentIsTrump)  return false;

            // Both trump or both non-trump: higher rank in the SAME suit wins.
            if (challenger.Suit == current.Suit)
                return (int)challenger.Rank > (int)current.Rank;

            // Different suits, neither trump — only the led suit can take the trick.
            if (LedSuit.HasValue && challenger.Suit == LedSuit.Value && current.Suit != LedSuit.Value)
                return true;

            return false;
        }
    }
}
