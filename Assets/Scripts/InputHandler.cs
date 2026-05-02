using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

public class InputHandler : MonoBehaviour
{
    [SerializeField] private AtomManager atomManager;
    [SerializeField] private DashedBondManager dashedBondManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private HistoryManager historyManager;
    [SerializeField] private CameraController cameraController;

    private Element selectedElement;
    private GameObject selectedAtom;
    private int selectedBondType = 1;
    private int maxBondCount;

    private GameObject cachedSelectedAtom;
    private int cachedSelectedBondType;
    private int cachedMaxBondCount;

    private Dictionary<GameObject, int> atomBondTypes = new Dictionary<GameObject, int>();

    // 脏标记：仅在选中状态变化时重建 UI 文本，避免每帧分配 StringBuilder 和字符串
    private bool isSelectionDirty = true;

    public Button undoButton;
    public Button redoButton;

    // 缓存 Camera，避免 Camera.main 内部 FindObjectOfType 导致 TLS 泄漏
    private Camera cachedCamera;
    // 预分配 RaycastHit，避免 Physics.Raycast(out RaycastHit) 在 TLS 上分配
    private RaycastHit raycastHitCache;

    private void Awake()
    {
        cachedCamera = Camera.main;
    }

    void Update()
    {
        HandleElementSelection();
        HandleAtomCreation();
        HandleSelection();
        HandleBondTypeSelection();
        HandleDeletion();
        DashedBondsVisition();
        HandleUndoRedo();
        ButtonVisiable();
        UpdateSelectedInfo();

        if (Input.GetKeyDown(KeyCode.F12))
        {
            OutputDebugInfo();
        }
    }

    private void HandleElementSelection()
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
        uiManager.UpdateElementText(selectedElement);

