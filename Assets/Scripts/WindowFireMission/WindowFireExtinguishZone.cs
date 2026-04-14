using UnityEngine;

/// <summary>
/// 单个火点的触发范围：进入后可拉起「按 F 开水」；灭火进度由任务逻辑按全局规则计算，与是否在本触发区内无关；索引与 <see cref="WindowFireMission"/> 的 <c>fireEffects</c> 下标一致。
/// </summary>
[RequireComponent(typeof(Collider))]
public class WindowFireExtinguishZone : MonoBehaviour
{
    [Tooltip("父级或场景中的任务组件")]
    [SerializeField] WindowFireMission mission;

    [Tooltip("对应 fireEffects 列表中的下标（从 0 起）")]
    [SerializeField] int fireIndex;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;
        mission = GetComponentInParent<WindowFireMission>();
    }

    void Awake()
    {
        var c = GetComponent<Collider>();
        if (c != null && !c.isTrigger)
            c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (mission == null || !WindowFireMission.IsDroneCollider(other))
            return;
        mission.NotifyFireExtinguishZoneEntered(fireIndex);
    }

    void OnTriggerExit(Collider other)
    {
        if (mission == null || !WindowFireMission.IsDroneCollider(other))
            return;
        mission.NotifyFireExtinguishZoneExited(fireIndex);
    }
}
