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

    bool _prevSpace;
    bool _prevCtrl;
    bool _prevRmb;

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
    /// <param name="allowBoost">是否允许鼠标右键加速</param>
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
    /// TutorialRing 在检测加速时也需要用这个来查询右键状态。
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
    /// 返回右键加速是否被允许。
    /// </summary>
    public bool IsBoostAllowed() => _allowBoost;

    /// <summary>
    /// 返回当前右键是否按下且被允许。
    /// </summary>
    public bool IsBoosting()
    {
        if (!_allowBoost) return false;
        return Input.GetMouseButton(1);
    }
}
