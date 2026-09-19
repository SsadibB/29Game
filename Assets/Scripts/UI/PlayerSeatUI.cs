using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Visual UI component representing one of the 4 player seats (South, North, East, West).
    /// Landscape layout: avatar uses Vector.png, cards are larger (130×190 for South),
    /// deal animations fly from the board center, and AI backs also animate on deal.
    /// </summary>
    public class PlayerSeatUI : MonoBehaviour
    {
        [Header("Seat Identity")]
        public PlayerSeat Seat;

        [Header("UI Elements")]
        [SerializeField] private Image avatarBg;
        [SerializeField] private Image avatarIcon;
        [SerializeField] private Image turnGlowBorder;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Image actionBubbleBg;
        [SerializeField] private Text actionBubbleText;
        [SerializeField] private Button skipButton;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private Image dealerCoinImage;

        private readonly List<CardUI> _spawnedCards = new List<CardUI>();
        private Coroutine _actionBubbleCoroutine;
        private bool _skipButtonInitialized;
        private bool _skipButtonVisible;
        private bool _thinkingVisible;
        private bool _isPassed;
        private Tween _skipPulseTween;
        private Vector3 _originalActionBubbleScale = Vector3.one;
        private Vector3 _originalSkipButtonScale = Vector3.one;
        private bool _skipScalesCached;

        [SerializeField] private Sprite avatarSprite;

        public Transform CardContainer => cardContainer != null ? cardContainer : transform;
        public Image AvatarBg => avatarBg;
        public Image AvatarIcon => avatarIcon;
        public Sprite AvatarSprite { get => avatarSprite; set { avatarSprite = value; ApplyAvatar(); } }

        // ── Container rotation per seat ───────────────────────────────────────────────
        // The card arc is computed in the South (upward arc) frame of reference.
        // Each other seat's CardContainer is simply rotated so the same arc faces
        // the correct direction without any per-card position recalculation:
        //   South  0°  → upward arc (reference)
        //   North  180° → downward arc
        //   East   90°  → left-facing arc (rotated CW)
        //   West  -90°  → right-facing arc (rotated CCW)
        private float GetContainerRotationZ()
        {
            return Seat switch
            {
                PlayerSeat.North => 180f,
                PlayerSeat.East => 90f,
                PlayerSeat.West => -90f,
                _ => 0f,   // South
            };
        }

        /// <summary>
        /// Returns the anchored-position offset for the dealer coin relative to this seat's center.
        /// The coin is placed adjacent to the avatar circle so it's clearly associated with that player
        /// but doesn't obscure the face image.
        /// South → top-right of avatar | North → bottom-right | East → top-left | West → top-right
        /// </summary>
        private Vector2 GetDealerCoinOffset()
        {
            return Seat switch
            {
                PlayerSeat.North => new Vector2(40f, -40f),
                PlayerSeat.East  => new Vector2(-50f, 40f),
                PlayerSeat.West  => new Vector2(50f, 40f),
                _                => new Vector2(40f, 40f),   // South
            };
        }

        // Fan-out look for the hand: each card gets a small rotation and a
        // slight downward arc toward the edges, so the hand reads as a fan
        // held from below (bottom edges angling toward the table) instead of
        // a flat row. Approximated with a parabola + linear rotation rather
        // than true polar placement around a pivot point, so it drops into
        // the existing anchoredPosition/hover/DOTween code with no other
        // changes needed.
        private const float FanMaxRotationDeg = 24f;
        private const float FanArcHeight = 34f;

        /// <summary>Returns the (extra Y offset, rotation) for hand card index i of count.</summary>
        private (float yOffset, float rotZ) GetFanOffset(int i, int count)
        {
            if (count <= 1) return (0f, 0f);
            float u = (i / (float)(count - 1)) * 2f - 1f; // -1 (leftmost) .. 1 (rightmost)
            float rot = Mathf.Clamp(count * 1.4f, 6f, FanMaxRotationDeg);
            return (-FanArcHeight * (u * u), -u * rot);
        }

        // Same arc/fan treatment as the human hand above, scaled down for the
        // smaller face-down AI card backs (55×80 vs 130×190). The center card
        // sits closest to straight (u≈0 → near-zero offset/rotation); cards
        // toward either end curve outward and rotate away from center, same
        // parabola-arc + linear-rotation approximation as GetFanOffset.
        private const float AIFanMaxRotationDeg = 16f;
        private const float AIFanArcHeight = 14f;

        /// <summary>Returns the (extra arc offset, rotation) for AI card back index i of count.</summary>
        private (float arcOffset, float rotZ) GetAIFanOffset(int i, int count)
        {
            if (count <= 1) return (0f, 0f);
            float u = (i / (float)(count - 1)) * 2f - 1f; // -1 (leftmost/topmost) .. 1 (rightmost/bottommost)
            float rot = Mathf.Clamp(count * 1.6f, 5f, AIFanMaxRotationDeg);
            return (-AIFanArcHeight * (u * u), -u * rot);
        }

        // Card dimensions for landscape layout
        private const float HumanCardW = 130f;
        private const float HumanCardH = 190f;
        private const float AICardW = 55f;
        private const float AICardH = 80f;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnDestroy()
        {
            if (turnGlowBorder != null) turnGlowBorder.transform.DOKill();
            if (actionBubbleBg != null) actionBubbleBg.transform.DOKill();
            if (_skipPulseTween != null) _skipPulseTween.Kill();
        }

        public void EnsureComponents()
        {
            if (cardContainer == null)
            {
                Transform existingCC = transform.Find("CardContainer");
                GameObject cc;
                RectTransform rt;
                if (existingCC != null)
                {
                    cc = existingCC.gameObject;
                    rt = cc.GetComponent<RectTransform>();
                    if (rt == null)
                    {
                        if (Application.isPlaying) Destroy(cc);
                        else DestroyImmediate(cc);
                        cc = new GameObject("CardContainer", typeof(RectTransform));
                        cc.transform.SetParent(transform, false);
                        rt = cc.GetComponent<RectTransform>();
                    }
                }
                else
                {
                    cc = new GameObject("CardContainer", typeof(RectTransform));
                    cc.transform.SetParent(transform, false);
                    rt = cc.GetComponent<RectTransform>();
                }

                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(900, 200);
                }
                cardContainer = cc.transform;
            }

            // Apply the seat-specific container rotation so the South arc is
            // reused for all seats — only the container orientation changes.
            if (cardContainer != null)
                cardContainer.localEulerAngles = new Vector3(0f, 0f, GetContainerRotationZ());

            if (avatarBg == null)
            {
                Transform existingAv = transform.Find("Avatar");
                if (existingAv == null) existingAv = transform.Find("AvatarBg");

                GameObject av;
                RectTransform rt;
                if (existingAv != null)
                {
                    av = existingAv.gameObject;
                    rt = av.GetComponent<RectTransform>();
                    if (rt == null)
                    {
                        if (Application.isPlaying) Destroy(av);
                        else DestroyImmediate(av);
                        av = new GameObject("AvatarBg", typeof(RectTransform));
                        av.transform.SetParent(transform, false);
                        rt = av.GetComponent<RectTransform>();
                    }
                }
                else
                {
                    av = new GameObject("AvatarBg", typeof(RectTransform));
                    av.transform.SetParent(transform, false);
                    rt = av.GetComponent<RectTransform>();
                }

                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(62, 62);
                }

                avatarBg = av.GetComponent<Image>();
                if (avatarBg == null) avatarBg = av.AddComponent<Image>();
                avatarBg.sprite = CardVisualTheme.CircleAvatar;
                avatarBg.color = Color.white;
                avatarBg.raycastTarget = false;

                // Delete any legacy single-letter "Initial" text inside Avatar —
                // no longer wanted, not just hidden.
                Transform oldInit = av.transform.Find("Initial");
                if (oldInit != null)
                {
                    if (Application.isPlaying) Destroy(oldInit.gameObject);
                    else DestroyImmediate(oldInit.gameObject);
                }
            }

            if (avatarIcon == null && avatarBg != null)
            {
                Transform existingIcon = avatarBg.transform.Find("AvatarIcon");
                if (existingIcon != null && existingIcon.GetComponent<RectTransform>() == null)
                {
                    if (Application.isPlaying) Destroy(existingIcon.gameObject);
                    else DestroyImmediate(existingIcon.gameObject);
                    existingIcon = null;
                }

                GameObject icon = existingIcon != null ? existingIcon.gameObject : new GameObject("AvatarIcon", typeof(RectTransform));
                if (existingIcon == null) icon.transform.SetParent(avatarBg.transform, false);
                RectTransform irt = icon.GetComponent<RectTransform>();
                if (irt != null)
                {
                    irt.anchorMin = new Vector2(0.5f, 0.5f);
                    irt.anchorMax = new Vector2(0.5f, 0.5f);
                    irt.pivot = new Vector2(0.5f, 0.5f);
                    irt.anchoredPosition = Vector2.zero;
                    irt.sizeDelta = new Vector2(35, 35);
                }

                avatarIcon = icon.GetComponent<Image>();
                if (avatarIcon == null) avatarIcon = icon.AddComponent<Image>();
                avatarIcon.sprite = CardVisualTheme.VectorAvatar;
                avatarIcon.color = Color.white;
                avatarIcon.preserveAspect = true;
                avatarIcon.raycastTarget = false;
            }

            if (turnGlowBorder == null && avatarBg != null)
            {
                Transform existingGlow = avatarBg.transform.Find("TurnGlow");
                GameObject glow = existingGlow != null ? existingGlow.gameObject : new GameObject("TurnGlow", typeof(RectTransform));
                if (existingGlow == null) glow.transform.SetParent(avatarBg.transform, false);
                RectTransform grt = glow.GetComponent<RectTransform>();
                if (grt != null)
                {
                    grt.anchorMin = Vector2.zero;
                    grt.anchorMax = Vector2.one;
                    grt.sizeDelta = new Vector2(18, 18);
                }
                turnGlowBorder = glow.GetComponent<Image>();
                if (turnGlowBorder == null) turnGlowBorder = glow.AddComponent<Image>();
                turnGlowBorder.sprite = CardVisualTheme.CreateCircleSprite(80, Color.clear, CardVisualTheme.ColorGold, 6);
                turnGlowBorder.color = CardVisualTheme.ColorGoldGlow;
                turnGlowBorder.raycastTarget = false;
                glow.SetActive(false);
            }

            if (nameLabel == null)
            {
                Transform existingNL = transform.Find("NameLabel");
                GameObject nl = existingNL != null ? existingNL.gameObject : new GameObject("NameLabel", typeof(RectTransform));
                if (existingNL == null) nl.transform.SetParent(transform, false);
                RectTransform nrt = nl.GetComponent<RectTransform>();
                if (nrt != null)
                {
                    nrt.anchorMin = new Vector2(0.5f, 0.5f);
                    nrt.anchorMax = new Vector2(0.5f, 0.5f);
                    nrt.sizeDelta = new Vector2(200, 28);
                }
                nameLabel = nl.GetComponent<Text>();
                if (nameLabel == null) nameLabel = nl.AddComponent<Text>();
                nameLabel.font = CardVisualTheme.GetFont();
                nameLabel.fontSize = 17;
                nameLabel.fontStyle = FontStyle.Bold;
                nameLabel.alignment = TextAnchor.MiddleCenter;
                nameLabel.color = Color.white;
                nameLabel.raycastTarget = false;
            }

            if (actionBubbleBg == null)
            {
                Transform existingAB = transform.Find("ActionBubble");
                GameObject ab = existingAB != null ? existingAB.gameObject : new GameObject("ActionBubble", typeof(RectTransform));
                if (existingAB == null) ab.transform.SetParent(transform, false);
                RectTransform abrt = ab.GetComponent<RectTransform>();
                if (abrt != null)
                {
                    abrt.anchorMin = new Vector2(0.5f, 0.5f);
                    abrt.anchorMax = new Vector2(0.5f, 0.5f);
                    abrt.sizeDelta = new Vector2(130, 34);
                }
                actionBubbleBg = ab.GetComponent<Image>();
                if (actionBubbleBg == null) actionBubbleBg = ab.AddComponent<Image>();
                actionBubbleBg.sprite = CardVisualTheme.PillBadge;
                actionBubbleBg.type = Image.Type.Sliced;
                actionBubbleBg.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);
                actionBubbleBg.raycastTarget = false;

                Transform existingABT = ab.transform.Find("Text");
                GameObject abt = existingABT != null ? existingABT.gameObject : new GameObject("Text", typeof(RectTransform));
                if (existingABT == null) abt.transform.SetParent(ab.transform, false);
                RectTransform abtrt = abt.GetComponent<RectTransform>();
                if (abtrt != null)
                {
                    abtrt.anchorMin = Vector2.zero;
                    abtrt.anchorMax = Vector2.one;
                    abtrt.sizeDelta = Vector2.zero;
                }
                actionBubbleText = abt.GetComponent<Text>();
                if (actionBubbleText == null) actionBubbleText = abt.AddComponent<Text>();
                actionBubbleText.font = CardVisualTheme.GetFont();
                actionBubbleText.fontSize = 16;
                actionBubbleText.fontStyle = FontStyle.Bold;
                actionBubbleText.alignment = TextAnchor.MiddleCenter;
                actionBubbleText.color = CardVisualTheme.ColorGold;
                actionBubbleText.raycastTarget = false;
                ab.SetActive(false);
            }

            // Skip_Text is hand-created/wired in the Editor (Button component
            // already added), so it isn't null here — just force it hidden the
            // first time this seat initializes, regardless of whatever active
            // state it was left in in the Editor. GameTableUI turns it back on
            // via SetSkipButtonActive once the calling team hits its target.
            if (skipButton != null && !_skipButtonInitialized)
            {
                skipButton.gameObject.SetActive(false);
                _skipButtonInitialized = true;
            }

            // Dealer Coin — auto-create from the DealerCoin Resources sprite.
            if (dealerCoinImage == null)
            {
                Transform existing = transform.Find("DealerCoin");
                GameObject coinObj = existing != null ? existing.gameObject : new GameObject("DealerCoin", typeof(RectTransform));
                if (existing == null) coinObj.transform.SetParent(transform, false);
                RectTransform coinRT = coinObj.GetComponent<RectTransform>();
                if (coinRT != null)
                {
                    coinRT.anchorMin = new Vector2(0.5f, 0.5f);
                    coinRT.anchorMax = new Vector2(0.5f, 0.5f);
                    coinRT.pivot     = new Vector2(0.5f, 0.5f);
                    // Offset relative to avatar so coin sits visibly next to it.
                    coinRT.anchoredPosition = GetDealerCoinOffset();
                    coinRT.sizeDelta = new Vector2(40, 40);
                }
                dealerCoinImage = coinObj.GetComponent<Image>();
                if (dealerCoinImage == null) dealerCoinImage = coinObj.AddComponent<Image>();
                Sprite coinSprite = CardVisualTheme.DealerCoin;
                if (coinSprite != null)
                {
                    dealerCoinImage.sprite = coinSprite;
                    dealerCoinImage.color  = Color.white;
                    dealerCoinImage.type   = Image.Type.Simple;
                    dealerCoinImage.preserveAspect = true;
                }
                else
                {
                    // Fallback: gold circle if asset not found
                    dealerCoinImage.sprite = CardVisualTheme.CreateCircleSprite(40, CardVisualTheme.ColorGold, Color.white, 2);
                    dealerCoinImage.color  = Color.white;
                }
                dealerCoinImage.raycastTarget = false;
                coinObj.SetActive(false);
            }

            SetupIdentity();
        }

        public void SetupIdentity()
        {
            ApplyAvatar();
            if (nameLabel == null) return;
            switch (Seat)
            {
                case PlayerSeat.South:
                    nameLabel.text = "SOUTH";
                    nameLabel.color = CardVisualTheme.ColorCyan;
                    break;
                case PlayerSeat.North:
                    nameLabel.text = "NORTH";
                    nameLabel.color = CardVisualTheme.ColorCyan;
                    break;
                case PlayerSeat.East:
                    nameLabel.text = "EAST";
                    var orangeE = new Color(0.95f, 0.55f, 0.35f);
                    nameLabel.color = orangeE;
                    break;
                case PlayerSeat.West:
                    nameLabel.text = "WEST";
                    var orangeW = new Color(0.95f, 0.55f, 0.35f);
                    nameLabel.color = orangeW;
                    break;
            }
        }

        public void ApplyAvatar()
        {
            Sprite sprite = avatarSprite != null ? avatarSprite : CardVisualTheme.GetAvatarForSeat(Seat);
            // Avatar1-4 belong on each player's avatarIcon (the child portrait),
            // not on avatarBg (the shared circular background) — avatarBg keeps
            // its plain CircleAvatar background for every seat.
            if (avatarIcon != null)
            {
                if (sprite != null) avatarIcon.sprite = sprite;
                avatarIcon.color = Color.white;
                avatarIcon.preserveAspect = true;
                avatarIcon.gameObject.SetActive(true);
            }
        }

        // Deliberately does NOT touch actionBubbleBg's anchor/position. The action
        // bubble is anchored and positioned by hand in the Editor per seat, and this
        // method used to overwrite that with a hardcoded bubblePos on every call
        // (GameTableUI calls this from Start/Awake for every seat), which is why the
        // bubble appeared to "reset to default" the moment you pressed Play — it
        // wasn't reverting, this was actively repositioning it every time.
        public void SetLayoutPositions(Vector2 avatarPos, Vector2 namePos)
        {
            EnsureComponents();
            if (avatarBg != null) ((RectTransform)avatarBg.transform).anchoredPosition = avatarPos;
            if (nameLabel != null) ((RectTransform)nameLabel.transform).anchoredPosition = namePos;
        }

        private bool _isDisabledPartner;
        public bool IsDisabledPartner => _isDisabledPartner;

        /// <summary>
        /// Visually disables/enables this partner seat when Single Play is active.
        /// </summary>
        public void SetDisabledPartner(bool disabled)
        {
            if (_isDisabledPartner == disabled) return;
            _isDisabledPartner = disabled;
            if (disabled)
            {
                if (turnGlowBorder != null) turnGlowBorder.gameObject.SetActive(false);
                if (avatarBg != null) avatarBg.color = new Color(1f, 1f, 1f, 0.35f);
                if (avatarIcon != null) avatarIcon.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
                if (nameLabel != null) nameLabel.text = $"{Seat.ToString().ToUpper()} (INACTIVE)";
                if (cardContainer != null) cardContainer.gameObject.SetActive(false);
            }
            else
            {
                if (cardContainer != null) cardContainer.gameObject.SetActive(true);
                // Restore the avatar background to pure white (255, 255, 255, 255)
                // and restore seat-specific icon and text colors via SetupIdentity().
                if (avatarBg != null) avatarBg.color = Color.white;
                SetupIdentity();
            }
        }

        public void SetActiveTurn(bool isMyTurn)
        {
            if (turnGlowBorder != null)
            {
                turnGlowBorder.gameObject.SetActive(isMyTurn);
                turnGlowBorder.transform.DOKill();
                if (isMyTurn)
                {
                    turnGlowBorder.transform.localScale = Vector3.one;
                    turnGlowBorder.transform
                        .DOScale(1.22f, 0.65f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetLink(turnGlowBorder.gameObject);
                }
            }
        }

        /// <summary>
        /// Shows or hides the Dealer Coin badge on this seat.
        /// Only the current dealer's seat should have it active.
        /// </summary>
        public void SetDealerCoin(bool isDealer)
        {
            if (dealerCoinImage == null) EnsureComponents();
            if (dealerCoinImage != null)
                dealerCoinImage.gameObject.SetActive(isDealer);
        }

        /// <summary>
        /// Shows a permanent "Pass" bubble and dims the avatar to indicate
        /// this player has passed during bidding. Unlike ShowActionBubble(),
        /// this does NOT auto-hide — it persists until ResetPassState() is called.
        /// </summary>
        public void ShowPersistentPass()
        {
            if (_isPassed) return;
            _isPassed = true;

            // Dim the avatar to signal this player is out of bidding
            if (avatarBg   != null) avatarBg.color   = new Color(1f, 1f, 1f, 0.35f);
            if (avatarIcon != null) avatarIcon.color  = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            if (actionBubbleBg == null || actionBubbleText == null) return;

            // Cancel any running auto-hide coroutine
            if (_actionBubbleCoroutine != null)
            {
                StopCoroutine(_actionBubbleCoroutine);
                _actionBubbleCoroutine = null;
            }
            HideThinking();

            actionBubbleText.text = "Pass";
            actionBubbleText.color = new Color(0.75f, 0.78f, 0.85f, 1f); // muted pale colour
            actionBubbleText.gameObject.SetActive(true);

            bool rootWasActive = actionBubbleBg.gameObject.activeSelf;
            actionBubbleBg.gameObject.SetActive(true);
            if (!rootWasActive)
            {
                actionBubbleBg.transform.DOKill();
                actionBubbleBg.transform.localScale = Vector3.one * 0.6f;
                actionBubbleBg.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetLink(actionBubbleBg.gameObject);
            }
        }

        /// <summary>
        /// Resets the pass state set by ShowPersistentPass() — restores avatar
        /// colors and hides the action bubble. Call at the start of a new round.
        /// </summary>
        public void ResetPassState()
        {
            if (!_isPassed) return;
            _isPassed = false;

            // Restore avatar (SetupIdentity handles name label color as well)
            if (avatarBg   != null) avatarBg.color  = Color.white;
            if (avatarIcon != null) avatarIcon.color = Color.white;
            SetupIdentity();

            // Restore action bubble text color and hide the bubble
            if (actionBubbleText != null)
            {
                actionBubbleText.color = CardVisualTheme.ColorGold;
                actionBubbleText.gameObject.SetActive(false);
            }
            if (!_skipButtonVisible && actionBubbleBg != null)
                actionBubbleBg.gameObject.SetActive(false);
        }

        public void ShowActionBubble(string text, float duration = 2.5f)
        {
            if (actionBubbleBg == null || actionBubbleText == null) return;

            // Don't overwrite a persistent Pass bubble
            if (_isPassed) return;

            // Skip_Text (once available) owns the shared bubble — Bid_Text
            // ("Text") doesn't get to interrupt it with a trick-win message
            // until Skip goes unavailable again.
            if (_skipButtonVisible) return;

            // Hide any active Thinking state before showing a result bubble.
            HideThinking();

            actionBubbleText.text = text;
            actionBubbleText.gameObject.SetActive(true);

            // Skip_Text lives under this same ActionBubble root, so the root
            // may already be active/scaled-up because Skip is currently
            // showing — only replay the pop-in animation when the root itself
            // was actually off, otherwise a live Skip button would flicker
            // every time a message bubble fires.
            bool rootWasActive = actionBubbleBg.gameObject.activeSelf;
            actionBubbleBg.gameObject.SetActive(true);
            if (!rootWasActive)
            {
                actionBubbleBg.transform.DOKill();
                actionBubbleBg.transform.localScale = Vector3.one * 0.6f;
                actionBubbleBg.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetLink(actionBubbleBg.gameObject);
            }

            if (_actionBubbleCoroutine != null) StopCoroutine(_actionBubbleCoroutine);
            _actionBubbleCoroutine = StartCoroutine(HideActionBubbleRoutine(duration));
        }

        /// <summary>
        /// Shows a persistent "Thinking…" bubble while the AI is deciding its bid.
        /// Stays visible until HideThinking() or ShowActionBubble() clears it.
        /// </summary>
        public void ShowThinking()
        {
            if (actionBubbleBg == null || actionBubbleText == null) return;
            if (_skipButtonVisible) return;

            _thinkingVisible = true;

            // Cancel any auto-hide coroutine so the thinking bubble persists.
            if (_actionBubbleCoroutine != null)
            {
                StopCoroutine(_actionBubbleCoroutine);
                _actionBubbleCoroutine = null;
            }

            actionBubbleText.text = "Thinking\u2026";
            actionBubbleText.gameObject.SetActive(true);

            bool rootWasActive = actionBubbleBg.gameObject.activeSelf;
            actionBubbleBg.gameObject.SetActive(true);
            if (!rootWasActive)
            {
                actionBubbleBg.transform.DOKill();
                actionBubbleBg.transform.localScale = Vector3.one * 0.6f;
                actionBubbleBg.transform.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetLink(actionBubbleBg.gameObject);
            }
        }

        /// <summary>
        /// Hides the "Thinking…" bubble if it is currently visible.
        /// Called automatically by ShowActionBubble and HideActionBubbleRoutine.
        /// </summary>
        public void HideThinking()
        {
            if (!_thinkingVisible) return;
            _thinkingVisible = false;

            if (actionBubbleText != null) actionBubbleText.gameObject.SetActive(false);

            if (!_skipButtonVisible && actionBubbleBg != null)
            {
                actionBubbleBg.transform.DOKill();
                actionBubbleBg.transform.DOScale(0.5f, 0.12f).SetEase(Ease.InQuad)
                    .SetLink(actionBubbleBg.gameObject)
                    .OnComplete(() =>
                    {
                        if (actionBubbleBg != null && !_skipButtonVisible && !_thinkingVisible)
                            actionBubbleBg.gameObject.SetActive(false);
                    });
            }
        }

        private System.Collections.IEnumerator HideActionBubbleRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            // Only the timed message text goes away here — Skip_Text (if
            // active) is managed independently by SetSkipButtonActive and
            // must not be pulled down along with the message.
            if (actionBubbleText != null) actionBubbleText.gameObject.SetActive(false);

            if (!_skipButtonVisible && actionBubbleBg != null)
            {
                actionBubbleBg.transform.DOKill();
                actionBubbleBg.transform.DOScale(0.5f, 0.15f).SetEase(Ease.InQuad)
                    .SetLink(actionBubbleBg.gameObject)
                    .OnComplete(() =>
                    {
                        if (actionBubbleBg != null && !_skipButtonVisible)
                            actionBubbleBg.gameObject.SetActive(false);
                    });
            }
            _actionBubbleCoroutine = null;
        }

        /// <summary>
        /// Shows or hides the Skip button (Skip_Text). GameTableUI drives this
        /// every refresh from GameManager.IsHumanSkipAvailable(), which is now
        /// true once the round's outcome is decided either way — the bidding
        /// team already hit its target, or the opponent already put it out of
        /// reach — and no trick is currently mid-play.
        ///
        /// Skip_Text shares the ActionBubble root with Bid_Text ("Text"), so
        /// turning Skip on both forces that shared root active and hides
        /// Bid_Text (Skip takes over the bubble); turning Skip off lets
        /// Bid_Text resume showing its normal messages.
        /// </summary>
        /// <summary>
        /// Shows or hides the Skip button (Skip_Text). GameTableUI drives this
        /// every refresh from GameManager.IsHumanSkipAvailable().
        ///
        /// When active, the Skip button remains enlarged and the ActionBubble
        /// performs a smooth continuous pulse animation that persists across turn changes
        /// until Skip is clicked or the round ends.
        /// </summary>
        public void SetSkipButtonActive(bool active)
        {
            if (!_skipScalesCached)
            {
                if (actionBubbleBg != null) _originalActionBubbleScale = actionBubbleBg.transform.localScale;
                if (skipButton != null) _originalSkipButtonScale = skipButton.transform.localScale;
                _skipScalesCached = true;
            }

            _skipButtonVisible = active;

            if (active)
            {
                if (skipButton != null)
                {
                    skipButton.gameObject.SetActive(true);
                    skipButton.interactable = true;
                    skipButton.transform.localScale = _originalSkipButtonScale * 1.15f;
                }

                if (actionBubbleText != null) actionBubbleText.gameObject.SetActive(false);

                if (actionBubbleBg != null)
                {
                    actionBubbleBg.gameObject.SetActive(true);

                    // Keep smooth pulse animation playing without resetting/stopping on turn changes
                    if (_skipPulseTween == null || !_skipPulseTween.IsActive())
                    {
                        actionBubbleBg.transform.DOKill();
                        actionBubbleBg.transform.localScale = _originalActionBubbleScale * 1.10f;
                        _skipPulseTween = actionBubbleBg.transform
                            .DOScale(_originalActionBubbleScale * 1.25f, 0.65f)
                            .SetEase(Ease.InOutSine)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetLink(actionBubbleBg.gameObject);
                    }
                }
            }
            else
            {
                // Stop pulse animation and restore scales when Skip is used or round ends
                if (_skipPulseTween != null)
                {
                    _skipPulseTween.Kill();
                    _skipPulseTween = null;
                }

                if (skipButton != null)
                {
                    skipButton.transform.localScale = _originalSkipButtonScale;
                    skipButton.gameObject.SetActive(false);
                }

                if (actionBubbleBg != null)
                {
                    actionBubbleBg.transform.DOKill();
                    actionBubbleBg.transform.localScale = _originalActionBubbleScale;

                    if (_actionBubbleCoroutine == null
                        && (actionBubbleText == null || !actionBubbleText.gameObject.activeSelf))
                    {
                        actionBubbleBg.gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// Wires the Skip button's click handler. Call once during setup. Clears
        /// any previous listener first so repeated calls don't stack. No-op if
        /// skipButton hasn't been assigned in the Inspector.
        /// </summary>
        public void BindSkipButton(UnityEngine.Events.UnityAction onSkipClicked)
        {
            if (skipButton == null) return;
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(onSkipClicked);
        }

        /// <summary>World position to fly a played card from this seat toward the trick area.</summary>
        public Vector3 GetPlayOriginWorld(Card card)
        {
            if (card != null)
            {
                for (int i = 0; i < _spawnedCards.Count; i++)
                {
                    CardUI ui = _spawnedCards[i];
                    if (ui != null && ui.CurrentCard != null && ui.CurrentCard.Equals(card))
                        return ui.transform.position;
                }
            }

            if (_spawnedCards.Count > 0)
            {
                CardUI last = _spawnedCards[_spawnedCards.Count - 1];
                if (last != null) return last.transform.position;
            }

            return CardContainer != null ? CardContainer.position : transform.position;
        }

        /// <summary>World rotation of the played card in this seat's hand, so it begins flying at its exact current angle.</summary>
        public Quaternion GetPlayOriginRotation(Card card)
        {
            if (card != null)
            {
                for (int i = 0; i < _spawnedCards.Count; i++)
                {
                    CardUI ui = _spawnedCards[i];
                    if (ui != null && ui.CurrentCard != null && ui.CurrentCard.Equals(card))
                        return ui.transform.rotation;
                }
            }

            if (_spawnedCards.Count > 0)
            {
                CardUI last = _spawnedCards[_spawnedCards.Count - 1];
                if (last != null) return last.transform.rotation;
            }

            return CardContainer != null ? CardContainer.rotation : transform.rotation;
        }

        /// <summary>Plays a horizontal shake animation on the card matching <paramref name="card"/>.</summary>
        public void ShakeCard(Card card)
        {
            if (card == null) return;
            for (int i = 0; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] != null && _spawnedCards[i].CurrentCard != null && _spawnedCards[i].CurrentCard.Equals(card))
                {
                    _spawnedCards[i].Shake();
                    break;
                }
            }
        }

        /// <summary>
        /// Renders South's cards as full interactive CardUI objects.
        /// When animate=true, cards fly from the board center to their hand positions.
        /// </summary>
        public void RenderHumanHand(Hand hand, List<Card> validPlays, Action<Card> onCardClick, bool animate = false)
        {
            if (hand == null || hand.Count == 0)
            {
                ClearCards();
                return;
            }

            int count = hand.Count;
            float cardW = HumanCardW;
            float cardH = HumanCardH;
            // Wider spread for landscape — up to 1200px total width
            float spacing = Mathf.Min(cardW * 0.88f, 1100f / Mathf.Max(1, count));
            float startX = -(count - 1) * spacing * 0.5f;

            // Check if existing spawned cards match the hand exactly
            bool sameHand = _spawnedCards.Count == count;
            if (sameHand)
            {
                for (int i = 0; i < count; i++)
                {
                    if (_spawnedCards[i] == null || _spawnedCards[i].CurrentCard != hand.Cards[i])
                    {
                        sameHand = false;
                        break;
                    }
                }
            }

            // Case 1: Same hand — update playability only, no animation
            if (sameHand)
            {
                for (int i = 0; i < count; i++)
                {
                    Card card = hand.Cards[i];
                    bool isPlayable = validPlays != null && validPlays.Contains(card);
                    _spawnedCards[i].BindClick(onCardClick);
                    _spawnedCards[i].SetPlayable(isPlayable);
                }
                return;
            }

            // Case 2: Second batch deal — existing cards slide to new positions, new cards fly from center
            int existingCount = _spawnedCards.Count;
            bool isAppend = existingCount > 0 && count > existingCount;
            if (isAppend)
            {
                for (int i = 0; i < existingCount; i++)
                {
                    if (_spawnedCards[i] == null || _spawnedCards[i].CurrentCard != hand.Cards[i])
                    {
                        isAppend = false;
                        break;
                    }
                }
            }

            if (isAppend)
            {
                // Slide existing cards to their new positions in the wider spread
                for (int i = 0; i < existingCount; i++)
                {
                    Card card = hand.Cards[i];
                    bool isPlayable = validPlays != null && validPlays.Contains(card);
                    _spawnedCards[i].BindClick(onCardClick);
                    _spawnedCards[i].SetPlayable(isPlayable);

                    var (yOff, rotZ) = GetFanOffset(i, count);
                    Vector2 targetPos = new Vector2(startX + i * spacing, yOff);
                    _spawnedCards[i].SetBasePosition(targetPos);
                    RectTransform rtExisting = _spawnedCards[i].GetComponent<RectTransform>();
                    if (rtExisting != null)
                    {
                        rtExisting.DOKill();
                        rtExisting.DOAnchorPos(targetPos, 0.28f).SetEase(Ease.OutQuad);
                        rtExisting.DORotate(new Vector3(0, 0, rotZ), 0.28f).SetEase(Ease.OutQuad);
                    }
                }

                // Spawn and animate the new cards from the board center
                Vector2 dealOrigin = GetCenterOffsetInContainer();
                for (int i = existingCount; i < count; i++)
                {
                    Card card = hand.Cards[i];
                    bool isPlayable = validPlays != null && validPlays.Contains(card);

                    GameObject cardObj = new GameObject($"Card_{card.Rank}_{card.Suit}");
                    cardObj.transform.SetParent(CardContainer, false);
                    RectTransform rt = cardObj.AddComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(cardW, cardH);

                    var (yOffA, rotZA) = GetFanOffset(i, count);
                    Vector2 pos = new Vector2(startX + i * spacing, yOffA);
                    rt.anchoredPosition = pos;
                    rt.localEulerAngles = new Vector3(0, 0, rotZA);

                    CardUI cardUI = cardObj.AddComponent<CardUI>();
                    cardUI.SetCard(card, isPlayable, onCardClick);
                    cardUI.SetBasePosition(pos);

                    if (animate)
                        cardUI.AnimateDealFrom(dealOrigin, pos, delay: (i - existingCount) * 0.07f, duration: 0.35f);

                    _spawnedCards.Add(cardUI);
                }
                return;
            }

            // Case 2b: Card played / removed — remove only the played card, slide remaining cards smoothly
            if (existingCount > 0 && count < existingCount)
            {
                // Find and remove the card(s) no longer in hand
                for (int i = _spawnedCards.Count - 1; i >= 0; i--)
                {
                    CardUI ui = _spawnedCards[i];
                    if (ui == null || ui.CurrentCard == null || !hand.Cards.Any(c => c.Equals(ui.CurrentCard)))
                    {
                        if (ui != null && ui.gameObject != null)
                        {
                            ui.transform.DOKill();
                            Destroy(ui.gameObject);
                        }
                        _spawnedCards.RemoveAt(i);
                    }
                }

                // Smoothly slide remaining cards to their new fan positions and update playability
                for (int i = 0; i < _spawnedCards.Count; i++)
                {
                    CardUI ui = _spawnedCards[i];
                    if (ui == null) continue;

                    Card card = ui.CurrentCard;
                    bool isPlayable = validPlays != null && card != null && validPlays.Contains(card);
                    ui.BindClick(onCardClick);
                    ui.SetPlayable(isPlayable);

                    var (yOff, rotZ) = GetFanOffset(i, count);
                    Vector2 targetPos = new Vector2(startX + i * spacing, yOff);
                    ui.SetBasePosition(targetPos);

                    RectTransform rt = ui.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.DOKill();
                        rt.DOAnchorPos(targetPos, 0.28f).SetEase(Ease.OutQuad);
                        rt.DORotate(new Vector3(0, 0, rotZ), 0.28f).SetEase(Ease.OutQuad);
                    }
                }
                return;
            }

            // Case 3: Fresh deal or full hand reset
            ClearCards();

            Vector2 origin = GetCenterOffsetInContainer();
            for (int i = 0; i < count; i++)
            {
                Card card = hand.Cards[i];
                bool isPlayable = validPlays != null && validPlays.Contains(card);

                GameObject cardObj = new GameObject($"Card_{card.Rank}_{card.Suit}");
                cardObj.transform.SetParent(CardContainer, false);
                RectTransform rt = cardObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cardW, cardH);

                var (yOff, rotZ) = GetFanOffset(i, count);
                Vector2 pos = new Vector2(startX + i * spacing, yOff);
                rt.anchoredPosition = pos;
                rt.localEulerAngles = new Vector3(0, 0, rotZ);

                CardUI cardUI = cardObj.AddComponent<CardUI>();
                cardUI.SetCard(card, isPlayable, onCardClick);
                cardUI.SetBasePosition(pos);

                if (animate)
                    cardUI.AnimateDealFrom(origin, pos, delay: i * 0.07f, duration: 0.35f);

                _spawnedCards.Add(cardUI);
            }
        }

        /// <summary>
        /// Renders AI partner/opponent remaining cards as mini card backs.
        /// When animate=true, backs fly in from the board center.
        /// </summary>
        public void RenderAICardCount(int cardCount, bool horizontal = true, bool animate = false)
        {
            if (!animate && cardCount == _spawnedCards.Count)
                return;

            // Common layout constants — computed once, used by both the smooth-remove
            // branch and the full-rebuild branch below.
            float cardW = AICardW;
            float cardH = AICardH;
            float spacing = horizontal ? 20f : 22f;
            float start = -(cardCount - 1) * spacing * 0.5f;

            // Smoothly remove played card and refan remaining cards without destroying/respawning
            if (!animate && _spawnedCards.Count > 0 && cardCount < _spawnedCards.Count)
            {
                int toRemove = _spawnedCards.Count - cardCount;
                for (int r = 0; r < toRemove; r++)
                {
                    int lastIdx = _spawnedCards.Count - 1;
                    CardUI ui = _spawnedCards[lastIdx];
                    if (ui != null && ui.gameObject != null)
                    {
                        ui.transform.DOKill();
                        Destroy(ui.gameObject);
                    }
                    _spawnedCards.RemoveAt(lastIdx);
                }

                if (cardCount <= 0) return;

                for (int i = 0; i < cardCount; i++)
                {
                    var (arcOffset, rotZ) = GetAIFanOffset(i, cardCount);
                    Vector2 pos = horizontal
                        ? new Vector2(start + i * spacing, arcOffset)
                        : new Vector2(arcOffset, start + i * spacing);

                    RectTransform rt = _spawnedCards[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.DOKill();
                        rt.DOAnchorPos(pos, 0.28f).SetEase(Ease.OutQuad);
                        rt.DORotate(new Vector3(0, 0, rotZ), 0.28f).SetEase(Ease.OutQuad);
                    }
                }
                return;
            }

            ClearCards();
            if (cardCount <= 0) return;

            Vector2 origin = GetCenterOffsetInContainer();

            for (int i = 0; i < cardCount; i++)
            {
                GameObject cardObj = new GameObject($"AICardBack_{i}");
                cardObj.transform.SetParent(CardContainer, false);
                RectTransform rt = cardObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cardW, cardH);

                var (arcOffset, rotZ) = GetAIFanOffset(i, cardCount);

                // Arc formation: cards spread along the main axis (X for a
                // horizontal row, Y for a vertical stack) at even spacing, with
                // a perpendicular arc offset + rotation from GetAIFanOffset so
                // the center card sits straight and the ones toward either end
                // curve/rotate outward — same fan treatment as the human hand.
                Vector2 pos = horizontal
                    ? new Vector2(start + i * spacing, arcOffset)
                    : new Vector2(arcOffset, start + i * spacing);

                rt.anchoredPosition = pos;
                rt.localEulerAngles = new Vector3(0, 0, rotZ);

                CardUI cardUI = cardObj.AddComponent<CardUI>();
                cardUI.SetFaceDown();
                _spawnedCards.Add(cardUI);

                if (animate)
                    cardUI.AnimateDealFrom(origin, pos, delay: i * 0.06f, duration: 0.30f);
            }
        }

        /// <summary>
        /// Computes the board center (canvas root world position) in this seat's
        /// CardContainer local space, so deal animations start from the center of the board.
        /// Falls back to Vector2.zero if the canvas root is unavailable.
        /// </summary>
        private Vector2 GetCenterOffsetInContainer()
        {
            RectTransform containerRT = CardContainer as RectTransform;
            if (containerRT == null) return Vector2.zero;

            // Walk up to find the root Canvas
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null) return Vector2.zero;

            RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
            if (canvasRT == null) return Vector2.zero;

            // Canvas center in world space
            Vector3 canvasCenter = canvasRT.TransformPoint(Vector3.zero);

            // Convert that world point into the CardContainer's local space
            Vector2 localCenter = containerRT.InverseTransformPoint(canvasCenter);
            return localCenter;
        }

        public void ClearCards()
        {
            for (int i = _spawnedCards.Count - 1; i >= 0; i--)
            {
                if (_spawnedCards[i] != null && _spawnedCards[i].gameObject != null)
                {
                    _spawnedCards[i].transform.DOKill();
                    Destroy(_spawnedCards[i].gameObject);
                }
            }
            _spawnedCards.Clear();

            // Safety: remove orphan card objects
            if (CardContainer != null)
            {
                foreach (Transform child in CardContainer)
                {
                    if (child != null)
                    {
                        child.DOKill();
                        Destroy(child.gameObject);
                    }
                }
            }
        }
    }
}