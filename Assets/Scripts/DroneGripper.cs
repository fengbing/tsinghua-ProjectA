using UnityEngine;
using System.Collections;

/// <summary>
/// 按 F 抓取/放下：播放爪子动画，并用 FixedJoint 真实连接箱子 Rigidbody。
/// 约定：
/// - 抓取点为子物体 GrabPoint（可自动创建/查找）
/// - 抓取触发器为 GrabPoint 子物体 GrabTrigger（SphereCollider/IsTrigger）
/// - 目标箱子需有 Rigidbody + Grabbable
/// </summary>
public class DroneGripper : MonoBehaviour
{
    [SerializeField] KeyCode grabKey = KeyCode.F;

    [Header("动画（可不填）")]
    [SerializeField] Animator clawAnimator;
    [SerializeField] string grabTriggerName = "Grab";
    [SerializeField] string releaseTriggerName = "Release";
    [SerializeField] float attachDelay = 0.25f;
    [Tooltip("若 Animator 参数名拼错（例如 Relsase），可在这里填备用 Trigger 名称")]
    [SerializeField] string releaseTriggerFallbackName = "Relsase";

    [Header("抓取点")]
    [SerializeField] Transform grabPoint;

    [Header("抓取点自动居中")]
    [SerializeField] bool autoRecenterGrabPoint = false;
    [Tooltip("用于计算爪瓣中心：取抓取根节点下所有 Renderer 的 bounds.center 平均值")]
    [SerializeField] bool useRenderersBounds = true;
    [Tooltip("当 useRenderersBounds=false 时，改用 Collider 的 bounds.center 平均值")]
    [SerializeField] bool useCollidersBounds = false;

    [Header("Joint 参数")]
    [SerializeField] float breakForce = Mathf.Infinity;
    [SerializeField] float breakTorque = Mathf.Infinity;
    [Tooltip("降低载荷对无人机的拖累（越小越不影响飞行，但越不“真实”）")]
    [SerializeField, Range(0.05f, 2f)] float payloadMassScale = 0.25f;

    [Header("防穿模（高速释放时）")]
    [SerializeField] bool enableAntiTunneling = true;
    [SerializeField] float releaseUpOffset = 0.15f;
    [SerializeField] float releaseMaxSpeed = 25f;
    [SerializeField] float postReleaseCcdSeconds = 1.0f;

    Rigidbody _droneRb;
    Grabbable _candidate;
    Grabbable _holding;
    FixedJoint _joint;
    float _attachAt;
    bool _pendingAttach;

    CollisionDetectionMode _prevCcd;
    RigidbodyInterpolation _prevInterp;
    bool _savedRbSettings;
    bool _prevBoxUseGravity;
    bool _savedBoxGravity;

    void Awake()
    {
        _droneRb = GetComponentInParent<Rigidbody>();
        if (_droneRb == null) _droneRb = GetComponent<Rigidbody>();

        if (clawAnimator == null) clawAnimator = GetComponentInChildren<Animator>();
        if (grabPoint == null)
        {
            var t = transform.Find("GrabPoint");
            if (t != null) grabPoint = t;
            else
            {
                var go = new GameObject("GrabPoint");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                grabPoint = go.transform;
            }
        }

        // 你不需要运行时自动居中；避免在 Play 中修改 GrabPoint 位置导致抓取点偏差与抖动。
        // 仅允许在编辑状态（非 Play）且你手动开启时才执行一次。
#if UNITY_EDITOR
        if (!Application.isPlaying && autoRecenterGrabPoint)
            RecenterGrabPoint();
#endif
    }

    Animator GetAnimator()
    {
        if (clawAnimator != null) return clawAnimator;
        clawAnimator = GetComponentInChildren<Animator>();
        return clawAnimator;
    }

