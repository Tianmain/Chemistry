using UnityEngine;

public enum Language { English, Chinese }

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;
    [SerializeField] private LocalizationData localizationData;
    public Language CurrentLanguage => currentLanguage;

    private Language currentLanguage = Language.English;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (PlayerPrefs.HasKey("Language"))
            {
                currentLanguage = (Language)PlayerPrefs.GetInt("Language");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetLanguage(Language lang)
    {
        currentLanguage = lang;
        PlayerPrefs.SetInt("Language", (int)lang);
        UIManager.Instance.OnLanguageChanged();
    }

    public string GetLocalizedText(string key)
    {
        foreach (var entry in localizationData.entries)
        {
            if (entry.key == key)
            {
                return currentLanguage == Language.English ? entry.englishText : entry.chineseText;
            }
        }
        return $"<{key}>";
    }
}
