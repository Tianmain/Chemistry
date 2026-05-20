using UnityEngine;

[CreateAssetMenu(fileName = "MaterialManager", menuName = "Chemistry/Material Manager")]
/// <summary>
/// 材质管理器，为每种元素提供对应的 Material。
/// 在 Inspector 中配置，供 AtomManager 使用。
/// </summary>
public class MaterialManager : ScriptableObject
{
    public Material hydrogen;
    public Material lithium;
    public Material carbon;
    public Material nitrogen;
    public Material oxygen;
    public Material fluorine;
    public Material sodium;
    public Material magnesium;
    public Material aluminum;
    public Material silicon;
    public Material phosphorus;
    public Material sulfur;
    public Material chlorine;
    public Material potassium;
    public Material calcium;

    public Material highlightedMaterial;

    public Material glowHaloMaterial;

    public Material singleBondMaterial;
    public Material doubleBondMaterial;
    public Material tripleBondMaterial;

    public Material dashedBondMaterial;
    public Material dashedBondHighlightMaterial;

    /// <summary>
    /// 在 Inspector 中赋值：所有 Element 资源（顺序不限）
    /// </summary>
    public Element[] elements;

    /// <summary>
    /// 按资源名查找 Element（用于存档加载）
    /// </summary>
    public Element GetElement(string elementName)
    {
        if (elements == null) return null;
        foreach (var e in elements)
            if (e != null && e.name == elementName)
                return e;
        return null;
    }

    public Material GetElementMaterial(string elementName)
    {
        return elementName switch
        {
            "Hydrogen" => hydrogen,
            "Lithium" => lithium,
            "Carbon" => carbon,
            "Nitrogen" => nitrogen,
            "Oxygen" => oxygen,
            "Fluorine" => fluorine,
            "Sodium" => sodium,
            "Magnesium" => magnesium,
            "Aluminum" => aluminum,
            "Silicon" => silicon,
            "Phosphorus" => phosphorus,
            "Sulfur" => sulfur,
            "Chlorine" => chlorine,
            "Potassium" => potassium,
            "Calcium" => calcium,
            _ => null
        };
    }
}
