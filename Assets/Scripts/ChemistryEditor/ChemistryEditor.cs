using UnityEngine;

/// <summary>
/// 化学编辑器入口，初始化 UI 文本。
/// </summary>
public class ChemistryEditor : MonoBehaviour
{
    [SerializeField] private AtomManager atomManager;
    [SerializeField] private DashedBondManager dashedBondManager;
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private UIManager uiManager;

    void Start()
    {
        uiManager.UpdateKeyMapText();
    }

    void Update()
    {
    }
}
