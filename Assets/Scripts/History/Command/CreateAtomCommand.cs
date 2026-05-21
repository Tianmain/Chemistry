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

    // 创建原子
    public void Execute()
    {
        if (createdAtom == null)
        {
            createdAtom = atomManager.CreateAtom(position, element);
            wasExecuted = true;
            //Debug.Log($"创建原子 {element.name}，ID: {createdAtom?.GetInstanceID()}");
        }
    }

    // 删除原子
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

    // 检查命令是否有效
    public bool IsValid()
    {
        return !atomManager.CheckAtomOverlap(position);
    }

    // 获取已创建的原子
    public GameObject GetCreatedAtom()
    {
        return createdAtom;
    }
}
