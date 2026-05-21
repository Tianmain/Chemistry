using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 历史管理器，实现撤销/重做功能。
/// 维护撤销栈和重做栈，最大容量可在 Inspector 中配置。
/// </summary>
public class HistoryManager : MonoBehaviour
{
    public Stack<ICommand> undoStack = new Stack<ICommand>();
    public Stack<ICommand> redoStack = new Stack<ICommand>();

    [SerializeField] private int maxHistorySize = 50;
    public int MaxHistorySize => maxHistorySize;

    public event System.Action<int, int> OnHistoryChanged;

    public void ExecuteCommand(ICommand command)
    {
        if (command == null)
        {
            Debug.LogWarning("空命令，未记录到历史");
            return;
        }

        if (!command.IsValid())
        {
            Debug.LogWarning("无效命令，未记录到历史");
            return;
        }

        try
        {
            command.Execute();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"命令执行失败: {e.Message}");
            return;
        }

        undoStack.Push(command);
        redoStack.Clear();
        TrimStack(undoStack);

        Debug.Log($"命令记录成功，当前撤销栈数量: {undoStack.Count}");
        NotifyHistoryChanged();
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
        {
            Debug.LogWarning("无操作可撤销");
            return;
        }

        ICommand command = undoStack.Pop();

        try
        {
            command.Undo();
            redoStack.Push(command);
            Debug.Log($"撤销成功，剩余可撤销操作：{undoStack.Count}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"撤销失败: {e.Message}");
            try
            {
                command.Execute();
                Debug.LogWarning("撤销失败，但已成功恢复执行");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"撤销失败且无法恢复执行: {ex.Message}");
            }
        }

        NotifyHistoryChanged();
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            Debug.LogWarning("无操作可重做");
            return;
        }

        ICommand command = redoStack.Pop();

        try
        {
            command.Execute();
            undoStack.Push(command);
            Debug.Log($"重做成功");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"重做失败: {e.Message}");
        }

        NotifyHistoryChanged();
    }

    public bool CanUndo() => undoStack.Count > 0;
    public bool CanRedo() => redoStack.Count > 0;
    public int GetUndoCount() => undoStack.Count;
    public int GetRedoCount() => redoStack.Count;

    public void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
        NotifyHistoryChanged();
        Debug.Log("历史记录已清空");
    }

    private void TrimStack(Stack<ICommand> stack)
    {
        if (stack.Count <= maxHistorySize)
            return;

        // 使用两个临时栈来移除最旧的命令，避免 List 分配
        // Stack 是 LIFO，最旧的在底部，最新的在顶部
        // 步骤1：将栈中元素反转（现在最新的在底部，最旧的在顶部）
        Stack<ICommand> tempStack = new Stack<ICommand>();
        while (stack.Count > 0)
            tempStack.Push(stack.Pop());

        // 步骤2：弹出多余的命令（现在顶部是最旧的）
        int removeCount = tempStack.Count - maxHistorySize;
        for (int i = 0; i < removeCount; i++)
            tempStack.Pop();

        // 步骤3：再次反转，将保留的命令压回原栈（现在最新的在顶部）
        while (tempStack.Count > 0)
            stack.Push(tempStack.Pop());
    }

    private void NotifyHistoryChanged()
    {
        OnHistoryChanged?.Invoke(undoStack.Count, redoStack.Count);
    }
}
