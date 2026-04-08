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
    [SerializeField] PlaneController planeController;
    [SerializeField] DroneAutocruiseRoute route;
    [SerializeField] DroneGripper gripper;
    [SerializeField] FollowCamera followCamera;

    [Tooltip("开始/取消自动巡航")]
    [SerializeField] KeyCode cruiseToggleKey = KeyCode.Y;

    [Tooltip("进入此距离视为到达当前航点")]
    [SerializeField] float arrivalRadius = 2f;

    [SerializeField] bool loopRoute = false;

    Rigidbody _rb;
    bool _cruising;
    int _waypointIndex;
    bool _inputEnabledBeforeCruise;
    bool _plannerInputBlocked;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (planeController == null)
            planeController = GetComponent<PlaneController>();
        if (gripper == null)
            gripper = GetComponentInChildren<DroneGripper>();
        if (followCamera == null)
            followCamera = FindObjectOfType<FollowCamera>();
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
                TryStartCruise();
        }
    }

    void TryStartCruise()
    {
        if (_plannerInputBlocked)
            return;
        if (route == null || route.WaypointCount == 0)
            return;
        if (gripper != null && gripper.IsHolding)
            return;

        _inputEnabledBeforeCruise = planeController != null && planeController.IsInputEnabled;
        _waypointIndex = 0;
        _cruising = true;

        if (planeController != null)
        {
            planeController.SetInputEnabled(false);
            planeController.SetAutocruiseActive(true);
        }
    }

    public void SetPlannerInputBlocked(bool blocked)
    {
        _plannerInputBlocked = blocked;
        if (blocked && _cruising)
            EndCruise();
    }

    public bool TryApplyPlannedRoute(IReadOnlyList<Transform> waypointChain, bool startCruiseImmediately = false)
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

        route.SetRuntimeWaypoints(arr);
        if (startCruiseImmediately)
            TryStartCruise();
        return true;
    }

    void EndCruise()
    {
        if (!_cruising)
            return;
        _cruising = false;

        followCamera?.SetAutocruiseLookAssist(false, Vector3.zero);

        if (planeController != null)
        {
            planeController.SetAutocruiseActive(false);
            planeController.SetInputEnabled(_inputEnabledBeforeCruise);
        }
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
                    EndCruise();
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
                        EndCruise();
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
            if (axes.sqrMagnitude > 1e-6f)
                axes = Vector3.ClampMagnitude(axes, 1f);
            planeController.SetAutocruiseAxes(axes);
            break;
        }
    }
}
