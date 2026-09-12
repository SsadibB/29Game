using System;

namespace Game29
{
    /// <summary>
    /// Immutable data representation of a single playing card.
    ///
    /// Point values (sum to 28 across all 32 cards):
    ///   Jack = 3 | Nine = 2 | Ace = 1 | Ten = 1 | rest = 0
    /// </summary>
    [Serializable]
    public class Card
    {
        public Suit Suit { get; private set; }
        public Rank Rank { get; private set; }

        /// <summary>Point value of this card per 29 game rules.</summary>
        public int PointValue
        {
            get
            {
                switch (Rank)
                {
                    case Rank.Jack: return 3;
                    case Rank.Nine: return 2;
                    case Rank.Ace:  return 1;
                    case Rank.Ten:  return 1;
                    default:        return 0;
                }
            }
        }

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        /// <summary>Returns e.g. "Jack of Hearts".</summary>
        public override string ToString() => $"{Rank} of {Suit}";

        public override bool Equals(object obj)
        {
            if (obj is Card other)
                return other.Suit == Suit && other.Rank == Rank;
            return false;
        }

        public override int GetHashCode() => HashCode.Combine(Suit, Rank);
    }
}
