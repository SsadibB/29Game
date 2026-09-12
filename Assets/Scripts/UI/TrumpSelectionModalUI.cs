using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Full-screen Trump Selection overlay shown when Human (South) wins the bid.
    /// Layout:
    ///   ┌─────────────── full screen overlay ──────────────────┐
    ///   │  ╔══════════════ glass panel ═══════════════╗        │
    ///   │  ║  ★ YOU WON THE BID (17)! ★               ║        │
    ///   │  ║  Choose your trump carefully              ║        │
    ///   │  ║                                           ║        │
    ///   │  ║  [♥ Hearts] [♦ Diamonds] [♣ Clubs] [♠ Spades]  ║  │
    ///   │  ║     3 cards     1 card    2 cards    0 cards    ║  │
    ///   │  ║                                           ║        │
    ///   │  ║  [🎴 7TH CARD - Blind Mystery Trump]      ║        │
    ///   │  ║  [🃏 JOKER   - No Trump Mode      ]       ║        │
    ///   │  ╚═══════════════════════════════════════════╝        │
    ///   └──────────────────────────────────────────────────────┘
    /// </summary>
    public class TrumpSelectionModalUI : MonoBehaviour
    {
        // ── Backdrop overlay ────────────────────────────────────────────────
        [SerializeField] private Image  backdropImage;

        // ── Panel ──────────────────────────────────────────────────────────
        [SerializeField] private RectTransform panelRT;
        [SerializeField] private Image         panelBg;

        // ── Header ─────────────────────────────────────────────────────────
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;

        // ── Suit buttons ───────────────────────────────────────────────────
        [SerializeField] private Button heartsBtn;
        [SerializeField] private Button diamondsBtn;
        [SerializeField] private Button clubsBtn;
        [SerializeField] private Button spadesBtn;

        // ── Special mode buttons ────────────────────────────────────────────
        [SerializeField] private Button seventhCardBtn;
        [SerializeField] private Button jokerBtn;

        // ── Divider text ───────────────────────────────────────────────────
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
            // This component lives on the root object which is full-screen
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // ── Full-screen semi-transparent backdrop ──
            if (backdropImage == null)
            {
                backdropImage = GetComponent<Image>();
                if (backdropImage == null) backdropImage = gameObject.AddComponent<Image>();
                backdropImage.color = new Color(0f, 0f, 0f, 0.82f);
                backdropImage.raycastTarget = true;
            }

            // ── Centered glass panel ──
            if (panelRT == null)
            {
                GameObject panelGO = new GameObject("TrumpPanel");
                panelGO.transform.SetParent(transform, false);
                panelRT = panelGO.AddComponent<RectTransform>();
                panelRT.anchorMin = new Vector2(0.5f, 0.5f);
                panelRT.anchorMax = new Vector2(0.5f, 0.5f);
                panelRT.pivot     = new Vector2(0.5f, 0.5f);
                panelRT.sizeDelta = new Vector2(580, 520);
                panelRT.anchoredPosition = Vector2.zero;

                panelBg = panelGO.AddComponent<Image>();
                panelBg.sprite = CardVisualTheme.RoundedPanel;
                panelBg.type   = Image.Type.Sliced;
                panelBg.color  = new Color(0.06f, 0.09f, 0.16f, 0.97f);

                // Gold border outline
                GameObject borderGO = new GameObject("Border");
                borderGO.transform.SetParent(panelGO.transform, false);
                RectTransform brt = borderGO.AddComponent<RectTransform>();
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                Image borderImg = borderGO.AddComponent<Image>();
                borderImg.sprite = CardVisualTheme.CreateRoundedRectSprite(580, 520, 28, Color.clear, CardVisualTheme.ColorGold, 2);
                borderImg.type = Image.Type.Sliced;
                borderImg.raycastTarget = false;
            }

            // ── Header ──
            if (titleText == null)
            {
                titleText = CreateText(panelRT, "Title",
                    new Vector2(0, 215), new Vector2(540, 46),
                    24, FontStyle.Bold, CardVisualTheme.ColorGold);
                titleText.text = "★  YOU WON THE BID!  ★";
            }

            if (subtitleText == null)
            {
                subtitleText = CreateText(panelRT, "Subtitle",
                    new Vector2(0, 178), new Vector2(540, 28),
                    14, FontStyle.Normal, new Color(0.75f, 0.82f, 0.92f));
                subtitleText.text = "Select Trump Suit — or choose a special mode below";
            }

            // ── Thin separator ──
            CreateSeparator(panelRT, new Vector2(0, 154), new Vector2(510, 2));

            // ── Suit card buttons (top row) ──
            if (heartsBtn == null)
            {
                heartsBtn   = CreateSuitBtn(panelRT, "HeartsBtn",   new Vector2(-195, 45), Suit.Hearts);
                diamondsBtn = CreateSuitBtn(panelRT, "DiamondsBtn", new Vector2(-65,  45), Suit.Diamonds);
                clubsBtn    = CreateSuitBtn(panelRT, "ClubsBtn",    new Vector2( 65,  45), Suit.Clubs);
                spadesBtn   = CreateSuitBtn(panelRT, "SpadesBtn",   new Vector2( 195, 45), Suit.Spades);
            }

            // ── Divider label ──
            if (dividerText == null)
            {
                dividerText = CreateText(panelRT, "DividerLabel",
                    new Vector2(0, -68), new Vector2(540, 24),
                    12, FontStyle.Bold, new Color(0.5f, 0.55f, 0.65f));
                dividerText.text = "─────────   SPECIAL MODES   ─────────";
            }

            // ── Special mode buttons (bottom row) ──
            if (seventhCardBtn == null)
            {
                seventhCardBtn = CreateSpecialBtn(panelRT, "SeventhCardBtn",
                    new Vector2(0, -120),
                    "🎴",
                    "7TH CARD",
                    "Blind mystery trump — revealed from 2nd deal",
                    new Color(0.98f, 0.72f, 0.15f),
                    new Color(0.16f, 0.12f, 0.04f, 0.95f));

                jokerBtn = CreateSpecialBtn(panelRT, "JokerBtn",
                    new Vector2(0, -190),
                    "🃏",
                    "JOKER  —  NO TRUMP",
                    "Highest card of the led suit always wins",
                    new Color(0.45f, 0.88f, 1.0f),
                    new Color(0.04f, 0.12f, 0.18f, 0.95f));
            }

            HookButtonListeners();
        }

        private void HookButtonListeners()
        {
            HookSuit(heartsBtn,   Suit.Hearts);
            HookSuit(diamondsBtn, Suit.Diamonds);
            HookSuit(clubsBtn,    Suit.Clubs);
            HookSuit(spadesBtn,   Suit.Spades);

            if (seventhCardBtn != null)
            {
                seventhCardBtn.onClick.RemoveAllListeners();
                seventhCardBtn.onClick.AddListener(OnSeventhCardSelected);
            }
            if (jokerBtn != null)
            {
                jokerBtn.onClick.RemoveAllListeners();
                jokerBtn.onClick.AddListener(OnJokerSelected);
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

            if (titleText != null)
                titleText.text = $"★  YOU WON THE BID  ( {winningBid} )  ★";

            // Update suit card counts
            UpdateSuitBtn(heartsBtn,   Suit.Hearts,   hand);
            UpdateSuitBtn(diamondsBtn, Suit.Diamonds, hand);
            UpdateSuitBtn(clubsBtn,    Suit.Clubs,    hand);
            UpdateSuitBtn(spadesBtn,   Suit.Spades,   hand);

            // Backdrop fade in
            if (backdropImage != null)
            {
                backdropImage.color = new Color(0f, 0f, 0f, 0f);
                backdropImage.DOFade(0.82f, 0.2f).SetLink(gameObject);
            }

            // Panel punch-in from center
            if (panelRT != null)
            {
                panelRT.DOKill();
                panelRT.localScale = Vector3.one * 0.7f;
                panelRT.DOScale(1f, 0.32f).SetEase(Ease.OutBack).SetLink(gameObject);
            }

            // Staggered button entrance animations
            AnimateButtonEntrance(heartsBtn,      0.08f);
            AnimateButtonEntrance(diamondsBtn,    0.14f);
            AnimateButtonEntrance(clubsBtn,       0.20f);
            AnimateButtonEntrance(spadesBtn,      0.26f);
            AnimateButtonEntrance(seventhCardBtn, 0.33f);
            AnimateButtonEntrance(jokerBtn,       0.39f);
        }

        public void Hide()
        {
            // If not visible, just deactivate immediately (e.g. initial setup call)
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(false);
                return;
            }

            transform.DOKill();
            if (panelRT != null) panelRT.DOKill();

            if (backdropImage != null)
                backdropImage.DOFade(0f, 0.15f).SetLink(gameObject);

            if (panelRT != null)
            {
                panelRT.DOScale(0.85f, 0.18f).SetEase(Ease.InQuad).SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        if (this != null && gameObject != null)
                            gameObject.SetActive(false);
                    });
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // BUTTON HANDLERS
        // ════════════════════════════════════════════════════════════════════

        private void OnSuitSelected(Suit suit, Button btn)
        {
            Debug.Log($"[29 TrumpSelection] Human South selected Trump: {suit}");
            if (btn != null)
                btn.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 10, 1).SetLink(gameObject);

            Hide();
            GameManager.Instance.SelectHumanTrump(suit);
        }

        private void OnSeventhCardSelected()
        {
            Debug.Log("[29 TrumpSelection] Human South selected 7th Card!");
            if (seventhCardBtn != null)
                seventhCardBtn.transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 10, 1).SetLink(gameObject);

            Hide();
            GameManager.Instance.SelectHumanSeventhCard();
        }

        private void OnJokerSelected()
        {
            Debug.Log("[29 TrumpSelection] Human South selected Joker (No-Trump)!");
            if (jokerBtn != null)
                jokerBtn.transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 10, 1).SetLink(gameObject);

            Hide();
            GameManager.Instance.SelectHumanJoker();
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        private void UpdateSuitBtn(Button btn, Suit suit, Hand hand)
        {
            if (btn == null) return;
            int count = hand != null ? hand.GetCardsBySuit(suit).Count : 0;

            // Find the count sub-text
            Text countTxt = btn.transform.Find("Count")?.GetComponent<Text>();
            if (countTxt != null)
            {
                countTxt.text = count > 0 ? $"{count} in hand" : "none";
                countTxt.color = count > 0 ? new Color(0.75f, 0.95f, 0.75f) : new Color(0.5f, 0.5f, 0.55f);
            }

            // Dim buttons with no cards
            Image bg = btn.GetComponent<Image>();
            if (bg != null)
                bg.color = count > 0
                    ? new Color(0.12f, 0.18f, 0.28f, 0.97f)
                    : new Color(0.08f, 0.10f, 0.15f, 0.6f);
        }

        private void AnimateButtonEntrance(Button btn, float delay)
        {
            if (btn == null) return;
            btn.transform.DOKill();
            btn.transform.localScale = Vector3.one * 0.5f;
            btn.transform.DOScale(1f, 0.22f).SetDelay(delay).SetEase(Ease.OutBack).SetLink(gameObject);
        }

        // ════════════════════════════════════════════════════════════════════
        // WIDGET BUILDERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Creates a large suit card button with symbol, suit name and card count.</summary>
        private Button CreateSuitBtn(RectTransform parent, string name, Vector2 pos, Suit suit)
        {
            string sym      = CardVisualTheme.GetSuitSymbol(suit);
            Color  suitCol  = CardVisualTheme.GetSuitColor(suit);
            string suitName = CardVisualTheme.GetSuitName(suit).ToUpper();

            GameObject btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            RectTransform rt = btnGO.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta        = new Vector2(118, 148);

            // Background card shape
            Image bg = btnGO.AddComponent<Image>();
            bg.sprite         = CardVisualTheme.RoundedCardSlot;
            bg.type           = Image.Type.Sliced;
            bg.color          = new Color(0.12f, 0.18f, 0.28f, 0.97f);
            bg.raycastTarget  = true;

            // Colored left accent strip
            GameObject accent = new GameObject("Accent");
            accent.transform.SetParent(btnGO.transform, false);
            RectTransform art = accent.AddComponent<RectTransform>();
            art.anchorMin = new Vector2(0, 0);
            art.anchorMax = new Vector2(0, 1);
            art.offsetMin = Vector2.zero;
            art.offsetMax = new Vector2(5, 0);
            Image accentImg = accent.AddComponent<Image>();
            accentImg.color = new Color(suitCol.r, suitCol.g, suitCol.b, 0.85f);
            accentImg.raycastTarget = false;

            // Suit symbol (large, centered top)
            Text symTxt = CreateText(rt, "Symbol", new Vector2(0, 28), new Vector2(110, 68),
                52, FontStyle.Bold, suitCol);
            symTxt.text = sym;

            // Suit name
            Text nameTxt = CreateText(rt, "Name", new Vector2(0, -22), new Vector2(110, 26),
                13, FontStyle.Bold, Color.white);
            nameTxt.text = suitName;

            // Card count
            Text countTxt = CreateText(rt, "Count", new Vector2(0, -46), new Vector2(110, 22),
                11, FontStyle.Normal, new Color(0.7f, 0.85f, 0.7f));
            countTxt.text = "0 in hand";

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = bg;

            // Button tint colors
            ColorBlock cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cb;

            return btn;
        }

        /// <summary>Creates a wide special mode button (7th Card / Joker).</summary>
        private Button CreateSpecialBtn(RectTransform parent, string name, Vector2 pos,
            string emoji, string label, string description, Color accentColor, Color bgColor)
        {
            GameObject btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            RectTransform rt = btnGO.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta        = new Vector2(510, 58);

            Image bg = btnGO.AddComponent<Image>();
            bg.sprite         = CardVisualTheme.RoundedCardSlot;
            bg.type           = Image.Type.Sliced;
            bg.color          = bgColor;
            bg.raycastTarget  = true;

            // Left accent bar
            GameObject accent = new GameObject("Accent");
            accent.transform.SetParent(btnGO.transform, false);
            RectTransform art = accent.AddComponent<RectTransform>();
            art.anchorMin = new Vector2(0, 0);
            art.anchorMax = new Vector2(0, 1);
            art.offsetMin = Vector2.zero;
            art.offsetMax = new Vector2(5, 0);
            Image accentImg = accent.AddComponent<Image>();
            accentImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.9f);
            accentImg.raycastTarget = false;

            // Emoji icon
            Text emojiTxt = CreateText(rt, "Emoji", new Vector2(-218, 0), new Vector2(40, 50),
                26, FontStyle.Normal, Color.white);
            emojiTxt.text = emoji;

            // Label (bold title)
            Text lblTxt = CreateText(rt, "Label", new Vector2(20, 10), new Vector2(380, 28),
                16, FontStyle.Bold, accentColor);
            lblTxt.text      = label;
            lblTxt.alignment = TextAnchor.MiddleLeft;

            // Description (small subtitle)
            Text descTxt = CreateText(rt, "Desc", new Vector2(20, -12), new Vector2(380, 22),
                11, FontStyle.Normal, new Color(0.75f, 0.80f, 0.88f));
            descTxt.text      = description;
            descTxt.alignment = TextAnchor.MiddleLeft;

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = bg;

            ColorBlock cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cb;

            return btn;
        }

        private void CreateSeparator(RectTransform parent, Vector2 pos, Vector2 size)
        {
            GameObject sep = new GameObject("Separator");
            sep.transform.SetParent(parent, false);
            RectTransform rt = sep.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
            Image img = sep.AddComponent<Image>();
            img.color          = new Color(1f, 1f, 1f, 0.08f);
            img.raycastTarget  = false;
        }

        private Text CreateText(RectTransform parent, string name, Vector2 pos, Vector2 size,
            int fontSize, FontStyle style, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;

            Text txt = obj.AddComponent<Text>();
            txt.font          = CardVisualTheme.GetFont();
            txt.fontSize      = fontSize;
            txt.fontStyle     = style;
            txt.alignment     = TextAnchor.MiddleCenter;
            txt.color         = color;
            txt.raycastTarget = false;
            return txt;
        }
    }
}
