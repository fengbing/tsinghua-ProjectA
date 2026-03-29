using UnityEngine;

/// <summary>Moves the drone toward world XY targets; works when <see cref="Time.timeScale"/> is 0.</summary>
public sealed class MiniGameDrone : MonoBehaviour
{
    [SerializeField] bool snap;
    [SerializeField] float smoothTime = 0.22f;

    Vector3 _target;
    Vector3 _velocity;

    void Start()
    {
        _target = transform.position;
    }

    void Update()
    {
        if (snap)
        {
            transform.position = _target;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            _target,
            ref _velocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
    }

    public void MoveTo(Vector3 worldXY)
    {
        _target = new Vector3(worldXY.x, worldXY.y, transform.position.z);
        if (snap)
            transform.position = _target;
    }

    /// <summary>XY distance to world point; ignores Z difference.</summary>
    public bool IsWithinPlanarDistance(Vector3 worldXY, float radius)
    {
        float dx = transform.position.x - worldXY.x;
        float dy = transform.position.y - worldXY.y;
        return dx * dx + dy * dy <= radius * radius;
    }
}
