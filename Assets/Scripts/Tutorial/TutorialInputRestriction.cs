using UnityEngine;

/// <summary>
/// 教学关卡输入限制组件。
/// 挂在 PlaneController 同一 GameObject 上，由 TutorialManager 控制。
/// 通过拦截原始 Input 来限制飞行控制输入。
/// </summary>
public class TutorialInputRestriction : MonoBehaviour
{
    public static TutorialInputRestriction Instance { get; private set; }

    PlaneController _planeController;
    FollowCamera _followCamera;

    bool _allowVertical = true;
    bool _allowBoost = true;
    bool _allowMouseLook = true;

    void Awake()
    {
        Instance = this;
        _planeController = GetComponent<PlaneController>();
        _followCamera = FindObjectOfType<FollowCamera>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 设置当前阶段的输入限制。
    /// </summary>
    /// <param name="allowVertical">是否允许空格/Ctrl 垂直移动</param>
    /// <param name="allowBoost">是否允许通过方向键蓄力达到峰值速度</param>
    /// <param name="allowMouseLook">是否允许鼠标旋转视角</param>
    public void SetRestriction(bool allowVertical, bool allowBoost, bool allowMouseLook)
    {
        _allowVertical = allowVertical;
        _allowBoost = allowBoost;
        _allowMouseLook = allowMouseLook;

        if (_planeController != null)
        {
            _planeController.SetBoostAllowed(allowBoost);
            _planeController.SetVerticalEnabled(allowVertical);
        }

        if (_followCamera != null)
            _followCamera.SetMouseLookAllowed(allowMouseLook);
    }

    /// <summary>
    /// 返回被限制后的垂直输入值（-1 到 1）。
    /// 仅用于读取垂直键；水平加速判定请用 <see cref="PlaneController.IsFullMovementAccelerationActive"/>。
    /// </summary>
    public float GetVerticalInput()
    {
        if (!_allowVertical) return 0f;
        float y = 0f;
        if (Input.GetKey(KeyCode.Space)) y += 1f;
        if (Input.GetKey(KeyCode.LeftControl)) y -= 1f;
        return y;
    }

    /// <summary>
    /// 返回峰值速度（蓄力满速）是否被教程允许。
    /// </summary>
    public bool IsBoostAllowed() => _allowBoost;

    /// <summary>
    /// 与金色光圈检测一致：允许峰值、有平面方向输入、且蓄力进度已满。
    /// </summary>
    public bool IsBoosting()
    {
        if (!_allowBoost) return false;
        if (_planeController == null) return false;
        return _planeController.IsFullMovementAccelerationActive();
    }
}
