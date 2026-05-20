/// <summary>
/// 原子数据组件，附加在原子 GameObject 上。
/// 记录元素类型、已用键数，以及由 VSEPR 理论计算的键方向。
/// </summary>
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 原子数据组件，存储元素信息、已用键数和键方向。
/// </summary>
public class AtomData : MonoBehaviour
{
    /// <summary>元素类型（决定半径、最大键数、孤电子对数）。</summary>
    public Element element;

    /// <summary>已使用的键数。</summary>
    public int usedBonds;

    /// <summary>由 VSEPR 理论计算的键方向列表。</summary>
    public List<Vector3> bondDirections = new List<Vector3>();

    /// <summary>
    /// 剩余可用键数。
    /// </summary>
    public int RemainingBonds => element.maxBondCount - usedBonds;
}
