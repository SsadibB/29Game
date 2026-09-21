using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Bidding panel for the human player (South).
    ///
    /// Replaces the old +/- stepper with a 4-column selectable-number grid:
    ///   Row 1 : 16  17  18  19
    ///   Row 2 : 20  21  22  23
    ///   Row 3 : 24  25  26  27
    ///   Row 4 : 28  [Pass - spans remaining 3 columns]
    ///
    /// Active bids (>= minRaise) are fully clickable.
    /// Inactive bids (< minRaise) are dimmed and non-interactable.
    /// Pass is always enabled.
    ///
    /// All visual constants are in the "Visual Configuration" region below -
    /// change colours, sizes, fonts, and spacing there without touching any logic.
    /// </summary>
    public class BiddingPanelUI : MonoBehaviour
    {
        // ======================================================================
        #region Visual Configuration -- edit freely to restyle the panel

        // -- Outer panel -------------------------------------------------------
        /// <summary>Overall size of the panel in pixels.</summary>
        private static readonly Vector2 PanelSize = new Vector2(580, 350);
        /// <summary>Panel tint — white so PopupBG image shows through unchanged.</summary>
        private static readonly Color PanelBgColor = Color.white;

        // -- Grid layout -------------------------------------------------------
        private const int GridColumns = 4;
        private const float BtnWidth = 96f;   // width of each number button
        private const float BtnHeight = 56f;   // height for all buttons
        private const float BtnSpacingX = 8f;    // horizontal gap
        private const float BtnSpacingY = 8f;    // vertical gap
        /// <summary>
        /// Nudges the whole grid up (+) or down (-) from dead-centre of the panel, in pixels.
        /// The grid is centred vertically automatically; use this only to compensate for
        /// uneven padding baked into the PopupBG artwork.
        /// </summary>
        private const float GridVerticalOffset = 0f;

        // -- Number buttons (active) -------------------------------------------
        private static readonly Color BtnActiveBg = new Color(0.18f, 0.14f, 0.10f, 1.00f);
        private static readonly Color BtnActiveText = Color.white;
        private const int BtnFontSize = 22;
        /// <summary>Border outline colour on every bid/pass button. Hex #B1B1B1.</summary>
        private static readonly Color BtnBorderColor = new Color(0.694f, 0.694f, 0.694f, 1f); // #B1B1B1
        /// <summary>Thickness of the button border in pixels.</summary>
        private const float BtnBorderThickness = 2f;

        // -- Inactive (disabled) state -----------------------------------------
        private static readonly Color BtnInactiveBg = new Color(0.30f, 0.22f, 0.12f, 0.55f);
        private static readonly Color BtnInactiveText = new Color(0.55f, 0.55f, 0.55f, 1f);

        // -- Pass button -------------------------------------------------------
        private static readonly Color PassActiveBg = new Color(0.18f, 0.14f, 0.10f, 1.00f);
        private static readonly Color PassActiveText = Color.white;
        private const int PassFontSize = 22;
        private const FontStyle PassFontStyle = FontStyle.Bold;

        #endregion
        // ======================================================================

        // -- Runtime state -----------------------------------------------------
        private int _minAllowedBid = GameRules.MinBid;

        // All selectable bid values (13 number buttons, last one is 28)
        private static readonly int[] BidValues = { 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 };

        private readonly List<Button> _bidButtons = new List<Button>();
        private readonly List<Text> _bidTexts = new List<Text>();
        private readonly List<Image> _bidImages = new List<Image>();
        private Button _passButton;

        // Guards against rebuilding the button grid on repeated EnsureComponents() calls.
        private bool _gridBuilt = false;

        // -- Unity lifecycle ---------------------------------------------------

        private void Awake() => EnsureComponents();

        // -- Public API --------------------------------------------------------

        /// <summary>
        /// Show the bidding panel.
        /// Signature is identical to the previous version -- no callers need to change.
        /// </summary>
        /// <param name="currentHighBid">The current highest bid (or MinBid-1 if none).</param>
        /// <param name="currentHighBidder">Player who placed the current high bid.</param>
        /// <param name="minRaise">Minimum bid this player must make to take the lead.</param>
        public void Show(int currentHighBid, PlayerSeat currentHighBidder, int minRaise)
        {
            EnsureComponents();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            _minAllowedBid = Mathf.Clamp(minRaise, GameRules.MinBid, GameRules.MaxBid);
            RefreshButtonStates();

            // Pop-in animation
            transform.DOKill();
            transform.localScale = Vector3.one * 0.75f;
            transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack);
        }

        /// <summary>Hide the panel immediately.</summary>
        public void Hide()
        {
            transform.DOKill();
            gameObject.SetActive(false);
        }

        // -- Setup (idempotent) ------------------------------------------------

        /// <summary>
        /// Ensures the panel background and button grid exist.
        /// Safe to call multiple times; only builds the grid once.
        /// </summary>
        public void EnsureComponents()
        {
            RectTransform rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = PanelSize;

            // Use the PopupBG resource sprite as the panel background.
            Image bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            Sprite popupBg = CardVisualTheme.PopupBG;
            if (popupBg != null)
            {
                bg.sprite = popupBg;
                bg.type = Image.Type.Simple;
                bg.preserveAspect = false;
            }
            else
            {
                // Fallback: solid rounded rect if PopupBG asset is missing.
                bg.sprite = CardVisualTheme.PillBadge;
                bg.type = Image.Type.Sliced;
            }
            bg.color = PanelBgColor;

            if (_gridBuilt) return;
            _gridBuilt = true;
            BuildGrid();
        }

        // -- Grid builder ------------------------------------------------------

        private void BuildGrid()
        {
            // Total width of the 4-column grid
            float totalW = GridColumns * BtnWidth + (GridColumns - 1) * BtnSpacingX;
            float gridLeft = -totalW * 0.5f;
            // Total height of the 4-row grid, then centre it vertically in the panel
            // (children are positioned relative to the panel's centre).
            int rows = 4;
            float totalH = rows * BtnHeight + (rows - 1) * BtnSpacingY;
            // Y of the top row's button centre
            float gridTop = totalH * 0.5f - BtnHeight * 0.5f + GridVerticalOffset;

            // Rows 0-2: bids 16-27 (12 buttons, 4 per row)
            for (int i = 0; i < BidValues.Length - 1; i++)
            {
                int col = i % GridColumns;
                int row = i / GridColumns;
                float cx = gridLeft + col * (BtnWidth + BtnSpacingX) + BtnWidth * 0.5f;
                float cy = gridTop - row * (BtnHeight + BtnSpacingY);
                BuildNumberButton(BidValues[i], new Vector2(cx, cy), new Vector2(BtnWidth, BtnHeight));
            }

            // Row 3: "28" (col 0) + wide "Pass" (cols 1-3)
            float row3Y = gridTop - 3 * (BtnHeight + BtnSpacingY);
            float btn28X = gridLeft + BtnWidth * 0.5f;
            BuildNumberButton(28, new Vector2(btn28X, row3Y), new Vector2(BtnWidth, BtnHeight));

            float passW = 3 * BtnWidth + 2 * BtnSpacingX;
            float passX = gridLeft + (BtnWidth + BtnSpacingX) + passW * 0.5f;
            BuildPassButton(new Vector2(passX, row3Y), new Vector2(passW, BtnHeight));
        }

        private void BuildNumberButton(int bidValue, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject("BidBtn_" + bidValue);
            obj.transform.SetParent(transform, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            // Border layer — drawn first (behind the fill)
            AddBorderLayer(obj.transform, size);

            // Fill layer — inset by border thickness so the border shows as a frame
            Image img = AddFillLayer(obj.transform, size, BtnActiveBg);

            Button btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;

            // Subtle color-tint transitions on hover/press
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.10f, 0.90f, 1f);
            cb.pressedColor = new Color(0.80f, 0.75f, 0.55f, 1f);
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            btn.colors = cb;

            Text lbl = CreateLabelChild(obj.transform, bidValue.ToString(),
                                        BtnFontSize, FontStyle.Bold, BtnActiveText);

            // Capture bid value for the closure
            int capturedBid = bidValue;
            btn.onClick.AddListener(() => OnBidButtonClicked(capturedBid));

            _bidButtons.Add(btn);
            _bidTexts.Add(lbl);
            _bidImages.Add(img);
        }

        private void BuildPassButton(Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject("PassBtn");
            obj.transform.SetParent(transform, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            // Border layer
            AddBorderLayer(obj.transform, size);

            // Fill layer
            Image img = AddFillLayer(obj.transform, size, PassActiveBg);

            Button btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.10f, 0.90f, 1f);
            cb.pressedColor = new Color(0.80f, 0.75f, 0.55f, 1f);
            btn.colors = cb;

            CreateLabelChild(obj.transform, "Pass", PassFontSize, PassFontStyle, PassActiveText);
            btn.onClick.AddListener(OnPassButtonClicked);
            _passButton = btn;
        }

        // -- State refresh -----------------------------------------------------

        /// <summary>
        /// Dims/disables bids below _minAllowedBid; keeps Pass always enabled.
        /// </summary>
        private void RefreshButtonStates()
        {
            for (int i = 0; i < _bidButtons.Count; i++)
            {
                bool active = BidValues[i] >= _minAllowedBid;
                _bidButtons[i].interactable = active;
                _bidImages[i].color = active ? BtnActiveBg : BtnInactiveBg;
                _bidTexts[i].color = active ? BtnActiveText : BtnInactiveText;
            }

            if (_passButton != null)
                _passButton.interactable = true;
        }

        // -- Click handlers ----------------------------------------------------

        private void OnBidButtonClicked(int bid)
        {
            Debug.Log("[29 BiddingPanel] Human South clicked BID " + bid);
            bool ok = GameManager.Instance.PlaceHumanBid(bid);
            if (ok) Hide();
        }

        private void OnPassButtonClicked()
        {
            Debug.Log("[29 BiddingPanel] Human South clicked PASS");
            bool ok = GameManager.Instance.HumanPass();
            if (ok) Hide();
        }

        // -- Helpers -----------------------------------------------------------

        /// <summary>
        /// Adds the #B1B1B1 outline layer to a button. It fills the whole button
        /// rect and is drawn first (behind the fill), so the fill — inset by
        /// BtnBorderThickness — leaves a visible frame around it.
        /// The border is NOT the button's target graphic, so hover/press/disabled
        /// tinting never changes its colour: every button keeps the same border.
        /// </summary>
        private static void AddBorderLayer(Transform parent, Vector2 size)
        {
            GameObject obj = new GameObject("Border");
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image border = obj.AddComponent<Image>();
            border.sprite = CardVisualTheme.PillBadge;
            border.type = Image.Type.Sliced;
            border.color = BtnBorderColor;
            border.raycastTarget = false;
        }

        /// <summary>
        /// Adds the button's fill layer, inset on every side by BtnBorderThickness
        /// so the border layer behind it shows as a frame. Returns the fill Image,
        /// which the caller uses as the Button's targetGraphic.
        /// </summary>
        private static Image AddFillLayer(Transform parent, Vector2 size, Color fillColor)
        {
            GameObject obj = new GameObject("Fill");
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(BtnBorderThickness, BtnBorderThickness);
            rt.offsetMax = new Vector2(-BtnBorderThickness, -BtnBorderThickness);

            Image fill = obj.AddComponent<Image>();
            fill.sprite = CardVisualTheme.PillBadge;
            fill.type = Image.Type.Sliced;
            fill.color = fillColor;
            return fill;
        }

        /// <summary>Creates a full-stretch Text child inside a button GameObject.</summary>
        private Text CreateLabelChild(Transform parent, string text,
                                      int fontSize, FontStyle style, Color color)
        {
            GameObject obj = new GameObject("Label");
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text lbl = obj.AddComponent<Text>();
            lbl.font = CardVisualTheme.GetFont();
            lbl.fontSize = fontSize;
            lbl.fontStyle = style;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = color;
            lbl.text = text;
            lbl.raycastTarget = false;
            return lbl;
        }

        /// <summary>
        /// Creates a simple procedural rounded-rect sprite for the panel background.
        /// Falls back to PillBadge if texture creation fails.
        /// </summary>
        private static Sprite BuildRoundedSprite(float radius)
        {
            try
            {
                const int w = 128, h = 128;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                Color[] pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dist = RoundedRectSDF(x + 0.5f, y + 0.5f, w, h, radius);
                        float alpha = Mathf.Clamp01(0.5f - dist);
                        pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
                    }
                tex.SetPixels(pixels);
                tex.Apply();
                return Sprite.Create(tex,
                    new Rect(0, 0, w, h),
                    new Vector2(0.5f, 0.5f),
                    100f, 0,
                    SpriteMeshType.FullRect,
                    new Vector4(radius, radius, radius, radius));
            }
            catch
            {
                return CardVisualTheme.PillBadge;
            }
        }

        private static float RoundedRectSDF(float px, float py, float w, float h, float r)
        {
            float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
            return Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f)
                            + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) - r;
        }
    }
}