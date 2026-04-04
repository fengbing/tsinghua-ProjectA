using UnityEngine;

/// <summary>
/// 触发器内：包裹已不再被 FixedJoint 抓持、且速度足够低并保持一小段时间，则视为投递成功。
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlaneGameDeliveryZone : MonoBehaviour
{
    [SerializeField] PlaneGameNarrativeDirector director;
    [SerializeField] float maxSettleSpeed = 0.6f;
    [SerializeField] float settleHoldSeconds = 0.35f;

    float _settleTimer;

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        if (director == null)
            director = FindObjectOfType<PlaneGameNarrativeDirector>();
    }

    void OnTriggerStay(Collider other)
    {
        if (director == null || director.IsOutroStarted)
            return;

        var g = other.GetComponentInParent<Grabbable>();
        if (g == null || g.Rigidbody == null)
        {
            _settleTimer = 0f;
            return;
        }

        var rb = g.Rigidbody;
        if (IsStillHeldByGripper(rb))
        {
            _settleTimer = 0f;
            return;
        }

        float maxSqr = maxSettleSpeed * maxSettleSpeed;
        if (rb.velocity.sqrMagnitude > maxSqr || rb.angularVelocity.sqrMagnitude > maxSqr * 2f)
        {
            _settleTimer = 0f;
            return;
        }

        _settleTimer += Time.deltaTime;
        if (_settleTimer >= settleHoldSeconds)
        {
            director.NotifyDeliveryComplete();
            _settleTimer = 0f;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<Grabbable>() != null)
            _settleTimer = 0f;
    }

    static bool IsStillHeldByGripper(Rigidbody rb)
    {
        var j = rb.GetComponent<FixedJoint>();
        return j != null && j.connectedBody != null;
    }
}
