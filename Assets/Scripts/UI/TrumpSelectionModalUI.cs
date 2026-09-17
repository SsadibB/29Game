using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Interactive hover and elevation effect for trump selection cards.
    /// </summary>
    public class TrumpCardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject glowOutline;
        private Vector3 _originalScale = Vector3.one;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.DOKill();
            transform.DOScale(_originalScale * 1.06f, 0.16f).SetEase(Ease.OutQuad).SetLink(gameObject);
            if (glowOutline != null) glowOutline.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.DOKill();
            transform.DOScale(_originalScale, 0.16f).SetEase(Ease.OutQuad).SetLink(gameObject);
            if (glowOutline != null) glowOutline.SetActive(false);
        }
    }

    /// <summary>
    /// Full-screen Trump Selection modal overlay shown when Human (South) wins the bid.
    /// Displays 6 consistent, selectable card visuals (without bottom badges):
    ///   1. 2 of Spades (representing Spades)
    ///   2. 2 of Hearts (representing Hearts)
    ///   3. 2 of Clubs (representing Clubs)
    ///   4. 2 of Diamonds (representing Diamonds)
    ///   5. JOKER (Vertical J-O-K-E-R text corners, CenterGraphix image displaying "JOKER", no normal suit)
    ///   6. 7th Card (Dynamic suit display matching player's 7-rank card, using project identifier '7thCard')
    /// </summary>
    public class TrumpSelectionModalUI : MonoBehaviour
    {
        // ── Backdrop overlay ────────────────────────────────────────────────
        [SerializeField] private Image backdropImage;

        // ── Panel ──────────────────────────────────────────────────────────
        [SerializeField] private RectTransform panelRT;
        [SerializeField] private Image panelBg;

        // ── Header ─────────────────────────────────────────────────────────
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;

        // ── 6 Selectable Card Buttons ───────────────────────────────────────
        [SerializeField] private Button spadesBtn;
        [SerializeField] private Button heartsBtn;
        [SerializeField] private Button clubsBtn;
        [SerializeField] private Button diamondsBtn;
        [SerializeField] private Button jokerBtn;
        [SerializeField] private Button seventhCardBtn;

        // ── Legacy serialized fields kept for scene backwards compatibility ──
        [SerializeField] private Text dividerText;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            HookButtonListeners();
        }

        // ════════════════════════════════════════════════════════════════════
        // BUILD UI
        // ════════════════════════════════════════════════════════════════════

        public void EnsureComponents()
        {
            // Root RectTransform setup
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition3D = Vector3.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            // Full-screen semi-transparent backdrop
            if (backdropImage == null)
            {
                backdropImage = GetComponent<Image>();
                if (backdropImage == null) backdropImage = gameObject.AddComponent<Image>();
                backdropImage.color = new Color(0f, 0f, 0f, 0.82f);
                backdropImage.raycastTarget = true;
            }

            // Centered modal panel
            if (panelRT == null)
            {
                Transform existing = transform.Find("TrumpPanel");
                if (existing != null)
                {
                    panelRT = existing.GetComponent<RectTransform>();
                    panelBg = existing.GetComponent<Image>();
                }
            }

            if (panelRT == null)
            {
                GameObject panelGO = new GameObject("TrumpPanel");
                panelGO.transform.SetParent(transform, false);
                panelRT = panelGO.AddComponent<RectTransform>();
                panelRT.anchorMin = new Vector2(0.5f, 0.5f);
                panelRT.anchorMax = new Vector2(0.5f, 0.5f);
                panelRT.pivot = new Vector2(0.5f, 0.5f);

                panelBg = panelGO.AddComponent<Image>();
                panelBg.sprite = CardVisualTheme.TrumpBG;
                panelBg.type = Image.Type.Simple;
                panelBg.color = Color.white;
            }

            // Standardize panel geometry: 1100x420 holds all 6 cards horizontally with generous margins
            panelRT.sizeDelta = new Vector2(1100, 420);
            panelRT.anchoredPosition = new Vector2(0, 65);

            // Gold border — disabled for now
            Transform borderT = panelRT.Find("Border");
            if (borderT == null)
            {
                GameObject borderGO = new GameObject("Border");
                borderGO.transform.SetParent(panelRT, false);
                RectTransform brt = borderGO.AddComponent<RectTransform>();
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                Image borderImg = borderGO.AddComponent<Image>();
                borderImg.sprite = CardVisualTheme.CreateRoundedRectSprite(1100, 420, 24, Color.clear, CardVisualTheme.ColorGold, 2);
                borderImg.type = Image.Type.Sliced;
                borderImg.raycastTarget = false;
                borderGO.SetActive(false);
            }
            else
            {
                borderT.gameObject.SetActive(false);
            }

            // Detect and clear any legacy layout (e.g. old layout with bottom badges or old wide buttons)
            bool needsRebuild = false;
            Transform existingJoker = panelRT.Find("JOKER");
            if (existingJoker != null && (existingJoker.Find("BottomBadge") != null || existingJoker.Find("CornerTL/Suit") != null))
                needsRebuild = true;
            Transform existingSpades = panelRT.Find("2OfSpades");
            if (existingSpades != null && existingSpades.Find("BottomBadge") != null)
                needsRebuild = true;
            if (panelRT.Find("DividerLabel") != null || panelRT.Find("SeventhCardBtn") != null || panelRT.Find("HeartsBtn") != null)
                needsRebuild = true;

            if (needsRebuild)
            {
                for (int i = panelRT.childCount - 1; i >= 0; i--)
                {
                    Transform child = panelRT.GetChild(i);
                    if (child.name == "Border") continue;
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
                titleText = null;
                subtitleText = null;
                spadesBtn = null;
                heartsBtn = null;
                clubsBtn = null;
                diamondsBtn = null;
                jokerBtn = null;
                seventhCardBtn = null;
            }

            // Header Title
            if (titleText == null)
            {
                Transform t = panelRT.Find("Title");
                if (t != null) titleText = t.GetComponent<Text>();
            }
            if (titleText == null)
            {
                titleText = CreateText(panelRT, "Title",
                    new Vector2(0, -60), new Vector2(880, 42),
                    38, FontStyle.Bold, CardVisualTheme.ColorGold, TextAnchor.UpperCenter,
                    anchorPreset: new Vector2(0.5f, 1f)); // anchor preset: top-center
                titleText.text = "★  YOU WON THE BID!  ★";
            }

            // Header Subtitle
            if (subtitleText == null)
            {
                Transform st = panelRT.Find("Subtitle");
                if (st != null) subtitleText = st.GetComponent<Text>();
            }
            if (subtitleText == null)
            {
                subtitleText = CreateText(panelRT, "Subtitle",
                    new Vector2(0, 50), new Vector2(880, 26),
                    20, FontStyle.Normal, new Color(0.75f, 0.82f, 0.92f), TextAnchor.LowerCenter,
                    anchorPreset: new Vector2(0.5f, 0f)); // anchor preset: bottom-center
                subtitleText.text = "Select Trump: 4 Fixed Suits, JOKER, or dynamic 7th Card";
            }

            // Separator line
            if (panelRT.Find("Separator") == null)
            {
                CreateSeparator(panelRT, new Vector2(0, 84), new Vector2(880, 2));
            }

            // ── The 6 Selectable Card Visuals (No Bottom Badges) ──
            // Card dimensions: 130x190. Spacing: 18px.
            // X-centers: -370, -222, -74, +74, +222, +370. Centered Y = -25.
            if (spadesBtn == null)
            {
                Transform t = panelRT.Find("2OfSpades");
                if (t != null) spadesBtn = t.GetComponent<Button>();
                if (spadesBtn == null)
                {
                    spadesBtn = CreateCardButton(panelRT, "2OfSpades", new Vector2(-370, -25),
                        "2", CardVisualTheme.GetSuitSymbol(Suit.Spades), CardVisualTheme.GetSuitColor(Suit.Spades),
                        CardVisualTheme.GetSuitSprite(Suit.Spades), CardVisualTheme.GetSuitSymbol(Suit.Spades));
                }
            }

            if (heartsBtn == null)
            {
                Transform t = panelRT.Find("2OfHearts");
                if (t != null) heartsBtn = t.GetComponent<Button>();
                if (heartsBtn == null)
                {
                    heartsBtn = CreateCardButton(panelRT, "2OfHearts", new Vector2(-222, -25),
                        "2", CardVisualTheme.GetSuitSymbol(Suit.Hearts), CardVisualTheme.GetSuitColor(Suit.Hearts),
                        CardVisualTheme.GetSuitSprite(Suit.Hearts), CardVisualTheme.GetSuitSymbol(Suit.Hearts));
                }
            }

            if (clubsBtn == null)
            {
                Transform t = panelRT.Find("2OfClubs");
                if (t != null) clubsBtn = t.GetComponent<Button>();
                if (clubsBtn == null)
                {
                    clubsBtn = CreateCardButton(panelRT, "2OfClubs", new Vector2(-74, -25),
                        "2", CardVisualTheme.GetSuitSymbol(Suit.Clubs), CardVisualTheme.GetSuitColor(Suit.Clubs),
                        CardVisualTheme.GetSuitSprite(Suit.Clubs), CardVisualTheme.GetSuitSymbol(Suit.Clubs));
                }
            }

            if (diamondsBtn == null)
            {
                Transform t = panelRT.Find("2OfDiamonds");
                if (t != null) diamondsBtn = t.GetComponent<Button>();
                if (diamondsBtn == null)
                {
                    diamondsBtn = CreateCardButton(panelRT, "2OfDiamonds", new Vector2(74, -25),
                        "2", CardVisualTheme.GetSuitSymbol(Suit.Diamonds), CardVisualTheme.GetSuitColor(Suit.Diamonds),
                        CardVisualTheme.GetSuitSprite(Suit.Diamonds), CardVisualTheme.GetSuitSymbol(Suit.Diamonds));
                }
            }

            if (jokerBtn == null)
            {
                Transform t = panelRT.Find("JOKER");
                if (t != null) jokerBtn = t.GetComponent<Button>();
                if (jokerBtn == null)
                {
                    jokerBtn = CreateJokerCardButton(panelRT, new Vector2(222, -25));
                }
            }

            if (seventhCardBtn == null)
            {
                Transform t = panelRT.Find("7thCard");
                if (t != null) seventhCardBtn = t.GetComponent<Button>();
                if (seventhCardBtn == null)
                {
                    seventhCardBtn = CreateCardButton(panelRT, "7thCard", new Vector2(370, -25),
                        "7", CardVisualTheme.GetSuitSymbol(Suit.Hearts), CardVisualTheme.GetSuitColor(Suit.Hearts),
                        CardVisualTheme.GetSuitSprite(Suit.Hearts), CardVisualTheme.GetSuitSymbol(Suit.Hearts));
                }
            }

            HookButtonListeners();
        }

        private void HookButtonListeners()
        {
            HookSuit(spadesBtn, Suit.Spades);
            HookSuit(heartsBtn, Suit.Hearts);
            HookSuit(clubsBtn, Suit.Clubs);
            HookSuit(diamondsBtn, Suit.Diamonds);

            if (jokerBtn != null)
            {
                jokerBtn.onClick.RemoveAllListeners();
                jokerBtn.onClick.AddListener(OnJokerSelected);
            }

            if (seventhCardBtn != null)
            {
                seventhCardBtn.onClick.RemoveAllListeners();
                seventhCardBtn.onClick.AddListener(OnSeventhCardSelected);
            }
        }

        private void HookSuit(Button btn, Suit suit)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnSuitSelected(suit, btn));
        }

        // ════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ════════════════════════════════════════════════════════════════════

        public void Show(int winningBid, Hand hand)
        {
            EnsureComponents();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            // Clear any leftover glow from the previous Show() or a hover that
            // never got its OnPointerExit fired due to a fast click-dismiss.
            ResetAllHighlights();

            if (titleText != null)
                titleText.text = $"★  YOU WON THE BID  ( {winningBid} )  ★";
            if (subtitleText != null)
                subtitleText.text = "Select Trump: 4 Fixed Suits, JOKER, or dynamic 7th Card";

            // Dynamically determine the 7th Card's suit from the 7-number card owned by player
            Card sevenCard = hand?.Cards != null ? hand.Cards.FirstOrDefault(c => c.Rank == Rank.Seven) : null;
            Suit dynamicSuit = sevenCard != null ? sevenCard.Suit : Suit.Hearts;
            UpdateSeventhCardVisual(dynamicSuit);

            // Backdrop fade in
            if (backdropImage != null)
            {
                backdropImage.raycastTarget = true;
                backdropImage.color = new Color(0f, 0f, 0f, 0f);
                backdropImage.DOFade(0.82f, 0.2f).SetLink(gameObject);
            }

            // Panel punch-in
            if (panelRT != null)
            {
                panelRT.DOKill();
                panelRT.localScale = Vector3.one * 0.7f;
                panelRT.DOScale(1f, 0.32f).SetEase(Ease.OutBack).SetLink(gameObject);
            }

            // Staggered card entrance animations across the 6 cards
            AnimateButtonEntrance(spadesBtn, 0.05f);
            AnimateButtonEntrance(heartsBtn, 0.10f);
            AnimateButtonEntrance(clubsBtn, 0.15f);
            AnimateButtonEntrance(diamondsBtn, 0.20f);
            AnimateButtonEntrance(jokerBtn, 0.25f);
            AnimateButtonEntrance(seventhCardBtn, 0.30f);
        }

        public void Hide()
        {
            // Ensure all hover glows are off when the modal closes so no
            // button stays lit for the next time the modal is shown.
            ResetAllHighlights();

            transform.DOKill();
            if (panelRT != null) panelRT.DOKill();

            if (backdropImage != null)
                backdropImage.raycastTarget = false;

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Deactivates the GlowOutline child on every selectable card button.
        /// Call this when the modal opens or closes to clear any stale hover state.
        /// </summary>
        private void ResetAllHighlights()
        {
            ResetButtonGlow(spadesBtn);
            ResetButtonGlow(heartsBtn);
            ResetButtonGlow(clubsBtn);
            ResetButtonGlow(diamondsBtn);
            ResetButtonGlow(jokerBtn);
            ResetButtonGlow(seventhCardBtn);
        }

        private static void ResetButtonGlow(Button btn)
        {
            if (btn == null) return;
            Transform glow = btn.transform.Find("GlowOutline");
            if (glow != null) glow.gameObject.SetActive(false);
            // Also reset the scale in case a DOTween hover scale is in flight.
            btn.transform.DOKill();
            btn.transform.localScale = Vector3.one;
        }

        // ════════════════════════════════════════════════════════════════════
        // BUTTON HANDLERS
        // ════════════════════════════════════════════════════════════════════

        private void OnSuitSelected(Suit suit, Button btn)
        {
            Debug.Log($"[29 TrumpSelection] Human South selected Trump: {suit}");
            if (btn != null)
                btn.transform.DOPunchScale(Vector3.one * 0.18f, 0.2f, 10, 1).SetLink(gameObject);

            Hide();
            GameManager.Instance.SelectHumanTrump(suit);
        }

        private void OnSeventhCardSelected()
        {
            Debug.Log("[29 TrumpSelection] Human South selected 7th Card!");
            if (seventhCardBtn != null)
                seventhCardBtn.transform.DOPunchScale(Vector3.one * 0.18f, 0.2f, 10, 1).SetLink(gameObject);

            Hide();
            GameManager.Instance.SelectHumanSeventhCard();
        }

        private void OnJokerSelected()
        {
            Debug.Log("[29 TrumpSelection] Human South selected Joker!");
            if (jokerBtn != null)
                jokerBtn.transform.DOPunchScale(Vector3.one * 0.18f, 0.2f, 10, 1).SetLink(gameObject);

            Hide();
            GameManager.Instance.SelectHumanJoker();
        }

        // ════════════════════════════════════════════════════════════════════
        // DYNAMIC UPDATES & CARD BUILDERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Dynamically updates the 7th Card visual to match the player's 7-rank card suit.
        /// </summary>
        public void UpdateSeventhCardVisual(Suit suit)
        {
            if (seventhCardBtn == null) return;
            Transform t = seventhCardBtn.transform;
            Color suitCol = CardVisualTheme.GetSuitColor(suit);
            string sym = CardVisualTheme.GetSuitSymbol(suit);

            // Corner Top-Left
            Transform tl = t.Find("CornerTL");
            if (tl != null)
            {
                Text rTxt = tl.Find("Rank")?.GetComponent<Text>();
                if (rTxt != null) { rTxt.text = "7"; rTxt.color = suitCol; }
                Text sTxt = tl.Find("Suit")?.GetComponent<Text>();
                if (sTxt != null) { sTxt.text = sym; sTxt.color = suitCol; }
            }

            // Corner Bottom-Right
            Transform br = t.Find("CornerBR");
            if (br != null)
            {
                Text rTxt = br.Find("Rank")?.GetComponent<Text>();
                if (rTxt != null) { rTxt.text = "7"; rTxt.color = suitCol; }
                Text sTxt = br.Find("Suit")?.GetComponent<Text>();
                if (sTxt != null) { sTxt.text = sym; sTxt.color = suitCol; }
            }

            // Center Graphic
            Sprite suitSprite = CardVisualTheme.GetSuitSprite(suit);
            Image centerImg = t.Find("CenterGraphix")?.GetComponent<Image>() ?? t.Find("CenterGraphic")?.GetComponent<Image>();
            Text centerTxt = t.Find("CenterText")?.GetComponent<Text>();

            if (suitSprite != null && centerImg != null)
            {
                centerImg.sprite = suitSprite;
                centerImg.color = Color.white;
                centerImg.gameObject.SetActive(true);
                if (centerTxt != null) centerTxt.gameObject.SetActive(false);
            }
            else if (centerTxt != null)
            {
                centerTxt.text = sym;
                centerTxt.color = suitCol;
                centerTxt.gameObject.SetActive(true);
                if (centerImg != null) centerImg.gameObject.SetActive(false);
            }
        }

        private void AnimateButtonEntrance(Button btn, float delay)
        {
            if (btn == null) return;
            btn.transform.DOKill();
            btn.transform.localScale = Vector3.one * 0.4f;
            btn.transform.DOScale(1f, 0.24f).SetDelay(delay).SetEase(Ease.OutBack).SetLink(gameObject);
        }

        /// <summary>
        /// Builds standard card button (130x190) with corner rank+suit and centered artwork,
        /// without bottom badges.
        /// </summary>
        private Button CreateCardButton(RectTransform parent, string goName, Vector2 pos,
            string rankText, string suitSymbol, Color suitColor, Sprite centerSprite, string centerTextFallback)
        {
            GameObject cardGO = new GameObject(goName);
            cardGO.transform.SetParent(parent, false);
            RectTransform rt = cardGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(130, 190);
            rt.anchoredPosition = pos;

            // Card Face Background
            Image bg = cardGO.AddComponent<Image>();
            bg.sprite = CardVisualTheme.CardFront;
            bg.color = Color.white;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.raycastTarget = true;

            // Card Border Outline
            GameObject borderGO = new GameObject("CardBorder");
            borderGO.transform.SetParent(cardGO.transform, false);
            RectTransform brt = borderGO.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            Image borderImg = borderGO.AddComponent<Image>();
            borderImg.sprite = CardVisualTheme.CreateRoundedRectSprite(130, 190, 12, Color.clear, new Color(0.85f, 0.70f, 0.28f, 0.6f), 2);
            borderImg.type = Image.Type.Sliced;
            borderImg.raycastTarget = false;

            // Glow Outline on Hover
            GameObject glowGO = new GameObject("GlowOutline");
            glowGO.transform.SetParent(cardGO.transform, false);
            RectTransform grt = glowGO.AddComponent<RectTransform>();
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(-6, -6);
            grt.offsetMax = new Vector2(6, 6);
            Image glowImg = glowGO.AddComponent<Image>();
            glowImg.sprite = CardVisualTheme.CreateRoundedRectSprite(142, 202, 16, Color.clear, CardVisualTheme.ColorGold, 4);
            glowImg.type = Image.Type.Sliced;
            glowImg.raycastTarget = false;
            glowGO.SetActive(false);

            // Corner Indicators (Top-Left & Bottom-Right)
            CreateCornerIndicator(rt, "CornerTL", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(11, -8), rankText, suitSymbol, suitColor, TextAnchor.UpperLeft);

            CreateCornerIndicator(rt, "CornerBR", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-11, 8), rankText, suitSymbol, suitColor, TextAnchor.LowerRight);

            // Center Artwork Graphic
            GameObject centerGO = new GameObject("CenterGraphix");
            centerGO.transform.SetParent(cardGO.transform, false);
            RectTransform crt = centerGO.AddComponent<RectTransform>();
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(64, 64);
            Image centerImg = centerGO.AddComponent<Image>();
            centerImg.preserveAspect = true;
            centerImg.raycastTarget = false;

            // Center Artwork Fallback Text
            GameObject centerTxtGO = new GameObject("CenterText");
            centerTxtGO.transform.SetParent(cardGO.transform, false);
            RectTransform ctrt = centerTxtGO.AddComponent<RectTransform>();
            ctrt.anchoredPosition = Vector2.zero;
            ctrt.sizeDelta = new Vector2(64, 64);
            Text centerTxt = centerTxtGO.AddComponent<Text>();
            centerTxt.font = CardVisualTheme.GetFont();
            centerTxt.fontSize = 54;
            centerTxt.fontStyle = FontStyle.Normal;
            centerTxt.alignment = TextAnchor.MiddleCenter;
            centerTxt.color = suitColor;
            centerTxt.raycastTarget = false;

            if (centerSprite != null)
            {
                centerImg.sprite = centerSprite;
                centerImg.color = Color.white;
                centerImg.gameObject.SetActive(true);
                centerTxt.gameObject.SetActive(false);
            }
            else
            {
                centerImg.gameObject.SetActive(false);
                centerTxt.text = centerTextFallback;
                centerTxt.gameObject.SetActive(true);
            }

            // Button Component
            Button btn = cardGO.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            cb.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;

            // Hover Elevation and Glow Effect
            TrumpCardHoverEffect hover = cardGO.AddComponent<TrumpCardHoverEffect>();
            hover.glowOutline = glowGO;

            return btn;
        }

        /// <summary>
        /// Creates the special JOKER card:
        /// - Identifier: 'JOKER'
        /// - CornerTL & CornerBR display vertical "J\nO\nK\nE\nR" with no suit icon
        /// - CenterGraphix displays "JOKER"
        /// - No normal suit assigned
        /// - No bottom badge
        /// </summary>
        private Button CreateJokerCardButton(RectTransform parent, Vector2 pos)
        {
            GameObject cardGO = new GameObject("JOKER");
            cardGO.transform.SetParent(parent, false);
            RectTransform rt = cardGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(130, 190);
            rt.anchoredPosition = pos;

            // Card Face Background
            Image bg = cardGO.AddComponent<Image>();
            bg.sprite = CardVisualTheme.CardFront;
            bg.color = Color.white;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.raycastTarget = true;

            // Card Border Outline
            GameObject borderGO = new GameObject("CardBorder");
            borderGO.transform.SetParent(cardGO.transform, false);
            RectTransform brt = borderGO.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            Image borderImg = borderGO.AddComponent<Image>();
            borderImg.sprite = CardVisualTheme.CreateRoundedRectSprite(130, 190, 12, Color.clear, CardVisualTheme.ColorGold, 2);
            borderImg.type = Image.Type.Sliced;
            borderImg.raycastTarget = false;

            // Glow Outline on Hover
            GameObject glowGO = new GameObject("GlowOutline");
            glowGO.transform.SetParent(cardGO.transform, false);
            RectTransform grt = glowGO.AddComponent<RectTransform>();
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(-6, -6);
            grt.offsetMax = new Vector2(6, 6);
            Image glowImg = glowGO.AddComponent<Image>();
            glowImg.sprite = CardVisualTheme.CreateRoundedRectSprite(142, 202, 16, Color.clear, CardVisualTheme.ColorGold, 4);
            glowImg.type = Image.Type.Sliced;
            glowImg.raycastTarget = false;
            glowGO.SetActive(false);

            // ── CornerTL: Vertical "J\nO\nK\nE\nR" with NO suit icon ──
            CreateJokerCornerText(rt, "CornerTL", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(11, -8), TextAnchor.UpperLeft);

            // ── CornerBR: Vertical "J\nO\nK\nE\nR" with NO suit icon ──
            CreateJokerCornerText(rt, "CornerBR", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-11, 8), TextAnchor.LowerRight);

            // ── CenterGraphix: Image component set to the JOKER named resource sprite, no children ──
            GameObject centerGO = new GameObject("CenterGraphix");
            centerGO.transform.SetParent(cardGO.transform, false);
            RectTransform crt = centerGO.AddComponent<RectTransform>();
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(96, 96);

            Image centerImg = centerGO.AddComponent<Image>();
            Sprite jokerSprite = Resources.Load<Sprite>("JOKER");
            if (jokerSprite == null)
            {
                // Fallback: try as Texture2D and wrap
                Texture2D jokerTex = Resources.Load<Texture2D>("JOKER");
                if (jokerTex != null)
                    jokerSprite = Sprite.Create(jokerTex, new Rect(0, 0, jokerTex.width, jokerTex.height), new Vector2(0.5f, 0.5f));
            }
            centerImg.sprite = jokerSprite;
            centerImg.color = Color.white;
            centerImg.preserveAspect = true;
            centerImg.type = Image.Type.Simple;
            centerImg.raycastTarget = false;


            // Button Component
            Button btn = cardGO.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            cb.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;

            // Hover Effect
            TrumpCardHoverEffect hover = cardGO.AddComponent<TrumpCardHoverEffect>();
            hover.glowOutline = glowGO;

            return btn;
        }

        /// <summary>
        /// Helper to create vertical J-O-K-E-R text in card corner without suit icon.
        /// </summary>
        private void CreateJokerCornerText(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, TextAnchor alignment)
        {
            GameObject cornerGO = new GameObject(name);
            cornerGO.transform.SetParent(parent, false);
            RectTransform rt = cornerGO.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(24, 88);

            GameObject rankGO = new GameObject("Rank");
            rankGO.transform.SetParent(cornerGO.transform, false);
            RectTransform rrt = rankGO.AddComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;

            Text rTxt = rankGO.AddComponent<Text>();
            rTxt.font = CardVisualTheme.GetFont();
            rTxt.fontSize = 12;
            rTxt.fontStyle = FontStyle.Bold;
            rTxt.lineSpacing = 0.85f;
            rTxt.alignment = alignment;
            rTxt.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            rTxt.text = "J\nO\nK\nE\nR";
            rTxt.raycastTarget = false;
        }

        private void CreateCornerIndicator(RectTransform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 pos, string rank, string suitSym, Color color, TextAnchor alignment)
        {
            GameObject cornerGO = new GameObject(name);
            cornerGO.transform.SetParent(parent, false);
            RectTransform rt = cornerGO.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(34, 48);

            // Rank
            GameObject rankGO = new GameObject("Rank");
            rankGO.transform.SetParent(cornerGO.transform, false);
            RectTransform rrt = rankGO.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 1f);
            rrt.anchorMax = new Vector2(0.5f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = Vector2.zero;
            rrt.sizeDelta = new Vector2(34, 24);
            Text rTxt = rankGO.AddComponent<Text>();
            rTxt.font = CardVisualTheme.GetFont();
            rTxt.fontSize = 20;
            rTxt.fontStyle = FontStyle.Bold;
            rTxt.alignment = alignment;
            rTxt.color = color;
            rTxt.text = rank;
            rTxt.raycastTarget = false;

            // Suit
            GameObject suitGO = new GameObject("Suit");
            suitGO.transform.SetParent(cornerGO.transform, false);
            RectTransform srt = suitGO.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0f);
            srt.anchorMax = new Vector2(0.5f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(34, 22);
            Text sTxt = suitGO.AddComponent<Text>();
            sTxt.font = CardVisualTheme.GetFont();
            sTxt.fontSize = 18;
            sTxt.fontStyle = FontStyle.Normal;
            sTxt.alignment = alignment;
            sTxt.color = color;
            sTxt.text = suitSym;
            sTxt.raycastTarget = false;
        }

        private void CreateSeparator(RectTransform parent, Vector2 pos, Vector2 size)
        {
            GameObject sep = new GameObject("Separator");
            sep.transform.SetParent(parent, false);
            RectTransform rt = sep.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = sep.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.12f);
            img.raycastTarget = false;
        }

        private Text CreateText(RectTransform parent, string name, Vector2 pos, Vector2 size,
            int fontSize, FontStyle style, Color color, TextAnchor alignment = TextAnchor.MiddleCenter,
            Vector2? anchorPreset = null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            Vector2 anchor = anchorPreset ?? new Vector2(0.5f, 0.5f);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text txt = obj.AddComponent<Text>();
            txt.font = CardVisualTheme.GetFont();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = alignment;
            txt.color = color;
            txt.raycastTarget = false;
            return txt;
        }
    }
}