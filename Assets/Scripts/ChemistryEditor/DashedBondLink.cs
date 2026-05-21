using UnityEngine;

/// <summary>
/// 虚键组件，记录关联原子、末端位置和键类型
/// 当关联原子失效时自动销毁
/// </summary>
public class DashedBondLink : MonoBehaviour
{
    public GameObject linkedAtom;
    public Vector3 endPosition;
    public int bondType;

    public Vector3 Direction => (endPosition - linkedAtom.transform.position).normalized;

    void Update()
    {
        // 实键的生命周期由 PreservedBond 管理
        if (GetComponent<PreservedBond>() != null)
            return;

        if (linkedAtom == null || !linkedAtom.activeInHierarchy)
        {
            //Debug.LogWarning($"[DashedBondLink.Update] 虚键即将被销毁! " +
            //    $"名称:{gameObject.name}, Tag:{gameObject.tag}, " +
            //    $"bondType:{bondType}, linkedAtom:{(linkedAtom == null ? "null" : linkedAtom.name)}, " +
            //    $"位置:{transform.position}");

            Destroy(gameObject);
        }
    }
}
