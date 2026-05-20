using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 历史管理器，实现撤销/重做功能。
/// 维护撤销栈和重做栈，最大容量 50 条。
/// </summary>
public class HistoryManager : MonoBehaviour
{
    public Stack<ICommand> undoStack = new Stack<ICommand>();
    public Stack<ICommand> redoStack = new Stack<ICommand>();

    private const int MaxHistorySize = 50;

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
            try { command.Execute(); }
            catch { }
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
        // Stack 无法直接访问底部，需转为 List 移除最旧的命令
        while (stack.Count > MaxHistorySize)
        {
            var tempList = new List<ICommand>(stack);
            tempList.RemoveAt(0);
            stack.Clear();
            for (int i = tempList.Count - 1; i >= 0; i--)
                stack.Push(tempList[i]);
        }
    }

    private void NotifyHistoryChanged()
    {
        OnHistoryChanged?.Invoke(undoStack.Count, redoStack.Count);
    }
}
