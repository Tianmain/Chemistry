using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 原子数据组件，附加在原子 GameObject 上
/// 记录元素类型、已用键数，以及由 VSEPR 理论计算的键方向
/// </summary>
public class AtomData : MonoBehaviour
{
    // 元素类型
    public Element element;

    // 已使用的键数
    public int usedBonds;

    // 由 VSEPR 理论计算的键方向列表
    public List<Vector3> bondDirections = new List<Vector3>();

    // 剩余可用键数
    public int RemainingBonds => element.maxBondCount - usedBonds;
}
