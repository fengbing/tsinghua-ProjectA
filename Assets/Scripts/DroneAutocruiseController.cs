using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 按 <see cref="cruiseToggleKey"/>（默认 Y）沿 <see cref="DroneAutocruiseRoute"/> 自动飞行；再按一次取消。
/// 巡航时关闭玩家对 <see cref="PlaneController"/> 的输入，但用「虚拟摇杆」沿用与手动相同的蓄力加速、满速与音效；
/// 通过 <see cref="FollowCamera"/> 的水平视角辅助对准下一航点，鼠标仍可与辅助叠加。
/// 抓着货物时不会开始巡航。
/// </summary>
[DefaultExecutionOrder(-40)]
[RequireComponent(typeof(Rigidbody))]
public class DroneAutocruiseController : MonoBehaviour
{
    [Serializable]
    public class SegmentCruiseEffect
    {
        public float speedScale = 1f;
    }

    [SerializeField] PlaneController planeController;
    [SerializeField] DroneAutocruiseRoute route;
    [SerializeField] DroneGripper gripper;
    [SerializeField] FollowCamera followCamera;

    [Tooltip("开始/取消自动巡航")]
    [SerializeField] KeyCode cruiseToggleKey = KeyCode.Y;

    [Tooltip("进入此距离视为到达当前航点")]
    [SerializeField] float arrivalRadius = 2f;

    [Header("航点与场景同步")]
    [Tooltip("写入规划路线或按 Y 开始巡航前，按场景 Waypoint 层级用名字重新解析链上的 Transform（改坐标/替换物体后仍飞到当前场景节点）。")]
    [SerializeField] bool refreshWaypointChainAgainstHierarchy = true;
    [Tooltip("留空则使用名称 Waypoint 的根物体（与路线规划一致）。")]
    [SerializeField] Transform waypointHierarchyRoot;

    [SerializeField] bool loopRoute = false;

    [Tooltip("巡航开始时禁用的物体（如空气墙）。留空则在 Awake 时按名称 air 在场景中查找（含未激活）。")]
    [SerializeField] GameObject airBarrierRoot;
    [Tooltip("巡航期间临时关闭无人机惯性平滑，到达/退出巡航后恢复。")]
    [SerializeField] bool disableInertiaDuringCruise = true;
    [Header("巡航到达后视角")]
    [Tooltip("非循环巡航到达终点后，是否自动将视角转向指定目标。")]
    [SerializeField] bool autoLookAtOnRouteCompleted = true;
    [Tooltip("巡航到达后视角对准的目标物体。")]
    [SerializeField] Transform routeCompletedLookTarget;

    Rigidbody _rb;
    bool _cruising;
    int _waypointIndex;
    bool _inputEnabledBeforeCruise;
    bool _plannerInputBlocked;
    SegmentCruiseEffect[] _segmentEffects;

    /// <summary>非循环路线下飞完最后一个航点后触发（在 <see cref="EndCruise"/> 之前）。</summary>
    public event Action OnAutocruiseRouteCompleted;

    /// <summary>成功进入自动巡航时触发（含 Y 键、<see cref="TryStartCruiseFromExternal"/>、确认路线后立即起飞等）。</summary>
    public event Action OnCruiseStarted;

