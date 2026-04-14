using System;
using UnityEngine;

/// <summary>
/// 第一人称准星射线检测。
/// 从主相机屏幕中心发出射线，检测是否对准 BalconyApproachZone，
/// 命中时显示目标阳台描边 + 触发旁白4。
/// </summary>
public class CrosshairTargetDetector : MonoBehaviour
{
    [Header("描边控制")]
    [Tooltip("由本组件负责开关的描边效果")]
    [SerializeField] BalconyOutlineEffect outlineEffect;

    [Header("射线参数")]
    [Tooltip("最大检测距离（米）")]
    [SerializeField] float maxDistance = 500f;
    [Tooltip("检测的 Unity Layer")]
    [SerializeField] LayerMask detectionLayers = ~0;

    [Header("锥形检测（扩大准星范围）")]
    [Tooltip("是否使用多射线锥形检测")]
    [SerializeField] bool useConeDetection = true;
    [Tooltip("锥形检测的额外射线数量（不含中心射线）")]
    [SerializeField] int coneRayCount = 16;
    [Tooltip("锥形检测的视角半径（度）")]
    [SerializeField] [Range(1f, 45f)] float coneHalfAngleDeg = 20f;
    [Tooltip("额外射线分布层数")]
    [SerializeField] [Range(0, 2)] int extraRingCount = 2;

    [Header("检测间隔（降低性能开销）")]
    [Tooltip("每多少帧检测一次（1 = 每帧）")]
    [SerializeField] int checkInterval = 1;

    public event Action OnTargetHit;
    public event Action OnTargetMissed;

    bool _targetCurrentlyHit;
    int _frameCounter;
    float _lastLogTime;

    void Awake()
    {
        enabled = false;
    }

    void OnEnable()
    {
        _frameCounter = 0;
        _targetCurrentlyHit = false;
        Debug.Log($"[CrosshairTargetDetector] OnEnable — outlineEffect={(outlineEffect != null ? outlineEffect.name : "NULL")}, cone={useConeDetection}, maxDist={maxDistance}");
    }

    void Update()
    {
        _frameCounter++;
        if (_frameCounter < checkInterval) return;
        _frameCounter = 0;

        bool hit = PerformRaycastCheck();

        if (hit != _targetCurrentlyHit)
        {
            _targetCurrentlyHit = hit;
            if (hit)
            {
                Debug.Log("[CrosshairTargetDetector] 准星对准目标阳台（BalconyApproachZone）");
                if (outlineEffect != null)
                    outlineEffect.SetOutlineEnabled(true);
                OnTargetHit?.Invoke();
            }
            else
            {
                Debug.Log("[CrosshairTargetDetector] 准星离开目标阳台");
                if (outlineEffect != null)
                    outlineEffect.SetOutlineEnabled(false);
                OnTargetMissed?.Invoke();
            }
        }
    }

    bool PerformRaycastCheck()
    {
        if (Camera.main == null)
        {
            if (Time.time - _lastLogTime > 2f)
            {
                Debug.LogWarning("[CrosshairTargetDetector] Camera.main 为 null");
                _lastLogTime = Time.time;
            }
            return false;
        }

        if (useConeDetection && coneRayCount > 0)
        {
            if (IsRayHitTarget(0.5f, 0.5f)) return true;

            float halfAngleRad = coneHalfAngleDeg * Mathf.Deg2Rad;
            float[] angles = { halfAngleRad };
            if (extraRingCount >= 1)
                angles = new float[] { halfAngleRad, halfAngleRad * 1.8f };
            if (extraRingCount >= 2)
                angles = new float[] { halfAngleRad, halfAngleRad * 1.8f, halfAngleRad * 2.8f };

            foreach (float angle in angles)
            {
                for (int i = 0; i < coneRayCount; i++)
                {
                    float theta = (360f / coneRayCount) * i * Mathf.Deg2Rad;
                    if (IsRayHitByConeAngle(angle, theta)) return true;
                }
            }
            return false;
        }

        return IsRayHitTarget(0.5f, 0.5f);
    }

    bool IsRayHitTarget(float viewportU, float viewportV)
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(viewportU, viewportV, 0f));
        return IsTargetOnRay(ray);
    }

    bool IsTargetOnRay(Ray ray)
    {
        var hits = Physics.RaycastAll(ray, maxDistance, detectionLayers);
        foreach (var hit in hits)
        {
            if (IsTargetHitByCollider(hit.collider))
                return true;
        }
        return false;
    }

    bool IsRayHitByConeAngle(float coneAngle, float theta)
    {
        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward;
        Vector3 right = cam.right;
        Vector3 up = cam.up;

        float sinT = Mathf.Sin(theta), cosT = Mathf.Cos(theta);
        float sinA = Mathf.Sin(coneAngle), cosA = Mathf.Cos(coneAngle);

        Vector3 localDir = new Vector3(sinA * cosT, sinA * sinT, cosA);
        Vector3 worldDir = localDir.x * right + localDir.y * up + localDir.z * forward;
        worldDir.Normalize();

        Ray ray = new Ray(cam.position, worldDir);
        var hits = Physics.RaycastAll(ray, maxDistance, detectionLayers);
        foreach (var hit in hits)
        {
            if (IsTargetHitByCollider(hit.collider))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 射线命中判断：碰撞体自身或父物体上有 DeliverySceneBridge
    /// 且 bridgeType == BalconyApproachZone 时视为命中目标阳台。
    /// </summary>
    bool IsTargetHitByCollider(Collider col)
    {
        if (col == null) return false;
        var bridge = col.GetComponentInParent<DeliverySceneBridge>();
        return bridge != null && bridge.GetBridgeType() == DeliverySceneBridge.BridgeType.BalconyApproachZone;
    }

    public bool QueryCurrentHit()
    {
        return PerformRaycastCheck();
    }
}