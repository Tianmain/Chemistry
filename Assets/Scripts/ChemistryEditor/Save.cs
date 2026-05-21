#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 场景存档面板：打开 / 保存 / 另存为
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
    private void OnOpenClicked()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[SavePanel] SaveManager.Instance 为空");
            return;
        }

        string path = EditorUtility.OpenFilePanel(
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
            //Debug.Log($"[SavePanel] Opened: {Path.GetFileName(path)} / 已打开：{Path.GetFileName(path)}");
        }
        else
        {
            Debug.LogError($"[SavePanel] Failed to open: {Path.GetFileName(path)} / 打开失败：{Path.GetFileName(path)}");
        }
    }

    private void OnSaveClicked()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[SavePanel] SaveManager.Instance 为空");
            return;
        }

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            OnSaveAsClicked();
            return;
        }

        bool ok = SaveManager.Instance.SaveSceneToPath(_currentFilePath, atomManager, dashedBondManager);
        //Debug.Log(ok
        //    ? $"[SavePanel] Saved: {Path.GetFileName(_currentFilePath)} / 已保存：{Path.GetFileName(_currentFilePath)}"
        //    : $"[SavePanel] Failed to save: {Path.GetFileName(_currentFilePath)} / 保存失败：{Path.GetFileName(_currentFilePath)}");
    }

    private void OnSaveAsClicked()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[SavePanel] SaveManager.Instance 为空");
            return;
        }

        string directory = string.IsNullOrEmpty(_currentFilePath)
            ? SaveManager.Instance.GetSaveDirectory()
            : Path.GetDirectoryName(_currentFilePath);

        string defaultName = string.IsNullOrEmpty(_currentFilePath)
            ? "scene.json"
            : Path.GetFileName(_currentFilePath);

        string path = EditorUtility.SaveFilePanel(
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
            //Debug.Log($"[SavePanel] Saved as: {Path.GetFileName(path)} / 另存为成功：{Path.GetFileName(path)}");
        }
        else
        {
            Debug.LogError($"[SavePanel] Failed to save as: {Path.GetFileName(path)} / 另存为失败：{Path.GetFileName(path)}");
        }
    }
}
#endif
