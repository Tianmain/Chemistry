using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 场景存档面板：打开 / 保存 / 另存为
/// 同时支持 Unity Editor 和独立打包版本（Windows）
/// </summary>
public class Save : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button saveAsButton;

    [Header("场景引用")]
    [SerializeField] private AtomManager atomManager;
    [SerializeField] private DashedBondManager dashedBondManager;
    [SerializeField] private HistoryManager historyManager;

    private string _currentFilePath = "";

    private void Start()
    {
        if (openButton   != null) openButton.onClick.AddListener(OnOpenClicked);
        if (saveButton   != null) saveButton.onClick.AddListener(OnSaveClicked);
        if (saveAsButton != null) saveAsButton.onClick.AddListener(OnSaveAsClicked);
    }

    // ==================== Open ====================
    private void OnOpenClicked()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[Save] SaveManager.Instance 为空");
            return;
        }

        string path = OpenFileDialog(
            "Open Scene / 打开场景",
            SaveManager.Instance.GetSaveDirectory(),
            "json"
        );

        if (string.IsNullOrEmpty(path))
            return;

        bool ok = SaveManager.Instance.LoadSceneFromPath(path, atomManager, dashedBondManager);
        if (ok)
        {
            _currentFilePath = path;
        }
        else
        {
            Debug.LogError($"[Save] 打开失败：{Path.GetFileName(path)}");
        }
    }

    // ==================== Save ====================
    private void OnSaveClicked()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[Save] SaveManager.Instance 为空");
            return;
        }

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            OnSaveAsClicked();
            return;
        }

        bool ok = SaveManager.Instance.SaveSceneToPath(_currentFilePath, atomManager, dashedBondManager);
        if (!ok)
        {
            Debug.LogError($"[Save] 保存失败：{Path.GetFileName(_currentFilePath)}");
        }
    }

    // ==================== Save As ====================
    private void OnSaveAsClicked()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[Save] SaveManager.Instance 为空");
            return;
        }

        string directory = string.IsNullOrEmpty(_currentFilePath)
            ? SaveManager.Instance.GetSaveDirectory()
            : Path.GetDirectoryName(_currentFilePath);

        string defaultName = string.IsNullOrEmpty(_currentFilePath)
            ? "scene.json"
            : Path.GetFileName(_currentFilePath);

        string path = SaveFileDialog(
            "Save Scene As / 场景另存为",
            directory,
            defaultName,
            "json"
        );

        if (string.IsNullOrEmpty(path))
            return;

        bool ok = SaveManager.Instance.SaveSceneToPath(path, atomManager, dashedBondManager);
        if (ok)
        {
            _currentFilePath = path;
        }
        else
        {
            Debug.LogError($"[Save] 另存为失败：{Path.GetFileName(path)}");
        }
    }

    // ==================== 平台适配 ====================
#if UNITY_EDITOR
    private string OpenFileDialog(string title, string directory, string extension)
    {
        return UnityEditor.EditorUtility.OpenFilePanel(title, directory, extension);
    }

    private string SaveFileDialog(string title, string directory, string defaultName, string extension)
    {
        return UnityEditor.EditorUtility.SaveFilePanel(title, directory, defaultName, extension);
    }
#else
    private string OpenFileDialog(string title, string directory, string extension)
    {
        return NativeFileDialog.OpenFilePanel(title, directory, extension);
    }

    private string SaveFileDialog(string title, string directory, string defaultName, string extension)
    {
        return NativeFileDialog.SaveFilePanel(title, directory, defaultName, extension);
    }
#endif
}
