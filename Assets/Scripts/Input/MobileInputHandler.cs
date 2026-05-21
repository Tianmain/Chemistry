using UnityEngine;
using System.Collections.Generic;

namespace ChemistryEditor.Input
{
    public class MobileInputHandler : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private ChemistryEditor chemistryEditor;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private MobileCameraController cameraController;
        [SerializeField] private DashedBondManager dashedBondManager;
        [SerializeField] private AtomManager atomManager;
        [SerializeField] private MaterialManager materialManager;
        [SerializeField] private HistoryManager historyManager;

        [Header("触摸设置")]
        [SerializeField] private float touchDragThreshold = 10f;
        [SerializeField] private float doubleTapTimeout = 0.4f;

        private GameObject selectedAtom = null;
        private GameObject firstBondAtom = null;
        private bool isCreatingBond = false;
        private bool isDraggingAtom = false;
        private Vector3 dragOffset;
        private List<GameObject> connectedAtoms = new List<GameObject>();

        private float lastTapTime = 0f;
        private int lastTapTouchId = -1;

        private bool isPinching = false;
        private float lastPinchDistance = 0f;
        private bool isTwoFingerDragging = false;
        private Vector2 lastTwoFingerPosition;

        private Plane dragPlane;
        private int selectedBondType = 1;

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (chemistryEditor == null)
                chemistryEditor = FindObjectOfType<ChemistryEditor>();

            if (uiManager == null)
                uiManager = FindObjectOfType<UIManager>();

            if (cameraController == null)
                cameraController = FindObjectOfType<MobileCameraController>();

            if (dashedBondManager == null)
                dashedBondManager = FindObjectOfType<DashedBondManager>();

            if (atomManager == null)
                atomManager = FindObjectOfType<AtomManager>();

            if (materialManager == null)
                materialManager = FindObjectOfType<MaterialManager>();

            if (historyManager == null)
                historyManager = FindObjectOfType<HistoryManager>();

            if (cameraController == null && mainCamera != null)
            {
                cameraController = mainCamera.gameObject.AddComponent<MobileCameraController>();
            }

