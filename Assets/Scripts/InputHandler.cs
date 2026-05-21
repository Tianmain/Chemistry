using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 输入处理中心：元素选择、原子创建、选中、键类型切换、删除、撤销/重做。
/// 同时处理键旋转模式下的输入。
/// </summary>
public class InputHandler : MonoBehaviour
{
    [SerializeField] private AtomManager atomManager;
    [SerializeField] private DashedBondManager dashedBondManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private HistoryManager historyManager;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private BondRotator bondRotator;

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

    // 原子拖拽相关字段
    private bool isDragging = false;
    private Vector3 dragStartMouseWorldPos;
    private Vector3 dragStartAtomPos;
    private List<GameObject> draggingConnectedAtoms;
    private Vector3[] draggingOffsets;

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
        // 原子拖拽处理（优先于其他输入）
        HandleAtomDragging();

        // 键旋转模式下，跳过选择/创建/删除等输入，避免冲突
        bool isRotating = (bondRotator != null && bondRotator.IsRotating());

        if (!isRotating && !isDragging)
        {
            HandleElementSelection();
            HandleAtomCreation();
            HandleSelection();
            HandleBondTypeSelection();
            HandleDeletion();
            DashedBondsVisition();
        }
        else if (isRotating)
        {
            // 旋转模式下：由 BondRotator 自己处理输入
            bondRotator.HandleRotationInput();

            // 旋转时每帧更新角度显示
            isSelectionDirty = true;
        }

        HandleUndoRedo();
        ButtonVisiable();
        UpdateSelectedInfo();

        if (Input.GetKeyDown(KeyCode.F12))
        {
            OutputDebugInfo();
        }

        // 按 R 键启动键旋转（需要选中实键）
        if (Input.GetKeyDown(KeyCode.R))
        {
            TryStartBondRotation();
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

    /// <summary>
    /// 处理原子拖拽：选中原子后按住左键拖动，所有相连原子一起移动。
    /// </summary>
    private void HandleAtomDragging()
    {
        // 开始拖拽：鼠标左键按下，且点击了选中的原子
        if (!isDragging && Input.GetMouseButtonDown(0))
        {
            if (selectedAtom != null)
            {
                Ray ray = cachedCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    if (hit.transform.gameObject == selectedAtom)
                    {
                        isDragging = true;
                        dragStartAtomPos = selectedAtom.transform.position;
                        dragStartMouseWorldPos = GetMouseWorldPosition();

                        // 获取所有相连的原子及其相对偏移
                        draggingConnectedAtoms = dashedBondManager.GetConnectedAtoms(selectedAtom);
                        draggingOffsets = new Vector3[draggingConnectedAtoms.Count];
                        for (int i = 0; i < draggingConnectedAtoms.Count; i++)
                        {
                            draggingOffsets[i] = draggingConnectedAtoms[i].transform.position - dragStartAtomPos;
                        }

                        return;
                    }
                }
            }
        }

        // 拖拽中：鼠标左键按住
        if (isDragging && Input.GetMouseButton(0))
        {
            Vector3 currentMouseWorldPos = GetMouseWorldPosition();
            Vector3 delta = currentMouseWorldPos - dragStartMouseWorldPos;
            Vector3 newSelectedAtomPos = dragStartAtomPos + delta;

            // 移动选中的原子
            selectedAtom.transform.position = newSelectedAtomPos;

            // 移动所有相连的原子（保持相对偏移）
            for (int i = 0; i < draggingConnectedAtoms.Count; i++)
            {
                if (draggingConnectedAtoms[i] != null && draggingConnectedAtoms[i] != selectedAtom)
                {
                    draggingConnectedAtoms[i].transform.position = newSelectedAtomPos + draggingOffsets[i];
                }
            }

            // 更新所有键的 Transform
            dashedBondManager.UpdateAllBondTransforms();

            return;
        }

        // 停止拖拽：鼠标左键释放
        if (isDragging && Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            draggingConnectedAtoms = null;
            draggingOffsets = null;
        }

        // 取消拖拽：按 ESC
        if (isDragging && Input.GetKeyDown(KeyCode.Escape))
        {
            // 恢复初始位置
            selectedAtom.transform.position = dragStartAtomPos;
            for (int i = 0; i < draggingConnectedAtoms.Count; i++)
            {
                if (draggingConnectedAtoms[i] != null && draggingConnectedAtoms[i] != selectedAtom)
                {
                    draggingConnectedAtoms[i].transform.position = dragStartAtomPos + draggingOffsets[i];
                }
            }
            dashedBondManager.UpdateAllBondTransforms();

            isDragging = false;
            draggingConnectedAtoms = null;
            draggingOffsets = null;
        }
    }

