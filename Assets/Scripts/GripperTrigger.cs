using UnityEngine;

/// <summary>
/// 挂在 GrabTrigger（Trigger Collider）上：检测范围内的 Grabbable，并通知 DroneGripper。
/// </summary>
[RequireComponent(typeof(Collider))]
public class GripperTrigger : MonoBehaviour
{
    [SerializeField] DroneGripper gripper;

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        if (gripper == null) gripper = GetComponentInParent<DroneGripper>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (gripper == null) return;
        Grabbable g = other.GetComponentInParent<Grabbable>();
        if (g != null) gripper.SetCandidate(g);
    }

    void OnTriggerExit(Collider other)
    {
        if (gripper == null) return;
        Grabbable g = other.GetComponentInParent<Grabbable>();
        if (g != null) gripper.ClearCandidate(g);
    }
}
