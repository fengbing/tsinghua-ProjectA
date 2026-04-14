using UnityEngine;

/// <summary>
/// 第三人称 + 按 Alt 切换第一人称；两种模式共用同一套鼠标视角（yaw/pitch），仅相机位置不同。
/// </summary>
public class FollowCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 7f, -8f);
    [SerializeField] float smoothTime = 0.2f;
    [SerializeField] float mouseSensitivity = 120f;
    [SerializeField] float minPitch = -30f;
    [SerializeField] float maxPitch = 60f;
    [SerializeField] bool lockCursor = true;

    [Header("相机避障")]
    [Tooltip("是否启用第三人称相机避障。")]
    [SerializeField] bool enableCollisionAvoidance = true;
    [Tooltip("相机距离障碍物的最小间隔")]
    [SerializeField] float collisionOffset = 0.1f;
    [Tooltip("避障检测的层级")]
    [SerializeField] LayerMask collisionLayers = ~0;
    [Tooltip("相机球形检测半径")]
    [SerializeField] float cameraRadius = 0.2f;

    [Header("第一人称（按 Alt 切换）")]
    [Tooltip("第一人称时相机在目标局部空间的偏移")]
    [SerializeField] Vector3 firstPersonOffset = new Vector3(0f, 0.3f, 0.5f);
    [SerializeField] float firstPersonSmoothTime = 0.05f;

    [Header("自动巡航视角辅助")]
    [Tooltip("巡航时水平视角转向下一航点；在鼠标输入之后叠加，仍可用鼠标微调")]
    [SerializeField] float cruiseLookAssistDegreesPerSecond = 90f;

    Vector3 _vel;
    float _yaw;
    float _pitch;
    bool _firstPersonMode;
    bool _mouseLookEnabled = true;
    bool _isPaused;
    bool _autocruiseLookAssist;
    Vector3 _autocruiseLookWorldPoint;

    void Start()
    {
        var euler = transform.rotation.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// 从目标到期望相机位进行球形射线检测，命中时把相机放到障碍物前方。
    /// </summary>
    Vector3 GetCollisionSafePosition(Vector3 origin, Vector3 desired, float offset, LayerMask layers, float radius)
    {
        if (!enableCollisionAvoidance)
            return desired;
        Vector3 direction = desired - origin;
        float distance = direction.magnitude;
        if (distance < 0.001f)
            return desired;

        direction.Normalize();
        var hits = Physics.SphereCastAll(origin, radius, direction, distance, layers, QueryTriggerInteraction.Ignore);
        RaycastHit? nearestValidHit = null;
        foreach (var hit in hits)
        {
            var hitCollider = hit.collider;
            if (hitCollider == null)
                continue;
            if (target != null && hitCollider.transform.IsChildOf(target))
                continue;

            if (!nearestValidHit.HasValue || hit.distance < nearestValidHit.Value.distance)
                nearestValidHit = hit;
        }

        if (nearestValidHit.HasValue)
            return nearestValidHit.Value.point - direction * (radius + offset);
        return desired;
    }

    void LateUpdate()
    {
        if (target == null || _isPaused) return;

        if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
            _firstPersonMode = !_firstPersonMode;

        float mouseX = _mouseLookEnabled ? Input.GetAxis("Mouse X") : 0f;
        float mouseY = _mouseLookEnabled ? Input.GetAxis("Mouse Y") : 0f;
        _yaw += mouseX * mouseSensitivity * Time.deltaTime;
        _pitch -= mouseY * mouseSensitivity * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        if (_autocruiseLookAssist && target != null)
        {
            Vector3 to = Vector3.ProjectOnPlane(_autocruiseLookWorldPoint - target.position, Vector3.up);
            if (to.sqrMagnitude > 0.01f)
            {
                Vector3 wantH = to.normalized;
                Vector3 camH = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (camH.sqrMagnitude > 0.0001f)
                {
                    camH.Normalize();
                    float signed = Vector3.SignedAngle(camH, wantH, Vector3.up);
                    float step = cruiseLookAssistDegreesPerSecond * Time.deltaTime;
                    _yaw += Mathf.Clamp(signed, -step, step);
                }
            }
        }

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        if (_firstPersonMode)
        {
            Vector3 desiredPos = target.position + target.TransformDirection(firstPersonOffset);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _vel, firstPersonSmoothTime);
            transform.rotation = rotation;
        }
        else
        {
            Vector3 desired = target.position + rotation * offset;
            Vector3 safePosition = GetCollisionSafePosition(target.position, desired, collisionOffset, collisionLayers, cameraRadius);
            transform.position = Vector3.SmoothDamp(transform.position, safePosition, ref _vel, smoothTime);
            transform.rotation = rotation;
        }
    }

    /// <summary>教程模式：是否允许鼠标旋转视角。</summary>
    public void SetMouseLookAllowed(bool enabled) => _mouseLookEnabled = enabled;

    /// <summary>自动巡航：水平方向把视角转向世界坐标点（与鼠标叠加）。</summary>
    public void SetAutocruiseLookAssist(bool active, Vector3 worldPoint)
    {
        _autocruiseLookAssist = active;
        _autocruiseLookWorldPoint = worldPoint;
    }

    /// <summary>暂停相机跟随（由 PlaneGameNarrativeDirector.PauseGame 调用）。</summary>
    public void Pause()
    {
        _isPaused = true;
    }

    /// <summary>恢复相机跟随（由 PlaneGameNarrativeDirector.ResumeGame 调用）。</summary>
    public void Resume()
    {
        _isPaused = false;
    }
}
