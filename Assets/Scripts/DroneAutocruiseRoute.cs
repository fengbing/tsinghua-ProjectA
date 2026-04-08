using UnityEngine;

/// <summary>
/// 在场景中按顺序摆放航点 Transform（可用空物体），拖到本组件的列表即可预设巡航路线。
/// </summary>
public class DroneAutocruiseRoute : MonoBehaviour
{
    [Tooltip("按飞行顺序排列；可拖场景中的空物体作为路点")]
    [SerializeField] Transform[] waypoints;

    [Tooltip("在 Scene 视图中选中本物体时绘制连线和到达球")]
    [SerializeField] bool drawGizmosWhenSelected = true;

    [Tooltip("Gizmo 中显示的到达半径示意（与控制器里的 Arrival Radius 独立，仅作参考）")]
    [SerializeField] float gizmoArrivalRadius = 2f;
    Transform[] _runtimeWaypoints;

    Transform[] ActiveWaypoints => _runtimeWaypoints != null && _runtimeWaypoints.Length > 0
        ? _runtimeWaypoints
        : waypoints;

    /// <summary>非空航点数量。</summary>
    public int WaypointCount
    {
        get
        {
            var active = ActiveWaypoints;
            if (active == null || active.Length == 0)
                return 0;
            int n = 0;
            for (int i = 0; i < active.Length; i++)
            {
                if (active[i] != null)
                    n++;
            }
            return n;
        }
    }

    /// <summary>按「仅非空航点」的下标取世界坐标；失败返回 false。</summary>
    public bool TryGetWaypointWorld(int orderIndex, out Vector3 worldPosition)
    {
        worldPosition = default;
        var active = ActiveWaypoints;
        if (active == null || orderIndex < 0)
            return false;
        int seen = 0;
        for (int i = 0; i < active.Length; i++)
        {
            Transform w = active[i];
            if (w == null)
                continue;
            if (seen == orderIndex)
            {
                worldPosition = w.position;
                return true;
            }
            seen++;
        }
        return false;
    }

    public void SetRuntimeWaypoints(Transform[] runtimeWaypoints)
    {
        _runtimeWaypoints = runtimeWaypoints;
    }

    public void ClearRuntimeWaypoints()
    {
        _runtimeWaypoints = null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawGizmosWhenSelected || waypoints == null)
            return;

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Vector3? prev = null;
        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform w = waypoints[i];
            if (w == null)
                continue;
            Gizmos.DrawWireSphere(w.position, gizmoArrivalRadius);
            if (prev.HasValue)
                Gizmos.DrawLine(prev.Value, w.position);
            prev = w.position;
        }
    }
#endif
}
