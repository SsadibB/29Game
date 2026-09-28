using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game29
{
    /// <summary>
    /// RulesButton shows DescriptionPanel. The rules text inside the panel
    /// is a vertical ScrollRect so long copy can be dragged/scrolled.
    /// </summary>
    public class RulesPanelUI : MonoBehaviour
    {
        [SerializeField] private Button rulesButton;
        [SerializeField] private GameObject descriptionPanel;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform textRect;
        [SerializeField] private Button closeBackgroundButton;

        private void Awake()
        {
            EnsureWired();
            Hide();
        }

        public void EnsureWired()
        {
            if (descriptionPanel == null)
                descriptionPanel = gameObject.name == "DescriptionPanel"
                    ? gameObject
                    : FindObjectByName("DescriptionPanel");

            if (rulesButton == null)
            {
                GameObject btnObj = FindObjectByName("RulesButton");
                if (btnObj != null)
                {
                    Image img = btnObj.GetComponent<Image>();
                    rulesButton = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
                    if (img != null) rulesButton.targetGraphic = img;
                }
            }

            if (rulesButton != null)
            {
                rulesButton.onClick.RemoveListener(Toggle);
                rulesButton.onClick.AddListener(Toggle);
            }

            if (descriptionPanel != null)
            {
                Transform bg = descriptionPanel.transform.Find("BG");
                if (bg != null)
                {
                    Image bgImage = bg.GetComponent<Image>();
                    closeBackgroundButton = bg.GetComponent<Button>() ?? bg.gameObject.AddComponent<Button>();
                    if (bgImage != null) closeBackgroundButton.targetGraphic = bgImage;
                    closeBackgroundButton.onClick.RemoveListener(Hide);
                    closeBackgroundButton.onClick.AddListener(Hide);
                }

                ConfigureScrollableText(descriptionPanel.transform);
            }
        }

        public void Show()
        {
            if (descriptionPanel == null) return;
            descriptionPanel.SetActive(true);
            descriptionPanel.transform.SetAsLastSibling();
            if (scrollRect != null)
                scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        public void Hide()
        {
            if (descriptionPanel != null)
                descriptionPanel.SetActive(false);
        }

        public void Toggle()
        {
            if (descriptionPanel != null && descriptionPanel.activeSelf)
                Hide();
            else
                Show();
        }

        private void ConfigureScrollableText(Transform panel)
        {
            if (scrollRect == null)
                scrollRect = panel.GetComponentInChildren<ScrollRect>(true);

            if (scrollRect == null) return;

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 40f;
            scrollRect.inertia = true;

            Image chrome = scrollRect.GetComponent<Image>();
            if (chrome != null)
            {
                Color c = chrome.color;
                c.a = 0f;
                chrome.color = c;
                chrome.raycastTarget = true;
            }

            RectTransform viewport = scrollRect.viewport;
            if (viewport == null && scrollRect.transform.Find("Viewport") != null)
                viewport = scrollRect.transform.Find("Viewport") as RectTransform;
            if (viewport != null)
            {
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = Vector2.zero;
                viewport.pivot = new Vector2(0.5f, 1f);
            }

            RectTransform content = scrollRect.content;
            if (content == null && viewport != null)
            {
                Transform contentT = viewport.Find("Content");
                if (contentT != null) content = contentT as RectTransform;
            }
            if (content == null) return;

            scrollRect.content = content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.offsetMin = new Vector2(0f, content.offsetMin.y);
            content.offsetMax = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>()
                                        ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(24, 24, 16, 40);
            layout.spacing = 8f;

            ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>()
                                             ?? content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (textRect == null)
            {
                TMP_Text tmp = content.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) textRect = tmp.rectTransform;
            }

            if (textRect == null) return;

            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = Vector2.zero;

            TMP_Text rulesText = textRect.GetComponent<TMP_Text>();
            if (rulesText != null)
            {
                rulesText.enableWordWrapping = true;
                rulesText.overflowMode = TextOverflowModes.Overflow;
                rulesText.raycastTarget = true;
            }

            ContentSizeFitter textFitter = textRect.GetComponent<ContentSizeFitter>()
                                          ?? textRect.gameObject.AddComponent<ContentSizeFitter>();
            textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private static GameObject FindObjectByName(string objectName)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == objectName && all[i].gameObject.scene.IsValid())
                    return all[i].gameObject;
            }
            return null;
        }
    }
}
