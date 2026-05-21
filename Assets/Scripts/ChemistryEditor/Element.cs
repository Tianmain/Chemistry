[System.Serializable]
public class Element
{
    public string name;
    public string symbol;
    public float radius;
    public int maxBondCount;

    /// <summary>
    /// 孤电子对数
    /// C/Si: 0对(109.5°)  N/P/Al: 1对(107°)  O/S: 2对(104.5°)
    /// </summary>
    public int lonePairCount;

    /// <summary>
    /// 实际键角，由maxBondCount和孤电子对共同决定
    /// </summary>
    public float bondAngle;

    /// <summary>
    /// 元素定义，包含名称、符号、半径、最大键数、孤电子对数和键角
    /// 使用 VSEPR 理论计算虚键方向。
    /// </summary>
    public Element(string name, string symbol, float radius, int maxBondCount,
                   int lonePairCount = 0, float bondAngle = 109.5f)
    {
        this.name = name;
        this.symbol = symbol;
        this.radius = radius;
        this.maxBondCount = maxBondCount;
        this.lonePairCount = lonePairCount;
        this.bondAngle = bondAngle;
    }

    // H/Li/F/Na/Cl/K: maxBond=1，末端原子，键角不影响虚键布局
    public static Element Hydrogen   = new("Hydrogen",   "H",  0.5f, 1, lonePairCount: 3, bondAngle: 109.5f);
    public static Element Lithium    = new("Lithium",    "Li", 0.8f, 1, lonePairCount: 0, bondAngle: 109.5f);
    public static Element Carbon     = new("Carbon",     "C",  0.7f, 4, lonePairCount: 0, bondAngle: 109.5f);
    public static Element Nitrogen   = new("Nitrogen",   "N",  0.6f, 3, lonePairCount: 1, bondAngle: 107.0f);
    public static Element Oxygen     = new("Oxygen",     "O",  0.5f, 2, lonePairCount: 2, bondAngle: 104.5f);
    public static Element Fluorine   = new("Fluorine",   "F",  0.4f, 1, lonePairCount: 3, bondAngle: 109.5f);
    public static Element Sodium     = new("Sodium",     "Na", 0.9f, 1, lonePairCount: 0, bondAngle: 109.5f);
    public static Element Magnesium  = new("Magnesium",  "Mg", 0.8f, 2, lonePairCount: 2, bondAngle: 104.5f);
    public static Element Aluminum   = new("Aluminum",   "Al", 0.7f, 3, lonePairCount: 1, bondAngle: 107.0f);
    public static Element Silicon    = new("Silicon",    "Si", 0.6f, 4, lonePairCount: 0, bondAngle: 109.5f);
    public static Element Phosphorus = new("Phosphorus", "P",  0.5f, 3, lonePairCount: 1, bondAngle: 107.0f);
    public static Element Sulfur     = new("Sulfur",     "S",  0.6f, 2, lonePairCount: 2, bondAngle: 104.5f);
    public static Element Chlorine   = new("Chlorine",   "Cl", 0.5f, 1, lonePairCount: 3, bondAngle: 109.5f);
    public static Element Potassium  = new("Potassium",  "K",  0.8f, 1, lonePairCount: 0, bondAngle: 109.5f);
    public static Element Calcium    = new("Calcium",    "Ca", 0.9f, 2, lonePairCount: 2, bondAngle: 104.5f);
}
