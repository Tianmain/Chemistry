using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;

/// <summary>
/// 管理虚键和实键的创建、转换、销毁和存档序列化。
/// 负责虚键自动生成、自动转实键、以及键生命周期管理。
/// </summary>
public class DashedBondManager : MonoBehaviour
{
    [SerializeField] private MaterialManager materialManager;
    [SerializeField] private AtomManager atomManager;
    [SerializeField] private HistoryManager historyManager;

    // 供 Command 类访问 AtomManager
    public AtomManager AtomMgr => atomManager;

    private List<GameObject> dashedBonds = new List<GameObject>();
    private const float FixedBondLength = 1f;
    private const float PositionCheckRadius = 0.5f;

    private Dictionary<GameObject, List<GameObject>> atomToDashedBonds = new Dictionary<GameObject, List<GameObject>>();

    private List<GameObject> dashedBondsPool = new List<GameObject>();
    private HashSet<GameObject> activeBonds = new HashSet<GameObject>();

    // 预分配碰撞检测数组
    private Collider[] overlapSphereBuffer = new Collider[16];

    private float lastCleanupTime = 0f;
    private const float CleanupInterval = 5f;

    public List<GameObject> preservedBonds = new List<GameObject>();
    [SerializeField] public Material preservedBondMaterial;

    // 原子相连实键索引
    private Dictionary<GameObject, List<GameObject>> atomToPreservedBonds =
        new Dictionary<GameObject, List<GameObject>>();

    // 供 SaveManager 获取所有实键
    public List<GameObject> GetAllPreservedBonds() => preservedBonds;

    // 将键加入原子相连实键索引字典
    private void AddBondToAtomIndex(GameObject bond)
    {
        if (bond == null) return;
        PreservedBond pb = bond.GetComponent<PreservedBond>();
        if (pb == null) return;

        GameObject a1 = pb.OriginalLinkedAtom;
        GameObject a2 = pb.OtherLinkedAtom;

        if (a1 != null)
        {
            if (!atomToPreservedBonds.ContainsKey(a1))
                atomToPreservedBonds[a1] = new List<GameObject>();
            if (!atomToPreservedBonds[a1].Contains(bond))
                atomToPreservedBonds[a1].Add(bond);
        }

        if (a2 != null)
        {
            if (!atomToPreservedBonds.ContainsKey(a2))
                atomToPreservedBonds[a2] = new List<GameObject>();
            if (!atomToPreservedBonds[a2].Contains(bond))
                atomToPreservedBonds[a2].Add(bond);
        }
    }

    // 将键从原子相连实键索引字典中移除
    private void RemoveBondFromAtomIndex(GameObject bond)
    {
        if (bond == null) return;
        PreservedBond pb = bond.GetComponent<PreservedBond>();
        if (pb == null) return;

        GameObject a1 = pb.OriginalLinkedAtom;
        GameObject a2 = pb.OtherLinkedAtom;

        if (a1 != null && atomToPreservedBonds.ContainsKey(a1))
        {
            atomToPreservedBonds[a1].Remove(bond);
            if (atomToPreservedBonds[a1].Count == 0)
                atomToPreservedBonds.Remove(a1);
        }

        if (a2 != null && atomToPreservedBonds.ContainsKey(a2))
        {
            atomToPreservedBonds[a2].Remove(bond);
            if (atomToPreservedBonds[a2].Count == 0)
                atomToPreservedBonds.Remove(a2);
        }
    }

