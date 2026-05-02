using UnityEngine;

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
