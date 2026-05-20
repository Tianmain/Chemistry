/// <summary>
/// 命令接口，定义执行、撤销和有效性检查方法。
/// </summary>
public interface ICommand
{
    /// <summary>
    /// 执行命令。
    /// </summary>
    void Execute();

    /// <summary>
    /// 撤销命令。
    /// </summary>
    void Undo();

    /// <summary>
    /// 检查命令是否有效。
    /// </summary>
    bool IsValid();
}
