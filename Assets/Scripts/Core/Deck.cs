using System;
using System.Collections.Generic;

namespace Game29
{
    /// <summary>
    /// Creates and manages a 32-card deck (7–Ace of all four suits).
    /// Provides Fisher-Yates shuffle and card dealing.
    /// </summary>
    public class Deck
    {
        private List<Card> _cards = new List<Card>();
        private Random _rng;

        public int Count => _cards.Count;

        public Deck(int seed = -1)
        {
            _rng = seed >= 0 ? new Random(seed) : new Random();
        }

        /// <summary>Resets the deck to all 32 cards in order, then shuffles.</summary>
        public void InitializeAndShuffle()
        {
            _cards.Clear();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    _cards.Add(new Card(suit, rank));
                }
            }
            Shuffle();
        }

        /// <summary>Fisher-Yates in-place shuffle.</summary>
        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                Card tmp = _cards[i];
                _cards[i] = _cards[j];
                _cards[j] = tmp;
            }
        }

        /// <summary>Deals the top card from the deck. Returns null if empty.</summary>
        public Card DealOne()
        {
            if (_cards.Count == 0) return null;
            int last = _cards.Count - 1;
            Card card = _cards[last];
            _cards.RemoveAt(last);
            return card;
        }

        /// <summary>Deals a batch of cards from the deck.</summary>
        public List<Card> DealBatch(int count)
        {
            List<Card> batch = new List<Card>(count);
            for (int i = 0; i < count && _cards.Count > 0; i++)
                batch.Add(DealOne());
            return batch;
        }
    }
}
