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
    ///   • Awareness — respects what trump knowledge it legitimately has
    ///     (bidding-team members know trump from the start; defenders learn on reveal).
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
        // TRUMP SELECTION
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Selects the best trump suit from this player's hand.
        /// Used by the partner of the bid-winner.
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
                    if (card.Rank == Rank.Jack)  score += 5; // trump Jack is the top trump
                    if (card.Rank == Rank.Ace)   score += 2;
                    if (card.Rank == Rank.King)  score += 1;
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
        ///   • The real trump suit if this AI is on the bidding team, or if trump has been revealed.
        ///   • Null if this AI is a defender and trump hasn't been revealed yet.
        /// </summary>
        public Card DecideCardToPlay(
            Hand       hand,
            Trick      currentTrick,
            Suit?      visibleTrump,
            PlayerSeat myPartner)
        {
            List<Card> validPlays = hand.GetValidPlays(currentTrick);
            if (validPlays.Count == 0) return hand.Cards[0]; // safety fallback
            if (validPlays.Count == 1) return validPlays[0];

            // ── Leading a trick ──────────────────────────────────────────────
            if (currentTrick == null || currentTrick.IsEmpty)
                return ChooseLeadCard(validPlays, visibleTrump);

            // ── Following a trick ────────────────────────────────────────────
            bool partnerCurrentlyWinning = currentTrick.DetermineWinner(visibleTrump) == myPartner;
            return ChooseFollowCard(validPlays, currentTrick, visibleTrump, partnerCurrentlyWinning);
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
                if (card.Rank == Rank.Jack)  strength += 2; // Jacks are the highest trump — extra weight
                if (card.Rank == Rank.King)  strength += 1;
                if (card.Rank == Rank.Queen) strength += 1;
            }

            // Suit-length bonus — a long suit is likely to become trump.
            int maxLength = 0;
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
                maxLength = Mathf.Max(maxLength, hand.GetCardsBySuit(suit).Count);

            strength += Mathf.Max(0, maxLength - 3); // bonus for 4+ cards in one suit

            return strength;
        }

        private Card ChooseLeadCard(List<Card> valid, Suit? trump)
        {
            // Prefer leading a high-value non-trump card to cash points early.
            List<Card> nonTrump = trump.HasValue
                ? valid.Where(c => c.Suit != trump.Value).ToList()
                : new List<Card>(valid);

            if (nonTrump.Count > 0)
            {
                // Lead highest-point non-trump if we have one.
                Card best = nonTrump.OrderByDescending(c => c.PointValue)
                                    .ThenByDescending(c => (int)c.Rank)
                                    .First();
                if (best.PointValue > 0) return best;

                // Otherwise lead our safest (lowest rank) non-trump.
                return nonTrump.OrderBy(c => (int)c.Rank).First();
            }

            // Only trump cards left — lead lowest to save big trumps.
            return valid.OrderBy(c => (int)c.Rank).First();
        }

        private Card ChooseFollowCard(
            List<Card> valid,
            Trick      trick,
            Suit?      trump,
            bool       partnerWinning)
        {
            // If partner is already winning, contribute cheapest card (don't over-spend).
            if (partnerWinning)
                return valid.OrderBy(c => c.PointValue).ThenBy(c => (int)c.Rank).First();

            // Try to win the trick.
            Card currentBest = GetWinningCard(trick, trump);
            List<Card> beaters = valid
                .Where(c => CardBeats(c, currentBest, trick.LedSuit, trump))
                .ToList();

            if (beaters.Count > 0)
            {
                // Win with the cheapest winning card (preserve big cards for later).
                return beaters.OrderBy(c => (int)c.Rank).First();
            }

            // Can't win — discard lowest-value card.
            return valid.OrderBy(c => c.PointValue).ThenBy(c => (int)c.Rank).First();
        }

        private Card GetWinningCard(Trick trick, Suit? trump)
        {
            if (trick == null || trick.IsEmpty) return null;
            (PlayerSeat _, Card winning) = trick.Plays[0];
            for (int i = 1; i < trick.Plays.Count; i++)
            {
                if (CardBeats(trick.Plays[i].Card, winning, trick.LedSuit, trump))
                    winning = trick.Plays[i].Card;
            }
            return winning;
        }

        private bool CardBeats(Card challenger, Card current, Suit? ledSuit, Suit? trump)
        {
            if (current == null) return true;

            bool challengerTrump = trump.HasValue && challenger.Suit == trump.Value;
            bool currentTrump    = trump.HasValue && current.Suit    == trump.Value;

            if (challengerTrump && !currentTrump) return true;
            if (!challengerTrump && currentTrump)  return false;

            if (challenger.Suit == current.Suit)
                return (int)challenger.Rank > (int)current.Rank;

            if (ledSuit.HasValue && challenger.Suit == ledSuit.Value && current.Suit != ledSuit.Value)
                return true;

            return false;
        }
    }
}
