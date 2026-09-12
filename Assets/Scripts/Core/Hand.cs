using System.Collections.Generic;
using System.Linq;

namespace Game29
{
    /// <summary>
    /// Represents a player's hand of cards.
    /// Provides helpers for suit-filtering and valid play determination.
    /// </summary>
    public class Hand
    {
        private List<Card> _cards = new List<Card>();

        public IReadOnlyList<Card> Cards => _cards.AsReadOnly();
        public int Count => _cards.Count;

        public void AddCard(Card card) => _cards.Add(card);

        public void AddCards(IEnumerable<Card> cards) => _cards.AddRange(cards);

        /// <summary>Removes one instance of a card. Returns true if found and removed.</summary>
        public bool RemoveCard(Card card) => _cards.Remove(card);

        public void Clear() => _cards.Clear();

        /// <summary>All cards in this hand that match the given suit.</summary>
        public List<Card> GetCardsBySuit(Suit suit)
            => _cards.Where(c => c.Suit == suit).ToList();

        public bool HasSuit(Suit suit) => _cards.Any(c => c.Suit == suit);

        /// <summary>Sum of point values of all cards currently held.</summary>
        public int TotalPoints() => _cards.Sum(c => c.PointValue);

        /// <summary>
        /// Returns the subset of cards this player is legally allowed to play.
        ///
        /// Rules:
        ///   - If leading a trick (trick is empty): any card.
        ///   - Otherwise: must follow the led suit if possible.
        ///   - If unable to follow suit: any card (may play trump or discard).
        /// </summary>
        public List<Card> GetValidPlays(Trick currentTrick)
        {
            if (currentTrick == null || currentTrick.IsEmpty)
                return new List<Card>(_cards);

            Suit ledSuit = currentTrick.LedSuit.Value;
            List<Card> suitCards = GetCardsBySuit(ledSuit);

            // Must follow suit if holding any cards of that suit.
            return suitCards.Count > 0 ? suitCards : new List<Card>(_cards);
        }
    }
}
