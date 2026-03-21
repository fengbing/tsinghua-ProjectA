using UnityEngine;

/// <summary>
/// Reports distance in meters between <see cref="measureFrom"/> and a specific <see cref="target"/> object.
/// Enable only one <see cref="IDistanceHudSource"/> on the same GameObject as <see cref="DistanceHudView"/>.
/// </summary>
public class DistanceToTargetSource : MonoBehaviour, IDistanceHudSource
{
    [SerializeField] Transform measureFrom;
    [Tooltip("The specific object to measure distance to (e.g. landing pad, marker).")]
    [SerializeField] Transform target;
    [SerializeField] float fallbackWhenUnsetMeters = 9999f;

    void Awake()
    {
        if (measureFrom == null && Camera.main != null)
            measureFrom = Camera.main.transform;
    }

    public float GetDistanceMeters()
    {
        if (target == null || measureFrom == null)
            return fallbackWhenUnsetMeters;
        return Vector3.Distance(measureFrom.position, target.position);
    }
}
