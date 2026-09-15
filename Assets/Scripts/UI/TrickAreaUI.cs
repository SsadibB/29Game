using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Visual representation of the center trick table.
    /// Features 4 card slots in a cross pattern for South, North, East, West,
    /// animated card placement (pop-in on play), victory banner, and a
    /// sweep-to-winner animation where all cards fly toward the winning seat.
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
        [SerializeField] private Text bannerText;
        [SerializeField] private Image bannerBg;

        private Coroutine _bannerCoroutine;
        private readonly Vector2[] _baseSlotPositions = new Vector2[4];

        // Landscape slot size — bigger and more visible
        private const float SlotW = 120f;
        private const float SlotH = 175f;

        // Slot positions: pulled in tight around the table center so the four
        // played cards visually overlap each other (like a real trick pile)
        // instead of sitting in four separated cross-arm slots. Small per-seat
        // rotations added for the same "loosely tossed onto the table" look.
        private static readonly Vector2 SouthPos = new Vector2(8f, -55f);
        private static readonly Vector2 NorthPos = new Vector2(-8f, 55f);
        private static readonly Vector2 WestPos = new Vector2(-60f, 5f);
        private static readonly Vector2 EastPos = new Vector2(60f, -5f);

        private const float SouthRot = -5f;
        private const float NorthRot = 6f;
        private const float WestRot = -9f;
        private const float EastRot = 8f;

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
            // _baseSlotPositions must always reflect the current SouthPos/NorthPos/
            // WestPos/EastPos constants — not just when a slot is freshly created.
            // If a slot GameObject was already wired up in the Inspector/prefab from
            // a previous session, its RectTransform keeps whatever anchoredPosition
            // was last saved there; it does NOT jump to match new constants on its
            // own. Without this, ClearSlot() would fall back to a stale/zeroed
            // _baseSlotPositions entry for that seat instead of the intended overlap
            // position. Setting these unconditionally, and pushing the values onto
            // any pre-existing slot's RectTransform, keeps both in sync with the code.
            _baseSlotPositions[(int)PlayerSeat.South] = SouthPos;
            _baseSlotPositions[(int)PlayerSeat.North] = NorthPos;
            _baseSlotPositions[(int)PlayerSeat.West] = WestPos;
            _baseSlotPositions[(int)PlayerSeat.East] = EastPos;

            if (southSlot == null)
            {
                southSlot = CreateSlot("Slot_South", SouthPos, SlotW, SlotH, SouthRot);
            }
            else
            {
                ApplySlotTransform(southSlot, SouthPos, SouthRot);
            }

            if (northSlot == null)
            {
                northSlot = CreateSlot("Slot_North", NorthPos, SlotW, SlotH, NorthRot);
            }
            else
            {
                ApplySlotTransform(northSlot, NorthPos, NorthRot);
            }

            if (westSlot == null)
            {
                westSlot = CreateSlot("Slot_West", WestPos, SlotW, SlotH, WestRot);
            }
            else
            {
                ApplySlotTransform(westSlot, WestPos, WestRot);
            }

            if (eastSlot == null)
            {
                eastSlot = CreateSlot("Slot_East", EastPos, SlotW, SlotH, EastRot);
            }
            else
            {
                ApplySlotTransform(eastSlot, EastPos, EastRot);
            }

            if (bannerObj == null)
            {
                bannerObj = new GameObject("TrickWinnerBanner");
                bannerObj.transform.SetParent(transform, false);
                RectTransform brt = bannerObj.AddComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 0.5f);
                brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(380, 56);
                brt.anchoredPosition = Vector2.zero;

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
                bannerText.fontSize = 19;
                bannerText.fontStyle = FontStyle.Bold;
                bannerText.alignment = TextAnchor.MiddleCenter;
                bannerText.color = CardVisualTheme.ColorGold;

                bannerObj.SetActive(false);
            }
        }

        // Pushes the current position/rotation constants onto a slot that already
        // existed before this pass (e.g. wired up in the Inspector/prefab). Only
        // touches empty slots — a slot with a card on it may be mid-animation
        // (AnimatePlayTo / AnimateSweepTo), and snapping its transform here would
        // fight that tween every time EnsureComponents() runs.
        private void ApplySlotTransform(CardUI slot, Vector2 pos, float rotationZ)
        {
            if (slot == null || slot.CurrentCard != null) return;

            RectTransform rt = slot.GetComponent<RectTransform>();
            if (rt == null) return;

            rt.sizeDelta = new Vector2(SlotW, SlotH);
            rt.anchoredPosition = pos;
            rt.localEulerAngles = new Vector3(0, 0, rotationZ);
        }

        private CardUI CreateSlot(string name, Vector2 pos, float w, float h, float rotationZ)
        {
            GameObject slotObj = new GameObject(name);
            slotObj.transform.SetParent(transform, false);
            RectTransform rt = slotObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;
            rt.localEulerAngles = new Vector3(0, 0, rotationZ);

            CardUI card = slotObj.AddComponent<CardUI>();
            card.EnsureComponents();
            card.SetEmptySlot();

            return card;
        }

        public void DisplayTrick(Trick trick, PlayerSeat? flyFromSeat = null, Vector3 flyFromWorld = default)
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

            bool[] hasPlay = new bool[4];
            foreach (var play in trick.Plays)
            {
                hasPlay[(int)play.Player] = true;
                CardUI slot = GetSlotForSeat(play.Player);
                if (slot == null) continue;

                // Keep an in-flight travel animation; don't snap the card to the slot.
                if (slot.CurrentCard != null && slot.CurrentCard == play.Card)
                    continue;

                bool isNew = slot.CurrentCard == null;
                slot.SetCard(play.Card, false, null);
                slot.SetGlow(false, Color.clear);
                // Cards played into the trick area must stay fully visible —
                // never fade/transparent, even if this slot still had a fade
                // tween left over from a previous trick's collection sweep.
                slot.SetFullyOpaque();

                if (isNew)
                {
                    // Move the freshly played card to the top of the render
                    // order so the pile overlaps in play order (each new card
                    // sits over the ones already on the table) instead of a
                    // fixed South/North/West/East hierarchy order.
                    slot.transform.SetAsLastSibling();

                    // The winner banner must always stay above every card,
                    // even ones played (or still animating in) after the
                    // banner appeared — otherwise the SetAsLastSibling() call
                    // above would push this card in front of it.
                    if (bannerObj != null)
                        bannerObj.transform.SetAsLastSibling();
                }

                if (isNew && flyFromSeat.HasValue && flyFromSeat.Value == play.Player)
                    AnimateCardFromWorld(slot, play.Player, flyFromWorld);
                else if (isNew)
                {
                    slot.transform.DOKill();
                    slot.transform.localScale = Vector3.one * 0.6f;
                    slot.transform.DOScale(1f, 0.24f).SetEase(Ease.OutBack).SetLink(slot.gameObject);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if (!hasPlay[i])
                    ClearSlot(GetSlotForSeat((PlayerSeat)i), (PlayerSeat)i);
            }
        }

        public const float CardTravelDuration = 0.9f;

        private void AnimateCardFromWorld(CardUI slot, PlayerSeat seat, Vector3 worldStart)
        {
            RectTransform areaRT = transform as RectTransform;
            RectTransform slotRT = slot.GetComponent<RectTransform>();
            if (areaRT == null || slotRT == null) return;

            Vector3 local = areaRT.InverseTransformPoint(worldStart);
            Vector2 localStart = new Vector2(local.x, local.y);
            Vector2 target = _baseSlotPositions[(int)seat];

            float duration = CardTravelDuration;
            if (GameManager.Instance != null)
                duration = GameManager.Instance.CardTravelDuration;

            slotRT.DOKill();
            slotRT.anchoredPosition = localStart;
            slotRT.localScale = Vector3.one * 1.08f;
            slot.AnimatePlayTo(target, duration);
        }

        public void ShowTrickWinner(PlayerSeat winner, int points)
        {
            EnsureComponents();
            string winnerName = winner switch
            {
                PlayerSeat.South => "You",
                PlayerSeat.North => "Partner",
                _ => winner.ToString()
            };

            // Glow and punch the winning slot
            CardUI winningSlot = GetSlotForSeat(winner);
            if (winningSlot != null)
            {
                winningSlot.SetGlow(true, CardVisualTheme.ColorGold);
                winningSlot.transform.DOKill();
                winningSlot.transform.DOPunchScale(Vector3.one * 0.25f, 0.35f, 6, 1).SetLink(winningSlot.gameObject);
            }

            // Banner pop-in
            if (bannerObj != null && bannerText != null)
            {
                bannerText.text = $"★ Trick won by {winnerName} (+{points} pts)";
                bannerObj.transform.SetAsLastSibling();
                bannerObj.SetActive(true);
                bannerObj.transform.DOKill();
                bannerObj.transform.localScale = Vector3.one * 0.65f;
                bannerObj.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(bannerObj);
            }

            if (_bannerCoroutine != null) StopCoroutine(_bannerCoroutine);
            _bannerCoroutine = StartCoroutine(SweepAndClearRoutine(winner));
        }

        /// <summary>
        /// Shows winner banner briefly, then animates all 4 trick cards sweeping
        /// toward the winner's seat direction, then clears the table.
        /// </summary>
        private System.Collections.IEnumerator SweepAndClearRoutine(PlayerSeat winner)
        {
            // Wait for punch animation and banner to display
            yield return new WaitForSeconds(0.85f);

            // Sweep direction: all cards fly toward the winner's side of the screen
            Vector2 sweepTarget = winner switch
            {
                PlayerSeat.South => new Vector2(0, -500f),
                PlayerSeat.North => new Vector2(0, 500f),
                PlayerSeat.West => new Vector2(-600f, 0),
                PlayerSeat.East => new Vector2(600f, 0),
                _ => Vector2.zero
            };

            float sweepDuration = 0.38f;
            bool anyCard = false;

            CardUI[] slots = { southSlot, northSlot, westSlot, eastSlot };
            foreach (CardUI slot in slots)
            {
                if (slot != null && slot.CurrentCard != null)
                {
                    anyCard = true;
                    slot.AnimateSweepTo(sweepTarget, sweepDuration);
                }
            }

            if (anyCard)
                yield return new WaitForSeconds(sweepDuration + 0.08f);

            // Dismiss banner
            if (bannerObj != null && bannerObj.activeInHierarchy)
            {
                bannerObj.transform.DOKill();
                bannerObj.transform.DOScale(0.65f, 0.16f).SetEase(Ease.InQuad).SetLink(bannerObj).OnComplete(() =>
                {
                    if (bannerObj != null) bannerObj.SetActive(false);
                });
            }

            // Reset all slots to empty at their base positions
            ClearSlot(southSlot, PlayerSeat.South);
            ClearSlot(northSlot, PlayerSeat.North);
            ClearSlot(westSlot, PlayerSeat.West);
            ClearSlot(eastSlot, PlayerSeat.East);

            _bannerCoroutine = null;
        }

        public void ClearAll()
        {
            EnsureComponents();
            if (_bannerCoroutine != null)
            {
                StopCoroutine(_bannerCoroutine);
                _bannerCoroutine = null;
            }
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
                PlayerSeat.West => westSlot,
                PlayerSeat.East => eastSlot,
                _ => null
            };
        }
    }
}