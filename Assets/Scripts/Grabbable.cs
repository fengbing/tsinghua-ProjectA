using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Grabbable : MonoBehaviour
{
    public Rigidbody Rigidbody { get; private set; }

    /// <summary>包裹碰到任意碰撞体时触发（速度足够时）。</summary>
    public event System.Action<Collision> OnAnyCollision;

    void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        OnAnyCollision?.Invoke(collision);
    }
}
