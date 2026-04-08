using System;
using UnityEngine;

/// <summary>
/// 第一人称准星射线检测。
/// 从主相机屏幕中心发出射线，检测是否对准目标阳台，并根据准星命中状态控制描边显示。
/// 在 DeliveryPhaseManager 启用后激活（第一人称开启后）。
/// </summary>
public class CrosshairTargetDetector : MonoBehaviour
{
    [Header("检测目标")]
    [Tooltip("目标阳台（带 BalconyOutlineEffect 的对象）")]
    [SerializeField] GameObject targetBalcony;

    [Header("描边控制")]
    [Tooltip("由本组件负责开关的描边效果（可留空，不填则只触发事件不控制描边）")]
    [SerializeField] BalconyOutlineEffect outlineEffect;

    [Header("射线参数")]
    [Tooltip("最大检测距离（米）")]
    [SerializeField] float maxDistance = 500f;
    [Tooltip("检测的 Unity Layer")]
    [SerializeField] LayerMask detectionLayers = ~0;

    [Header("锥形检测（扩大准星范围）")]
    [Tooltip("是否使用多射线锥形检测（扩大命中区域）")]
    [SerializeField] bool useConeDetection = true;
    [Tooltip("锥形检测的额外射线数量（不含中心射线），默认 8 根")]
    [SerializeField] int coneRayCount = 16;
    [Tooltip("锥形检测的视角半径（度）。视角越小越精准，越大越容易命中远处目标")]
    [SerializeField] [Range(1f, 45f)] float coneHalfAngleDeg = 20f;
    [Tooltip("额外射线分布层数（0 = 仅中心 + 第1圈；1 = 再加上第2圈更大半径；2 = 再加第3圈）")]
    [SerializeField] [Range(0, 2)] int extraRingCount = 2;

    [Header("检测间隔（降低性能开销）")]
    [Tooltip("每多少帧检测一次（1 = 每帧）")]
    [SerializeField] int checkInterval = 1;

    public event Action OnTargetHit;
    public event Action OnTargetMissed;

    bool _targetCurrentlyHit;
    int _frameCounter;

    void Awake()
    {
        enabled = false;
    }

    void OnEnable()
    {
        _frameCounter = 0;
        _targetCurrentlyHit = false;
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
                Debug.Log("[CrosshairTargetDetector] 准星对准目标阳台");
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
        if (Camera.main == null) return false;

        if (useConeDetection && coneRayCount > 0)
        {
            // 中心射线
            if (IsRayHitTarget(0.5f, 0.5f)) return true;

            float halfAngleRad = coneHalfAngleDeg * Mathf.Deg2Rad;

            // 第1圈用 halfAngleRad，第2圈用 1.8x，第3圈用 2.8x
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

    /// <summary>
    /// 检查射线沿途所有碰撞体，看是否有属于目标阳台的。
    /// </summary>
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

    /// <summary>
    /// 从相机发出锥形射线，偏转 coneAngle 弧度，方向由 theta 控制。
    /// </summary>
    bool IsRayHitByConeAngle(float coneAngle, float theta)
    {
        Transform cam = Camera.main.transform;

        // 相机本地方向
        Vector3 forward = cam.forward;
        Vector3 right = cam.right;
        Vector3 up = cam.up;

        // 以 forward 为基准，用球坐标偏转出圆锥方向
        float sinT = Mathf.Sin(theta), cosT = Mathf.Cos(theta);
        float sinA = Mathf.Sin(coneAngle), cosA = Mathf.Cos(coneAngle);

        Vector3 localDir = new Vector3(sinA * cosT, sinA * sinT, cosA);

        // 转换到世界空间（相机局部 → 世界）
        Vector3 worldDir =
            localDir.x * right +
            localDir.y * up +
            localDir.z * forward;
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

    bool IsTargetHitByCollider(Collider col)
    {
        if (col == null) return false;
        if (col.gameObject == targetBalcony) return true;
        if (col.attachedRigidbody != null && col.attachedRigidbody.gameObject == targetBalcony) return true;
        return IsChildOfTarget(col.gameObject, targetBalcony);
    }

    bool IsChildOfTarget(GameObject obj, GameObject target)
    {
        Transform t = obj.transform;
        while (t != null)
        {
            if (t.gameObject == target)
                return true;
            t = t.parent;
        }
        return false;
    }

    public void SetTarget(GameObject newTarget)
    {
        targetBalcony = newTarget;
        _targetCurrentlyHit = false;
    }

    public bool QueryCurrentHit()
    {
        return PerformRaycastCheck();
    }
}