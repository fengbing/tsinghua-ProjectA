/// <summary>
/// 立面救援小游戏入口；由 <see cref="FacadeRescueMiniGameController"/> 实现，<see cref="WindowFireMission"/> 仅持有 <see cref="UnityEngine.Component"/> 引用以避免程序集/文件缺失时的硬类型依赖。
/// </summary>
public interface IFacadeRescueMinigameEntry
{
    void Open(WindowFireMission mission);
}
