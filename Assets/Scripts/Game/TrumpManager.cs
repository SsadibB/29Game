using System;

namespace Game29
{
    /// <summary>
    /// Manages trump suit selection and the "hidden trump" reveal mechanic.
    ///
    /// Trump is chosen by the bidding winner's partner (AI always picks best suit).
    /// The chosen suit is SECRET until a player physically plays a trump card,
    /// at which point it is revealed to all.
    ///
    /// Knowledge access:
    ///   • Bidding team (bidder + partner) always knows the trump suit.
    ///   • Defending team knows trump only after TrumpRevealed == true.
    /// </summary>
    public class TrumpManager
    {
        // ── State ───────────────────────────────────────────────────────────────
        /// <summary>The actual trump suit — always set after SelectTrump().</summary>
        public Suit? TrumpSuit     { get; private set; }

        /// <summary>True once trump has been played/revealed to all players.</summary>
        public bool  TrumpRevealed { get; private set; }

        /// <summary>The player who won the bidding.</summary>
        public PlayerSeat Bidder  { get; private set; }

        /// <summary>The bidder's partner (who selects trump).</summary>
        public PlayerSeat Partner { get; private set; }

        /// <summary>The active trump mode: Standard Suit, Seventh Card, or Joker (No Trump).</summary>
        public TrumpMode Mode { get; private set; } = TrumpMode.Suit;

        /// <summary>The physical 7th card dealt to bidder when SeventhCard mode is active.</summary>
        public Card SeventhCard { get; private set; }

        public bool IsSeventhCard => Mode == TrumpMode.SeventhCard;
        public bool IsJoker       => Mode == TrumpMode.Joker;

        // ── Events ──────────────────────────────────────────────────────────────
        /// <summary>Fired when the partner has privately chosen a trump suit (suit hidden from opponents).</summary>
        public event Action OnTrumpChosen;

        /// <summary>Fired the moment trump is first revealed publicly.</summary>
        public event Action<Suit> OnTrumpRevealed;

        // ── API ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called after bidding resolves.
        /// The bidder's partner selects the best trump from their hand.
        /// </summary>
        public void SelectTrump(PlayerSeat bidder, Hand partnerHand)
        {
            Bidder        = bidder;
            Partner       = GameRules.GetPartner(bidder);
            Mode          = TrumpMode.Suit;
            TrumpRevealed = false;
            TrumpSuit     = ChooseBestTrump(partnerHand);
            OnTrumpChosen?.Invoke();
        }

        /// <summary>Sets the trump suit chosen explicitly (e.g. by the human bidder).</summary>
        public void SetTrumpSuit(PlayerSeat bidder, Suit suit)
        {
            Bidder        = bidder;
            Partner       = GameRules.GetPartner(bidder);
            Mode          = TrumpMode.Suit;
            TrumpRevealed = false;
            TrumpSuit     = suit;
            OnTrumpChosen?.Invoke();
        }

        /// <summary>Activates blind 7th Card trump mode. Suit is resolved when 2nd batch is dealt.</summary>
        public void SetSeventhCardTrump(PlayerSeat bidder)
        {
            Bidder        = bidder;
            Partner       = GameRules.GetPartner(bidder);
            Mode          = TrumpMode.SeventhCard;
            TrumpRevealed = false;
            TrumpSuit     = null;
            SeventhCard   = null;
            OnTrumpChosen?.Invoke();
        }

        /// <summary>Activates Joker / No-Trump mode. Highest card of led suit wins; no trump suit.</summary>
        public void SetJokerTrump(PlayerSeat bidder)
        {
            Bidder        = bidder;
            Partner       = GameRules.GetPartner(bidder);
            Mode          = TrumpMode.Joker;
            TrumpRevealed = true; // No-Trump is publicly known from the start
            TrumpSuit     = null;
            OnTrumpChosen?.Invoke();
        }

        /// <summary>Resolves the blind 7th card once hands are fully dealt.</summary>
        public void ResolveSeventhCard(Card card7)
        {
            SeventhCard = card7;
            TrumpSuit   = card7.Suit;
        }

        /// <summary>
        /// Returns the trump suit visible to the given player.
        /// Bidding-team members see standard suit immediately; opponents see null until revealed.
        /// 7th Card is secret to everyone until revealed.
        /// Joker mode has no trump suit (returns null).
        /// </summary>
        public Suit? GetVisibleTrump(PlayerSeat asker)
        {
            if (Mode == TrumpMode.Joker) return null;
            if (TrumpRevealed) return TrumpSuit;
            if (Mode == TrumpMode.SeventhCard) return null; // blind even to bidder until revealed
            return (GameRules.GetTeam(asker) == GameRules.GetTeam(Bidder)) ? TrumpSuit : null;
        }

        /// <summary>
        /// Call this when a card is about to be played.
        /// If the card is the trump suit and trump hasn't been revealed yet, reveals it now.
        /// </summary>
        public void NotifyCardPlayed(Card playedCard)
        {
            if (Mode == TrumpMode.Joker) return;
            if (!TrumpRevealed && TrumpSuit.HasValue && playedCard.Suit == TrumpSuit.Value)
                RevealTrump();
        }

        /// <summary>Allows a player to explicitly ask to reveal trump when following suit is impossible.</summary>
        public void RevealTrumpExplicitly()
        {
            if (!TrumpRevealed && (TrumpSuit.HasValue || Mode == TrumpMode.SeventhCard))
                RevealTrump();
        }

        public void Reset()
        {
            TrumpSuit     = null;
            TrumpRevealed = false;
            Mode          = TrumpMode.Suit;
            SeventhCard   = null;
        }

        // ── Private ─────────────────────────────────────────────────────────────

        private void RevealTrump()
        {
            TrumpRevealed = true;
            OnTrumpRevealed?.Invoke(TrumpSuit.Value);
        }

        /// <summary>
        /// AI trump-selection heuristic: score each suit by its point-bearing and
        /// controlling cards, pick the best.
        /// </summary>
        private Suit ChooseBestTrump(Hand hand)
        {
            Suit bestSuit  = Suit.Hearts;
            int  bestScore = -1;

            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                int score = 0;
                foreach (Card card in hand.GetCardsBySuit(suit))
                {
                    score += card.PointValue * 3; // weight point cards heavily
                    if (card.Rank == Rank.Jack)  score += 5; // trump Jack is the highest trump
                    if (card.Rank == Rank.Ace)   score += 2;
                    if (card.Rank == Rank.King)  score += 1;
                    score += 1; // length bonus
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
