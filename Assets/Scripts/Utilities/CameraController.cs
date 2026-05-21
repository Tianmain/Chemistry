using UnityEngine;

/// <summary>
/// 摄像机控制：旋转、平移、缩放。
/// 旋转围绕选中原子或原点，支持通过 SettingsPanel 调整速度。
/// </summary>
public class CameraController : MonoBehaviour
{
    public float rotationSpeed = 300f;
    public float moveSpeed = 10f;
    public float zoomSpeed = 5f;
    public float minZoomDistance = 1f;
    public float maxZoomDistance = 10f;

    private Vector3 targetPosition = Vector3.zero;
    private GameObject selectedAtom = null;

    public float RotationSpeed
    {
        get => rotationSpeed;
        set => rotationSpeed = value;
    }

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = value;
    }

    public float ZoomSpeed
    {
        get => zoomSpeed;
        set => zoomSpeed = value;
    }

    void Update()
    {
        if (SettingsPanel.IsPanelActive) return;

        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            // 选中原子时以原子为中心旋转，否则以原点为中心
            if (selectedAtom != null)
            {
                transform.RotateAround(selectedAtom.transform.position, Vector3.up, mouseX * rotationSpeed * Time.deltaTime);
                transform.RotateAround(selectedAtom.transform.position, transform.right, -mouseY * rotationSpeed * Time.deltaTime);
            }
            else
            {
                transform.RotateAround(Vector3.zero, Vector3.up, mouseX * rotationSpeed * Time.deltaTime);
                transform.RotateAround(Vector3.zero, transform.right, -mouseY * rotationSpeed * Time.deltaTime);
            }
        }

        if (Input.GetMouseButton(2))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            Vector3 move = new Vector3(mouseX, mouseY, 0) * -moveSpeed * Time.deltaTime;
            transform.position += transform.TransformDirection(move);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Vector3 zoom = new Vector3(0, 0, scroll * zoomSpeed);
        float targetDistance = Vector3.Distance(transform.position, targetPosition) - zoom.z;
        if (targetDistance >= minZoomDistance && targetDistance <= maxZoomDistance)
        {
            transform.position += transform.forward * zoom.z;
        }
    }

    public void SetSelectedAtom(GameObject atom)
    {
        selectedAtom = atom;
    }

    public void ClearSelectedAtom()
    {
        selectedAtom = null;
    }
}
