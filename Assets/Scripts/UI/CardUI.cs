using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Visual representation of a single playing card in Unity uGUI.
    /// Handles face-up card display (Rank, Suit), face-down CardBack display,
    /// playability highlights, hover elevation, and click callbacks.
    /// </summary>
    public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // ── Visual Components ───────────────────────────────────────────────────
        [Header("Card Visuals")]
        [SerializeField] private Image bgImage;
        [SerializeField] private Image cardBorder;
        [SerializeField] private Image glowOutline;
        [SerializeField] private Text rankTopLeft;
        [SerializeField] private Text suitTopLeft;
        [SerializeField] private Text centerSuitText;
        [SerializeField] private Image centerSuitImage;
        [SerializeField] private Text rankBottomRight;
        [SerializeField] private Text suitBottomRight;
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform rectTransform;

        // ── State ───────────────────────────────────────────────────────────────
        public Card CurrentCard { get; private set; }
        public bool IsPlayable { get; private set; }
        public bool IsFaceUp { get; private set; }

        private Action<Card> _onClickCallback;
        private Vector2 _basePosition;
        private bool _isHovered;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnDestroy()
        {
            // Kill all running tweens on this object to prevent MissingReferenceException
            transform.DOKill();
            if (rectTransform != null) rectTransform.DOKill();
            if (canvasGroup != null) canvasGroup.DOKill();
        }

        /// <summary>Ensures all required uGUI components exist on this GameObject.</summary>
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

            // Force every ColorBlock state — including Disabled — to full
            // opaque white. Unity's default Button ColorBlock multiplies the
            // targetGraphic (bgImage) by disabledColor whenever
            // button.interactable == false, and the stock disabledColor is
            // white at ~50% alpha. Since every slot goes through
            // interactable = false at some point (empty slots, face-down
            // cards, and any card marked not-playable), that default tint is
            // what makes cards look translucent even though canvasGroup and
            // bgImage are both fully opaque — so pin every state to alpha
            // 255 here instead of relying on the transition system.
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            button.colors = colors;

            // Create child elements if not yet built
            if (cardBorder == null)
            {
                // Thin, always-on outline hugging the card's edge — gives every
                // card a bit of visual separation/definition against the table
                // and against other overlapping cards, matching the reference look.
                cardBorder = CreateChildImage("CardBorder", new Vector2(0, 0), new Vector2(1, 1), 0f, 0f);
                // Smaller radius than before (was 18) — at the small Trick-area slot
                // size (120x175) an 18px radius cut in far enough that the corner
                // rank/suit labels (12px inset) landed inside the rounded cut and
                // appeared to spill outside the border. 12 keeps the cut shallow
                // enough that the 12px-inset labels stay safely inside it at every
                // card size currently used across the project.
                cardBorder.sprite = CardVisualTheme.CreateRoundedRectSprite(180, 260, 12, Color.clear, Color.white, 3);
                cardBorder.type = Image.Type.Sliced;
                cardBorder.color = new Color(1f, 1f, 1f, 0.8f);
                cardBorder.raycastTarget = false;
            }

            if (glowOutline == null)
                glowOutline = CreateChildImage("GlowOutline", new Vector2(0, 0), new Vector2(1, 1), 0f, 0f);

            if (rankTopLeft == null)
                rankTopLeft = CreateChildText("RankTopLeft", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -8), new Vector2(36, 32), TextAnchor.UpperLeft, 22, FontStyle.Bold);

            if (suitTopLeft == null)
                suitTopLeft = CreateChildText("SuitTopLeft", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -32), new Vector2(36, 26), TextAnchor.UpperLeft, 20, FontStyle.Normal);

            if (centerSuitText == null)
                centerSuitText = CreateChildText("CenterSuit", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80), TextAnchor.MiddleCenter, 54, FontStyle.Normal);

            if (centerSuitImage == null)
            {
                centerSuitImage = CreateChildImage("CenterSuitIcon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -40f, 40f);
                centerSuitImage.preserveAspect = true;
                centerSuitImage.gameObject.SetActive(false);
            }

            if (rankBottomRight == null)
                rankBottomRight = CreateChildText("RankBottomRight", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 8), new Vector2(36, 32), TextAnchor.LowerRight, 22, FontStyle.Bold);

            if (suitBottomRight == null)
                suitBottomRight = CreateChildText("SuitBottomRight", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 32), new Vector2(36, 26), TextAnchor.LowerRight, 20, FontStyle.Normal);

            Transform leftoverBadge = transform.Find("PointsBadge");
            if (leftoverBadge != null)
            {
                if (Application.isPlaying) Destroy(leftoverBadge.gameObject);
                else DestroyImmediate(leftoverBadge.gameObject);
            }

            if (glowOutline != null)
            {
                glowOutline.sprite = CardVisualTheme.CreateRoundedRectSprite(180, 260, 16, Color.clear, CardVisualTheme.ColorGold, 6);
                glowOutline.type = Image.Type.Sliced;
                glowOutline.gameObject.SetActive(false);
            }
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Configures the card face up with full rules and point values.</summary>
        public void SetCard(Card card, bool isPlayable, Action<Card> onClick)
        {
            EnsureComponents();
            // Stop any leftover fade (e.g. from AnimateSweepTo on a trick this slot
            // just collected) before we hand it a new card — otherwise that old
            // tween keeps running afterward and drags the new card's alpha back
            // toward 0, which is what makes freshly-played cards look transparent.
            if (canvasGroup != null) canvasGroup.DOKill();
            CurrentCard = card;
            IsPlayable = isPlayable;
            IsFaceUp = true;
            _onClickCallback = onClick;

            gameObject.SetActive(true);
            bgImage.sprite = CardVisualTheme.CardFront;
            bgImage.color = Color.white;
            bgImage.raycastTarget = true;
            if (cardBorder != null) cardBorder.gameObject.SetActive(true);

            Color suitColor = CardVisualTheme.GetSuitColor(card.Suit);
            string suitSym = CardVisualTheme.GetSuitSymbol(card.Suit);
            string rankStr = CardVisualTheme.GetRankString(card.Rank);

            // Labels
            rankTopLeft.text = rankStr;
            rankTopLeft.color = suitColor;
            suitTopLeft.text = suitSym;
            suitTopLeft.color = suitColor;

            rankBottomRight.text = rankStr;
            rankBottomRight.color = suitColor;
            suitBottomRight.text = suitSym;
            suitBottomRight.color = suitColor;

            // Center suit mark: prefer the SuitIcons artwork if it loaded, fall back
            // to the unicode glyph so cards still render correctly without the asset.
            Sprite suitSprite = CardVisualTheme.GetSuitSprite(card.Suit);
            if (suitSprite != null && centerSuitImage != null)
            {
                centerSuitImage.sprite = suitSprite;
                centerSuitImage.color = Color.white;
                centerSuitImage.gameObject.SetActive(true);
                centerSuitText.gameObject.SetActive(false);
            }
            else
            {
                centerSuitText.text = suitSym;
                centerSuitText.color = suitColor;
                centerSuitText.gameObject.SetActive(true);
                if (centerSuitImage != null) centerSuitImage.gameObject.SetActive(false);
            }

            // Show face-up elements
            rankTopLeft.gameObject.SetActive(true);
            suitTopLeft.gameObject.SetActive(true);
            rankBottomRight.gameObject.SetActive(true);
            suitBottomRight.gameObject.SetActive(true);

            // Playability and interactability
            SetPlayable(isPlayable);
        }

        /// <summary>Configures the card as face-down showing the card back.</summary>
        public void SetFaceDown()
        {
            EnsureComponents();
            if (canvasGroup != null) canvasGroup.DOKill();
            CurrentCard = null;
            IsPlayable = false;
            IsFaceUp = false;
            _onClickCallback = null;

            gameObject.SetActive(true);
            bgImage.sprite = CardVisualTheme.CardBack;
            bgImage.color = Color.white;
            bgImage.raycastTarget = false;
            if (cardBorder != null) cardBorder.gameObject.SetActive(true);

            rankTopLeft.gameObject.SetActive(false);
            suitTopLeft.gameObject.SetActive(false);
            centerSuitText.gameObject.SetActive(false);
            if (centerSuitImage != null) centerSuitImage.gameObject.SetActive(false);
            rankBottomRight.gameObject.SetActive(false);
            suitBottomRight.gameObject.SetActive(false);

            if (glowOutline != null) glowOutline.gameObject.SetActive(false);
            canvasGroup.alpha = 1f;
            button.interactable = false;
        }

        /// <summary>Configures the card as an empty transparent slot outline.</summary>
        public void SetEmptySlot()
        {
            EnsureComponents();
            if (canvasGroup != null) canvasGroup.DOKill();
            CurrentCard = null;
            IsPlayable = false;
            IsFaceUp = false;
            _onClickCallback = null;

            // No background at all for an empty trick slot — fully invisible
            // until a card is actually played into it (previously showed a
            // translucent RoundedCardSlot placeholder here).
            bgImage.sprite = null;
            bgImage.color = new Color(1f, 1f, 1f, 0f);
            bgImage.raycastTarget = false;
            if (cardBorder != null) cardBorder.gameObject.SetActive(false);

            rankTopLeft.gameObject.SetActive(false);
            suitTopLeft.gameObject.SetActive(false);
            centerSuitText.gameObject.SetActive(false);
            if (centerSuitImage != null) centerSuitImage.gameObject.SetActive(false);
            rankBottomRight.gameObject.SetActive(false);
            suitBottomRight.gameObject.SetActive(false);

            if (glowOutline != null) glowOutline.gameObject.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0.5f;
            if (button != null) button.interactable = false;
        }

        public void BindClick(Action<Card> onClick)
        {
            _onClickCallback = onClick;
            if (button != null) button.interactable = _onClickCallback != null;
        }

        public void SetPlayable(bool playable)
        {
            IsPlayable = playable;
            if (button != null) button.interactable = _onClickCallback != null;

            // No hint dimming — all cards stay fully opaque regardless of playability.
            if (canvasGroup != null)
                canvasGroup.alpha = 1.0f;

            // Glow outline is hidden by default
            if (glowOutline != null)
                glowOutline.gameObject.SetActive(false);
        }

        public void SetGlow(bool glow, Color color)
        {
            if (glowOutline != null)
            {
                glowOutline.gameObject.SetActive(glow);
                glowOutline.color = color;
            }
        }

        /// <summary>
        /// Forces the card to full opacity and cancels any in-progress fade.
        /// Used when a card is placed into the trick area so it always
        /// renders fully visible, even if a leftover fade tween (e.g. from a
        /// previous trick's collection sweep) is still running on this slot.
        /// </summary>
        public void SetFullyOpaque()
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.alpha = 1f;
            }
        }

        public void SetBasePosition(Vector2 pos)
        {
            _basePosition = pos;
            if (rectTransform != null) rectTransform.anchoredPosition = pos;
        }

        /// <summary>Shakes the card horizontally when an invalid play is attempted.</summary>
        public void Shake()
        {
            if (rectTransform != null)
            {
                rectTransform.DOKill();
                rectTransform.anchoredPosition = _basePosition;
                rectTransform.DOShakePosition(0.28f, new Vector3(16f, 0f, 0f), 10, 90, false, true).SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        if (rectTransform != null) rectTransform.anchoredPosition = _basePosition;
                    });
            }
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
            // Match SetPlayable()'s "no hint dimming" design — always fade to fully
            // opaque. (Previously faded to 0.55 when IsPlayable was false at the moment
            // this animation was queued, which could get baked-in as stale and never
            // corrected even after a later SetPlayable(true) call.)
            if (canvasGroup != null)
                seq.Append(canvasGroup.DOFade(1f, 0.1f));
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
            seq.Append(rectTransform.DOAnchorPos(targetPos, duration).SetEase(Ease.InOutCubic));
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
            if (CurrentCard == null || _onClickCallback == null) return;

            // Play immediately. Waiting on a punch tween's OnComplete is unsafe:
            // hover/deal DOKill() on the same RectTransform can cancel it, so the
            // card click visually "does nothing" and the trick never starts.
            Card cardToPlay = CurrentCard;
            _onClickCallback.Invoke(cardToPlay);

            if (rectTransform != null)
            {
                rectTransform.DOPunchScale(Vector3.one * 0.12f, 0.12f, 8, 1)
                    .SetId("cardPunch")
                    .SetLink(gameObject);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_onClickCallback == null) return;
            _isHovered = true;
            if (rectTransform != null)
            {
                rectTransform.DOKill();
                rectTransform.DOAnchorPosY(_basePosition.y + 22f, 0.12f).SetEase(Ease.OutQuad);
                rectTransform.DOScale(1.05f, 0.12f).SetEase(Ease.OutQuad);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isHovered) return;
            _isHovered = false;
            if (rectTransform != null)
            {
                rectTransform.DOKill();
                rectTransform.DOAnchorPosY(_basePosition.y, 0.12f).SetEase(Ease.OutQuad);
                rectTransform.DOScale(1.0f, 0.12f).SetEase(Ease.OutQuad);
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
            rt.pivot = anchorMin;
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