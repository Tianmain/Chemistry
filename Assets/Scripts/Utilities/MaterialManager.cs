using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MaterialManager", menuName = "Chemistry/Material Manager")]
/// <summary>
/// 材质管理器，为每种元素提供对应的 Material
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

    public Element[] elements;

    private Dictionary<string, Material> elementMaterialMap;
    private Dictionary<string, Element> elementNameMap;

    private void OnEnable()
    {
        InitMaterialMap();
        InitElementMap();
    }

    private void InitMaterialMap()
    {
        if (elementMaterialMap != null) return;

        elementMaterialMap = new Dictionary<string, Material>()
        {
            { "Hydrogen", hydrogen },
            { "Lithium", lithium },
            { "Carbon", carbon },
            { "Nitrogen", nitrogen },
            { "Oxygen", oxygen },
            { "Fluorine", fluorine },
            { "Sodium", sodium },
            { "Magnesium", magnesium },
            { "Aluminum", aluminum },
            { "Silicon", silicon },
            { "Phosphorus", phosphorus },
            { "Sulfur", sulfur },
            { "Chlorine", chlorine },
            { "Potassium", potassium },
            { "Calcium", calcium }
        };
    }

    private void InitElementMap()
    {
        if (elementNameMap != null) return;

        elementNameMap = new Dictionary<string, Element>();
        if (elements == null) return;

        foreach (var e in elements)
        {
            if (e != null && !string.IsNullOrEmpty(e.name))
                elementNameMap[e.name] = e;
        }
    }

    // 按资源名查找Element
    public Element GetElement(string elementName)
    {
        if (string.IsNullOrEmpty(elementName)) return null;

        if (elementNameMap == null)
            InitElementMap();

        if (elementNameMap.TryGetValue(elementName, out Element elem))
            return elem;

        return null;
    }

    // 获取元素对应的材质
    public Material GetElementMaterial(string elementName)
    {
        if (string.IsNullOrEmpty(elementName)) return null;

        if (elementMaterialMap == null)
            InitMaterialMap();

        if (elementMaterialMap.TryGetValue(elementName, out Material mat))
            return mat;

        Debug.LogWarning($"[MaterialManager] 未找到元素 {elementName} 的材质！");
        return null;
    }
}