    /// <summary>
    /// 将鼠标屏幕坐标投影到 XZ 平面（Y = dragStartAtomPos.y）
    /// </summary>
    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = cachedCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, dragStartAtomPos);
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return dragStartAtomPos;
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
        // 旋转时也需要更新角度显示
        bool isRotating = (bondRotator != null && bondRotator.IsRotating());
        if (!isSelectionDirty && !isRotating) return;
        isSelectionDirty = false;

        StringBuilder sb = new StringBuilder();

        // 旋转时只显示旋转角度，不显示其他信息（包括键类型）
        if (isRotating)
        {
            float angle = bondRotator.GetCurrentAngle();
            string rotationText = LocalizationManager.Instance.GetLocalizedText("rotation_angle");
            sb.Append(string.Format(rotationText, angle.ToString("F1")));
            UIManager.Instance.UpdateSelectedInfo(sb.ToString());
            return;
        }

        // 非旋转时，显示选中信息（选中原子时不显示键类型）
        if (selectedAtom != null)
        {
            Element element = GetElementFromAtom(selectedAtom);
            string elemName = LocalizationManager.Instance.GetLocalizedText($"element_{element.name.ToLower()}");
            sb.Append($"{elemName} ({element.symbol})");
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
        // 虚键生成由 HandleSelection和CheckAndConvertDashedBondsToPreserved负责，此处仅同步缓存。
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

    /// <summary>
    /// 尝试启动键旋转
    /// </summary>
    private void TryStartBondRotation()
    {
        // 检查是否正在旋转中
        if (bondRotator != null && bondRotator.IsRotating())
        {
            bondRotator.StopRotation();
            Debug.Log("[BondRotator] 停止键旋转");
            return;
        }

        GameObject bond = dashedBondManager.selectedDashedBond;
        if (bond == null || !bond.CompareTag("PreservedBond"))
        {
            Debug.LogWarning("[BondRotator] 需要先选中一个实键（PreservedBond）才能旋转");
            return;
        }

        // 如果 bondRotator 未赋值，尝试获取
        if (bondRotator == null)
        {
            bondRotator = GetComponent<BondRotator>();
            if (bondRotator == null)
            {
                bondRotator = gameObject.AddComponent<BondRotator>();
                Debug.Log("[BondRotator] 已自动添加 BondRotator 组件");
            }
        }

        bool success = bondRotator.StartRotation(bond);
        if (success)
        {
            bondRotator.onStopRotation = OnBondRotationStopped;
            Debug.Log("[BondRotator] 键旋转已启动，按住左键拖拽旋转，ESC停止");
        }
        else
        {
            Debug.LogError("[BondRotator] 启动键旋转失败");
        }
    }

    /// <summary>
    /// 键旋转停止时的回调：取消所有选中
    /// </summary>
    private void OnBondRotationStopped()
    {
        Debug.Log("[BondRotator] 旋转停止，取消选中");

        // 取消选中键
        dashedBondManager.UnselectAllBonds();
        UIManager.Instance.UpdateSelectedInfo("");

        // 取消选中原子
        if (selectedAtom != null)
        {
            atomManager.ResetAtomMaterial(selectedAtom);
            selectedAtom = null;
            if (cameraController != null)
                cameraController.ClearSelectedAtom();
            dashedBondManager.ClearDashedBonds();
            isSelectionDirty = true;
        }
    }
}
