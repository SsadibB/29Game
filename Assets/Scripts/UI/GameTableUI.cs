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
    /// Board background uses board.png from Resources.
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
            _gm.OnCardPlayed += HandleCardPlayed;
            _gm.OnTrickWon += HandleTrickWon;
            _gm.OnTrumpRevealed += HandleTrumpRevealed;
            _gm.OnRoundScored += HandleRoundScored;
            _gm.OnGameOver += HandleGameOver;
            _gm.OnHumanTrumpSelectionRequired += HandleHumanTrumpSelectionRequired;
            _gm.OnStateChanged += RefreshAllDisplay;

            RefreshAllDisplay();
        }

        private void OnDestroy()
        {
            if (_gm == null) return;
            _gm.OnPhaseChanged -= HandlePhaseChanged;
            _gm.OnHumanHandDealt -= HandleHumanHandDealt;
            _gm.OnCurrentPlayerChanged -= HandleCurrentPlayerChanged;
            _gm.OnBiddingAction -= HandleBiddingAction;
            _gm.OnCardPlayed -= HandleCardPlayed;
            _gm.OnTrickWon -= HandleTrickWon;
            _gm.OnTrumpRevealed -= HandleTrumpRevealed;
            _gm.OnRoundScored -= HandleRoundScored;
            _gm.OnGameOver -= HandleGameOver;
            _gm.OnHumanTrumpSelectionRequired -= HandleHumanTrumpSelectionRequired;
            _gm.OnStateChanged -= RefreshAllDisplay;
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
                    scoreHUD.SetStatusMessage("Dealing cards from the deck...");
                    trickArea.ClearAll();
                    biddingPanel.Hide();
                    break;

                case GamePhase.Bidding:
                    scoreHUD.SetStatusMessage("Bidding Phase — Place your bid");
                    trickArea.ClearAll();
                    break;

                case GamePhase.TrumpSelection:
                    if (_gm != null)
                    {
                        PlayerSeat bidWinner = _gm.GetBidWinner();
                        if (bidWinner == GameManager.HumanSeat)
                            scoreHUD.SetStatusMessage("★ YOU WON THE BID! Choose your Trump card ★");
                        else
                        {
                            string bidderName = bidWinner == PlayerSeat.North ? "Partner (North)" : bidWinner.ToString();
                            scoreHUD.SetStatusMessage($"{bidderName} won the bid and is setting the Trump card...");
                        }
                    }
                    biddingPanel.Hide();
                    break;

                case GamePhase.Playing:
                    scoreHUD.SetStatusMessage("YOUR TURN — Select a card to play");
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
                if (seat == GameManager.HumanSeat)
                {
                    scoreHUD.SetStatusMessage("YOUR TURN TO BID!");
                    biddingPanel.Show(_gm.GetCurrentBid(), _gm.GetCurrentHighBidder(), _gm.GetMinimumBid());
                }
                else
                {
                    biddingPanel.Hide();
                    string name = seat == PlayerSeat.North ? "Partner" : seat.ToString();
                    scoreHUD.SetStatusMessage($"Waiting for {name} to bid...");
                }
            }
            else if (_gm.CurrentPhase == GamePhase.Playing)
            {
                if (seat == GameManager.HumanSeat)
                {
                    if (_gm.CanHumanRevealTrump())
                        scoreHUD.SetStatusMessage("You have no cards of the led suit — tap REVEAL TRUMP to play trump, or discard.");
                    else
                        scoreHUD.SetStatusMessage("YOUR TURN — Select a card to play");
                }
                else
                {
                    string name = seat == PlayerSeat.North ? "Partner" : seat.ToString();
                    scoreHUD.SetStatusMessage($"{name}'s turn to play...");
                }
            }

            // Immediately refresh playability whenever active player changes
            RefreshHumanCards();
            if (scoreHUD != null) scoreHUD.UpdateHUD(_gm);
            if (trumpCardSlot != null) trumpCardSlot.UpdateDisplay(_gm);
        }

        private void HandleBiddingAction(PlayerSeat seat, int? bid)
        {
            PlayerSeatUI seatUI = GetSeatUI(seat);
            string text = bid.HasValue ? $"Bid {bid.Value}!" : "Pass";
            if (seatUI != null) seatUI.ShowActionBubble(text);
            scoreHUD.UpdateHUD(_gm);
        }

        private void HandleCardPlayed(PlayerSeat seat, Card card)
        {
            PlayerSeatUI seatUI = GetSeatUI(seat);
            Vector3 origin = seatUI != null ? seatUI.GetPlayOriginWorld(card) : transform.position;

            if (trickArea != null && _gm.GetCurrentTrick() != null)
                trickArea.DisplayTrick(_gm.GetCurrentTrick(), seat, origin);

            if (seat == GameManager.HumanSeat)
                RefreshHumanCards();
            else
                RefreshAICardCounts();

            scoreHUD.UpdateHUD(_gm);
        }

        private void HandleTrickWon(PlayerSeat winner, int points)
        {
            if (trickArea != null)
                trickArea.ShowTrickWinner(winner, points);

            PlayerSeatUI seatUI = GetSeatUI(winner);
            if (seatUI != null) seatUI.ShowActionBubble($"Won +{points} pts!");

            scoreHUD.UpdateHUD(_gm);
        }

        private void HandleTrumpRevealed(Suit trump)
        {
            string sym = CardVisualTheme.GetSuitSymbol(trump);
            string name = CardVisualTheme.GetSuitName(trump);
            scoreHUD.SetStatusMessage($"★ TRUMP REVEALED: {sym} {name.ToUpper()}! ★");
            scoreHUD.UpdateHUD(_gm);
            if (trumpCardSlot != null) trumpCardSlot.UpdateDisplay(_gm);
            RefreshHumanCards();
        }

        private void HandleHumanTrumpSelectionRequired(Hand hand)
        {
            if (trumpSelectionModal == null) BuildUIIfMissing();
            if (trumpSelectionModal != null)
                trumpSelectionModal.Show(_gm.GetCurrentBid(), hand);
            scoreHUD.SetStatusMessage("★ YOU WON THE BID! Choose your Trump card ★");
        }

        private void HandleRoundScored(bool biddingTeamWon)
        {
            if (roundEndModal != null) roundEndModal.ShowRoundOver(_gm.ScoreManager, biddingTeamWon);
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

            scoreHUD.UpdateHUD(_gm);
            UpdateTurnHighlights(_gm.CurrentPlayer);
            RefreshHumanCards();
            RefreshAICardCounts();
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

        private void OnHumanCardSelected(Card card)
        {
            if (_gm == null) return;

            if (_gm.CurrentPhase != GamePhase.Playing)
            {
                scoreHUD.SetStatusMessage("⚠ Bidding in progress — cards cannot be played yet!");
                if (southSeat != null) southSeat.ShakeCard(card);
                return;
            }

            if (_gm.CurrentPlayer != GameManager.HumanSeat)
            {
                scoreHUD.SetStatusMessage("Wait for your turn to play!");
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
                    scoreHUD.SetStatusMessage($"⚠ Must follow suit: {suitSym} {suitName}!");
                }
                else
                {
                    scoreHUD.SetStatusMessage("⚠ Invalid card play!");
                }
                return;
            }

            Debug.Log($"[29 GameTableUI] Playing {card}");
            _gm.PlayHumanCard(card);
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
        /// using board.png as the background.
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
                Transform bgT = transform.Find("Board_BG") ?? transform.Find("TableFelt_BG");
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
                Transform bgT = transform.Find("Board_BG") ?? transform.Find("TableFelt_BG");
                GameObject bgObj = bgT != null ? bgT.gameObject : new GameObject("Board_BG");
                if (bgT == null) bgObj.transform.SetParent(transform, false);
                tableBackground = bgObj.GetComponent<Image>() ?? bgObj.AddComponent<Image>();
            }

            // Score HUD
            if (scoreHUD == null)
            {
                Transform sT = transform.Find("ScoreHUD");
                GameObject hudObj = sT != null ? sT.gameObject : new GameObject("ScoreHUD");
                if (sT == null) hudObj.transform.SetParent(transform, false);
                scoreHUD = hudObj.GetComponent<ScoreHUDUI>() ?? hudObj.AddComponent<ScoreHUDUI>();
                scoreHUD.EnsureComponents();
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
                GameObject obj = existing != null ? existing.gameObject : new GameObject("PointCard_Opponent");
                if (existing == null) obj.transform.SetParent(transform, false);
                opponentPointCard = obj.GetComponent<PointCardSlotUI>() ?? obj.AddComponent<PointCardSlotUI>();
                opponentPointCard.TeamIndex = 1;
                opponentPointCard.TeamTitle = "Opponent";
                opponentPointCard.EnsureComponents();
            }

            // Your Team Point Card (Right table board)
            if (yourTeamPointCard == null)
            {
                Transform existing = transform.Find("PointCard_YourTeam");
                GameObject obj = existing != null ? existing.gameObject : new GameObject("PointCard_YourTeam");
                if (existing == null) obj.transform.SetParent(transform, false);
                yourTeamPointCard = obj.GetComponent<PointCardSlotUI>() ?? obj.AddComponent<PointCardSlotUI>();
                yourTeamPointCard.TeamIndex = 0;
                yourTeamPointCard.TeamTitle = "Your Team";
                yourTeamPointCard.EnsureComponents();
            }

            ApplyLandscapeLayout();
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