    bool HasTriggerParameter(Animator anim, string parameterName)
    {
        if (anim == null) return false;
        if (string.IsNullOrEmpty(parameterName)) return false;

        // Animator.parameters 在运行时可遍历并判断类型
        // 目的：避免对不存在的参数调用 SetTrigger 导致报错刷屏。
        var ps = anim.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            var p = ps[i];
            if (p == null) continue;
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == parameterName)
                return true;
        }
        return false;
    }

    void TrySetTrigger(Animator anim, string parameterName)
    {
        if (HasTriggerParameter(anim, parameterName))
            anim.SetTrigger(parameterName);
    }

    void Update()
    {
        if (Input.GetKeyDown(grabKey))
        {
            if (_holding == null) BeginGrab();
            else BeginRelease();
        }

        if (_pendingAttach && Time.time >= _attachAt)
        {
            _pendingAttach = false;
            AttachNow();
        }
    }

    public void SetCandidate(Grabbable g)
    {
        if (_holding != null) return;
        _candidate = g;
    }

    public void ClearCandidate(Grabbable g)
    {
        if (_candidate == g) _candidate = null;
    }

    void BeginGrab()
    {
        if (_candidate == null || grabPoint == null) return;

        Animator anim = GetAnimator();
        if (anim != null && !string.IsNullOrEmpty(grabTriggerName))
            TrySetTrigger(anim, grabTriggerName);

        _pendingAttach = true;
        _attachAt = Time.time + Mathf.Max(0f, attachDelay);
    }

    void AttachNow()
    {
        if (_candidate == null || _droneRb == null || grabPoint == null) return;

        _holding = _candidate;
        _candidate = null;

        Rigidbody boxRb = _holding.Rigidbody;
        if (boxRb == null) return;

        if (enableAntiTunneling)
        {
            // 抓住时关闭箱子的重力：否则箱子会拖拽无人机向下，导致你看到的“悬停时下坠/抖动”
            _prevBoxUseGravity = boxRb.useGravity;
            _savedBoxGravity = true;
            boxRb.useGravity = false;
        }

        if (enableAntiTunneling && !_savedRbSettings)
        {
            _prevCcd = boxRb.collisionDetectionMode;
            _prevInterp = boxRb.interpolation;
            _savedRbSettings = true;
        }
        if (enableAntiTunneling)
        {
            boxRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            boxRb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        Vector3 attachWorld = GetGrabPointCenterWorld();

        // 把箱子“几何中心”对齐到 GrabPoint：RigidBody 的 position 通常是质心，不一定是几何中心
        // 所以这里按所有 Collider 的 bounds.center 计算一个几何中心并做偏移对齐
        Vector3 geometryCenterWorld = GetGeometryCenterWorld(boxRb);
        Vector3 delta = attachWorld - geometryCenterWorld;
        Vector3 newBoxWorldPos = boxRb.position + delta;

        // 避免一连接就被拉扯产生巨大扭矩：先对齐位置并清速度
        boxRb.MovePosition(newBoxWorldPos);
        boxRb.velocity = Vector3.zero;
        boxRb.angularVelocity = Vector3.zero;

        // 用 FixedJoint 把箱子连到无人机刚体，并将锚点对齐到 GrabPoint（否则会绕无人机质心“吊”起来导致失衡）
        _joint = boxRb.gameObject.AddComponent<FixedJoint>();
        _joint.connectedBody = _droneRb;
        _joint.breakForce = breakForce;
        _joint.breakTorque = breakTorque;
        _joint.enableCollision = false;
        _joint.autoConfigureConnectedAnchor = false;
        _joint.anchor = boxRb.transform.InverseTransformPoint(attachWorld);
        _joint.connectedAnchor = _droneRb.transform.InverseTransformPoint(attachWorld);
        _joint.massScale = payloadMassScale;
    }

    Vector3 GetGrabPointCenterWorld()
    {
        if (grabPoint == null) return Vector3.zero;

        var col = grabPoint.GetComponent<Collider>();
        if (col != null) return col.bounds.center;

        // 如果抓取点对象本身没有 Collider（例如空物体），取其子物体的 Collider 的平均
        var cols = grabPoint.GetComponentsInChildren<Collider>();
        if (cols != null && cols.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null) continue;
                sum += c.bounds.center;
                count++;
            }
            if (count > 0) return sum / count;
        }

        return grabPoint.position;
    }

    Vector3 GetGeometryCenterWorld(Rigidbody rb)
    {
        if (rb == null) return Vector3.zero;

        // 你现在箱子只有 BoxCollider：优先使用 BoxCollider 的 bounds.center，
        // 避免平均多个 collider（比如包围体/附加碰撞体）导致中心偏移。
        var box = rb.GetComponent<BoxCollider>();
        if (box != null) return box.bounds.center;

        var boxes = rb.GetComponentsInChildren<BoxCollider>();
        if (boxes != null && boxes.Length > 0)
        {
            // 选最大体积的那个作为“几何中心”参考
            BoxCollider best = null;
            float bestVol = -1f;
            for (int i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i];
                if (b == null) continue;
                Vector3 size = b.size;
                float vol = size.x * size.y * size.z;
                if (vol > bestVol)
                {
                    bestVol = vol;
                    best = b;
                }
            }
            if (best != null) return best.bounds.center;
        }

        var cols = rb.GetComponentsInChildren<Collider>();
        if (cols != null && cols.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null) continue;
                sum += c.bounds.center;
                count++;
            }
            if (count > 0) return sum / count;
        }

        if (useRenderersBounds)
        {
            var renderers = rb.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    sum += r.bounds.center;
                    count++;
                }
                if (count > 0) return sum / count;
            }
        }

        // 兜底：用当前 Rigidbody 位置
        return rb.position;
    }

    void RecenterGrabPoint()
    {
        if (grabPoint == null) return;

        Vector3 sum = Vector3.zero;
        int count = 0;

        if (useRenderersBounds)
        {
            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    sum += r.bounds.center;
                    count++;
                }
            }
        }
        else if (useCollidersBounds)
        {
            var cols = GetComponentsInChildren<Collider>(includeInactive: true);
            if (cols != null)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    if (c == null) continue;
                    sum += c.bounds.center;
                    count++;
                }
            }
        }

        if (count <= 0) return;
        grabPoint.position = sum / count;
    }

    void BeginRelease()
    {
        Animator anim = GetAnimator();
        if (anim != null)
        {
            // 优先用 releaseTriggerName；不存在时才尝试 fallback。
            if (!string.IsNullOrEmpty(releaseTriggerName) && HasTriggerParameter(anim, releaseTriggerName))
            {
                anim.SetTrigger(releaseTriggerName);
            }
            else
            {
                // 兼容：Animator 参数可能写成了 Relsase（或其它你填的 fallback）
                if (!string.IsNullOrEmpty(releaseTriggerFallbackName) && releaseTriggerFallbackName != releaseTriggerName)
                    TrySetTrigger(anim, releaseTriggerFallbackName);
            }
        }

        if (_holding == null) return;

        Rigidbody boxRb = _holding.Rigidbody;
        _holding = null;

        if (boxRb != null)
        {
            var j = boxRb.GetComponent<FixedJoint>();
            if (j != null) Destroy(j);

            if (enableAntiTunneling)
            {
                // 释放后保持一段时间 CCD，避免高速抛出穿地（tunneling）
                if (_savedRbSettings)
                {
                    // 把“原设置”复制出来，协程结束后再恢复（不要在这里立刻恢复）
                    CollisionDetectionMode restoreCcd = _prevCcd;
                    RigidbodyInterpolation restoreInterp = _prevInterp;
                    _savedRbSettings = false;

                    boxRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    boxRb.interpolation = RigidbodyInterpolation.Interpolate;
                    StartCoroutine(RestoreRbAfterDelay(boxRb, restoreCcd, restoreInterp, postReleaseCcdSeconds));
                }
                else
                {
                    boxRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    boxRb.interpolation = RigidbodyInterpolation.Interpolate;
                }

                if (_savedBoxGravity)
                {
                    boxRb.useGravity = _prevBoxUseGravity;
                    _savedBoxGravity = false;
                }

                // 释放瞬间上抬一点，避免与地面/爪子重叠造成穿模或弹飞
                boxRb.position = boxRb.position + Vector3.up * releaseUpOffset;

                // 限速（可选）：避免速度过大导致 tunneling
                float v = boxRb.velocity.magnitude;
                if (v > releaseMaxSpeed)
                    boxRb.velocity = boxRb.velocity.normalized * releaseMaxSpeed;
            }
        }
    }

    IEnumerator RestoreRbAfterDelay(Rigidbody rb, CollisionDetectionMode ccd, RigidbodyInterpolation interp, float seconds)
    {
        if (rb == null) yield break;
        float t = Mathf.Max(0.05f, seconds);
        yield return new WaitForSeconds(t);
        if (rb == null) yield break;
        rb.collisionDetectionMode = ccd;
        rb.interpolation = interp;
    }

    public bool IsHolding => _holding != null;
    public float HoldingMass => _holding != null && _holding.Rigidbody != null ? _holding.Rigidbody.mass : 0f;

    /// <summary>
    /// GrabPoint 的中心点（用于 PlaneController 计算 FixedJoint 相对质心的偏移）。
    /// </summary>
    public Vector3 GrabPointCenterWorld => GetGrabPointCenterWorld();
}

