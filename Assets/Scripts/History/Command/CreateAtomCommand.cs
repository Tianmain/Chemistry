using UnityEngine;

public class CreateAtomCommand : ICommand
{
    private AtomManager atomManager;
    private Element element;
    private Vector3 position;
    private GameObject createdAtom;
    private bool wasExecuted;

    public CreateAtomCommand(AtomManager manager, Element element, Vector3 position)
    {
        this.atomManager = manager;
        this.element = element;
        this.position = position;
        this.createdAtom = null;
        this.wasExecuted = false;
    }

    /// <summary>
    /// 执行命令：创建原子。
    /// </summary>
    public void Execute()
    {
        if (createdAtom == null)
        {
            createdAtom = atomManager.CreateAtom(position, element);
            wasExecuted = true;
            Debug.Log($"创建原子 {element.name}，ID: {createdAtom?.GetInstanceID()}");
        }
    }

    /// <summary>
    /// 撤销命令：删除原子。
    /// </summary>
    public void Undo()
    {
        if (createdAtom != null && wasExecuted)
        {
            atomManager.DeleteAtom(createdAtom);
            Debug.Log($"撤销原子 {element.name}，ID: {createdAtom.GetInstanceID()}");
            createdAtom = null;
            wasExecuted = false;
        }
    }

    /// <summary>
    /// 检查命令是否有效（位置未被占用）。
    /// </summary>
    public bool IsValid()
    {
        return !atomManager.CheckAtomOverlap(position);
    }

    /// <summary>
    /// 获取已创建的原子（供外部使用）。
    /// </summary>
    public GameObject GetCreatedAtom()
    {
        return createdAtom;
    }
}
