using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        private readonly Deck _deck = new Deck();
        private readonly BiddingManager _biddingMgr = new BiddingManager();
        private readonly TrumpManager _trumpMgr = new TrumpManager();
        private readonly ScoreManager _scoreMgr = new ScoreManager();
        private TrickManager _trickMgr;          // created after trump is ready

        // Public read access for UI
        public BiddingManager BiddingManager => _biddingMgr;
        public TrumpManager TrumpManager => _trumpMgr;
        public ScoreManager ScoreManager => _scoreMgr;
        public TrickManager TrickManager => _trickMgr;

        // ════════════════════════════════════════════════════════════════════════
        // PLAYER DATA
        // ════════════════════════════════════════════════════════════════════════

        private readonly Hand[] _hands = new Hand[4];
        private readonly AIPlayer[] _aiPlayers = new AIPlayer[3]; // West, North, East

        public const PlayerSeat HumanSeat = PlayerSeat.South;

        /// <summary>The human player's current hand.</summary>
        public Hand HumanHand => _hands[(int)HumanSeat];

        // ════════════════════════════════════════════════════════════════════════
        // GAME STATE
        // ════════════════════════════════════════════════════════════════════════

        public GamePhase CurrentPhase { get; private set; } = GamePhase.WaitingToStart;
        public PlayerSeat Dealer { get; private set; } = PlayerSeat.West;
        public PlayerSeat CurrentPlayer { get; private set; }

        public bool IsSinglePlayActive { get; private set; }
        public PlayerSeat? SinglePlayerSeat { get; private set; }
        public PlayerSeat? DisabledPartnerSeat => IsSinglePlayActive && SinglePlayerSeat.HasValue ? GameRules.GetPartner(SinglePlayerSeat.Value) : (PlayerSeat?)null;
        public DoubleStatus DoubleState => _scoreMgr.CurrentDoubleStatus;
        public PlayerSeat? Doubler => _scoreMgr.Doubler;
        public PlayerSeat? ReDoubler => _scoreMgr.ReDoubler;

        // ════════════════════════════════════════════════════════════════════════
        // PACING & COROUTINES
        // ════════════════════════════════════════════════════════════════════════

        [Header("Pacing & Delays")]
        [SerializeField] private bool enablePacing = true;
        [SerializeField] private float aiBidDelay = 0.5f;
        [SerializeField] private float aiPlayDelay = 0.45f;
        [SerializeField] private float cardTravelDuration = 0.9f;
        [SerializeField] private float trickClearDelay = 1.2f;

        public bool EnablePacing { get => enablePacing; set => enablePacing = value; }
        public float AIBidDelay => aiBidDelay;
        public float AIPlayDelay => aiPlayDelay;
        public float CardTravelDuration => cardTravelDuration;
        public float TrickClearDelay => trickClearDelay;

        private Coroutine _aiBiddingRoutine;
        private Coroutine _aiPlayRoutine;
        private bool _doubleDecisionCompleted;
        private bool _waitingForSinglePlayDecision;

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

        /// <summary>Fired when human player meets Single Play dependency conditions.</summary>
        public event Action OnSinglePlayEligible;

        /// <summary>Fired when the opposing player gets the opportunity to Double.</summary>
        public event Action OnDoubleDecisionRequired;

        /// <summary>Fired when an opponent sets Double, giving the bidding team the opportunity to Re-Double.</summary>
        public event Action<PlayerSeat> OnReDoubleDecisionRequired;

        /// <summary>General "something changed — refresh your display" event.</summary>
        public event Action OnStateChanged;

        /// <summary>
        /// Fired when an AI player starts "thinking" about their bid.
        /// UI can show a "Thinking…" bubble on the corresponding seat.
        /// Complement: OnBiddingAction fires when the decision is made.
        /// </summary>
        public event Action<PlayerSeat> OnAIBiddingThinking;

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
            _trickMgr.OnCardPlayed += HandleCardPlayed;
            _trickMgr.OnTrickWon += HandleTrickWon;
            _trickMgr.OnRoundComplete += HandleRoundComplete;
            _trumpMgr.OnTrumpChosen += NotifyStateChanged;
            _trumpMgr.OnTrumpRevealed += t => { OnTrumpRevealed?.Invoke(t); NotifyStateChanged(); };
            _scoreMgr.OnRoundScored += HandleRoundScored;
            _scoreMgr.OnGameOver += HandleGameOver;
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
            if (_waitingForSinglePlayDecision) return false;
            if (CurrentPhase != GamePhase.Playing || CurrentPlayer != HumanSeat) return false;
            if (IsSinglePlayActive && DisabledPartnerSeat.HasValue && HumanSeat == DisabledPartnerSeat.Value) return false;

            int completingCount = (_trickMgr != null && _trickMgr.IsSinglePlay) ? 2 : 3;
            bool wasCompletingCard = _trickMgr.CurrentTrick != null && _trickMgr.CurrentTrick.PlayCount == completingCount;
            bool ok = _trickMgr.PlayCard(HumanSeat, card, _hands[(int)HumanSeat]);
            if (ok)
            {
                NotifyStateChanged();
                if (!_trickMgr.RoundComplete)
                {
                    float delay = cardTravelDuration;
                    if (wasCompletingCard && enablePacing)
                        delay += trickClearDelay;
                    if (enablePacing)
                        StartCoroutine(DelayedAdvancePlayTurn(delay));
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

        /// <summary>
        /// Reveal trump. Only legal when this player cannot follow the led suit.
        /// After a 7th-card reveal, that card is returned to the bidder's hand.
        /// </summary>
        public bool RevealTrump()
        {
            if (!CanRevealTrump(CurrentPlayer)) return false;

            if (!_trumpMgr.RevealTrumpExplicitly()) return false;

            if (_trumpMgr.TryReturnSeventhCardToHand(out Card seventh))
            {
                _hands[(int)_trumpMgr.Bidder].AddCard(seventh);
            }

            NotifyStateChanged();
            return true;
        }

        /// <summary>True if this seat may reveal the face-down trump right now.</summary>
        public bool CanRevealTrump(PlayerSeat seat)
        {
            if (_waitingForSinglePlayDecision || CurrentPhase != GamePhase.Playing || CurrentPlayer != seat) return false;
            return _trumpMgr.CanReveal(seat, _trickMgr?.CurrentTrick, _hands[(int)seat]);
        }

        public bool CanHumanRevealTrump() => CanRevealTrump(HumanSeat);

        // ── MARRIAGE ────────────────────────────────────────────────────────

        /// <summary>
        /// True if <paramref name="player"/> may declare a Marriage right now:
        /// trump has been revealed, they still hold both the King and Queen of
        /// the trump suit, their team has won at least one trick this round,
        /// and no Marriage has been declared yet this round.
        /// </summary>
        public bool CanDeclareMarriage(PlayerSeat player)
        {
            if (CurrentPhase != GamePhase.Playing) return false;
            if (IsSinglePlayActive && DisabledPartnerSeat.HasValue && player == DisabledPartnerSeat.Value) return false;
            if (_trumpMgr == null || !_trumpMgr.TrumpRevealed || !_trumpMgr.TrumpSuit.HasValue) return false;
            if (_scoreMgr.MarriageDeclared) return false;

            Suit trump = _trumpMgr.TrumpSuit.Value;
            List<Card> trumpCards = _hands[(int)player].GetCardsBySuit(trump);
            bool hasKing = trumpCards.Any(c => c.Rank == Rank.King);
            bool hasQueen = trumpCards.Any(c => c.Rank == Rank.Queen);
            if (!hasKing || !hasQueen) return false;

            int team = GameRules.GetTeam(player);
            int[] tricksTaken = _trickMgr != null ? _trickMgr.GetTricksTaken() : new int[4];
            int teamTricks = 0;
            for (int s = 0; s < 4; s++)
                if (GameRules.GetTeam((PlayerSeat)s) == team) teamTricks += tricksTaken[s];

            return teamTricks >= 1;
        }

        public bool CanHumanDeclareMarriage() => CanDeclareMarriage(HumanSeat);

        /// <summary>
        /// Declares a Marriage for <paramref name="player"/>'s team, shifting the
        /// calling team's target ±4 (see <see cref="ScoreManager.DeclareMarriage"/>).
        /// Returns false if <see cref="CanDeclareMarriage"/> would return false.
        /// </summary>
        public bool DeclareMarriage(PlayerSeat player)
        {
            if (!CanDeclareMarriage(player)) return false;

            bool applied = _scoreMgr.DeclareMarriage(GameRules.GetTeam(player));
            if (applied) NotifyStateChanged();
            return applied;
        }

        public bool DeclareHumanMarriage() => DeclareMarriage(HumanSeat);

        // ── SKIP (early finish once the round's outcome is already decided) ─

        /// <summary>
        /// True if it's currently <paramref name="player"/>'s turn to act,
        /// their team is the calling (bidding) team, and the round's outcome
        /// is already mathematically settled either way:
        ///   • the bidding team has already reached <see cref="ScoreManager.EffectiveTarget"/>
        ///     in card points this round (an early win), or
        ///   • the opposing team already holds enough card points that the
        ///     bidding team can no longer reach that target even by winning
        ///     every remaining point (an early loss) — i.e. opposing points
        ///     ≥ <see cref="GameRules.TotalCardPoints"/> − EffectiveTarget.
        /// Available any time it's this player's turn, including mid-trick —
        /// not just between tricks — since tying it to an empty trick meant
        /// the window could pass without the player (who isn't always the
        /// next trick's leader) ever getting a turn to use it.
        /// </summary>
        public bool IsSkipAvailable(PlayerSeat player)
        {
            if (CurrentPhase != GamePhase.Playing) return false;
            if (_trickMgr == null || _trickMgr.RoundComplete) return false;
            if (IsSinglePlayActive) return false;

            int biddingTeam = _scoreMgr.BiddingTeam;
            int opposingTeam = 1 - biddingTeam;
            int target = _scoreMgr.EffectiveTarget;

            bool alreadyWon = _trickMgr.GetTeamPoints(biddingTeam) >= target;
            bool alreadyLost = _trickMgr.GetTeamPoints(opposingTeam) >= (GameRules.TotalCardPoints - target);

            return alreadyWon || alreadyLost;
        }

        public bool IsHumanSkipAvailable() => !_waitingForSinglePlayDecision && IsSkipAvailable(HumanSeat);

        /// <summary>Ends the round immediately, forfeiting the remaining unplayed tricks.</summary>
        public bool SkipRemainingPlay()
        {
            if (!IsHumanSkipAvailable()) return false;
            StopAllCoroutines();
            _aiPlayRoutine = null;
            _aiBiddingRoutine = null;
            return _trickMgr.SkipRemaining();
        }

        // ════════════════════════════════════════════════════════════════════════
        // PUBLIC QUERY API  (for UI read-only access)
        // ════════════════════════════════════════════════════════════════════════

        public Hand GetHand(PlayerSeat seat) => _hands[(int)seat];
        public int[] GetGamePoints() => _scoreMgr.GamePoints;
        public int[] GetRoundPoints() => _trickMgr?.GetTeamPoints() ?? new int[2];
        public Trick GetCurrentTrick() => _trickMgr?.CurrentTrick;
        public Trick LastCompletedTrick => _trickMgr?.LastCompletedTrick;
        public int GetCurrentBid() => _biddingMgr.CurrentHighBid;
        public PlayerSeat GetCurrentHighBidder() => _biddingMgr.CurrentHighBidder;
        public int GetFinalBid() => _scoreMgr.CurrentBid;
        public int GetEffectiveTarget() => _scoreMgr.EffectiveTarget;
        public PlayerSeat GetBidWinner() => _scoreMgr.BidWinner;
        public Suit? GetTrumpForHuman() => _trumpMgr.GetVisibleTrump(HumanSeat);
        public bool IsTrumpRevealed() => _trumpMgr.TrumpRevealed;
        public int GetMinimumBid() => _biddingMgr.MinimumRaiseBid();

        /// <summary>Returns the legal cards South can play right now (empty if not their turn).</summary>
        public List<Card> GetHumanValidPlays()
        {
            if (_waitingForSinglePlayDecision)
                return new List<Card>();
            if (CurrentPhase != GamePhase.Playing || CurrentPlayer != HumanSeat)
                return new List<Card>();
            if (IsSinglePlayActive && DisabledPartnerSeat.HasValue && HumanSeat == DisabledPartnerSeat.Value)
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

            // Reset Single Play and Double state for the new round
            IsSinglePlayActive = false;
            SinglePlayerSeat = null;
            _doubleDecisionCompleted = false;
            _waitingForSinglePlayDecision = false;

            // Clear hands and reset round/trick points.
            for (int i = 0; i < 4; i++) _hands[i].Clear();
            _trumpMgr.Reset();
            _trickMgr.ResetPoints();
            _trickMgr.SetSinglePlay(false, null);

            // Advance dealer clockwise.
            Dealer = GameRules.NextPlayer(Dealer);

            // Deal first batch of 4 cards starting from the player after the dealer.
            ChangePhase(GamePhase.Dealing);
            DealFirstBatch();

            StartBiddingPhase();
        }

        /// <summary>Starts normal bidding phase clockwise from the player after the dealer.</summary>
        public void StartBiddingPhase()
        {
            PlayerSeat firstBidder = GameRules.NextPlayer(Dealer);
            _biddingMgr.StartBidding(firstBidder);
            ChangePhase(GamePhase.Bidding);
            SetCurrentPlayer(firstBidder);

            if (CurrentPlayer != HumanSeat)
                RunAIBidding();

            NotifyStateChanged();
        }

        /// <summary>Called when human player accepts Single Play via Decision Panel after seeing 8 cards.</summary>
        public void AcceptSinglePlay()
        {
            _waitingForSinglePlayDecision = false;
            IsSinglePlayActive = true;
            SinglePlayerSeat = HumanSeat;
            PlayerSeat partner = GameRules.GetPartner(HumanSeat);
            _trickMgr.SetSinglePlay(true, partner);

            Debug.Log($"[29] ★ SINGLE PLAY ACTIVATED by {HumanSeat}! Partner {partner} is disabled for this round.");

            PlayerSeat winner = _scoreMgr.BidWinner;
            PlayerSeat firstLeader = (winner == partner) ? HumanSeat : winner;
            _trickMgr.StartRound(firstLeader);
            SetCurrentPlayer(firstLeader);

            NotifyStateChanged();

            if (CurrentPlayer != HumanSeat)
                RunAIPlay();
        }

        /// <summary>Called when human player rejects Single Play via Negative button after seeing 8 cards.</summary>
        public void RejectSinglePlay()
        {
            _waitingForSinglePlayDecision = false;
            Debug.Log("[29] Single Play declined. Continuing normal 4-player round.");
            IsSinglePlayActive = false;
            SinglePlayerSeat = null;
            _trickMgr.SetSinglePlay(false, null);

            NotifyStateChanged();

            if (CurrentPlayer != HumanSeat)
                RunAIPlay();
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

            SortAllHands();

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
                {
                    Card card7 = bidderHand.Cards[6];
                    bidderHand.RemoveCard(card7);
                    _trumpMgr.ResolveSeventhCard(card7);
                    Debug.Log($"[29] 7th card set aside face-down as hidden trump ({_trumpMgr.Bidder}).");
                }
            }

            SortAllHands();

            Debug.Log($"[29] 🃏 Second 4 cards dealt. Full hand (8 cards): {string.Join(", ", HumanHand.Cards)}");
            OnHumanHandDealt?.Invoke(HumanHand);
        }

        private void SortAllHands()
        {
            for (int i = 0; i < 4; i++)
                _hands[i].SortForDisplay();
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
                // Show "Thinking" bubble on the current AI seat immediately,
                // before the delay so the player sees the AI considering.
                OnAIBiddingThinking?.Invoke(CurrentPlayer);

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
            if (_aiBiddingRoutine != null)
            {
                StopCoroutine(_aiBiddingRoutine);
                _aiBiddingRoutine = null;
            }

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
                Hand winnerHand = _hands[(int)winner];
                AIPlayer ai = GetAI(winner);
                if (ai != null)
                    ApplyAITrumpChoice(ai, winner, winnerHand);
                else
                    _trumpMgr.SelectTrump(winner, winnerHand);

                Debug.Log($"[29] Bid won by {winner} at {bid}. Trump mode: {_trumpMgr.Mode} (hidden until revealed).");
                StartDoubleDecisionStep();
            }
        }

        /// <summary>Called when the human Bid Winner selects a trump suit from their 4 cards.</summary>
        public void SelectHumanTrump(Suit suit)
        {
            if (CurrentPhase != GamePhase.TrumpSelection) return;

            _trumpMgr.SetTrumpSuit(HumanSeat, suit);
            Debug.Log($"[29] Human South set Trump to {suit}.");
            StartDoubleDecisionStep();
        }

        /// <summary>Called when human player selects 7th Card (blind mystery trump).</summary>
        public void SelectHumanSeventhCard()
        {
            if (CurrentPhase != GamePhase.TrumpSelection) return;

            _trumpMgr.SetSeventhCardTrump(HumanSeat);
            Debug.Log("[29] Human South set Trump to 7th Card (blind mystery trump).");
            StartDoubleDecisionStep();
        }

        /// <summary>Called when human player selects Joker (No-Trump mode).</summary>
        public void SelectHumanJoker()
        {
            if (CurrentPhase != GamePhase.TrumpSelection) return;

            _trumpMgr.SetJokerTrump(HumanSeat);
            Debug.Log("[29] Human South set Trump to Joker (Jacks are super-trumps).");
            StartDoubleDecisionStep();
        }

        private void StartDoubleDecisionStep()
        {
            if (_doubleDecisionCompleted)
            {
                CompleteTrumpSelectionAndStartPlay();
                return;
            }

            int biddingTeam = _scoreMgr.BiddingTeam;
            int humanTeam = GameRules.GetTeam(HumanSeat);

            if (humanTeam != biddingTeam)
            {
                // Human is on opposing team — give human the opportunity to Double
                Debug.Log("[29] Opposing team (Human South) offered Double opportunity.");
                NotifyStateChanged();
                OnDoubleDecisionRequired?.Invoke();
            }
            else
            {
                // Human is on bidding team — check if opposing AI doubles
                PlayerSeat? doublerAI = CheckOpposingAIDouble();
                if (doublerAI.HasValue)
                {
                    _scoreMgr.SetDouble(doublerAI.Value);
                    Debug.Log($"[29] Opponent {doublerAI.Value} set DOUBLE!");
                    NotifyStateChanged();

                    // Bidding team (Human South) gets opportunity to Re-Double
                    OnReDoubleDecisionRequired?.Invoke(doublerAI.Value);
                }
                else
                {
                    Debug.Log("[29] Opponents did not set Double.");
                    _doubleDecisionCompleted = true;
                    CompleteTrumpSelectionAndStartPlay();
                }
            }
        }

        private PlayerSeat? CheckOpposingAIDouble()
        {
            int biddingTeam = _scoreMgr.BiddingTeam;
            for (int s = 0; s < 4; s++)
            {
                PlayerSeat seat = (PlayerSeat)s;
                if (seat == HumanSeat) continue;
                if (IsSinglePlayActive && DisabledPartnerSeat.HasValue && seat == DisabledPartnerSeat.Value) continue;

                if (GameRules.GetTeam(seat) != biddingTeam)
                {
                    AIPlayer ai = GetAI(seat);
                    if (ai != null && ai.DecideDouble(_hands[(int)seat], _scoreMgr.CurrentBid))
                        return seat;
                }
            }
            return null;
        }

        private void CheckBiddingAIReDouble()
        {
            int biddingTeam = _scoreMgr.BiddingTeam;
            for (int s = 0; s < 4; s++)
            {
                PlayerSeat seat = (PlayerSeat)s;
                if (seat == HumanSeat) continue;
                if (IsSinglePlayActive && DisabledPartnerSeat.HasValue && seat == DisabledPartnerSeat.Value) continue;

                if (GameRules.GetTeam(seat) == biddingTeam)
                {
                    AIPlayer ai = GetAI(seat);
                    if (ai != null && ai.DecideReDouble(_hands[(int)seat], _scoreMgr.CurrentBid))
                    {
                        _scoreMgr.SetReDouble(seat);
                        Debug.Log($"[29] Bidding team AI ({seat}) responded with RE-DOUBLE!");
                        NotifyStateChanged();
                        break;
                    }
                }
            }
        }

        /// <summary>Called when human player clicks DOUBLE on the Decision Panel.</summary>
        public void AcceptHumanDouble()
        {
            if (_doubleDecisionCompleted) return;
            _doubleDecisionCompleted = true;

            _scoreMgr.SetDouble(HumanSeat);
            Debug.Log("[29] Human South set DOUBLE!");
            NotifyStateChanged();

            // Check if bidding AI wants to Re-Double
            CheckBiddingAIReDouble();
            CompleteTrumpSelectionAndStartPlay();
        }

        /// <summary>Called when human player clicks NO to reject Double.</summary>
        public void RejectHumanDouble()
        {
            if (_doubleDecisionCompleted) return;
            _doubleDecisionCompleted = true;

            Debug.Log("[29] Human South declined Double.");
            CompleteTrumpSelectionAndStartPlay();
        }

        /// <summary>Called when human player clicks RE-DOUBLE on the Decision Panel.</summary>
        public void AcceptHumanReDouble()
        {
            if (_doubleDecisionCompleted) return;
            _doubleDecisionCompleted = true;

            _scoreMgr.SetReDouble(HumanSeat);
            Debug.Log("[29] Human South set RE-DOUBLE!");
            NotifyStateChanged();
            CompleteTrumpSelectionAndStartPlay();
        }

        /// <summary>Called when human player clicks NO to reject Re-Double.</summary>
        public void RejectHumanReDouble()
        {
            if (_doubleDecisionCompleted) return;
            _doubleDecisionCompleted = true;

            Debug.Log("[29] Human South declined Re-Double (Double remains active).");
            CompleteTrumpSelectionAndStartPlay();
        }

        private void ApplyAITrumpChoice(AIPlayer ai, PlayerSeat winner, Hand winnerHand)
        {
            TrumpMode mode = ai.DecideTrumpMode(winnerHand, out Suit suit);
            switch (mode)
            {
                case TrumpMode.SeventhCard:
                    _trumpMgr.SetSeventhCardTrump(winner);
                    break;
                case TrumpMode.Joker:
                    _trumpMgr.SetJokerTrump(winner);
                    break;
                default:
                    _trumpMgr.SetTrumpSuit(winner, suit);
                    break;
            }
        }

        private void CompleteTrumpSelectionAndStartPlay()
        {
            PlayerSeat winner = _scoreMgr.BidWinner;

            // Transition phase to Playing before dealing second batch
            // so queries for valid plays recognize the Playing phase immediately
            ChangePhase(GamePhase.Playing);

            // Begin play — bid winner leads the first trick. This MUST happen before
            // DealSecondBatch(): dealing fires OnHumanHandDealt, which the UI uses to
            // render the newly dealt cards (with a fly-in animation that bakes in their
            // "is this playable" state at creation time). If CurrentPlayer/CurrentTrick
            // aren't set yet, GetHumanValidPlays() sees a stale player, the new cards get
            // built as (incorrectly) unplayable, and no later correction can fix them
            // because the deal animation already queued a fade using that stale value.
            _trickMgr.StartRound(winner);
            SetCurrentPlayer(winner);

            // Deal second batch of 4 cards to each player (total 8 cards)
            DealSecondBatch();

            // After seeing all 8 cards, prompt Human player if they want to play single before trick play starts
            _waitingForSinglePlayDecision = true;
            Debug.Log("[29] 🃏 All 8 cards dealt. Prompting Human for Single Play decision.");
            NotifyStateChanged();
            OnSinglePlayEligible?.Invoke();
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

                    PlayerSeat partner = GameRules.GetPartner(CurrentPlayer);
                    MaybeAIRevealTrump(CurrentPlayer);

                    Suit? visibleTrump = _trumpMgr.GetVisibleTrump(CurrentPlayer);

                    Card card = ai.DecideCardToPlay(
                        _hands[(int)CurrentPlayer],
                        _trickMgr.CurrentTrick,
                        visibleTrump,
                        partner,
                        _trumpMgr.Mode);

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

                PlayerSeat partner = GameRules.GetPartner(CurrentPlayer);
                MaybeAIRevealTrump(CurrentPlayer);

                Suit? visibleTrump = _trumpMgr.GetVisibleTrump(CurrentPlayer);

                Card card = ai.DecideCardToPlay(
                    _hands[(int)CurrentPlayer],
                    _trickMgr.CurrentTrick,
                    visibleTrump,
                    partner,
                    _trumpMgr.Mode);

                int completingCount = (_trickMgr != null && _trickMgr.IsSinglePlay) ? 2 : 3;
                bool completesTrick = _trickMgr.CurrentTrick != null && _trickMgr.CurrentTrick.PlayCount == completingCount;
                _trickMgr.PlayCard(CurrentPlayer, card, _hands[(int)CurrentPlayer]);

                yield return new WaitForSeconds(cardTravelDuration);

                if (completesTrick)
                {
                    yield return new WaitForSeconds(trickClearDelay);
                }

                if (!_trickMgr.RoundComplete)
                    SetCurrentPlayer(_trickMgr.GetCurrentPlayer());
            }
            _aiPlayRoutine = null;
        }

        private void MaybeAIRevealTrump(PlayerSeat seat)
        {
            if (!CanRevealTrump(seat)) return;

            AIPlayer ai = GetAI(seat);
            if (ai == null) return;

            Suit? visibleTrump = _trumpMgr.GetVisibleTrump(seat);
            if (!ai.ShouldRevealTrump(_hands[(int)seat], _trickMgr.CurrentTrick, visibleTrump))
                return;

            RevealTrump();
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

            // Single Hand failure condition: if any opponent player wins a trick, Single Hand fails immediately!
            if (IsSinglePlayActive && SinglePlayerSeat.HasValue && CurrentPhase == GamePhase.Playing)
            {
                int singleTeam = GameRules.GetTeam(SinglePlayerSeat.Value);
                if (GameRules.GetTeam(winner) != singleTeam)
                {
                    Debug.Log($"[29] ✘ Opponent {winner} won a trick! Single Hand FAILED immediately (-3 set points).");
                    StopAllCoroutines();
                    _aiPlayRoutine = null;
                    _aiBiddingRoutine = null;
                    _trickMgr.TerminateRoundEarly();
                    ChangePhase(GamePhase.RoundOver);
                    _scoreMgr.ScoreSingleHand(singleTeam, success: false);
                    return;
                }
            }
        }

        private void HandleRoundComplete()
        {
            if (CurrentPhase == GamePhase.RoundOver || CurrentPhase == GamePhase.GameOver) return;

            ChangePhase(GamePhase.RoundOver);
            int[] teamPts = _trickMgr.GetTeamPoints();
            Debug.Log($"[29] Round complete. Team points — {GameRules.TeamName(0)}: {teamPts[0]}, {GameRules.TeamName(1)}: {teamPts[1]}");

            if (IsSinglePlayActive && SinglePlayerSeat.HasValue)
            {
                int singleTeam = GameRules.GetTeam(SinglePlayerSeat.Value);
                int opposingTeam = 1 - singleTeam;
                int opposingTricks = 0;
                int[] tricksTaken = _trickMgr.GetTricksTaken();
                for (int s = 0; s < 4; s++)
                {
                    if (GameRules.GetTeam((PlayerSeat)s) == opposingTeam)
                        opposingTricks += tricksTaken[s];
                }
                bool success = opposingTricks == 0;
                _scoreMgr.ScoreSingleHand(singleTeam, success);
            }
            else
            {
                _scoreMgr.ScoreRound(teamPts);
            }
        }

        private void HandleRoundScored(int biddingTeam, int bid, bool biddingTeamWon)
        {
            if (_scoreMgr.LastRoundWasSingleHand)
            {
                string res = _scoreMgr.LastSingleHandSuccess ? "SUCCESS (+3) ✓" : "FAILED (-3) ✗";
                Debug.Log($"[29] Single Hand by {GameRules.TeamName(biddingTeam)} → {res}. {_scoreMgr.GetScoreString()}");
            }
            else
            {
                string result = biddingTeamWon ? "WON ✓" : "LOST ✗";
                Debug.Log($"[29] {GameRules.TeamName(biddingTeam)} bid {bid} → {result}. {_scoreMgr.GetScoreString()}");
            }
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
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();

        private AIPlayer GetAI(PlayerSeat seat)
        {
            switch (seat)
            {
                case PlayerSeat.West: return _aiPlayers[0];
                case PlayerSeat.North: return _aiPlayers[1];
                case PlayerSeat.East: return _aiPlayers[2];
                default: return null; // South = human
            }
        }
    }
}