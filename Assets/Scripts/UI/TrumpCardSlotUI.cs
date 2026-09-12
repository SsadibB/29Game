using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Displays the Trump card slot on the game table.
    /// Supports:
    ///   • Standard Suit trump (hidden card back -> 3D DOTween flip to revealed suit)
    ///   • 7th Card mode (blind mystery card back -> flips to revealed 7th card)
    ///   • Joker mode (public No-Trump icon & badge)
    ///   • Interactive REVEAL button when it's human player's turn to reveal trump.
    /// </summary>
    public class TrumpCardSlotUI : MonoBehaviour
    {
        [Header("Slot Components")]
        [SerializeField] private Image  cardBg;
        [SerializeField] private Image  glowOutline;
        [SerializeField] private Text   suitSymbolText;
        [SerializeField] private Text   suitNameText;
        [SerializeField] private Text   statusLabelText;
        [SerializeField] private Button revealBtn;

        private bool _wasRevealed = false;

        private void Awake()
        {
            EnsureComponents();
        }

        public void EnsureComponents()
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110, 150);

            if (cardBg == null)
            {
                cardBg = GetComponent<Image>();
                if (cardBg == null) cardBg = gameObject.AddComponent<Image>();
                cardBg.sprite = CardVisualTheme.CardBack;
                cardBg.type = Image.Type.Simple;
                cardBg.preserveAspect = true;
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
                glowOutline.sprite = CardVisualTheme.CreateRoundedRectSprite(110, 150, 16, Color.clear, CardVisualTheme.ColorGold, 5);
                glowOutline.type = Image.Type.Sliced;
                glowOutline.raycastTarget = false;
                glowOutline.gameObject.SetActive(false);
            }

            if (suitSymbolText == null)
            {
                GameObject sObj = CreateText("SuitSymbol", new Vector2(0, 15), 52, FontStyle.Bold, CardVisualTheme.ColorGold);
                suitSymbolText = sObj.GetComponent<Text>();
            }

            if (suitNameText == null)
            {
                GameObject nObj = CreateText("SuitName", new Vector2(0, -32), 13, FontStyle.Bold, Color.white);
                suitNameText = nObj.GetComponent<Text>();
            }

            if (statusLabelText == null)
            {
                GameObject lObj = CreateText("StatusLabel", new Vector2(0, 88), 13, FontStyle.Bold, CardVisualTheme.ColorGold);
                statusLabelText = lObj.GetComponent<Text>();
            }

            if (revealBtn == null)
            {
                GameObject btnObj = new GameObject("RevealButton");
                btnObj.transform.SetParent(transform, false);
                RectTransform brt = btnObj.AddComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 0f);
                brt.anchorMax = new Vector2(0.5f, 0f);
                brt.anchoredPosition = new Vector2(0, -26);
                brt.sizeDelta = new Vector2(100, 32);

                Image bimg = btnObj.AddComponent<Image>();
                bimg.sprite = CardVisualTheme.PillBadge;
                bimg.type = Image.Type.Sliced;
                bimg.color = new Color(0.90f, 0.70f, 0.15f, 0.98f);
                bimg.raycastTarget = true;

                revealBtn = btnObj.AddComponent<Button>();
                revealBtn.targetGraphic = bimg;
                revealBtn.onClick.AddListener(OnRevealClicked);

                GameObject tObj = new GameObject("Text");
                tObj.transform.SetParent(btnObj.transform, false);
                RectTransform trt = tObj.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;

                Text txt = tObj.AddComponent<Text>();
                txt.font = CardVisualTheme.GetFont();
                txt.fontSize = 12;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.black;
                txt.raycastTarget = false;
                txt.text = "REVEAL";
                btnObj.SetActive(false);
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
                return;
            }

            gameObject.SetActive(true);

            TrumpManager tm = gm.TrumpManager;
            if (tm == null) return;

            // Handle Joker (No-Trump) Mode
            if (tm.IsJoker)
            {
                cardBg.sprite = CardVisualTheme.RoundedCardSlot;
                cardBg.color  = new Color(0.12f, 0.20f, 0.35f, 0.95f);
                suitSymbolText.gameObject.SetActive(true);
                suitSymbolText.text = "🃏";
                suitSymbolText.color = new Color(0.5f, 0.85f, 1f);
                suitNameText.gameObject.SetActive(true);
                suitNameText.text = "JOKER";
                suitNameText.color = Color.white;
                statusLabelText.text = "★ NO TRUMP ★";
                statusLabelText.color = new Color(0.5f, 0.85f, 1f);
                glowOutline.gameObject.SetActive(true);
                if (revealBtn != null) revealBtn.gameObject.SetActive(false);
                return;
            }

            bool isRevealed = gm.IsTrumpRevealed();
            Suit? actualTrump = tm.TrumpSuit;
            Suit? visibleTrump = gm.GetTrumpForHuman();

            // Trigger DOTween 3D flip animation when transitioning to revealed
            if (isRevealed && !_wasRevealed && actualTrump.HasValue)
            {
                _wasRevealed = true;
                AnimateFlipToRevealed(actualTrump.Value);
                return;
            }

            if (isRevealed && actualTrump.HasValue)
            {
                _wasRevealed = true;
                ApplyRevealedState(actualTrump.Value);
            }
            else if (tm.IsSeventhCard)
            {
                // Blind 7th Card trump mode before reveal
                _wasRevealed = false;
                cardBg.sprite = CardVisualTheme.CardBack;
                cardBg.color = Color.white;
                glowOutline.gameObject.SetActive(false);

                suitSymbolText.gameObject.SetActive(true);
                suitSymbolText.text = "🎴";
                suitSymbolText.color = CardVisualTheme.ColorGold;

                suitNameText.gameObject.SetActive(true);
                suitNameText.text = "7TH CARD";
                suitNameText.color = Color.white;

                statusLabelText.text = "7TH CARD (SECRET)";
                statusLabelText.color = CardVisualTheme.ColorGold;

                bool canReveal = gm.CurrentPhase == GamePhase.Playing && gm.CurrentPlayer == GameManager.HumanSeat;
                if (revealBtn != null) revealBtn.gameObject.SetActive(canReveal);
            }
            else if (actualTrump.HasValue)
            {
                // Standard Suit secret trump before reveal
                _wasRevealed = false;
                cardBg.sprite = CardVisualTheme.CardBack;
                cardBg.color = Color.white;
                glowOutline.gameObject.SetActive(false);

                if (visibleTrump.HasValue)
                {
                    // South knows their team's trump
                    suitSymbolText.gameObject.SetActive(true);
                    suitSymbolText.text = CardVisualTheme.GetSuitSymbol(visibleTrump.Value);
                    suitSymbolText.color = new Color(1f, 0.85f, 0.4f, 0.9f);

                    suitNameText.gameObject.SetActive(true);
                    suitNameText.text = "YOUR TRUMP";
                    suitNameText.color = Color.white;

                    statusLabelText.text = $"SECRET ({CardVisualTheme.GetSuitName(visibleTrump.Value)})";
                    statusLabelText.color = CardVisualTheme.ColorGold;
                }
                else
                {
                    // Opponent trump — hidden
                    suitSymbolText.gameObject.SetActive(true);
                    suitSymbolText.text = "?";
                    suitSymbolText.color = CardVisualTheme.ColorGold;

                    suitNameText.gameObject.SetActive(true);
                    suitNameText.text = "HIDDEN";
                    suitNameText.color = new Color(0.7f, 0.8f, 0.9f);

                    statusLabelText.text = "TRUMP (SECRET)";
                    statusLabelText.color = new Color(0.7f, 0.8f, 0.9f);
                }

                bool canReveal = gm.CurrentPhase == GamePhase.Playing && gm.CurrentPlayer == GameManager.HumanSeat;
                if (revealBtn != null) revealBtn.gameObject.SetActive(canReveal);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyRevealedState(Suit trump)
        {
            cardBg.sprite = CardVisualTheme.CardFront;
            cardBg.color = Color.white;

            Color col = CardVisualTheme.GetSuitColor(trump);
            suitSymbolText.gameObject.SetActive(true);
            suitSymbolText.text = CardVisualTheme.GetSuitSymbol(trump);
            suitSymbolText.color = col;

            suitNameText.gameObject.SetActive(true);
            suitNameText.text = CardVisualTheme.GetSuitName(trump).ToUpper();
            suitNameText.color = col;

            statusLabelText.text = "★ TRUMP ★";
            statusLabelText.color = CardVisualTheme.ColorGold;
            glowOutline.gameObject.SetActive(true);

            if (revealBtn != null) revealBtn.gameObject.SetActive(false);
        }

        private void AnimateFlipToRevealed(Suit trump)
        {
            transform.DOKill();
            transform.DOScaleX(0f, 0.16f).SetEase(Ease.InQuad).SetLink(gameObject).OnComplete(() =>
            {
                if (this == null || gameObject == null) return;
                ApplyRevealedState(trump);
                transform.DOScaleX(1f, 0.18f).SetEase(Ease.OutQuad).SetLink(gameObject).OnComplete(() =>
                {
                    if (this != null && gameObject != null)
                        transform.DOPunchScale(Vector3.one * 0.18f, 0.25f, 8, 1).SetLink(gameObject);
                });
            });
        }

        private void OnRevealClicked()
        {
            Debug.Log("[29 TrumpSlot] Human South clicked REVEAL TRUMP");
            if (revealBtn != null)
                revealBtn.transform.DOPunchScale(Vector3.one * 0.2f, 0.15f, 10, 1).SetLink(gameObject);

            GameManager.Instance.RevealTrump();
        }

        private GameObject CreateText(string name, Vector2 pos, int fontSize, FontStyle style, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(120, 40);

            Text txt = obj.AddComponent<Text>();
            txt.font = CardVisualTheme.GetFont();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.raycastTarget = false;
            return obj;
        }
    }
}
