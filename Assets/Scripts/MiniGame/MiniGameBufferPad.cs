using UnityEngine;

/// <summary>
/// Visual buffer pad paired with a <see cref="MiniGameReceiver"/>.
/// Does not participate in gameplay clicks by default (close only via receiver).
/// </summary>
public sealed class MiniGameBufferPad : MonoBehaviour
{
    [Tooltip("若开启，缓冲垫会参与 2D 碰撞检测（一般保持关闭，避免挡住下层点击）。")]
    [SerializeField] bool colliderParticipatesInPhysics;

    void Awake() => ApplyColliderPolicy();

    void OnEnable() => ApplyColliderPolicy();

    void ApplyColliderPolicy()
    {
        var c = GetComponent<Collider2D>();
        if (c != null)
            c.enabled = colliderParticipatesInPhysics;
    }

    /// <summary>Receiver shows the pad; placement is handled only on <see cref="MiniGameReceiver"/>.</summary>
    public void OnShownByReceiver()
    {
        ApplyColliderPolicy();
    }
}