    /// <summary>退出巡航时触发（含飞完全程、手动取消、路线被清空等）；抵达终点时先于本事件触发 <see cref="OnAutocruiseRouteCompleted"/>。</summary>
    public event Action OnCruiseStopped;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (planeController == null)
            planeController = GetComponent<PlaneController>();
        if (gripper == null)
            gripper = GetComponentInChildren<DroneGripper>();
        if (followCamera == null)
            followCamera = FindObjectOfType<FollowCamera>();
        if (airBarrierRoot == null)
            airBarrierRoot = FindSceneObjectNamedAir();
    }

    static GameObject FindSceneObjectNamedAir()
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && t.gameObject.name == "air")
                return t.gameObject;
        }

        return null;
    }

    void OnDisable()
    {
        if (_cruising)
            EndCruise();
    }

    void Update()
    {
        if (_plannerInputBlocked)
            return;

        if (Input.GetKeyDown(cruiseToggleKey))
        {
            if (_cruising)
                EndCruise();
            else
                TryStartCruiseInternal();
        }
    }

    /// <summary>由路线规划等脚本在规划 UI 关闭后启动巡航（与 Y 键逻辑一致）。</summary>
    public bool TryStartCruiseFromExternal()
    {
        if (_plannerInputBlocked)
            return false;
        TryStartCruiseInternal();
        return _cruising;
    }

    void TryStartCruiseInternal()
    {
        if (_plannerInputBlocked)
            return;
        if (route == null || route.WaypointCount == 0)
            return;
        if (gripper != null && gripper.IsHolding)
            return;

        TryRefreshRouteChainAgainstWaypointHierarchy();

        _inputEnabledBeforeCruise = planeController != null && planeController.IsInputEnabled;
        _waypointIndex = 0;
        _cruising = true;
        CancelRouteCompletedLookAssist();

        if (planeController != null)
        {
            planeController.SetInputEnabled(false);
            planeController.SetAutocruiseActive(true);
            if (disableInertiaDuringCruise)
                planeController.SetInertiaEnabled(false);
        }

        if (airBarrierRoot != null)
            airBarrierRoot.SetActive(false);

        OnCruiseStarted?.Invoke();
    }

    public void SetPlannerInputBlocked(bool blocked)
    {
        _plannerInputBlocked = blocked;
        if (blocked && _cruising)
            EndCruise();
    }

    /// <summary>规划路线写入前必须为 <see cref="route"/> 指定场景里的 <see cref="DroneAutocruiseRoute"/>，否则 <see cref="TryApplyPlannedRoute"/> 会失败。</summary>
    public bool HasAutocruiseRouteAssigned() => route != null;

    public bool TryApplyPlannedRoute(IReadOnlyList<Transform> waypointChain, bool startCruiseImmediately = false)
    {
        return TryApplyPlannedRoute(waypointChain, null, startCruiseImmediately);
    }

    /// <summary>
    /// 写入规划路线并可选附带每段速度倍率（段定义：route[i] -&gt; route[i+1]）。
    /// </summary>
    public bool TryApplyPlannedRoute(
        IReadOnlyList<Transform> waypointChain,
        IReadOnlyList<float> segmentSpeedScales,
        bool startCruiseImmediately = false)
    {
        if (route == null || waypointChain == null || waypointChain.Count == 0)
            return false;

        var arr = new Transform[waypointChain.Count];
        for (int i = 0; i < waypointChain.Count; i++)
        {
            if (waypointChain[i] == null)
                return false;
            arr[i] = waypointChain[i];
        }

        if (refreshWaypointChainAgainstHierarchy)
        {
            Transform root = ResolveWaypointHierarchyRoot();
            if (root != null)
                DroneAutocruiseRoute.RefreshTransformChainAgainstHierarchy(arr, root);
        }

        route.SetRuntimeWaypoints(arr);
        _segmentEffects = BuildSegmentEffects(arr.Length, segmentSpeedScales);
        if (startCruiseImmediately)
            TryStartCruiseInternal();
        return true;
    }

    SegmentCruiseEffect[] BuildSegmentEffects(int waypointCount, IReadOnlyList<float> segmentSpeedScales)
    {
        int segmentCount = Mathf.Max(0, waypointCount - 1);
        if (segmentCount == 0)
            return null;

        var effects = new SegmentCruiseEffect[segmentCount];
        for (int i = 0; i < segmentCount; i++)
        {
            float s = 1f;
            if (segmentSpeedScales != null && i < segmentSpeedScales.Count)
                s = segmentSpeedScales[i];
            effects[i] = new SegmentCruiseEffect
            {
                speedScale = Mathf.Clamp(s, 0.2f, 2.5f)
            };
        }

        return effects;
    }

    Transform ResolveWaypointHierarchyRoot()
    {
        if (waypointHierarchyRoot != null)
            return waypointHierarchyRoot;
        GameObject go = GameObject.Find("Waypoint");
        return go != null ? go.transform : null;
    }

    void TryRefreshRouteChainAgainstWaypointHierarchy()
    {
        if (!refreshWaypointChainAgainstHierarchy || route == null)
            return;
        Transform root = ResolveWaypointHierarchyRoot();
        if (root == null)
            return;
        route.RefreshActiveWaypointChainAgainstHierarchy(root);
    }

    void EndCruiseAfterRouteCompleted()
    {
        OnAutocruiseRouteCompleted?.Invoke();
        bool keepLookAssist = TryApplyRouteCompletedLookAssist();
        EndCruise(clearLookAssist: !keepLookAssist);
    }

    void EndCruise(bool clearLookAssist = true)
    {
        if (!_cruising)
            return;
        _cruising = false;

        if (clearLookAssist)
            CancelRouteCompletedLookAssist();

        if (planeController != null)
        {
            planeController.SetAutocruiseActive(false);
            planeController.SetInertiaEnabled(true);
            planeController.SetInputEnabled(_inputEnabledBeforeCruise);
        }

        OnCruiseStopped?.Invoke();
    }

    bool TryApplyRouteCompletedLookAssist()
    {
        if (!autoLookAtOnRouteCompleted || followCamera == null || routeCompletedLookTarget == null)
            return false;

        followCamera.SetAutocruiseLookAssistUntilMouseMove(routeCompletedLookTarget.position);
        return true;
    }

    void CancelRouteCompletedLookAssist()
    {
        followCamera?.SetAutocruiseLookAssist(false, Vector3.zero);
    }

    void FixedUpdate()
    {
        if (!_cruising || route == null || planeController == null)
            return;

        while (true)
        {
            int n = route.WaypointCount;
            if (n == 0)
            {
                EndCruise();
                return;
            }

            if (_waypointIndex >= n)
            {
                if (loopRoute)
                    _waypointIndex = 0;
                else
                {
                    EndCruiseAfterRouteCompleted();
                    return;
                }
            }

            if (!route.TryGetWaypointWorld(_waypointIndex, out Vector3 target))
            {
                EndCruise();
                return;
            }

            followCamera?.SetAutocruiseLookAssist(true, target);

            Vector3 pos = _rb.position;
            Vector3 to = target - pos;
            float dist = to.magnitude;
            if (dist <= arrivalRadius)
            {
                if (_waypointIndex >= n - 1)
                {
                    if (loopRoute)
                        _waypointIndex = 0;
                    else
                    {
                        EndCruiseAfterRouteCompleted();
                        return;
                    }
                }
                else
                    _waypointIndex++;
                continue;
            }

            if (dist < 0.02f)
            {
                planeController.SetAutocruiseAxes(Vector3.zero);
                break;
            }

            Vector3 dirW = to / dist;
            Vector3 local = transform.InverseTransformDirection(dirW);
            Vector3 axes = new Vector3(
                Mathf.Clamp(local.x, -1f, 1f),
                Mathf.Clamp(local.y, -1f, 1f),
                Mathf.Clamp(local.z, -1f, 1f));

            float segScale = ResolveCurrentSegmentSpeedScale(_waypointIndex);
            axes *= segScale;
            if (axes.sqrMagnitude > 1e-6f)
                axes = Vector3.ClampMagnitude(axes, 1f);
            planeController.SetAutocruiseAxes(axes);
            break;
        }
    }

    float ResolveCurrentSegmentSpeedScale(int targetWaypointIndex)
    {
        // _waypointIndex 表示当前“要飞到的点”索引；段索引 = target - 1（start->firstNode 为 0）。
        int segIdx = targetWaypointIndex - 1;
        if (_segmentEffects == null || segIdx < 0 || segIdx >= _segmentEffects.Length)
            return 1f;

        SegmentCruiseEffect e = _segmentEffects[segIdx];
        return e != null ? Mathf.Clamp(e.speedScale, 0.2f, 2.5f) : 1f;
    }
}
