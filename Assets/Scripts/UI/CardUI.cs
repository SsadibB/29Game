using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Visual representation of a single playing card in Unity uGUI.
    /// Handles face-up card display (Rank, Suit, Points Badge), face-down CardBack display,
    /// playability highlights, hover elevation, and click callbacks.
    /// </summary>
    public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // ── Visual Components ───────────────────────────────────────────────────
        [Header("Card Visuals")]
        [SerializeField] private Image       bgImage;
        [SerializeField] private Image       glowOutline;
        [SerializeField] private Text        rankTopLeft;
        [SerializeField] private Text        suitTopLeft;
        [SerializeField] private Text        centerSuitText;
        [SerializeField] private Image       centerSuitImage;
        [SerializeField] private Text        rankBottomRight;
        [SerializeField] private Text        suitBottomRight;
        [SerializeField] private GameObject  pointsBadgeObj;
        [SerializeField] private Image       pointsBadgeBg;
        [SerializeField] private Text        pointsBadgeText;
        [SerializeField] private Button      button;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform rectTransform;

        // ── State ───────────────────────────────────────────────────────────────
        public Card   CurrentCard { get; private set; }
        public bool   IsPlayable  { get; private set; }
        public bool   IsFaceUp    { get; private set; }

        private Action<Card> _onClickCallback;
        private Vector2      _basePosition;
        private bool         _isHovered;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnDestroy()
        {
            // Kill all running tweens on this object to prevent MissingReferenceException
            transform.DOKill();
            if (rectTransform != null) rectTransform.DOKill();
            if (canvasGroup   != null) canvasGroup.DOKill();
        }

        /// <summary>Ensures all required uGUI components exist on this GameObject.</summary>
        public void EnsureComponents()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
                if (rectTransform == null)
                    rectTransform = gameObject.AddComponent<RectTransform>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (bgImage == null)
            {
                bgImage = GetComponent<Image>();
                if (bgImage == null)
                    bgImage = gameObject.AddComponent<Image>();
                bgImage.type = Image.Type.Simple;
                bgImage.preserveAspect = true;
            }

            if (button == null)
            {
                button = GetComponent<Button>();
                if (button == null)
                    button = gameObject.AddComponent<Button>();
            }

            button.targetGraphic = bgImage;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);

            // Create child elements if not yet built
            if (glowOutline == null)
                glowOutline = CreateChildImage("GlowOutline", new Vector2(0, 0), new Vector2(1, 1), 0f, 0f);

            if (rankTopLeft == null)
                rankTopLeft = CreateChildText("RankTopLeft", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -8), new Vector2(36, 32), TextAnchor.UpperLeft, 22, FontStyle.Bold);

            if (suitTopLeft == null)
                suitTopLeft = CreateChildText("SuitTopLeft", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -32), new Vector2(36, 26), TextAnchor.UpperLeft, 20, FontStyle.Normal);

            if (centerSuitText == null)
                centerSuitText = CreateChildText("CenterSuit", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80), TextAnchor.MiddleCenter, 54, FontStyle.Normal);

            if (rankBottomRight == null)
                rankBottomRight = CreateChildText("RankBottomRight", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 8), new Vector2(36, 32), TextAnchor.LowerRight, 22, FontStyle.Bold);

            if (suitBottomRight == null)
                suitBottomRight = CreateChildText("SuitBottomRight", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 32), new Vector2(36, 26), TextAnchor.LowerRight, 20, FontStyle.Normal);

            if (pointsBadgeObj == null)
            {
                pointsBadgeObj = new GameObject("PointsBadge");
                pointsBadgeObj.transform.SetParent(transform, false);
                RectTransform rt = pointsBadgeObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot     = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(-6, -6);
                rt.sizeDelta = new Vector2(46, 22);

                pointsBadgeBg = pointsBadgeObj.AddComponent<Image>();
                pointsBadgeBg.sprite = CardVisualTheme.PillBadge;
                pointsBadgeBg.type = Image.Type.Sliced;
                pointsBadgeBg.raycastTarget = false;

                pointsBadgeText = CreateChildText("Text", new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 13, FontStyle.Bold, pointsBadgeObj.transform);
                pointsBadgeText.color = Color.white;
            }

            if (glowOutline != null)
            {
                glowOutline.sprite = CardVisualTheme.CreateRoundedRectSprite(180, 260, 22, Color.clear, CardVisualTheme.ColorGold, 6);
                glowOutline.type = Image.Type.Sliced;
                glowOutline.gameObject.SetActive(false);
            }
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Configures the card face up with full rules and point values.</summary>
        public void SetCard(Card card, bool isPlayable, Action<Card> onClick)
        {
            EnsureComponents();
            CurrentCard = card;
            IsPlayable  = isPlayable;
            IsFaceUp    = true;
            _onClickCallback = onClick;

            gameObject.SetActive(true);
            bgImage.sprite = CardVisualTheme.CardFront;
            bgImage.color  = Color.white;
            bgImage.raycastTarget = true;

            Color suitColor = CardVisualTheme.GetSuitColor(card.Suit);
            string suitSym  = CardVisualTheme.GetSuitSymbol(card.Suit);
            string rankStr  = CardVisualTheme.GetRankString(card.Rank);
            int pts         = CardVisualTheme.GetPoints(card.Rank);

            // Labels
            rankTopLeft.text = rankStr;
            rankTopLeft.color = suitColor;
            suitTopLeft.text = suitSym;
            suitTopLeft.color = suitColor;

            centerSuitText.text = suitSym;
            centerSuitText.color = suitColor;

            rankBottomRight.text = rankStr;
            rankBottomRight.color = suitColor;
            suitBottomRight.text = suitSym;
            suitBottomRight.color = suitColor;

            // Show face-up elements
            rankTopLeft.gameObject.SetActive(true);
            suitTopLeft.gameObject.SetActive(true);
            centerSuitText.gameObject.SetActive(true);
            rankBottomRight.gameObject.SetActive(true);
            suitBottomRight.gameObject.SetActive(true);

            // Point badge
            if (pts > 0)
            {
                pointsBadgeObj.SetActive(true);
                pointsBadgeBg.color = CardVisualTheme.GetPointsBadgeColor(pts);
                pointsBadgeText.text = $"+{pts}";
            }
            else
            {
                pointsBadgeObj.SetActive(false);
            }

            // Playability and interactability
            SetPlayable(isPlayable);
        }

        /// <summary>Configures the card as face-down showing the card back.</summary>
        public void SetFaceDown()
        {
            EnsureComponents();
            CurrentCard = null;
            IsPlayable  = false;
            IsFaceUp    = false;
            _onClickCallback = null;

            gameObject.SetActive(true);
            bgImage.sprite = CardVisualTheme.CardBack;
            bgImage.color  = Color.white;
            bgImage.raycastTarget = false;

            rankTopLeft.gameObject.SetActive(false);
            suitTopLeft.gameObject.SetActive(false);
            centerSuitText.gameObject.SetActive(false);
            rankBottomRight.gameObject.SetActive(false);
            suitBottomRight.gameObject.SetActive(false);
            pointsBadgeObj.SetActive(false);

            if (glowOutline != null) glowOutline.gameObject.SetActive(false);
            canvasGroup.alpha = 1f;
            button.interactable = false;
        }

        /// <summary>Configures the card as an empty transparent slot outline.</summary>
        public void SetEmptySlot()
        {
            EnsureComponents();
            CurrentCard = null;
            IsPlayable  = false;
            IsFaceUp    = false;
            _onClickCallback = null;

            bgImage.sprite = CardVisualTheme.RoundedCardSlot;
            bgImage.color  = new Color(1f, 1f, 1f, 0.45f);
            bgImage.raycastTarget = false;

            rankTopLeft.gameObject.SetActive(false);
            suitTopLeft.gameObject.SetActive(false);
            centerSuitText.gameObject.SetActive(false);
            rankBottomRight.gameObject.SetActive(false);
            suitBottomRight.gameObject.SetActive(false);
            pointsBadgeObj.SetActive(false);

            if (glowOutline != null) glowOutline.gameObject.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0.5f;
            if (button != null) button.interactable = false;
        }

        public void SetPlayable(bool playable)
        {
            IsPlayable = playable;
            if (button != null) button.interactable = playable && _onClickCallback != null;

            if (canvasGroup != null)
                canvasGroup.alpha = playable ? 1.0f : 0.55f;

            if (glowOutline != null)
                glowOutline.gameObject.SetActive(playable);
        }

        public void SetGlow(bool glow, Color color)
        {
            if (glowOutline != null)
            {
                glowOutline.gameObject.SetActive(glow);
                glowOutline.color = color;
            }
        }

        public void SetBasePosition(Vector2 pos)
        {
            _basePosition = pos;
            if (rectTransform != null) rectTransform.anchoredPosition = pos;
        }

        // ── DOTween Animations ───────────────────────────────────────────────────

        /// <summary>Animates a dealt card flying into the hand with smooth scale and fade.</summary>
        public void AnimateDealFrom(Vector2 startPos, Vector2 endPos, float delay, float duration = 0.32f, Action onComplete = null)
        {
            EnsureComponents();
            _basePosition = endPos;
            rectTransform.DOKill();
            rectTransform.anchoredPosition = startPos;
            rectTransform.localScale = Vector3.one * 0.5f;
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            Sequence seq = DOTween.Sequence();
            seq.SetDelay(delay);
            seq.SetLink(gameObject);  // auto-kill when this GameObject is destroyed
            if (canvasGroup != null)
                seq.Append(canvasGroup.DOFade(IsPlayable ? 1f : 0.55f, 0.1f));
            seq.Join(rectTransform.DOAnchorPos(endPos, duration).SetEase(Ease.OutCubic));
            seq.Join(rectTransform.DOScale(1f, duration).SetEase(Ease.OutBack));
            if (onComplete != null)
                seq.OnComplete(() =>
                {
                    if (this != null && gameObject != null)
                        onComplete.Invoke();
                });
        }

        /// <summary>Animates card flying to a target trick slot or winning seat.</summary>
        public void AnimatePlayTo(Vector2 targetPos, float duration = 0.25f, Action onComplete = null)
        {
            if (rectTransform == null) return;
            rectTransform.DOKill();
            Sequence seq = DOTween.Sequence();
            seq.SetLink(gameObject);
            seq.Append(rectTransform.DOAnchorPos(targetPos, duration).SetEase(Ease.OutQuad));
            seq.Join(rectTransform.DOScale(1f, duration));
            if (onComplete != null)
                seq.OnComplete(() =>
                {
                    if (this != null && gameObject != null)
                        onComplete.Invoke();
                });
        }

        /// <summary>Animates trick collection sweep towards a winner's seat with fade out.</summary>
        public void AnimateSweepTo(Vector2 targetPos, float duration = 0.35f, Action onComplete = null)
        {
            if (rectTransform == null) return;
            rectTransform.DOKill();
            Sequence seq = DOTween.Sequence();
            seq.SetLink(gameObject);
            seq.Append(rectTransform.DOAnchorPos(targetPos, duration).SetEase(Ease.InCubic));
            seq.Join(rectTransform.DOScale(0.5f, duration).SetEase(Ease.InQuad));
            if (canvasGroup != null)
                seq.Join(canvasGroup.DOFade(0f, duration));
            if (onComplete != null)
                seq.OnComplete(() =>
                {
                    if (this != null && gameObject != null)
                        onComplete.Invoke();
                });
        }

        // ── Interaction ──────────────────────────────────────────────────────────

        private void HandleClick()
        {
            if (!IsPlayable || CurrentCard == null) return;

            Card cardToPlay = CurrentCard; // capture before DOTween async
            transform.DOKill();
            transform.DOPunchScale(Vector3.one * 0.15f, 0.15f, 8, 1).SetLink(gameObject).OnComplete(() =>
            {
                if (this != null && gameObject != null)
                    _onClickCallback?.Invoke(cardToPlay);
            });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsPlayable) return;
            _isHovered = true;
            if (rectTransform != null)
            {
                rectTransform.DOKill();
                rectTransform.DOAnchorPosY(_basePosition.y + 22f, 0.14f).SetEase(Ease.OutQuad);
                rectTransform.DOScale(1.06f, 0.14f).SetEase(Ease.OutQuad);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isHovered) return;
            _isHovered = false;
            if (rectTransform != null)
            {
                rectTransform.DOKill();
                rectTransform.DOAnchorPosY(_basePosition.y, 0.14f).SetEase(Ease.OutQuad);
                rectTransform.DOScale(1.0f, 0.14f).SetEase(Ease.OutQuad);
            }
        }

        // ── Helper Builders ──────────────────────────────────────────────────────

        private Image CreateChildImage(string name, Vector2 anchorMin, Vector2 anchorMax, float offsetMin, float offsetMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(offsetMin, offsetMin);
            rt.offsetMax = new Vector2(offsetMax, offsetMax);
            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private Text CreateChildText(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, TextAnchor align, int fontSize, FontStyle style, Transform parent = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = anchorMin;
            rt.anchoredPosition = pos;
            if (size != Vector2.zero) rt.sizeDelta = size;

            Text txt = go.AddComponent<Text>();
            txt.font = CardVisualTheme.GetFont();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = align;
            txt.raycastTarget = false;
            txt.supportRichText = false;
            return txt;
        }
    }
}
