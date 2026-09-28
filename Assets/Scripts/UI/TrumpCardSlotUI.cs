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

            HideOverlay("GlowOutline");
            HideOverlay("StatusLabel");
            HideOverlay("SuitSymbol");
            HideOverlay("SuitName");
            glowOutline = null;
            statusLabelText = null;
            suitSymbolText = null;
            suitNameText = null;

            if (tapToRevealText == null)
            {
                Transform existingTap = transform.Find("TapToReveal");
                if (existingTap != null)
                    tapToRevealText = existingTap.GetComponent<Text>();
            }
        }

        private void HideOverlay(string childName)
        {
            Transform child = transform.Find(childName);
            if (child != null)
                child.gameObject.SetActive(false);
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
                Sprite jokerFace = CardVisualTheme.LoadCardSprite("JOKER");
                if (jokerFace != null)
                {
                    cardBg.sprite = jokerFace;
                    cardBg.color = Color.white;
                }
                else
                {
                    cardBg.sprite = CardVisualTheme.RoundedCardSlot;
                    cardBg.color = new Color(0.12f, 0.20f, 0.35f, 0.95f);
                }
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
            cardBg.type = Image.Type.Simple;
            cardBg.preserveAspect = true;
            cardBg.color = Color.white;

            if (tapToRevealText != null)
            {
                tapToRevealText.gameObject.SetActive(true);
                tapToRevealText.text = canReveal ? "TAP TO REVEAL" : "HIDDEN";
            }

            if (canReveal) StartPulse();
            else StopPulse();
        }

        private void ApplyRevealedState(Suit trump, Card faceCard = null)
        {
            StopPulse();
            cardBg.type = Image.Type.Simple;
            cardBg.preserveAspect = true;
            cardBg.color = Color.white;

            Sprite faceArt = faceCard != null
                ? CardVisualTheme.GetCardFace(faceCard)
                : CardVisualTheme.GetTrumpMarkerFace(trump);
            if (faceArt != null)
                cardBg.sprite = faceArt;
            else
                cardBg.sprite = CardVisualTheme.CardFront;

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
        }

        private void StopPulse()
        {
            if (_pulseTween != null)
            {
                _pulseTween.Kill();
                _pulseTween = null;
            }
            if (cardBg != null)
            {
                Color c = cardBg.color;
                c.a = 1f;
                cardBg.color = c;
            }
        }

        /// <summary>
        /// Removes the glow highlight entirely (e.g. when a new round starts and the
        /// previous trump card selection should no longer be highlighted).
        /// </summary>
        public void ResetHighlight()
        {
            StopPulse();
        }
    }
}
