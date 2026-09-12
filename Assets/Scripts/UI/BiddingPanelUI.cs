using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game29
{
    /// <summary>
    /// Interactive bidding modal panel for the human player (South).
    /// Features bid stepper [-][+], quick preset buttons, current high bid display,
    /// and prominent BID and PASS action buttons.
    /// </summary>
    public class BiddingPanelUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image  panelBg;
        [SerializeField] private Text   titleText;
        [SerializeField] private Text   currentHighText;
        [SerializeField] private Text   bidValueText;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button bidButton;
        [SerializeField] private Text   bidButtonText;
        [SerializeField] private Button passButton;
        [SerializeField] private Transform presetsContainer;

        private readonly List<Button> _presetButtons = new List<Button>();
        private int _selectedBid = 16;
        private int _minAllowedBid = 16;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            HookButtonListeners();
        }

        public void EnsureComponents()
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(440, 240);

            if (panelBg == null)
            {
                panelBg = gameObject.GetComponent<Image>();
                if (panelBg == null) panelBg = gameObject.AddComponent<Image>();
                panelBg.sprite = CardVisualTheme.RoundedPanel;
                panelBg.type = Image.Type.Sliced;
                panelBg.color = new Color(0.06f, 0.10f, 0.16f, 0.96f);
            }

            if (titleText == null)
            {
                GameObject tObj = CreateTextObj("Title", new Vector2(0, 85), new Vector2(400, 30), 20, FontStyle.Bold, CardVisualTheme.ColorGold);
                titleText = tObj.GetComponent<Text>();
                titleText.text = "YOUR TURN TO BID";
            }

            if (currentHighText == null)
            {
                GameObject chObj = CreateTextObj("CurrentHigh", new Vector2(0, 56), new Vector2(400, 24), 14, FontStyle.Normal, new Color(0.75f, 0.82f, 0.90f));
                currentHighText = chObj.GetComponent<Text>();
            }

            if (bidValueText == null)
            {
                // Stepper row
                GameObject valObj = CreateTextObj("BidValue", new Vector2(0, 10), new Vector2(100, 48), 34, FontStyle.Bold, Color.white);
                bidValueText = valObj.GetComponent<Text>();

                minusButton = CreateButton("MinusBtn", new Vector2(-80, 10), new Vector2(50, 40), "-", 22, new Color(0.18f, 0.25f, 0.35f), Color.white, OnMinusClicked);
                plusButton  = CreateButton("PlusBtn", new Vector2(80, 10), new Vector2(50, 40), "+", 22, new Color(0.18f, 0.25f, 0.35f), Color.white, OnPlusClicked);
            }

            if (bidButton == null)
            {
                bidButton = CreateButton("BidBtn", new Vector2(105, -60), new Vector2(180, 46), "BID 16", 18, new Color(0.10f, 0.58f, 0.28f), Color.white, OnBidClicked);
                bidButtonText = bidButton.GetComponentInChildren<Text>();

                passButton = CreateButton("PassBtn", new Vector2(-105, -60), new Vector2(160, 46), "PASS", 17, new Color(0.60f, 0.18f, 0.18f), Color.white, OnPassClicked);
            }

            // Always ensure click listeners are registered even when loaded from serialized scene
            HookButtonListeners();
        }

        private void HookButtonListeners()
        {
            if (minusButton != null)
            {
                minusButton.onClick.RemoveAllListeners();
                minusButton.onClick.AddListener(OnMinusClicked);
            }
            if (plusButton != null)
            {
                plusButton.onClick.RemoveAllListeners();
                plusButton.onClick.AddListener(OnPlusClicked);
            }
            if (bidButton != null)
            {
                bidButton.onClick.RemoveAllListeners();
                bidButton.onClick.AddListener(OnBidClicked);
            }
            if (passButton != null)
            {
                passButton.onClick.RemoveAllListeners();
                passButton.onClick.AddListener(OnPassClicked);
            }
        }

        public void Show(int currentHighBid, PlayerSeat currentHighBidder, int minRaise)
        {
            EnsureComponents();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            _minAllowedBid = Mathf.Clamp(minRaise, GameRules.MinBid, GameRules.MaxBid);
            _selectedBid   = _minAllowedBid;

            if (currentHighBid >= GameRules.MinBid)
            {
                string bidderName = currentHighBidder == PlayerSeat.North ? "Partner (North)" : currentHighBidder.ToString();
                currentHighText.text = $"Current High: <b>{currentHighBid}</b> by {bidderName}";
            }
            else
            {
                currentHighText.text = $"Opening Bid (Minimum: {GameRules.MinBid})";
            }

            UpdateDisplay();

            transform.DOKill();
            transform.localScale = Vector3.one * 0.75f;
            transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
        }

        public void Hide()
        {
            transform.DOKill();
            transform.DOScale(0.8f, 0.16f).SetEase(Ease.InQuad).OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }

        private void UpdateDisplay()
        {
            if (bidValueText != null)
                bidValueText.text = _selectedBid.ToString();

            if (bidButtonText != null)
                bidButtonText.text = $"BID {_selectedBid}";

            if (minusButton != null)
                minusButton.interactable = _selectedBid > _minAllowedBid;

            if (plusButton != null)
                plusButton.interactable = _selectedBid < GameRules.MaxBid;

            if (bidButton != null)
                bidButton.interactable = _selectedBid >= _minAllowedBid;
        }

        private void OnMinusClicked()
        {
            if (_selectedBid > _minAllowedBid)
            {
                _selectedBid--;
                UpdateDisplay();
            }
        }

        private void OnPlusClicked()
        {
            if (_selectedBid < GameRules.MaxBid)
            {
                _selectedBid++;
                UpdateDisplay();
            }
        }

        private void OnBidClicked()
        {
            Debug.Log($"[29 BiddingPanel] Human South clicked BID {_selectedBid}");
            bool ok = GameManager.Instance.PlaceHumanBid(_selectedBid);
            if (ok) Hide();
        }

        private void OnPassClicked()
        {
            Debug.Log("[29 BiddingPanel] Human South clicked PASS");
            bool ok = GameManager.Instance.HumanPass();
            if (ok) Hide();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private GameObject CreateTextObj(string name, Vector2 pos, Vector2 size, int fontSize, FontStyle style, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text txt = obj.AddComponent<Text>();
            txt.font = CardVisualTheme.GetFont();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.raycastTarget = false;
            return obj;
        }

        private Button CreateButton(string name, Vector2 pos, Vector2 size, string label, int fontSize, Color bgCol, Color textCol, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.sprite = CardVisualTheme.PillBadge;
            img.type = Image.Type.Sliced;
            img.color = bgCol;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            GameObject tObj = new GameObject("Label");
            tObj.transform.SetParent(btnObj.transform, false);
            RectTransform trt = tObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;

            Text txt = tObj.AddComponent<Text>();
            txt.font = CardVisualTheme.GetFont();
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = textCol;
            txt.raycastTarget = false;
            txt.text = label;

            return btn;
        }
    }
}
