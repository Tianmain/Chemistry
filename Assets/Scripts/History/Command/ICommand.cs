/// <summary>
/// 命令接口，定义执行、撤销和有效性检查方法
/// </summary>
public interface ICommand
{
    // 执行命令
    void Execute();

    // 撤销命令
    void Undo();

    // 检查命令是否有效
    bool IsValid();
}