    // 获取与指定原子相连的所有原子
    public List<GameObject> GetConnectedAtoms(GameObject startAtom)
    {
        if (startAtom == null) return new List<GameObject>();

        List<GameObject> connected = new List<GameObject>();
        Queue<GameObject> queue = new Queue<GameObject>();
        HashSet<GameObject> visited = new HashSet<GameObject>();

        queue.Enqueue(startAtom);
        visited.Add(startAtom);

        while (queue.Count > 0)
        {
            GameObject current = queue.Dequeue();
            connected.Add(current);

            if (!atomToPreservedBonds.ContainsKey(current))
                continue;

            foreach (var bond in atomToPreservedBonds[current])
            {
                if (bond == null) continue;
                PreservedBond pb = bond.GetComponent<PreservedBond>();
                if (pb == null) continue;

                GameObject neighbor = null;
                if (pb.OriginalLinkedAtom == current)
                    neighbor = pb.OtherLinkedAtom;
                else if (pb.OtherLinkedAtom == current)
                    neighbor = pb.OriginalLinkedAtom;

                if (neighbor != null && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return connected;
    }

    // 获取与指定原子相连的所有原子
    public HashSet<GameObject> GetConnectedAtomsHashSet(GameObject startAtom)
    {
        var list = GetConnectedAtoms(startAtom);
        var set = new HashSet<GameObject>(list);
        return set;
    }

    // 获取与 startAtom 相连的原子堆，排除 excludeAtom 所在的那一侧。
    // 用于在指定键处"切开"分子，获取旋转端原子堆。
    public HashSet<GameObject> GetConnectedAtomsExcluding(GameObject startAtom, GameObject excludeAtom)
    {
        var visited = new HashSet<GameObject>();
        if (startAtom == null) return visited;

        var queue = new Queue<GameObject>();
        queue.Enqueue(startAtom);
        visited.Add(startAtom);

        while (queue.Count > 0)
        {
            GameObject current = queue.Dequeue();

            if (!atomToPreservedBonds.ContainsKey(current))
                continue;

            foreach (var bond in atomToPreservedBonds[current])
            {
                if (bond == null) continue;
                PreservedBond pb = bond.GetComponent<PreservedBond>();
                if (pb == null) continue;

                GameObject neighbor = null;
                if (pb.OriginalLinkedAtom == current)
                    neighbor = pb.OtherLinkedAtom;
                else if (pb.OtherLinkedAtom == current)
                    neighbor = pb.OriginalLinkedAtom;

                //  excludeAtom 时，不向另一侧扩散
                if (neighbor != null && neighbor != excludeAtom && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited;
    }

    // 更新所有与指定原子相连的实键的 Transform。
    public void UpdateBondsForAtom(GameObject atom)
    {
        if (atom == null) return;

        foreach (var bond in preservedBonds)
        {
            if (bond == null) continue;
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (pb == null) continue;

            // 只更新与指定原子相连的键
            if (pb.OriginalLinkedAtom != atom && pb.OtherLinkedAtom != atom)
                continue;

            // 更新键的 Transform
            GameObject atom1 = pb.OriginalLinkedAtom;
            GameObject atom2 = pb.OtherLinkedAtom;

            if (atom1 == null || atom2 == null) continue;

            Vector3 start = atom1.transform.position;
            Vector3 end = atom2.transform.position;
            Vector3 direction = (end - start).normalized;
            float length = Vector3.Distance(start, end);

            bond.transform.position = start + direction * length * 0.5f;
            bond.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            bond.transform.localScale = new Vector3(0.1f, length / 2, 0.1f);
        }
    }

    // 更新所有实键的 Transform
    public void UpdateAllBondTransforms()
    {
        foreach (var bond in preservedBonds)
        {
            if (bond == null) continue;
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (pb == null) continue;

            GameObject atom1 = pb.OriginalLinkedAtom;
            GameObject atom2 = pb.OtherLinkedAtom;

            if (atom1 == null || atom2 == null) continue;

            Vector3 start = atom1.transform.position;
            Vector3 end = atom2.transform.position;
            Vector3 direction = (end - start).normalized;
            float length = Vector3.Distance(start, end);

            bond.transform.position = start + direction * length * 0.5f;
            bond.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            bond.transform.localScale = new Vector3(0.1f, length / 2, 0.1f);
        }
    }

    public GameObject selectedDashedBond;

    // 检查当前选中的键是否为实键
    public bool IsSelectedBondPreserved()
    {
        return selectedDashedBond != null && selectedDashedBond.CompareTag("PreservedBond");
    }

    private GameObject lastSelectedAtom;
    private int lastSelectedBondType;
    private int lastMaxBondCount;
    private GameObject currentDashedBondAtom;

    // 刷新所有原子的虚键显示，为所有有实键连接且键未连全的原子生成虚键。
    public void RefreshAllDashedBonds(GameObject highlightedAtom = null, int highlightedBondType = 1)
    {
        // 先清除所有现有虚键
        ClearDashedBonds();

        if (atomManager == null || atomManager.atoms == null) return;

        // 收集所有有实键连接的原子
        HashSet<GameObject> atomsWithBonds = new HashSet<GameObject>();
        foreach (var bond in preservedBonds)
        {
            if (bond == null) continue;
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (pb == null) continue;
            if (pb.OriginalLinkedAtom != null) atomsWithBonds.Add(pb.OriginalLinkedAtom);
            if (pb.OtherLinkedAtom != null) atomsWithBonds.Add(pb.OtherLinkedAtom);
        }

        // 选中的原子也需要显示虚键
        if (highlightedAtom != null)
            atomsWithBonds.Add(highlightedAtom);

        // 为每个需要显示虚键且键未连全的原子生成虚键
        foreach (GameObject atom in atomsWithBonds)
        {
            if (atom == null) continue;

            AtomData data = atom.GetComponent<AtomData>();
            if (data == null || data.element == null) continue;

            int remaining = data.element.maxBondCount - data.usedBonds;
            if (remaining <= 0) continue;

            // 选中的原子使用用户指定的键类型，其他原子默认单键
            int bondType = (atom == highlightedAtom) ? highlightedBondType : 1;
            GenerateDashedBondsFor(atom, bondType, data.element.maxBondCount);
        }

        // 检查新虚键末端是否有原子
        CheckAndConvertDashedBondsToPreserved();
    }

    public GameObject GetFirstDashedBond()
    {
        return dashedBonds.Count > 0 ? dashedBonds[0] : null;
    }

    private void Update()
    {
        if (Time.time - lastCleanupTime > CleanupInterval)
        {
            CleanupInvalidObjects();
            lastCleanupTime = Time.time;
        }
    }

    private void CleanupInvalidObjects()
    {
        dashedBondsPool.RemoveAll(b => b == null);
        dashedBonds.RemoveAll(b => b == null);
        activeBonds.RemoveWhere(b => b == null);
        preservedBonds.RemoveAll(b => b == null);
    }

    public void UpdateDashedBonds(GameObject selectedAtom, int selectedBondType, int maxBondCount)
    {
        lastSelectedAtom = selectedAtom;
        lastSelectedBondType = selectedBondType;
        lastMaxBondCount = maxBondCount;

        UpdateDashedBondsInternal(selectedAtom, selectedBondType, maxBondCount);
        CheckAndConvertDashedBondsToPreserved();
    }

    // 只生成虚键，不触发自动转换为实键。
    private void UpdateDashedBondsInternal(GameObject selectedAtom, int selectedBondType, int maxBondCount)
    {
        ClearDashedBonds();
        if (selectedAtom == null) return;
        GenerateDashedBondsFor(selectedAtom, selectedBondType, maxBondCount);
    }

    // 只为指定原子生成虚键，不清除全局虚键。
    private void UpdateDashedBondsLocal(GameObject atom, int bondType, int maxBondCount)
    {
        if (atom == null) return;
        GenerateDashedBondsFor(atom, bondType, maxBondCount);
    }

    // 为指定原子生成虚键，排除已有实键占用的方向
    private void GenerateDashedBondsFor(GameObject selectedAtom, int selectedBondType, int maxBondCount)
    {
        currentDashedBondAtom = selectedAtom;

        Vector3 position = selectedAtom.transform.position;

        AtomData atomData = selectedAtom.GetComponent<AtomData>();
        int usedBonds = (atomData != null) ? atomData.usedBonds : 0;
        int remainingSlots = maxBondCount - usedBonds;

        //Debug.Log($"[UpdateDashedBonds] 原子:{selectedAtom.name}, 键类型(selectedBondType):{selectedBondType}, " +
        //          $"maxBondCount:{maxBondCount}, usedBonds:{usedBonds}, remainingSlots:{remainingSlots}");

        if (remainingSlots <= 0) return;

        float bondAngle = (atomData != null && atomData.element != null)
            ? atomData.element.bondAngle
            : 109.5f;

        List<Vector3> occupiedDirections = GetOccupiedDirections(selectedAtom);
        int occupiedCount = occupiedDirections.Count;

        //Debug.Log($"[GenerateDashedBondsFor] {selectedAtom.name} bondAngle:{bondAngle}°, " +
        //          $"已占用方向数:{occupiedCount}, 需要生成:{remainingSlots}");

        switch (maxBondCount)
        {
            case 4:
                HandleMaxBond4Excluding(position, selectedBondType, remainingSlots, occupiedDirections, bondAngle);
                break;
            case 3:
                HandleMaxBond3Excluding(position, selectedBondType, remainingSlots, occupiedDirections, bondAngle);
                break;
            case 2:
                HandleMaxBond2Excluding(position, selectedBondType, remainingSlots, occupiedDirections, bondAngle);
                break;
            case 1:
                CreateDashedLine(position, Vector3.up, 1);
                break;
        }
    }

    // 获取指定原子已有实键占用的方向列表
    private List<Vector3> GetOccupiedDirections(GameObject atom)
    {
        List<Vector3> dirs = new List<Vector3>();
        if (atom == null) return dirs;

        Vector3 atomPos = atom.transform.position;

        if (!atomToPreservedBonds.ContainsKey(atom))
            return dirs;

        foreach (var bond in atomToPreservedBonds[atom])
        {
            if (bond == null) continue;
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (pb == null) continue;

            GameObject otherAtom = null;
            if (pb.OriginalLinkedAtom == atom)
                otherAtom = pb.OtherLinkedAtom;
            else if (pb.OtherLinkedAtom == atom)
                otherAtom = pb.OriginalLinkedAtom;

            if (otherAtom != null)
            {
                Vector3 dir = (otherAtom.transform.position - atomPos).normalized;
                dirs.Add(dir);
            }
        }

        return dirs;
    }

    private bool IsDirectionOccupied(Vector3 direction, List<Vector3> occupiedDirections, float threshold = 30f)
    {
        foreach (var occDir in occupiedDirections)
        {
            if (Vector3.Angle(direction, occDir) < threshold)
                return true;
        }
        return false;
    }

    private List<Vector3> FilterUnoccupiedDirections(Vector3[] candidates, List<Vector3> occupiedDirections, int maxCount)
    {
        List<Vector3> result = new List<Vector3>();
        foreach (var dir in candidates)
        {
            if (result.Count >= maxCount) break;
            if (!IsDirectionOccupied(dir, occupiedDirections))
                result.Add(dir);
        }
        return result;
    }

    // 检查虚键末端是否有原子，如果有则自动转换为实键
    public void CheckAndConvertDashedBondsToPreserved(bool shouldRefreshGlow = false)
    {
        // 先收集需要转换的虚键信息，避免遍历时修改集合
        var bondsToConvert = new List<(GameObject dashedBond, GameObject endAtom)>();

        foreach (var dashedBond in activeBonds.ToList())
        {
            if (dashedBond == null || !dashedBond.activeSelf) continue;

            DashedBondLink link = dashedBond.GetComponent<DashedBondLink>();
            if (link == null) continue;

            if (HasAtomAtPosition(link.endPosition))
            {
                GameObject endAtom = FindAtomAtPosition(link.endPosition);
                if (endAtom != null && endAtom != link.linkedAtom)
                {
                    bondsToConvert.Add((dashedBond, endAtom));
                }
            }
        }

        foreach (var (dashedBond, endAtom) in bondsToConvert)
        {
            AutoConvertToPreservedBond(dashedBond, endAtom);
        }

        RefreshDashedBondsForAffectedAtoms(bondsToConvert);

        // 只有在真正创建/恢复原子时才刷新光晕
        if (shouldRefreshGlow)
        {
            RefreshGlowForAffectedAtoms(bondsToConvert);
        }
    }

    private GameObject FindAtomAtPosition(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position, PositionCheckRadius, overlapSphereBuffer);
        for (int i = 0; i < count; i++)
        {
            var collider = overlapSphereBuffer[i];
            if (collider.CompareTag("Atom") && collider.gameObject.GetComponent<AtomData>() != null)
            {
                return collider.gameObject;
            }
        }
        return null;
    }

    private void AutoConvertToPreservedBond(GameObject dashedBond, GameObject endAtom)
    {
        if (dashedBond == null) return;

        DashedBondLink link = dashedBond.GetComponent<DashedBondLink>();
        if (link == null) return;

        GameObject startAtom = link.linkedAtom;
        int bondType = link.bondType;

        // 检查两个原子的键位是否都充足，任一不足则拒绝创建
        if (!HasEnoughBondSlots(startAtom, bondType) || !HasEnoughBondSlots(endAtom, bondType))
        {
            Debug.LogWarning($"[AutoConvert] 键位不足，取消转换: {startAtom?.name}-{endAtom?.name}, 键类型:{bondType}");
            // 清除这个无效的虚键
            dashedBond.SetActive(false);
            activeBonds.Remove(dashedBond);
            dashedBonds.Remove(dashedBond);
            return;
        }

        dashedBonds.Remove(dashedBond);
        activeBonds.Remove(dashedBond);
        dashedBond.SetActive(false);

        // 通过 Command 系统记录历史
        if (historyManager != null)
        {
            var command = new CreateBondCommand(this, startAtom, endAtom, bondType);
            historyManager.ExecuteCommand(command);
            //Debug.Log($"自动转换虚键为实键（已记录历史）: {startAtom.name} - {endAtom.name}, 键类型: {bondType}");
        }
        else
        {
            // 直接创建
            GameObject preservedBond = CreateAutoPreservedBond(startAtom, endAtom, bondType);
            if (preservedBond != null)
            {
                UpdateAtomBondCount(startAtom, endAtom, bondType);
                //Debug.Log($"自动转换虚键为实键: {startAtom.name} - {endAtom.name}, 键类型: {bondType}");
            }
        }
    }

    /// <summary>
    /// 检查原子是否有足够的键位容纳指定类型的键
    /// </summary>
    private bool HasEnoughBondSlots(GameObject atom, int bondType)
    {
        if (atom == null) return false;
        AtomData data = atom.GetComponent<AtomData>();
        if (data == null || data.element == null) return false;
        int remaining = data.element.maxBondCount - data.usedBonds;
        return remaining >= bondType;
    }

    private GameObject CreateAutoPreservedBond(GameObject startAtom, GameObject endAtom, int bondType)
    {
        Vector3 start = startAtom.transform.position;
        Vector3 end = endAtom.transform.position;

        // 防止重复创建：检查这对原子之间是否已存在实键
        foreach (var existingBond in preservedBonds)
        {
            if (existingBond == null) continue;
            DashedBondLink existLink = existingBond.GetComponent<DashedBondLink>();
            if (existLink == null) continue;

            bool samePair = (existLink.linkedAtom == startAtom && existLink.linkedAtom != null &&
                             Vector3.Distance(existLink.endPosition, end) < 0.5f) ||
                            (existLink.linkedAtom == endAtom && existLink.linkedAtom != null &&
                             Vector3.Distance(existLink.endPosition, start) < 0.5f);
            if (samePair)
            {
                Debug.LogWarning($"[CreateAutoPreservedBond] 跳过重复键: {startAtom.name}-{endAtom.name} (已存在)");
                return null;
            }
        }

        // 从对象池中查找匹配的隐藏虚键
        GameObject bond = null;
        foreach (var b in dashedBondsPool)
        {
            if (b == null || b.activeSelf) continue;
            if (b.CompareTag("PreservedBond"))
                continue;
            DashedBondLink poolLink = b.GetComponent<DashedBondLink>();
            if (poolLink == null) continue;
            if (Vector3.Distance(poolLink.endPosition, end) < 0.1f)
            {
                bond = b;
                break;
            }
        }

        if (bond == null)
        {
            bond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dashedBondsPool.Add(bond);
            bond.AddComponent<DashedBondLink>();
            bond.AddComponent<PreservedBond>();
        }
        else
        {
            // 复用时完全重置旧数据
            DashedBondLink oldLink = bond.GetComponent<DashedBondLink>();
            if (oldLink != null)
            {
                oldLink.linkedAtom = null;
                oldLink.endPosition = Vector3.zero;
                oldLink.bondType = 0;
            }
        }

        // 彻底重置对象状态
        bond.transform.SetParent(null);
        bond.layer = 0;
        bond.tag = "PreservedBond";
        bond.SetActive(false);
        bond.SetActive(true);

        Renderer renderer = bond.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
#if UNITY_2021_1_OR_NEWER
            renderer.forceRenderingOff = false;
#endif
        }

        Vector3 direction = (end - start).normalized;
        float length = Vector3.Distance(start, end);

        bond.transform.position = start + direction * length * 0.5f;
        bond.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        bond.transform.localScale = new Vector3(0.1f, length / 2, 0.1f);

        if (renderer != null && materialManager != null)
        {
            switch (bondType)
            {
                case 1: renderer.material = materialManager.singleBondMaterial; break;
                case 2: renderer.material = materialManager.doubleBondMaterial; break;
                case 3: renderer.material = materialManager.tripleBondMaterial; break;
            }
        }

        string parentName = bond.transform.parent != null ? bond.transform.parent.gameObject.name : "null";
        //Debug.Log("[CreateAutoPreservedBond] 创建完成: " + startAtom.name + "-" + endAtom.name + ", 类型:" + bondType +
        //          ", 位置:" + bond.transform.position +
        //          ", 旋转:" + bond.transform.rotation.eulerAngles +
        //          ", 缩放:" + bond.transform.localScale +
        //          ", 激活:" + bond.activeInHierarchy + ", 层级:" + bond.layer + ", 父对象:" + parentName +
        //          ", RendererEnabled:" + (renderer?.enabled) + ", 材质:" + (renderer?.material?.name) +
        //          ", InstanceID:" + bond.GetInstanceID() + ", 池中总数:" + dashedBondsPool.Count);

        DashedBondLink link = bond.GetComponent<DashedBondLink>();
        link.linkedAtom = startAtom;
        link.endPosition = end;
        link.bondType = bondType;

        PreservedBond preserved = bond.GetComponent<PreservedBond>();
        if (preserved == null)
        {
            preserved = bond.AddComponent<PreservedBond>();
        }
        preserved.Initialize(this, link, endAtom);

        preservedBonds.Add(bond);
        AddBondToAtomIndex(bond);
        RemoveDuplicatePreservedBond(bond, startAtom, endAtom);

        return bond;
    }

    // 如果同一原子对之间已存在实键，删除旧的，保留新建的
    private void RemoveDuplicatePreservedBond(GameObject newBond, GameObject startAtom, GameObject endAtom)
    {
        Vector3 newStart = startAtom.transform.position;
        Vector3 newEnd = endAtom.transform.position;

        for (int i = preservedBonds.Count - 1; i >= 0; i--)
        {
            GameObject existing = preservedBonds[i];
            if (existing == null || existing == newBond) continue;

            DashedBondLink existLink = existing.GetComponent<DashedBondLink>();
            PreservedBond existPB = existing.GetComponent<PreservedBond>();
            if (existLink == null || existPB == null) continue;
            if (existLink.linkedAtom == null) continue;

            bool samePair =
                (existLink.linkedAtom == startAtom && existPB.OtherLinkedAtom == endAtom) ||
                (existLink.linkedAtom == endAtom && existPB.OtherLinkedAtom == startAtom);

            if (!samePair) continue;

            Debug.LogWarning($"[RemoveDuplicate] 检测到同一位置重复实键! 删除旧键: " +
                           $"{existLink.linkedAtom.name}-{existPB.OtherLinkedAtom?.name}, " +
                           $"InstanceID:{existing.GetInstanceID()}, 保留新键 InstanceID:{newBond.GetInstanceID()}");

            if (existPB.reverseBond != null)
            {
                PreservedBond reversePB = existPB.reverseBond.GetComponent<PreservedBond>();
                if (reversePB != null)
                    reversePB.reverseBond = null;
            }

            RemoveBondFromAtomIndex(existing);

            preservedBonds.RemoveAt(i);
            Destroy(existing);
        }
    }

    /// 遍历所有实键，每对原子只保留一根
    public void RemoveAllDuplicatePreservedBonds()
    {
        HashSet<string> seenPairs = new HashSet<string>();

        for (int i = preservedBonds.Count - 1; i >= 0; i--)
        {
            GameObject bond = preservedBonds[i];
            if (bond == null) continue;

            DashedBondLink link = bond.GetComponent<DashedBondLink>();
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (link == null || pb == null) continue;

            int id1 = link.linkedAtom != null ? link.linkedAtom.GetInstanceID() : -1;
            int id2 = pb.OtherLinkedAtom != null ? pb.OtherLinkedAtom.GetInstanceID() : -1;

            // 保证 A→B 和 B→A 生成相同的 key
            int minId = Mathf.Min(id1, id2);
            int maxId = Mathf.Max(id1, id2);
            string pairKey = $"{minId}_{maxId}";

            if (seenPairs.Contains(pairKey))
            {
                Debug.LogWarning($"[RemoveAllDuplicates] 删除重复实键: " +
                               $"{link.linkedAtom?.name}-{pb.OtherLinkedAtom?.name}, InstanceID:{bond.GetInstanceID()}");

                if (pb.reverseBond != null)
                {
                    PreservedBond reversePB = pb.reverseBond.GetComponent<PreservedBond>();
                    if (reversePB != null)
                        reversePB.reverseBond = null;
                }

                preservedBonds.RemoveAt(i);
                Destroy(bond);
            }
            else
            {
                seenPairs.Add(pairKey);
            }
        }
    }

    // 基于位置的去重
    private void RemoveDuplicateByPosition(GameObject linkedAtom, Vector3 endPosition)
    {
        for (int i = preservedBonds.Count - 1; i >= 0; i--)
        {
            GameObject existing = preservedBonds[i];
            if (existing == null) continue;

            DashedBondLink existLink = existing.GetComponent<DashedBondLink>();
            PreservedBond existPB = existing.GetComponent<PreservedBond>();
            if (existLink == null || existPB == null) continue;

            if (existLink.linkedAtom == linkedAtom &&
                Vector3.Distance(existPB.OriginalEndPosition, endPosition) < 0.5f)
            {
                Debug.LogWarning($"[RemoveDuplicateByPosition] 删除重复实键: " +
                               $"{existLink.linkedAtom?.name}→endPos({endPosition.x:F1},{endPosition.y:F1},{endPosition.z:F1}), " +
                               $"InstanceID:{existing.GetInstanceID()}");

                if (existPB.reverseBond != null)
                {
                    PreservedBond reversePB = existPB.reverseBond.GetComponent<PreservedBond>();
                    if (reversePB != null)
                        reversePB.reverseBond = null;
                }

                preservedBonds.RemoveAt(i);
                Destroy(existing);
            }
        }
    }

    // 撤销删除原子时的局部刷新：只为恢复的原子生成虚键并检查自动转换，不清除全局虚键
    public void RefreshForRestoredAtom(GameObject restoredAtom, int usedBonds, int maxBondCount)
    {
        if (restoredAtom == null) return;

        ClearDashedBondsForAtom(restoredAtom);

        currentDashedBondAtom = restoredAtom;
        Vector3 position = restoredAtom.transform.position;
        int remainingSlots = maxBondCount - usedBonds;

        if (remainingSlots <= 0) return;

        switch (maxBondCount)
        {
            case 4: HandleMaxBond4(position, 1, remainingSlots); break;
            case 3: HandleMaxBond3(position, 1, remainingSlots); break;
            case 2: HandleMaxBond2(position, 1, remainingSlots); break;
            case 1: HandleMaxBond1(position, 1); break;
        }

        var bondsToConvert = new List<(GameObject dashedBond, GameObject endAtom)>();
        foreach (var dashedBond in activeBonds.ToList())
        {
            if (dashedBond == null || !dashedBond.activeSelf) continue;
            DashedBondLink link = dashedBond.GetComponent<DashedBondLink>();
            if (link == null || link.linkedAtom != restoredAtom) continue;

            if (HasAtomAtPosition(link.endPosition))
            {
                GameObject endAtom = FindAtomAtPosition(link.endPosition);
                if (endAtom != null && endAtom != restoredAtom)
                    bondsToConvert.Add((dashedBond, endAtom));
            }
        }

        foreach (var (dashedBond, endAtom) in bondsToConvert)
        {
            AutoConvertToPreservedBond(dashedBond, endAtom);
        }

        RefreshDashedBondsForAffectedAtoms(bondsToConvert);

        // 恢复原子后，立即检查受影响原子的光晕状态
        RefreshGlowForAffectedAtoms(bondsToConvert);
    }

    // 更新两个原子的 usedBonds，不刷新虚键显示
    private void UpdateAtomBondCount(GameObject atom1, GameObject atom2, int bondType)
    {
        AtomData data1 = atom1.GetComponent<AtomData>();
        AtomData data2 = atom2.GetComponent<AtomData>();

        if (data1 != null) data1.usedBonds += bondType;
        if (data2 != null) data2.usedBonds += bondType;
    }

    // 批量转换完成后，统一刷新受影响原子的虚键显示。
    private void RefreshDashedBondsForAffectedAtoms(List<(GameObject dashedBond, GameObject endAtom)> bondsToConvert)
    {
        var affectedAtoms = new HashSet<GameObject>();
        foreach (var (dashedBond, endAtom) in bondsToConvert)
        {
            DashedBondLink link = dashedBond.GetComponent<DashedBondLink>();
            if (link != null && link.linkedAtom != null)
                affectedAtoms.Add(link.linkedAtom);
            if (endAtom != null)
                affectedAtoms.Add(endAtom);
        }

        foreach (var atom in affectedAtoms)
        {
            AtomData data = atom.GetComponent<AtomData>();
            if (data != null && data.element != null)
            {
                ClearDashedBondsForAtom(atom);
                int remaining = data.element.maxBondCount - data.usedBonds;

                if (remaining <= 0) continue;

                UpdateDashedBondsLocal(atom, 1, data.element.maxBondCount);
            }
        }
    }

    // 创建/恢复原子并自动成键后，立即刷新受影响原子的光晕状态。
    private void RefreshGlowForAffectedAtoms(List<(GameObject dashedBond, GameObject endAtom)> bondsToConvert)
    {
        if (atomManager == null) return;

        var affectedAtoms = new HashSet<GameObject>();
        foreach (var (dashedBond, endAtom) in bondsToConvert)
        {
            DashedBondLink link = dashedBond.GetComponent<DashedBondLink>();
            if (link != null && link.linkedAtom != null)
                affectedAtoms.Add(link.linkedAtom);
            if (endAtom != null)
                affectedAtoms.Add(endAtom);
        }

        foreach (var atom in affectedAtoms)
        {
            atomManager.UpdateAtomGlow(atom);
        }
    }

    // 为 maxBondCount=4 的原子生成虚键
    private void HandleMaxBond4Excluding(Vector3 position, int bondType, int remainingSlots,
        List<Vector3> occupiedDirections, float bondAngle)
    {
        int occupiedCount = occupiedDirections.Count;

        // 计算新键数量：1 个选中类型 + 剩余全部用单键补
        int newBondsCount;
        int totalSubstituents;

        if (remainingSlots >= bondType && bondType > 1)
        {
            // 1 个 bondType 类型键 + (remainingSlots - bondType) 个单键
            newBondsCount = 1 + Mathf.Max(0, remainingSlots - bondType);
            totalSubstituents = occupiedCount + newBondsCount;
        }
        else
        {
            // 键位不足，全部放单键
            newBondsCount = remainingSlots;
            totalSubstituents = occupiedCount + remainingSlots;
        }

        // 根据总取代基数量确定正确的键角
        float effectiveAngle;
        if (totalSubstituents >= 4)
            effectiveAngle = 109.5f;       // sp³ 四面体
        else if (totalSubstituents == 3)
            effectiveAngle = 120f;         // sp² 平面三角形
        else
            effectiveAngle = 180f;         // sp  直线

        // 根据总取代基数量生成对应数量的方向
        Vector3[] dirs;
        if (totalSubstituents >= 4 || occupiedCount >= 4)
            dirs = GetTetrahedralDirections(effectiveAngle);
        else if (totalSubstituents == 3 || occupiedCount == 3)
            dirs = GetPlanarDirections(effectiveAngle, 3);
        else
            dirs = GetLinearDirections();

        // 旋转方向组，使其与已有键方向尽量对齐
        if (occupiedCount > 0)
        {
            if (dirs.Length == 4)
                dirs = RotateTetrahedralToAlign(dirs, occupiedDirections);
            else
                dirs = RotateDirectionsToAlign(dirs, occupiedDirections);
        }

        // 过滤掉已被占用方向
        var availableDirs = FilterUnoccupiedDirections(dirs, occupiedDirections, newBondsCount);

        // 为每个方向分配正确的键型：第一个用选中类型，其余用单键
        bool placedSelectedType = false;
        int simulatedRemainingSlots = remainingSlots;

        foreach (var dir in availableDirs)
        {
            int typeForThisBond;
            if (!placedSelectedType && simulatedRemainingSlots >= bondType)
            {
                typeForThisBond = bondType;
                placedSelectedType = true;
                simulatedRemainingSlots -= bondType;
            }
            else
            {
                typeForThisBond = 1;
                simulatedRemainingSlots -= 1;
            }

            CreateDashedLine(position, dir, typeForThisBond);
        }
    }

    /// 为 maxBondCount=3 的原子生成虚键
    private void HandleMaxBond3Excluding(Vector3 position, int bondType, int remainingSlots,
        List<Vector3> occupiedDirections, float bondAngle)
    {
        int occupiedCount = occupiedDirections.Count;
        int newBondsCount;
        int totalSubstituents;

        if (remainingSlots >= bondType && bondType > 1)
        {
            // 1 个选中类型 + 剩余用单键补
            newBondsCount = 1 + Mathf.Max(0, remainingSlots - bondType);
            totalSubstituents = occupiedCount + newBondsCount;
        }
        else
        {
            newBondsCount = remainingSlots;
            totalSubstituents = occupiedCount + remainingSlots;
        }

        // 生成方向
        Vector3[] dirs;

        if (totalSubstituents >= 3 || occupiedCount >= 3)
        {
            // 3 个取代基：三角锥形（sp³ + 孤对电子）
            Vector3[] tetra = GetTetrahedralDirections(bondAngle);

            dirs = new Vector3[] { tetra[1], tetra[2], tetra[3] };
        }
        else if (totalSubstituents == 2 || occupiedCount >= 2)
        {
            // 2 个取代基：V 型或直线型
            if (bondAngle < 179f)
                dirs = GetBentDirections(bondAngle);
            else
                dirs = GetLinearDirections();
        }
        else
        {
            dirs = GetLinearDirections();
        }

        // 旋转对齐
        if (occupiedCount > 0)
        {
            // 对于 3 个方向的情况，用通用旋转
            if (dirs.Length == 3)
                dirs = RotateDirectionsToAlign(dirs, occupiedDirections);
            else
                dirs = RotateDirectionsToAlign(dirs, occupiedDirections);
        }

        // 过滤未占用方向
        var availableDirs = FilterUnoccupiedDirections(dirs, occupiedDirections, newBondsCount);

        // 分配键型：第一个用选中类型，其余用单键
        bool placedSelectedType = false;
        int simulatedRemainingSlots = remainingSlots;

        foreach (var dir in availableDirs)
        {
            int typeForThisBond;
            if (!placedSelectedType && simulatedRemainingSlots >= bondType)
            {
                typeForThisBond = bondType;
                placedSelectedType = true;
                simulatedRemainingSlots -= bondType;
            }
            else
            {
                typeForThisBond = 1;
                simulatedRemainingSlots -= 1;
            }

            CreateDashedLine(position, dir, typeForThisBond);
        }
    }

    // 为 maxBondCount=2 的原子生成虚键
    private void HandleMaxBond2Excluding(Vector3 position, int bondType, int remainingSlots,
        List<Vector3> occupiedDirections, float bondAngle)
    {
        int occupiedCount = occupiedDirections.Count;
        int newBondsCount;
        int totalSubstituents;

        if (remainingSlots >= bondType && bondType > 1)
        {
            // 1 个选中类型 + 剩余用单键补
            newBondsCount = 1 + Mathf.Max(0, remainingSlots - bondType);
            totalSubstituents = occupiedCount + newBondsCount;
        }
        else
        {
            newBondsCount = remainingSlots;
            totalSubstituents = occupiedCount + remainingSlots;
        }

        // 生成方向：有孤对电子且总取代基≥2 → V 型；否则直线型
        Vector3[] dirs;
        if (totalSubstituents >= 2 && bondAngle < 179f)
            dirs = GetBentDirections(bondAngle);
        else
            dirs = GetLinearDirections();

        // 旋转对齐
        if (occupiedCount > 0)
            dirs = RotateDirectionsToAlign(dirs, occupiedDirections);

        // 过滤未占用方向
        var availableDirs = FilterUnoccupiedDirections(dirs, occupiedDirections, newBondsCount);

        // 分配键型：第一个用选中类型，其余用单键
        bool placedSelectedType = false;
        int simulatedRemainingSlots = remainingSlots;

        foreach (var dir in availableDirs)
        {
            int typeForThisBond;
            if (!placedSelectedType && simulatedRemainingSlots >= bondType)
            {
                typeForThisBond = bondType;
                placedSelectedType = true;
                simulatedRemainingSlots -= bondType;
            }
            else
            {
                typeForThisBond = 1;
                simulatedRemainingSlots -= 1;
            }

            CreateDashedLine(position, dir, typeForThisBond);
        }
    }

    /// 生成四面体 4 方向，以 Y 轴为第一方向，其余三个均匀分布在底面圆锥上
    private Vector3[] GetTetrahedralDirections(float bondAngleDeg)
    {
        float theta = bondAngleDeg * Mathf.Deg2Rad;
        Vector3 d0 = Vector3.up;

        // 与 d0 成 bondAngle 角，绕 Y 轴每 120° 分布
        float cosTheta = Mathf.Cos(theta);
        float sinTheta = Mathf.Sin(theta);

        Vector3 d1 = new Vector3(0f, cosTheta, sinTheta).normalized;
        Vector3 d2 = (Quaternion.AngleAxis(120f, Vector3.up) * d1).normalized;
        Vector3 d3 = (Quaternion.AngleAxis(240f, Vector3.up) * d1).normalized;

        return new Vector3[] { d0, d1, d2, d3 };
    }

    /// 生成 N 个平面内均匀分布的方向
    private Vector3[] GetPlanarDirections(float bondAngleDeg, int count)
    {
        Vector3[] dirs = new Vector3[count];
        float stepAngle = 360f / count;
        for (int i = 0; i < count; i++)
        {
            dirs[i] = Quaternion.Euler(0f, i * stepAngle, 0f) * Vector3.forward;
        }
        return dirs;
    }

    // 生成 V 形 2 方向，两方向关于 Y 轴对称
    private Vector3[] GetBentDirections(float bondAngleDeg)
    {
        float half = bondAngleDeg / 2f * Mathf.Deg2Rad;
        Vector3 d0 = new Vector3(Mathf.Sin(half), 0f, Mathf.Cos(half)).normalized;
        Vector3 d1 = new Vector3(-Mathf.Sin(half), 0f, Mathf.Cos(half)).normalized;
        return new Vector3[] { d0, d1 };
    }

    /// 旋转四面体方向组，使其尽量对齐已占用方向
    private Vector3[] RotateTetrahedralToAlign(Vector3[] tetraDirs, List<Vector3> occupiedDirections)
    {
        Vector3 target = occupiedDirections[0].normalized;
        Vector3 source = tetraDirs[0].normalized;
        Quaternion rot = Quaternion.FromToRotation(source, target);

        // 搜索绕 target 轴的最优旋转角度
        float bestScore = float.MinValue;
        Quaternion bestRot = rot;
        for (int angle = 0; angle < 360; angle += 5)
        {
            Quaternion twist = Quaternion.AngleAxis(angle, target);
            Quaternion candidate = twist * rot;
            float score = 0f;
            for (int i = 0; i < tetraDirs.Length; i++)
            {
                Vector3 candidateDir = (candidate * tetraDirs[i]).normalized;
                foreach (var occDir in occupiedDirections)
                {
                    float angleBetween = Vector3.Angle(candidateDir, occDir.normalized);
                    if (angleBetween < 45f)
                        score += (45f - angleBetween);
                }
            }
            if (score > bestScore)
            {
                bestScore = score;
                bestRot = candidate;
            }
        }

        Vector3[] result = new Vector3[tetraDirs.Length];
        for (int i = 0; i < tetraDirs.Length; i++)
        {
            result[i] = (bestRot * tetraDirs[i]).normalized;
        }
        return result;
    }

    // 通用方向旋转对齐：旋转方向组使其整体最接近已占用方向
    private Vector3[] RotateDirectionsToAlign(Vector3[] baseDirs, List<Vector3> occupiedDirections)
    {
        Vector3 target = occupiedDirections[0].normalized;
        Vector3 source = baseDirs[0].normalized;
        Quaternion baseRot = Quaternion.FromToRotation(source, target);
        float bestScore = float.MinValue;
        Quaternion bestRot = baseRot;

        for (int angle = 0; angle < 360; angle += 5)
        {
            Quaternion twist = Quaternion.AngleAxis(angle, target);
            Quaternion candidate = twist * baseRot;
            float score = 0f;
            for (int i = 0; i < baseDirs.Length; i++)
            {
                Vector3 candidateDir = (candidate * baseDirs[i]).normalized;
                foreach (var occDir in occupiedDirections)
                {
                    float angleBetween = Vector3.Angle(candidateDir, occDir.normalized);
                    if (angleBetween < 45f)
                        score += (45f - angleBetween);
                }
            }
            if (score > bestScore)
            {
                bestScore = score;
                bestRot = candidate;
            }
        }

        Vector3[] result = new Vector3[baseDirs.Length];
        for (int i = 0; i < baseDirs.Length; i++)
        {
            result[i] = (bestRot * baseDirs[i]).normalized;
        }
        return result;
    }

    private void HandleMaxBond4(Vector3 position, int bondType, int remainingSlots)
    {
        switch (bondType)
        {
            case 1: CreateTetrahedralDashes(position, bondType); break;
            case 2: CreateDoubleBondWithSlots(position, remainingSlots, isTrigonal: true); break;
            case 3: CreateTripleBondWithSlots(position, remainingSlots, isLinear: true); break;
        }
    }

    private void HandleMaxBond3(Vector3 position, int bondType, int remainingSlots)
    {
        switch (bondType)
        {
            case 1: CreateTrigonalDashes(position, bondType); break;
            case 2: CreateDoubleBondWithSlots(position, remainingSlots, isTrigonal: false); break;
            case 3: CreateTripleBondWithSlots(position, remainingSlots, isLinear: true); break;
        }
    }

    private void HandleMaxBond2(Vector3 position, int bondType, int remainingSlots)
    {
        switch (bondType)
        {
            case 1: CreateLinearDashes(position, bondType); break;
            case 2: CreateDashedLine(position, Vector3.forward, bondType); break;
        }
    }

    private void HandleMaxBond1(Vector3 position, int bondType)
    {
        if (bondType == 1)
            CreateDashedLine(position, Vector3.up, bondType);
    }

    private void CreateTrigonalDashes(Vector3 center, int bondType)
    {
        Vector3[] directions = GetTrigonalDirections();
        foreach (var dir in directions)
            CreateDashedLine(center, dir, bondType);
    }

    private void CreateDoubleBondWithSlots(Vector3 position, int remainingSlots, bool isTrigonal)
    {
        Vector3 mainDir = isTrigonal ? GetTrigonalDirections()[0] : GetLinearDirections()[0];
        CreateDashedLine(position, mainDir, 2);

        Vector3[] singleDirs = isTrigonal ?
            GetTrigonalDirections().Skip(1).Take(remainingSlots).ToArray() :
            GetLinearDirections().Skip(1).Take(remainingSlots).ToArray();

        foreach (var dir in singleDirs)
            CreateDashedLine(position, dir, 1);
    }

    private void CreateTripleBondWithSlots(Vector3 position, int remainingSlots, bool isLinear)
    {
        Vector3 mainDir = isLinear ? GetLinearDirections()[0] : Vector3.forward;
        CreateDashedLine(position, mainDir, 3);

        if (remainingSlots > 0)
            CreateDashedLine(position, isLinear ? GetLinearDirections()[1] : Vector3.back, 1);
    }

    private void CreateTetrahedralDashes(Vector3 center, int bondType)
    {
        Vector3[] directions = new Vector3[]
        {
            Vector3.up,
            new Vector3(0f, -1f/3f, Mathf.Sqrt(8f)/3f).normalized,
            new Vector3(Mathf.Sqrt(6f)/3f, -1f/3f, -Mathf.Sqrt(2f)/3f).normalized,
            new Vector3(-Mathf.Sqrt(6f)/3f, -1f/3f, -Mathf.Sqrt(2f)/3f).normalized
        };

        foreach (var dir in directions)
            CreateDashedLine(center, dir, bondType);
    }

    private Vector3[] GetTrigonalDirections()
    {
        return new Vector3[]
        {
            Quaternion.Euler(0, 0, 0) * Vector3.forward,
            Quaternion.Euler(0, 120, 0) * Vector3.forward,
            Quaternion.Euler(0, 240, 0) * Vector3.forward
        };
    }

    private Vector3[] GetLinearDirections()
    {
        return new Vector3[] { Vector3.right, Vector3.left };
    }

    private void CreateLinearDashes(Vector3 center, int bondType)
    {
        CreateDashedLine(center, Vector3.right, bondType);
        CreateDashedLine(center, Vector3.left, bondType);
    }

    public GameObject CreateDashedBond(Vector3 start, Vector3 direction, int bondType, GameObject parentAtom)
    {
        direction.Normalize();
        Vector3 end = start + direction * FixedBondLength;

        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.tag = "DashedBond";
        cylinder.transform.position = (start + end) * 0.5f;
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        cylinder.transform.localScale = new Vector3(0.1f, FixedBondLength / 2, 0.1f);

        Renderer renderer = cylinder.GetComponent<Renderer>();
        Material bondMat = GetBondMaterial(bondType);
        if (bondMat != null)
            renderer.material = bondMat;

        DashedBondLink link = cylinder.AddComponent<DashedBondLink>();
        link.linkedAtom = parentAtom;
        link.endPosition = end;
        link.bondType = bondType;

        if (atomManager != null && parentAtom != null)
        {
            GameObject parent = atomManager.GetParentObject(parentAtom);
            if (parent != null)
                cylinder.transform.SetParent(parent.transform);
        }

        return cylinder;
    }

    private Material GetBondMaterial(int bondType)
    {
        if (materialManager == null)
        {
            Debug.LogWarning("MaterialManager 未初始化，使用默认材质");
            return null;
        }

        switch (bondType)
        {
            case 1: return materialManager.dashedBondMaterial;
            case 2: return materialManager.doubleBondMaterial;
            case 3: return materialManager.tripleBondMaterial;
            default: return materialManager.dashedBondMaterial;
        }
    }

    private void CreateDashedLine(Vector3 start, Vector3 direction, int bondType)
    {
        direction.Normalize();
        Vector3 end = start + direction * FixedBondLength;

        if (IsPositionBlocked(end)) return;

        // 从对象池中查找可复用的虚键
        GameObject cylinder = null;
        foreach (var b in dashedBondsPool)
        {
            if (b == null) continue;
            if (b.CompareTag("PreservedBond")) continue;

            DashedBondLink link = b.GetComponent<DashedBondLink>();
            if (link == null) continue;

            if (!b.activeSelf &&
                Vector3.Distance(link.endPosition, end) < 0.1f &&
                link.bondType == bondType)
            {
                cylinder = b;
                break;
            }
        }

        DashedBondLink bondLink;
        if (cylinder == null)
        {
            cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.tag = "DashedBond";
            dashedBondsPool.Add(cylinder);
            bondLink = cylinder.AddComponent<DashedBondLink>();
        }
        else
        {
            bondLink = cylinder.GetComponent<DashedBondLink>();
        }

        GameObject linkedAtom = currentDashedBondAtom;

        // 重置数据
        bondLink.linkedAtom = linkedAtom;
        bondLink.endPosition = end;
        bondLink.bondType = bondType;

        if (atomManager != null && linkedAtom != null)
        {
            GameObject parent = atomManager.GetParentObject(linkedAtom);
            if (parent != null) cylinder.transform.SetParent(parent.transform);
        }

        cylinder.transform.position = (start + end) * 0.5f;
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        cylinder.transform.localScale = new Vector3(0.1f, FixedBondLength / 2, 0.1f);

        Renderer renderer = cylinder.GetComponent<Renderer>();
        renderer.material = GetBondMaterial(bondType);

        cylinder.SetActive(true);
        activeBonds.Add(cylinder);
        dashedBonds.Add(cylinder);

        //Debug.Log($"[CreateDashedLine] 虚键生成: linkedAtom={linkedAtom?.name}, 末端={end:F2}, bondType={bondType}, 激活={cylinder.activeInHierarchy}");
    }

    public bool IsPositionBlocked(Vector3 targetPosition)
    {
        foreach (var bond in preservedBonds)
        {
            if (bond == null) continue;
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (pb == null) continue;
            if (Vector3.Distance(pb.OriginalEndPosition, targetPosition) < 0.1f)
                return true;
        }
        return false;
    }

    public void HighlightBond(GameObject bond)
    {
        if (materialManager == null)
        {
            Debug.LogWarning("MaterialManager 未初始化");
            selectedDashedBond = null;
            return;
        }

        foreach (var b in dashedBonds)
        {
            if (b == null) continue;
            DashedBondLink link = b.GetComponent<DashedBondLink>();
            if (link == null) continue;
            Renderer renderer = b.GetComponent<Renderer>();
            if (renderer == null) continue;

            switch (link.bondType)
            {
                case 1: renderer.material = materialManager.dashedBondMaterial; break;
                case 2: renderer.material = materialManager.doubleBondMaterial; break;
                case 3: renderer.material = materialManager.tripleBondMaterial; break;
            }
        }

        foreach (var b in preservedBonds)
        {
            if (b == null) continue;
            DashedBondLink link = b.GetComponent<DashedBondLink>();
            if (link == null) continue;
            Renderer renderer = b.GetComponent<Renderer>();
            if (renderer == null) continue;

            switch (link.bondType)
            {
                case 1: renderer.material = materialManager.singleBondMaterial; break;
                case 2: renderer.material = materialManager.doubleBondMaterial; break;
                case 3: renderer.material = materialManager.tripleBondMaterial; break;
            }
        }

        if (bond != null)
        {
            Renderer renderer = bond.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = materialManager.dashedBondHighlightMaterial;
            selectedDashedBond = bond;
        }
        else
        {
            selectedDashedBond = null;
        }
    }

    public bool HasPreservedBondForAtom(GameObject atom)
    {
        foreach (var bond in preservedBonds)
        {
            if (bond == null) continue;
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (pb != null && pb.OriginalLinkedAtom == atom)
                return true;
        }
        return false;
    }

    public void DeletePreservedBond(GameObject bond)
    {
        //Debug.Log($"[DeletePreservedBond] 被调用! InstanceID:{bond?.GetInstanceID()}, " +
        //          $"名称:{bond?.name}, 活跃:{bond?.activeSelf}, 在preservedBonds中:{preservedBonds.Contains(bond)}");
        if (preservedBonds.Contains(bond))
        {
            // 更新相邻原子的 usedBonds
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            DashedBondLink link = bond.GetComponent<DashedBondLink>();
            if (pb != null && link != null)
            {
                int bondType = link.bondType;
                if (pb.OriginalLinkedAtom != null)
                {
                    AtomData data1 = pb.OriginalLinkedAtom.GetComponent<AtomData>();
                    if (data1 != null)
                        data1.usedBonds = Mathf.Max(0, data1.usedBonds - bondType);
                }
                if (pb.OtherLinkedAtom != null)
                {
                    AtomData data2 = pb.OtherLinkedAtom.GetComponent<AtomData>();
                    if (data2 != null)
                        data2.usedBonds = Mathf.Max(0, data2.usedBonds - bondType);
                }
                
                // 删除实键后，立即刷新相连原子的光晕状态
                if (atomManager != null)
                {
                    if (pb.OriginalLinkedAtom != null) atomManager.UpdateAtomGlow(pb.OriginalLinkedAtom);
                    if (pb.OtherLinkedAtom != null) atomManager.UpdateAtomGlow(pb.OtherLinkedAtom);
                }
            }

            // 断开 reverseBond 引用
            if (pb != null && pb.reverseBond != null)
            {
                PreservedBond reversePB = pb.reverseBond.GetComponent<PreservedBond>();
                if (reversePB != null)
                    reversePB.reverseBond = null;
            }

            RemoveBondFromAtomIndex(bond);

            preservedBonds.Remove(bond);
            Destroy(bond);
            //Debug.Log($"[DeletePreservedBond] 已从列表移除并Destroy, InstanceID:{bond.GetInstanceID()}");
        }
        else
        {
            Debug.LogWarning($"[DeletePreservedBond] 键不在preservedBonds列表中! InstanceID:{bond?.GetInstanceID()}");
        }
    }

    public void RestorePreservedBond(GameObject bond)
    {
        if (preservedBonds.Contains(bond))
        {
            preservedBonds.Remove(bond);
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            DashedBondLink link = bond.GetComponent<DashedBondLink>();

            if (pb != null && link != null)
            {
                link.linkedAtom = pb.OriginalLinkedAtom;
                link.endPosition = pb.OriginalEndPosition;
            }

            Destroy(pb);
            bond.tag = "DashedBond";
            bond.GetComponent<Renderer>().material = materialManager.dashedBondMaterial;
            dashedBonds.Add(bond);
        }
    }

    public void ForceClearAllBonds()
    {
        ClearDashedBonds();
        foreach (var bond in preservedBonds.Where(b => b != null))
            Destroy(bond);
        preservedBonds.Clear();
    }

    // 立即销毁所有键并清空内部列表
    public void ClearAllBondsImmediate()
    {
        // 立即销毁所有虚键
        foreach (var bond in dashedBondsPool)
        {
            if (bond != null)
                DestroyImmediate(bond);
        }
        dashedBondsPool.Clear();
        dashedBonds.Clear();
        activeBonds.Clear();
        atomToDashedBonds.Clear();

        // 立即销毁所有实键
        foreach (var bond in preservedBonds)
        {
            if (bond != null)
                DestroyImmediate(bond);
        }
        preservedBonds.Clear();
        atomToPreservedBonds.Clear();
    }

    // 重建atomToPreservedBonds索引字典
    public void RebuildAtomToPreservedBondsIndex()
    {
        atomToPreservedBonds.Clear();

        foreach (var bond in preservedBonds)
        {
            if (bond == null) continue;
            AddBondToAtomIndex(bond);
        }

        //Debug.Log($"[DashedBondManager] 重建索引完成，共 {atomToPreservedBonds.Count} 个原子有相连实键");
    }

    public void ClearDashedBondsForAtom(GameObject atom)
    {
        foreach (var bond in dashedBonds.Where(b =>
            b != null &&
            b.GetComponent<DashedBondLink>()?.linkedAtom == atom))
        {
            // 跳过已被转换为 PreservedBond 的对象
            if (bond.CompareTag("PreservedBond"))
            {
                Debug.LogWarning($"[ClearDashedBondsForAtom] 跳过 PreservedBond: {bond.name} (InstanceID:{bond.GetInstanceID()}), 该对象不应在 dashedBonds 中！");
                continue;
            }
            bond.SetActive(false);
            activeBonds.Remove(bond);
        }
        dashedBonds.RemoveAll(b => b.GetComponent<DashedBondLink>()?.linkedAtom == atom);
    }

    public void UnhighlightDashedBondsForAtom(GameObject atom)
    {
        if (atomToDashedBonds.ContainsKey(atom))
        {
            foreach (var bond in atomToDashedBonds[atom])
            {
                if (bond != null)
                    bond.GetComponent<Renderer>().material = materialManager.dashedBondMaterial;
            }
            atomToDashedBonds.Remove(atom);
        }
    }

    public void ClearDashedBonds()
    {
        foreach (var bond in activeBonds)
        {
            if (bond != null)
            {
                // 跳过已转换为 PreservedBond 的对象
                if (bond.CompareTag("PreservedBond"))
                {
                    Debug.LogWarning($"[ClearDashedBonds] 跳过 PreservedBond: {bond.name} (InstanceID:{bond.GetInstanceID()}), 该对象不应在 activeBonds 中！");
                    continue;
                }
                bond.SetActive(false);
            }
        }
        activeBonds.Clear();
        atomToDashedBonds.Clear();
        selectedDashedBond = null;
    }

    // 清除虚键并重置缓存
    public void ClearDashedBondsAndResetCache()
    {
        ClearDashedBonds();
        lastSelectedAtom = null;
    }

    public void ClearAllPreservedBonds()
    {
        foreach (var bond in preservedBonds.Where(b => b != null))
            Destroy(bond);
        preservedBonds.Clear();
    }

    // 删除与原子关联的所有实键，同时减少关联原子的 usedBonds
    // 如果原子被删除，相连的实键会变成虚键
    public List<GameObject> DeletePreservedBondsForAtom(GameObject atom)
    {
        List<GameObject> affectedNeighbors = new List<GameObject>();
        Vector3 atomPosition = atom != null ? atom.transform.position : Vector3.zero;

        foreach (var bond in preservedBonds.ToList())
        {
            if (bond == null) continue;
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (pb == null) continue;

            bool isLinked = (pb.OriginalLinkedAtom == atom || pb.OtherLinkedAtom == atom);
            if (!isLinked) continue;

            GameObject otherAtom = (pb.OriginalLinkedAtom == atom) ? pb.OtherLinkedAtom : pb.OriginalLinkedAtom;
            if (otherAtom != null)
            {
                AtomData otherData = otherAtom.GetComponent<AtomData>();
                if (otherData != null)
                    otherData.usedBonds = Mathf.Max(0, otherData.usedBonds - pb.bondType);

                affectedNeighbors.Add(otherAtom);
            }

            // 将实键转换回虚键，而不是销毁
            ConvertToDashedBond(bond, otherAtom, atomPosition);
        }

        preservedBonds.RemoveAll(b => b == null);
        dashedBonds.RemoveAll(b => b == null);

        return affectedNeighbors;
    }

    // 将实键转换回虚键
    private void ConvertToDashedBond(GameObject bond, GameObject remainingAtom, Vector3 endPosition)
    {
        if (bond == null) return;

        PreservedBond pb = bond.GetComponent<PreservedBond>();
        DashedBondLink link = bond.GetComponent<DashedBondLink>();
        Renderer renderer = bond.GetComponent<Renderer>();

        if (link == null) return;

        // 如果 remainingAtom 为 null，销毁键
        if (remainingAtom == null)
        {
            if (pb != null && pb.reverseBond != null)
                Destroy(pb.reverseBond);
            Destroy(bond);
            return;
        }

        // 存储 reverseBond 引用
        GameObject reverseBond = (pb != null) ? pb.reverseBond : null;

        // 更新 DashedBondLink
        link.linkedAtom = remainingAtom;
        link.endPosition = endPosition;

        // 移除 PreservedBond 组件
        if (pb != null)
            Destroy(pb);

        // 更新 tag
        bond.tag = "DashedBond";
        bond.SetActive(false);
        bond.SetActive(true);

        // 更新材质为虚键材质
        if (renderer != null && materialManager != null)
        {
            switch (link.bondType)
            {
                case 1: renderer.material = materialManager.dashedBondMaterial; break;
                case 2: renderer.material = materialManager.doubleBondMaterial; break;
                case 3: renderer.material = materialManager.tripleBondMaterial; break;
            }
        }

        // 更新位置
        UpdateDashedBondTransform(bond);

        // 维护原子→键索引字典
        RemoveBondFromAtomIndex(bond);

        // 更新列表
        preservedBonds.Remove(bond);
        if (!dashedBonds.Contains(bond))
            dashedBonds.Add(bond);
        activeBonds.Add(bond);

        // 处理 reverse bond
        if (reverseBond != null)
        {
            Destroy(reverseBond);
        }
    }

    /// <summary>
    /// 更新虚键的 Transform，使其正确显示
    /// </summary>
    private void UpdateDashedBondTransform(GameObject dashedBond)
    {
        DashedBondLink link = dashedBond.GetComponent<DashedBondLink>();
        if (link == null || link.linkedAtom == null) return;

        Vector3 start = link.linkedAtom.transform.position;
        Vector3 end = link.endPosition;
        Vector3 direction = (end - start).normalized;
        float length = Vector3.Distance(start, end);

        dashedBond.transform.position = start + direction * length * 0.5f;
        dashedBond.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        dashedBond.transform.localScale = new Vector3(0.1f, length / 2, 0.1f);
    }

    public void UnselectAllBonds()
    {
        if (materialManager == null)
        {
            Debug.LogWarning("MaterialManager 未初始化");
            selectedDashedBond = null;
            return;
        }

        foreach (var bond in dashedBonds.Where(b => b != null))
        {
            DashedBondLink link = bond.GetComponent<DashedBondLink>();
            if (link != null)
            {
                Renderer renderer = bond.GetComponent<Renderer>();
                if (renderer == null) continue;

                switch (link.bondType)
                {
                    case 1: renderer.material = materialManager.dashedBondMaterial; break;
                    case 2: renderer.material = materialManager.doubleBondMaterial; break;
                    case 3: renderer.material = materialManager.tripleBondMaterial; break;
                }
            }
        }

        foreach (var bond in preservedBonds.Where(b => b != null))
        {
            DashedBondLink link = bond.GetComponent<DashedBondLink>();
            if (link != null)
            {
                Renderer renderer = bond.GetComponent<Renderer>();
                if (renderer == null) continue;

                switch (link.bondType)
                {
                    case 1: renderer.material = materialManager.singleBondMaterial; break;
                    case 2: renderer.material = materialManager.doubleBondMaterial; break;
                    case 3: renderer.material = materialManager.tripleBondMaterial; break;
                }
            }
        }

        selectedDashedBond = null;
    }

    public void PreserveBondManually(GameObject linkedAtom, Vector3 endPosition, int bondType)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.tag = "PreservedBond";

        // 确保可见性：断开父对象、设置正确 layer、刷新激活状态
        cylinder.transform.SetParent(null);
        cylinder.layer = 0;
        cylinder.SetActive(false);
        cylinder.SetActive(true);

        Vector3 start = linkedAtom.transform.position;
        Vector3 direction = (endPosition - start).normalized;
        float length = Vector3.Distance(start, endPosition);

        cylinder.transform.position = start + direction * length * 0.5f;
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        cylinder.transform.localScale = new Vector3(0.1f, length / 2, 0.1f);

        Renderer renderer = cylinder.GetComponent<Renderer>();
        renderer.enabled = true;
#if UNITY_2021_1_OR_NEWER
        renderer.forceRenderingOff = false;
#endif
        switch (bondType)
        {
            case 1: renderer.material = materialManager.singleBondMaterial; break;
            case 2: renderer.material = materialManager.doubleBondMaterial; break;
            case 3: renderer.material = materialManager.tripleBondMaterial; break;
        }

        DashedBondLink link = cylinder.AddComponent<DashedBondLink>();
        link.linkedAtom = linkedAtom;
        link.endPosition = endPosition;
        link.bondType = bondType;

        PreservedBond preserved = cylinder.AddComponent<PreservedBond>();
        preserved.Initialize(this, link);

        preservedBonds.Add(cylinder);
        AddBondToAtomIndex(cylinder);
        RemoveDuplicateByPosition(linkedAtom, endPosition);

        foreach (var dashedBond in dashedBonds.ToList())
        {
            if (dashedBond != null &&
                Vector3.Distance(dashedBond.GetComponent<DashedBondLink>().endPosition, endPosition) < 0.1f)
            {
                dashedBonds.Remove(dashedBond);
                activeBonds.Remove(dashedBond);
                dashedBond.SetActive(false);
            }
        }
    }

    public bool HasAtomAtPosition(Vector3 position)
    {
        // 预分配数组，避免 TLS 堆栈内存泄漏
        int count = Physics.OverlapSphereNonAlloc(position, PositionCheckRadius, overlapSphereBuffer);
        for (int i = 0; i < count; i++)
        {
            if (overlapSphereBuffer[i].CompareTag("Atom"))
                return true;
        }
        return false;
    }

    public string GetDebugInfo()
    {
        StringBuilder sb = new StringBuilder();

        if (atomManager != null && atomManager.atoms != null)
        {
            sb.AppendLine($"=== 原子总数: {atomManager.atoms.Count} ===");
            for (int i = 0; i < atomManager.atoms.Count; i++)
            {
                GameObject atom = atomManager.atoms[i];
                if (atom == null) continue;

                Vector3 pos = atom.transform.position;
                AtomData data = atom.GetComponent<AtomData>();
                int usedBonds = data?.usedBonds ?? 0;
                int maxBonds = data?.element?.maxBondCount ?? 0;

                sb.AppendLine($"原子{i + 1}: {atom.name}({data?.element?.symbol}) | 坐标:({pos.x:F2},{pos.y:F2},{pos.z:F2}) | 键数:{usedBonds}/{maxBonds}(剩{maxBonds - usedBonds})");
            }
        }

        if (preservedBonds != null && preservedBonds.Count > 0)
        {
            sb.AppendLine($"\n=== 实键(PreservedBond): {preservedBonds.Count}根 ===");
            for (int i = 0; i < preservedBonds.Count; i++)
            {
                GameObject bond = preservedBonds[i];
                if (bond == null) continue;

                DashedBondLink link = bond.GetComponent<DashedBondLink>();
                if (link == null) continue;

                PreservedBond pb = bond.GetComponent<PreservedBond>();
                string bondTypeName = GetBondTypeName(link.bondType);

                sb.AppendLine($"  实键{i + 1}: {bondTypeName}");
                sb.AppendLine($"    起始: {(link.linkedAtom != null ? link.linkedAtom.name : "null")} @ ({(link.linkedAtom != null ? $"{link.linkedAtom.transform.position.x:F2},{link.linkedAtom.transform.position.y:F2},{link.linkedAtom.transform.position.z:F2}" : "---")})");
                sb.AppendLine($"    末端: {(pb?.OtherLinkedAtom != null ? pb.OtherLinkedAtom.name : "---")} @ ({link.endPosition.x:F2},{link.endPosition.y:F2},{link.endPosition.z:F2})");
            }
        }

        if (activeBonds != null && activeBonds.Count > 0)
        {
            var activeList = activeBonds.Where(b => b != null && b.activeSelf).ToList();
            sb.AppendLine($"\n=== 活跃虚键(DashedBond): {activeList.Count}根 ===");
            for (int i = 0; i < activeList.Count; i++)
            {
                GameObject dash = activeList[i];
                DashedBondLink link = dash.GetComponent<DashedBondLink>();
                if (link == null) continue;

                sb.AppendLine($"  虚键{i + 1}: {GetBondTypeName(link.bondType)}");
                sb.AppendLine($"    关联: {(link.linkedAtom != null ? link.linkedAtom.name : "null")}");
                sb.AppendLine($"    末端: ({link.endPosition.x:F2},{link.endPosition.y:F2},{link.endPosition.z:F2})");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 程序化创建实键（供 Command 使用）
    /// 创建前检查两个原子的键位是否都充足
    /// </summary>
    public GameObject CreateBond(GameObject atom1, GameObject atom2, int bondType)
    {
        if (atom1 == null || atom2 == null) return null;

        // 检查两个原子的键位是否都充足
        if (!HasEnoughBondSlots(atom1, bondType) || !HasEnoughBondSlots(atom2, bondType))
        {
            Debug.LogWarning($"[CreateBond] 键位不足，取消创建: {atom1.name}-{atom2.name}, 键类型:{bondType}");
            return null;
        }

        // 去重：避免同一对原子重复创建
        GameObject existing = FindBondBetweenAtoms(atom1, atom2);
        if (existing != null)
        {
            Debug.Log($"[CreateBond] 键已存在: {atom1.name}-{atom2.name}");
            return existing;
        }

        GameObject newBond = CreateAutoPreservedBond(atom1, atom2, bondType);

        if (newBond != null)
        {
            // 更新相连原子的 usedBonds 计数
            UpdateAtomBondCount(atom1, atom2, bondType);

            // 创建实键后，立即检测并刷新相连原子的光晕状态
            if (atomManager != null)
            {
                atomManager.UpdateAtomGlow(atom1);
                atomManager.UpdateAtomGlow(atom2);
            }
        }

        return newBond;
    }

    /// <summary>
    /// 查找两个原子之间的实键
    /// </summary>
    public GameObject FindBondBetweenAtoms(GameObject atom1, GameObject atom2)
    {
        if (atom1 == null || atom2 == null) return null;

        foreach (var bond in preservedBonds)
        {
            if (bond == null) continue;
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            DashedBondLink link = bond.GetComponent<DashedBondLink>();
            if (pb == null || link == null) continue;

            bool matches =
                (link.linkedAtom == atom1 && pb.OtherLinkedAtom == atom2) ||
                (link.linkedAtom == atom2 && pb.OtherLinkedAtom == atom1);

            if (matches) return bond;
        }
        return null;
    }

    /// <summary>
    /// 删除两个原子之间的实键（供 Command 使用）
    /// </summary>
    public void DeleteBondBetweenAtoms(GameObject atom1, GameObject atom2)
    {
        GameObject bond = FindBondBetweenAtoms(atom1, atom2);
        if (bond != null)
        {
            DeletePreservedBond(bond);
        }
    }

    public static string GetBondTypeName(int bondType)
    {
        return bondType switch
        {
            1 => "单键",
            2 => "双键",
            3 => "三键",
            _ => $"未知({bondType})"
        };
    }

    /// <summary>
    /// 获取当前活跃虚键的只读副本（供外部调试面板使用）
    /// </summary>
    public List<GameObject> GetActiveBondList()
    {
        return activeBonds.Where(b => b != null && b.activeSelf).ToList();
    }
}
