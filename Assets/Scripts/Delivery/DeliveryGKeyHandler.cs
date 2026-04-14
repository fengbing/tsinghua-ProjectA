using UnityEngine;

/// <summary>
/// 配送流程 G 键输入处理。
/// 在 DeliveryPhaseManager 进入 DroneApproachBalcony 状态（及 CrosshairOnTarget）后，
/// 拦截 E 键触发阳台打开。
/// 与 DroneGripper 的 F 键抓取逻辑独立，按阳台打开优先级运行。
public class DeliveryEKeyHandler : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] DroneGripper droneGripper;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E))
            return;

        var manager = DeliveryPhaseManager.Instance;
        Debug.Log($"[DeliveryEKeyHandler] E 键按下，manager={(manager != null ? "存在" : "null")}，" +
            $"当前状态={(manager != null ? manager.CurrentState.ToString() : "N/A")}");
        if (manager == null)
            return;

        DeliveryPhaseManager.DeliveryState state = manager.CurrentState;
        bool nearBalcony = state == DeliveryPhaseManager.DeliveryState.DroneApproachBalcony
                        || state == DeliveryPhaseManager.DeliveryState.CrosshairOnTarget
                        || state == DeliveryPhaseManager.DeliveryState.DroneInFirstPerson;

        if (nearBalcony)
        {
            Debug.Log($"[DeliveryEKeyHandler] E 键触发阳台打开，当前状态: {state}");
            manager.NotifyOpenBalcony();
        }
    }
}
