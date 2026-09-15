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
    ///   • Positive Points (+1 to +6): The 6 of Hearts is the base card underneath.
    ///   • The UpperCard slides/rotates per point count to reveal a specific set of
    ///     suit pips — see VisiblePipsByCount and the Get UpperCard Offset/Rotation
    ///     helpers below for the exact reveal pattern and slide poses.
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
                baseSuitTopLeft = CreateCornerText("SuitTL", baseCardTransform, new Vector2(0, 1), new Vector2(6.7f, -18f), new Vector2(15, 20), TextAnchor.UpperLeft, 14);

                // Corner Bottom-Right: mirrored across the card so it hugs the
                // opposite corner with the same tight spacing.
                baseRankBottomRight = CreateCornerText("RankBR", baseCardTransform, new Vector2(1, 0), new Vector2(-6.7f, 5.6f), new Vector2(15, 17), TextAnchor.LowerRight, 15);
                baseSuitBottomRight = CreateCornerText("SuitBR", baseCardTransform, new Vector2(1, 0), new Vector2(-6.7f, 18f), new Vector2(15, 20), TextAnchor.LowerRight, 14);

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
                    // Anchor/pivot per pip row — top-center for the top pair
                    // (0, 3), middle-center for the mid pair (1, 4), bottom-
                    // center for the bottom pair (2, 5). Anchor == pivot so
                    // the anchoredPosition set in ApplyStackedScore is read
                    // from that same edge, matching the Editor-measured
                    // values (Pip_0/3 top-center, Pip_1/4 middle-center,
                    // Pip_2/5 bottom-center).
                    Vector2 pipAnchor = GetPipAnchor(i);
                    pipRt.anchorMin = pipAnchor;
                    pipRt.anchorMax = pipAnchor;
                    pipRt.pivot = pipAnchor;
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

                    // Only the exact suits required for this point count remain
                    // visible (see VisiblePipsByCount) — NOT simply "the first N
                    // pip indices". E.g. at 2 points the top-left AND top-right
                    // pips show (indices 0 and 3), not top-left + mid-left.
                    bool isVisible = IsPipVisible(i, count);
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
        /// Standard 6-card pip layout coordinates (from Editor-measured positions):
        ///   • Pip 0: Top-left pip
        ///   • Pip 1: Mid-left pip
        ///   • Pip 2: Bottom-left pip
        ///   • Pip 3: Top-right pip
        ///   • Pip 4: Mid-right pip
        ///   • Pip 5: Bottom-right pip
        /// Note: Y is negative toward the top of the card and positive toward
        /// the bottom in this stack's coordinate space (matches the UpperCard
        /// pose convention below, where "upward" reads as negative Y too).
        /// </summary>
        private static Vector2[] GetBaseCardPipPositions()
        {
            return new Vector2[]
            {
                new Vector2(-17, -15), // Pip 0: Top-left
                new Vector2(-15,   0), // Pip 1: Mid-left
                new Vector2(-15,  15), // Pip 2: Bottom-left
                new Vector2( 17, -15), // Pip 3: Top-right
                new Vector2( 17,   0), // Pip 4: Mid-right
                new Vector2( 17,  15)  // Pip 5: Bottom-right
            };
        }

        /// <summary>
        /// Anchor/pivot point for each pip slot, per the Editor-measured
        /// values: Pip_0/Pip_3 (top row) anchor top-center, Pip_1/Pip_4
        /// (mid row) anchor middle-center, Pip_2/Pip_5 (bottom row) anchor
        /// bottom-center. Used for both team and opponent point card slots
        /// since they share this same build path.
        /// </summary>
        private static Vector2 GetPipAnchor(int pipIndex)
        {
            switch (pipIndex)
            {
                case 0:
                case 3:
                    return new Vector2(0.5f, 1f);  // top-center
                case 2:
                case 5:
                    return new Vector2(0.5f, 0f);  // bottom-center
                default:
                    return new Vector2(0.5f, 0.5f); // middle-center (1, 4)
            }
        }

        /// <summary>
        /// Which of the 6 pip slots (0=TL, 1=ML, 2=BL, 3=TR, 4=MR, 5=BR — see
        /// GetBaseCardPipPositions) are exposed at each point count. This is
        /// deliberately NOT "the first N pip indices" — the reveal order skips
        /// around (e.g. 2 points shows TL+TR, not TL+ML) to match the physical
        /// card-sliding reference.
        /// </summary>
        private static readonly int[][] VisiblePipsByCount =
        {
            new int[] { },                  // 0: fully covered
            new int[] { 0 },                // 1: top-left
            new int[] { 0, 3 },             // 2: top-left + top-right
            new int[] { 0, 1, 2 },          // 3: top-left + mid-left + bottom-left
            new int[] { 0, 1, 3, 4 },       // 4: top-left + top-right + mid-left + mid-right
            new int[] { 0, 1, 2, 3, 4 },    // 5: everything except bottom-right
            new int[] { 0, 1, 2, 3, 4, 5 }, // 6: all six
        };

        private static bool IsPipVisible(int pipIndex, int count)
        {
            int clamped = Mathf.Clamp(count, 0, VisiblePipsByCount.Length - 1);
            int[] visible = VisiblePipsByCount[clamped];
            for (int j = 0; j < visible.Length; j++)
                if (visible[j] == pipIndex) return true;
            return false;
        }

        // ── UpperCard slide/rotate poses per point count ────────────────────
        // 2, 3, and 4-pt poses below are your confirmed Editor values. 1, 5,
        // and 6-pt poses were never specified, so they're still an estimate —
        // tune those in Play mode if the peel doesn't look right. Pip
        // visibility (above) is driven purely by point count and is correct
        // regardless of these numbers; these only control how the UpperCard
        // visually slides/rotates to sell the "peeling back" effect.
        //
        //   0 pts: upright, fully covering (0, 0) @ 0°
        //   1 pt : small diagonal peel — reveals just the top-left corner (estimate)
        //   2 pts: rotate 90°, Y = -25 to reveal both top pips (confirmed)
        //   3 pts: no rotation, X = 50 to reveal the whole left column
        //          (top-left, mid-left, bottom-left) (confirmed)
        //   4 pts: rotate 90°, Y = -65 to additionally reveal both middle
        //          pips (confirmed)
        //   5 pts: union of the 3-pt and 4-pt poses (right + further -Y,
        //          still rotated 90°), since 5 pts = those two pip sets
        //          combined (estimate)
        //   6 pts: pushed even further along the same diagonal to also clear
        //          the last (bottom-right) pip (estimate)

        private static readonly Vector2 OneSuitPos = new Vector2(26, -18);
        private const float OneSuitRot = -41f;

        private static readonly Vector2 TwoSuitPos = new Vector2(0, -25);
        private const float TwoSuitRot = 90f;

        private static readonly Vector2 ThreeSuitPos = new Vector2(50, 0);
        private const float ThreeSuitRot = 0f;

        private static readonly Vector2 FourSuitPos = new Vector2(0, -65);
        private const float FourSuitRot = 90f;

        private static readonly Vector2 FiveSuitPos = new Vector2(50, -85);
        private const float FiveSuitRot = 90f;

        private static readonly Vector2 SixSuitPos = new Vector2(70, -115);
        private const float SixSuitRot = 90f;

        private static Vector2 GetUpperCardOffset(int count)
        {
            switch (count)
            {
                case 1: return OneSuitPos;
                case 2: return TwoSuitPos;
                case 3: return ThreeSuitPos;
                case 4: return FourSuitPos;
                case 5: return FiveSuitPos;
                case 6: return SixSuitPos;
                default: return Vector2.zero;
            }
        }

        private static float GetUpperCardRotation(int count)
        {
            switch (count)
            {
                case 1: return OneSuitRot;
                case 2: return TwoSuitRot;
                case 3: return ThreeSuitRot;
                case 4: return FourSuitRot;
                case 5: return FiveSuitRot;
                case 6: return SixSuitRot;
                default: return 0f;
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