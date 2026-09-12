using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game29
{
    /// <summary>
    /// Central game controller and state machine for 29.
    ///
    /// Responsibilities:
    ///   • Orchestrates all sub-managers (Deck, Bidding, Trump, Tricks, Score).
    ///   • Drives AI decisions synchronously (UI will add timing/delays later).
    ///   • Exposes a clean API for the UI layer:
    ///       PlaceHumanBid()  / HumanPass()    → during Bidding phase
    ///       PlayHumanCard()                   → during Playing phase
    ///       StartNewGame()   / StartNextRound()
    ///   • Fires high-level events the UI subscribes to.
    ///
    /// Human player is ALWAYS <see cref="PlayerSeat.South"/>.
    /// Teams: South+North (team 0) vs East+West (team 1).
    /// First team to 6 game-points wins.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ════════════════════════════════════════════════════════════════════════
        // SINGLETON
        // ════════════════════════════════════════════════════════════════════════

        public static GameManager Instance { get; private set; }

        // ════════════════════════════════════════════════════════════════════════
        // SUB-MANAGERS
        // ════════════════════════════════════════════════════════════════════════

        private readonly Deck           _deck           = new Deck();
        private readonly BiddingManager _biddingMgr     = new BiddingManager();
        private readonly TrumpManager   _trumpMgr       = new TrumpManager();
        private readonly ScoreManager   _scoreMgr       = new ScoreManager();
        private TrickManager            _trickMgr;          // created after trump is ready

        // Public read access for UI
        public BiddingManager BiddingManager => _biddingMgr;
        public TrumpManager   TrumpManager   => _trumpMgr;
        public ScoreManager   ScoreManager   => _scoreMgr;
        public TrickManager   TrickManager   => _trickMgr;

        // ════════════════════════════════════════════════════════════════════════
        // PLAYER DATA
        // ════════════════════════════════════════════════════════════════════════

        private readonly Hand[]     _hands     = new Hand[4];
        private readonly AIPlayer[] _aiPlayers = new AIPlayer[3]; // West, North, East

        public const PlayerSeat HumanSeat = PlayerSeat.South;

        /// <summary>The human player's current hand.</summary>
        public Hand HumanHand => _hands[(int)HumanSeat];

        // ════════════════════════════════════════════════════════════════════════
        // GAME STATE
        // ════════════════════════════════════════════════════════════════════════

        public GamePhase   CurrentPhase   { get; private set; } = GamePhase.WaitingToStart;
        public PlayerSeat  Dealer         { get; private set; } = PlayerSeat.West;
        public PlayerSeat  CurrentPlayer  { get; private set; }

        // ════════════════════════════════════════════════════════════════════════
        // PACING & COROUTINES
        // ════════════════════════════════════════════════════════════════════════

        [Header("Pacing & Delays")]
        [SerializeField] private bool  enablePacing     = true;
        [SerializeField] private float aiBidDelay       = 0.6f;
        [SerializeField] private float aiPlayDelay      = 0.6f;
        [SerializeField] private float trickClearDelay  = 1.2f;

        public bool  EnablePacing    { get => enablePacing; set => enablePacing = value; }
        public float AIBidDelay      => aiBidDelay;
        public float AIPlayDelay     => aiPlayDelay;
        public float TrickClearDelay => trickClearDelay;

        private Coroutine _aiBiddingRoutine;
        private Coroutine _aiPlayRoutine;

        // ════════════════════════════════════════════════════════════════════════
        // EVENTS  (UI subscribes to these)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Phase has changed.</summary>
        public event Action<GamePhase> OnPhaseChanged;

        /// <summary>Human's hand has been dealt / updated.</summary>
        public event Action<Hand> OnHumanHandDealt;

        /// <summary>The active player has changed.</summary>
        public event Action<PlayerSeat> OnCurrentPlayerChanged;

        /// <summary>A bidding action was taken: bid value, or null if passed.</summary>
        public event Action<PlayerSeat, int?> OnBiddingAction;

        /// <summary>A card was played to the current trick.</summary>
        public event Action<PlayerSeat, Card> OnCardPlayed;

        /// <summary>A trick was won.</summary>
        public event Action<PlayerSeat, int> OnTrickWon;

        /// <summary>A round has been scored — inspect ScoreManager for results.</summary>
        public event Action<bool> OnRoundScored;    // bool = did bidding team win?

        /// <summary>The game is over — inspect ScoreManager.GetWinningTeam().</summary>
        public event Action<int> OnGameOver;        // int = winning team index

        /// <summary>Trump has been publicly revealed.</summary>
        public event Action<Suit> OnTrumpRevealed;

        /// <summary>Fired when the human player won the bid and must choose the trump suit.</summary>
        public event Action<Hand> OnHumanTrumpSelectionRequired;

        /// <summary>General "something changed — refresh your display" event.</summary>
        public event Action OnStateChanged;

        // ════════════════════════════════════════════════════════════════════════
        // UNITY LIFECYCLE
        // ════════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            Application.runInBackground = true;
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            Initialise();
        }

        // ════════════════════════════════════════════════════════════════════════
        // INITIALISATION
        // ════════════════════════════════════════════════════════════════════════

        private void Initialise()
        {
            for (int i = 0; i < 4; i++)
                _hands[i] = new Hand();

            // AI seats: West=1, North=2, East=3
            _aiPlayers[0] = new AIPlayer(PlayerSeat.West);
            _aiPlayers[1] = new AIPlayer(PlayerSeat.North);
            _aiPlayers[2] = new AIPlayer(PlayerSeat.East);

            _trickMgr = new TrickManager(_trumpMgr);

            // Wire sub-manager events.
            _biddingMgr.OnBiddingComplete += HandleBiddingComplete;
            _trickMgr.OnCardPlayed        += HandleCardPlayed;
            _trickMgr.OnTrickWon          += HandleTrickWon;
            _trickMgr.OnRoundComplete     += HandleRoundComplete;
            _trumpMgr.OnTrumpRevealed     += t => { OnTrumpRevealed?.Invoke(t); NotifyStateChanged(); };
            _scoreMgr.OnRoundScored       += HandleRoundScored;
            _scoreMgr.OnGameOver          += HandleGameOver;
        }

        // ════════════════════════════════════════════════════════════════════════
        // PUBLIC GAME FLOW API
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Starts a completely new game (resets scores).</summary>
        public void StartNewGame()
        {
            _scoreMgr.ResetGame();
            Dealer = PlayerSeat.West; // first dealer; StartNewRound advances it
            StartNewRound();
        }

        /// <summary>Starts the next round after RoundOver. Does nothing if the game is over.</summary>
        public void StartNextRound()
        {
            if (CurrentPhase == GamePhase.RoundOver && !_scoreMgr.IsGameOver())
                StartNewRound();
        }

        // ── BIDDING ACTIONS ──────────────────────────────────────────────────

        /// <summary>Human places a bid. Only valid during Bidding phase when it is South's turn.</summary>
        public bool PlaceHumanBid(int bid)
        {
            if (CurrentPhase != GamePhase.Bidding || CurrentPlayer != HumanSeat) return false;

            bool ok = _biddingMgr.PlaceBid(HumanSeat, bid);
            if (ok)
            {
                OnBiddingAction?.Invoke(HumanSeat, bid);
                AfterHumanBiddingAction();
            }
            return ok;
        }

        /// <summary>Human passes in bidding. Only valid during Bidding phase when it is South's turn.</summary>
        public bool HumanPass()
        {
            if (CurrentPhase != GamePhase.Bidding || CurrentPlayer != HumanSeat) return false;

            bool ok = _biddingMgr.Pass(HumanSeat);
            if (ok)
            {
                OnBiddingAction?.Invoke(HumanSeat, null);
                AfterHumanBiddingAction();
            }
            return ok;
        }

        // ── PLAY ACTIONS ────────────────────────────────────────────────────

        /// <summary>Human plays a card from their hand. Only valid during Playing phase on South's turn.</summary>
        public bool PlayHumanCard(Card card)
        {
            if (CurrentPhase != GamePhase.Playing || CurrentPlayer != HumanSeat) return false;

            bool wasCompletingCard = _trickMgr.CurrentTrick != null && _trickMgr.CurrentTrick.PlayCount == 3;
            bool ok = _trickMgr.PlayCard(HumanSeat, card, _hands[(int)HumanSeat]);
            if (ok)
            {
                NotifyStateChanged();
                if (!_trickMgr.RoundComplete)
                {
                    if (wasCompletingCard && enablePacing)
                        StartCoroutine(DelayedAdvancePlayTurn(trickClearDelay));
                    else
                        AdvancePlayTurn();
                }
            }
            return ok;
        }

        private IEnumerator DelayedAdvancePlayTurn(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!_trickMgr.RoundComplete)
                AdvancePlayTurn();
        }

        /// <summary>Allows the human player to request revealing trump when follow suit is not possible.</summary>
        public void RevealTrump()
        {
            _trumpMgr.RevealTrumpExplicitly();
            NotifyStateChanged();
        }

        // ════════════════════════════════════════════════════════════════════════
        // PUBLIC QUERY API  (for UI read-only access)
        // ════════════════════════════════════════════════════════════════════════

        public Hand       GetHand(PlayerSeat seat)       => _hands[(int)seat];
        public int[]      GetGamePoints()                 => _scoreMgr.GamePoints;
        public int[]      GetRoundPoints()                => _trickMgr?.GetTeamPoints() ?? new int[2];
        public Trick      GetCurrentTrick()               => _trickMgr?.CurrentTrick;
        public Trick      LastCompletedTrick              => _trickMgr?.LastCompletedTrick;
        public int        GetCurrentBid()                 => _biddingMgr.CurrentHighBid;
        public PlayerSeat GetCurrentHighBidder()          => _biddingMgr.CurrentHighBidder;
        public int        GetFinalBid()                   => _scoreMgr.CurrentBid;
        public PlayerSeat GetBidWinner()                  => _scoreMgr.BidWinner;
        public Suit?      GetTrumpForHuman()              => _trumpMgr.GetVisibleTrump(HumanSeat);
        public bool       IsTrumpRevealed()               => _trumpMgr.TrumpRevealed;
        public int        GetMinimumBid()                 => _biddingMgr.MinimumRaiseBid();

        /// <summary>Returns the legal cards South can play right now (empty if not their turn).</summary>
        public List<Card> GetHumanValidPlays()
        {
            if (CurrentPhase != GamePhase.Playing || CurrentPlayer != HumanSeat)
                return new List<Card>();
            return _hands[(int)HumanSeat].GetValidPlays(_trickMgr.CurrentTrick);
        }

        // ════════════════════════════════════════════════════════════════════════
        // PRIVATE — ROUND SETUP
        // ════════════════════════════════════════════════════════════════════════

        private void StartNewRound()
        {
            StopAllCoroutines();
            _aiBiddingRoutine = null;
            _aiPlayRoutine = null;

            // Clear hands.
            for (int i = 0; i < 4; i++) _hands[i].Clear();
            _trumpMgr.Reset();

            // Advance dealer clockwise.
            Dealer = GameRules.NextPlayer(Dealer);

            // Deal first batch of 4 cards starting from the player after the dealer.
            ChangePhase(GamePhase.Dealing);
            DealFirstBatch();

            // Start bidding — player after dealer bids first.
            PlayerSeat firstBidder = GameRules.NextPlayer(Dealer);
            _biddingMgr.StartBidding(firstBidder);
            ChangePhase(GamePhase.Bidding);
            SetCurrentPlayer(firstBidder);

            // Let AI act if it's not the human's turn.
            if (CurrentPlayer != HumanSeat)
                RunAIBidding();

            NotifyStateChanged();
        }

        private void DealFirstBatch()
        {
            _deck.InitializeAndShuffle();

            PlayerSeat seat = GameRules.NextPlayer(Dealer);
            for (int p = 0; p < 4; p++)
            {
                List<Card> batch4 = _deck.DealBatch(4);
                _hands[(int)seat].AddCards(batch4);
                seat = GameRules.NextPlayer(seat);
            }

            Debug.Log($"[29] 🃏 First 4 cards dealt to each player. Your hand (4 cards): {string.Join(", ", HumanHand.Cards)}");
            OnHumanHandDealt?.Invoke(HumanHand);
        }

        private void DealSecondBatch()
        {
            PlayerSeat seat = GameRules.NextPlayer(Dealer);
            for (int p = 0; p < 4; p++)
            {
                List<Card> batch4 = _deck.DealBatch(4);
                _hands[(int)seat].AddCards(batch4);
                seat = GameRules.NextPlayer(seat);
            }

            if (_trumpMgr.IsSeventhCard)
            {
                Hand bidderHand = _hands[(int)_trumpMgr.Bidder];
                if (bidderHand.Count >= 7)
                    _trumpMgr.ResolveSeventhCard(bidderHand.Cards[6]);
            }

            Debug.Log($"[29] 🃏 Second 4 cards dealt. Full hand (8 cards): {string.Join(", ", HumanHand.Cards)}");
            OnHumanHandDealt?.Invoke(HumanHand);
        }

        private void DealCards()
        {
            DealFirstBatch();
            DealSecondBatch();
        }

        // ════════════════════════════════════════════════════════════════════════
        // PRIVATE — BIDDING
        // ════════════════════════════════════════════════════════════════════════

        private void AfterHumanBiddingAction()
        {
            if (_biddingMgr.BiddingComplete) return;   // HandleBiddingComplete will fire

            SetCurrentPlayer(_biddingMgr.CurrentBidder);
            if (CurrentPlayer != HumanSeat)
                RunAIBidding();

            NotifyStateChanged();
        }

        /// <summary>Runs all consecutive AI bids/passes until it's the human's turn or bidding ends.</summary>
        private void RunAIBidding()
        {
            if (enablePacing && gameObject.activeInHierarchy)
            {
                if (_aiBiddingRoutine != null) StopCoroutine(_aiBiddingRoutine);
                _aiBiddingRoutine = StartCoroutine(AIBiddingRoutine());
            }
            else
            {
                while (!_biddingMgr.BiddingComplete && CurrentPlayer != HumanSeat)
                {
                    AIPlayer ai = GetAI(CurrentPlayer);
                    if (ai == null) break;

                    bool partnerLeading =
                        GameRules.GetTeam(_biddingMgr.CurrentHighBidder) == GameRules.GetTeam(CurrentPlayer)
                        && _biddingMgr.CurrentHighBid >= GameRules.MinBid;

                    int? bid = ai.DecideBid(_hands[(int)CurrentPlayer], _biddingMgr.CurrentHighBid, partnerLeading);

                    if (bid.HasValue)
                        _biddingMgr.PlaceBid(CurrentPlayer, bid.Value);
                    else
                        _biddingMgr.Pass(CurrentPlayer);

                    OnBiddingAction?.Invoke(CurrentPlayer, bid);

                    if (!_biddingMgr.BiddingComplete)
                        SetCurrentPlayer(_biddingMgr.CurrentBidder);
                }
            }
        }

        private IEnumerator AIBiddingRoutine()
        {
            while (!_biddingMgr.BiddingComplete && CurrentPlayer != HumanSeat)
            {
                yield return new WaitForSeconds(aiBidDelay);

                if (_biddingMgr.BiddingComplete || CurrentPlayer == HumanSeat)
                    yield break;

                AIPlayer ai = GetAI(CurrentPlayer);
                if (ai == null) yield break;

                bool partnerLeading =
                    GameRules.GetTeam(_biddingMgr.CurrentHighBidder) == GameRules.GetTeam(CurrentPlayer)
                    && _biddingMgr.CurrentHighBid >= GameRules.MinBid;

                int? bid = ai.DecideBid(_hands[(int)CurrentPlayer], _biddingMgr.CurrentHighBid, partnerLeading);

                if (bid.HasValue)
                    _biddingMgr.PlaceBid(CurrentPlayer, bid.Value);
                else
                    _biddingMgr.Pass(CurrentPlayer);

                OnBiddingAction?.Invoke(CurrentPlayer, bid);

                if (!_biddingMgr.BiddingComplete)
                    SetCurrentPlayer(_biddingMgr.CurrentBidder);
            }
            _aiBiddingRoutine = null;
        }

        private void HandleBiddingComplete(PlayerSeat winner, int bid)
        {
            _scoreMgr.RegisterBid(winner, bid);
            ChangePhase(GamePhase.TrumpSelection);

            if (winner == HumanSeat)
            {
                Debug.Log($"[29] Bid won by Human (South) at {bid}. Waiting for Trump selection...");
                NotifyStateChanged();
                OnHumanTrumpSelectionRequired?.Invoke(HumanHand);
            }
            else
            {
                PlayerSeat partner     = GameRules.GetPartner(winner);
                Hand       partnerHand = _hands[(int)partner];
                _trumpMgr.SelectTrump(winner, partnerHand);
                Debug.Log($"[29] Bid won by {winner} at {bid}. Trump selected (hidden): {_trumpMgr.TrumpSuit}");
                CompleteTrumpSelectionAndStartPlay();
            }
        }

        /// <summary>Called when human player selects a trump suit from their 4 cards.</summary>
        public void SelectHumanTrump(Suit suit)
        {
            if (CurrentPhase != GamePhase.TrumpSelection) return;

            _trumpMgr.SetTrumpSuit(HumanSeat, suit);
            Debug.Log($"[29] Human South set Trump to {suit}.");
            CompleteTrumpSelectionAndStartPlay();
        }

        /// <summary>Called when human player selects 7th Card (blind mystery trump).</summary>
        public void SelectHumanSeventhCard()
        {
            if (CurrentPhase != GamePhase.TrumpSelection) return;

            _trumpMgr.SetSeventhCardTrump(HumanSeat);
            Debug.Log("[29] Human South set Trump to 7th Card (blind mystery trump).");
            CompleteTrumpSelectionAndStartPlay();
        }

        /// <summary>Called when human player selects Joker (No-Trump mode).</summary>
        public void SelectHumanJoker()
        {
            if (CurrentPhase != GamePhase.TrumpSelection) return;

            _trumpMgr.SetJokerTrump(HumanSeat);
            Debug.Log("[29] Human South set Trump to Joker (No-Trump mode).");
            CompleteTrumpSelectionAndStartPlay();
        }

        private void CompleteTrumpSelectionAndStartPlay()
        {
            // Deal second batch of 4 cards to each player (total 8 cards)
            DealSecondBatch();

            // Begin play — bid winner leads the first trick.
            PlayerSeat winner = _scoreMgr.BidWinner;
            ChangePhase(GamePhase.Playing);
            _trickMgr.StartRound(winner);
            SetCurrentPlayer(winner);

            if (CurrentPlayer != HumanSeat)
                RunAIPlay();

            NotifyStateChanged();
        }

        // ════════════════════════════════════════════════════════════════════════
        // PRIVATE — TRICK PLAY
        // ════════════════════════════════════════════════════════════════════════

        private void AdvancePlayTurn()
        {
            SetCurrentPlayer(_trickMgr.GetCurrentPlayer());
            if (CurrentPlayer != HumanSeat)
                RunAIPlay();
        }

        /// <summary>Runs all consecutive AI card plays until it's the human's turn or the round ends.</summary>
        private void RunAIPlay()
        {
            if (enablePacing && gameObject.activeInHierarchy)
            {
                if (_aiPlayRoutine != null) StopCoroutine(_aiPlayRoutine);
                _aiPlayRoutine = StartCoroutine(AIPlayRoutine());
            }
            else
            {
                while (CurrentPhase == GamePhase.Playing
                       && !_trickMgr.RoundComplete
                       && CurrentPlayer != HumanSeat)
                {
                    AIPlayer ai = GetAI(CurrentPlayer);
                    if (ai == null) break;

                    PlayerSeat partner        = GameRules.GetPartner(CurrentPlayer);
                    Suit?      visibleTrump   = _trumpMgr.GetVisibleTrump(CurrentPlayer);

                    Card card = ai.DecideCardToPlay(
                        _hands[(int)CurrentPlayer],
                        _trickMgr.CurrentTrick,
                        visibleTrump,
                        partner);

                    _trickMgr.PlayCard(CurrentPlayer, card, _hands[(int)CurrentPlayer]);

                    if (!_trickMgr.RoundComplete)
                        SetCurrentPlayer(_trickMgr.GetCurrentPlayer());
                }
            }
        }

        private IEnumerator AIPlayRoutine()
        {
            while (CurrentPhase == GamePhase.Playing
                   && !_trickMgr.RoundComplete
                   && CurrentPlayer != HumanSeat)
            {
                yield return new WaitForSeconds(aiPlayDelay);

                if (CurrentPhase != GamePhase.Playing || _trickMgr.RoundComplete || CurrentPlayer == HumanSeat)
                    yield break;

                AIPlayer ai = GetAI(CurrentPlayer);
                if (ai == null) yield break;

                PlayerSeat partner        = GameRules.GetPartner(CurrentPlayer);
                Suit?      visibleTrump   = _trumpMgr.GetVisibleTrump(CurrentPlayer);

                Card card = ai.DecideCardToPlay(
                    _hands[(int)CurrentPlayer],
                    _trickMgr.CurrentTrick,
                    visibleTrump,
                    partner);

                bool completesTrick = _trickMgr.CurrentTrick != null && _trickMgr.CurrentTrick.PlayCount == 3;
                _trickMgr.PlayCard(CurrentPlayer, card, _hands[(int)CurrentPlayer]);

                if (completesTrick)
                {
                    yield return new WaitForSeconds(trickClearDelay);
                }

                if (!_trickMgr.RoundComplete)
                    SetCurrentPlayer(_trickMgr.GetCurrentPlayer());
            }
            _aiPlayRoutine = null;
        }

        private void HandleCardPlayed(PlayerSeat player, Card card)
        {
            Debug.Log($"[29] {player} played {card}");
            OnCardPlayed?.Invoke(player, card);
            NotifyStateChanged();
        }

        private void HandleTrickWon(PlayerSeat winner, int points)
        {
            Debug.Log($"[29] Trick won by {winner} ({points} pts). Trick total: {_trickMgr.TricksCompleted}/8");
            OnTrickWon?.Invoke(winner, points);
            NotifyStateChanged();
        }

        private void HandleRoundComplete()
        {
            ChangePhase(GamePhase.RoundOver);
            int[] teamPts = _trickMgr.GetTeamPoints();
            Debug.Log($"[29] Round complete. Team points — {GameRules.TeamName(0)}: {teamPts[0]}, {GameRules.TeamName(1)}: {teamPts[1]}");
            _scoreMgr.ScoreRound(teamPts);
        }

        private void HandleRoundScored(int biddingTeam, int bid, bool biddingTeamWon)
        {
            string result = biddingTeamWon ? "WON ✓" : "LOST ✗";
            Debug.Log($"[29] {GameRules.TeamName(biddingTeam)} bid {bid} → {result}. {_scoreMgr.GetScoreString()}");
            OnRoundScored?.Invoke(biddingTeamWon);
            NotifyStateChanged();
        }

        private void HandleGameOver(int winningTeam)
        {
            ChangePhase(GamePhase.GameOver);
            Debug.Log($"[29] GAME OVER — {GameRules.TeamName(winningTeam)} wins! {_scoreMgr.GetScoreString()}");
            OnGameOver?.Invoke(winningTeam);
            NotifyStateChanged();
        }

        // ════════════════════════════════════════════════════════════════════════
        // PRIVATE UTILITIES
        // ════════════════════════════════════════════════════════════════════════

        private void ChangePhase(GamePhase phase)
        {
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        private void SetCurrentPlayer(PlayerSeat seat)
        {
            CurrentPlayer = seat;
            OnCurrentPlayerChanged?.Invoke(seat);
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();

        private AIPlayer GetAI(PlayerSeat seat)
        {
            switch (seat)
            {
                case PlayerSeat.West:  return _aiPlayers[0];
                case PlayerSeat.North: return _aiPlayers[1];
                case PlayerSeat.East:  return _aiPlayers[2];
                default:               return null; // South = human
            }
        }
    }
}
