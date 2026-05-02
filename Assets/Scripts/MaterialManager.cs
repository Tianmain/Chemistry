using UnityEngine;

[CreateAssetMenu(fileName = "MaterialManager", menuName = "Chemistry/Material Manager")]
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
