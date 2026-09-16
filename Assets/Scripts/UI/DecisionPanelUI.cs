using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game29
{
    /// <summary>
    /// Controls the existing Decision Panel GameObject for Single, Double, and Re-Double decisions.
    /// Dynamically alters its Title and DecisionBtn text based on the decision context:
    ///   • Single     → "Do you want to play single?" / "SINGLE"
    ///   • Double     → "Do you want to set double?" / "DOUBLE"
    ///   • Re-Double  → "[Position] set double." / "RE-DOUBLE"
    /// </summary>
    public class DecisionPanelUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text titleText;
        [SerializeField] private Button decisionBtn;
        [SerializeField] private Text decisionBtnText;
        [SerializeField] private Button negativeBtn;
        [SerializeField] private Text negativeBtnText;

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
                Transform t = transform.Find("Title");
                if (t != null) titleText = t.GetComponent<Text>();
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

        /// <summary>
        /// Displays the panel for Single Play decision:
        /// Title: "Do you want to play single?"
        /// DecisionBtn: "SINGLE"
        /// </summary>
        public void ShowSinglePlay(Action onConfirm, Action onReject)
        {
            EnsureComponents();
            _onConfirm = onConfirm;
            _onReject = onReject;

            if (titleText != null) titleText.text = "Do you want to play single?";
            if (decisionBtnText != null) decisionBtnText.text = "SINGLE";
            if (negativeBtnText != null) negativeBtnText.text = "NO";

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        /// <summary>
        /// Displays the panel for Double decision:
        /// Title: "Do you want to set double?"
        /// DecisionBtn: "DOUBLE"
        /// </summary>
        public void ShowDouble(Action onConfirm, Action onReject)
        {
            EnsureComponents();
            _onConfirm = onConfirm;
            _onReject = onReject;

            if (titleText != null) titleText.text = "Do you want to set double?";
            if (decisionBtnText != null) decisionBtnText.text = "DOUBLE";
            if (negativeBtnText != null) negativeBtnText.text = "NO";

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        /// <summary>
        /// Displays the panel for Re-Double decision:
        /// Title: "[Player Position] set double." (e.g. "EAST set double.")
        /// DecisionBtn: "RE-DOUBLE"
        /// </summary>
        public void ShowReDouble(string doublerPositionName, Action onConfirm, Action onReject)
        {
            EnsureComponents();
            _onConfirm = onConfirm;
            _onReject = onReject;

            string posName = string.IsNullOrEmpty(doublerPositionName) ? "Opponent" : doublerPositionName.ToUpper();
            if (titleText != null) titleText.text = $"{posName} set double.";
            if (decisionBtnText != null) decisionBtnText.text = "RE-DOUBLE";
            if (negativeBtnText != null) negativeBtnText.text = "NO";

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            _onConfirm = null;
            _onReject = null;
            gameObject.SetActive(false);
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
