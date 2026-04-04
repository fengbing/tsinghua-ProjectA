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

    Vector3 _vel;
    float _yaw;
    float _pitch;
    bool _firstPersonMode;
    bool _mouseLookEnabled = true;

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
    /// 计算避障后的安全相机位置。
    /// 从目标位置向期望相机位置发射射线，检测障碍物并调整相机位置。
    /// </summary>
    Vector3 GetCollisionSafePosition(Vector3 origin, Vector3 desired, float offset, LayerMask layers, float radius)
    {
        Vector3 direction = desired - origin;
        float distance = direction.magnitude;
        if (distance < 0.001f) return desired;

        direction.Normalize();

        // 球形射线检测，防止相机穿入障碍物
        if (Physics.SphereCast(origin, radius, direction, out RaycastHit hit, distance, layers))
        {
            // 将相机放在障碍物表面之外，留出 offset 间隔
            return hit.point - direction * (radius + offset);
        }

        return desired;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
            _firstPersonMode = !_firstPersonMode;

        float mouseX = _mouseLookEnabled ? Input.GetAxis("Mouse X") : 0f;
        float mouseY = _mouseLookEnabled ? Input.GetAxis("Mouse Y") : 0f;
        _yaw += mouseX * mouseSensitivity * Time.deltaTime;
        _pitch -= mouseY * mouseSensitivity * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

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
}