        // 选中键时，按原子键直接在键末端方向创建原子
        if (selectedElement != null && dashedBondManager.selectedDashedBond != null)
        {
            TryCreateAtomAtSelectedBond();
        }
    }

    /// <summary>
    /// 选中键时按原子键，在键的末端方向创建原子
    /// </summary>
    private void TryCreateAtomAtSelectedBond()
    {
        GameObject selectedBond = dashedBondManager.selectedDashedBond;
        if (selectedBond == null || selectedElement == null) return;

        Vector3 createPosition;
        int bondType;

        if (selectedBond.CompareTag("DashedBond"))
        {
            // 虚键：直接使用 endPosition
            DashedBondLink link = selectedBond.GetComponent<DashedBondLink>();
            if (link == null || link.linkedAtom == null) return;

            createPosition = link.endPosition;
            bondType = link.bondType;
        }
        else if (selectedBond.CompareTag("PreservedBond"))
        {
            // 实键：在 OriginalLinkedAtom → OriginalEndPosition 方向上创建
            PreservedBond pb = selectedBond.GetComponent<PreservedBond>();
            DashedBondLink link = selectedBond.GetComponent<DashedBondLink>();
            if (pb == null || pb.OriginalLinkedAtom == null || link == null) return;

            Vector3 atomPos = pb.OriginalLinkedAtom.transform.position;
            Vector3 direction = (link.endPosition - atomPos).normalized;
            createPosition = atomPos + direction * 1.0f; // FixedBondLength = 1.0f
            bondType = pb.bondType;
        }
        else
        {
            return;
        }

        // 验证键类型
        if (selectedElement.maxBondCount < bondType)
        {
            string errorMsg = LocalizationManager.Instance.GetLocalizedText("error_max_bond");
            Debug.LogWarning($"{errorMsg} ({selectedElement.maxBondCount} < {bondType})");
            selectedElement = null;
            uiManager.UpdateElementText(null);
            return;
        }

        // 创建原子
        var command = new CreateAtomCommand(atomManager, selectedElement, createPosition);
        if (command.IsValid())
        {
            historyManager.ExecuteCommand(command);
            Debug.Log($"键触发原子创建: {selectedElement.name} at {createPosition}");

            // 创建后保持选中原来的原子，不切换到新原子
            GameObject newAtom = command.GetCreatedAtom();
            if (newAtom != null && selectedAtom != null)
            {
                if (cameraController != null)
                    cameraController.SetSelectedAtom(selectedAtom);
            }

            // 创建原子后刷新选中原子虚键
            dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
        }

        selectedElement = null;
        uiManager.UpdateElementText(null);
        dashedBondManager.HighlightBond(null);
    }

    private void HandleAtomCreation()
    {
        if (Input.GetMouseButtonDown(1) && selectedElement != null)
        {
            Vector3 screenCenter = new Vector3(0.5f, 0.5f, 10f);
            Vector3 worldCenter = Camera.main.ViewportToWorldPoint(screenCenter);

            if (selectedAtom != null)
            {
                atomManager.ResetAtomMaterial(selectedAtom);
                selectedAtom = null;
                isSelectionDirty = true;
                if (cameraController != null)
                    cameraController.ClearSelectedAtom();
            }

            var command = new CreateAtomCommand(atomManager, selectedElement, worldCenter);
            historyManager.ExecuteCommand(command);

            selectedElement = null;
        }
    }

    private void HandleSelection()
    {
        if (GUIUtility.hotControl != 0)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cachedCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out raycastHitCache))
            {
                if (raycastHitCache.transform.CompareTag("PreservedBond"))
                {
                    DashedBondLink link = raycastHitCache.transform.GetComponent<DashedBondLink>();
                    if (link != null)
                    {
                        if (dashedBondManager.selectedDashedBond != null)
                        {
                            dashedBondManager.UnselectAllBonds();
                        }

                        dashedBondManager.HighlightBond(raycastHitCache.transform.gameObject);
                        isSelectionDirty = true;

                        if (selectedAtom != null)
                        {
                            atomManager.ResetAtomMaterial(selectedAtom);
                            selectedAtom = null;
                            if (cameraController != null)
                                cameraController.ClearSelectedAtom();
                            // 取消选中原子后清除虚键
                            dashedBondManager.ClearDashedBonds();
                        }
                    }
                    return;
                }

                if (raycastHitCache.transform.CompareTag("DashedBond"))
                {
                    DashedBondLink link = raycastHitCache.transform.GetComponent<DashedBondLink>();
                    if (link != null && link.linkedAtom != null)
                    {
                        Debug.Log($"[选中] 虚键: 关联原子={link.linkedAtom.name}, 末端位置=({link.endPosition.x:F2},{link.endPosition.y:F2},{link.endPosition.z:F2}), bondType={link.bondType}");
                        Vector3 endPosition = link.endPosition;

                        if (selectedElement != null)
                        {
                            if (selectedElement.maxBondCount < link.bondType)
                            {
                                string errorMsg = LocalizationManager.Instance.GetLocalizedText("error_max_bond");
                                Debug.LogWarning($"{errorMsg} ({selectedElement.maxBondCount} < {link.bondType})");
                                selectedElement = null;
                                uiManager.UpdateElementText(null);
                                return;
                            }

                            var command = new CreateAtomCommand(atomManager, selectedElement, endPosition);
                            if (command.IsValid())
                            {
                                historyManager.ExecuteCommand(command);
                                Debug.Log("虚键触发原子创建命令已记录");
                            }
                            selectedElement = null;
                            uiManager.UpdateElementText(null);
                        }

                        if (selectedAtom != null)
                        {
                            atomManager.ResetAtomMaterial(selectedAtom);
                        }

                        selectedAtom = link.linkedAtom;
                        atomManager.HighlightAtom(selectedAtom);
                        isSelectionDirty = true;
                        SetMaxBondCount();

                        if (!atomBondTypes.ContainsKey(selectedAtom))
                        {
                            atomBondTypes[selectedAtom] = 1;
                        }
                        selectedBondType = atomBondTypes[selectedAtom];

                        // 选中原子后刷新虚键
                        dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
                        cameraController.SetSelectedAtom(selectedAtom);

                        dashedBondManager.HighlightBond(raycastHitCache.transform.gameObject);
                    }
                    return;
                }

                if (raycastHitCache.transform.CompareTag("Atom"))
                {
                    GameObject clickedAtom = raycastHitCache.transform.gameObject;
                    bool isSameAtom = (selectedAtom == clickedAtom);
                    Debug.Log($"[选中] 原子: {clickedAtom.name}, 坐标=({clickedAtom.transform.position.x:F2},{clickedAtom.transform.position.y:F2},{clickedAtom.transform.position.z:F2}), 是否切换={isSameAtom}");
                    dashedBondManager.UnhighlightDashedBondsForAtom(clickedAtom);

                    selectedElement = null;
                    uiManager.UpdateElementText(null);

                    if (selectedAtom != null && !isSameAtom)
                    {
                        atomManager.ResetAtomMaterial(selectedAtom);
                        if (cameraController != null)
                            cameraController.ClearSelectedAtom();
                    }

                    if (!isSameAtom)
                    {
                        selectedAtom = clickedAtom;
                        atomManager.HighlightAtom(selectedAtom);
                        SetMaxBondCount();
                        isSelectionDirty = true;

                        if (!atomBondTypes.ContainsKey(selectedAtom))
                        {
                            atomBondTypes[selectedAtom] = 1;
                        }
                        selectedBondType = atomBondTypes[selectedAtom];

                        // 选中原子后刷新虚键
                        dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
                        if (cameraController != null)
                            cameraController.SetSelectedAtom(selectedAtom);
                    }

                    dashedBondManager.HighlightBond(null);
                    return;
                }
            }
            else
            {
                Debug.Log($"[取消选中] 点击了空白区域, 原选中原子={selectedAtom?.name ?? "null"}");
                dashedBondManager.UnselectAllBonds();
                UIManager.Instance.UpdateSelectedInfo("");

                if (selectedAtom != null)
                {
                    atomManager.ResetAtomMaterial(selectedAtom);
                    selectedAtom = null;
                    if (cameraController != null)
                        cameraController.ClearSelectedAtom();
                    // 取消选中后清除虚键
                    dashedBondManager.ClearDashedBonds();
                }

                isSelectionDirty = true;
            }
        }
    }

    private void HandleDeletion()
    {
        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
        {
        if (dashedBondManager.selectedDashedBond != null &&
            dashedBondManager.selectedDashedBond.CompareTag("PreservedBond"))
        {
            // 使用 DeleteBondCommand 记录到历史，支持撤销/重做
            var command = new DeleteBondCommand(dashedBondManager, dashedBondManager.selectedDashedBond);
            historyManager.ExecuteCommand(command);
            dashedBondManager.selectedDashedBond = null;

            // 删除实键后刷新虚键
            if (selectedAtom != null)
                dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
            return;
        }

            Ray ray = cachedCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out raycastHitCache))
            {
                if (raycastHitCache.transform.CompareTag("PreservedBond"))
                {
                    // 使用 DeleteBondCommand 记录到历史，支持撤销/重做
                    var command = new DeleteBondCommand(dashedBondManager, raycastHitCache.transform.gameObject);
                    historyManager.ExecuteCommand(command);

                    // 删除实键后刷新虚键
                    if (selectedAtom != null)
                        dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
                    return;
                }
            }

            if (selectedAtom != null)
            {
                var command = new DeleteAtomCommand(atomManager, dashedBondManager, historyManager, selectedAtom);
                historyManager.ExecuteCommand(command);

                if (atomBondTypes.ContainsKey(selectedAtom))
                {
                    atomBondTypes.Remove(selectedAtom);
                }

                selectedAtom = null;
                cameraController.ClearSelectedAtom();
                isSelectionDirty = true;

                // 删除原子后清除虚键
                dashedBondManager.ClearDashedBonds();
            }
        }
    }

    private void HandleUndoRedo()
    {
        if (historyManager == null)
        {
            Debug.LogWarning("HistoryManager未初始化！");
            return;
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z))
        {
            historyManager.Undo();
            // 撤销后刷新虚键
            if (selectedAtom != null)
                dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
            else
                dashedBondManager.ClearDashedBonds();
        }
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Y))
        {
            historyManager.Redo();
            // 重做后刷新虚键
            if (selectedAtom != null)
                dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
            else
                dashedBondManager.ClearDashedBonds();
        }
    }

    private void ButtonVisiable()
    {
        undoButton.interactable = historyManager.CanUndo();
        redoButton.interactable = historyManager.CanRedo();
    }

    private void UpdateSelectedInfo()
    {
        // 脏标记检查：仅当选中状态变化时才重建字符串，避免每帧 new StringBuilder + string.Format 的临时分配
        if (!isSelectionDirty) return;
        isSelectionDirty = false;

        StringBuilder sb = new StringBuilder();

        if (selectedAtom != null)
        {
            Element element = GetElementFromAtom(selectedAtom);
            string elemName = LocalizationManager.Instance.GetLocalizedText($"element_{element.name.ToLower()}");
            sb.Append($"{elemName} ({element.symbol})");

            string bondTypeText = LocalizationManager.Instance.GetLocalizedText("bond_type_info");
            sb.Append($" {string.Format(bondTypeText, selectedBondType)}");
        }

        if (dashedBondManager.selectedDashedBond != null)
        {
            DashedBondLink link = dashedBondManager.selectedDashedBond.GetComponent<DashedBondLink>();
            if (link != null)
            {
                string bondTypeText = LocalizationManager.Instance.GetLocalizedText("bond_type_info");
                sb.Append($" {string.Format(bondTypeText, link.bondType)}");

                string bondType = dashedBondManager.selectedDashedBond.CompareTag("PreservedBond") ?
                    LocalizationManager.Instance.GetLocalizedText("preserved_bond") :
                    LocalizationManager.Instance.GetLocalizedText("dashed_bond");
                sb.Append($" ({bondType})");
            }
        }

        UIManager.Instance.UpdateSelectedInfo(sb.ToString());
    }

    public void UIUndo()
    {
        if (historyManager != null)
            historyManager.Undo();
    }

    public void UIRedo()
    {
        if (historyManager != null)
            historyManager.Redo();
    }

    private void HandleBondTypeSelection()
    {
        if (selectedAtom == null || dashedBondManager.HasPreservedBondForAtom(selectedAtom)) return;

        AtomData atomData = selectedAtom.GetComponent<AtomData>();
        if (atomData == null || atomData.element == null) return;

        int remainingSlots = atomData.element.maxBondCount - atomData.usedBonds;

        int maxType = atomData.element.maxBondCount switch
        {
            4 => 3,
            3 => 3,
            2 => 2,
            _ => 1
        };

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            // 单键始终允许（至少剩1个键位）
            if (remainingSlots >= 1)
            {
                selectedBondType = 1;
                atomBondTypes[selectedAtom] = selectedBondType;
                isSelectionDirty = true;
                dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            // 双键需要至少剩2个键位
            if (remainingSlots >= 2)
            {
                selectedBondType = 2;
                atomBondTypes[selectedAtom] = selectedBondType;
                isSelectionDirty = true;
                dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            // 三键需要至少剩3个键位
            if (remainingSlots >= 3)
            {
                selectedBondType = 3;
                atomBondTypes[selectedAtom] = selectedBondType;
                isSelectionDirty = true;
                dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
            }
        }
    }

    private void DashedBondsVisition()
    {
        // 虚键生成由 HandleSelection（选中原子时）和 CheckAndConvertDashedBondsToPreserved
        // （创建原子后自动转换）负责，此处仅同步缓存。
        // 不在此处调用 UpdateDashedBonds，避免键类型切换时错误重建虚键。
        cachedSelectedAtom = selectedAtom;
        cachedSelectedBondType = selectedBondType;
        cachedMaxBondCount = maxBondCount;
    }

    private void UpdateDashedBonds()
    {
        dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
    }

    private void SetMaxBondCount()
    {
        if (selectedAtom == null)
        {
            maxBondCount = 0;
            return;
        }

        Element element = atomManager.GetElementFromAtom(selectedAtom);
        maxBondCount = element?.maxBondCount ?? 0;
    }

    private Element GetElementFromAtom(GameObject atom)
    {
        return atom.name switch
        {
            "Hydrogen" => Element.Hydrogen,
            "Lithium" => Element.Lithium,
            "Carbon" => Element.Carbon,
            "Nitrogen" => Element.Nitrogen,
            "Oxygen" => Element.Oxygen,
            "Fluorine" => Element.Fluorine,
            "Sodium" => Element.Sodium,
            "Magnesium" => Element.Magnesium,
            "Aluminum" => Element.Aluminum,
            "Silicon" => Element.Silicon,
            "Phosphorus" => Element.Phosphorus,
            "Sulfur" => Element.Sulfur,
            "Chlorine" => Element.Chlorine,
            "Potassium" => Element.Potassium,
            "Calcium" => Element.Calcium,
            _ => throw new System.ArgumentException($"未知原子名称: {atom.name}")
        };
    }

    /// <summary>
    /// F12：输出所有原子类型、坐标和键类型的调试信息
    /// </summary>
    public void OutputDebugInfo()
    {
        if (dashedBondManager == null)
        {
            Debug.LogWarning("DashedBondManager 未初始化，无法输出调试信息");
            return;
        }

        string debugInfo = dashedBondManager.GetDebugInfo();
        Debug.Log("========== 原子与键调试信息 ==========\n" + debugInfo + "\n======================================");
    }
}
