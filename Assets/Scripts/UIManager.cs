using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI elementText;
    [SerializeField] private TextMeshProUGUI keyMapText;
    [SerializeField] private TextMeshProUGUI selectedInfoText;
    [SerializeField] private RectTransform selectedInfoRect;

    [SerializeField] private Button aboutButton;
    [SerializeField] private Button helpButton;
    private bool isHelpVisible = false;

    [SerializeField] private SettingsPanel settingsPanel;
    public void OnSettingsButtonClick() => settingsPanel.Show();

    public static UIManager Instance;

    [System.Serializable]
    public class LocalizedUIElement
    {
        public TMP_Text textComponent;
        public string localizationKey;
    }

    [SerializeField] private List<LocalizedUIElement> localizedElements = new List<LocalizedUIElement>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 首次运行默认中文
            if (!PlayerPrefs.HasKey("Language"))
            {
                LocalizationManager.Instance.SetLanguage(Language.Chinese);
                PlayerPrefs.SetInt("Language", (int)Language.Chinese);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnLanguageChanged()
    {
        UpdateAllUITexts();
        UpdateKeyMapText();
        UpdateUILayout();
    }

    public void UpdateAllUITexts()
    {
        foreach (var element in localizedElements)
        {
            if (element.textComponent != null)
            {
                element.textComponent.text = LocalizationManager.Instance.GetLocalizedText(element.localizationKey);
            }
        }
    }

    public void UpdateSelectedInfo(string content)
    {
        if (selectedInfoText != null)
        {
            string template = LocalizationManager.Instance.GetLocalizedText("selected_info");
            selectedInfoText.text = string.Format(template, content);
        }
    }

    private void UpdateUILayout()
    {
        if (selectedInfoRect == null) return;

        // 英文文本较长，需要更宽的布局
        if (LocalizationManager.Instance.CurrentLanguage == Language.English)
        {
            selectedInfoRect.anchoredPosition = new Vector2(-350, selectedInfoRect.anchoredPosition.y);
            selectedInfoRect.sizeDelta = new Vector2(700, selectedInfoRect.sizeDelta.y);
        }
        else
        {
            selectedInfoRect.anchoredPosition = new Vector2(-210, selectedInfoRect.anchoredPosition.y);
            selectedInfoRect.sizeDelta = new Vector2(500, selectedInfoRect.sizeDelta.y);
        }
    }

    public void RegisterLocalizedUI(TMP_Text textComponent, string key)
    {
        localizedElements.Add(new LocalizedUIElement { textComponent = textComponent, localizationKey = key });
    }

    void Start()
    {
        keyMapText.gameObject.SetActive(false);
        UpdateAllUITexts();
        UpdateKeyMapText();
        aboutButton.onClick.AddListener(OnAboutButtonClick);
        UpdateUILayout();
    }

    public void ToggleHelp()
    {
        isHelpVisible = !isHelpVisible;
        keyMapText.gameObject.SetActive(isHelpVisible);
    }

    public void UpdateElementText(Element element)
    {
        if (element != null)
        {
            string localizedElementName = LocalizationManager.Instance.GetLocalizedText($"element_{element.name.ToLower()}");
            string template = LocalizationManager.Instance.GetLocalizedText("current_element");
            elementText.text = string.Format(template, localizedElementName, element.symbol);
        }
        else
        {
            elementText.text = LocalizationManager.Instance.GetLocalizedText("current_element_empty");
        }
    }

    public void UpdateKeyMapText()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_title"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_hydrogen"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_lithium"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_carbon"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_nitrogen"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_oxygen"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_fluorine"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_sodium"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_magnesium"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_aluminum"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_silicon"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_phosphorus"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_sulfur"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_chlorine"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_potassium"));
        sb.AppendLine(LocalizationManager.Instance.GetLocalizedText("keymap_calcium"));
        keyMapText.text = sb.ToString();
    }

    private void OnAboutButtonClick()
    {
        Application.OpenURL("https://space.bilibili.com/3546583949904055?spm_id_from=333.1007.0.0");
    }
}
