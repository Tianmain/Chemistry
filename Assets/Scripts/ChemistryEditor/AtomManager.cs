using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        // 清理光晕
        HideGlowHalo(atom);

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

    public GameObject GetParentObject(GameObject atom)
    {
        return atomToParentMap.ContainsKey(atom) ? atomToParentMap[atom] : null;
    }

    public void HighlightAtom(GameObject atom)
    {
        if (atom == null) return;
        atom.GetComponent<Renderer>().material = materialManager.highlightedMaterial;
    }

    public void ResetAtomMaterial(GameObject atom)
    {
        if (atom == null) return;
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
