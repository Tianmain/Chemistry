using UnityEngine;
using System.Diagnostics;

/// <summary>
/// 实键组件，记录键两端原子及键类型，管理双向键引用
/// </summary>
public class PreservedBond : MonoBehaviour
{
    private DashedBondManager manager;
    public GameObject OriginalLinkedAtom { get; private set; }
    public Vector3 OriginalEndPosition { get; private set; }
    public GameObject OtherLinkedAtom { get; private set; }
    public GameObject reverseBond;

    public int bondType;

    public void Initialize(DashedBondManager manager, DashedBondLink link, GameObject otherAtom = null)
    {
        this.manager = manager;
        gameObject.tag = "PreservedBond";
        OriginalLinkedAtom = link.linkedAtom;
        OriginalEndPosition = link.endPosition;
        this.bondType = link.bondType;
        OtherLinkedAtom = otherAtom;
    }

    private void OnDisable()
    {
        if (this == null || gameObject == null) return;
    }

    private void OnDestroy()
    {
        if (reverseBond != null)
        {
            manager.DeletePreservedBond(reverseBond);
            reverseBond = null;
        }
    }
}
