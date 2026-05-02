using System.Collections.Generic;
using UnityEngine;

public class AtomData : MonoBehaviour
{
    public Element element;
    public int usedBonds;
    public List<Vector3> bondDirections = new List<Vector3>();

    public int RemainingBonds => element.maxBondCount - usedBonds;
}
