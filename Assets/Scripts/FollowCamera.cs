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

    [Header("第一人称（按 Alt 切换）")]
    [Tooltip("第一人称时相机在目标局部空间的偏移")]
    [SerializeField] Vector3 firstPersonOffset = new Vector3(0f, 0.3f, 0.5f);
    [SerializeField] float firstPersonSmoothTime = 0.05f;

    Vector3 _vel;
    float _yaw;
    float _pitch;
    bool _firstPersonMode;

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

    void LateUpdate()
    {
        if (target == null) return;

        if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
            _firstPersonMode = !_firstPersonMode;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
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
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
            transform.rotation = rotation;
        }
    }
}
