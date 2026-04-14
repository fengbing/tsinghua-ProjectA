/// <summary>
/// 立面救援小游戏是否占用全屏输入；供 <see cref="GameUi"/> 等与具体控制器类型解耦。
/// </summary>
public static class FacadeRescueSessionState
{
    public static bool IsOpen { get; private set; }

    public static void SetOpen(bool open) => IsOpen = open;
}
