using UnityEngine;
using System.Collections.Generic;

public enum Language { English, Chinese }

/// <summary>
/// 本地化管理器，提供中英文文本查询。
/// 单例模式，在 Awake 中初始化。
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;
    [SerializeField] private LocalizationData localizationData;
    public Language CurrentLanguage => currentLanguage;

    private Language currentLanguage = Language.English;

    // 缓存字典，避免每次遍历查找
    private Dictionary<string, string> englishMap;
    private Dictionary<string, string> chineseMap;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 初始化缓存字典
            InitCache();

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

    private void InitCache()
    {
        if (localizationData == null || localizationData.entries == null)
            return;

        englishMap = new Dictionary<string, string>();
        chineseMap = new Dictionary<string, string>();

        foreach (var entry in localizationData.entries)
        {
            if (!string.IsNullOrEmpty(entry.key))
            {
                englishMap[entry.key] = entry.englishText;
                chineseMap[entry.key] = entry.chineseText;
            }
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
        if (string.IsNullOrEmpty(key))
            return $"<{key}>";

        var map = currentLanguage == Language.English ? englishMap : chineseMap;
        if (map != null && map.TryGetValue(key, out string text))
            return text;

        return $"<{key}>";
    }
}
