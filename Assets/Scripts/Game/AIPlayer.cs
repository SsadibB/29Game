using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game29
{
    /// <summary>
    /// AI decision-making for a single CPU player.
    /// Handles bidding strategy, trump selection, and card play.
    ///
    /// Design principles:
    ///   • Bidding — estimates personal hand strength; bids conservatively.
    ///   • Trump   — chooses the suit with the most point/control cards.
    ///   • Play    — tries to win tricks it should win; avoids wasting points.
    ///   • Awareness — only the Bid Winner knows trump until it is revealed.
    /// </summary>
    public class AIPlayer
    {
        public PlayerSeat Seat { get; private set; }

        public AIPlayer(PlayerSeat seat)
        {
            Seat = seat;
        }

        // ════════════════════════════════════════════════════════════════════════
        // BIDDING
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns a bid value to place, or null to pass.
        ///
        /// Strategy:
        ///   • Estimate expected card-points based on own hand.
        ///   • If partner has already claimed a high bid and hand is weak → pass.
        ///   • Otherwise bid the minimum needed to surpass current leader.
        /// </summary>
        public int? DecideBid(Hand hand, int currentHighBid, bool partnerIsLeading)
        {
            int strength = EstimateHandStrength(hand);

            // If our partner is already winning the bid and our hand doesn't
            // justify a raise, let them run with it.
            if (partnerIsLeading && strength < 9)
                return null;

            // Calculate the maximum bid we're confident about.
            // strength maps roughly onto expected card-points if chosen as trump suit.
            int maxConfidentBid = GameRules.MinBid + Mathf.Max(0, strength - 6);
            maxConfidentBid = Mathf.Clamp(maxConfidentBid, GameRules.MinBid, GameRules.MaxBid);

            int minRaise = currentHighBid + 1;

            if (minRaise <= maxConfidentBid)
                return minRaise;   // bid the minimum necessary

            return null;           // pass — hand too weak to justify a higher bid
        }

        // ════════════════════════════════════════════════════════════════════════
        // DOUBLE & RE-DOUBLE DECISIONS
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Decides whether this AI opponent wants to set Double against the bidding team.
        /// An AI will Double if it holds a strong hand (e.g. strength >= 7 and >= 4 card points,
        /// or holds 2+ Jacks).
        /// </summary>
        public bool DecideDouble(Hand hand, int currentBid)
        {
            int strength = EstimateHandStrength(hand);
            int jacks = hand.Cards.Count(c => c.Rank == Rank.Jack);

            // Double if hand is strong or opponent bid high
            if (jacks >= 2 || (strength >= 8 && hand.TotalPoints() >= 4) || (currentBid >= 21 && strength >= 6))
                return true;

            return false;
        }

        /// <summary>
        /// Decides whether this AI (on the bidding team) wants to respond with Re-Double
        /// after the opponent sets Double.
        /// </summary>
        public bool DecideReDouble(Hand hand, int currentBid)
        {
            int strength = EstimateHandStrength(hand);
            int jacks = hand.Cards.Count(c => c.Rank == Rank.Jack);

            // Re-Double if confident
            if ((jacks >= 2 && strength >= 9) || strength >= 11)
                return true;

            return false;
        }

        // ════════════════════════════════════════════════════════════════════════
        // TRUMP SELECTION
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Selects the best trump suit from this player's first 4 cards.
        /// Used by the Bid Winner.
        /// </summary>
        public Suit SelectTrump(Hand hand)
        {
            Suit bestSuit  = Suit.Hearts;
            int  bestScore = -1;

            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
            {
                int score = 0;
                foreach (Card card in hand.GetCardsBySuit(suit))
                {
                    score += card.PointValue * 3;
                    score += GameRules.GetTrickRank(card.Rank); // J > 9 > A > 10 > K > Q > 8 > 7
                    score += 1; // length bonus per card
                }
                if (score > bestScore) { bestScore = score; bestSuit = suit; }
            }
            return bestSuit;
        }

        // ════════════════════════════════════════════════════════════════════════
        // CARD PLAY
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Picks the best legal card to play given the current trick state.
        ///
        /// <paramref name="visibleTrump"/> should be:
        ///   • The real trump suit if this AI won the bid, or if trump has been revealed.
        ///   • Null otherwise (including the bid winner's partner).
        /// </summary>
        /// <summary>
        /// Chooses Suit, 7th Card, or Joker from the first 4 cards.
        /// </summary>
        public TrumpMode DecideTrumpMode(Hand hand, out Suit suit)
        {
            suit = SelectTrump(hand);
            int jacks = 0;
            foreach (Card card in hand.Cards)
            {
                if (card.Rank == Rank.Jack) jacks++;
            }

            int bestLen = hand.GetCardsBySuit(suit).Count;
            if (jacks >= 2 && bestLen <= 1)
                return TrumpMode.Joker;
            if (bestLen <= 1)
                return TrumpMode.SeventhCard;
            return TrumpMode.Suit;
        }

        /// <summary>
        /// Reveal hidden trump only when void in the led suit and a trump play is useful.
        /// </summary>
        public bool ShouldRevealTrump(Hand hand, Trick currentTrick, Suit? visibleTrump)
        {
            if (currentTrick == null || currentTrick.IsEmpty || !currentTrick.LedSuit.HasValue)
                return false;
            if (hand.HasSuit(currentTrick.LedSuit.Value))
                return false;

            if (visibleTrump.HasValue)
                return hand.HasSuit(visibleTrump.Value);

            foreach (Card card in hand.Cards)
            {
                if (card.Rank == Rank.Jack || card.Rank == Rank.Nine)
                    return true;
            }
            return false;
        }

        public Card DecideCardToPlay(
            Hand       hand,
            Trick      currentTrick,
            Suit?      visibleTrump,
            PlayerSeat myPartner,
            TrumpMode  mode = TrumpMode.Suit)
        {
            List<Card> validPlays = hand.GetValidPlays(currentTrick);
            if (validPlays.Count == 0) return hand.Cards[0];
            if (validPlays.Count == 1) return validPlays[0];

            if (currentTrick == null || currentTrick.IsEmpty)
                return ChooseLeadCard(validPlays, visibleTrump, mode);

            bool partnerCurrentlyWinning = currentTrick.DetermineWinner(visibleTrump, mode) == myPartner;
            return ChooseFollowCard(validPlays, currentTrick, visibleTrump, partnerCurrentlyWinning, mode);
        }

        // ════════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Hand strength estimate used for bidding.
        /// Returns a value in roughly [0, 20] — higher = stronger.
        /// </summary>
        private int EstimateHandStrength(Hand hand)
        {
            int strength = 0;

            foreach (Card card in hand.Cards)
            {
                strength += card.PointValue;             // raw point cards
                strength += GameRules.GetTrickRank(card.Rank) >= 7 ? 2 : 0; // Jack / Nine extra weight
            }

            // Suit-length bonus — a long suit is likely to become trump.
            int maxLength = 0;
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
                maxLength = Mathf.Max(maxLength, hand.GetCardsBySuit(suit).Count);

            strength += Mathf.Max(0, maxLength - 3); // bonus for 4+ cards in one suit

            return strength;
        }

        private Card ChooseLeadCard(List<Card> valid, Suit? trump, TrumpMode mode)
        {
            if (mode == TrumpMode.Joker)
            {
                List<Card> nonJoker = valid.Where(c => !GameRules.IsJokerCard(c)).ToList();
                if (nonJoker.Count > 0)
                {
                    Card best = nonJoker.OrderByDescending(c => c.PointValue)
                                        .ThenByDescending(c => GameRules.GetTrickRank(c.Rank))
                                        .First();
                    if (best.PointValue > 0) return best;
                    return nonJoker.OrderBy(c => GameRules.GetTrickRank(c.Rank)).First();
                }
                return valid.OrderBy(c => GameRules.GetJokerRank(c.Suit)).First();
            }

            List<Card> nonTrump = trump.HasValue
                ? valid.Where(c => c.Suit != trump.Value).ToList()
                : new List<Card>(valid);

            if (nonTrump.Count > 0)
            {
                Card best = nonTrump.OrderByDescending(c => c.PointValue)
                                    .ThenByDescending(c => GameRules.GetTrickRank(c.Rank))
                                    .First();
                if (best.PointValue > 0) return best;
                return nonTrump.OrderBy(c => GameRules.GetTrickRank(c.Rank)).First();
            }

            return valid.OrderBy(c => GameRules.GetTrickRank(c.Rank)).First();
        }

        private Card ChooseFollowCard(
            List<Card> valid,
            Trick      trick,
            Suit?      trump,
            bool       partnerWinning,
            TrumpMode  mode)
        {
            if (partnerWinning)
                return valid.OrderBy(c => c.PointValue).ThenBy(c => GameRules.GetTrickRank(c.Rank)).First();

            Card currentBest = GetWinningCard(trick, trump, mode);
            List<Card> beaters = valid
                .Where(c => CardBeats(c, currentBest, trick.LedSuit, trump, mode))
                .ToList();

            if (beaters.Count > 0)
                return beaters.OrderBy(c => c.PointValue).ThenBy(c => GameRules.GetTrickRank(c.Rank)).First();

            return valid.OrderBy(c => c.PointValue).ThenBy(c => GameRules.GetTrickRank(c.Rank)).First();
        }

        private Card GetWinningCard(Trick trick, Suit? trump, TrumpMode mode)
        {
            if (trick == null || trick.IsEmpty) return null;
            Card winning = trick.Plays[0].Card;
            for (int i = 1; i < trick.Plays.Count; i++)
            {
                if (CardBeats(trick.Plays[i].Card, winning, trick.LedSuit, trump, mode))
                    winning = trick.Plays[i].Card;
            }
            return winning;
        }

        private bool CardBeats(Card challenger, Card current, Suit? ledSuit, Suit? trump, TrumpMode mode)
        {
            if (current == null) return true;
            return GameRules.Beats(challenger, current, ledSuit, trump, mode);
        }
    }
}
