using System;

namespace Game29
{
    /// <summary>
    /// Manages trump selection by the Bid Winner.
    ///
    /// Suit / 7th Card: trump stays face-down until a player who cannot follow
    /// the led suit reveals it. Only then may trump cards be used as trump.
    /// Joker: public immediately — Jacks are super-trumps.
    /// </summary>
    public class TrumpManager
    {
        public Suit? TrumpSuit     { get; private set; }
        public bool  TrumpRevealed { get; private set; }
        public PlayerSeat Bidder  { get; private set; }
        public PlayerSeat Partner { get; private set; }
        public TrumpMode Mode { get; private set; } = TrumpMode.Suit;
        public Card SeventhCard { get; private set; }

        public bool IsSeventhCard => Mode == TrumpMode.SeventhCard;
        public bool IsJoker       => Mode == TrumpMode.Joker;

        private bool _seventhCardReturnedToHand;

        public event Action OnTrumpChosen;
        public event Action<Suit> OnTrumpRevealed;

        public void SelectTrump(PlayerSeat bidder, Hand bidderHand)
        {
            ApplySuitTrump(bidder, ChooseBestTrump(bidderHand));
        }

        public void SetTrumpSuit(PlayerSeat bidder, Suit suit)
        {
            ApplySuitTrump(bidder, suit);
        }

        /// <summary>Blind 7th-card trump. Suit is the bidder's 7th dealt card, kept face-down.</summary>
        public void SetSeventhCardTrump(PlayerSeat bidder)
        {
            Bidder        = bidder;
            Partner       = GameRules.GetPartner(bidder);
            Mode          = TrumpMode.SeventhCard;
            TrumpRevealed = false;
            TrumpSuit     = null;
            SeventhCard   = null;
            _seventhCardReturnedToHand = false;
            OnTrumpChosen?.Invoke();
        }

        /// <summary>Joker trump: Jacks are super-trumps and the mode is public.</summary>
        public void SetJokerTrump(PlayerSeat bidder)
        {
            Bidder        = bidder;
            Partner       = GameRules.GetPartner(bidder);
            Mode          = TrumpMode.Joker;
            TrumpRevealed = true;
            TrumpSuit     = null;
            OnTrumpChosen?.Invoke();
        }

        /// <summary>Stores the bidder's 7th card as the hidden trump indicator (already removed from hand).</summary>
        public void ResolveSeventhCard(Card card7)
        {
            SeventhCard = card7;
            TrumpSuit   = card7.Suit;
            _seventhCardReturnedToHand = false;
        }

        /// <summary>
        /// Trump visible to this player. Until reveal: only a suit-mode bid winner
        /// knows their chosen suit. 7th-card suit stays hidden from everyone.
        /// </summary>
        public Suit? GetVisibleTrump(PlayerSeat asker)
        {
            if (Mode == TrumpMode.Joker) return null;
            if (TrumpRevealed) return TrumpSuit;
            if (Mode == TrumpMode.SeventhCard) return null;
            return asker == Bidder ? TrumpSuit : null;
        }

        /// <summary>Playing a card never auto-reveals trump. Reveal is a separate action.</summary>
        public void NotifyCardPlayed(Card playedCard) { }

        public bool CanReveal(PlayerSeat player, Trick currentTrick, Hand playerHand)
        {
            if (TrumpRevealed) return false;
            if (Mode == TrumpMode.Joker) return false;
            if (!TrumpSuit.HasValue && !IsSeventhCard) return false;
            if (currentTrick == null || currentTrick.IsEmpty || !currentTrick.LedSuit.HasValue) return false;
            if (playerHand == null) return false;
            return !playerHand.HasSuit(currentTrick.LedSuit.Value);
        }

        public bool RevealTrumpExplicitly()
        {
            if (TrumpRevealed) return false;
            if (Mode == TrumpMode.Joker) return false;
            if (!TrumpSuit.HasValue && Mode != TrumpMode.SeventhCard) return false;
            if (Mode == TrumpMode.SeventhCard && !TrumpSuit.HasValue) return false;
            RevealTrump();
            return true;
        }

        /// <summary>After 7th-card trump is revealed, return that card to the bidder's hand once.</summary>
        public bool TryReturnSeventhCardToHand(out Card card)
        {
            card = SeventhCard;
            if (!IsSeventhCard || SeventhCard == null || _seventhCardReturnedToHand)
                return false;
            _seventhCardReturnedToHand = true;
            return true;
        }

        public void Reset()
        {
            TrumpSuit     = null;
            TrumpRevealed = false;
            Mode          = TrumpMode.Suit;
            SeventhCard   = null;
            _seventhCardReturnedToHand = false;
        }

        private void ApplySuitTrump(PlayerSeat bidder, Suit suit)
        {
            Bidder        = bidder;
            Partner       = GameRules.GetPartner(bidder);
            Mode          = TrumpMode.Suit;
            TrumpSuit     = suit;
            TrumpRevealed = false;
            OnTrumpChosen?.Invoke();
        }

        private void RevealTrump()
        {
            TrumpRevealed = true;
            if (TrumpSuit.HasValue)
                OnTrumpRevealed?.Invoke(TrumpSuit.Value);
        }

        private Suit ChooseBestTrump(Hand hand)
        {
            Suit bestSuit  = Suit.Hearts;
            int  bestScore = -1;

            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                int score = 0;
                foreach (Card card in hand.GetCardsBySuit(suit))
                {
                    score += card.PointValue * 3;
                    score += GameRules.GetTrickRank(card.Rank);
                    score += 1;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSuit  = suit;
                }
            }

            return bestSuit;
        }
    }
}
