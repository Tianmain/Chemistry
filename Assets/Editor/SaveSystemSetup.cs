using UnityEngine;
using UnityEditor;

/// <summary>
/// 一键配置化学编辑器存档系统
/// Unity 菜单 → Chemistry / Setup Save System
/// 只确保 SaveManager 存在并绑定 MaterialManager
/// SavePanel 和按钮请在场景中手动创建
/// </summary>
public class SaveSystemSetup : EditorWindow
{
    private const string MENU_PATH = "Chemistry/Setup Save System";

    [MenuItem(MENU_PATH)]
    static void Setup()
    {
        // ── 1. SaveManager ─────────────────────────────
        GameObject saveMgrObj = GameObject.Find("SaveManager");
        if (saveMgrObj == null)
        {
            saveMgrObj = new GameObject("SaveManager");
            Undo.RegisterCreatedObjectUndo(saveMgrObj, "Create SaveManager");
        }
        var saveManager = saveMgrObj.GetComponent<SaveManager>();
        if (saveManager == null)
            saveManager = saveMgrObj.AddComponent<SaveManager>();

        MaterialManager mm = FindMaterialManager();
        if (mm != null)
        {
            SerializedObject so = new SerializedObject(saveManager);
            SerializedProperty prop = so.FindProperty("materialManager");
            if (prop != null)
            {
                prop.objectReferenceValue = mm;
                so.ApplyModifiedProperties();
            }
            Debug.Log("[Setup] SaveManager.materialManager 已绑定");
        }
        else
        {
            Debug.LogWarning("[Setup] 未找到 MaterialManager.asset，请手动绑定");
        }

        Debug.Log("[Setup] 完成！SaveManager 已就绪。SavePanel 和按钮请手动创建。");
        Selection.activeGameObject = saveMgrObj;
    }

    static MaterialManager FindMaterialManager()
    {
        string[] guids = AssetDatabase.FindAssets("t:MaterialManager");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<MaterialManager>(path);
        }
        return null;
    }
}
