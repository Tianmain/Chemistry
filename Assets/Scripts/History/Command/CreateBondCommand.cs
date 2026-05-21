using UnityEngine;

/// <summary>
/// 记录实键创建，支持撤销/重做
/// Execute：创建键；Undo：删除键
/// 每次执行后自动刷新相邻原子的光晕状态
/// </summary>
public class CreateBondCommand : ICommand
{
    private DashedBondManager dashedBondManager;
    private GameObject atom1;
    private GameObject atom2;
    private int bondType;

    public CreateBondCommand(DashedBondManager manager, GameObject atom1, GameObject atom2, int bondType)
    {
        this.dashedBondManager = manager;
        this.atom1 = atom1;
        this.atom2 = atom2;
        this.bondType = bondType;
    }

    public void Execute()
    {
        if (atom1 != null && atom2 != null && dashedBondManager != null)
        {
            dashedBondManager.CreateBond(atom1, atom2, bondType);
            RefreshGlow();
        }
    }

    public void Undo()
    {
        if (atom1 != null && atom2 != null && dashedBondManager != null)
        {
            // 确认键仍然存在
            if (dashedBondManager.FindBondBetweenAtoms(atom1, atom2) != null)
            {
                dashedBondManager.DeleteBondBetweenAtoms(atom1, atom2);
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
