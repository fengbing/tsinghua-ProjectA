using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Study 场景的 Esc 暂停菜单控制器。
/// 流程：按 Esc → 显示/隐藏 windowUi → 调用 GlobalGamePause 暂停/恢复。
/// 三个按钮：继续游戏（关闭菜单恢复游戏）、跳转 menu 场景（黑幕过渡）、退出游戏。
/// </summary>
public class WindowStudy : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("windowUi 根节点（通过 Inspector 拖入）")]
    [SerializeField] private GameObject windowUi;

    [Header("按钮（留空则自动查找子级 Button）")]
    [Tooltip("继续游戏按钮（点击后关闭菜单，恢复游戏）")]
    [SerializeField] private Button continueButton;
    [Tooltip("跳转 menu 按钮")]
    [SerializeField] private Button menuButton;
    [Tooltip("退出游戏按钮")]
    [SerializeField] private Button quitButton;

    private bool _isVisible = false;

    void Awake()
    {
        SetupRaycast();
    }

    void Start()
    {
        if (windowUi != null)
            windowUi.SetActive(false);

        if (continueButton == null)
            continueButton = GetButtonInChildren("ContinueButton");
        if (menuButton == null)
            menuButton = GetButtonInChildren("MenuButton");
        if (quitButton == null)
            quitButton = GetButtonInChildren("QuitButton");

        BindButtons();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            ToggleWindow();
    }

    /// <summary>切换 windowUi 显示状态，同步调用 GlobalGamePause。</summary>
    public void ToggleWindow()
    {
        _isVisible = !_isVisible;

        if (windowUi != null)
            windowUi.SetActive(_isVisible);

        if (_isVisible)
        {
            GlobalGamePause.Pause();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            GlobalGamePause.Resume();
        }
    }

    /// <summary>关闭菜单并恢复游戏（继续游戏按钮调用）。</summary>
    public void CloseAndResume()
    {
        if (!_isVisible)
            return;
        _isVisible = false;
        if (windowUi != null)
            windowUi.SetActive(false);
        GlobalGamePause.Resume();
    }

    /// <summary>跳转 menu：黑幕淡入 → 预加载 → 跳转。</summary>
    public void GoToMenu()
    {
        if (_isVisible)
        {
            _isVisible = false;
            if (windowUi != null)
                windowUi.SetActive(false);
            GlobalGamePause.ForceResumeIfPaused();
        }

        SceneTransitionHelper.TransitionTo("menu");
    }

    /// <summary>退出游戏（Editor 下退出 Play Mode，打包后退出 Application）。</summary>
    public void QuitGame()
    {
        Debug.Log("[WindowStudy] QuitGame called — 退出游戏");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void BindButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(CloseAndResume);
        }
        else
            Debug.LogWarning("[WindowStudy] continueButton 未赋值");

        if (menuButton != null)
        {
            menuButton.onClick.RemoveAllListeners();
            menuButton.onClick.AddListener(GoToMenu);
        }
        else
            Debug.LogWarning("[WindowStudy] menuButton 未赋值");

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
        }
        else
            Debug.LogWarning("[WindowStudy] quitButton 未赋值");
    }

    /// <summary>按子对象名称查找 Button 组件（兜底逻辑）。</summary>
    Button GetButtonInChildren(string childName)
    {
        if (windowUi == null) return null;
        foreach (var btn in windowUi.GetComponentsInChildren<Button>(true))
        {
            if (btn.name.Contains(childName))
                return btn;
        }
        return null;
    }

    // ==================== 射线检测配置 ====================

    /// <summary>
    /// 确保场景 Canvas 有 GraphicRaycaster，且存在 EventSystem，使按钮可响应鼠标交互。
    /// </summary>
    void SetupRaycast()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[WindowStudy] 未找到 Canvas！");
            return;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
