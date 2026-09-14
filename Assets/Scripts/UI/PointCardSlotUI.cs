using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Visual representation of the Point Card System on the table for a team.
    ///
    /// Authentic Visual Style:
    ///   • Point cards are stacked on top of each other.
    ///   • 0 Points: Upper card is upright (0°), completely covering the base card with
    ///     the red card back (0 suit icons visible).
    ///   • Negative Points (-1 to -6): The 6 of Spades is the base card underneath.
    ///     The upper card (GreenBack) is tilted diagonally (approx -41° for -1 pt),
    ///     leaving the top-left suit icons of the 6 of Spades exposed while covering the rest.
    ///   • Positive Points (+1 to +6): The 6 of Hearts is the base card underneath,
    ///     with the upper card tilted diagonally to expose the exact number of Heart icons.
    ///   • Current board trick card points (e.g. "Card Pts: 7") are displayed below,
    ///     resetting to 0-0 each deal without altering the bid/win score.
    /// </summary>
    public class PointCardSlotUI : MonoBehaviour
    {
        [Header("Team Configuration")]
        [SerializeField] private int teamIndex = 0; // 0 = Your Team, 1 = Opponent
        [SerializeField] private string teamTitle = "Your Team";

        [Header("UI Elements")]
        [SerializeField] private Text headerTitleText;
        [SerializeField] private RectTransform cardContainer;

        // Base Card (Underneath: The 6-point card)
        [SerializeField] private RectTransform baseCardTransform;
        [SerializeField] private Image baseCardBg;
        [SerializeField] private Text baseRankTopLeft;
        [SerializeField] private Text baseSuitTopLeft;
        [SerializeField] private Text baseRankBottomRight;
        [SerializeField] private Text baseSuitBottomRight;
        [SerializeField] private GameObject pipsContainer;
        [SerializeField] private Text[] pipTexts;
        [SerializeField] private Image[] pipImages;

        // Upper Card (Covering Card on top)
        [SerializeField] private RectTransform upperCardTransform;
        [SerializeField] private Image upperCardShadow;
        [SerializeField] private Image upperCardBg;

        // Score Badges & Subtext
        [SerializeField] private Text scoreBadgeText;
        [SerializeField] private Text boardCardPointsText;

        private int _currentScore = 0;
        private int _currentBoardCardPts = 0;
        private bool _isBuilt = false;
        private Tween _slideTween;

        public int TeamIndex
        {
            get => teamIndex;
            set => teamIndex = value;
        }

        public string TeamTitle
        {
            get => teamTitle;
            set
            {
                teamTitle = value;
                if (headerTitleText != null) headerTitleText.text = value;
            }
        }

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnDestroy()
        {
            transform.DOKill();
            _slideTween?.Kill();
            if (upperCardTransform != null) upperCardTransform.DOKill();
            if (baseCardTransform != null) baseCardTransform.DOKill();
        }

        public void EnsureComponents()
        {
            if (_isBuilt && headerTitleText != null && upperCardBg != null) return;
            _isBuilt = true;

            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(130, 220);

            // 1. Header Title ("Your Team" / "Opponent" clean white text with shadow)
            if (headerTitleText == null)
            {
                GameObject hObj = new GameObject("HeaderTitle");
                hObj.transform.SetParent(transform, false);
                RectTransform hrt = hObj.AddComponent<RectTransform>();
                hrt.anchorMin = new Vector2(0.5f, 1f);
                hrt.anchorMax = new Vector2(0.5f, 1f);
                hrt.pivot = new Vector2(0.5f, 1f);
                hrt.anchoredPosition = new Vector2(0, 0);
                hrt.sizeDelta = new Vector2(140, 28);

                headerTitleText = hObj.AddComponent<Text>();
                headerTitleText.font = CardVisualTheme.GetFont();
                headerTitleText.fontSize = 17;
                headerTitleText.fontStyle = FontStyle.Bold;
                headerTitleText.alignment = TextAnchor.MiddleCenter;
                headerTitleText.color = Color.white;
                headerTitleText.text = teamTitle;
                headerTitleText.raycastTarget = false;

                Shadow shadow = hObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // 2. Card Container (Holds stacked cards)
            if (cardContainer == null)
            {
                GameObject cObj = new GameObject("CardContainer");
                cObj.transform.SetParent(transform, false);
                cardContainer = cObj.AddComponent<RectTransform>();
                cardContainer.anchorMin = new Vector2(0.5f, 0.5f);
                cardContainer.anchorMax = new Vector2(0.5f, 0.5f);
                cardContainer.pivot = new Vector2(0.5f, 0.5f);
                cardContainer.anchoredPosition = new Vector2(0, 4);
                cardContainer.sizeDelta = new Vector2(100, 140);
            }

            // 3. Base Card (Underneath: The 6-point card)
            if (baseCardTransform == null)
            {
                GameObject bObj = new GameObject("BaseCard_6Point");
                bObj.transform.SetParent(cardContainer, false);
                baseCardTransform = bObj.AddComponent<RectTransform>();
                baseCardTransform.anchorMin = new Vector2(0.5f, 0.5f);
                baseCardTransform.anchorMax = new Vector2(0.5f, 0.5f);
                baseCardTransform.pivot = new Vector2(0.5f, 0.5f);
                baseCardTransform.anchoredPosition = Vector2.zero;
                baseCardTransform.localEulerAngles = Vector3.zero;
                baseCardTransform.sizeDelta = new Vector2(100, 140);

                baseCardBg = bObj.AddComponent<Image>();
                baseCardBg.sprite = CardVisualTheme.CreateRoundedRectSprite(100, 140, 14, CardVisualTheme.ColorCardPaper, CardVisualTheme.ColorBorderGold, 3);
                baseCardBg.type = Image.Type.Simple;
                baseCardBg.preserveAspect = true;
                baseCardBg.color = Color.white;

                // Corner Top-Left: "6" and small suit symbol underneath, pinned tight
                // to the true top-left corner of the card (matches the reference
                // Inspector values: anchor/pivot (0,1), pos (6.7,-5.6)/(6.7,-15),
                // size 15x17 / 15x20) so rank and suit sit close together.
                baseRankTopLeft = CreateCornerText("RankTL", baseCardTransform, new Vector2(0, 1), new Vector2(6.7f, -5.6f), new Vector2(15, 17), TextAnchor.UpperLeft, 15);
                baseSuitTopLeft = CreateCornerText("SuitTL", baseCardTransform, new Vector2(0, 1), new Vector2(6.7f, -15f), new Vector2(15, 20), TextAnchor.UpperLeft, 14);

                // Corner Bottom-Right: mirrored across the card so it hugs the
                // opposite corner with the same tight spacing.
                baseRankBottomRight = CreateCornerText("RankBR", baseCardTransform, new Vector2(1, 0), new Vector2(-6.7f, 5.6f), new Vector2(15, 17), TextAnchor.LowerRight, 15);
                baseSuitBottomRight = CreateCornerText("SuitBR", baseCardTransform, new Vector2(1, 0), new Vector2(-6.7f, 15f), new Vector2(15, 20), TextAnchor.LowerRight, 14);

                // Pips Container on base card (holds all 6 suit icons)
                GameObject pipsObj = new GameObject("PipsContainer");
                pipsObj.transform.SetParent(baseCardTransform, false);
                RectTransform prt = pipsObj.AddComponent<RectTransform>();
                prt.anchorMin = Vector2.zero;
                prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero;
                prt.offsetMax = Vector2.zero;
                pipsContainer = pipsObj;

                pipTexts = new Text[6];
                pipImages = new Image[6];

                for (int i = 0; i < 6; i++)
                {
                    GameObject pipObj = new GameObject($"Pip_{i}");
                    pipObj.transform.SetParent(pipsContainer.transform, false);
                    RectTransform pipRt = pipObj.AddComponent<RectTransform>();
                    pipRt.anchorMin = new Vector2(0.5f, 0.5f);
                    pipRt.anchorMax = new Vector2(0.5f, 0.5f);
                    pipRt.pivot = new Vector2(0.5f, 0.5f);
                    pipRt.sizeDelta = new Vector2(25, 25);

                    Image pImg = pipObj.AddComponent<Image>();
                    pImg.preserveAspect = true;
                    pImg.raycastTarget = false;
                    pImg.gameObject.SetActive(false);
                    pipImages[i] = pImg;

                    GameObject pTxtObj = new GameObject("Glyph");
                    pTxtObj.transform.SetParent(pipObj.transform, false);
                    RectTransform ptRt = pTxtObj.AddComponent<RectTransform>();
                    ptRt.anchorMin = Vector2.zero;
                    ptRt.anchorMax = Vector2.one;
                    ptRt.offsetMin = Vector2.zero;
                    ptRt.offsetMax = Vector2.zero;

                    Text pTxt = pTxtObj.AddComponent<Text>();
                    pTxt.font = CardVisualTheme.GetFont();
                    pTxt.fontSize = 20;
                    pTxt.fontStyle = FontStyle.Bold;
                    pTxt.alignment = TextAnchor.MiddleCenter;
                    pTxt.raycastTarget = false;
                    pipTexts[i] = pTxt;
                }

                // Draw the corner rank/suit text ON TOP of the pips container,
                // and give them a visible default color immediately (instead of
                // staying invisible-white until the first UpdateDisplay call),
                // so "6" + the small suit mark are never hidden behind a pip icon.
                baseRankTopLeft.color = CardVisualTheme.ColorBlackSuit;
                baseSuitTopLeft.color = CardVisualTheme.ColorBlackSuit;
                baseRankBottomRight.color = CardVisualTheme.ColorBlackSuit;
                baseSuitBottomRight.color = CardVisualTheme.ColorBlackSuit;
                baseRankTopLeft.transform.SetAsLastSibling();
                baseSuitTopLeft.transform.SetAsLastSibling();
                baseRankBottomRight.transform.SetAsLastSibling();
                baseSuitBottomRight.transform.SetAsLastSibling();
            }

            // 4. Upper Card (Stacked diagonally over base card)
            if (upperCardTransform == null)
            {
                GameObject uObj = new GameObject("UpperCard_Covering");
                uObj.transform.SetParent(cardContainer, false);
                upperCardTransform = uObj.AddComponent<RectTransform>();
                upperCardTransform.anchorMin = new Vector2(0.5f, 0.5f);
                upperCardTransform.anchorMax = new Vector2(0.5f, 0.5f);
                upperCardTransform.pivot = new Vector2(0.5f, 0.5f);
                upperCardTransform.anchoredPosition = Vector2.zero;
                upperCardTransform.localEulerAngles = Vector3.zero;
                upperCardTransform.sizeDelta = new Vector2(100, 140);

                // Subtle shadow underneath the upper card for physical depth
                GameObject sObj = new GameObject("DropShadow");
                sObj.transform.SetParent(upperCardTransform, false);
                RectTransform srt = sObj.AddComponent<RectTransform>();
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.one;
                srt.offsetMin = new Vector2(-2, -3);
                srt.offsetMax = new Vector2(2, 1);

                upperCardShadow = sObj.AddComponent<Image>();
                upperCardShadow.sprite = CardVisualTheme.CreateRoundedRectSprite(100, 140, 14, new Color(0f, 0f, 0f, 0.40f), Color.clear, 0);
                upperCardShadow.type = Image.Type.Sliced;
                upperCardShadow.raycastTarget = false;

                // Upper Card Face: GreenBack card back
                upperCardBg = uObj.AddComponent<Image>();
                upperCardBg.sprite = CardVisualTheme.CardBack;
                upperCardBg.type = Image.Type.Simple;
                upperCardBg.preserveAspect = true;
                upperCardBg.color = Color.white;
            }

            // Ensure upper card is stacked on top of base card
            if (upperCardTransform != null && baseCardTransform != null)
            {
                baseCardTransform.SetSiblingIndex(0);
                upperCardTransform.SetSiblingIndex(1);
            }

            // 5. Round Display (Current round card points: Team: X / Opponent: Y)
            if (boardCardPointsText == null)
            {
                GameObject bObj = new GameObject("RoundDisplay");
                bObj.transform.SetParent(transform, false);
                RectTransform brt = bObj.AddComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 0f);
                brt.anchorMax = new Vector2(0.5f, 0f);
                brt.pivot = new Vector2(0.5f, 0f);
                brt.anchoredPosition = new Vector2(0, 2);
                brt.sizeDelta = new Vector2(140, 24);

                boardCardPointsText = bObj.AddComponent<Text>();
                boardCardPointsText.font = CardVisualTheme.GetFont();
                boardCardPointsText.fontSize = 15;
                boardCardPointsText.fontStyle = FontStyle.Bold;
                boardCardPointsText.alignment = TextAnchor.MiddleCenter;
                boardCardPointsText.color = Color.white;
                boardCardPointsText.supportRichText = true;
                boardCardPointsText.raycastTarget = false;

                Shadow bShadow = bObj.AddComponent<Shadow>();
                bShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                bShadow.effectDistance = new Vector2(1.5f, -1.5f);

                string label = teamIndex == 0 ? "Team" : "Opponent";
                boardCardPointsText.text = $"{label}: <b>0</b>";
            }
        }

        /// <summary>
        /// Updates the visual display with the team's point score (-6 to +6)
        /// and current board trick card points.
        /// </summary>
        public void UpdateDisplay(int gameScore, int boardCardPoints)
        {
            EnsureComponents();

            int clampedScore = Mathf.Clamp(gameScore, -GameRules.GamePointsToWin, GameRules.GamePointsToWin);
            bool scoreChanged = clampedScore != _currentScore;
            _currentScore = clampedScore;
            _currentBoardCardPts = boardCardPoints;

            // Round Display: Team: [goted card points] / Opponent: [goted card points]
            if (boardCardPointsText != null)
            {
                string label = teamIndex == 0 ? "Team" : "Opponent";
                boardCardPointsText.text = $"{label}: <b>{boardCardPoints}</b>";
            }

            // Hide old score badge if present in scene hierarchy
            if (scoreBadgeText != null && scoreBadgeText.transform.parent != null)
            {
                scoreBadgeText.transform.parent.gameObject.SetActive(false);
            }

            int pointCount = Mathf.Abs(clampedScore);
            Suit suit = clampedScore >= 0 ? Suit.Hearts : Suit.Spades;

            if (clampedScore == 0)
            {
                ApplyZeroScore(scoreChanged);
            }
            else
            {
                ApplyStackedScore(pointCount, suit, clampedScore > 0, scoreChanged);
            }

            if (scoreChanged && gameObject.activeInHierarchy)
            {
                transform.DOKill();
                transform.DOPunchScale(Vector3.one * 0.08f, 0.20f, 6, 1).SetLink(gameObject);
            }
        }

        private void ApplyZeroScore(bool animate)
        {
            // 0 Points: Only card back is visible, aligned upright (0° at 0, 0)
            if (baseCardTransform != null)
                baseCardTransform.gameObject.SetActive(false);

            if (upperCardBg != null)
            {
                upperCardBg.sprite = CardVisualTheme.CardBack;
                upperCardBg.color = Color.white;
            }

            if (upperCardShadow != null)
                upperCardShadow.gameObject.SetActive(false);

            MoveUpperCard(Vector2.zero, 0f, animate);
        }

        private void ApplyStackedScore(int count, Suit suit, bool isPositive, bool animate)
        {
            // Base Card (Underneath: The 6-point card)
            if (baseCardTransform != null)
            {
                baseCardTransform.gameObject.SetActive(true);
                baseCardBg.sprite = CardVisualTheme.CreateRoundedRectSprite(100, 140, 14, CardVisualTheme.ColorCardPaper, CardVisualTheme.ColorBorderGold, 3);
            }

            Color suitColor = CardVisualTheme.GetSuitColor(suit);
            string suitSym = CardVisualTheme.GetSuitSymbol(suit);
            Sprite suitSprite = CardVisualTheme.GetSuitSprite(suit);

            // Base Card corner indices
            if (baseRankTopLeft != null)
            {
                baseRankTopLeft.gameObject.SetActive(true);
                baseRankTopLeft.text = "6";
                baseRankTopLeft.color = suitColor;
            }

            if (baseSuitTopLeft != null)
            {
                baseSuitTopLeft.gameObject.SetActive(true);
                baseSuitTopLeft.text = suitSym;
                baseSuitTopLeft.color = suitColor;
            }

            if (baseRankBottomRight != null)
            {
                baseRankBottomRight.gameObject.SetActive(true);
                baseRankBottomRight.text = "6";
                baseRankBottomRight.color = suitColor;
            }

            if (baseSuitBottomRight != null)
            {
                baseSuitBottomRight.gameObject.SetActive(true);
                baseSuitBottomRight.text = suitSym;
                baseSuitBottomRight.color = suitColor;
            }

            // Position and activate the 6 suit icons on the 6-point card:
            Vector2[] pipPositions = GetBaseCardPipPositions();

            if (pipsContainer != null)
                pipsContainer.SetActive(true);

            for (int i = 0; i < 6; i++)
            {
                if (pipTexts[i] != null && pipTexts[i].transform.parent != null)
                {
                    RectTransform prt = (RectTransform)pipTexts[i].transform.parent;
                    prt.anchoredPosition = pipPositions[i];

                    // Only the exact number of suit icons remain visible.
                    // The upper card covers the remaining icons.
                    bool isVisible = i < count;
                    prt.gameObject.SetActive(isVisible);

                    if (isVisible)
                    {
                        if (suitSprite != null && pipImages[i] != null)
                        {
                            pipImages[i].sprite = suitSprite;
                            pipImages[i].color = Color.white;
                            pipImages[i].gameObject.SetActive(true);
                            pipTexts[i].gameObject.SetActive(false);
                        }
                        else
                        {
                            if (pipImages[i] != null) pipImages[i].gameObject.SetActive(false);
                            pipTexts[i].gameObject.SetActive(true);
                            pipTexts[i].text = suitSym;
                            pipTexts[i].color = suitColor;
                        }
                    }
                }
            }

            // Upper Card (Tilted diagonally over base card)
            if (upperCardShadow != null)
                upperCardShadow.gameObject.SetActive(true);

            if (upperCardBg != null)
            {
                upperCardBg.sprite = CardVisualTheme.CardBack;
                upperCardBg.color = Color.white;
            }

            // Move the upper card to match the exact diagonal placement from the reference image
            Vector2 targetPos = GetUpperCardOffset(count);
            float targetRot = GetUpperCardRotation(count);
            MoveUpperCard(targetPos, targetRot, animate);
        }

        private void MoveUpperCard(Vector2 targetPos, float targetRot, bool animate)
        {
            if (upperCardTransform == null) return;

            _slideTween?.Kill();

            if (animate && gameObject.activeInHierarchy && Application.isPlaying)
            {
                _slideTween = DOTween.Sequence()
                    .Join(upperCardTransform.DOAnchorPos(targetPos, 0.28f).SetEase(Ease.OutQuad))
                    .Join(upperCardTransform.DOLocalRotate(new Vector3(0, 0, targetRot), 0.28f).SetEase(Ease.OutQuad))
                    .SetLink(gameObject);
            }
            else
            {
                upperCardTransform.anchoredPosition = targetPos;
                upperCardTransform.localEulerAngles = new Vector3(0, 0, targetRot);
            }
        }

        /// <summary>
        /// Standard 6-card pip layout coordinates:
        ///   • Pip 0: Top-left pip right next to corner "6" (exposed at -1 pt as in reference image)
        ///   • Pip 1: Mid-left pip (exposed at -2 pts)
        ///   • Pip 2: Bottom-left pip (exposed at -3 pts)
        ///   • Pip 3: Top-right pip (exposed at -4 pts)
        ///   • Pip 4: Mid-right pip (exposed at -5 pts)
        ///   • Pip 5: Bottom-right pip (exposed at -6 pts)
        /// </summary>
        private static Vector2[] GetBaseCardPipPositions()
        {
            return new Vector2[]
            {
                new Vector2(-15,  42), // Pip 0: Top-left pip (next to rank "6")
                new Vector2(-15,  14), // Pip 1: Mid-left pip
                new Vector2(-15, -14), // Pip 2: Bottom-left pip
                new Vector2( 15,  42), // Pip 3: Top-right pip
                new Vector2( 15,  14), // Pip 4: Mid-right pip
                new Vector2( 15, -14)  // Pip 5: Bottom-right pip
            };
        }

        /// <summary>
        /// Diagonal card offset matching the reference image.
        /// At -1 pt: rotation is -41°, position is (14, -6), leaving top-left corner exposed.
        /// As points increase, card shifts down-right along the diagonal to expose more icons.
        /// </summary>
        private static Vector2 GetUpperCardOffset(int count)
        {
            switch (count)
            {
                case 1:
                    // Shifted further right/down than the original reference so the
                    // cover card's diagonal edge fully clears the "6" corner + pip 0
                    // instead of clipping through them.
                    return new Vector2(26, -18);
                case 2:
                    // Shifts down-right, exposing 2 pips in the left column
                    return new Vector2(35, -30);
                case 3:
                    // Shifts further, exposing 3 pips in the left column
                    return new Vector2(44, -42);
                case 4:
                    // Exposing 4 pips
                    return new Vector2(54, -54);
                case 5:
                    // Exposing 5 pips
                    return new Vector2(62, -66);
                case 6:
                default:
                    // Fully exposing all 6 pips
                    return new Vector2(72, -78);
            }
        }

        /// <summary>
        /// Diagonal rotation matching reference image (-41° at 1 pt).
        /// </summary>
        private static float GetUpperCardRotation(int count)
        {
            switch (count)
            {
                case 0:
                    return 0f;
                case 1:
                    return -41f; // Exact diagonal tilt from user's image
                case 2:
                    return -35f;
                case 3:
                    return -28f;
                case 4:
                    return -22f;
                case 5:
                    return -16f;
                case 6:
                default:
                    return -10f;
            }
        }

        /// <summary>
        /// Creates a corner rank/suit label pinned to a specific corner of its
        /// parent card via anchor == pivot == <paramref name="anchor"/> (e.g.
        /// (0,1) for top-left, (1,0) for bottom-right), instead of the old
        /// center-anchored layout. This keeps rank and suit text tight against
        /// the card edge and close together, matching the reference Inspector
        /// values for RankTL/SuitTL (and mirrored for the BR corner).
        /// </summary>
        private Text CreateCornerText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, TextAnchor alignment, int fontSize)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text txt = obj.AddComponent<Text>();
            txt.font = CardVisualTheme.GetFont();
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = alignment;
            txt.raycastTarget = false;
            return txt;
        }

        private Text CreateSubText(string name, Transform parent, Vector2 pos, int fontSize, FontStyle style, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text txt = obj.AddComponent<Text>();
            txt.font = CardVisualTheme.GetFont();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.supportRichText = true;
            txt.raycastTarget = false;
            return txt;
        }
    }
}