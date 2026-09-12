using System.Linq;
using UnityEngine;

namespace Game29
{
    /// <summary>
    /// Attach to the GameManager GameObject.
    /// Automatically starts a new game when the scene plays and forwards
    /// every game event to the Unity console for monitoring.
    ///
    /// Safe to leave in production — just disable <see cref="verboseLogging"/>
    /// once real UI is in place.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Automatically call StartNewGame() when the scene starts.")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("Log every game event to the console (useful during development).")]
        [SerializeField] private bool verboseLogging = true;

        private GameManager _gm;

        private void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null)
            {
                Debug.LogError("[29] GameBootstrap: GameManager.Instance is null — " +
                               "ensure GameManager is on the same GameObject.");
                return;
            }

            // Ensure Visual UI Canvas exists
            if (FindFirstObjectByType<GameTableUI>() == null)
            {
                GameObject canvasGO = new GameObject("[Canvas_GameTable]");
                var tableUI = canvasGO.AddComponent<GameTableUI>();
                tableUI.BuildUIIfMissing();
            }

            if (verboseLogging) SubscribeToEvents();

            // Defer StartNewGame by one frame so every UI component's Start() has run
            // and all event subscriptions (GameTableUI, etc.) are in place before events fire.
            if (autoStart) StartCoroutine(StartGameNextFrame());
        }

        private System.Collections.IEnumerator StartGameNextFrame()
        {
            yield return null; // wait one frame for all Start() methods to complete
            if (_gm != null)
                _gm.StartNewGame();
        }

        // ════════════════════════════════════════════════════════════════════════
        // EVENT LOGGING
        // ════════════════════════════════════════════════════════════════════════

        private void SubscribeToEvents()
        {
            _gm.OnPhaseChanged += phase =>
                Debug.Log($"[29] ═══════  Phase → {phase}  ═══════");

            _gm.OnHumanHandDealt += hand =>
            {
                string cards = string.Join(", ", hand.Cards.Select(c => c.ToString()));
                Debug.Log($"[29] 🃏  Your hand ({hand.Count} cards, {hand.TotalPoints()} pts): {cards}");
            };

            _gm.OnCurrentPlayerChanged += seat =>
            {
                if (seat == GameManager.HumanSeat)
                    Debug.Log("[29] ▶  YOUR TURN  (South)");
                else
                    Debug.Log($"[29]    Waiting for {seat} …");
            };

            _gm.OnBiddingAction += (seat, bid) =>
            {
                if (bid.HasValue)
                    Debug.Log($"[29] 📢  {seat} BID {bid.Value}");
                else
                    Debug.Log($"[29] 📢  {seat} PASSED");
            };

            _gm.OnHumanTrumpSelectionRequired += hand =>
                Debug.Log($"[29] 👑  Human Trump Selection Required! (Hand: {hand.Count} cards)");

            _gm.OnCardPlayed += (seat, card) =>
                Debug.Log($"[29]   {seat} played  {card}");

            _gm.OnTrickWon += (seat, pts) =>
                Debug.Log($"[29] ✔  {seat} wins the trick  (+{pts} pts)");

            _gm.OnTrumpRevealed += suit =>
                Debug.Log($"[29] ★  TRUMP REVEALED → {suit}!");

            _gm.OnRoundScored += won =>
            {
                var sm = _gm.ScoreManager;
                string result = won ? "WON ✔" : "LOST ✘";
                Debug.Log($"[29] Round over — {GameRules.TeamName(sm.BiddingTeam)} " +
                          $"bid {sm.CurrentBid}: {result}");
                Debug.Log($"[29] Scores → {sm.GetScoreString()}");
            };

            _gm.OnGameOver += team =>
            {
                Debug.Log($"[29] ██  GAME OVER  ██   {GameRules.TeamName(team)} WINS!");
                Debug.Log($"[29] Final → {_gm.ScoreManager.GetScoreString()}");
            };
        }
    }
}
