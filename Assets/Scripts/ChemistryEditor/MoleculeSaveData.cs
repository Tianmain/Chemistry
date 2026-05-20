using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 分子结构存档数据（纯数据，可 JSON 序列化）
/// </summary>
[System.Serializable]
public class MoleculeSaveData
{
    public string version = "1.0";
    public long saveTimeTicks;
    public List<AtomSaveEntry> atoms;
    public List<BondSaveEntry> bonds;

    public MoleculeSaveData()
    {
        atoms = new List<AtomSaveEntry>();
        bonds = new List<BondSaveEntry>();
    }
}

[System.Serializable]
public class AtomSaveEntry
{
    public string elementName;   // 元素名，如 "Hydrogen"
    public float posX, posY, posZ;
    public int usedBonds;
}

[System.Serializable]
public class BondSaveEntry
{
    public int atomIndexA;   // atoms 列表中的索引
    public int atomIndexB;
    public int bondType;     // 1=单键, 2=双键, 3=三键
}
