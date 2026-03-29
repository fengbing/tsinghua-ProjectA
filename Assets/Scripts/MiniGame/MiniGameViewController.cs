using UnityEngine;

/// <summary>Orthographic pan (follows mouse) and scroll zoom; respects UI blocking.</summary>
[DisallowMultipleComponent]
public sealed class MiniGameViewController : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] float minOrthoSize = 2.5f;
    [SerializeField] float maxOrthoSize = 14f;
    [SerializeField] float zoomSpeed = 0.65f;
    [Tooltip("Orthographic size at which panHalfSpanWorld is defined.")]
    [SerializeField] float referenceOrthoSize = 5f;
    [Tooltip("Half-width in world units of pan range at reference ortho when cursor is at screen edge.")]
    [SerializeField] float panHalfSpanWorld = 10f;
    [SerializeField] float panLerp = 14f;
    [SerializeField] float maxPanRadiusFromStart = 30f;

    Vector3 _camAnchor;

    void Awake()
    {
        if (cam == null)
            cam = GetComponent<Camera>();
        _camAnchor = cam != null ? cam.transform.position : Vector3.zero;
    }

    void LateUpdate()
    {
        if (cam == null)
            return;

        if (!MiniGameWorldPointer.IsPointerOverBlockingUi())
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > Mathf.Epsilon)
            {
                float next = cam.orthographicSize - scroll * zoomSpeed;
                cam.orthographicSize = Mathf.Clamp(next, minOrthoSize, maxOrthoSize);
            }

            float nx = Input.mousePosition.x / Screen.width - 0.5f;
            float ny = Input.mousePosition.y / Screen.height - 0.5f;
            float t = Mathf.Max(0.01f, referenceOrthoSize);
            float scale = cam.orthographicSize / t;

            var target = _camAnchor;
            target.x += nx * 2f * panHalfSpanWorld * scale;
            target.y += ny * 2f * panHalfSpanWorld * scale;

            var delta = new Vector2(target.x - _camAnchor.x, target.y - _camAnchor.y);
            if (delta.magnitude > maxPanRadiusFromStart)
            {
                var c = delta.normalized * maxPanRadiusFromStart;
                target.x = _camAnchor.x + c.x;
                target.y = _camAnchor.y + c.y;
            }

            target.z = cam.transform.position.z;
            cam.transform.position = Vector3.Lerp(
                cam.transform.position,
                target,
                Time.unscaledDeltaTime * panLerp);
        }
    }
}
