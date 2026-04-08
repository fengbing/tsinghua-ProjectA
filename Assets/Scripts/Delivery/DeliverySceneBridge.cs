using UnityEngine;

/// <summary>
/// 配送场景触发区桥接器。
/// 挂在场景中的触发区 GameObject 上，自动将 Trigger 事件转发给 DeliveryPhaseManager。
///
/// 需要在 Inspector 中配置：
/// - BuildingExteriorZone：楼栋外触发区（进入时通知状态机，2秒后触发弹窗）
/// - BalconyApproachZone：阳台旁触发区（进入时通知"到达阳台旁"）
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeliverySceneBridge : MonoBehaviour
{
    public enum BridgeType
    {
        BuildingExteriorZone,
        BalconyApproachZone
    }

    [SerializeField] BridgeType bridgeType;

    [Header("楼栋待机配置（仅 BuildingExteriorZone 生效）")]
    [Tooltip("无人机在楼栋区域待多久后弹出提示（秒）")]
    [SerializeField] float buildingStayDuration = 2f;

    float _stayTimer;
    bool _isDroneInside;
    bool _dialogTriggered;
    bool _nearBalconyNotified;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[DeliverySceneBridge] {gameObject.name} 的 Collider 不是触发器，已自动设为触发器");
            col.isTrigger = true;
        }

        // 确保触发器所在物体有 Rigidbody（OnTriggerEnter 需要）
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            Debug.Log($"[DeliverySceneBridge] {gameObject.name} 自动添加了 Kinematic Rigidbody");
        }
    }

    void Update()
    {
        // 两种 zone 共用 bounds 检测兜底（不依赖物理触发事件）
        if (!IsDroneOrCarriedInside())
        {
            _nearBalconyNotified = false;
            return;
        }

        if (bridgeType == BridgeType.BuildingExteriorZone)
        {
            if (_dialogTriggered)
                return;

            bool droneInside = IsDroneOrCarriedInside();
            _isDroneInside = droneInside;

            if (!droneInside)
            {
                _stayTimer = 0f;
                return;
            }

            _stayTimer += Time.deltaTime;

            // 每0.5秒打印一次，方便观察计时状态
            if (Mathf.FloorToInt(_stayTimer * 2f) != Mathf.FloorToInt((_stayTimer - Time.deltaTime) * 2f))
            {
                Debug.Log($"[DeliverySceneBridge.Update] 计时中: {_stayTimer:F2}s / {buildingStayDuration}s");
            }

            if (_stayTimer >= buildingStayDuration)
            {
                _dialogTriggered = true;
                Debug.Log($"[DeliverySceneBridge.Update] 计时到达 {buildingStayDuration}s，调用 TriggerBuildingDialogFromBridge");

                var manager = DeliveryPhaseManager.Instance;
                if (manager != null)
                {
                    Debug.Log($"[DeliverySceneBridge.Update] 当前状态: {manager.CurrentState}");
                    manager.TriggerBuildingDialogFromBridge();
                }
                else
                {
                    Debug.LogError("[DeliverySceneBridge.Update] DeliveryPhaseManager.Instance 为空！");
                }
            }
        }
        else if (bridgeType == BridgeType.BalconyApproachZone)
        {
            if (_nearBalconyNotified)
                return;

            var manager = DeliveryPhaseManager.Instance;
            if (manager == null)
                return;

            _nearBalconyNotified = true;
            Debug.Log($"[DeliverySceneBridge.Update] bounds检测到无人机进入 BalconyApproachZone，当前状态: {manager.CurrentState}");
            manager.NotifyDroneNearBalcony();
        }
    }

    /// <summary>检查触发区内是否有无人机本体或已抓取的物品（通过物理查询）</summary>
    bool IsDroneOrCarriedInside()
    {
        var col = GetComponent<Collider>();
        if (col == null) return false;

        var drone = FindDroneOrCarried();
        if (drone == null) return false;

        return col.bounds.Contains(drone.position);
    }

    /// <summary>查找无人机 Rigidbody 或已抓取物品的 Rigidbody</summary>
    Rigidbody FindDroneOrCarried()
    {
        var plane = FindObjectOfType<PlaneController>();
        if (plane != null)
        {
            var rb = plane.GetComponent<Rigidbody>();
            if (rb != null) return rb;
        }

        foreach (var gripper in FindObjectsOfType<DroneGripper>())
        {
            if (gripper.IsHolding)
            {
                var rb = gripper.GetComponent<Rigidbody>();
                if (rb != null) return rb;
            }
        }

        return null;
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[DeliverySceneBridge.OnTriggerEnter] 物体进入（总接收）: {other.gameObject.name} (tag={other.tag}, layer={LayerMask.LayerToName(other.gameObject.layer)})");

        if (!IsDroneOrCarried(other))
        {
            Debug.Log($"[DeliverySceneBridge.OnTriggerEnter] 跳过非无人机物体: {other.gameObject.name}");
            return;
        }

        Debug.Log($"[DeliverySceneBridge] 进入触发区: {gameObject.name}, 类型: {bridgeType}");

        var manager = DeliveryPhaseManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[DeliverySceneBridge] DeliveryPhaseManager.Instance 为空！");
            return;
        }

        if (bridgeType == BridgeType.BuildingExteriorZone)
        {
            // 仅在没有触发弹窗时才通知状态机（避免重复通知）
            if (!_dialogTriggered)
                manager.NotifyDroneAtBuilding();
        }
        else if (bridgeType == BridgeType.BalconyApproachZone)
        {
            Debug.Log($"[DeliverySceneBridge] 准备调用 NotifyDroneNearBalcony，当前状态: {manager.CurrentState}");
            manager.NotifyDroneNearBalcony();
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (bridgeType != BridgeType.BalconyApproachZone)
            return;

        if (!IsDroneOrCarried(other))
            return;

        var manager = DeliveryPhaseManager.Instance;
        if (manager == null)
            return;

        if (_nearBalconyNotified)
            return;

        _nearBalconyNotified = true;
        Debug.Log($"[DeliverySceneBridge.OnTriggerStay] 触发 NotifyDroneNearBalcony，当前状态: {manager.CurrentState}");
        manager.NotifyDroneNearBalcony();
    }

    /// <summary>检查碰撞体是否属于无人机本体或已抓取的物品</summary>
    bool IsDroneOrCarried(Collider col)
    {
        if (col.GetComponentInParent<PlaneController>() != null)
            return true;

        var grabbable = col.GetComponentInParent<Grabbable>();
        if (grabbable != null)
        {
            var gripper = grabbable.GetComponentInParent<DroneGripper>();
            if (gripper != null && gripper.IsHolding)
            {
                Debug.Log($"[DeliverySceneBridge] 检测到已抓取物品: {grabbable.gameObject.name}");
                return true;
            }
        }

        return false;
    }

    /// <summary>外部重置状态（用于重新开始配送流程）</summary>
    public void ResetState()
    {
        _stayTimer = 0f;
        _isDroneInside = false;
        _dialogTriggered = false;
        _nearBalconyNotified = false;
    }
}