            UpdateDragPlane();
        }

        private void Update()
        {
#if UNITY_IOS || UNITY_ANDROID
            HandleTouchInput();
#endif
        }

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
        }

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

        private void HandleTouchBegan(Touch touch)
        {
            if (IsTouchOverUI(touch))
                return;

            Ray ray = mainCamera.ScreenPointToRay(touch.position);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                GameObject hitObj = hit.collider.gameObject;

                if (hitObj.CompareTag("Atom"))
                {
                    HandleAtomTouch(hitObj, touch);
                }
                else if (hitObj.CompareTag("DashedBond"))
                {
                    HandleDashedBondTouch(hitObj);
                }
                else if (hitObj.CompareTag("PreservedBond"))
                {
                    HandleBondTouch(hitObj);
                }
            }
            else
            {
                ClearSelection();
            }
        }

        private void HandleAtomTouch(GameObject atom, Touch touch)
        {
            float timeSinceLastTap = Time.time - lastTapTime;

            if (timeSinceLastTap < doubleTapTimeout && lastTapTouchId == touch.fingerId)
            {
                DeleteAtom(atom);
                lastTapTime = 0f;
                return;
            }

            lastTapTime = Time.time;
            lastTapTouchId = touch.fingerId;

            if (isCreatingBond && firstBondAtom != null && firstBondAtom != atom)
            {
                CreateBond(firstBondAtom, atom);
                firstBondAtom = null;
                isCreatingBond = false;
                return;
            }

            SelectAtom(atom);
        }

        private void HandleDashedBondTouch(GameObject dashedBond)
        {
            DashedBondLink link = dashedBond.GetComponent<DashedBondLink>();
            if (link != null && link.linkedAtom != null)
            {
                SelectAtom(link.linkedAtom);
                if (dashedBondManager != null)
                    dashedBondManager.HighlightBond(dashedBond);
            }
        }

        private void HandleBondTouch(GameObject bond)
        {
            if (dashedBondManager != null)
                dashedBondManager.HighlightBond(bond);
        }

        private void HandleTouchMoved(Touch touch)
        {
            Vector2 delta = touch.deltaPosition;
            float moveDistance = delta.magnitude;

            if (isDraggingAtom && selectedAtom != null)
            {
                DragAtom(touch.position);
                return;
            }

            if (moveDistance > touchDragThreshold && selectedAtom != null && !isDraggingAtom)
            {
                isDraggingAtom = true;
                CalculateDragOffset(selectedAtom, touch.position);
                return;
            }

            if (moveDistance > touchDragThreshold && selectedAtom == null)
            {
                if (cameraController != null)
                {
                    cameraController.Rotate(delta);
                }
            }
        }

        private void HandleTouchEnded(Touch touch)
        {
            if (isDraggingAtom)
            {
                isDraggingAtom = false;
                connectedAtoms.Clear();

                if (dashedBondManager != null)
                    dashedBondManager.UpdateAllBondTransforms();
            }
        }

        private void HandleTwoFingerTouch(Touch touch0, Touch touch1)
        {
            float currentDistance = Vector2.Distance(touch0.position, touch1.position);

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                lastPinchDistance = currentDistance;
                lastTwoFingerPosition = (touch0.position + touch1.position) * 0.5f;
                isPinching = true;
                isTwoFingerDragging = true;
                return;
            }

            if (isPinching && touch0.phase == TouchPhase.Moved && touch1.phase == TouchPhase.Moved)
            {
                float deltaDistance = currentDistance - lastPinchDistance;
                if (cameraController != null)
                {
                    cameraController.Zoom(deltaDistance * 0.1f);
                }
                lastPinchDistance = currentDistance;
            }

            if (isTwoFingerDragging && touch0.phase == TouchPhase.Moved && touch1.phase == TouchPhase.Moved)
            {
                Vector2 currentTwoFingerPos = (touch0.position + touch1.position) * 0.5f;
                Vector2 delta = currentTwoFingerPos - lastTwoFingerPosition;

                if (delta.magnitude > touchDragThreshold)
                {
                    if (cameraController != null)
                    {
                        cameraController.Pan(delta);
                    }
                }

                lastTwoFingerPosition = currentTwoFingerPos;
            }

            if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended ||
                touch0.phase == TouchPhase.Canceled || touch1.phase == TouchPhase.Canceled)
            {
                isPinching = false;
                isTwoFingerDragging = false;
            }
        }

        private void SelectAtom(GameObject atom)
        {
            ClearSelection();

            selectedAtom = atom;
            firstBondAtom = atom;
            isCreatingBond = true;

            HighlightAtom(atom, true);

            if (cameraController != null)
            {
                cameraController.SetTargetPosition(atom.transform.position);
            }

            if (dashedBondManager != null)
            {
                int maxBondCount = GetMaxBondCount(atom);
                dashedBondManager.UpdateDashedBonds(atom, selectedBondType, maxBondCount);
            }
        }

        private void HighlightAtom(GameObject atom, bool highlight)
        {
            if (atom == null || materialManager == null)
                return;

            if (highlight)
            {
                if (materialManager.highlightedMaterial != null)
                {
                    atom.GetComponent<Renderer>().material = materialManager.highlightedMaterial;
                }
            }
            else
            {
                Material mat = materialManager.GetElementMaterial(atom.name);
                if (mat != null)
                {
                    atom.GetComponent<Renderer>().material = mat;
                }
            }
        }

        private void DragAtom(Vector2 screenPosition)
        {
            if (selectedAtom == null)
                return;

            if (GetDragPlanePosition(screenPosition, out Vector3 targetPosition))
            {
                targetPosition += dragOffset;

                Vector3 delta = targetPosition - selectedAtom.transform.position;
                selectedAtom.transform.position = targetPosition;

                foreach (GameObject connectedAtom in connectedAtoms)
                {
                    if (connectedAtom != null && connectedAtom != selectedAtom)
                    {
                        connectedAtom.transform.position += delta;
                    }
                }
            }
        }

        private void CalculateDragOffset(GameObject atom, Vector2 screenPosition)
        {
            UpdateDragPlane();

            if (GetDragPlanePosition(screenPosition, out Vector3 planePosition))
            {
                dragOffset = atom.transform.position - planePosition;
            }

            if (dashedBondManager != null)
            {
                connectedAtoms = dashedBondManager.GetConnectedAtoms(atom);
            }
        }

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

        private void UpdateDragPlane()
        {
            if (mainCamera == null)
                return;

            dragPlane = new Plane(-mainCamera.transform.forward, mainCamera.transform.position);
        }

        private void DeleteAtom(GameObject atom)
        {
            if (chemistryEditor != null)
                chemistryEditor.SaveHistoryState();

            if (atomManager != null)
                atomManager.DeleteAtom(atom);

            ClearSelection();
        }

        private void CreateBond(GameObject atom1, GameObject atom2)
        {
            if (chemistryEditor != null)
                chemistryEditor.SaveHistoryState();

            if (dashedBondManager != null)
            {
                dashedBondManager.CreateAutoPreservedBond(atom1, atom2, selectedBondType);
            }
        }

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

            if (dashedBondManager != null)
                dashedBondManager.ClearDashedBonds();
        }

        private int GetMaxBondCount(GameObject atom)
        {
            if (atom == null)
                return 0;

            AtomData atomData = atom.GetComponent<AtomData>();
            if (atomData != null && atomData.element != null)
                return atomData.element.maxBondCount;

            return 0;
        }

        private bool IsTouchOverUI(Touch touch)
        {
#if UNITY_IOS || UNITY_ANDROID
            return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId);
#else
            return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
#endif
        }

        public void SetSelectedElement(int elementIndex)
        {
            // TODO: 根据 elementIndex 创建对应元素
        }

        public void SetBondType(int bondType)
        {
            selectedBondType = bondType;

            if (selectedAtom != null && dashedBondManager != null)
            {
                int maxBondCount = GetMaxBondCount(selectedAtom);
                dashedBondManager.UpdateDashedBonds(selectedAtom, selectedBondType, maxBondCount);
            }
        }

        public void Undo()
        {
            if (historyManager != null)
                historyManager.Undo();
        }

        public void Redo()
        {
            if (historyManager != null)
                historyManager.Redo();
        }
    }
}
