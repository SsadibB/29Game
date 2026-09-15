using System;
using System.Collections.Generic;
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
        [SerializeField] private Transform cardContainer;

        private readonly List<CardUI> _spawnedCards = new List<CardUI>();
        private Coroutine _actionBubbleCoroutine;

        public Transform CardContainer => cardContainer != null ? cardContainer : transform;

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
        }

        public void EnsureComponents()
        {
            if (cardContainer == null)
            {
                GameObject cc = new GameObject("CardContainer");
                cc.transform.SetParent(transform, false);
                RectTransform rt = cc.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(900, 200);
                cardContainer = cc.transform;
            }

            if (avatarBg == null)
            {
                Transform existingAv = transform.Find("Avatar") ?? transform.Find("AvatarBg");
                GameObject av = existingAv != null ? existingAv.gameObject : new GameObject("AvatarBg");
                if (existingAv == null) av.transform.SetParent(transform, false);
                RectTransform rt = av.GetComponent<RectTransform>() ?? av.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(62, 62);
                avatarBg = av.GetComponent<Image>() ?? av.AddComponent<Image>();
                avatarBg.sprite = CardVisualTheme.CircleAvatar;
                avatarBg.color = new Color(0.10f, 0.16f, 0.24f, 0.95f);
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

            if (avatarIcon == null)
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
                // Fixed 35x35 box, anchored/pivoted dead center of the avatar
                // (instead of the old 12%-88% stretch-to-fit).
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(35, 35);
                avatarIcon = icon.GetComponent<Image>() ?? icon.AddComponent<Image>();
                avatarIcon.sprite = CardVisualTheme.VectorAvatar;
                avatarIcon.color = Color.white;
                avatarIcon.preserveAspect = true;
                avatarIcon.raycastTarget = false;
            }

            if (turnGlowBorder == null)
            {
                GameObject glow = new GameObject("TurnGlow");
                glow.transform.SetParent(avatarBg.transform, false);
                RectTransform grt = glow.AddComponent<RectTransform>();
                grt.anchorMin = Vector2.zero;
                grt.anchorMax = Vector2.one;
                grt.sizeDelta = new Vector2(18, 18);
                turnGlowBorder = glow.AddComponent<Image>();
                turnGlowBorder.sprite = CardVisualTheme.CreateCircleSprite(80, Color.clear, CardVisualTheme.ColorGold, 6);
                turnGlowBorder.color = CardVisualTheme.ColorGoldGlow;
                turnGlowBorder.raycastTarget = false;
                glow.SetActive(false);
            }

            if (nameLabel == null)
            {
                GameObject nl = new GameObject("NameLabel");
                nl.transform.SetParent(transform, false);
                RectTransform nrt = nl.AddComponent<RectTransform>();
                nrt.anchorMin = new Vector2(0.5f, 0.5f);
                nrt.anchorMax = new Vector2(0.5f, 0.5f);
                nrt.sizeDelta = new Vector2(200, 28);
                nameLabel = nl.AddComponent<Text>();
                nameLabel.font = CardVisualTheme.GetFont();
                nameLabel.fontSize = 17;
                nameLabel.fontStyle = FontStyle.Bold;
                nameLabel.alignment = TextAnchor.MiddleCenter;
                nameLabel.color = Color.white;
                nameLabel.raycastTarget = false;
            }

            if (actionBubbleBg == null)
            {
                GameObject ab = new GameObject("ActionBubble");
                ab.transform.SetParent(transform, false);
                RectTransform abrt = ab.AddComponent<RectTransform>();
                abrt.anchorMin = new Vector2(0.5f, 0.5f);
                abrt.anchorMax = new Vector2(0.5f, 0.5f);
                abrt.sizeDelta = new Vector2(130, 34);
                actionBubbleBg = ab.AddComponent<Image>();
                actionBubbleBg.sprite = CardVisualTheme.PillBadge;
                actionBubbleBg.type = Image.Type.Sliced;
                actionBubbleBg.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);
                actionBubbleBg.raycastTarget = false;

                GameObject abt = new GameObject("Text");
                abt.transform.SetParent(ab.transform, false);
                RectTransform abtrt = abt.AddComponent<RectTransform>();
                abtrt.anchorMin = Vector2.zero;
                abtrt.anchorMax = Vector2.one;
                abtrt.sizeDelta = Vector2.zero;
                actionBubbleText = abt.AddComponent<Text>();
                actionBubbleText.font = CardVisualTheme.GetFont();
                actionBubbleText.fontSize = 16;
                actionBubbleText.fontStyle = FontStyle.Bold;
                actionBubbleText.alignment = TextAnchor.MiddleCenter;
                actionBubbleText.color = CardVisualTheme.ColorGold;
                actionBubbleText.raycastTarget = false;
                ab.SetActive(false);
            }

            SetupIdentity();
        }

        public void SetupIdentity()
        {
            if (nameLabel == null || avatarIcon == null) return;
            switch (Seat)
            {
                case PlayerSeat.South:
                    nameLabel.text = "SOUTH";
                    nameLabel.color = CardVisualTheme.ColorCyan;
                    avatarIcon.color = CardVisualTheme.ColorCyan;
                    break;
                case PlayerSeat.North:
                    nameLabel.text = "NORTH";
                    nameLabel.color = CardVisualTheme.ColorCyan;
                    avatarIcon.color = CardVisualTheme.ColorCyan;
                    break;
                case PlayerSeat.East:
                    nameLabel.text = "EAST";
                    var orangeE = new Color(0.95f, 0.55f, 0.35f);
                    nameLabel.color = orangeE;
                    avatarIcon.color = orangeE;
                    break;
                case PlayerSeat.West:
                    nameLabel.text = "WEST";
                    var orangeW = new Color(0.95f, 0.55f, 0.35f);
                    nameLabel.color = orangeW;
                    avatarIcon.color = orangeW;
                    break;
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

        public void ShowActionBubble(string text, float duration = 2.5f)
        {
            if (actionBubbleBg == null || actionBubbleText == null) return;
            actionBubbleText.text = text;
            actionBubbleBg.gameObject.SetActive(true);
            actionBubbleBg.transform.DOKill();
            actionBubbleBg.transform.localScale = Vector3.one * 0.6f;
            actionBubbleBg.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetLink(actionBubbleBg.gameObject);

            if (_actionBubbleCoroutine != null) StopCoroutine(_actionBubbleCoroutine);
            _actionBubbleCoroutine = StartCoroutine(HideActionBubbleRoutine(duration));
        }

        private System.Collections.IEnumerator HideActionBubbleRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (actionBubbleBg != null)
            {
                actionBubbleBg.transform.DOKill();
                actionBubbleBg.transform.DOScale(0.5f, 0.15f).SetEase(Ease.InQuad)
                    .SetLink(actionBubbleBg.gameObject)
                    .OnComplete(() =>
                    {
                        if (actionBubbleBg != null) actionBubbleBg.gameObject.SetActive(false);
                    });
            }
            _actionBubbleCoroutine = null;
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
            // Skip the destroy+rebuild when this seat's card count hasn't actually
            // changed. RefreshAllDisplay() runs on every OnStateChanged tick — which
            // fires for ANY seat's play, not just this one — and this method used to
            // unconditionally ClearCards() + respawn every face-down back every time
            // it was called. That meant every AI seat's still-in-hand cards were
            // destroyed and instantly recreated whenever ANY player played a card,
            // which is what read as a little "shake"/flicker in the other players'
            // hands. Face-down backs carry no per-card state, so if the count is
            // unchanged there's nothing to update.
            if (!animate && cardCount == _spawnedCards.Count)
                return;

            ClearCards();
            if (cardCount <= 0) return;

            float cardW = AICardW;
            float cardH = AICardH;
            float spacing = horizontal ? 20f : 22f;
            float start = -(cardCount - 1) * spacing * 0.5f;

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