using UnityEngine;
using System.Text;

/// <summary>
/// 调试面板 - F12 切换显示，展示所有原子坐标、键数及实键/虚键信息
/// </summary>
public class DebugOutputPanel : MonoBehaviour
{
    [SerializeField] private DashedBondManager dashedBondManager;
    [SerializeField] private AtomManager atomManager;
    [SerializeField] private TMPro.TextMeshProUGUI debugText;
    [SerializeField] private GameObject panelObject;
    [SerializeField] private KeyCode toggleKey = KeyCode.F12;

    private bool isVisible = false;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }

        if (isVisible)
        {
            UpdateDebugInfo();
        }
    }

    public void TogglePanel()
    {
        isVisible = !isVisible;
        if (panelObject != null)
            panelObject.SetActive(isVisible);
    }

    private void UpdateDebugInfo()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== 原子与键调试信息 ===");
        sb.AppendLine();

        if (atomManager != null && atomManager.atoms != null)
        {
            sb.AppendLine($"--- 原子总数: {atomManager.atoms.Count} ---");

            for (int i = 0; i < atomManager.atoms.Count; i++)
            {
                GameObject atom = atomManager.atoms[i];
                if (atom == null) continue;

                Vector3 pos = atom.transform.position;
                AtomData data = atom.GetComponent<AtomData>();
                int usedBonds = data?.usedBonds ?? 0;
                int maxBonds = data?.element?.maxBondCount ?? 0;

                sb.AppendLine($"【原子 {i + 1}】{atom.name} ({(data?.element?.symbol ?? "?")})");
                sb.AppendLine($"   坐标: ({pos.x:F3}, {pos.y:F3}, {pos.z:F3})");
                sb.AppendLine($"   已用键数: {usedBonds}/{maxBonds} (剩余: {maxBonds - usedBonds})");
            }
        }

        sb.AppendLine();

        if (dashedBondManager != null && dashedBondManager.preservedBonds != null)
        {
            sb.AppendLine($"--- 实键 (PreservedBond) 总数: {dashedBondManager.preservedBonds.Count} ---");

            for (int i = 0; i < dashedBondManager.preservedBonds.Count; i++)
            {
                GameObject bond = dashedBondManager.preservedBonds[i];
                if (bond == null) continue;

                DashedBondLink link = bond.GetComponent<DashedBondLink>();
                if (link == null) continue;

                PreservedBond pb = bond.GetComponent<PreservedBond>();
                GameObject startAtom = link.linkedAtom;
                GameObject endAtom = pb?.OtherLinkedAtom;

                string bondTypeName = link.bondType switch
                {
                    1 => "单键",
                    2 => "双键",
                    3 => "三键",
                    _ => $"未知({link.bondType})"
                };

                sb.AppendLine($"【实键 {i + 1}】{bondTypeName}");
                sb.AppendLine($"   起始原子: {(startAtom != null ? startAtom.name : "null")}");
                if (startAtom != null)
                    sb.AppendLine($"   起始坐标: ({startAtom.transform.position.x:F3}, {startAtom.transform.position.y:F3}, {startAtom.transform.position.z:F3})");
                sb.AppendLine($"   末端原子: {(endAtom != null ? endAtom.name : "null")}");
                if (endAtom != null)
                    sb.AppendLine($"   末端坐标: ({endAtom.transform.position.x:F3}, {endAtom.transform.position.y:F3}, {endAtom.transform.position.z:F3})");
                else
                    sb.AppendLine($"   末端坐标 (目标): ({link.endPosition.x:F3}, {link.endPosition.y:F3}, {link.endPosition.z:F3})");
                sb.AppendLine($"   键中心位置: ({bond.transform.position.x:F3}, {bond.transform.position.y:F3}, {bond.transform.position.z:F3})");
            }
        }

        sb.AppendLine();

        if (dashedBondManager != null)
        {
            var activeList = dashedBondManager.GetActiveBondList();
            if (activeList.Count > 0)
            {
                sb.AppendLine($"--- 活跃虚键 (DashedBond) 总数: {activeList.Count} ---");

                for (int i = 0; i < activeList.Count; i++)
                {
                    GameObject dash = activeList[i];
                    DashedBondLink link = dash.GetComponent<DashedBondLink>();
                    if (link == null) continue;

                    string bondTypeName = link.bondType switch
                    {
                        1 => "单键",
                        2 => "双键",
                        3 => "三键",
                        _ => $"未知({link.bondType})"
                    };

                    sb.AppendLine($"【虚键 {i + 1}】{bondTypeName}");
                    sb.AppendLine($"   关联原子: {(link.linkedAtom != null ? link.linkedAtom.name : "null")}");
                    if (link.linkedAtom != null)
                        sb.AppendLine($"   起始坐标: ({link.linkedAtom.transform.position.x:F3}, {link.linkedAtom.transform.position.y:F3}, {link.linkedAtom.transform.position.z:F3})");
                    sb.AppendLine($"   末端坐标: ({link.endPosition.x:F3}, {link.endPosition.y:F3}, {link.endPosition.z:F3})");
                }
            }
        }

        if (debugText != null)
            debugText.text = sb.ToString();
    }
}
