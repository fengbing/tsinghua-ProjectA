using System.Collections.Generic;
using System.Linq;
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

    [Header("音效 - 包裹撞击")]
    [Tooltip("包裹撞击到阳台投递区时播放的音效")]
    [SerializeField] AudioClip impactClip;
    [Tooltip("撞击音效的音量（0~1）")]
    [Range(0f, 1f)][SerializeField] float impactVolume = 1f;
    [Tooltip("撞击音效从第几秒开始播放")]
    [SerializeField] float impactStartTime = 0f;

    // 每个包裹追踪：计时器 + 正在触发器内重叠的 collider 集合
    readonly Dictionary<Grabbable, (float timer, HashSet<Collider> activeColliders, bool frameProcessed)> _settleMap = new();
    AudioSource _audioSource;
    readonly HashSet<Grabbable> _impactPlayed = new();

    void Awake()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }

    void OnEnable()
    {
        // 全局订阅所有 Grabbable 的碰撞事件（任何碰撞体碰到包裹时播放撞击音效）
        foreach (var g in FindObjectsByType<Grabbable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            SubscribeGrabbable(g);
    }

    void OnDisable()
    {
        foreach (var g in FindObjectsByType<Grabbable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            UnsubscribeGrabbable(g);
    }

    void SubscribeGrabbable(Grabbable g)
    {
        if (g == null) return;
        UnsubscribeGrabbable(g);
        g.OnAnyCollision += OnGrabbableAnyCollision;
    }

    void UnsubscribeGrabbable(Grabbable g)
    {
        if (g == null) return;
        g.OnAnyCollision -= OnGrabbableAnyCollision;
    }

    void OnGrabbableAnyCollision(Collision collision)
    {
        if (collision.rigidbody == null) return;
        var g = collision.rigidbody.GetComponent<Grabbable>();
        if (g == null) return;

        // 不在投递区内也播放音效（掉落碰到地面/其他物体）
        TryPlayImpactSound(g, collision);
    }

    void OnTriggerEnter(Collider other)
    {
        var g = other.GetComponentInParent<Grabbable>();
        if (g == null) return;

        // 订阅该包裹的碰撞事件（若尚未订阅）
        SubscribeGrabbable(g);

        if (!_settleMap.TryGetValue(g, out var entry))
            entry = (0f, new HashSet<Collider>(), false);

        entry.activeColliders.Add(other);
        _settleMap[g] = entry;

        TryPlayImpactSound(g, null);
    }

    void TryPlayImpactSound(Grabbable g, Collision collision)
    {
        if (impactClip == null || _audioSource == null) return;
        if (_impactPlayed.Contains(g)) return;

        // 如果有碰撞信息，检查相对速度是否足够（排除静止接触）
        if (collision != null && collision.relativeVelocity.sqrMagnitude < 0.01f) return;

        // 被 Gripper 抓持时不播放
        if (IsStillHeldByGripper(g.Rigidbody)) return;

        _impactPlayed.Add(g);
        float clampedTime = Mathf.Clamp(impactStartTime, 0f, impactClip.length);
        _audioSource.clip = impactClip;
        _audioSource.time = clampedTime;
        _audioSource.volume = impactVolume;
        _audioSource.loop = false;
        _audioSource.Play();
    }

    void OnTriggerStay(Collider other)
    {
        var g = other.GetComponentInParent<Grabbable>();
        if (g == null)
        {
            Debug.Log($"[PlaneGameDeliveryZone] Collider {other.gameObject.name} 没有 Grabbable，跳过");
            return;
        }
        if (g.Rigidbody == null)
        {
            Debug.Log($"[PlaneGameDeliveryZone] Grabbable {g.gameObject.name}.Rigidbody 为空，跳过");
            return;
        }

        var rb = g.Rigidbody;
        if (IsStillHeldByGripper(rb))
        {
            Debug.Log($"[PlaneGameDeliveryZone] {g.gameObject.name} 仍被 Gripper 抓持中，移除追踪");
            _settleMap.Remove(g);
            return;
        }

        float maxSqr = maxSettleSpeed * maxSettleSpeed;
        if (rb.velocity.sqrMagnitude > maxSqr)
        {
            Debug.Log($"[PlaneGameDeliveryZone] {g.gameObject.name} 速度太高 ({rb.velocity.sqrMagnitude:F3})，暂停计时");
            _settleMap.Remove(g);
            return;
        }
        if (rb.angularVelocity.sqrMagnitude > maxSqr * 2f)
        {
            Debug.Log($"[PlaneGameDeliveryZone] {g.gameObject.name} 角速度太高 ({rb.angularVelocity.sqrMagnitude:F3})，暂停计时");
            _settleMap.Remove(g);
            return;
        }

        if (!_settleMap.TryGetValue(g, out var entry))
        {
            // 虽然还没 OnTriggerEnter，但当前 collider 已在触发器内，加入集合
            entry = (0f, new HashSet<Collider>(), false);
            entry.activeColliders.Add(other);
            _settleMap[g] = entry;
        }

        // 每个包裹每帧只累加一次计时
        if (!entry.frameProcessed)
        {
            entry.timer += Time.deltaTime;
            entry.frameProcessed = true;
            _settleMap[g] = entry;
        }

        Debug.Log($"[PlaneGameDeliveryZone] 包裹 {g.gameObject.name} 静止中: {entry.timer:F2}s / {settleHoldSeconds}s（重叠 collider 数: {entry.activeColliders.Count}）");
        if (entry.timer >= settleHoldSeconds)
        {
            Debug.Log($"[PlaneGameDeliveryZone] 投递成功！调用 NotifyDeliverySettled");
            if (DeliveryPhaseManager.Instance == null)
            {
                Debug.LogError("[PlaneGameDeliveryZone] DeliveryPhaseManager.Instance 为空！无法推进状态机");
                return;
            }
            DeliveryPhaseManager.Instance.NotifyDeliverySettled();
            _settleMap.Remove(g);
        }
    }

    void OnTriggerExit(Collider other)
    {
        var g = other.GetComponentInParent<Grabbable>();
        if (g == null) return;
        if (!_settleMap.TryGetValue(g, out var entry)) return;

        entry.activeColliders.Remove(other);
        _impactPlayed.Remove(g);
        if (entry.activeColliders.Count == 0)
        {
            Debug.Log($"[PlaneGameDeliveryZone] 包裹 {g.gameObject.name} 所有 Collider 均离开投递区，清零计时器");
            _settleMap.Remove(g);
        }
        else
        {
            _settleMap[g] = entry;
            Debug.Log($"[PlaneGameDeliveryZone] Collider 离开（剩余重叠: {entry.activeColliders.Count}），保留计时器");
        }
    }

    void LateUpdate()
    {
        foreach (var g in _settleMap.Keys.ToList())
        {
            if (!_settleMap.TryGetValue(g, out var entry))
                continue;
            entry.frameProcessed = false;
            _settleMap[g] = entry;
        }
    }

    static bool IsStillHeldByGripper(Rigidbody rb)
    {
        var j = rb.GetComponent<FixedJoint>();
        bool held = j != null && j.connectedBody != null;
        if (held)
            Debug.Log($"[PlaneGameDeliveryZone] FixedJoint 仍存在，包裹 {rb.gameObject.name} 被抓持中");
        return held;
    }
}
