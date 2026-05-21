using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 管理所有原子的创建、高亮、光晕和销毁。
/// 同时维护原子与光晕 GameObject 的映射。
/// </summary>
public class AtomManager : MonoBehaviour
{
    public List<GameObject> atoms = new List<GameObject>();

    [SerializeField] private MaterialManager materialManager;
    [SerializeField] private DashedBondManager dashedBondManager;
    [SerializeField] private HistoryManager historyManager;

    [SerializeField] private int atomCount = 1;
    private Dictionary<GameObject, GameObject> atomToParentMap = new Dictionary<GameObject, GameObject>();
    private Dictionary<GameObject, GameObject> atomToGlowMap = new Dictionary<GameObject, GameObject>();

    // 预分配碰撞检测数组，避免 Physics.OverlapSphere 导致 TLS 堆栈内存泄漏
    private Collider[] overlapSphereBuffer = new Collider[16];

    private void Awake()
    {
        if (materialManager == null)
            materialManager = FindObjectOfType<MaterialManager>();
        if (materialManager == null)
            Debug.LogError("[AtomManager] MaterialManager 未找到，请在 Inspector 中手动绑定！");
    }

    public bool CheckAtomOverlap(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position, 0.5f, overlapSphereBuffer);
        for (int i = 0; i < count; i++)
        {
            if (overlapSphereBuffer[i].CompareTag("Atom"))
                return true;
        }
        return false;
    }

    public GameObject CreateAtom(Vector3 position, Element element)
    {
        Debug.Log($"尝试创建 {element.name} 原子，位置: {position}");

        if (IsPositionOccupied(position, element.radius))
        {
            Debug.LogError($"创建失败：位置 {position} 已被占用（元素半径: {element.radius}）");
            return null;
        }

        GameObject atom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        atom.tag = "Atom";
        atom.name = element.name;
        atom.transform.position = position;
        atom.transform.localScale = Vector3.one * element.radius;

        GameObject parent = new GameObject("Atom" + atomCount);
        parent.transform.position = position;
        atom.transform.SetParent(parent.transform);
        atom.transform.localPosition = Vector3.zero;

        atomToParentMap.Add(atom, parent);
        atomCount++;

        if (materialManager == null)
        {
            Debug.LogError($"[{nameof(AtomManager)}] materialManager 为空，无法获取 {element.name} 的材质！");
            Destroy(atom);
            Destroy(parent);
            return null;
        }

        Material mat = materialManager.GetElementMaterial(element.name);
        if (mat != null)
            atom.GetComponent<Renderer>().material = mat;
        else
            Debug.LogError($"未找到元素 {element.name} 的材质");

        Rigidbody rb = atom.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        AtomData data = atom.AddComponent<AtomData>();
        data.element = element;
        data.usedBonds = 0;

        atoms.Add(atom);
        Debug.Log($"原子 {element.name} 创建成功，ID: {atom.GetInstanceID()}");

        if (dashedBondManager != null)
        {
            // 注意：不要在 CreateAtom 中自动转换虚键为实键
            // 因为 CreateAtom 可能被 CreateAtomCommand 调用，
            // 而键的创建应该由 Command 统一记录到历史
            // 虚键转实键的逻辑改由 InputHandler 在创建原子后显式调用
        }

        return atom;
    }

    public void DeleteAtom(GameObject atom)
    {
        if (atom == null) return;

        // 1. 清理光晕
        HideGlowHalo(atom);

        // 2. 清理与原子相连的键（先实键转虚键，再清理虚键）
        if (dashedBondManager != null)
        {
            // 将实键转换回虚键
            dashedBondManager.DeletePreservedBondsForAtom(atom);

            // 清理该原子的虚键
            dashedBondManager.ClearDashedBondsForAtom(atom);
        }

        // 3. 从列表和映射中移除，并销毁
        if (atomToParentMap.ContainsKey(atom))
        {
            GameObject parent = atomToParentMap[atom];
            atoms.Remove(atom);
            Destroy(parent);
            atomToParentMap.Remove(atom);
        }
        else
        {
            atoms.Remove(atom);
            Destroy(atom);
        }
    }

    /// <summary>
    /// 立即销毁所有原子并清空内部列表（用于场景加载前清空，避免 Destroy 延迟导致重叠检测失败）
    /// </summary>
    public void ClearAllImmediate()
    {
        // 立即销毁所有光晕
        foreach (var kv in atomToGlowMap)
        {
            if (kv.Value != null)
                DestroyImmediate(kv.Value);
        }
        atomToGlowMap.Clear();

        // 立即销毁所有原子（通过 parent）
        foreach (var atom in atoms)
        {
            if (atom == null) continue;
            if (atomToParentMap.TryGetValue(atom, out var parent))
            {
                if (parent != null)
                    DestroyImmediate(parent); // 销毁 parent 会自动销毁 child atom
            }
            else
            {
                DestroyImmediate(atom);
            }
        }

        atoms.Clear();
        atomToParentMap.Clear();
    }

    /// <summary>
    /// 获取当前所有原子（供 SaveManager 存档用）
    /// </summary>
    public List<GameObject> GetAllAtoms() => atoms;

    public GameObject GetParentObject(GameObject atom)
    {
        return atomToParentMap.ContainsKey(atom) ? atomToParentMap[atom] : null;
    }

    public void HighlightAtom(GameObject atom)
    {
        if (atom == null) return;
        if (materialManager == null || materialManager.highlightedMaterial == null)
        {
            Debug.LogWarning("[HighlightAtom] materialManager 或 highlightedMaterial 为空，跳过高亮");
            return;
        }
        atom.GetComponent<Renderer>().material = materialManager.highlightedMaterial;
    }

    public void ResetAtomMaterial(GameObject atom)
    {
        if (atom == null) return;
        if (materialManager == null)
        {
            Debug.LogWarning("[ResetAtomMaterial] materialManager 为空，跳过材质重置");
            return;
        }
        Material mat = materialManager.GetElementMaterial(atom.name);
        if (mat != null)
            atom.GetComponent<Renderer>().material = mat;

        // 取消选中时检查：键未连全则显示红色光晕
        UpdateAtomGlow(atom);
    }

    /// <summary>
    /// 根据原子的键连接情况更新光晕信号。
    /// 键没连全（有虚键/未满足的键位）→ 显示红色光晕壳；键已连全 → 移除光晕壳。
    /// </summary>
    public void UpdateAtomGlow(GameObject atom)
    {
        if (atom == null) return;

        AtomData atomData = atom.GetComponent<AtomData>();
        if (atomData == null || atomData.element == null) return;

        bool hasUnsatisfiedBonds = atomData.usedBonds < atomData.element.maxBondCount;

        if (hasUnsatisfiedBonds)
            ShowGlowHalo(atom);
        else
            HideGlowHalo(atom);
    }

    /// <summary>
    /// 在原子外显示红色光晕壳
    /// </summary>
    private void ShowGlowHalo(GameObject atom)
    {
        // 已有光晕则不重复创建
        if (atomToGlowMap.ContainsKey(atom) && atomToGlowMap[atom] != null)
            return;

        Material glowMat = materialManager != null ? materialManager.glowHaloMaterial : null;
        if (glowMat == null)
        {
            Debug.LogWarning("GlowHalo 材质未配置，请在 MaterialManager 中设置 glowHaloMaterial");
            return;
        }

        // 创建光晕球体，比原子稍大
        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        glow.name = "GlowHalo";
        glow.tag = "Untagged";

        // 移除碰撞体，光晕不需要物理交互
        Collider col = glow.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // 光晕比原子大 15%
        float glowScale = 1.15f;
        glow.transform.localScale = Vector3.one * glowScale;
        glow.transform.SetParent(atom.transform);
        glow.transform.localPosition = Vector3.zero;

        Renderer glowRenderer = glow.GetComponent<Renderer>();
        glowRenderer.material = glowMat;

        atomToGlowMap[atom] = glow;
    }

    /// <summary>
    /// 移除原子的光晕壳
    /// </summary>
    private void HideGlowHalo(GameObject atom)
    {
        if (atomToGlowMap.TryGetValue(atom, out GameObject glow) && glow != null)
        {
            Destroy(glow);
        }
        atomToGlowMap.Remove(atom);
    }

    /// <summary>
    /// 刷新所有原子的光晕状态（用于撤销等批量操作后）
    /// </summary>
    public void RefreshAllAtomGlows()
    {
        foreach (GameObject atom in atoms)
        {
            if (atom != null)
                UpdateAtomGlow(atom);
        }
    }

    private bool IsPositionOccupied(Vector3 position, float radius)
    {
        // 预分配数组，避免 Physics.OverlapSphere 导致 TLS 堆栈内存泄漏
        int count = Physics.OverlapSphereNonAlloc(position, radius * 0.5f, overlapSphereBuffer);
        for (int i = 0; i < count; i++)
        {
            if (overlapSphereBuffer[i].CompareTag("Atom"))
                return true;
        }
        return false;
    }

    public Element GetElementFromAtom(GameObject atom)
    {
        if (atom == null) return null;

        AtomData atomData = atom.GetComponent<AtomData>();
        if (atomData != null && atomData.element != null)
            return atomData.element;

        // 兜底：如果 AtomData 不存在，才使用名称匹配（兼容旧逻辑）
        Debug.LogWarning($"[AtomManager] 原子 {atom.name} 缺少 AtomData 组件，使用名称匹配");
        return atom.name switch
        {
            "Hydrogen" => Element.Hydrogen,
            "Lithium" => Element.Lithium,
            "Carbon" => Element.Carbon,
            "Nitrogen" => Element.Nitrogen,
            "Oxygen" => Element.Oxygen,
            "Fluorine" => Element.Fluorine,
            "Sodium" => Element.Sodium,
            "Magnesium" => Element.Magnesium,
            "Aluminum" => Element.Aluminum,
            "Silicon" => Element.Silicon,
            "Phosphorus" => Element.Phosphorus,
            "Sulfur" => Element.Sulfur,
            "Chlorine" => Element.Chlorine,
            "Potassium" => Element.Potassium,
            "Calcium" => Element.Calcium,
            _ => throw new System.ArgumentException($"未知原子名称: {atom.name}")
        };
    }
}
