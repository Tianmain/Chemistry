using UnityEngine;

namespace ChemistryEditor.Input
{
    /// <summary>
    /// 移动端摄像机控制器
    /// 支持触摸手势：单指旋转、双指平移、双指捏合缩放
    /// </summary>
    public class MobileCameraController : MonoBehaviour
    {
        [Header("旋转设置")]
        [SerializeField] private float rotationSpeed = 5.0f;
        [SerializeField] private bool invertX = false;
        [SerializeField] private bool invertY = false;

        [Header("平移设置")]
        [SerializeField] private float panSpeed = 0.1f;

        [Header("缩放设置")]
        [SerializeField] private float zoomSpeed = 0.5f;
        [SerializeField] private float minFOV = 10f;
        [SerializeField] private float maxFOV = 120f;

        [Header("目标")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = Vector3.zero;

        private Camera cam;
        private Vector3 lastTouchPosition;
        private Vector2 lastTwoFingerPosition;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
                cam = Camera.main;

            // 如果没有设置目标，创建一个默认的
            if (target == null)
            {
                GameObject targetObj = new GameObject("CameraTarget");
                target = targetObj.transform;
                target.position = Vector3.zero;
            }
        }

        /// <summary>
        /// 单指旋转摄像机（围绕目标）
        /// </summary>
        public void Rotate(Vector2 delta)
        {
            if (cam == null) return;

            float x = delta.x * rotationSpeed * (invertX ? 1 : -1) * Time.deltaTime;
            float y = delta.y * rotationSpeed * (invertY ? -1 : 1) * Time.deltaTime;

            // 围绕目标旋转
            transform.RotateAround(target.position + targetOffset, Vector3.up, x);
            transform.RotateAround(target.position + targetOffset, transform.right, -y);

            // 确保摄像机始终朝向目标
            transform.LookAt(target.position + targetOffset);
        }

        /// <summary>
        /// 双指平移摄像机
        /// </summary>
        public void Pan(Vector2 delta)
        {
            if (cam == null) return;

            // 计算平移向量（在摄像机本地坐标系中）
            Vector3 right = transform.right * -delta.x * panSpeed;
            Vector3 up = transform.up * -delta.y * panSpeed;

            // 移动摄像机和目标
            transform.position += right + up;
            target.position += right + up;
        }

        /// <summary>
        /// 双指捏合缩放
        /// </summary>
        public void Zoom(float delta)
        {
            if (cam == null) return;

            // 使用 Field of View 缩放（透视相机）或正交大小缩放（正交相机）
            if (cam.orthographic)
            {
                cam.orthographicSize = Mathf.Clamp(
                    cam.orthographicSize - delta * zoomSpeed,
                    1f,
                    100f
                );
            }
            else
            {
                cam.fieldOfView = Mathf.Clamp(
                    cam.fieldOfView - delta * zoomSpeed,
                    minFOV,
                    maxFOV
                );
            }
        }

        /// <summary>
        /// 设置摄像机目标点
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// 设置摄像机目标位置
        /// </summary>
        public void SetTargetPosition(Vector3 position)
        {
            if (target != null)
                target.position = position;
        }

        /// <summary>
        /// 获取当前目标位置
        /// </summary>
        public Vector3 GetTargetPosition()
        {
            return target != null ? target.position : Vector3.zero;
        }
    }
}
