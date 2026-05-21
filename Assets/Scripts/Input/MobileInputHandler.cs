using UnityEngine;
using System.Collections.Generic;

namespace ChemistryEditor.Input
{
    /// <summary>
    /// 移动端触摸输入处理器
    /// 专门用于处理触摸屏输入，与 InputHandler.cs 独立并存
    /// </summary>
    public class MobileInputHandler : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private ChemistryEditor chemistryEditor;
        [SerializeField] private UIManager uiManager;

        [Header("触摸设置")]
        [SerializeField] private float touchDragThreshold = 10f;  // 触摸拖动阈值（像素）
        [SerializeField] private float tapTimeout = 0.3f;         // 点击超时时间（秒）
        [SerializeField] private float doubleTapTimeout = 0.4f;    // 双击超时时间（秒）

        [Header("摄像机控制")]
        [SerializeField] private float pinchZoomSpeed = 0.5f;     // 双指缩放速度
        [SerializeField] private float twoFingerDragSpeed = 0.1f; // 双指拖拽速度
        [SerializeField] private float rotationSpeed = 0.2f;      // 旋转速度

        // 触摸状态
        private int selectedElement = 0;
        private GameObject selectedAtom = null;
        private GameObject firstBondAtom = null;  // 化学键第一个原子
        private bool isCreatingBond = false;
        private bool isDraggingAtom = false;
        private bool isRotatingView = false;
        private Vector3 dragOffset;
        private List<GameObject> connectedAtoms = new List<GameObject>();

        // 触摸状态追踪
        private float lastTapTime = 0f;
        private int lastTapTouchId = -1;
        private Vector2 lastTouchPosition;
        private Vector2 lastTwoFingerPos;

        // 双指触摸状态
        private bool isPinching = false;
        private float lastPinchDistance = 0f;
        private bool isTwoFingerDragging = false;

        // 拖动平面（与摄像机垂直的平面）
        private Plane dragPlane;

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (chemistryEditor == null)
                chemistryEditor = FindObjectOfType<ChemistryEditor>();

            if (uiManager == null)
                uiManager = FindObjectOfType<UIManager>();

