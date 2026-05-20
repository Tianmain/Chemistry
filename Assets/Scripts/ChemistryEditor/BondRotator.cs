using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 键旋转功能：选中实键后按 R 键进入旋转模式，
/// 按住左键拖拽旋转，带动原子数较少的那端原子堆一起旋转。
/// </summary>
public class BondRotator : MonoBehaviour
{
    // 当前旋转的键
    private GameObject _selectedBond;
    private Vector3 _bondAxis;        // 键的轴向
    private GameObject _rotateAtom;     // 旋转端的根原子
    private GameObject _fixedAtom;      // 固定端的根原子
    private List<GameObject> _rotateAtoms = new List<GameObject>();
    private List<GameObject> _rotateBonds = new List<GameObject>();

    private bool _isRotating = false;
    private float _currentAngle = 0f;

    // 拖拽状态
    private bool _isDragging = false;
    private Vector3 _dragStartPos;
    private float _dragStartAngle;

    // 引用
    private DashedBondManager _bondManager;
    private AtomManager _atomManager;

    private const float ROTATION_SPEED = 0.3f;
    private const float SNAP_ANGLE = 15f;  // Shift 键吸附角度

    // 旋转停止时触发的事件（用于取消选中）
    public System.Action onStopRotation;

    void Awake()
    {
        _bondManager = GetComponent<DashedBondManager>();
        _atomManager = GetComponent<AtomManager>();
    }

    void Update()
    {
        if (_isRotating)
        {
            HandleRotationInput();
        }
    }

    /// <summary>
    /// 开始旋转指定的键，返回 true 表示成功启动
    /// </summary>
    public bool StartRotation(GameObject bond)
    {
        if (bond == null)
        {
            Debug.LogError("[BondRotator] StartRotation: bond 为 null");
            return false;
        }

        PreservedBond pb = bond.GetComponent<PreservedBond>();
        if (pb == null)
        {
            Debug.LogError("[BondRotator] StartRotation: PreservedBond 组件不存在");
            return false;
        }

        GameObject atom1 = pb.OriginalLinkedAtom;
        GameObject atom2 = pb.OtherLinkedAtom;
        if (atom1 == null || atom2 == null)
        {
            Debug.LogError($"[BondRotator] StartRotation: atom1={atom1}, atom2={atom2}");
            return false;
        }

        // 计算两端的原子堆大小
        HashSet<GameObject> pile1 = GetConnectedAtoms(atom1, atom2);
        HashSet<GameObject> pile2 = GetConnectedAtoms(atom2, atom1);

        Debug.Log($"[BondRotator] 键旋转: {atom1.name} 端 {pile1.Count} 个原子, {atom2.name} 端 {pile2.Count} 个原子");

        // 选择原子数较少的那一端作为旋转端
        if (pile1.Count <= pile2.Count)
        {
            _rotateAtom = atom1;
            _fixedAtom = atom2;
            _rotateAtoms = new List<GameObject>(pile1);
            _rotateBonds = GetBondsInPile(pile1, atom1);
        }
        else
        {
            _rotateAtom = atom2;
            _fixedAtom = atom1;
            _rotateAtoms = new List<GameObject>(pile2);
            _rotateBonds = GetBondsInPile(pile2, atom2);
        }

        // 排除旋转端的根原子（它作为旋转中心，不需要移动）
        _rotateAtoms.Remove(_rotateAtom);

        _selectedBond = bond;
        UpdateBondAxis();

        _isRotating = true;
        _currentAngle = 0f;
        _isDragging = false;

        Debug.Log($"[BondRotator] 旋转端: {_rotateAtom.name}, 固定端: {_fixedAtom.name}, 需旋转 {_rotateAtoms.Count} 个原子。按住左键拖拽旋转，ESC停止。");
        return true;
    }

    /// <summary>
    /// 停止旋转，并触发事件通知外部（如取消选中）
    /// </summary>
    public void StopRotation()
    {
        _isRotating = false;
        _isDragging = false;
        _selectedBond = null;
        _rotateAtoms.Clear();
        _rotateBonds.Clear();
        Debug.Log("[BondRotator] 旋转已停止");

        // 触发停止事件（InputHandler 会订阅此事件来取消选中）
        onStopRotation?.Invoke();
    }

    /// <summary>
    /// BFS 获取与指定原子相连的所有原子（排除 excludeAtom）
    /// </summary>
    private HashSet<GameObject> GetConnectedAtoms(GameObject startAtom, GameObject excludeAtom)
    {
        HashSet<GameObject> visited = new HashSet<GameObject>();
        Queue<GameObject> queue = new Queue<GameObject>();
        visited.Add(startAtom);
        queue.Enqueue(startAtom);

        while (queue.Count > 0)
        {
            GameObject current = queue.Dequeue();
            foreach (var bond in _bondManager.GetAllPreservedBonds())
            {
                if (bond == null) continue;
                PreservedBond pb = bond.GetComponent<PreservedBond>();
                if (pb == null) continue;

                GameObject other = null;
                if (pb.OriginalLinkedAtom == current)
                    other = pb.OtherLinkedAtom;
                else if (pb.OtherLinkedAtom == current)
                    other = pb.OriginalLinkedAtom;

                if (other != null && !visited.Contains(other) && other != excludeAtom)
                {
                    visited.Add(other);
                    queue.Enqueue(other);
                }
            }
        }
        return visited;
    }

