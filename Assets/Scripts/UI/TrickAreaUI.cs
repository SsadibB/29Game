using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Visual representation of the center trick table.
    /// Features 4 card slots in a cross pattern for South, North, East, West,
    /// animated card placement, victory callout banner, and winner trick sweep.
    /// </summary>
    public class TrickAreaUI : MonoBehaviour
    {
        [Header("Card Slots")]
        [SerializeField] private CardUI southSlot;
        [SerializeField] private CardUI northSlot;
        [SerializeField] private CardUI westSlot;
        [SerializeField] private CardUI eastSlot;

        [Header("Winner Announcement")]
        [SerializeField] private GameObject bannerObj;
        [SerializeField] private Text       bannerText;
        [SerializeField] private Image      bannerBg;

        private Coroutine _bannerCoroutine;
        private readonly Vector2[] _baseSlotPositions = new Vector2[4];

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnDestroy()
        {
            if (bannerObj != null) bannerObj.transform.DOKill();
        }

        public void EnsureComponents()
        {
            float slotW = 100f;
            float slotH = 145f;

            if (southSlot == null)
            {
                southSlot = CreateSlot("Slot_South", new Vector2(0, -100), slotW, slotH, "You");
                _baseSlotPositions[(int)PlayerSeat.South] = new Vector2(0, -100);
            }

            if (northSlot == null)
            {
                northSlot = CreateSlot("Slot_North", new Vector2(0, 100), slotW, slotH, "Partner");
                _baseSlotPositions[(int)PlayerSeat.North] = new Vector2(0, 100);
            }

            if (westSlot == null)
            {
                westSlot = CreateSlot("Slot_West", new Vector2(-125, 0), slotW, slotH, "West");
                _baseSlotPositions[(int)PlayerSeat.West] = new Vector2(-125, 0);
            }

            if (eastSlot == null)
            {
                eastSlot = CreateSlot("Slot_East", new Vector2(125, 0), slotW, slotH, "East");
                _baseSlotPositions[(int)PlayerSeat.East] = new Vector2(125, 0);
            }

            if (bannerObj == null)
            {
                bannerObj = new GameObject("TrickWinnerBanner");
                bannerObj.transform.SetParent(transform, false);
                RectTransform brt = bannerObj.AddComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 0.5f);
                brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(360, 52);
                brt.anchoredPosition = new Vector2(0, 0);

                bannerBg = bannerObj.AddComponent<Image>();
                bannerBg.sprite = CardVisualTheme.RoundedPanel;
                bannerBg.type = Image.Type.Sliced;
                bannerBg.color = new Color(0.06f, 0.12f, 0.22f, 0.98f);

                GameObject bt = new GameObject("Text");
                bt.transform.SetParent(bannerObj.transform, false);
                RectTransform btrt = bt.AddComponent<RectTransform>();
                btrt.anchorMin = Vector2.zero;
                btrt.anchorMax = Vector2.one;
                btrt.sizeDelta = Vector2.zero;

                bannerText = bt.AddComponent<Text>();
                bannerText.font = CardVisualTheme.GetFont();
                bannerText.fontSize = 18;
                bannerText.fontStyle = FontStyle.Bold;
                bannerText.alignment = TextAnchor.MiddleCenter;
                bannerText.color = CardVisualTheme.ColorGold;

                bannerObj.SetActive(false);
            }
        }

        private CardUI CreateSlot(string name, Vector2 pos, float w, float h, string label)
        {
            GameObject slotObj = new GameObject(name);
            slotObj.transform.SetParent(transform, false);
            RectTransform rt = slotObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;

            CardUI card = slotObj.AddComponent<CardUI>();
            card.EnsureComponents();
            card.SetEmptySlot();

            // Label
            GameObject lblObj = new GameObject("SlotLabel");
            lblObj.transform.SetParent(slotObj.transform, false);
            RectTransform lrt = lblObj.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(w, 20);
            lrt.anchoredPosition = new Vector2(0, -h * 0.5f - 14f);

            Text txt = lblObj.AddComponent<Text>();
            txt.font = CardVisualTheme.GetFont();
            txt.fontSize = 13;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.7f, 0.8f, 0.9f, 0.75f);
            txt.text = label;

            return card;
        }

        public void DisplayTrick(Trick trick)
        {
            EnsureComponents();

            if (trick == null || trick.Plays.Count == 0)
            {
                ClearSlot(southSlot, PlayerSeat.South);
                ClearSlot(northSlot, PlayerSeat.North);
                ClearSlot(westSlot, PlayerSeat.West);
                ClearSlot(eastSlot, PlayerSeat.East);
                return;
            }

            // Keep track of which slots are played in this trick
            bool[] hasPlay = new bool[4];
            foreach (var play in trick.Plays)
            {
                hasPlay[(int)play.Player] = true;
                CardUI slot = GetSlotForSeat(play.Player);
                if (slot != null)
                {
                    bool wasEmpty = slot.CurrentCard == null;
                    slot.SetCard(play.Card, false, null);
                    slot.SetGlow(false, Color.clear);

                    if (wasEmpty)
                    {
                        slot.transform.DOKill();
                        slot.transform.localScale = Vector3.one * 0.75f;
                        slot.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetLink(slot.gameObject);
                    }
                }
            }

            // Clear any unplayed slots
            for (int i = 0; i < 4; i++)
            {
                if (!hasPlay[i])
                    ClearSlot(GetSlotForSeat((PlayerSeat)i), (PlayerSeat)i);
            }
        }

        public void ShowTrickWinner(PlayerSeat winner, int points)
        {
            EnsureComponents();
            string winnerName = winner switch
            {
                PlayerSeat.South => "You",
                PlayerSeat.North => "Partner",
                _                => winner.ToString()
            };

            // Glow and punch winning card
            CardUI winningSlot = GetSlotForSeat(winner);
            if (winningSlot != null)
            {
                winningSlot.SetGlow(true, CardVisualTheme.ColorGold);
                winningSlot.transform.DOKill();
                winningSlot.transform.DOPunchScale(Vector3.one * 0.22f, 0.35f, 6, 1);
            }

            // Animate banner pop-in
            if (bannerObj != null && bannerText != null)
            {
                bannerText.text = $"★ Trick won by {winnerName} (+{points} pts)";
                bannerObj.SetActive(true);
                bannerObj.transform.DOKill();
                bannerObj.transform.localScale = Vector3.one * 0.65f;
                bannerObj.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(bannerObj);

                if (_bannerCoroutine != null) StopCoroutine(_bannerCoroutine);
                _bannerCoroutine = StartCoroutine(HideBannerRoutine(1.6f));
            }
        }

        private System.Collections.IEnumerator HideBannerRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (bannerObj != null && bannerObj.activeInHierarchy)
            {
                bannerObj.transform.DOKill();
                bannerObj.transform.DOScale(0.7f, 0.18f).SetEase(Ease.InQuad).SetLink(bannerObj).OnComplete(() =>
                {
                    if (bannerObj != null)
                        bannerObj.SetActive(false);
                });
            }
            _bannerCoroutine = null;
        }

        public void ClearAll()
        {
            EnsureComponents();
            ClearSlot(southSlot, PlayerSeat.South);
            ClearSlot(northSlot, PlayerSeat.North);
            ClearSlot(westSlot, PlayerSeat.West);
            ClearSlot(eastSlot, PlayerSeat.East);
            if (bannerObj != null) bannerObj.SetActive(false);
        }

        private void ClearSlot(CardUI slot, PlayerSeat seat)
        {
            if (slot != null)
            {
                slot.transform.DOKill();
                slot.transform.localScale = Vector3.one;
                RectTransform rt = slot.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = _baseSlotPositions[(int)seat];
                slot.SetEmptySlot();
            }
        }

        private CardUI GetSlotForSeat(PlayerSeat seat)
        {
            return seat switch
            {
                PlayerSeat.South => southSlot,
                PlayerSeat.North => northSlot,
                PlayerSeat.West  => westSlot,
                PlayerSeat.East  => eastSlot,
                _                => null
            };
        }
    }
}
