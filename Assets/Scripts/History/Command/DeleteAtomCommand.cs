using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 记录原子删除，支持撤销/重做
/// Execute：删除原子及相连键；Undo：重建原子并恢复键
/// </summary>
public class DeleteAtomCommand : ICommand
{
    private AtomManager atomManager;
    private DashedBondManager dashedBondManager;
    private HistoryManager historyManager;
    private GameObject targetAtom;
    private Element element;
    private Vector3 position;
    private int usedBonds;

    public DeleteAtomCommand(AtomManager manager, DashedBondManager dashedBondManager, HistoryManager historyManager, GameObject atom)
    {
        this.atomManager = manager;
        this.dashedBondManager = dashedBondManager;
        this.historyManager = historyManager;
        this.targetAtom = atom;

        if (atom != null)
        {
            this.element = manager.GetElementFromAtom(atom);
            this.position = atom.transform.position;

            AtomData atomData = atom.GetComponent<AtomData>();
            this.usedBonds = atomData != null ? atomData.usedBonds : 0;
        }
    }

    // 删除原子
    public void Execute()
    {
        // 先收集受影响的相邻原子
        List<GameObject> affectedNeighbors = dashedBondManager.DeletePreservedBondsForAtom(targetAtom);
        dashedBondManager.ClearDashedBondsForAtom(targetAtom);
        atomManager.DeleteAtom(targetAtom);

        // 更新相邻原子的光晕状态
        foreach (GameObject neighbor in affectedNeighbors)
        {
            if (neighbor != null)
                atomManager.UpdateAtomGlow(neighbor);
        }
    }

    // 重建原子
    public void Undo()
    {
        if (element == null)
        {
            Debug.LogError("无法撤销：元素信息丢失");
            return;
        }

        targetAtom = atomManager.CreateAtom(position, element);

        if (targetAtom != null)
        {
            AtomData atomData = targetAtom.GetComponent<AtomData>();
            if (atomData != null)
            {
                atomData.usedBonds = usedBonds;
            }

            // 只为恢复的原子生成虚键并自动转换，不影响其他原子的虚键
            dashedBondManager.RefreshForRestoredAtom(targetAtom, usedBonds, element.maxBondCount);
        }

        // 撤销删除后，刷新所有原子的光晕状态
        atomManager.RefreshAllAtomGlows();
    }

    // 检查命令是否有效
    public bool IsValid()
    {
        return targetAtom != null && element != null;
    }
}