    /// <summary>
    /// 获取原子堆中所有相关的键
    /// </summary>
    private List<GameObject> GetBondsInPile(HashSet<GameObject> pile, GameObject rootAtom)
    {
        List<GameObject> bonds = new List<GameObject>();
        foreach (var bond in _bondManager.GetAllPreservedBonds())
        {
            if (bond == null) continue;
            PreservedBond pb = bond.GetComponent<PreservedBond>();
            if (pb == null) continue;

            GameObject a1 = pb.OriginalLinkedAtom;
            GameObject a2 = pb.OtherLinkedAtom;

            bool inPile1 = pile.Contains(a1) || a1 == rootAtom;
            bool inPile2 = pile.Contains(a2) || a2 == rootAtom;

            if (inPile1 && inPile2 && !bonds.Contains(bond))
                bonds.Add(bond);
        }
        return bonds;
    }

    private void UpdateBondAxis()
    {
        if (_selectedBond == null) return;
        PreservedBond pb = _selectedBond.GetComponent<PreservedBond>();
        if (pb == null) return;

        Vector3 pos1 = pb.OriginalLinkedAtom.transform.position;
        Vector3 pos2 = pb.OtherLinkedAtom.transform.position;
        _bondAxis = (pos2 - pos1).normalized;
    }

    /// <summary>
    /// 处理旋转输入：按住左键拖拽旋转
    /// 按住 Shift 键时，角度吸附到 15° 倍数
    /// 由 InputHandler.Update() 在旋转模式下调用
    /// </summary>
    public void HandleRotationInput()
    {
        // 按住左键开始拖拽
        if (Input.GetMouseButtonDown(0))
        {
            _isDragging = true;
            _dragStartPos = Input.mousePosition;
            _dragStartAngle = _currentAngle;
        }

        // 左键按住并拖拽：应用旋转
        if (Input.GetMouseButton(0) && _isDragging)
        {
            Vector3 delta = Input.mousePosition - _dragStartPos;
            float angleDelta = delta.x * ROTATION_SPEED;
            float targetAngle = _dragStartAngle + angleDelta;

            // Shift 按下时，吸附到 15° 倍数
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shiftHeld)
            {
                float snapped = Mathf.Round(targetAngle / SNAP_ANGLE) * SNAP_ANGLE;
                // 只在角度实际变化时才应用，避免抖动
                if (Mathf.Abs(snapped - _currentAngle) > 0.01f)
                {
                    float applyDelta = snapped - _currentAngle;
                    ApplyRotation(applyDelta);
                    _currentAngle = snapped;
                }
            }
            else
            {
                ApplyRotation(targetAngle - _currentAngle);
                _currentAngle = targetAngle;
            }
        }

        // 左键松开：重置拖拽起点，支持连续旋转
        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
            _dragStartPos = Input.mousePosition;
            _dragStartAngle = _currentAngle;
        }

        // ESC 退出旋转模式
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StopRotation();
        }
    }

    /// <summary>
    /// 应用旋转角度到原子堆
    /// </summary>
    private void ApplyRotation(float angleDelta)
    {
        if (_rotateAtom == null) return;

        Vector3 pivot = _rotateAtom.transform.position;

        foreach (var atom in _rotateAtoms)
        {
            if (atom == null) continue;
            atom.transform.RotateAround(pivot, _bondAxis, angleDelta);
        }

        foreach (var bond in _rotateBonds)
        {
            if (bond == null) continue;
            UpdateBondTransform(bond);
        }

        // 旋转后更新轴方向
        UpdateBondAxis();
    }

    /// <summary>
    /// 更新键的 Transform（位置、旋转、缩放）
    /// </summary>
    private void UpdateBondTransform(GameObject bond)
    {
        DashedBondLink link = bond.GetComponent<DashedBondLink>();
        PreservedBond pb = bond.GetComponent<PreservedBond>();
        if (link == null || pb == null) return;

        GameObject atom1 = pb.OriginalLinkedAtom;
        GameObject atom2 = pb.OtherLinkedAtom;
        if (atom1 == null || atom2 == null) return;

        Vector3 start = atom1.transform.position;
        Vector3 end = atom2.transform.position;
        Vector3 direction = (end - start).normalized;
        float length = Vector3.Distance(start, end);

        bond.transform.position = start + direction * length * 0.5f;
        bond.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        bond.transform.localScale = new Vector3(0.1f, length / 2, 0.1f);

        link.endPosition = end;
    }

    public bool IsRotating() => _isRotating;
    public GameObject GetSelectedBond() => _selectedBond;
    public float GetCurrentAngle() => _currentAngle;
}
