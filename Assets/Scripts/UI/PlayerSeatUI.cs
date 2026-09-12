using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Visual UI component representing one of the 4 player seats (South, North, East, West).
    /// Displays avatar, name label, turn glow indicator, action speech bubble,
    /// mini card-backs for AI opponents/partner, and South's interactive hand cards.
    /// </summary>
    public class PlayerSeatUI : MonoBehaviour
    {
        [Header("Seat Identity")]
        public PlayerSeat Seat;

        [Header("UI Elements")]
        [SerializeField] private Image     avatarBg;
        [SerializeField] private Text      avatarInitialText;
        [SerializeField] private Image     turnGlowBorder;
        [SerializeField] private Text      nameLabel;
        [SerializeField] private Image     actionBubbleBg;
        [SerializeField] private Text      actionBubbleText;
        [SerializeField] private Transform cardContainer;

        private readonly List<CardUI> _spawnedCards = new List<CardUI>();
        private Coroutine _actionBubbleCoroutine;

        public Transform CardContainer => cardContainer != null ? cardContainer : transform;

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
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(900, 180);
                cardContainer = cc.transform;
            }

            if (avatarBg == null)
            {
                GameObject av = new GameObject("Avatar");
                av.transform.SetParent(transform, false);
                RectTransform rt = av.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(56, 56);
                avatarBg = av.AddComponent<Image>();
                avatarBg.sprite = CardVisualTheme.CircleAvatar;
                avatarBg.raycastTarget = false;

                GameObject avTxt = new GameObject("Initial");
                avTxt.transform.SetParent(av.transform, false);
                RectTransform art = avTxt.AddComponent<RectTransform>();
                art.anchorMin = Vector2.zero;
                art.anchorMax = Vector2.one;
                art.sizeDelta = Vector2.zero;
                avatarInitialText = avTxt.AddComponent<Text>();
                avatarInitialText.font = CardVisualTheme.GetFont();
                avatarInitialText.fontSize = 26;
                avatarInitialText.fontStyle = FontStyle.Bold;
                avatarInitialText.alignment = TextAnchor.MiddleCenter;
                avatarInitialText.color = CardVisualTheme.ColorGold;
                avatarInitialText.raycastTarget = false;
            }

            if (turnGlowBorder == null)
            {
                GameObject glow = new GameObject("TurnGlow");
                glow.transform.SetParent(avatarBg.transform, false);
                RectTransform grt = glow.AddComponent<RectTransform>();
                grt.anchorMin = Vector2.zero;
                grt.anchorMax = Vector2.one;
                grt.sizeDelta = new Vector2(16, 16);
                turnGlowBorder = glow.AddComponent<Image>();
                turnGlowBorder.sprite = CardVisualTheme.CreateCircleSprite(72, Color.clear, CardVisualTheme.ColorGold, 5);
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
                nrt.sizeDelta = new Vector2(180, 26);
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
                abrt.sizeDelta = new Vector2(120, 32);
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
                actionBubbleText.fontSize = 15;
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
            switch (Seat)
            {
                case PlayerSeat.South:
                    nameLabel.text = "YOU (South) ★";
                    nameLabel.color = CardVisualTheme.ColorCyan;
                    avatarInitialText.text = "S";
                    avatarInitialText.color = CardVisualTheme.ColorCyan;
                    break;
                case PlayerSeat.North:
                    nameLabel.text = "PARTNER (North)";
                    nameLabel.color = CardVisualTheme.ColorCyan;
                    avatarInitialText.text = "N";
                    avatarInitialText.color = CardVisualTheme.ColorCyan;
                    break;
                case PlayerSeat.East:
                    nameLabel.text = "EAST (Opponent)";
                    nameLabel.color = new Color(0.95f, 0.55f, 0.35f);
                    avatarInitialText.text = "E";
                    avatarInitialText.color = new Color(0.95f, 0.55f, 0.35f);
                    break;
                case PlayerSeat.West:
                    nameLabel.text = "WEST (Opponent)";
                    nameLabel.color = new Color(0.95f, 0.55f, 0.35f);
                    avatarInitialText.text = "W";
                    avatarInitialText.color = new Color(0.95f, 0.55f, 0.35f);
                    break;
            }
        }

        public void SetLayoutPositions(Vector2 avatarPos, Vector2 namePos, Vector2 bubblePos)
        {
            EnsureComponents();
            if (avatarBg != null) ((RectTransform)avatarBg.transform).anchoredPosition = avatarPos;
            if (nameLabel != null) ((RectTransform)nameLabel.transform).anchoredPosition = namePos;
            if (actionBubbleBg != null) ((RectTransform)actionBubbleBg.transform).anchoredPosition = bubblePos;
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
                    turnGlowBorder.transform.DOScale(1.18f, 0.65f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
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
            actionBubbleBg.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack);

            if (_actionBubbleCoroutine != null) StopCoroutine(_actionBubbleCoroutine);
            _actionBubbleCoroutine = StartCoroutine(HideActionBubbleRoutine(duration));
        }

        private System.Collections.IEnumerator HideActionBubbleRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (actionBubbleBg != null)
            {
                actionBubbleBg.transform.DOKill();
                actionBubbleBg.transform.DOScale(0.5f, 0.15f).SetEase(Ease.InQuad).OnComplete(() =>
                {
                    actionBubbleBg.gameObject.SetActive(false);
                });
            }
            _actionBubbleCoroutine = null;
        }

        /// <summary>Renders South's cards as full interactive CardUI objects.
        /// When animate=true (deal batch):
        ///   • First deal: animates all 4 dealt cards.
        ///   • Second deal: existing 4 cards smoothly slide to new spacing; only the 4 NEW cards animate flight.
        /// When animate=false (turn change / status refresh):
        ///   • Updates playability in place without re-animating or recreating cards.
        /// </summary>
        public void RenderHumanHand(Hand hand, List<Card> validPlays, Action<Card> onCardClick, bool animate = false)
        {
            if (hand == null || hand.Count == 0)
            {
                ClearCards();
                return;
            }

            int count = hand.Count;
            float cardW = 105f;
            float cardH = 155f;
            float spacing = Mathf.Min(cardW * 0.92f, 850f / Mathf.Max(1, count));
            float startX  = -(count - 1) * spacing * 0.5f;

            // Check if existing spawned cards match the hand exactly (same cards in same order)
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

            // Case 1: Same hand (e.g. bidding turn change, state refresh) -> purely update playability!
            if (sameHand)
            {
                for (int i = 0; i < count; i++)
                {
                    Card card = hand.Cards[i];
                    bool isPlayable = validPlays != null && validPlays.Contains(card);
                    _spawnedCards[i].SetPlayable(isPlayable);
                }
                return; // Absolutely NO animation, NO recreation!
            }

            // Case 2: Second batch deal (hand grew from existing cards, e.g. 4 -> 8 cards)
            int existingCount = _spawnedCards.Count;
            bool isAppend = existingCount > 0 && count > existingCount;
            if (isAppend)
            {
                // Verify the existing cards match prefix
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
                // Smoothly shift existing cards to their new positions in the expanded spread
                for (int i = 0; i < existingCount; i++)
                {
                    Card card = hand.Cards[i];
                    bool isPlayable = validPlays != null && validPlays.Contains(card);
                    _spawnedCards[i].SetPlayable(isPlayable);

                    Vector2 targetPos = new Vector2(startX + i * spacing, 0);
                    _spawnedCards[i].SetBasePosition(targetPos);
                    RectTransform rtExisting = _spawnedCards[i].GetComponent<RectTransform>();
                    if (rtExisting != null)
                    {
                        rtExisting.DOKill();
                        rtExisting.DOAnchorPos(targetPos, 0.25f).SetEase(Ease.OutQuad);
                    }
                }

                // Spawn and animate ONLY the new cards (indices existingCount .. count - 1)
                Vector2 dealOrigin = new Vector2(0, 350f);
                for (int i = existingCount; i < count; i++)
                {
                    Card card = hand.Cards[i];
                    bool isPlayable = validPlays != null && validPlays.Contains(card);

                    GameObject cardObj = new GameObject($"Card_{card.Rank}_{card.Suit}");
                    cardObj.transform.SetParent(CardContainer, false);
                    RectTransform rt = cardObj.AddComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(cardW, cardH);

                    Vector2 pos = new Vector2(startX + i * spacing, 0);
                    rt.anchoredPosition = pos;

                    CardUI cardUI = cardObj.AddComponent<CardUI>();
                    cardUI.SetCard(card, isPlayable, onCardClick);
                    cardUI.SetBasePosition(pos);

                    if (animate)
                    {
                        cardUI.AnimateDealFrom(dealOrigin, pos, delay: (i - existingCount) * 0.06f, duration: 0.32f);
                    }

                    _spawnedCards.Add(cardUI);
                }
                return;
            }

            // Case 3: Fresh deal or full hand reset (e.g. first 4 cards dealt, or new round)
            ClearCards();

            Vector2 origin = new Vector2(0, 350f);
            for (int i = 0; i < count; i++)
            {
                Card card = hand.Cards[i];
                bool isPlayable = validPlays != null && validPlays.Contains(card);

                GameObject cardObj = new GameObject($"Card_{card.Rank}_{card.Suit}");
                cardObj.transform.SetParent(CardContainer, false);
                RectTransform rt = cardObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cardW, cardH);

                Vector2 pos = new Vector2(startX + i * spacing, 0);
                rt.anchoredPosition = pos;

                CardUI cardUI = cardObj.AddComponent<CardUI>();
                cardUI.SetCard(card, isPlayable, onCardClick);
                cardUI.SetBasePosition(pos);

                if (animate)
                {
                    cardUI.AnimateDealFrom(origin, pos, delay: i * 0.06f, duration: 0.32f);
                }

                _spawnedCards.Add(cardUI);
            }
        }

        /// <summary>Renders AI partner/opponent remaining cards as mini card backs.</summary>
        public void RenderAICardCount(int cardCount, bool horizontal = true)
        {
            ClearCards();
            if (cardCount <= 0) return;

            float cardW = 44f;
            float cardH = 64f;
            float spacing = 16f;
            float start = -(cardCount - 1) * spacing * 0.5f;

            for (int i = 0; i < cardCount; i++)
            {
                GameObject cardObj = new GameObject($"AICardBack_{i}");
                cardObj.transform.SetParent(CardContainer, false);
                RectTransform rt = cardObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cardW, cardH);

                if (horizontal)
                    rt.anchoredPosition = new Vector2(start + i * spacing, 0);
                else
                    rt.anchoredPosition = new Vector2(0, start + i * spacing);

                CardUI cardUI = cardObj.AddComponent<CardUI>();
                cardUI.SetFaceDown();
                _spawnedCards.Add(cardUI);
            }
        }

        public void ClearCards()
        {
            for (int i = _spawnedCards.Count - 1; i >= 0; i--)
            {
                if (_spawnedCards[i] != null && _spawnedCards[i].gameObject != null)
                {
                    // Kill all tweens before destroying to prevent MissingReferenceException
                    _spawnedCards[i].transform.DOKill();
                    Destroy(_spawnedCards[i].gameObject);
                }
            }
            _spawnedCards.Clear();

            // Safety cleanup: also ensure no orphan card GameObjects remain
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
