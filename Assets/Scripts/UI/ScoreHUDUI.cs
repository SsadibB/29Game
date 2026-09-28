using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game29
{
    /// <summary>
    /// Binds the existing ScoreHUD texts to current-round card points only.
    /// </summary>
    public class ScoreHUDUI : MonoBehaviour
    {
        [Header("Existing ScoreHUD texts")]
        [SerializeField] private Text team0PointsText;
        [SerializeField] private Text team1PointsText;

        [Header("Existing BidWinnerInfo")]
        [SerializeField] private TMP_Text bidWinnerInfoText;

        // Legacy serialized fields kept so old scenes do not lose references.
        [SerializeField] private Text team0GamePointsText;
        [SerializeField] private Text team0RoundPointsText;
        [SerializeField] private Text team1GamePointsText;
        [SerializeField] private Text team1RoundPointsText;
        [SerializeField] private Text bidInfoText;
        [SerializeField] private Text trumpInfoText;
        [SerializeField] private Button revealTrumpBtn;
        [SerializeField] private Button marriageBtn;
        [SerializeField] private Text trickProgressText;
        [SerializeField] private GameObject statusBannerObj;
        [SerializeField] private Text statusBannerText;

        private void Awake()
        {
            BindExisting();
        }

        public void EnsureComponents()
        {
            BindExisting();
        }

        private void BindExisting()
        {
            if (team0PointsText == null)
                team0PointsText = team0GamePointsText != null ? team0GamePointsText : team0RoundPointsText;
            if (team1PointsText == null)
                team1PointsText = team1GamePointsText != null ? team1GamePointsText : team1RoundPointsText;

            if (team0PointsText == null || team1PointsText == null)
            {
                Text[] texts = GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    Text t = texts[i];
                    if (t == null) continue;
                    string label = t.text ?? string.Empty;
                    if (label.IndexOf("Our", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (team0PointsText == null) team0PointsText = t;
                    }
                    else if (team1PointsText == null)
                    {
                        team1PointsText = t;
                    }
                }

                if ((team0PointsText == null || team1PointsText == null) && texts.Length >= 2)
                {
                    // Scene order: first BoardCardPts is opponents, second is our team.
                    if (team1PointsText == null) team1PointsText = texts[0];
                    if (team0PointsText == null) team0PointsText = texts[1];
                }
            }

            if (bidWinnerInfoText == null)
            {
                Transform parent = transform.parent;
                Transform info = parent != null ? parent.Find("BidWinnerInfo") : null;
                if (info != null)
                    bidWinnerInfoText = info.GetComponentInChildren<TMP_Text>(true);
            }
        }

        public void UpdateHUD(GameManager gm)
        {
            BindExisting();
            if (gm == null) return;

            int ourPts = 0;
            int oppPts = 0;
            bool inRoundPlay = gm.CurrentPhase == GamePhase.Playing
                || gm.CurrentPhase == GamePhase.RoundOver
                || gm.CurrentPhase == GamePhase.GameOver;
            if (inRoundPlay)
            {
                int[] roundPts = gm.GetRoundPoints();
                if (roundPts != null && roundPts.Length > 1)
                {
                    ourPts = roundPts[0];
                    oppPts = roundPts[1];
                }
            }

            SetPointsText(team0PointsText, "Our points : ", ourPts);
            SetPointsText(team1PointsText, "Opp points : ", oppPts);
            UpdateBidWinnerInfo(gm);
        }

        public void SetStatusMessage(string msg)
        {
            if (statusBannerText != null)
                statusBannerText.text = msg;
            if (statusBannerObj != null)
                statusBannerObj.SetActive(!string.IsNullOrEmpty(msg));
        }

        private static void SetPointsText(Text target, string label, int points)
        {
            if (target == null) return;
            target.text = $"{label}<b>{points}</b>";
        }

        private void UpdateBidWinnerInfo(GameManager gm)
        {
            if (bidWinnerInfoText == null) return;

            if (gm.CurrentPhase < GamePhase.TrumpSelection || gm.ScoreManager == null || gm.ScoreManager.CurrentBid < GameRules.MinBid)
            {
                bidWinnerInfoText.text = "Trump Player: —";
                return;
            }

            PlayerSeat winner = gm.GetBidWinner();
            int bid = gm.ScoreManager.CurrentBid;
            bidWinnerInfoText.text = $"Trump Player: {GetPlayerName(winner)} - {bid}";
        }

        private static string GetPlayerName(PlayerSeat seat)
        {
            return seat switch
            {
                PlayerSeat.South => "South",
                PlayerSeat.North => "North",
                PlayerSeat.East => "East",
                PlayerSeat.West => "West",
                _ => seat.ToString()
            };
        }
    }
}
