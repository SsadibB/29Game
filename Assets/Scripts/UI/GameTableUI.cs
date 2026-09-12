using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace Game29
{
    /// <summary>
    /// Master UI Manager for the 29 Card Game.
    /// Orchestrates the entire Canvas visual hierarchy:
    ///   • Felt table background
    ///   • 4 Player seat layouts (South/Human, North/Partner, East/Opponent, West/Opponent)
    ///   • Center Trick area
    ///   • Bidding interactive modal
    ///   • Scoreboard HUD & status banners
    ///   • Round-end and Game-over popups
    ///
    /// Automatically builds the complete UI at runtime if not present in the scene.
    /// </summary>
    public class GameTableUI : MonoBehaviour
    {
        // ── Component References ────────────────────────────────────────────────
        [Header("Root & Layout")]
        [SerializeField] private Canvas         canvas;
        [SerializeField] private CanvasScaler   canvasScaler;
        [SerializeField] private Image          tableBackground;

        [Header("Player Seats")]
        [SerializeField] private PlayerSeatUI   southSeat;
        [SerializeField] private PlayerSeatUI   northSeat;
        [SerializeField] private PlayerSeatUI   westSeat;
        [SerializeField] private PlayerSeatUI   eastSeat;

        [Header("Game Areas")]
        [SerializeField] private TrickAreaUI          trickArea;
        [SerializeField] private BiddingPanelUI       biddingPanel;
        [SerializeField] private ScoreHUDUI           scoreHUD;
        [SerializeField] private RoundEndModalUI      roundEndModal;
        [SerializeField] private TrumpSelectionModalUI trumpSelectionModal;
        [SerializeField] private TrumpCardSlotUI      trumpCardSlot;

        private GameManager _gm;
        private bool _isInitialized;
        private bool _animateNextDeal;  // true only when cards are freshly dealt

        // ════════════════════════════════════════════════════════════════════════
        // INITIALISATION
        // ════════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsureInputSystemEventSystem();
            BuildUIIfMissing();
        }

        private void Start()
        {
            EnsureInputSystemEventSystem();
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

            // Subscribe to all game events
            _gm.OnPhaseChanged         += HandlePhaseChanged;
            _gm.OnHumanHandDealt       += HandleHumanHandDealt;
            _gm.OnCurrentPlayerChanged += HandleCurrentPlayerChanged;
            _gm.OnBiddingAction        += HandleBiddingAction;
            _gm.OnCardPlayed           += HandleCardPlayed;
            _gm.OnTrickWon             += HandleTrickWon;
            _gm.OnTrumpRevealed        += HandleTrumpRevealed;
            _gm.OnRoundScored                  += HandleRoundScored;
            _gm.OnGameOver                     += HandleGameOver;
            _gm.OnHumanTrumpSelectionRequired  += HandleHumanTrumpSelectionRequired;
            _gm.OnStateChanged                 += RefreshAllDisplay;

            RefreshAllDisplay();
        }

        private void OnDestroy()
        {
            if (_gm == null) return;
            _gm.OnPhaseChanged         -= HandlePhaseChanged;
            _gm.OnHumanHandDealt       -= HandleHumanHandDealt;
            _gm.OnCurrentPlayerChanged -= HandleCurrentPlayerChanged;
            _gm.OnBiddingAction        -= HandleBiddingAction;
            _gm.OnCardPlayed           -= HandleCardPlayed;
            _gm.OnTrickWon             -= HandleTrickWon;
            _gm.OnTrumpRevealed        -= HandleTrumpRevealed;
            _gm.OnRoundScored                  -= HandleRoundScored;
            _gm.OnGameOver                     -= HandleGameOver;
            _gm.OnHumanTrumpSelectionRequired  -= HandleHumanTrumpSelectionRequired;
            _gm.OnStateChanged                 -= RefreshAllDisplay;
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
                    scoreHUD.SetStatusMessage("Dealing cards...");
                    trickArea.ClearAll();
                    biddingPanel.Hide();
                    break;

                case GamePhase.Bidding:
                    scoreHUD.SetStatusMessage("Bidding Phase — Choose your bid wisely");
                    trickArea.ClearAll();
                    break;

                case GamePhase.TrumpSelection:
                    scoreHUD.SetStatusMessage("Partner is selecting Trump suit...");
                    biddingPanel.Hide();
                    break;

                case GamePhase.Playing:
                    scoreHUD.SetStatusMessage("Tricks in progress");
                    biddingPanel.Hide();
                    break;

                case GamePhase.RoundOver:
                    biddingPanel.Hide();
                    break;
            }
        }

        private void HandleHumanHandDealt(Hand hand)
        {
            _animateNextDeal = true;   // mark: next RefreshHumanCards should animate
            RefreshHumanCards();
            RefreshAICardCounts();
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
                    scoreHUD.SetStatusMessage("YOUR TURN — Select a card to play");
                else
                {
                    string name = seat == PlayerSeat.North ? "Partner" : seat.ToString();
                    scoreHUD.SetStatusMessage($"{name}'s turn to play...");
                }
            }

            RefreshHumanCards();
        }

        private void HandleBiddingAction(PlayerSeat seat, int? bid)
        {
            PlayerSeatUI seatUI = GetSeatUI(seat);
            string text = bid.HasValue ? $"Bid {bid.Value}!" : "Pass";
            if (seatUI != null)
                seatUI.ShowActionBubble(text);

            scoreHUD.UpdateHUD(_gm);
        }

        private void HandleCardPlayed(PlayerSeat seat, Card card)
        {
            // Update trick cards
            if (trickArea != null && _gm.GetCurrentTrick() != null)
                trickArea.DisplayTrick(_gm.GetCurrentTrick());

            // Update hand visuals
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
            if (seatUI != null)
                seatUI.ShowActionBubble($"Won +{points} pts!");

            scoreHUD.UpdateHUD(_gm);
        }

        private void HandleTrumpRevealed(Suit trump)
        {
            string sym = CardVisualTheme.GetSuitSymbol(trump);
            string name = CardVisualTheme.GetSuitName(trump);
            scoreHUD.SetStatusMessage($"★ TRUMP REVEALED: {sym} {name.ToUpper()}! ★");
            scoreHUD.UpdateHUD(_gm);
            if (trumpCardSlot != null)
                trumpCardSlot.UpdateDisplay(_gm);
            RefreshHumanCards();
        }

        private void HandleHumanTrumpSelectionRequired(Hand hand)
        {
            if (trumpSelectionModal == null)
                BuildUIIfMissing();

            if (trumpSelectionModal != null)
                trumpSelectionModal.Show(_gm.GetCurrentBid(), hand);
            scoreHUD.SetStatusMessage("★ YOU WON THE BID! Choose Trump suit ★");
        }

        private void HandleRoundScored(bool biddingTeamWon)
        {
            if (roundEndModal != null)
                roundEndModal.ShowRoundOver(_gm.ScoreManager, biddingTeamWon);
        }

        private void HandleGameOver(int winningTeam)
        {
            if (roundEndModal != null)
                roundEndModal.ShowGameOver(_gm.ScoreManager, winningTeam);
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
            if (trumpCardSlot != null)
                trumpCardSlot.UpdateDisplay(_gm);

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
            // Consume the animate flag: only play deal animation once per real deal
            bool animate = _animateNextDeal;
            _animateNextDeal = false;
            southSeat.RenderHumanHand(hand, validPlays, OnHumanCardSelected, animate);
        }

        private void RefreshAICardCounts()
        {
            if (_gm == null) return;
            if (northSeat != null) northSeat.RenderAICardCount(_gm.GetHand(PlayerSeat.North).Count, true);
            if (westSeat != null)  westSeat.RenderAICardCount(_gm.GetHand(PlayerSeat.West).Count, false);
            if (eastSeat != null)  eastSeat.RenderAICardCount(_gm.GetHand(PlayerSeat.East).Count, false);
        }

        private void UpdateTurnHighlights(PlayerSeat current)
        {
            if (southSeat != null) southSeat.SetActiveTurn(current == PlayerSeat.South);
            if (northSeat != null) northSeat.SetActiveTurn(current == PlayerSeat.North);
            if (westSeat  != null) westSeat.SetActiveTurn(current == PlayerSeat.West);
            if (eastSeat  != null) eastSeat.SetActiveTurn(current == PlayerSeat.East);
        }

        private void OnHumanCardSelected(Card card)
        {
            if (_gm == null || _gm.CurrentPhase != GamePhase.Playing || _gm.CurrentPlayer != GameManager.HumanSeat)
                return;

            _gm.PlayHumanCard(card);
        }

        private PlayerSeatUI GetSeatUI(PlayerSeat seat)
        {
            return seat switch
            {
                PlayerSeat.South => southSeat,
                PlayerSeat.North => northSeat,
                PlayerSeat.West  => westSeat,
                PlayerSeat.East  => eastSeat,
                _                => null
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        // AUTO-BUILD COMPLETE UI HIERARCHY
        // ════════════════════════════════════════════════════════════════════════

        public void BuildUIIfMissing()
        {
            // 1. Canvas Setup
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
            }

            canvasScaler = GetComponent<CanvasScaler>();
            if (canvasScaler == null)
            {
                canvasScaler = gameObject.AddComponent<CanvasScaler>();
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(1080, 1920);
                canvasScaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem in scene configured for Unity Input System
            EnsureInputSystemEventSystem();

            // 2. Fullscreen Table Felt Background
            if (tableBackground == null)
            {
                GameObject bgObj = new GameObject("TableFelt_BG");
                bgObj.transform.SetParent(transform, false);
                RectTransform bgrt = bgObj.AddComponent<RectTransform>();
                bgrt.anchorMin = Vector2.zero;
                bgrt.anchorMax = Vector2.one;
                bgrt.sizeDelta = Vector2.zero;

                tableBackground = bgObj.AddComponent<Image>();
                tableBackground.sprite = CardVisualTheme.TableFelt;
                tableBackground.type = Image.Type.Simple;
                tableBackground.preserveAspect = false;
                tableBackground.color = Color.white;
                tableBackground.raycastTarget = false;
            }

            // 3. Top Score HUD
            if (scoreHUD == null)
            {
                GameObject hudObj = new GameObject("ScoreHUD");
                hudObj.transform.SetParent(transform, false);
                scoreHUD = hudObj.AddComponent<ScoreHUDUI>();
                scoreHUD.EnsureComponents();
            }

            // 4. Center Trick Area
            if (trickArea == null)
            {
                GameObject trickObj = new GameObject("TrickArea");
                trickObj.transform.SetParent(transform, false);
                RectTransform trt = trickObj.AddComponent<RectTransform>();
                trt.anchorMin = new Vector2(0.5f, 0.5f);
                trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.anchoredPosition = new Vector2(0, 140);
                trt.sizeDelta = new Vector2(460, 460);

                trickArea = trickObj.AddComponent<TrickAreaUI>();
                trickArea.EnsureComponents();
            }

            // 5. Player Seats
            // North (Partner) - Top
            if (northSeat == null)
            {
                GameObject nObj = new GameObject("Player_North");
                nObj.transform.SetParent(transform, false);
                RectTransform nrt = nObj.AddComponent<RectTransform>();
                nrt.anchorMin = new Vector2(0.5f, 0.5f);
                nrt.anchorMax = new Vector2(0.5f, 0.5f);
                nrt.anchoredPosition = new Vector2(0, 680);
                nrt.sizeDelta = new Vector2(400, 140);

                northSeat = nObj.AddComponent<PlayerSeatUI>();
                northSeat.Seat = PlayerSeat.North;
                northSeat.EnsureComponents();
                northSeat.SetLayoutPositions(new Vector2(0, 48), new Vector2(0, 14), new Vector2(0, -22));
            }

            // West (Opponent) - Left
            if (westSeat == null)
            {
                GameObject wObj = new GameObject("Player_West");
                wObj.transform.SetParent(transform, false);
                RectTransform wrt = wObj.AddComponent<RectTransform>();
                wrt.anchorMin = new Vector2(0.5f, 0.5f);
                wrt.anchorMax = new Vector2(0.5f, 0.5f);
                wrt.anchoredPosition = new Vector2(-410, 140);
                wrt.sizeDelta = new Vector2(180, 400);

                westSeat = wObj.AddComponent<PlayerSeatUI>();
                westSeat.Seat = PlayerSeat.West;
                westSeat.EnsureComponents();
                westSeat.SetLayoutPositions(new Vector2(0, 160), new Vector2(0, 120), new Vector2(0, 80));
            }

            // East (Opponent) - Right
            if (eastSeat == null)
            {
                GameObject eObj = new GameObject("Player_East");
                eObj.transform.SetParent(transform, false);
                RectTransform ert = eObj.AddComponent<RectTransform>();
                ert.anchorMin = new Vector2(0.5f, 0.5f);
                ert.anchorMax = new Vector2(0.5f, 0.5f);
                ert.anchoredPosition = new Vector2(410, 140);
                ert.sizeDelta = new Vector2(180, 400);

                eastSeat = eObj.AddComponent<PlayerSeatUI>();
                eastSeat.Seat = PlayerSeat.East;
                eastSeat.EnsureComponents();
                eastSeat.SetLayoutPositions(new Vector2(0, 160), new Vector2(0, 120), new Vector2(0, 80));
            }

            // South (Human) - Bottom
            if (southSeat == null)
            {
                GameObject sObj = new GameObject("Player_South");
                sObj.transform.SetParent(transform, false);
                RectTransform srt = sObj.AddComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.5f, 0);
                srt.anchorMax = new Vector2(0.5f, 0);
                srt.pivot     = new Vector2(0.5f, 0);
                srt.anchoredPosition = new Vector2(0, 20);
                srt.sizeDelta = new Vector2(1060, 280);

                southSeat = sObj.AddComponent<PlayerSeatUI>();
                southSeat.Seat = PlayerSeat.South;
                southSeat.EnsureComponents();
                southSeat.SetLayoutPositions(new Vector2(-380, 210), new Vector2(-380, 168), new Vector2(-380, 130));

                // Position South's card container centered
                RectTransform ccrt = (RectTransform)southSeat.CardContainer;
                ccrt.anchorMin = new Vector2(0.5f, 0.5f);
                ccrt.anchorMax = new Vector2(0.5f, 0.5f);
                ccrt.anchoredPosition = new Vector2(0, -30);
                ccrt.sizeDelta = new Vector2(1040, 180);
            }

            // 6. Interactive Bidding Modal Panel
            if (biddingPanel == null)
            {
                GameObject bObj = new GameObject("BiddingPanel");
                bObj.transform.SetParent(transform, false);
                RectTransform brt = bObj.AddComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 0.5f);
                brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.anchoredPosition = new Vector2(0, -250);

                biddingPanel = bObj.AddComponent<BiddingPanelUI>();
                biddingPanel.EnsureComponents();
                biddingPanel.Hide();
            }

            // 7. Round End & Game Over Modal
            if (roundEndModal == null)
            {
                GameObject rObj = new GameObject("RoundEndModal");
                rObj.transform.SetParent(transform, false);
                RectTransform rrt = rObj.AddComponent<RectTransform>();
                rrt.anchorMin = new Vector2(0.5f, 0.5f);
                rrt.anchorMax = new Vector2(0.5f, 0.5f);
                rrt.anchoredPosition = Vector2.zero;

                roundEndModal = rObj.AddComponent<RoundEndModalUI>();
                roundEndModal.EnsureComponents();
                roundEndModal.Hide();
            }

            // 8. On-table physical Trump Card Slot
            if (trumpCardSlot == null)
            {
                Transform existingSlot = transform.Find("TrumpCardSlot");
                GameObject tSlotObj = existingSlot != null ? existingSlot.gameObject : new GameObject("TrumpCardSlot");
                if (existingSlot == null) tSlotObj.transform.SetParent(transform, false);
                RectTransform trt = tSlotObj.GetComponent<RectTransform>() ?? tSlotObj.AddComponent<RectTransform>();
                trt.anchorMin = new Vector2(0.5f, 0.5f);
                trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.anchoredPosition = new Vector2(-270, 390);
                trt.sizeDelta = new Vector2(120, 165);

                trumpCardSlot = tSlotObj.GetComponent<TrumpCardSlotUI>() ?? tSlotObj.AddComponent<TrumpCardSlotUI>();
                trumpCardSlot.EnsureComponents();
                tSlotObj.SetActive(false);
            }

            // 9. Trump Selection Modal  (full-screen overlay)
            if (trumpSelectionModal == null)
            {
                Transform existingModal = transform.Find("TrumpSelectionModal");
                GameObject tModalObj = existingModal != null ? existingModal.gameObject : new GameObject("TrumpSelectionModal");
                if (existingModal == null) tModalObj.transform.SetParent(transform, false);
                RectTransform mrt = tModalObj.GetComponent<RectTransform>() ?? tModalObj.AddComponent<RectTransform>();
                // Full-screen stretch so the backdrop covers everything
                mrt.anchorMin        = Vector2.zero;
                mrt.anchorMax        = Vector2.one;
                mrt.offsetMin        = Vector2.zero;
                mrt.offsetMax        = Vector2.zero;

                trumpSelectionModal = tModalObj.GetComponent<TrumpSelectionModalUI>() ?? tModalObj.AddComponent<TrumpSelectionModalUI>();
                trumpSelectionModal.EnsureComponents();
                trumpSelectionModal.Hide();
            }
        }

        private void EnsureInputSystemEventSystem()
        {
            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            // Remove legacy StandaloneInputModule if present to prevent InvalidOperationException with new Input System
            var legacyModule = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (legacyModule != null)
            {
                if (Application.isPlaying) Destroy(legacyModule);
                else DestroyImmediate(legacyModule);
            }

            // Ensure InputSystemUIInputModule is present and has active default action bindings
            var inputModule = es.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule != null)
            {
                inputModule.enabled = false;
                inputModule.AssignDefaultActions();
                inputModule.enabled = true;
            }
        }
    }
}