            // 初始化拖动平面（与摄像机垂直）
            UpdateDragPlane();
        }

        private void Update()
        {
            // 只在移动平台或编辑器中选择性启用
#if UNITY_IOS || UNITY_ANDROID
            HandleTouchInput();
#else
            // 在编辑器中也可以用鼠标模拟触摸（可选）
            // HandleMouseAsTouch();
#endif
        }

        /// <summary>
        /// 处理触摸输入
        /// </summary>
        private void HandleTouchInput()
        {
            int touchCount = Input.touchCount;

            if (touchCount == 1)
            {
                HandleSingleTouch(Input.GetTouch(0));
            }
            else if (touchCount == 2)
            {
                HandleTwoFingerTouch(Input.GetTouch(0), Input.GetTouch(1));
            }
            else
            {
                // 手指离开屏幕，重置状态
                ResetTouchState();
            }

            UpdateDragPlane();
        }

        /// <summary>
        /// 处理单指触摸
        /// </summary>
        private void HandleSingleTouch(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    HandleTouchBegan(touch);
                    break;

                case TouchPhase.Moved:
                    HandleTouchMoved(touch);
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    HandleTouchEnded(touch);
                    break;
            }
        }

        /// <summary>
        /// 触摸开始
        /// </summary>
        private void HandleTouchBegan(Touch touch)
        {
            // 检查是否点击到 UI
            if (IsTouchOverUI(touch))
                return;

            // 射线检测
            Ray ray = mainCamera.ScreenPointToRay(touch.position);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                GameObject hitObj = hit.collider.gameObject;

                // 检查是否点击到原子
                if (hitObj.CompareTag("Atom"))
                {
                    lastTouchPosition = touch.position;
                    float timeSinceLastTap = Time.time - lastTapTime;

                    // 双击检测
                    if (timeSinceLastTap < doubleTapTimeout && lastTapTouchId == touch.fingerId)
                    {
                        // 双击：删除原子
                        DeleteAtom(hitObj);
                        lastTapTime = 0f;
                    }
                    else
                    {
                        // 单击：选择原子
                        SelectAtom(hitObj);
                        lastTapTime = Time.time;
                        lastTapTouchId = touch.fingerId;
                    }
                }
                // 检查是否点击到化学键
                else if (hitObj.CompareTag("Bond"))
                {
                    SelectBond(hitObj);
                }
            }
            else
            {
                // 没有点击到物体，取消选择
                ClearSelection();
            }
        }

        /// <summary>
        /// 触摸移动
        /// </summary>
        private void HandleTouchMoved(Touch touch)
        {
            // 计算移动距离
            Vector2 delta = touch.deltaPosition;
            float moveDistance = delta.magnitude;

            // 如果已经在拖动原子
            if (isDraggingAtom && selectedAtom != null)
            {
                DragAtom(touch.position);
                return;
            }

            // 如果移动距离超过阈值，开始拖动
            if (moveDistance > touchDragThreshold && selectedAtom != null)
            {
                isDraggingAtom = true;
                CalculateDragOffset(selectedAtom, touch.position);
            }
            // 如果没有选中原子，单指移动旋转视角
            else if (moveDistance > touchDragThreshold && selectedAtom == null)
            {
                RotateView(delta);
            }
        }

        /// <summary>
        /// 触摸结束
        /// </summary>
        private void HandleTouchEnded(Touch touch)
        {
            // 如果只是点击（没有拖动）
            if (!isDraggingAtom && touch.deltaPosition.magnitude < touchDragThreshold)
            {
                // 处理点击逻辑（已在 Began 中处理）
            }

            // 停止拖动
            if (isDraggingAtom)
            {
                isDraggingAtom = false;
                connectedAtoms.Clear();
            }
        }

        /// <summary>
        /// 处理双指触摸（缩放、拖拽）
        /// </summary>
        private void HandleTwoFingerTouch(Touch touch0, Touch touch1)
        {
            // 计算两指距离
            float currentDistance = Vector2.Distance(touch0.position, touch1.position);

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                // 开始双指操作
                lastPinchDistance = currentDistance;
                lastTwoFingerPos = (touch0.position + touch1.position) * 0.5f;
                isPinching = true;
                isTwoFingerDragging = true;
                return;
            }

            // 缩放
            if (isPinching && touch0.phase == TouchPhase.Moved && touch1.phase == TouchPhase.Moved)
            {
                float deltaDistance = currentDistance - lastPinchDistance;
                ZoomCamera(deltaDistance * pinchZoomSpeed);
                lastPinchDistance = currentDistance;
            }

            // 双指拖拽（移动视角）
            if (isTwoFingerDragging && touch0.phase == TouchPhase.Moved && touch1.phase == TouchPhase.Moved)
            {
                Vector2 currentTwoFingerPos = (touch0.position + touch1.position) * 0.5f;
                Vector2 delta = currentTwoFingerPos - lastTwoFingerPos;

                if (delta.magnitude > touchDragThreshold)
                {
                    PanView(delta);
                }

                lastTwoFingerPos = currentTwoFingerPos;
            }

            // 手指离开
            if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended ||
                touch0.phase == TouchPhase.Canceled || touch1.phase == TouchPhase.Canceled)
            {
                isPinching = false;
                isTwoFingerDragging = false;
            }
        }

        /// <summary>
        /// 选择原子
        /// </summary>
        private void SelectAtom(GameObject atom)
        {
            // 如果正在创建化学键
            if (isCreatingBond && firstBondAtom != null && firstBondAtom != atom)
            {
                // 创建化学键
                CreateBond(firstBondAtom, atom);
                firstBondAtom = null;
                isCreatingBond = false;
                return;
            }

            // 选择新原子
            selectedAtom = atom;
            firstBondAtom = atom;
            isCreatingBond = true;

            // 高亮显示
            HighlightAtom(atom, true);

            // 更新 UI
            if (uiManager != null)
            {
                string elementName = AtomManager.Instance.GetElementFromAtom(atom);
                uiManager.OnAtomSelected(atom, elementName);
            }

            // 记录历史
            if (chemistryEditor != null)
                chemistryEditor.SaveHistoryState();
        }

        /// <summary>
        /// 选择化学键
        /// </summary>
        private void SelectBond(GameObject bond)
        {
            // TODO: 实现化学键选择逻辑
            // 可以参考 InputHandler.cs 中的实现
        }

        /// <summary>
        /// 拖动原子
        /// </summary>
        private void DragAtom(Vector2 screenPosition)
        {
            if (selectedAtom == null)
                return;

            // 计算目标位置（在与摄像机垂直的平面上）
            if (GetDragPlanePosition(screenPosition, out Vector3 targetPosition))
            {
                // 应用偏移
                targetPosition += dragOffset;

                // 移动选中的原子和所有连接的原子
                Vector3 delta = targetPosition - selectedAtom.transform.position;
                selectedAtom.transform.position = targetPosition;

                // 移动所有连接的原子
                foreach (GameObject connectedAtom in connectedAtoms)
                {
                    if (connectedAtom != null && connectedAtom != selectedAtom)
                    {
                        connectedAtom.transform.position += delta;
                    }
                }

                // 更新所有化学键的位置
                DashedBondManager.Instance.UpdateAllBondTransforms();
            }
        }

        /// <summary>
        /// 计算拖动偏移
        /// </summary>
        private void CalculateDragOffset(GameObject atom, Vector2 screenPosition)
        {
            // 获取与摄像机垂直的平面
            UpdateDragPlane();

            // 计算原子当前位置在平面上的投影点
            if (GetDragPlanePosition(screenPosition, out Vector3 planePosition))
            {
                dragOffset = atom.transform.position - planePosition;
            }

            // 获取所有连接的原子
            connectedAtoms = DashedBondManager.Instance.GetConnectedAtomsExcluding(
                atom,
                null  // 不需要排除特定原子
            );
        }

        /// <summary>
        /// 获取拖动平面上的位置
        /// </summary>
        private bool GetDragPlanePosition(Vector2 screenPosition, out Vector3 position)
        {
            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (dragPlane.Raycast(ray, out float enter))
            {
                position = ray.GetPoint(enter);
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        /// <summary>
        /// 更新拖动平面（与摄像机垂直）
        /// </summary>
        private void UpdateDragPlane()
        {
            if (mainCamera == null)
                return;

            // 平面法线为摄像机 forward 方向，平面经过摄像机位置
            dragPlane = new Plane(-mainCamera.transform.forward, mainCamera.transform.position);
        }

        /// <summary>
        /// 旋转视角（单指）
        /// </summary>
        private void RotateView(Vector2 delta)
        {
            // TODO: 实现视角旋转逻辑
            // 可以参考 CameraController.cs 或使用现有摄像机控制脚本
        }

        /// <summary>
        /// 平移视角（双指）
        /// </summary>
        private void PanView(Vector2 delta)
        {
            // TODO: 实现视角平移逻辑
        }

        /// <summary>
        /// 缩放摄像机（双指捏合）
        /// </summary>
        private void ZoomCamera(float delta)
        {
            // TODO: 实现缩放逻辑
            // 可以调整摄像机 fieldOfView 或位置
        }

        /// <summary>
        /// 删除原子
        /// </summary>
        private void DeleteAtom(GameObject atom)
        {
            if (chemistryEditor != null)
                chemistryEditor.SaveHistoryState();

            AtomManager.Instance.DeleteAtom(atom);

            ClearSelection();
        }

        /// <summary>
        /// 创建化学键
        /// </summary>
        private void CreateBond(GameObject atom1, GameObject atom2)
        {
            if (chemistryEditor != null)
                chemistryEditor.SaveHistoryState();

            // TODO: 获取当前选择的键类型
            int bondType = 0;  // 默认单键

            DashedBondManager.Instance.CreateAutoPreservedBond(atom1, atom2, bondType);
        }

        /// <summary>
        /// 高亮原子
        /// </summary>
        private void HighlightAtom(GameObject atom, bool highlight)
        {
            // TODO: 实现高亮逻辑
            // 可以参考 InputHandler.cs 中的实现
        }

        /// <summary>
        /// 取消选择
        /// </summary>
        private void ClearSelection()
        {
            if (selectedAtom != null)
            {
                HighlightAtom(selectedAtom, false);
                selectedAtom = null;
            }

            firstBondAtom = null;
            isCreatingBond = false;
            isDraggingAtom = false;
            connectedAtoms.Clear();

            if (uiManager != null)
                uiManager.OnSelectionCleared();
        }

        /// <summary>
        /// 检查触摸是否作用在 UI 上
        /// </summary>
        private bool IsTouchOverUI(Touch touch)
        {
#if UNITY_IOS || UNITY_ANDROID
            return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId);
#else
            return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
#endif
        }

        /// <summary>
        /// 重置触摸状态
        /// </summary>
        private void ResetTouchState()
        {
            isPinching = false;
            isTwoFingerDragging = false;
        }

        /// <summary>
        /// 设置选择的元素（供 UI 按钮调用）
        /// </summary>
        public void SetSelectedElement(int elementIndex)
        {
            selectedElement = elementIndex;
        }

        /// <summary>
        /// 设置化学键类型（供 UI 按钮调用）
        /// </summary>
        public void SetBondType(int bondType)
        {
            // TODO: 实现设置键类型逻辑
        }
    }
}
