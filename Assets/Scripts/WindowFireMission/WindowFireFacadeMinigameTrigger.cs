using UnityEngine;

/// <summary>
/// 挂在触发器 Collider 上：无人机与 <see cref="WindowFireMission"/> 处于「已灭火、旁白+系统提示播完、等待小游戏」阶段重叠时打开立面救援小游戏。
/// 触发器根物体可由 <see cref="WindowFireMission"/> 的 facadeMinigameTriggerRoot 在流程前保持隐藏，播完两段语音后再显示（含子物体特效）。
/// 使用 <see cref="OnTriggerStay"/>：避免无人机在进阶段前已站在区内导致不再收到 <see cref="OnTriggerEnter"/>。
/// </summary>
[RequireComponent(typeof(Collider))]
public class WindowFireFacadeMinigameTrigger : MonoBehaviour
{
    [SerializeField] WindowFireMission mission;

    void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other) => TryNotify(other);

    void OnTriggerStay(Collider other) => TryNotify(other);

    void TryNotify(Collider other)
    {
        if (mission == null || !WindowFireMission.IsDroneCollider(other))
            return;
        mission.TryOpenFacadeRescueFromTrigger();
    }
}
