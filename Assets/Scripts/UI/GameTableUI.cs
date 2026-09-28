using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace Game29
{
    /// <summary>
    /// Master UI Manager for the 29 Card Game — Landscape Layout.
    /// Reference resolution: 1920×1080.
    /// Board background uses Board.jpg from Resources.
    /// Player seat layout:
    ///   South  (Human)    — bottom-center with 8 large interactive cards
    ///   North  (Partner)  — top-center
    ///   West   (Opponent) — left
    ///   East   (Opponent) — right
    ///   Trump  (Single Card) — sits on the wooden table board left of trick area, click-to-reveal
    ///   Tricks (Center)   — cross pattern
    /// </summary>
    public class GameTableUI : MonoBehaviour
    {
        // ── Component References ────────────────────────────────────────────────
        [Header("Root & Layout")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private Image tableBackground;

        [Header("Player Seats")]
        [SerializeField] private PlayerSeatUI southSeat;
        [SerializeField] private PlayerSeatUI northSeat;
        [SerializeField] private PlayerSeatUI westSeat;
        [SerializeField] private PlayerSeatUI eastSeat;

        [Header("Game Areas")]
        [SerializeField] private TrickAreaUI trickArea;
        [SerializeField] private BiddingPanelUI biddingPanel;
        [SerializeField] private ScoreHUDUI scoreHUD;
        [SerializeField] private RoundEndModalUI roundEndModal;
        [SerializeField] private TrumpSelectionModalUI trumpSelectionModal;
        [SerializeField] private TrumpCardSlotUI trumpCardSlot;
        [SerializeField] private DecisionPanelUI decisionPanel;
        [SerializeField] private RulesPanelUI rulesPanel;

        [Header("Point Card System (-6 to +6)")]
        [SerializeField] private PointCardSlotUI yourTeamPointCard;
        [SerializeField] private PointCardSlotUI opponentPointCard;

        private GameManager _gm;
        private bool _isInitialized;
        private bool _animateNextDeal;

        // ════════════════════════════════════════════════════════════════════════
        // INITIALISATION
        // ════════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsureInputSystemEventSystem();
            BuildUIIfMissing();
            ApplyLandscapeLayout();
        }

        private void Start()
        {
            EnsureInputSystemEventSystem();
            ApplyLandscapeLayout();

            _gm = GameManager.Instance;
            if (_gm == null)
            {
                Debug.LogWarning("[29 GameTableUI] GameManager instance not found, waiting...");
                StartCoroutine(WaitForGameManager());
                return;
            }

            InitializeGameUI();
        }

        private IEnumerator WaitForGameManager()
        {
            while (GameManager.Instance == null)
                yield return null;

            _gm = GameManager.Instance;
            InitializeGameUI();
        }

        private void InitializeGameUI()
        {
            if (_isInitialized || _gm == null) return;
            _isInitialized = true;

            _gm.OnPhaseChanged += HandlePhaseChanged;
            _gm.OnHumanHandDealt += HandleHumanHandDealt;
            _gm.OnCurrentPlayerChanged += HandleCurrentPlayerChanged;
            _gm.OnBiddingAction += HandleBiddingAction;
            _gm.OnAIBiddingThinking += HandleAIBiddingThinking;
            _gm.OnCardPlayed += HandleCardPlayed;
            _gm.OnTrickWon += HandleTrickWon;
            _gm.OnTrumpChosen += HandleTrumpChosen;
            _gm.OnRoundScored += HandleRoundScored;
            _gm.OnGameOver += HandleGameOver;
            _gm.OnHumanTrumpSelectionRequired += HandleHumanTrumpSelectionRequired;
            _gm.OnSinglePlayEligible += HandleSinglePlayEligible;
            _gm.OnDoubleDecisionRequired += HandleDoubleDecisionRequired;
            _gm.OnReDoubleDecisionRequired += HandleReDoubleDecisionRequired;
            _gm.OnStateChanged += RefreshAllDisplay;
            _gm.ScoreManager.OnMarriageDeclared += HandleMarriageDeclared;

            if (southSeat != null) southSeat.BindSkipButton(OnSkipClicked);

            RefreshAllDisplay();
        }

        private void OnDestroy()
        {
            if (_gm == null) return;
            _gm.OnPhaseChanged -= HandlePhaseChanged;
            _gm.OnHumanHandDealt -= HandleHumanHandDealt;
            _gm.OnCurrentPlayerChanged -= HandleCurrentPlayerChanged;
            _gm.OnBiddingAction -= HandleBiddingAction;
            _gm.OnAIBiddingThinking -= HandleAIBiddingThinking;
            _gm.OnCardPlayed -= HandleCardPlayed;
            _gm.OnTrickWon -= HandleTrickWon;
            _gm.OnTrumpRevealed -= HandleTrumpRevealed;
            _gm.OnTrumpChosen -= HandleTrumpChosen;
            _gm.OnRoundScored -= HandleRoundScored;
            _gm.OnGameOver -= HandleGameOver;
            _gm.OnHumanTrumpSelectionRequired -= HandleHumanTrumpSelectionRequired;
            _gm.OnSinglePlayEligible -= HandleSinglePlayEligible;
            _gm.OnDoubleDecisionRequired -= HandleDoubleDecisionRequired;
            _gm.OnReDoubleDecisionRequired -= HandleReDoubleDecisionRequired;
            _gm.OnStateChanged -= RefreshAllDisplay;
            _gm.ScoreManager.OnMarriageDeclared -= HandleMarriageDeclared;
        }

        // ════════════════════════════════════════════════════════════════════════
        // GAME EVENT HANDLERS
        // ════════════════════════════════════════════════════════════════════════

        private void HandlePhaseChanged(GamePhase phase)
        {
            RefreshAllDisplay();

            switch (phase)
            {
                case GamePhase.Dealing:
                    SetHudStatus("Dealing cards from the deck...");
                    trickArea.ClearAll();
                    biddingPanel.Hide();
                    break;

                case GamePhase.Bidding:
                    SetHudStatus("Bidding Phase — Place your bid");
                    trickArea.ClearAll();
                    // New bidding round: clear every seat's previous bid or PASS.
                    ResetAllPassStates();
                    break;

                case GamePhase.TrumpSelection:
                    // Bid and Pass bubbles stay up until trump is actually set.
                    EndAllBiddingDimming();
                    if (_gm != null)
                    {
                        PlayerSeat bidWinner = _gm.GetBidWinner();
                        if (bidWinner == GameManager.HumanSeat)
                            SetHudStatus("★ YOU WON THE BID! Choose your Trump card ★");
                        else
                        {
                            string bidderName = bidWinner == PlayerSeat.North ? "Partner (North)" : bidWinner.ToString();
                            SetHudStatus($"{bidderName} won the bid and is setting the Trump card...");
                        }
                    }
                    biddingPanel.Hide();
                    break;

                case GamePhase.Playing:
                    // Safety-net: undim pass avatars, but leave bid/PASS bubbles up
                    // until the next bidding round.
                    EndAllBiddingDimming();
                    SetHudStatus("YOUR TURN — Select a card to play");
                    if (biddingPanel != null) biddingPanel.Hide();
                    if (trumpSelectionModal != null) trumpSelectionModal.Hide();
                    RefreshHumanCards();
                    RefreshAICardCounts();
                    if (trumpCardSlot != null) trumpCardSlot.UpdateDisplay(_gm);
                    break;

                case GamePhase.RoundOver:
                    biddingPanel.Hide();
                    break;
            }
        }

        private void HandleHumanHandDealt(Hand hand)
        {
            _animateNextDeal = true;
            RefreshHumanCards();
            RefreshAICardCounts(animate: true);
        }

        private void HandleCurrentPlayerChanged(PlayerSeat seat)
        {
            UpdateTurnHighlights(seat);

            if (_gm.CurrentPhase == GamePhase.Bidding)
            {
                // When a player is bidding, change only that player's Avatar Image color to 255, 255, 255, 255.
                // Their partner's Avatar Image must also remain 255, 255, 255, 255.
                // A seat that has already passed stays dimmed for the rest of the
                // bidding phase — skip it so its inactive look isn't overwritten.
                PlayerSeatUI activeSeatUI = GetSeatUI(seat);
                if (activeSeatUI != null && !activeSeatUI.IsPassed && activeSeatUI.AvatarBg != null)
                    activeSeatUI.AvatarBg.color = Color.white;

                PlayerSeat partnerSeat = GameRules.GetPartner(seat);
                PlayerSeatUI partnerSeatUI = GetSeatUI(partnerSeat);
                if (partnerSeatUI != null && !partnerSeatUI.IsPassed && partnerSeatUI.AvatarBg != null)
                    partnerSeatUI.AvatarBg.color = Color.white;

                if (seat == GameManager.HumanSeat)
                {
                    SetHudStatus("YOUR TURN TO BID!");
                    biddingPanel.Show(_gm.GetCurrentBid(), _gm.GetCurrentHighBidder(), _gm.GetMinimumBid());
                }
                else
                {
                    biddingPanel.Hide();
                    string name = seat == PlayerSeat.North ? "Partner" : seat.ToString();
                    SetHudStatus($"Waiting for {name} to bid...");
                }
            }
            else if (_gm.CurrentPhase == GamePhase.Playing)
            {
                if (seat == GameManager.HumanSeat)
                {
                    if (_gm.CanHumanRevealTrump())
                        SetHudStatus("You have no cards of the led suit — tap REVEAL TRUMP to play trump, or discard.");
                    else
                        SetHudStatus("YOUR TURN — Select a card to play");
                }
                else
                {
                    string name = seat == PlayerSeat.North ? "Partner" : seat.ToString();
                    SetHudStatus($"{name}'s turn to play...");
                }
            }

            // Immediately refresh playability whenever active player changes
            RefreshHumanCards();
            UpdateHud();
            if (trumpCardSlot != null) trumpCardSlot.UpdateDisplay(_gm);
        }

        private void HandleBiddingAction(PlayerSeat seat, int? bid)
        {
            PlayerSeatUI seatUI = GetSeatUI(seat);
            if (seatUI == null) return;

            if (bid.HasValue)
                seatUI.ShowBiddingAction(bid.Value.ToString(), false, false);
            else
                seatUI.ShowBiddingAction("Pass", true, true);

            UpdateHud();
        }

        /// <summary>
        /// Fired by GameManager right before an AI player's 2-second bidding delay.
        /// </summary>
        private void HandleAIBiddingThinking(PlayerSeat seat)
        {
            if (seat == GameManager.HumanSeat) return;
            PlayerSeatUI seatUI = GetSeatUI(seat);
            if (seatUI != null) seatUI.ShowThinking();
        }

        private void HandleTrumpChosen()
        {
            ResetAllPassStates();
        }

        private void HandleCardPlayed(PlayerSeat seat, Card card)
        {
            PlayerSeatUI seatUI = GetSeatUI(seat);
            Vector3 origin = seatUI != null ? seatUI.GetPlayOriginWorld(card) : transform.position;
            Quaternion originRot = seatUI != null ? seatUI.GetPlayOriginRotation(card) : Quaternion.identity;

            if (trickArea != null && _gm.GetCurrentTrick() != null)
                trickArea.DisplayTrick(_gm.GetCurrentTrick(), seat, origin, originRot);

            if (seat == GameManager.HumanSeat)
                RefreshHumanCards();
            else
                RefreshAICardCounts();

            UpdateHud();
        }

        private void HandleTrickWon(PlayerSeat winner, int points)
        {
            if (trickArea != null)
                trickArea.ShowTrickWinner(winner, points);

            PlayerSeatUI seatUI = GetSeatUI(winner);
            if (seatUI != null) seatUI.ShowActionBubble($"Won +{points} pts!");

            UpdateHud();
        }

        private void HandleTrumpRevealed(Suit trump)
        {
            string sym = CardVisualTheme.GetSuitSymbol(trump);
            string name = CardVisualTheme.GetSuitName(trump);
            SetHudStatus($"★ TRUMP REVEALED: {sym} {name.ToUpper()}! ★");
            UpdateHud();
            if (trumpCardSlot != null) trumpCardSlot.UpdateDisplay(_gm);
            RefreshHumanCards();
        }

        private void HandleHumanTrumpSelectionRequired(Hand hand)
        {
            if (trumpSelectionModal == null) BuildUIIfMissing();
            if (trumpSelectionModal != null)
            {
                trumpSelectionModal.Show(_gm.GetCurrentBid(), hand);
            }
            SetHudStatus("★ YOU WON THE BID! Choose your Trump card ★");
        }

        private void HandleSinglePlayEligible()
        {
            if (decisionPanel == null) BuildUIIfMissing();
            if (decisionPanel != null)
            {
                SetHudStatus("DO YOU WANT TO PLAY SINGLE?");
                decisionPanel.ShowSinglePlayDecision(
                    _gm.GetBidWinner().ToString(),
                    _gm.GetFinalBid(),
                    _gm.GetPublicTrumpTypeLabel(),
                    onConfirm: () => _gm.AcceptSinglePlay(),
                    onReject: () => _gm.RejectSinglePlay()
                );
            }
        }

        private void HandleDoubleDecisionRequired()
        {
            if (decisionPanel == null) BuildUIIfMissing();
            if (decisionPanel != null)
            {
                SetHudStatus("DO YOU WANT TO SET DOUBLE?");
                decisionPanel.ShowDoubleDecision(
                    _gm.GetBidWinner().ToString(),
                    _gm.GetFinalBid(),
                    _gm.GetPublicTrumpTypeLabel(),
                    onConfirm: () => _gm.AcceptHumanDouble(),
                    onReject: () => _gm.RejectHumanDouble()
                );
            }
        }

        private void HandleReDoubleDecisionRequired(PlayerSeat doubler)
        {
            if (decisionPanel == null) BuildUIIfMissing();
            if (decisionPanel != null)
            {
                SetHudStatus("DO YOU WANT TO RE-DOUBLE?");
                decisionPanel.ShowReDoubleDecision(
                    _gm.GetBidWinner().ToString(),
                    _gm.GetFinalBid(),
                    _gm.GetPublicTrumpTypeLabel(),
                    onConfirm: () => _gm.AcceptHumanReDouble(),
                    onReject: () => _gm.RejectHumanReDouble()
                );
            }
        }

        private void HandleRoundScored(bool biddingTeamWon)
        {
            if (roundEndModal != null) roundEndModal.ShowRoundOver(_gm.ScoreManager, biddingTeamWon);
        }

        private void HandleMarriageDeclared(int declaringTeam, int newTarget)
        {
            string teamName = declaringTeam == 0 ? "You & Partner" : "Opponents";
            bool loweredTarget = declaringTeam == _gm.ScoreManager.BiddingTeam;
            string verb = loweredTarget ? "lowered" : "raised";
            SetHudStatus($"♥♠ MARRIAGE! {teamName} declared — target {verb} to {newTarget} ♠♥");
            UpdateHud();
        }

        private void HandleGameOver(int winningTeam)
        {
            if (roundEndModal != null) roundEndModal.ShowGameOver(_gm.ScoreManager, winningTeam);
        }

        // ════════════════════════════════════════════════════════════════════════
        // DISPLAY REFRESH
        // ════════════════════════════════════════════════════════════════════════

        public void RefreshAllDisplay()
        {
            if (_gm == null) return;

            UpdateHud();
            UpdateTurnHighlights(_gm.CurrentPlayer);
            UpdateDealerCoin();
            if (_gm.IsSinglePlayActive)
            {
                if (northSeat != null) northSeat.SetDisabledPartner(_gm.DisabledPartnerSeat == PlayerSeat.North);
                if (southSeat != null) southSeat.SetDisabledPartner(_gm.DisabledPartnerSeat == PlayerSeat.South);
                if (eastSeat != null) eastSeat.SetDisabledPartner(_gm.DisabledPartnerSeat == PlayerSeat.East);
                if (westSeat != null) westSeat.SetDisabledPartner(_gm.DisabledPartnerSeat == PlayerSeat.West);
            }
            else
            {
                if (northSeat != null && northSeat.IsDisabledPartner) northSeat.SetDisabledPartner(false);
                if (southSeat != null && southSeat.IsDisabledPartner) southSeat.SetDisabledPartner(false);
                if (eastSeat != null && eastSeat.IsDisabledPartner) eastSeat.SetDisabledPartner(false);
                if (westSeat != null && westSeat.IsDisabledPartner) westSeat.SetDisabledPartner(false);
            }
            RefreshHumanCards();
            RefreshAICardCounts();
            if (southSeat != null) southSeat.SetSkipButtonActive(_gm.IsHumanSkipAvailable());
            if (trumpCardSlot != null) trumpCardSlot.UpdateDisplay(_gm);

            int[] gamePts = _gm.GetGamePoints();
            int[] roundPts = _gm.GetRoundPoints();
            if (yourTeamPointCard != null) yourTeamPointCard.UpdateDisplay(gamePts[0], roundPts[0]);
            if (opponentPointCard != null) opponentPointCard.UpdateDisplay(gamePts[1], roundPts[1]);

            if (_gm.CurrentPhase == GamePhase.Playing)
            {
                Trick currentTrick = _gm.GetCurrentTrick();
                if (currentTrick != null && currentTrick.PlayCount > 0)
                    trickArea.DisplayTrick(currentTrick);
            }
            else if (_gm.CurrentPhase == GamePhase.Bidding && _gm.CurrentPlayer == GameManager.HumanSeat)
            {
                biddingPanel.Show(_gm.GetCurrentBid(), _gm.GetCurrentHighBidder(), _gm.GetMinimumBid());
            }
        }

        private void RefreshHumanCards()
        {
            if (_gm == null || southSeat == null) return;
            Hand hand = _gm.HumanHand;
            List<Card> validPlays = _gm.GetHumanValidPlays();
            bool animate = _animateNextDeal;
            _animateNextDeal = false;
            southSeat.RenderHumanHand(hand, validPlays, OnHumanCardSelected, animate);
        }

        private void RefreshAICardCounts(bool animate = false)
        {
            if (_gm == null) return;
            // All seats use horizontal: true — the CardContainer rotation on each
            // seat handles the screen-space orientation (vertical for East/West).
            if (northSeat != null) northSeat.RenderAICardCount(_gm.GetHand(PlayerSeat.North).Count, horizontal: true, animate: animate);
            if (westSeat != null) westSeat.RenderAICardCount(_gm.GetHand(PlayerSeat.West).Count, horizontal: true, animate: animate);
            if (eastSeat != null) eastSeat.RenderAICardCount(_gm.GetHand(PlayerSeat.East).Count, horizontal: true, animate: animate);
        }

        private void UpdateTurnHighlights(PlayerSeat current)
        {
            if (southSeat != null) southSeat.SetActiveTurn(current == PlayerSeat.South);
            if (northSeat != null) northSeat.SetActiveTurn(current == PlayerSeat.North);
            if (westSeat != null) westSeat.SetActiveTurn(current == PlayerSeat.West);
            if (eastSeat != null) eastSeat.SetActiveTurn(current == PlayerSeat.East);
        }

        /// <summary>
        /// Shows the Dealer Coin on the current dealer's seat and hides it on all others.
        /// Called from RefreshAllDisplay() so it stays in sync whenever state changes.
        /// </summary>
        private void UpdateDealerCoin()
        {
            if (_gm == null) return;
            PlayerSeat dealer = _gm.Dealer;
            if (southSeat != null) southSeat.SetDealerCoin(dealer == PlayerSeat.South);
            if (northSeat != null) northSeat.SetDealerCoin(dealer == PlayerSeat.North);
            if (westSeat != null) westSeat.SetDealerCoin(dealer == PlayerSeat.West);
            if (eastSeat != null) eastSeat.SetDealerCoin(dealer == PlayerSeat.East);
        }

        /// <summary>
        /// Clears every seat's bid or PASS bubble. Called when a new bidding round begins.
        /// </summary>
        private void ResetAllPassStates()
        {
            if (southSeat != null) southSeat.ResetPassState();
            if (northSeat != null) northSeat.ResetPassState();
            if (westSeat != null) westSeat.ResetPassState();
            if (eastSeat != null) eastSeat.ResetPassState();
        }

        /// <summary>Restores pass-dimmed avatars without removing bid or PASS bubbles.</summary>
        private void EndAllBiddingDimming()
        {
            if (southSeat != null) southSeat.EndBiddingDimming();
            if (northSeat != null) northSeat.EndBiddingDimming();
            if (westSeat != null) westSeat.EndBiddingDimming();
            if (eastSeat != null) eastSeat.EndBiddingDimming();
        }

        private void OnHumanCardSelected(Card card)
        {
            if (_gm == null) return;

            if (_gm.CurrentPhase != GamePhase.Playing)
            {
                SetHudStatus("⚠ Bidding in progress — cards cannot be played yet!");
                if (southSeat != null) southSeat.ShakeCard(card);
                return;
            }

            if (_gm.CurrentPlayer != GameManager.HumanSeat)
            {
                SetHudStatus("Wait for your turn to play!");
                if (southSeat != null) southSeat.ShakeCard(card);
                return;
            }

            List<Card> validPlays = _gm.GetHumanValidPlays();
            if (!validPlays.Contains(card))
            {
                if (southSeat != null) southSeat.ShakeCard(card);
                Trick currentTrick = _gm.GetCurrentTrick();
                if (currentTrick != null && currentTrick.LedSuit.HasValue)
                {
                    string suitName = CardVisualTheme.GetSuitName(currentTrick.LedSuit.Value);
                    string suitSym = CardVisualTheme.GetSuitSymbol(currentTrick.LedSuit.Value);
                    SetHudStatus($"⚠ Must follow suit: {suitSym} {suitName}!");
                }
                else
                {
                    SetHudStatus("⚠ Invalid card play!");
                }
                return;
            }

            Debug.Log($"[29 GameTableUI] Playing {card}");
            _gm.PlayHumanCard(card);
        }

        private void OnSkipClicked()
        {
            if (_gm == null) return;

            bool skipped = _gm.SkipRemainingPlay();
            if (!skipped)
            {
                // Guards against a stale/late click racing a state change (e.g. a
                // trick started mid-play right as the button was tapped) — the
                // button will hide itself on the next RefreshAllDisplay anyway.
                Debug.Log("[29 GameTableUI] Skip clicked but not available anymore.");
                return;
            }

            Debug.Log("[29 GameTableUI] Round skipped early.");
        }

        private PlayerSeatUI GetSeatUI(PlayerSeat seat)
        {
            return seat switch
            {
                PlayerSeat.South => southSeat,
                PlayerSeat.North => northSeat,
                PlayerSeat.West => westSeat,
                PlayerSeat.East => eastSeat,
                _ => null
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        // LANDSCAPE LAYOUT ENFORCEMENT (1920×1080)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Explicitly positions all board components for the landscape 1920×1080 canvas
        /// using Board.jpg as the background.
        /// </summary>
        public void ApplyLandscapeLayout()
        {
            // 1. Canvas Scaler
            if (canvasScaler == null) canvasScaler = GetComponent<CanvasScaler>();
            if (canvasScaler != null)
            {
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(1920, 1080);
                canvasScaler.matchWidthOrHeight = 0.5f;
            }

            // 2. Full-Screen Board Background
            if (tableBackground == null)
            {
                Transform bgT = transform.Find("Board") ?? transform.Find("TableFelt_BG");
                if (bgT != null) tableBackground = bgT.GetComponent<Image>();
            }

            if (tableBackground != null)
            {
                tableBackground.sprite = CardVisualTheme.BoardBackground;
                tableBackground.color = Color.white;
                tableBackground.type = Image.Type.Simple;
                tableBackground.preserveAspect = false;
                tableBackground.raycastTarget = false;

                RectTransform bgrt = tableBackground.GetComponent<RectTransform>();
                if (bgrt != null)
                {
                    bgrt.anchorMin = Vector2.zero;
                    bgrt.anchorMax = Vector2.one;
                    bgrt.offsetMin = Vector2.zero;
                    bgrt.offsetMax = Vector2.zero;
                }
                tableBackground.transform.SetAsFirstSibling();
            }

            // 3. Score HUD — intentionally NOT touched here. ScoreHUDUI owns its
            // own RectTransform (default set once in EnsureComponents(), or
            // whatever you've positioned it to by hand). This block used to
            // force it back to a full-width top stretch on every Awake()/Start(),
            // which is why manual repositioning kept reverting on Play.

            // 4. North (Partner) — intentionally NOT touched here. northSeat owns its
            // own RectTransform position/anchor/size (whatever you've set by hand in
            // the Editor, or the prefab default), same as the Score HUD above. This
            // block used to force anchorMin/anchorMax/pivot/anchoredPosition/sizeDelta
            // — on both the seat panel and its CardContainer (and the avatar/name
            // position via SetLayoutPositions) — back to a hardcoded layout on every
            // Awake()/Start(), which is why manual repositioning/resizing kept
            // reverting on Play.

            // 5. South (Human) — not touched here either; see note above.

            // 6. West (Opponent) — not touched here either; see note above.

            // 7. East (Opponent) — not touched here either; see note above.

            // 8. Trick Area — intentionally NOT touched here. TrickAreaUI owns its own
            // RectTransform (default set once in EnsureComponents(), or whatever
            // you've positioned/sized it to by hand), same as the elements above.

            // 9. Trump Card Slot — not touched here either; see note above.

            // 10. Opponent Point Card Slot — not touched here either; see note above.
            //     EnsureComponents() is still called so its internal children exist.
            if (opponentPointCard != null)
                opponentPointCard.EnsureComponents();

            // 11. Your Team Point Card Slot — not touched here either; see note above.
            //     EnsureComponents() is still called so its internal children exist.
            if (yourTeamPointCard != null)
                yourTeamPointCard.EnsureComponents();

            // 12. Bidding Panel — not touched here either; see note above. Still kept
            // on top of the render order so it shows above other board elements.
            if (biddingPanel != null)
                biddingPanel.transform.SetAsLastSibling();

            if (trumpSelectionModal != null)
                trumpSelectionModal.transform.SetAsLastSibling();
            if (roundEndModal != null)
                roundEndModal.transform.SetAsLastSibling();
            if (decisionPanel != null)
                decisionPanel.transform.SetAsLastSibling();
        }

        // ════════════════════════════════════════════════════════════════════════
        // AUTO-BUILD COMPLETE UI HIERARCHY
        // ════════════════════════════════════════════════════════════════════════

        public void BuildUIIfMissing()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
            }

            canvasScaler = GetComponent<CanvasScaler>();
            if (canvasScaler == null)
                canvasScaler = gameObject.AddComponent<CanvasScaler>();

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            EnsureInputSystemEventSystem();

            // Background
            if (tableBackground == null)
            {
                Transform bgT = transform.Find("Board") ?? transform.Find("TableFelt_BG");
                GameObject bgObj = bgT != null ? bgT.gameObject : new GameObject("Board");
                if (bgT == null) bgObj.transform.SetParent(transform, false);
                tableBackground = bgObj.GetComponent<Image>() ?? bgObj.AddComponent<Image>();
            }

            // Score HUD — bind the existing scene object only.
            if (scoreHUD == null)
            {
                Transform sT = transform.Find("ScoreHUD");
                if (sT != null)
                    scoreHUD = sT.GetComponent<ScoreHUDUI>() ?? sT.gameObject.AddComponent<ScoreHUDUI>();
            }

            // Trick Area
            if (trickArea == null)
            {
                Transform tT = transform.Find("TrickArea");
                GameObject trickObj = tT != null ? tT.gameObject : new GameObject("TrickArea");
                if (tT == null) trickObj.transform.SetParent(transform, false);
                trickArea = trickObj.GetComponent<TrickAreaUI>() ?? trickObj.AddComponent<TrickAreaUI>();
                trickArea.EnsureComponents();
            }

            // Player Seats
            if (northSeat == null)
            {
                Transform nT = transform.Find("Player_North");
                GameObject nObj = nT != null ? nT.gameObject : new GameObject("Player_North");
                if (nT == null) nObj.transform.SetParent(transform, false);
                northSeat = nObj.GetComponent<PlayerSeatUI>() ?? nObj.AddComponent<PlayerSeatUI>();
                northSeat.Seat = PlayerSeat.North;
                northSeat.EnsureComponents();
            }

            if (westSeat == null)
            {
                Transform wT = transform.Find("Player_West");
                GameObject wObj = wT != null ? wT.gameObject : new GameObject("Player_West");
                if (wT == null) wObj.transform.SetParent(transform, false);
                westSeat = wObj.GetComponent<PlayerSeatUI>() ?? wObj.AddComponent<PlayerSeatUI>();
                westSeat.Seat = PlayerSeat.West;
                westSeat.EnsureComponents();
            }

            if (eastSeat == null)
            {
                Transform eT = transform.Find("Player_East");
                GameObject eObj = eT != null ? eT.gameObject : new GameObject("Player_East");
                if (eT == null) eObj.transform.SetParent(transform, false);
                eastSeat = eObj.GetComponent<PlayerSeatUI>() ?? eObj.AddComponent<PlayerSeatUI>();
                eastSeat.Seat = PlayerSeat.East;
                eastSeat.EnsureComponents();
            }

            if (southSeat == null)
            {
                Transform sT = transform.Find("Player_South");
                GameObject sObj = sT != null ? sT.gameObject : new GameObject("Player_South");
                if (sT == null) sObj.transform.SetParent(transform, false);
                southSeat = sObj.GetComponent<PlayerSeatUI>() ?? sObj.AddComponent<PlayerSeatUI>();
                southSeat.Seat = PlayerSeat.South;
                southSeat.EnsureComponents();
            }

            // Bidding Panel
            if (biddingPanel == null)
            {
                Transform bT = transform.Find("BiddingPanel");
                GameObject bObj = bT != null ? bT.gameObject : new GameObject("BiddingPanel");
                if (bT == null) bObj.transform.SetParent(transform, false);
                biddingPanel = bObj.GetComponent<BiddingPanelUI>() ?? bObj.AddComponent<BiddingPanelUI>();
                biddingPanel.EnsureComponents();
                biddingPanel.Hide();
            }

            // Round End Modal
            if (roundEndModal == null)
            {
                Transform rT = transform.Find("RoundEndModal");
                GameObject rObj = rT != null ? rT.gameObject : new GameObject("RoundEndModal");
                if (rT == null) rObj.transform.SetParent(transform, false);
                roundEndModal = rObj.GetComponent<RoundEndModalUI>() ?? rObj.AddComponent<RoundEndModalUI>();
                roundEndModal.EnsureComponents();
                roundEndModal.Hide();
            }

            // Trump Card Slot
            if (trumpCardSlot == null)
            {
                Transform existingSlot = transform.Find("TrumpCardSlot");
                GameObject tSlotObj = existingSlot != null ? existingSlot.gameObject : new GameObject("TrumpCardSlot");
                if (existingSlot == null) tSlotObj.transform.SetParent(transform, false);
                trumpCardSlot = tSlotObj.GetComponent<TrumpCardSlotUI>() ?? tSlotObj.AddComponent<TrumpCardSlotUI>();
                trumpCardSlot.EnsureComponents();
                tSlotObj.SetActive(false);
            }

            // Trump Selection Modal
            if (trumpSelectionModal == null)
            {
                Transform existingModal = transform.Find("TrumpSelectionModal");
                GameObject tModalObj = existingModal != null ? existingModal.gameObject : new GameObject("TrumpSelectionModal");
                if (existingModal == null) tModalObj.transform.SetParent(transform, false);
                trumpSelectionModal = tModalObj.GetComponent<TrumpSelectionModalUI>() ?? tModalObj.AddComponent<TrumpSelectionModalUI>();
                trumpSelectionModal.EnsureComponents();
                trumpSelectionModal.Hide();
            }

            // Safety net: if trumpSelectionModal was wired up manually in the Inspector
            // to an object living outside this Canvas's hierarchy, it will silently
            // never render (UI graphics require a Canvas ancestor). Force it under us.
            if (trumpSelectionModal != null && trumpSelectionModal.transform.parent != transform)
            {
                Debug.LogWarning("[29 GameTableUI] TrumpSelectionModal was parented outside the " +
                    "GameTable Canvas — reparenting it now so it can actually render.");
                trumpSelectionModal.transform.SetParent(transform, false);
            }

            // Opponent Point Card (Left table board)
            if (opponentPointCard == null)
            {
                Transform existing = transform.Find("PointCard_Opponent");
                if (existing != null)
                {
                    opponentPointCard = existing.GetComponent<PointCardSlotUI>();
                    if (opponentPointCard != null)
                    {
                        opponentPointCard.TeamIndex = 1;
                        opponentPointCard.TeamTitle = "Opponent";
                    }
                }
            }

            // Your Team Point Card (Right table board)
            if (yourTeamPointCard == null)
            {
                Transform existing = transform.Find("PointCard_YourTeam");
                if (existing != null)
                {
                    yourTeamPointCard = existing.GetComponent<PointCardSlotUI>();
                    if (yourTeamPointCard != null)
                    {
                        yourTeamPointCard.TeamIndex = 0;
                        yourTeamPointCard.TeamTitle = "Your Team";
                    }
                }
            }

            // Decision Panel (Single, Double, Re-Double)
            if (decisionPanel == null)
            {
                Transform dpT = transform.Find("DecisionPanel");
                GameObject dpObj = dpT != null ? dpT.gameObject : new GameObject("DecisionPanel");
                if (dpT == null) dpObj.transform.SetParent(transform, false);
                decisionPanel = dpObj.GetComponent<DecisionPanelUI>() ?? dpObj.AddComponent<DecisionPanelUI>();
                decisionPanel.EnsureComponents();
                decisionPanel.Hide();
            }

            if (rulesPanel == null)
            {
                Transform rpT = transform.Find("DescriptionPanel");
                if (rpT != null)
                {
                    rulesPanel = rpT.GetComponent<RulesPanelUI>() ?? rpT.gameObject.AddComponent<RulesPanelUI>();
                    rulesPanel.EnsureWired();
                    rulesPanel.Hide();
                }
            }

            ApplyLandscapeLayout();
        }

        private void UpdateHud()
        {
            if (scoreHUD != null) scoreHUD.UpdateHUD(_gm);
        }

        private void SetHudStatus(string message)
        {
            if (scoreHUD != null) scoreHUD.SetStatusMessage(message);
        }

        private void EnsureInputSystemEventSystem()
        {
            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            var legacyModule = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (legacyModule != null)
            {
                if (Application.isPlaying) Destroy(legacyModule);
                else DestroyImmediate(legacyModule);
            }

            var inputModule = es.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
                inputModule = es.gameObject.AddComponent<InputSystemUIInputModule>();

            if (inputModule != null)
            {
                inputModule.enabled = false;
                inputModule.AssignDefaultActions();
                inputModule.enabled = true;
            }
        }
    }
}