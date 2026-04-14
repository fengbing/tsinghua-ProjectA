using System.Collections.Generic;
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

    /// <summary>
    /// 按当前场景里 <paramref name="waypointHierarchyRoot"/> 下的物体，用「与路线规划相同的叶子命名表」
    /// 把链上每个 Transform 重新解析为同名节点。用于改场景坐标、替换航点物体后仍指向新实例。
    /// </summary>
    public void RefreshActiveWaypointChainAgainstHierarchy(Transform waypointHierarchyRoot)
    {
        if (waypointHierarchyRoot == null)
            return;

        Transform[] chain = _runtimeWaypoints != null && _runtimeWaypoints.Length > 0
            ? _runtimeWaypoints
            : waypoints;
        if (chain == null || chain.Length == 0)
            return;

        RefreshTransformChainAgainstHierarchy(chain, waypointHierarchyRoot);
    }

    /// <summary>对任意航点链（例如规划刚提交的数组）按场景层级按名重新绑定 Transform。</summary>
    public static void RefreshTransformChainAgainstHierarchy(Transform[] chain, Transform waypointHierarchyRoot)
    {
        if (chain == null || waypointHierarchyRoot == null)
            return;

        var byName = new Dictionary<string, Transform>();
        CollectNamedWaypointLeaves(waypointHierarchyRoot, byName);

        for (int i = 0; i < chain.Length; i++)
        {
            Transform t = chain[i];
            if (t == null)
                continue;

            if (byName.TryGetValue(t.name, out Transform mapped) && mapped != null)
            {
                chain[i] = mapped;
                continue;
            }

            Transform deep = FindFirstDescendantNamed(waypointHierarchyRoot, t.name);
            if (deep != null)
                chain[i] = deep;
        }
    }

    static void CollectNamedWaypointLeaves(Transform root, Dictionary<string, Transform> outMap)
    {
        if (root == null)
            return;

        bool isLeaf = root.childCount == 0;
        if (isLeaf && root.name != "start" && root.name != "end")
        {
            if (!outMap.ContainsKey(root.name))
                outMap.Add(root.name, root);
            return;
        }

        for (int i = 0; i < root.childCount; i++)
            CollectNamedWaypointLeaves(root.GetChild(i), outMap);
    }

    /// <summary>在航点层级下解析一个名字：先匹配「非 start/end 的叶子」表（与规划器一致），否则深度优先第一个同名 Transform。</summary>
    public static Transform ResolveWaypointTransformByName(Transform waypointHierarchyRoot, string transformName)
    {
        if (waypointHierarchyRoot == null || string.IsNullOrEmpty(transformName))
            return null;

        var byName = new Dictionary<string, Transform>();
        CollectNamedWaypointLeaves(waypointHierarchyRoot, byName);
        if (byName.TryGetValue(transformName, out Transform leaf) && leaf != null)
            return leaf;
        return FindFirstDescendantNamed(waypointHierarchyRoot, transformName);
    }

    static Transform FindFirstDescendantNamed(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindFirstDescendantNamed(root.GetChild(i), name);
            if (hit != null)
                return hit;
        }

        return null;
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
