using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game29
{
    /// <summary>
    /// Controls the shared Decision Panel GameObject used for every important
    /// in-round decision and announcement: who set trump, the target call,
    /// the trump type (once public), Double / Re-Double, and Single Play.
    ///
    /// The panel is additive — each call builds only the info rows that are
    /// currently relevant/known (see <see cref="BuildInfoLines"/>) and shows only
    /// the decision buttons that apply at that stage:
    ///   • Double decision      → "DOUBLE" / "NO"
    ///   • Re-Double decision   → "RE-DOUBLE" / "NO"
    ///   • Single Play decision → "SINGLE" / "NO"
    ///   • Info / announcement  → "OK" only (no action to take, just acknowledge)
    /// </summary>
    public class DecisionPanelUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text bidInfoText;
        [SerializeField] private Button decisionBtn;
        [SerializeField] private Text decisionBtnText;
        [SerializeField] private Button negativeBtn;
        [SerializeField] private Text negativeBtnText;

        private const int MaxFontSize = 35;
        private const int MinFontSize = 16;

        private Action _onConfirm;
        private Action _onReject;

        private void Awake()
        {
            EnsureComponents();
        }

        public void EnsureComponents()
        {
            if (titleText == null)
            {
                Transform t = FindChildByName(transform, "Title");
                if (t != null) titleText = t.GetComponent<Text>();
            }
            if (titleText != null)
            {
                titleText.resizeTextForBestFit = true;
                titleText.resizeTextMinSize = MinFontSize;
                titleText.resizeTextMaxSize = MaxFontSize;
                titleText.verticalOverflow = VerticalWrapMode.Overflow;
                titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
                titleText.alignment = TextAnchor.MiddleCenter;
            }

            if (bidInfoText == null)
            {
                Transform bid = FindChildByName(transform, "BidInfo");
                if (bid != null) bidInfoText = bid.GetComponent<Text>() ?? bid.GetComponentInChildren<Text>(true);
            }
            if (bidInfoText != null)
            {
                bidInfoText.horizontalOverflow = HorizontalWrapMode.Wrap;
                bidInfoText.verticalOverflow = VerticalWrapMode.Overflow;
                bidInfoText.supportRichText = true;
            }

            if (decisionBtn == null)
            {
                Transform d = transform.Find("DecisionBtn");
                if (d != null)
                {
                    decisionBtn = d.GetComponent<Button>();
                    decisionBtnText = d.GetComponentInChildren<Text>();
                }
            }
            else if (decisionBtnText == null)
            {
                decisionBtnText = decisionBtn.GetComponentInChildren<Text>();
            }
            if (decisionBtnText != null) decisionBtnText.fontSize = 26;

            if (negativeBtn == null)
            {
                Transform n = transform.Find("NegativeBtn");
                if (n != null)
                {
                    negativeBtn = n.GetComponent<Button>();
                    negativeBtnText = n.GetComponentInChildren<Text>();
                }
            }
            else if (negativeBtnText == null)
            {
                negativeBtnText = negativeBtn.GetComponentInChildren<Text>();
            }
            if (negativeBtnText != null) negativeBtnText.fontSize = 26;

            if (decisionBtn != null)
            {
                decisionBtn.onClick.RemoveAllListeners();
                decisionBtn.onClick.AddListener(OnDecisionButtonClicked);
            }

            if (negativeBtn != null)
            {
                negativeBtn.onClick.RemoveAllListeners();
                negativeBtn.onClick.AddListener(OnNegativeButtonClicked);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PUBLIC SHOW METHODS
        // ════════════════════════════════════════════════════════════════════
        // The card's title text is ALWAYS just the public round facts —
        // who set trump, target, trump type — and nothing else. The question
        // being asked ("DO YOU WANT TO...") lives only in the status banner
        // (set by the caller), never duplicated here.

        /// <summary>"Do you want to set Double?" decision.</summary>
        public void ShowDoubleDecision(string trumpSetterPosition, int target, string trumpTypeLabel,
            Action onConfirm, Action onReject)
        {
            ShowDecision(trumpSetterPosition, target, trumpTypeLabel,
                title: "DO YOU WANT TO DOUBLE?",
                decisionLabel: "DOUBLE", negativeLabel: "NO",
                onConfirm, onReject);
        }

        /// <summary>"Do you want to Re-Double?" decision.</summary>
        public void ShowReDoubleDecision(string trumpSetterPosition, int target, string trumpTypeLabel,
            Action onConfirm, Action onReject)
        {
            ShowDecision(trumpSetterPosition, target, trumpTypeLabel,
                title: "DO YOU WANT TO RE-DOUBLE?",
                decisionLabel: "RE-DOUBLE", negativeLabel: "NO",
                onConfirm, onReject);
        }

        /// <summary>"Do you want to play single?" decision.</summary>
        public void ShowSinglePlayDecision(string trumpSetterPosition, int target, string trumpTypeLabel,
            Action onConfirm, Action onReject)
        {
            ShowDecision(trumpSetterPosition, target, trumpTypeLabel,
                title: "DO YOU WANT TO PLAY SINGLE?",
                decisionLabel: "SINGLE", negativeLabel: "NO",
                onConfirm, onReject);
        }

        /// <summary>
        /// Announcement-only variant — no decision to make, just an "OK" to
        /// acknowledge the current round facts.
        /// </summary>
        public void ShowInfo(string trumpSetterPosition, int target, string trumpTypeLabel, Action onAcknowledge)
        {
            EnsureComponents();
            _onConfirm = onAcknowledge;
            _onReject = null;

            if (titleText != null) titleText.text = "ROUND INFO";
            SetBidInfo(trumpSetterPosition, target, trumpTypeLabel);

            if (decisionBtnText != null) decisionBtnText.text = "OK";
            if (decisionBtn != null) decisionBtn.gameObject.SetActive(true);
            if (negativeBtn != null) negativeBtn.gameObject.SetActive(false);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            _onConfirm = null;
            _onReject = null;
            if (negativeBtn != null) negativeBtn.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }

        // ════════════════════════════════════════════════════════════════════
        // INTERNAL
        // ════════════════════════════════════════════════════════════════════

        private void ShowDecision(string trumpSetterPosition, int target, string trumpTypeLabel,
            string title, string decisionLabel, string negativeLabel, Action onConfirm, Action onReject)
        {
            EnsureComponents();
            _onConfirm = onConfirm;
            _onReject = onReject;

            if (titleText != null) titleText.text = title;
            SetBidInfo(trumpSetterPosition, target, trumpTypeLabel);

            if (decisionBtnText != null) decisionBtnText.text = decisionLabel;
            if (negativeBtnText != null) negativeBtnText.text = negativeLabel;
            if (decisionBtn != null) decisionBtn.gameObject.SetActive(true);
            if (negativeBtn != null) negativeBtn.gameObject.SetActive(true);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void SetBidInfo(string trumpSetterPosition, int target, string trumpTypeLabel)
        {
            if (bidInfoText == null) return;
            bidInfoText.text = BuildInfoLines(trumpSetterPosition, target, trumpTypeLabel);
        }

        /// <summary>
        /// Bid facts on the existing BidInfo text: who set trump, target, trump type.
        /// </summary>
        private static string BuildInfoLines(string trumpSetterPosition, int? target, string trumpTypeLabel)
        {
            var lines = new List<string>(3);

            if (!string.IsNullOrEmpty(trumpSetterPosition) && target.HasValue)
                lines.Add($"Trump Player: {trumpSetterPosition} - {target.Value}");
            else if (!string.IsNullOrEmpty(trumpSetterPosition))
                lines.Add($"Trump Player: {trumpSetterPosition}");
            else if (target.HasValue)
                lines.Add($"TARGET : {target.Value}");

            if (!string.IsNullOrEmpty(trumpTypeLabel))
                lines.Add($"TRUMP TYPE : {trumpTypeLabel.ToUpper()}");

            return string.Join("\n", lines);
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void OnDecisionButtonClicked()
        {
            Action confirmAction = _onConfirm;
            Hide();
            confirmAction?.Invoke();
        }

        private void OnNegativeButtonClicked()
        {
            Action rejectAction = _onReject;
            Hide();
            rejectAction?.Invoke();
        }
    }
}