using UnityEngine;

/// <summary>
/// Distance = forward raycast from <see cref="rayOrigin"/> to first obstacle (or <see cref="maxDistance"/> if clear).
/// Assign the drone / camera transform and a layer mask that includes colliders you care about.
/// Disable <see cref="DevelopmentDistanceHudSource"/> on the same GameObject when using this as the active source.
/// </summary>
public class ForwardRaycastDistanceSource : MonoBehaviour, IDistanceHudSource
{
    [SerializeField] Transform rayOrigin;
    [SerializeField] LayerMask obstacleLayers = ~0;
    [SerializeField] float maxDistance = 800f;
    [SerializeField] QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    void Awake()
    {
        if (rayOrigin == null && Camera.main != null)
            rayOrigin = Camera.main.transform;
    }

    public float GetDistanceMeters()
    {
        if (rayOrigin == null)
            return maxDistance;
        var ray = new Ray(rayOrigin.position, rayOrigin.forward);
        return Physics.Raycast(ray, out RaycastHit hit, maxDistance, obstacleLayers, triggerInteraction)
            ? hit.distance
            : maxDistance;
    }
}
