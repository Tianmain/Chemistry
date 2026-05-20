using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 设置面板：语言切换、旋转/移动/缩放速度调节。
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private Slider rotationSlider;
    [SerializeField] private Slider moveSlider;
    [SerializeField] private Slider zoomSlider;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private Image backgroundMask;

    public static bool IsPanelActive { get; private set; }

    public void Show()
    {
        gameObject.SetActive(true);
        backgroundMask.raycastTarget = true;
        IsPanelActive = true;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        backgroundMask.raycastTarget = false;
        IsPanelActive = false;
    }

    private void Start()
    {
        languageDropdown.value = (int)LocalizationManager.Instance.CurrentLanguage;

        rotationSlider.value = cameraController.rotationSpeed;
        moveSlider.value = cameraController.moveSpeed;
        zoomSlider.value = cameraController.zoomSpeed;

        rotationSlider.onValueChanged.AddListener(OnRotationSpeedChanged);
        moveSlider.onValueChanged.AddListener(OnMoveSpeedChanged);
        zoomSlider.onValueChanged.AddListener(OnZoomSpeedChanged);
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    private void OnRotationSpeedChanged(float value) => cameraController.rotationSpeed = value;
    private void OnMoveSpeedChanged(float value) => cameraController.moveSpeed = value;
    private void OnZoomSpeedChanged(float value) => cameraController.zoomSpeed = value;

    private void OnLanguageChanged(int index)
    {
        LocalizationManager.Instance.SetLanguage((Language)index);
    }
}
