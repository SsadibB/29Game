using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Visual representation of the single Trump Card placed on the board.
    /// In 29, the bid winner sets the trump card face-down on the table.
    /// When someone wants to reveal the trump card during play, they simply click it.
    /// Clicking the card triggers a 3D flip animation revealing the secret trump suit/card.
    /// </summary>
    public class TrumpCardSlotUI : MonoBehaviour
    {
        [Header("Slot Components")]
        [SerializeField] private Image  cardBg;
        [SerializeField] private Image  glowOutline;
        [SerializeField] private Text   suitSymbolText;
        [SerializeField] private Text   suitNameText;
        [SerializeField] private Text   statusLabelText;
        [SerializeField] private Text   tapToRevealText;
        [SerializeField] private Button cardButton;

        private bool _wasRevealed = false;
        private Tween _pulseTween;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnDestroy()
        {
            transform.DOKill();
            _pulseTween?.Kill();
        }

        public void EnsureComponents()
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110, 160);

            if (cardBg == null)
            {
                cardBg = GetComponent<Image>();
                if (cardBg == null) cardBg = gameObject.AddComponent<Image>();
                cardBg.sprite = CardVisualTheme.CardBack;
                cardBg.type = Image.Type.Simple;
                cardBg.preserveAspect = true;
                cardBg.raycastTarget = true;
            }

            if (cardButton == null)
            {
                cardButton = GetComponent<Button>();
                if (cardButton == null) cardButton = gameObject.AddComponent<Button>();
                cardButton.targetGraphic = cardBg;
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(OnCardClicked);
            }

            if (glowOutline == null)
            {
                GameObject gObj = new GameObject("GlowOutline");
                gObj.transform.SetParent(transform, false);
                RectTransform grt = gObj.AddComponent<RectTransform>();
                grt.anchorMin = Vector2.zero;
                grt.anchorMax = Vector2.one;
                grt.offsetMin = Vector2.zero;
                grt.offsetMax = Vector2.zero;

                glowOutline = gObj.AddComponent<Image>();
                glowOutline.sprite = CardVisualTheme.CreateRoundedRectSprite(110, 160, 18, Color.clear, CardVisualTheme.ColorGold, 5);
                glowOutline.type = Image.Type.Sliced;
                glowOutline.raycastTarget = false;
                glowOutline.gameObject.SetActive(false);
            }

            if (statusLabelText == null)
            {
                GameObject lObj = CreateText("StatusLabel", new Vector2(0, 96), 13, FontStyle.Bold, CardVisualTheme.ColorGold);
                statusLabelText = lObj.GetComponent<Text>();
                statusLabelText.text = "TRUMP CARD";
            }

            if (suitSymbolText == null)
            {
                GameObject sObj = CreateText("SuitSymbol", new Vector2(0, 14), 54, FontStyle.Bold, CardVisualTheme.ColorGold);
                suitSymbolText = sObj.GetComponent<Text>();
            }

            if (suitNameText == null)
            {
                GameObject nObj = CreateText("SuitName", new Vector2(0, -36), 13, FontStyle.Bold, Color.white);
                suitNameText = nObj.GetComponent<Text>();
            }

            if (tapToRevealText == null)
            {
                GameObject trObj = CreateText("TapToReveal", new Vector2(0, -96), 11, FontStyle.Bold, new Color(0.95f, 0.85f, 0.40f, 0.95f));
                tapToRevealText = trObj.GetComponent<Text>();
                tapToRevealText.text = "TAP TO REVEAL";
            }
        }

        public void UpdateDisplay(GameManager gm)
        {
            EnsureComponents();
            if (gm == null) return;

            bool isTrumpPhaseOrPlaying = gm.CurrentPhase >= GamePhase.TrumpSelection && gm.CurrentPhase <= GamePhase.Playing;
            if (!isTrumpPhaseOrPlaying)
            {
                gameObject.SetActive(false);
                _wasRevealed = false;
                StopPulse();
                return;
            }

            gameObject.SetActive(true);

            TrumpManager tm = gm.TrumpManager;
            if (tm == null) return;

            // Handle Joker (No-Trump) Mode
            if (tm.IsJoker)
            {
                StopPulse();
                cardBg.sprite = CardVisualTheme.RoundedCardSlot;
                cardBg.color  = new Color(0.12f, 0.20f, 0.35f, 0.95f);
                suitSymbolText.gameObject.SetActive(true);
                suitSymbolText.text = "🃏";
                suitSymbolText.color = new Color(0.5f, 0.85f, 1f);
                suitNameText.gameObject.SetActive(true);
                suitNameText.text = "NO TRUMP";
                suitNameText.color = Color.white;
                statusLabelText.text = "★ JOKER ★";
                statusLabelText.color = new Color(0.5f, 0.85f, 1f);
                glowOutline.gameObject.SetActive(true);
                if (tapToRevealText != null) tapToRevealText.gameObject.SetActive(false);
                return;
            }

            bool isRevealed = gm.IsTrumpRevealed();
            Suit? actualTrump = tm.TrumpSuit;

            if (isRevealed && !_wasRevealed && actualTrump.HasValue)
            {
                _wasRevealed = true;
                AnimateFlipToRevealed(actualTrump.Value, tm.IsSeventhCard ? tm.SeventhCard : null);
                return;
            }

            if (isRevealed && actualTrump.HasValue)
            {
                _wasRevealed = true;
                ApplyRevealedState(actualTrump.Value, tm.IsSeventhCard ? tm.SeventhCard : null);
            }
            else if (actualTrump.HasValue || tm.IsSeventhCard)
            {
                ApplyFaceDownState(tm.IsSeventhCard, gm.CanHumanRevealTrump());
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyFaceDownState(bool isSeventhCard, bool canReveal)
        {
            transform.localScale = Vector3.one;
            _wasRevealed = false;
            cardBg.sprite = CardVisualTheme.CardBack;
            cardBg.color = Color.white;

            statusLabelText.text = isSeventhCard ? "TRUMP (7th Card)" : "TRUMP CARD";
            statusLabelText.color = CardVisualTheme.ColorGold;

            suitSymbolText.gameObject.SetActive(false);
            suitNameText.gameObject.SetActive(false);

            if (tapToRevealText != null)
            {
                tapToRevealText.gameObject.SetActive(true);
                tapToRevealText.text = canReveal ? "TAP TO REVEAL" : "HIDDEN";
            }

            if (canReveal) StartPulse();
            else
            {
                StopPulse();
                glowOutline.gameObject.SetActive(false);
            }
        }

        private void ApplyRevealedState(Suit trump, Card faceCard = null)
        {
            StopPulse();
            cardBg.sprite = CardVisualTheme.CardFront;
            cardBg.color = Color.white;

            Color col = CardVisualTheme.GetSuitColor(trump);
            suitSymbolText.gameObject.SetActive(true);
            suitNameText.gameObject.SetActive(true);

            if (faceCard != null)
            {
                suitSymbolText.text = $"{CardVisualTheme.GetRankString(faceCard.Rank)}\n{CardVisualTheme.GetSuitSymbol(trump)}";
                suitSymbolText.color = col;
                suitNameText.text = $"{CardVisualTheme.GetRankString(faceCard.Rank)} OF {CardVisualTheme.GetSuitName(trump).ToUpper()}";
                suitNameText.color = col;
                statusLabelText.text = "★ 7TH CARD TRUMP ★";
            }
            else
            {
                suitSymbolText.text = CardVisualTheme.GetSuitSymbol(trump);
                suitSymbolText.color = col;
                suitNameText.text = CardVisualTheme.GetSuitName(trump).ToUpper();
                suitNameText.color = col;
                statusLabelText.text = "★ TRUMP ★";
            }

            statusLabelText.color = CardVisualTheme.ColorGold;
            glowOutline.gameObject.SetActive(true);

            if (tapToRevealText != null)
                tapToRevealText.gameObject.SetActive(false);
        }

        private void AnimateFlipToRevealed(Suit trump, Card faceCard)
        {
            StopPulse();
            transform.DOKill();
            transform.DOScaleX(0f, 0.16f).SetEase(Ease.InQuad).SetLink(gameObject).OnComplete(() =>
            {
                if (this == null || gameObject == null) return;
                ApplyRevealedState(trump, faceCard);
                transform.DOScaleX(1f, 0.20f).SetEase(Ease.OutQuad).SetLink(gameObject).OnComplete(() =>
                {
                    if (this != null && gameObject != null)
                        transform.DOPunchScale(Vector3.one * 0.18f, 0.25f, 8, 1).SetLink(gameObject);
                });
            });
        }

        private void OnCardClicked()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.CanHumanRevealTrump())
            {
                Debug.Log("[29 TrumpSlot] Player cannot follow suit — revealing trump.");
                gm.RevealTrump();
                return;
            }

            transform.DOPunchScale(Vector3.one * 0.08f, 0.18f, 6, 1).SetLink(gameObject);
        }

        private void StartPulse()
        {
            if (_pulseTween != null && _pulseTween.IsActive()) return;
            glowOutline.gameObject.SetActive(true);
            glowOutline.color = new Color(CardVisualTheme.ColorGold.r, CardVisualTheme.ColorGold.g, CardVisualTheme.ColorGold.b, 0.2f);
            _pulseTween = glowOutline.DOFade(0.7f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }

        private void StopPulse()
        {
            if (_pulseTween != null)
            {
                _pulseTween.Kill();
                _pulseTween = null;
            }
        }

        private GameObject CreateText(string name, Vector2 pos, int fontSize, FontStyle style, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(160, 32);

            Text txt = obj.AddComponent<Text>();
            txt.font = CardVisualTheme.GetFont();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return obj;
        }
    }
}
