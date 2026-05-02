// 旧版编辑器脚本，建议迁移到 InputHandler 系统

using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class MainEditor : MonoBehaviour
{
    public TextMeshProUGUI elementText;
    public TextMeshProUGUI keyMapText;

    public Material HMaterial;
    public Material LiMaterial;
    public Material CMaterial;
    public Material NMaterial;
    public Material OMaterial;
    public Material FMaterial;
    public Material NaMaterial;
    public Material MgMaterial;
    public Material AlMaterial;
    public Material SiMaterial;
    public Material PMaterial;
    public Material SMaterial;
    public Material ClMaterial;
    public Material KMaterial;
    public Material CaMaterial;
    public Material highlightedMaterial;

    public Material bondMaterial;
    public Material dashedBondMaterial;

    private Element selectedElement;
    private GameObject selectedAtom;
    private List<GameObject> atoms = new List<GameObject>();
    private List<GameObject> bonds = new List<GameObject>();
    private List<GameObject> dashedBonds = new List<GameObject>();
    private int selectedBondType = 1;
    private int maxBondCount = 0;

    // 缓存 Camera，避免 Camera.main 内部 FindObjectOfType 导致 TLS 泄漏
    private Camera cachedCamera;
    // 预分配 RaycastHit，避免 Physics.Raycast(out RaycastHit) 在 TLS 上分配
    private RaycastHit raycastHitCache;

    private void Awake()
    {
        cachedCamera = Camera.main;
    }

    void Start()
    {
        elementText.text = "当前元素：";
        UpdateKeyMapText();
    }

    void Update()
    {
        HandleElementSelection();
        HandleAtomCreation();
        HandleAtomSelection();
        HandleBondTypeSelection();
        HandleDeletion();
    }

    #region 输入处理
    void HandleElementSelection()
    {
        if (Input.GetKeyDown(KeyCode.H)) selectedElement = Element.Hydrogen;
        if (Input.GetKeyDown(KeyCode.L)) selectedElement = Element.Lithium;
        if (Input.GetKeyDown(KeyCode.C)) selectedElement = Element.Carbon;
        if (Input.GetKeyDown(KeyCode.N)) selectedElement = Element.Nitrogen;
        if (Input.GetKeyDown(KeyCode.O)) selectedElement = Element.Oxygen;
        if (Input.GetKeyDown(KeyCode.F)) selectedElement = Element.Fluorine;
        if (Input.GetKeyDown(KeyCode.A)) selectedElement = Element.Sodium;
        if (Input.GetKeyDown(KeyCode.G)) selectedElement = Element.Magnesium;
        if (Input.GetKeyDown(KeyCode.P)) selectedElement = Element.Aluminum;
        if (Input.GetKeyDown(KeyCode.S)) selectedElement = Element.Silicon;
        if (Input.GetKeyDown(KeyCode.D)) selectedElement = Element.Phosphorus;
        if (Input.GetKeyDown(KeyCode.U)) selectedElement = Element.Sulfur;
        if (Input.GetKeyDown(KeyCode.Z)) selectedElement = Element.Chlorine;
        if (Input.GetKeyDown(KeyCode.K)) selectedElement = Element.Potassium;
        if (Input.GetKeyDown(KeyCode.B)) selectedElement = Element.Calcium;

        UpdateElementText();
    }

    void HandleAtomCreation()
    {
        if (Input.GetMouseButtonDown(1) && selectedElement != null)
        {
            if (selectedAtom != null)
            {
                CreateConnectedAtom(selectedAtom, selectedElement);
            }
            else
            {
                CreateAtom();
            }
            selectedElement = null;
        }
    }

    void HandleAtomSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cachedCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out raycastHitCache))
            {
                SelectAtom(raycastHitCache.transform.gameObject);
            }
            else
            {
                DeselectAtom();
            }
        }
    }

    void HandleBondTypeSelection()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) selectedBondType = 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) selectedBondType = 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) selectedBondType = 3;
        UpdateDashedBonds();
    }

    void HandleDeletion()
    {
        if (Input.GetKeyDown(KeyCode.Delete) && selectedAtom != null)
        {
            DeleteAtom(selectedAtom);
            selectedAtom = null;
        }
    }
    #endregion

    #region 原子操作
    void CreateAtom()
    {
        Vector3 mousePos = cachedCamera.ScreenToWorldPoint(Input.mousePosition);
        GameObject atom = InstantiateAtom(mousePos);
        ConfigureAtomPhysics(atom);
        SetAtomMaterial(atom);
        atoms.Add(atom);
    }

    GameObject InstantiateAtom(Vector3 position)
    {
        GameObject atom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        atom.transform.position = new Vector3(position.x, position.y, 0);
        atom.transform.localScale = Vector3.one * selectedElement.radius;
        atom.name = selectedElement.name;
        return atom;
    }

    void ConfigureAtomPhysics(GameObject atom)
    {
        Rigidbody rb = atom.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    void CreateConnectedAtom(GameObject parentAtom, Element element)
    {
        Vector3 offset = new Vector3(element.radius * 2, 0, 0);
        GameObject atom = InstantiateAtom(parentAtom.transform.position + offset);
        ConfigureAtomPhysics(atom);
        SetAtomMaterial(atom);
        atoms.Add(atom);
        CreateBond(parentAtom, atom, selectedBondType);
    }

    void SetAtomMaterial(GameObject atom)
    {
        Material material = GetDefaultMaterial(atom.name);
        if (material != null)
        {
            atom.GetComponent<Renderer>().material = material;
        }
    }

    Material GetDefaultMaterial(string elementName)
    {
        switch (elementName)
        {
            case "氢": return HMaterial;
            case "锂": return LiMaterial;
            case "碳": return CMaterial;
            case "氮": return NMaterial;
            case "氧": return OMaterial;
            case "氟": return FMaterial;
            case "钠": return NaMaterial;
            case "镁": return MgMaterial;
            case "铝": return AlMaterial;
            case "硅": return SiMaterial;
            case "磷": return PMaterial;
            case "硫": return SMaterial;
            case "氯": return ClMaterial;
            case "钾": return KMaterial;
            case "钙": return CaMaterial;
            default: return null;
        }
    }
    #endregion

    #region 键操作
    void CreateBond(GameObject atom1, GameObject atom2, int bondType)
    {
        GameObject bond = new GameObject($"Bond_{atom1.name}_{atom2.name}");
        bond.AddComponent<Bond>().Initialize(atom1, atom2);

        Vector3 bondPosition = (atom1.transform.position + atom2.transform.position) / 2;
        Vector3 bondDirection = (atom2.transform.position - atom1.transform.position).normalized;

        bond.transform.position = bondPosition;
        bond.transform.rotation = Quaternion.LookRotation(bondDirection) * Quaternion.Euler(90, 0, 0);

        for (int i = 0; i < bondType; i++)
        {
            CreateBondCylinder(bond.transform, bondDirection, i, bondType);
        }

        bonds.Add(bond);
    }

    void CreateBondCylinder(Transform parent, Vector3 direction, int index, int total)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(cylinder.GetComponent<Collider>());

        cylinder.transform.parent = parent;
        cylinder.transform.localScale = new Vector3(0.2f, direction.magnitude / 2, 0.2f);

        if (total > 1)
        {
            float offset = 0.2f * (index - (total - 1) / 2f);
            cylinder.transform.localPosition += new Vector3(0, offset, 0);
        }

        cylinder.GetComponent<Renderer>().material = bondMaterial;
    }
    #endregion

    #region 选择系统
    void SelectAtom(GameObject atom)
    {
        if (selectedAtom != null)
        {
            selectedAtom.GetComponent<Renderer>().material = GetDefaultMaterial(selectedAtom.name);
        }

        selectedAtom = atom;
        selectedAtom.GetComponent<Renderer>().material = highlightedMaterial;
        SetMaxBondCount(selectedAtom);
    }

    void DeselectAtom()
    {
        if (selectedAtom != null)
        {
            selectedAtom.GetComponent<Renderer>().material = GetDefaultMaterial(selectedAtom.name);
            selectedAtom = null;
        }
    }

    void SetMaxBondCount(GameObject atom)
    {
        maxBondCount = atom.name switch
        {
            "碳" => 4,
            "氮" => 3,
            "氧" => 2,
            "氟" or "氢" or "锂" or "钠" or "氯" or "钾" => 1,
            "镁" or "钙" => 2,
            "铝" => 3,
            "硅" => 4,
            "磷" => 3,
            "硫" => 2,
            _ => 0
        };
    }
    #endregion

    #region 虚线键
    void UpdateDashedBonds()
    {
        ClearDashedBonds();
        if (selectedAtom == null) return;

        Vector3 position = selectedAtom.transform.position;
        int remainingBonds = maxBondCount - selectedBondType;

        switch (remainingBonds)
        {
            case 3:
                CreateTetrahedralDashes(position);
                break;
            case 2:
                CreateTrigonalDashes(position);
                break;
            case 1:
                CreateLinearDashes(position);
                break;
        }

        // 氧等 maxBond=2 的元素在单键时使用线性结构
        if (maxBondCount == 2 && selectedBondType == 1)
        {
            CreateLinearDashes(position);
        }
    }

    void CreateTetrahedralDashes(Vector3 center)
    {
        Vector3[] directions = new Vector3[]
        {
            Vector3.up,
            new Vector3(0f, -1f/3f, Mathf.Sqrt(8f)/3f).normalized,
            new Vector3(Mathf.Sqrt(6f)/3f, -1f/3f, -Mathf.Sqrt(2f)/3f).normalized,
            new Vector3(-Mathf.Sqrt(6f)/3f, -1f/3f, -Mathf.Sqrt(2f)/3f).normalized
        };

        foreach (var dir in directions)
        {
            CreateDashedLine(center, center + dir * 1.5f);
        }
    }

    void CreateTrigonalDashes(Vector3 center)
    {
        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            CreateDashedLine(center, center + dir * 1.2f);
        }
    }

    void CreateLinearDashes(Vector3 center)
    {
        CreateDashedLine(center, center + Vector3.right * 2f);
        CreateDashedLine(center, center + Vector3.left * 2f);
    }

    void CreateDashedLine(Vector3 start, Vector3 end)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "DashedBondCylinder";
        cylinder.tag = "DashedBond";

        cylinder.GetComponent<Renderer>().material = dashedBondMaterial;

        Vector3 direction = end - start;
        float length = direction.magnitude;

        cylinder.transform.position = (start + end) * 0.5f;
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        cylinder.transform.localScale = new Vector3(0.1f, length / 2, 0.1f);

        dashedBonds.Add(cylinder);
    }

    void ClearDashedBonds()
    {
        foreach (var bond in dashedBonds.Where(b => b != null))
        {
            Destroy(bond);
        }
        dashedBonds.Clear();
    }
    #endregion

    #region 删除系统
    void DeleteAtom(GameObject atom)
    {
        for (int i = bonds.Count - 1; i >= 0; i--)
        {
            Bond bond = bonds[i].GetComponent<Bond>();
            if (bond.IsConnectedTo(atom))
            {
                Destroy(bonds[i]);
                bonds.RemoveAt(i);
            }
        }

        atoms.Remove(atom);
        Destroy(atom);
    }
    #endregion

    #region UI系统
    void UpdateElementText()
    {
        elementText.text = selectedElement != null
            ? $"当前元素：{selectedElement.name} ({selectedElement.symbol})"
            : "当前元素：";
    }

    void UpdateKeyMapText()
    {
        keyMapText.text = @"元素快捷键列表：
氢 (H) - H
锂 (Li) - L
碳 (C) - C
氮 (N) - N
氧 (O) - O
氟 (F) - F
钠 (Na) - A
镁 (Mg) - G
铝 (Al) - P
硅 (Si) - S
磷 (P) - D
硫 (S) - U
氯 (Cl) - Z
钾 (K) - K
钙 (Ca) - B";
    }
    #endregion

    #region 辅助类
    public class Element
    {
        public string name;
        public string symbol;
        public float radius;

        public Element(string name, string symbol, float radius)
        {
            this.name = name;
            this.symbol = symbol;
            this.radius = radius;
        }

        public static Element Hydrogen = new("氢", "H", 0.5f);
        public static Element Lithium = new("锂", "Li", 0.8f);
        public static Element Carbon = new("碳", "C", 0.7f);
        public static Element Nitrogen = new("氮", "N", 0.6f);
        public static Element Oxygen = new("氧", "O", 0.5f);
        public static Element Fluorine = new("氟", "F", 0.4f);
        public static Element Sodium = new("钠", "Na", 0.9f);
        public static Element Magnesium = new("镁", "Mg", 0.8f);
        public static Element Aluminum = new("铝", "Al", 0.7f);
        public static Element Silicon = new("硅", "Si", 0.6f);
        public static Element Phosphorus = new("磷", "P", 0.5f);
        public static Element Sulfur = new("硫", "S", 0.6f);
        public static Element Chlorine = new("氯", "Cl", 0.5f);
        public static Element Potassium = new("钾", "K", 0.8f);
        public static Element Calcium = new("钙", "Ca", 0.9f);
    }

    public class Bond : MonoBehaviour
    {
        public GameObject atom1;
        public GameObject atom2;

        public void Initialize(GameObject a1, GameObject a2)
        {
            atom1 = a1;
            atom2 = a2;
        }

        public bool IsConnectedTo(GameObject atom)
        {
            return atom1 == atom || atom2 == atom;
        }
    }
    #endregion
}
