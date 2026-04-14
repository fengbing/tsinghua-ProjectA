using UnityEngine;

/// <summary>
/// 子物体上的触发器：烟交互 / Zone2 烟环境音 等由 <see cref="WindowFireMission"/> 处理；WindowApproach 不触发逻辑。
/// </summary>
[RequireComponent(typeof(Collider))]
public class WindowFireProximityZone : MonoBehaviour
{
    /// <summary>
    /// Inspector 里的 Kind：本触发器负责哪一类逻辑（可各用独立碰撞体范围）。
    /// </summary>
    public enum ZoneKind
    {
        [InspectorName("窗口区域 (WindowApproach，无任务逻辑)")]
        WindowApproach = 0,
        [InspectorName("烟区域 — 旁白与交互 UI (SmokeApproach)")]
        SmokeApproach = 1,
        [InspectorName("Zone2 — 仅烟环境音 (Zone2SmokeAmbience)")]
        Zone2SmokeAmbience = 2
    }

    [Tooltip("窗口区不触发任务；烟区→第二段旁白；按 F 的 UI 由火点触发区（WindowFireExtinguishZone）进入后拉起；Zone2→仅控制烟循环环境音开停。")]
    [SerializeField] ZoneKind kind;

    [Tooltip("挂父物体（锚点根）上的 WindowFireMission。")]
    [SerializeField] WindowFireMission mission;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;
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
        mission.NotifyDroneEnteredZone(kind);
    }

    void OnTriggerExit(Collider other)
    {
        if (mission == null || !WindowFireMission.IsDroneCollider(other))
            return;
        mission.NotifyDroneExitedZone(kind);
    }
}
