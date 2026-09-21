using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game29
{
    /// <summary>
    /// Modal overlay displayed at Round Over and Game Over states.
    /// Provides round breakdown, card point statistics, and Next Round / New Game buttons.
    /// </summary>
    public class RoundEndModalUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image modalBg;
        [SerializeField] private Text headerText;
        [SerializeField] private Text resultText;
        [SerializeField] private Text detailsText;
        [SerializeField] private Text scoreBoardText;
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionButtonText;

        private bool _isGameOver;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }
        }

        public void EnsureComponents()
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(580, 400);

            if (modalBg == null)
            {
                modalBg = gameObject.GetComponent<Image>();
                if (modalBg == null) modalBg = gameObject.AddComponent<Image>();
            }

            // Always (re-)apply the PopupBG sprite so it survives scene reloads or
            // a missing Inspector reference. Fall back to the procedural panel if the
            // asset hasn't been imported yet.
            // NOTE: CardVisualTheme.PopupBG never returns null — when the asset is missing it
            // hands back RoundedPanel — so compare against that to detect a real PopupBG.
            Sprite popupSprite = CardVisualTheme.PopupBG;
            if (popupSprite != null && popupSprite != CardVisualTheme.RoundedPanel)
            {
                modalBg.sprite = popupSprite;
                modalBg.type = Image.Type.Simple;
                modalBg.color = Color.white;
                modalBg.preserveAspect = false;
            }
            else
            {
                modalBg.sprite = CardVisualTheme.RoundedPanel;
                modalBg.type = Image.Type.Sliced;
                modalBg.color = new Color(0.05f, 0.08f, 0.14f, 0.98f);
            }

            if (headerText == null)
            {
                headerText = CreateText("Header", new Vector2(0, 130), new Vector2(440, 44), 26, FontStyle.Bold, CardVisualTheme.ColorGold);
            }

            if (resultText == null)
            {
                resultText = CreateText("Result", new Vector2(0, 80), new Vector2(440, 36), 20, FontStyle.Bold, Color.white);
            }

            if (detailsText == null)
            {
                detailsText = CreateText("Details", new Vector2(0, 20), new Vector2(420, 60), 16, FontStyle.Normal, new Color(0.85f, 0.90f, 0.95f));
            }

            if (scoreBoardText == null)
            {
                scoreBoardText = CreateText("ScoreBoard", new Vector2(0, -45), new Vector2(420, 40), 18, FontStyle.Bold, CardVisualTheme.ColorCyan);
            }

            if (actionButton == null)
            {
                GameObject btnObj = new GameObject("ActionButton");
                btnObj.transform.SetParent(transform, false);
                RectTransform brt = btnObj.AddComponent<RectTransform>();
                brt.anchoredPosition = new Vector2(0, -115);
                brt.sizeDelta = new Vector2(240, 50);

                Image bimg = btnObj.AddComponent<Image>();
                bimg.sprite = CardVisualTheme.PillBadge;
                bimg.type = Image.Type.Sliced;
                bimg.color = new Color(0.12f, 0.65f, 0.32f);

                actionButton = btnObj.AddComponent<Button>();
                actionButton.targetGraphic = bimg;
                actionButton.onClick.AddListener(OnActionButtonClicked);

                actionButtonText = CreateText("Label", Vector2.zero, Vector2.zero, 18, FontStyle.Bold, Color.white, btnObj.transform);
                actionButtonText.rectTransform.anchorMin = Vector2.zero;
                actionButtonText.rectTransform.anchorMax = Vector2.one;
                actionButtonText.text = "NEXT ROUND";
            }
        }

        public void ShowRoundOver(ScoreManager scoreMgr, bool biddingTeamWon)
        {
            EnsureComponents();
            _isGameOver = false;
            gameObject.SetActive(true);

            if (scoreMgr.LastRoundWasSingleHand)
            {
                string singleTeamName = GameRules.TeamName(scoreMgr.LastSingleHandTeam);
                headerText.text = "[SINGLE HAND] ROUND COMPLETE";
                if (scoreMgr.LastSingleHandSuccess)
                {
                    resultText.text = $"★ {singleTeamName.ToUpper()} COMPLETED SINGLE HAND! ★";
                    resultText.color = new Color(0.25f, 0.85f, 0.45f);
                    detailsText.text = $"Single Hand Succeeded!\n{singleTeamName} +3 Set Points   |   Opponents unchanged";
                }
                else
                {
                    resultText.text = $"✘ {singleTeamName.ToUpper()} FAILED SINGLE HAND ✘";
                    resultText.color = new Color(0.95f, 0.30f, 0.30f);
                    detailsText.text = $"Opponent won a trick!\n{singleTeamName} −3 Set Points   |   Opponents unchanged";
                }

                scoreBoardText.text = $"CURRENT SCORE:\nYou & Partner: {scoreMgr.GamePoints[0]}   |   Opponents: {scoreMgr.GamePoints[1]}";
                actionButtonText.text = "NEXT ROUND ▶";
                return;
            }

            // Build a Double/Re-Double badge prefix if applicable
            string badge = "";
            if (scoreMgr.LastDoubleStatus == DoubleStatus.ReDouble)
                badge = "[RE-DOUBLE] ";
            else if (scoreMgr.LastDoubleStatus == DoubleStatus.Double)
                badge = "[DOUBLE] ";

            headerText.text = $"{badge}ROUND COMPLETE";
            string teamName = GameRules.TeamName(scoreMgr.BiddingTeam);
            int bid = scoreMgr.CurrentBid;

            if (biddingTeamWon)
            {
                resultText.text = $"★ {teamName} MADE THE BID! ★";
                resultText.color = new Color(0.25f, 0.85f, 0.45f);
            }
            else
            {
                resultText.text = $"✘ {teamName} FAILED THE BID ✘";
                resultText.color = new Color(0.95f, 0.30f, 0.30f);
            }

            // Show the actual set-point delta from LastRoundDelta
            int delta = scoreMgr.LastRoundDelta;
            string deltaStr = delta >= 0 ? $"+{delta}" : $"−{System.Math.Abs(delta)}";
            string sweepNote = scoreMgr.WasAllPointsSweep ? " (All-Points Sweep!)" : "";
            detailsText.text = $"Bid Target: <b>{bid}</b> points{sweepNote}\n{teamName} {deltaStr}   |   Opponents unchanged";
            scoreBoardText.text = $"CURRENT SCORE:\nYou & Partner: {scoreMgr.GamePoints[0]}   |   Opponents: {scoreMgr.GamePoints[1]}";

            actionButtonText.text = "NEXT ROUND ▶";
        }

        public void ShowGameOver(ScoreManager scoreMgr, int winningTeam)
        {
            EnsureComponents();
            _isGameOver = true;
            gameObject.SetActive(true);

            if (winningTeam == 0)
            {
                headerText.text = "🏆 VICTORY! 🏆";
                headerText.color = CardVisualTheme.ColorGold;
                resultText.text = "YOU AND YOUR PARTNER WON!";
                resultText.color = CardVisualTheme.ColorCyan;
            }
            else
            {
                headerText.text = "GAME OVER";
                headerText.color = new Color(0.95f, 0.35f, 0.35f);
                resultText.text = "OPPONENTS WON THE GAME";
                resultText.color = new Color(0.95f, 0.55f, 0.35f);
            }

            detailsText.text = $"First team to 6 game points!\nFinal Score: {scoreMgr.GamePoints[0]} to {scoreMgr.GamePoints[1]}";
            scoreBoardText.text = "Thank you for playing 29 Card Game!";
            actionButtonText.text = "PLAY AGAIN ↺";
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnActionButtonClicked()
        {
            Hide();
            if (_isGameOver)
                GameManager.Instance.StartNewGame();
            else
                GameManager.Instance.StartNextRound();
        }

        private Text CreateText(string name, Vector2 pos, Vector2 size, int fontSize, FontStyle style, Color color, Transform parent = null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent != null ? parent : transform, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            if (size != Vector2.zero) rt.sizeDelta = size;

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