using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game29
{
    /// <summary>
    /// Top scoreboard HUD and game phase announcement banner.
    /// Tracks team game-points (first to 6), round card-points, active bid,
    /// trump suit indicator, and interactive "Reveal Trump" button.
    /// </summary>
    [ExecuteAlways]
    public class ScoreHUDUI : MonoBehaviour
    {
        [Header("Team 0 (South + North)")]
        [SerializeField] private Text team0GamePointsText;
        [SerializeField] private Text team0RoundPointsText;

        [Header("Team 1 (East + West)")]
        [SerializeField] private Text team1GamePointsText;
        [SerializeField] private Text team1RoundPointsText;

        [Header("Center Trump & Bid")]
        [SerializeField] private Text bidInfoText;
        [SerializeField] private Text trumpInfoText;
        [SerializeField] private Button revealTrumpBtn;
        [SerializeField] private Button marriageBtn;
        [SerializeField] private Text trickProgressText;

        [Header("Status Banner")]
        [SerializeField] private GameObject statusBannerObj;
        [SerializeField] private Text statusBannerText;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            if (revealTrumpBtn != null)
            {
                revealTrumpBtn.onClick.RemoveAllListeners();
                revealTrumpBtn.onClick.AddListener(OnRevealTrumpClicked);
            }
            if (marriageBtn != null)
            {
                marriageBtn.onClick.RemoveAllListeners();
                marriageBtn.onClick.AddListener(OnMarriageClicked);
            }
        }

        public void EnsureComponents()
        {
            RectTransform rt = GetComponent<RectTransform>();
            bool isNewRect = rt == null;
            if (isNewRect) rt = gameObject.AddComponent<RectTransform>();

            // Only apply the default full-width top-bar layout the first time
            // this RectTransform is created. This method runs on every Awake()
            // AND on every UpdateHUD() call, so if we always forced these
            // values here, any anchor/position/size you set by hand in the
            // Inspector or Scene view would get stomped the moment you hit
            // Play (and again on every HUD refresh afterwards).
            // Default layout: fixed-size box anchored to top-center (not the old
            // full-width stretch), matching the position/size you set by hand
            // in the Inspector — anchor/pivot (0.5,1), pos (358,-172), size
            // 384.58x140.
            if (isNewRect)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(358f, -172f);
                rt.sizeDelta = new Vector2(384.58f, 140f);
            }

            // Background top bar
            Image barBg = gameObject.GetComponent<Image>();
            if (barBg == null) barBg = gameObject.AddComponent<Image>();
            barBg.sprite = CardVisualTheme.RoundedPanel;
            barBg.type = Image.Type.Sliced;
            barBg.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);
            barBg.raycastTarget = false;

            // Left Box: You & Partner
            if (team0GamePointsText == null)
            {
                GameObject box0 = CreateScoreBox("Team0Box", new Vector2(170, -65), new Vector2(240, 105), "YOU & PARTNER", CardVisualTheme.ColorCyan);
                team0GamePointsText = box0.transform.Find("GamePts").GetComponent<Text>();
                team0RoundPointsText = box0.transform.Find("RoundPts").GetComponent<Text>();
            }

            // Right Box: Opponents
            if (team1GamePointsText == null)
            {
                GameObject box1 = CreateScoreBox("Team1Box", new Vector2(-170, -65), new Vector2(240, 105), "OPPONENTS", new Color(0.95f, 0.55f, 0.35f), true);
                team1GamePointsText = box1.transform.Find("GamePts").GetComponent<Text>();
                team1RoundPointsText = box1.transform.Find("RoundPts").GetComponent<Text>();
            }

            // Center Box: Trump, Bid, Trick
            if (bidInfoText == null)
            {
                GameObject centerBox = new GameObject("CenterHUD");
                centerBox.transform.SetParent(transform, false);
                RectTransform crt = centerBox.AddComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.5f, 1f);
                crt.anchorMax = new Vector2(0.5f, 1f);
                crt.pivot = new Vector2(0.5f, 1f);
                crt.anchoredPosition = new Vector2(0, -10);
                crt.sizeDelta = new Vector2(400, 160);

                bidInfoText = CreateText("BidInfo", centerBox.transform, new Vector2(0, -14), 16, FontStyle.Bold, CardVisualTheme.ColorGold);
                trumpInfoText = CreateText("TrumpInfo", centerBox.transform, new Vector2(0, -42), 17, FontStyle.Bold, Color.white);
                trickProgressText = CreateText("TrickProgress", centerBox.transform, new Vector2(0, -70), 14, FontStyle.Normal, new Color(0.7f, 0.8f, 0.9f));

                // Reveal Trump button
                GameObject rBtn = new GameObject("RevealTrumpBtn");
                rBtn.transform.SetParent(centerBox.transform, false);
                RectTransform rrt = rBtn.AddComponent<RectTransform>();
                rrt.anchoredPosition = new Vector2(0, -58);
                rrt.sizeDelta = new Vector2(180, 32);

                Image rimg = rBtn.AddComponent<Image>();
                rimg.sprite = CardVisualTheme.PillBadge;
                rimg.type = Image.Type.Sliced;
                rimg.color = new Color(0.85f, 0.65f, 0.15f, 0.95f);

                revealTrumpBtn = rBtn.AddComponent<Button>();
                revealTrumpBtn.targetGraphic = rimg;
                revealTrumpBtn.onClick.AddListener(OnRevealTrumpClicked);

                Text rtxt = CreateText("Label", rBtn.transform, Vector2.zero, 13, FontStyle.Bold, Color.black);
                rtxt.text = "REVEAL TRUMP";
                rBtn.SetActive(false);
            }

            // Declare Marriage button — same row as Reveal Trump but offset so
            // both can coexist if eligibility ever overlaps. Standalone guard
            // (not nested in the centerBox-only-if-missing block above) so it
            // still gets created on a scene that was already built before this
            // button existed.
            if (marriageBtn == null)
            {
                Transform centerBoxT = bidInfoText != null ? bidInfoText.transform.parent : null;
                if (centerBoxT != null)
                {
                    GameObject mBtn = new GameObject("MarriageBtn");
                    mBtn.transform.SetParent(centerBoxT, false);
                    RectTransform mrt = mBtn.AddComponent<RectTransform>();
                    mrt.anchoredPosition = new Vector2(0, -92);
                    mrt.sizeDelta = new Vector2(180, 32);

                    Image mimg = mBtn.AddComponent<Image>();
                    mimg.sprite = CardVisualTheme.PillBadge;
                    mimg.type = Image.Type.Sliced;
                    mimg.color = new Color(0.65f, 0.20f, 0.55f, 0.96f);

                    marriageBtn = mBtn.AddComponent<Button>();
                    marriageBtn.targetGraphic = mimg;
                    marriageBtn.onClick.AddListener(OnMarriageClicked);

                    Text mtxt = CreateText("Label", mBtn.transform, Vector2.zero, 13, FontStyle.Bold, Color.white);
                    mtxt.text = "DECLARE MARRIAGE";
                    mBtn.SetActive(false);
                }
            }

            // Status Banner below top bar
            if (statusBannerObj == null)
            {
                statusBannerObj = new GameObject("StatusBanner");
                statusBannerObj.transform.SetParent(transform, false);
                RectTransform srt = statusBannerObj.AddComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.5f, 0);
                srt.anchorMax = new Vector2(0.5f, 0);
                srt.pivot = new Vector2(0.5f, 1);
                srt.anchoredPosition = new Vector2(0, -12);
                srt.sizeDelta = new Vector2(620, 38);

                Image sbg = statusBannerObj.AddComponent<Image>();
                sbg.sprite = CardVisualTheme.PillBadge;
                sbg.type = Image.Type.Sliced;
                sbg.color = new Color(0.08f, 0.14f, 0.22f, 0.92f);

                statusBannerText = CreateText("StatusText", statusBannerObj.transform, Vector2.zero, 16, FontStyle.Bold, CardVisualTheme.ColorGold);
            }
        }

        public void UpdateHUD(GameManager gm)
        {
            EnsureComponents();
            if (gm == null) return;

            int[] gamePts = gm.GetGamePoints();
            int[] roundPts = gm.GetRoundPoints();

            if (team0GamePointsText != null)
            {
                int g = gamePts[0];
                string sign = g > 0 ? $"+{g}" : g.ToString();
                team0GamePointsText.text = $"{sign} / 6";
            }

            if (team0RoundPointsText != null)
                team0RoundPointsText.text = $"Team: <b>{roundPts[0]}</b>";

            if (team1GamePointsText != null)
            {
                int g = gamePts[1];
                string sign = g > 0 ? $"+{g}" : g.ToString();
                team1GamePointsText.text = $"{sign} / 6";
            }

            if (team1RoundPointsText != null)
                team1RoundPointsText.text = $"Opponent: <b>{roundPts[1]}</b>";

            // Bid
            int currentBid = gm.GetCurrentBid();
            if (currentBid >= GameRules.MinBid)
            {
                PlayerSeat bidder = gm.GetCurrentHighBidder();
                string bidderName = bidder == PlayerSeat.North ? "Partner" : (bidder == PlayerSeat.South ? "You" : bidder.ToString());
                int target = gm.CurrentPhase == GamePhase.Playing ? gm.GetEffectiveTarget() : currentBid;
                bidInfoText.text = target != currentBid
                    ? $"TARGET: <b>{target}</b> (bid {currentBid}, {bidderName})"
                    : $"TARGET BID: <b>{currentBid}</b> ({bidderName})";
            }
            else
            {
                bidInfoText.text = "BIDDING IN PROGRESS";
            }

            // Trump
            var tm = gm.TrumpManager;
            Suit? trump = gm.GetTrumpForHuman();

            if (revealTrumpBtn != null)
            {
                bool canReveal = gm.CanHumanRevealTrump();
                revealTrumpBtn.gameObject.SetActive(canReveal);
                // Position/size are set once in EnsureComponents() at creation
                // time — re-applying them here on every HUD update would
                // stomp any manual repositioning of this button.
            }

            if (marriageBtn != null)
            {
                bool canMarriage = gm.CanHumanDeclareMarriage();
                marriageBtn.gameObject.SetActive(canMarriage);
                // Same note as revealTrumpBtn above — position is set once.
            }

            if (tm != null && tm.IsJoker)
            {
                trumpInfoText.text = "★ JOKER: <color=#80D8FF><b>♠J &gt; ♥J &gt; ♦J &gt; ♣J</b></color> ★";
            }
            else if (trump.HasValue)
            {
                string sym = CardVisualTheme.GetSuitSymbol(trump.Value);
                string name = CardVisualTheme.GetSuitName(trump.Value);
                Color col = CardVisualTheme.GetSuitColor(trump.Value);
                trumpInfoText.text = $"★ TRUMP: <color=#{ColorUtility.ToHtmlStringRGB(col)}><b>{sym} {name.ToUpper()}</b></color> ★";
            }
            else if (tm != null && (tm.TrumpSuit.HasValue || tm.IsSeventhCard))
            {
                trumpInfoText.text = "TRUMP: FACE DOWN";
            }
            else if (gm.CurrentPhase >= GamePhase.TrumpSelection)
            {
                trumpInfoText.text = "TRUMP: SELECTING…";
            }
            else
            {
                trumpInfoText.text = "TRUMP: NOT SET YET";
            }

            // Trick count
            var trickMgr = gm.TrickManager;
            if (trickMgr != null && gm.CurrentPhase == GamePhase.Playing)
            {
                int currentTrickNum = Mathf.Min(trickMgr.TricksCompleted + 1, GameRules.TricksPerRound);
                trickProgressText.text = $"Trick {currentTrickNum} of {GameRules.TricksPerRound}";
            }
            else
            {
                trickProgressText.text = $"Round Phase: {gm.CurrentPhase}";
            }
        }

        public void SetStatusMessage(string msg)
        {
            if (statusBannerText != null)
                statusBannerText.text = msg;
            if (statusBannerObj != null)
                statusBannerObj.SetActive(!string.IsNullOrEmpty(msg));
        }

        private void OnRevealTrumpClicked()
        {
            GameManager.Instance.RevealTrump();
        }

        private void OnMarriageClicked()
        {
            // Confirmation/target-shift text is pushed into this same status
            // banner via GameTableUI's ScoreManager.OnMarriageDeclared subscription.
            GameManager.Instance.DeclareHumanMarriage();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private GameObject CreateScoreBox(string name, Vector2 pos, Vector2 size, string title, Color accentCol, bool rightAlign = false)
        {
            GameObject box = new GameObject(name);
            box.transform.SetParent(transform, false);
            RectTransform rt = box.AddComponent<RectTransform>();
            rt.anchorMin = rightAlign ? new Vector2(1, 1) : new Vector2(0, 1);
            rt.anchorMax = rightAlign ? new Vector2(1, 1) : new Vector2(0, 1);
            rt.pivot = rightAlign ? new Vector2(1, 1) : new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image bg = box.AddComponent<Image>();
            bg.sprite = CardVisualTheme.PillBadge;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(accentCol.r * 0.2f, accentCol.g * 0.2f, accentCol.b * 0.2f, 0.7f);

            Text tTitle = CreateText("Title", box.transform, new Vector2(0, 32), 13, FontStyle.Bold, accentCol);
            tTitle.text = title;

            Text tGame = CreateText("GamePts", box.transform, new Vector2(0, 6), 26, FontStyle.Bold, Color.white);
            tGame.text = "0 / 6";

            Text tRound = CreateText("RoundPts", box.transform, new Vector2(0, -22), 14, FontStyle.Normal, new Color(0.8f, 0.85f, 0.9f));
            tRound.text = "0 pts";

            return box;
        }

        private Text CreateText(string name, Transform parent, Vector2 pos, int fontSize, FontStyle style, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(380, 26);

            Text txt = obj.AddComponent<Text>();
            txt.font = CardVisualTheme.GetFont();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.raycastTarget = false;
            txt.supportRichText = true;
            return txt;
        }
    }
}