using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 管理分子结构存档的保存/加载/删除
/// 所有存档为 JSON 文件，存储在 Saves 目录
/// </summary>
public class SaveManager : MonoBehaviour
{
    private static SaveManager _instance;
    public static SaveManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<SaveManager>();
            return _instance;
        }
    }

    [SerializeField] private MaterialManager materialManager;

    // 存档文件目录
    public string GetSaveDirectory()
    {
#if UNITY_EDITOR
        string projectSaves = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Saves")).TrimEnd('\\', '/');
        if (!Directory.Exists(projectSaves))
            Directory.CreateDirectory(projectSaves);
        return projectSaves;
#else
        string path = Path.Combine(Application.persistentDataPath, "ChemistrySaves");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
#endif
    }
    // 从任意本地路径加载场景
    public bool LoadSceneFromPath(string fullPath, AtomManager atomManager, DashedBondManager bondManager)
    {
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            Debug.LogError($"[SaveManager] 文件不存在: {fullPath}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            MoleculeSaveData data = JsonUtility.FromJson<MoleculeSaveData>(json);
            if (data == null)
            {
                Debug.LogError($"[SaveManager] JSON 解析失败: {Path.GetFileName(fullPath)}");
                return false;
            }

            if (historyManager != null) historyManager.Clear();
            ApplySaveData(data, atomManager, bondManager);
            //Debug.Log($"[SaveManager] 已加载: {Path.GetFileName(fullPath)} ({data.atoms.Count} 个原子, {data.bonds.Count} 条键)");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 加载失败: {e.Message}");
            return false;
        }
    }

    // 保存场景到任意本地路径
    public bool SaveSceneToPath(string fullPath, AtomManager atomManager, DashedBondManager bondManager)
    {
        if (atomManager == null || bondManager == null)
        {
            Debug.LogError("[SaveManager] atomManager 或 bondManager 为空");
            return false;
        }

        try
        {
            MoleculeSaveData data = BuildSaveData(atomManager, bondManager);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(fullPath, json);
            //Debug.Log($"[SaveManager] 已保存: {Path.GetFileName(fullPath)} ({data.atoms.Count} 个原子, {data.bonds.Count} 条键)");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 保存失败: {e.Message}");
            return false;
        }
    }

    private HistoryManager historyManager;
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (materialManager == null)
            materialManager = Resources.FindObjectsOfTypeAll<MaterialManager>().FirstOrDefault();

        historyManager = FindObjectOfType<HistoryManager>();

        // 触发目录创建
        string _ = GetSaveDirectory();
    }

    // 获取所有存档文件名
    public List<string> GetSaveFileNames()
    {
        string dir = GetSaveDirectory();
        if (!Directory.Exists(dir))
            return new List<string>();

        var files = Directory.GetFiles(dir, "*.json");
        var names = new List<string>();
        foreach (var f in files)
            names.Add(Path.GetFileNameWithoutExtension(f));

        var sorted = names
            .Select(n => new { Name = n, Path = GetFullPath(n) })
            .OrderByDescending(x => File.GetLastWriteTime(x.Path))
            .Select(x => x.Name)
            .ToList();
        return sorted;
    }

    // 保存当前场景到指定文件名
    public bool SaveScene(string fileName, AtomManager atomManager, DashedBondManager bondManager)
    {
        if (atomManager == null || bondManager == null)
        {
            Debug.LogError("SaveScene: atomManager 或 bondManager 为空");
            return false;
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Debug.LogError("SaveScene: 文件名为空");
            return false;
        }

        try
        {
            MoleculeSaveData data = BuildSaveData(atomManager, bondManager);
            string json = JsonUtility.ToJson(data, true);
            string path = GetFullPath(fileName);
            File.WriteAllText(path, json);
            //Debug.Log($"场景已保存: {fileName} ({data.atoms.Count} 个原子, {data.bonds.Count} 条键)");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存失败: {e.Message}");
            return false;
        }
    }

    // 从指定文件加载场景，清空当前场景
    public bool LoadScene(string fileName, AtomManager atomManager, DashedBondManager bondManager)
    {
        if (atomManager == null || bondManager == null)
        {
            Debug.LogError("LoadScene: atomManager 或 bondManager 为空");
            return false;
        }

        string path = GetFullPath(fileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"加载失败: 文件不存在 - {fileName}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            MoleculeSaveData data = JsonUtility.FromJson<MoleculeSaveData>(json);
            if (data == null)
            {
                Debug.LogError($"加载失败: JSON 解析错误 - {fileName}");
                return false;
            }

            if (historyManager != null) historyManager.Clear();
            ApplySaveData(data, atomManager, bondManager);
            //Debug.Log($"场景已加载: {fileName} ({data.atoms.Count} 个原子, {data.bonds.Count} 条键)");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"加载失败: {e.Message}");
            return false;
        }
    }

    // 删除指定存档文件
    //public bool DeleteSave(string fileName)
    //{
    //    string path = GetFullPath(fileName);
    //    if (!File.Exists(path))
    //        return false;

    //    try
    //    {
    //        File.Delete(path);
    //        Debug.Log($"存档已删除: {fileName}");
    //        return true;
    //    }
    //    catch (System.Exception e)
    //    {
    //        Debug.LogError($"删除失败: {e.Message}");
    //        return false;
    //    }
    //}

    // 检查指定文件名是否已存在
    public bool FileExists(string fileName)
    {
        return File.Exists(GetFullPath(fileName));
    }

    // 获取完整的文件路径
    public string GetFullPath(string fileName)
    {
        string name = fileName.Trim();
        if (!name.EndsWith(".json"))
            name += ".json";
        return Path.Combine(GetSaveDirectory(), name);
    }

    // 构建存档数据
    private MoleculeSaveData BuildSaveData(AtomManager atomManager, DashedBondManager bondManager)
    {
        MoleculeSaveData data = new MoleculeSaveData();
        data.saveTimeTicks = System.DateTime.UtcNow.Ticks;

        var atomList = atomManager.GetAllAtoms();
        var atomToIndex = new Dictionary<GameObject, int>();

        for (int i = 0; i < atomList.Count; i++)
        {
            GameObject atom = atomList[i];
            if (atom == null) continue;

            atomToIndex[atom] = i;

            AtomData atomData = atom.GetComponent<AtomData>();
            if (atomData == null || atomData.element == null) continue;

            data.atoms.Add(new AtomSaveEntry
            {
                elementName = atomData.element.name,
                posX = atom.transform.position.x,
                posY = atom.transform.position.y,
                posZ = atom.transform.position.z,
                usedBonds = atomData.usedBonds
            });
        }

        var bonds = bondManager.GetAllPreservedBonds();
        foreach (var bond in bonds)
        {
            if (bond == null) continue;

            DashedBondLink link = bond.GetComponent<DashedBondLink>();
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (link == null || pb == null) continue;
            if (link.linkedAtom == null || pb.OtherLinkedAtom == null) continue;

            if (!atomToIndex.TryGetValue(link.linkedAtom, out int idxA)) continue;
            if (!atomToIndex.TryGetValue(pb.OtherLinkedAtom, out int idxB)) continue;

            data.bonds.Add(new BondSaveEntry
            {
                atomIndexA = idxA,
                atomIndexB = idxB,
                bondType = link.bondType
            });
        }

        return data;
    }

    // 私有方法：应用存档数据
    private void ApplySaveData(MoleculeSaveData data, AtomManager atomManager, DashedBondManager bondManager)
    {
        // 先清空场景
        ClearCurrentScene(atomManager, bondManager);

        if (materialManager == null)
        {
            Debug.LogError("ApplySaveData: materialManager 为空，无法加载元素");
            return;
        }

        var createdAtoms = new List<GameObject>();

        foreach (var entry in data.atoms)
        {
            Element elem = materialManager.GetElement(entry.elementName);
            if (elem == null)
            {
                Debug.LogWarning($"加载警告: 找不到元素 {entry.elementName}，跳过");
                createdAtoms.Add(null);
                continue;
            }

            GameObject atom = atomManager.CreateAtom(
                new Vector3(entry.posX, entry.posY, entry.posZ),
                elem);
            createdAtoms.Add(atom);
        }

        foreach (var entry in data.bonds)
        {
            if (entry.atomIndexA < 0 || entry.atomIndexA >= createdAtoms.Count) continue;
            if (entry.atomIndexB < 0 || entry.atomIndexB >= createdAtoms.Count) continue;

            GameObject a = createdAtoms[entry.atomIndexA];
            GameObject b = createdAtoms[entry.atomIndexB];
            if (a == null || b == null) continue;

            bondManager.CreateBond(a, b, entry.bondType);
        }

        // 加载完成后，显式重建索引
        bondManager.RebuildAtomToPreservedBondsIndex();
    }

    // 清空当前场景所有原子和键
    private void ClearCurrentScene(AtomManager atomManager, DashedBondManager bondManager)
    {
        if (bondManager != null)
            bondManager.ClearAllBondsImmediate();
        if (atomManager != null)
            atomManager.ClearAllImmediate();
    }
}
