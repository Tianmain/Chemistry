using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 本地化翻译
/// </summary>
[CreateAssetMenu(fileName = "LocalizationData", menuName = "Localization/Data")]
public class LocalizationData : ScriptableObject
{
    [System.Serializable]
    public class LanguageEntry
    {
        public string key;
        public string englishText;
        public string chineseText;
    }

    public List<LanguageEntry> entries = new List<LanguageEntry>();
}
