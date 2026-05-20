using UnityEngine;

/// <summary>
/// 记录实键删除，支持撤销/重做。
/// Execute：删除键；Undo：重新创建键。
/// 每次执行后自动刷新相邻原子的光晕状态。
/// </summary>
public class DeleteBondCommand : ICommand
{
    private DashedBondManager dashedBondManager;
    private GameObject atom1;
    private GameObject atom2;
    private int bondType;

    public DeleteBondCommand(DashedBondManager manager, GameObject bond)
    {
        this.dashedBondManager = manager;

        if (bond != null)
        {
            DashedBondLink link = bond.GetComponent<DashedBondLink>();
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (link != null)
            {
                atom1 = link.linkedAtom;
                bondType = link.bondType;
            }
            if (pb != null)
                atom2 = pb.OtherLinkedAtom;
        }
    }

    public void Execute()
    {
        if (atom1 != null && atom2 != null && dashedBondManager != null)
        {
            // 安全检查：确认键仍然存在
            if (dashedBondManager.FindBondBetweenAtoms(atom1, atom2) != null)
            {
                dashedBondManager.DeleteBondBetweenAtoms(atom1, atom2);
                RefreshGlow();
            }
        }
    }

    public void Undo()
    {
        if (atom1 != null && atom2 != null && dashedBondManager != null)
        {
            // 安全检查：确认键尚未被重建（避免重复创建）
            if (dashedBondManager.FindBondBetweenAtoms(atom1, atom2) == null)
            {
                dashedBondManager.CreateBond(atom1, atom2, bondType);
                RefreshGlow();
            }
        }
    }

    public bool IsValid()
    {
        return atom1 != null && atom2 != null && dashedBondManager != null;
    }

    private void RefreshGlow()
    {
        if (dashedBondManager?.AtomMgr == null) return;
        if (atom1 != null) dashedBondManager.AtomMgr.UpdateAtomGlow(atom1);
        if (atom2 != null) dashedBondManager.AtomMgr.UpdateAtomGlow(atom2);
    }
}
