using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game29
{
    /// <summary>
    /// Fully playable debug overlay using Unity's legacy OnGUI system.
    /// Provides all controls needed to play a complete game of 29 in the Editor
    /// before the real UI is built.
    ///
    /// Features:
    ///   • Opaque dark background panel (always readable, any camera colour)
    ///   • Screen-proportional sizing (works in Simulator and Game view)
    ///   • Bidding controls (slider + bid/pass buttons)
    ///   • Clickable hand cards with suit colour coding
    ///   • Live trick display (all 4 played cards)
    ///   • Per-team card-point and game-point display
    ///   • Hidden/revealed trump indicator
    ///   • Round-over summary and "Next Round" button
    ///   • Game-over banner and "New Game" button
    ///
    /// REMOVE or DISABLE this component once proper UI is implemented.
    /// </summary>
    public class DebugGameDisplay : MonoBehaviour
    {
        // ── Layout ───────────────────────────────────────────────────────────
        private const float PanelX = 10f;
        private const float PanelY = 10f;
        private const float CardBtnH = 34f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private GameManager _gm;
        private int _bidSlider;
        private string _statusMsg = "";
        private GUIStyle _headerStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _dimCardStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _statusStyle;
        private bool _stylesBuilt;
        [SerializeField] private bool showDebugOverlay = false;
        public bool ShowDebugOverlay { get => showDebugOverlay; set => showDebugOverlay = value; }

        // Texture used for the solid background panel
        private Texture2D _bgTex;

        // ════════════════════════════════════════════════════════════════════════
        // UNITY
        // ════════════════════════════════════════════════════════════════════════

        private void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null) return;

            _bidSlider = GameRules.MinBid;

            // Solid dark background texture
            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, new Color(0.06f, 0.07f, 0.10f, 0.93f));
            _bgTex.Apply();

            _gm.OnTrickWon += (s, p) => _statusMsg = $"✔  {s} wins trick  (+{p} pts)";
            _gm.OnTrumpRevealed += s => _statusMsg = $"★  Trump revealed: {s}!";
            _gm.OnRoundScored += won => _statusMsg = won ? "Bidding team WON  ✔" : "Bidding team LOST  ✘";
            _gm.OnGameOver += team => _statusMsg = $"GAME OVER — {GameRules.TeamName(team)} WINS!";
            _gm.OnPhaseChanged += _ => _bidSlider = _gm.GetMinimumBid();
        }

        private void OnGUI()
        {
            if (!showDebugOverlay) return;
            if (_gm == null || _bgTex == null) return;
            BuildStyles();

            // Panel dimensions — 42 % of screen width, max 520 px
            float panelW = Mathf.Min(Screen.width * 0.42f, 520f);
            float panelH = Screen.height - 20f;

            // ── Solid background ──────────────────────────────────────────
            GUI.DrawTexture(new Rect(PanelX, PanelY, panelW, panelH), _bgTex);

            // Thin border
            GUI.color = new Color(0.3f, 0.6f, 1f, 0.6f);
            GUI.DrawTexture(new Rect(PanelX, PanelY, panelW, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(PanelX, PanelY + panelH - 1, panelW, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(PanelX, PanelY, 1, panelH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(PanelX + panelW - 1, PanelY, 1, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // ── Content area ──────────────────────────────────────────────
            GUILayout.BeginArea(new Rect(PanelX + 8, PanelY + 8, panelW - 16, panelH - 16));
            {
                DrawHeader();
                DrawScores();
                DrawTrumpBid();
                DrawStatusBar();
                GUILayout.Space(6);
                DrawPhasePanel(panelW);
                GUILayout.Space(6);
                DrawHand();
                GUILayout.Space(6);
                DrawCurrentTrick();
            }
            GUILayout.EndArea();
        }

        // ════════════════════════════════════════════════════════════════════════
        // SECTIONS
        // ════════════════════════════════════════════════════════════════════════

        private void DrawHeader()
        {
            GUILayout.Label("  ♠ ♥  29 GAME  ♦ ♣   [ " + _gm.CurrentPhase + " ]", _headerStyle);
            GUILayout.Label("Dealer: " + _gm.Dealer + "          Active: " + _gm.CurrentPlayer, _labelStyle);
        }

        private void DrawScores()
        {
            int[] gp = _gm.GetGamePoints();
            GUILayout.Label("Game Pts  S+N: " + gp[0] + " / " + GameRules.GamePointsToWin +
                            "    E+W: " + gp[1] + " / " + GameRules.GamePointsToWin, _labelStyle);

            if (_gm.CurrentPhase == GamePhase.Playing || _gm.CurrentPhase == GamePhase.RoundOver)
            {
                int[] rp = _gm.GetRoundPoints();
                GUILayout.Label("Round Pts  S+N: " + rp[0] +
                                "    E+W: " + rp[1] +
                                "    (Bid: " + _gm.GetFinalBid() + ")", _labelStyle);
            }
        }

        private void DrawTrumpBid()
        {
            if (_gm.CurrentPhase != GamePhase.Playing && _gm.CurrentPhase != GamePhase.RoundOver)
                return;

            Suit? trump = _gm.GetTrumpForHuman();
            if (trump.HasValue)
            {
                string revealed = _gm.IsTrumpRevealed()
                    ? "(revealed to all)"
                    : "(you know — hidden from opponents)";
                GUI.color = SuitColour(trump.Value);
                GUILayout.Label("Trump: " + SuitSymbol(trump.Value) + " " + trump.Value + "  " + revealed, _labelStyle);
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label("Trump: ? (not yet revealed)", _labelStyle);
            }
        }

        private void DrawStatusBar()
        {
            if (string.IsNullOrEmpty(_statusMsg)) return;
            GUILayout.Label("  " + _statusMsg, _statusStyle);
        }

        private void DrawPhasePanel(float panelW)
        {
            switch (_gm.CurrentPhase)
            {
                case GamePhase.WaitingToStart:
                    if (GUILayout.Button("  Start New Game  ", GUILayout.Height(38)))
                        _gm.StartNewGame();
                    break;

                case GamePhase.Bidding:
                    DrawBiddingPanel(panelW);
                    break;

                case GamePhase.TrumpSelection:
                    DrawTrumpSelectionPanel();
                    break;

                case GamePhase.Playing:
                    if (_gm.CurrentPlayer == GameManager.HumanSeat)
                        GUILayout.Label("YOUR TURN — click a card below", _headerStyle);
                    else
                        GUILayout.Label("... " + _gm.CurrentPlayer + " is thinking ...", _labelStyle);
                    break;

                case GamePhase.RoundOver:
                    DrawRoundOverPanel();
                    break;

                case GamePhase.GameOver:
                    DrawGameOverPanel();
                    break;
            }
        }

        private void DrawBiddingPanel(float panelW)
        {
            int minBid = _gm.GetMinimumBid();
            _bidSlider = Mathf.Max(_bidSlider, minBid);

            GUILayout.Label("--- BIDDING ---", _headerStyle);
            GUILayout.Label("High Bid: " + _gm.GetCurrentBid() + "  by  " + _gm.GetCurrentHighBidder(), _labelStyle);

            if (_gm.CurrentPlayer != GameManager.HumanSeat)
            {
                GUILayout.Label("... " + _gm.CurrentPlayer + " is deciding ...", _labelStyle);
                return;
            }

            GUILayout.Label("YOUR TURN TO BID:", _labelStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Bid:", _labelStyle, GUILayout.Width(36));
            float sliderW = panelW - 120f;
            _bidSlider = (int)GUILayout.HorizontalSlider(_bidSlider, minBid, GameRules.MaxBid, GUILayout.Width(sliderW));
            GUILayout.Label(_bidSlider.ToString("D2"), _labelStyle, GUILayout.Width(30));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("  Bid " + _bidSlider + "  ", GUILayout.Height(38)))
            {
                bool ok = _gm.PlaceHumanBid(_bidSlider);
                if (!ok) _statusMsg = "Invalid bid!";
            }
            GUILayout.Space(10);
            if (GUILayout.Button("  PASS  ", GUILayout.Height(38)))
                _gm.HumanPass();
            GUILayout.EndHorizontal();
        }

        private void DrawTrumpSelectionPanel()
        {
            GUILayout.Label("--- TRUMP SELECTION ---", _headerStyle);

            PlayerSeat bidWinner = _gm.GetBidWinner();
            if (bidWinner != GameManager.HumanSeat)
            {
                string name = bidWinner == PlayerSeat.North ? "Partner (North)" : bidWinner.ToString();
                GUILayout.Label(name + " won the bid and is choosing trump...", _labelStyle);
                return;
            }

            GUILayout.Label("YOU WON THE BID (" + _gm.GetCurrentBid() + ")! Choose trump:", _labelStyle);

            Hand hand = _gm.HumanHand;
            GUILayout.BeginHorizontal();
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
            {
                int count = hand.GetCardsBySuit(suit).Count;
                GUI.color = SuitColour(suit);
                GUI.enabled = count > 0;
                if (GUILayout.Button(SuitSymbol(suit) + " " + suit + "\n(" + count + " in hand)",
                        GUILayout.Height(48)))
                {
                    _gm.SelectHumanTrump(suit);
                }
                GUI.enabled = true;
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🎴 7TH CARD (blind mystery trump)", GUILayout.Height(34)))
                _gm.SelectHumanSeventhCard();
            GUILayout.Space(10);
            if (GUILayout.Button("🃏 JOKER (no trump)", GUILayout.Height(34)))
                _gm.SelectHumanJoker();
            GUILayout.EndHorizontal();
        }

        private void DrawRoundOverPanel()
        {
            int[] rp = _gm.GetRoundPoints();
            int[] gp = _gm.GetGamePoints();
            GUILayout.Label("=====  ROUND OVER  =====", _headerStyle);
            GUILayout.Label("Card-pts  S+N: " + rp[0] + "    E+W: " + rp[1] +
                            "    (Bid was: " + _gm.GetFinalBid() + ")", _labelStyle);
            GUILayout.Label("Scores    S+N: " + gp[0] + " / 6    E+W: " + gp[1] + " / 6", _labelStyle);
            GUILayout.Space(6);
            if (GUILayout.Button("  Next Round  ", GUILayout.Height(38)))
                _gm.StartNextRound();
        }

        private void DrawGameOverPanel()
        {
            int winner = _gm.ScoreManager.GetWinningTeam();
            GUILayout.Label("=====  GAME  OVER  =====", _headerStyle);
            GUILayout.Label("  " + GameRules.TeamName(winner) + "  WINS!", _headerStyle);
            GUILayout.Label("  " + _gm.ScoreManager.GetScoreString(), _labelStyle);
            GUILayout.Space(6);
            if (GUILayout.Button("  New Game  ", GUILayout.Height(38)))
            {
                _statusMsg = "";
                _gm.StartNewGame();
            }
        }

        // ── Hand ─────────────────────────────────────────────────────────────

        private void DrawHand()
        {
            if (_gm.CurrentPhase != GamePhase.Bidding &&
                _gm.CurrentPhase != GamePhase.Playing &&
                _gm.CurrentPhase != GamePhase.TrumpSelection) return;

            Hand hand = _gm.HumanHand;
            List<Card> validPlays = _gm.GetHumanValidPlays();
            bool isMyTurn = _gm.CurrentPhase == GamePhase.Playing
                                    && _gm.CurrentPlayer == GameManager.HumanSeat;

            GUILayout.Label("--- YOUR HAND  (" + hand.Count + " cards, " + hand.TotalPoints() + " pts) ---", _labelStyle);

            var cards = hand.Cards.ToList();
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < 4; col++)
                {
                    int idx = row * 4 + col;
                    if (idx >= cards.Count) { GUILayout.Space(CardBtnH + 4); continue; }

                    Card card = cards[idx];
                    bool canPlay = isMyTurn && validPlays.Contains(card);
                    bool isLegal = validPlays.Contains(card);

                    Color bgCol = Color.white;
                    if (isMyTurn && isLegal) bgCol = new Color(0.15f, 0.75f, 0.25f);  // green — can play
                    if (isMyTurn && !isLegal) bgCol = new Color(0.25f, 0.25f, 0.25f);  // grey  — can't play

                    GUI.backgroundColor = bgCol;
                    GUI.color = SuitColour(card.Suit);
                    GUI.enabled = canPlay;

                    if (GUILayout.Button(CardLabel(card), _cardStyle,
                            GUILayout.Width(CardBtnH * 2.4f), GUILayout.Height(CardBtnH)))
                    {
                        bool ok = _gm.PlayHumanCard(card);
                        _statusMsg = ok ? "You played " + card : "Cannot play that card!";
                    }

                    GUI.enabled = true;
                    GUI.color = Color.white;
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.EndHorizontal();
            }
        }

        // ── Current Trick ────────────────────────────────────────────────────

        private void DrawCurrentTrick()
        {
            Trick trick = _gm.GetCurrentTrick();
            if (trick == null || trick.IsEmpty) return;

            GUILayout.Label("--- CURRENT TRICK ---", _labelStyle);
            GUILayout.BeginHorizontal();
            foreach (var (player, card) in trick.Plays)
            {
                GUI.color = SuitColour(card.Suit);
                GUILayout.Box(player + "\n" + CardLabel(card),
                    GUILayout.Width(94), GUILayout.Height(46));
                GUI.color = Color.white;
            }
            for (int i = trick.PlayCount; i < 4; i++)
                GUILayout.Box("—", GUILayout.Width(94), GUILayout.Height(46));
            GUILayout.EndHorizontal();
        }

        // ════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════════

        private string CardLabel(Card c)
        {
            string rank;
            switch (c.Rank)
            {
                case Rank.Jack: rank = "J"; break;
                case Rank.Queen: rank = "Q"; break;
                case Rank.King: rank = "K"; break;
                case Rank.Ace: rank = "A"; break;
                case Rank.Ten: rank = "10"; break;
                case Rank.Nine: rank = "9"; break;
                case Rank.Eight: rank = "8"; break;
                default: rank = "7"; break;
            }
            string pts = c.PointValue > 0 ? "(" + c.PointValue + ")" : "";
            return rank + SuitSymbol(c.Suit) + pts;
        }

        private Color SuitColour(Suit s)
        {
            return (s == Suit.Hearts || s == Suit.Diamonds) ? new Color(1f, 0.35f, 0.35f) : new Color(0.85f, 0.85f, 0.95f);
        }

        private string SuitSymbol(Suit s)
        {
            switch (s)
            {
                case Suit.Hearts: return "♥";
                case Suit.Diamonds: return "♦";
                case Suit.Clubs: return "♣";
                default: return "♠";
            }
        }

        private void BuildStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _headerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = Color.white;

            _cardStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            _dimCardStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11
            };
            _dimCardStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);

            _statusStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _statusStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);
        }
    }
}