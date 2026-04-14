using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Backup 场景弹窗中央控制器：
/// - 投递成功弹窗（显示图片+按钮，点击后播放视频，视频结束后跳转 meau2）
/// - 包裹损坏弹窗（显示图片+按钮，点击后跳转 meau1）
/// - 电量耗尽弹窗（显示图片+按钮，点击后跳转 meau1）
/// 所有跳转统一使用 SceneTransitionHelper（与 StudyNarrativeController 完全一致的过渡逻辑）。
/// </summary>
public class BackupDialogEvents : MonoBehaviour
{
    [Header("成功弹窗")]
    [Tooltip("投递成功弹窗的根 Image 对象（通过 Inspector 拖入）")]
    [SerializeField] private GameObject successDialogImage;
    [Tooltip("投递成功弹窗的按钮对象（通过 Inspector 拖入）")]
    [SerializeField] private GameObject successDialogButton;

    [Header("损坏弹窗")]
    [Tooltip("包裹损坏弹窗的根 Image 对象（通过 Inspector 拖入）")]
    [SerializeField] private GameObject brokenDialogImage;
    [Tooltip("包裹损坏弹窗的按钮对象（通过 Inspector 拖入）")]
    [SerializeField] private GameObject brokenDialogButton;

    [Header("电量耗尽弹窗")]
    [Tooltip("电量耗尽弹窗的根 Image 对象（通过 Inspector 拖入）")]
    [SerializeField] private GameObject powerDepletedDialogImage;
    [Tooltip("电量耗尽弹窗的按钮对象（通过 Inspector 拖入）")]
    [SerializeField] private GameObject powerDepletedDialogButton;

    [Header("视频播放器（成功弹窗用）")]
    [Tooltip("场景中的 VideoPlayer 组件（通过 Inspector 拖入）")]
    [SerializeField] private VideoPlayer videoPlayer;
    [Tooltip("视频渲染目标 RawImage（通过 Inspector 拖入）")]
    [SerializeField] private RawImage videoRenderTarget;
    [Tooltip("成功后播放的视频片段（通过 Inspector 拖入）")]
    [SerializeField] private VideoClip successVideoClip;

    public static BackupDialogEvents Instance { get; private set; }

    void Update()
    {
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetupRaycast();
        DisableBackgroundRaycast();
        HideAllDialogs();
        BindAllButtons();
    }

    // ==================== 射线检测配置 ====================

    /// <summary>
    /// 根据当前 Canvas 类型自动配置射线检测：
    /// - ScreenSpaceOverlay：GraphicRaycaster 即可（射线从 EventSystem 直发，无需 Camera）
    /// - WorldSpace：Camera 加 PhysicsRaycaster + Canvas 加 BoxCollider
    /// </summary>
    void SetupRaycast()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[BackupDialogEvents] 场景中未找到 Canvas！");
            return;
        }

        Debug.Log($"[BackupDialogEvents] Canvas renderMode={canvas.renderMode}");

        // ScreenSpaceOverlay / ScreenSpaceCamera：只需 GraphicRaycaster
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log("[BackupDialogEvents] ScreenSpaceOverlay Canvas 已添加 GraphicRaycaster。");
            }
        }
        // WorldSpace：需要 PhysicsRaycaster + Collider
        else if (canvas.renderMode == RenderMode.WorldSpace)
        {
            // Camera 上加 PhysicsRaycaster
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.GetComponent<PhysicsRaycaster>() == null)
            {
                mainCam.gameObject.AddComponent<PhysicsRaycaster>();
                Debug.Log("[BackupDialogEvents] Main Camera 已添加 PhysicsRaycaster。");
            }
            // Canvas 上加 BoxCollider（射线必须命中碰撞体）
            if (canvas.GetComponent<Collider>() == null)
            {
                var col = canvas.gameObject.AddComponent<BoxCollider>();
                var rect = canvas.GetComponent<RectTransform>();
                col.size = new Vector3(
                    rect.lossyScale.x * rect.rect.width,
                    rect.lossyScale.y * rect.rect.height,
                    0.01f);
                Debug.Log("[BackupDialogEvents] WorldSpace Canvas 已添加 BoxCollider。");
            }
            // GraphicRaycaster 兜底
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        // 确保 EventSystem 存在
        if (FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Debug.Log("[BackupDialogEvents] 已创建 EventSystem。");
        }
    }

    /// <summary>禁用所有弹窗背景图片的射线检测，防止它们遮挡按钮。</summary>
    void DisableBackgroundRaycast()
    {
        DisableRaycastOnObj(successDialogImage);
        DisableRaycastOnObj(brokenDialogImage);
        DisableRaycastOnObj(powerDepletedDialogImage);
    }

    void DisableRaycastOnObj(GameObject go)
    {
        if (go == null) return;
        foreach (var img in go.GetComponentsInChildren<Image>(true))
            img.raycastTarget = false;
        foreach (var txt in go.GetComponentsInChildren<TextMeshProUGUI>(true))
            txt.raycastTarget = false;
    }

    // ==================== 按钮绑定 ====================

    /// <summary>为所有按钮动态绑定点击回调（先查自身，再查子级，双重兜底）。</summary>
    void BindAllButtons()
    {
        TryBindButton(successDialogButton, OnSuccessButtonClicked, "successDialogButton");
        TryBindButton(brokenDialogButton, OnBrokenButtonClicked, "brokenDialogButton");
        TryBindButton(powerDepletedDialogButton, OnPowerDepletedButtonClicked, "powerDepletedDialogButton");
    }

    void TryBindButton(GameObject btnGo, UnityEngine.Events.UnityAction callback, string name)
    {
        if (btnGo == null)
        {
            Debug.LogWarning($"[BackupDialogEvents] {name} 未在 Inspector 中赋值！");
            return;
        }

        Button btn = btnGo.GetComponent<Button>();
        if (btn == null)
            btn = btnGo.GetComponentInChildren<Button>(true);

        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(callback);
            Debug.Log($"[BackupDialogEvents] ✓ {name} 绑定成功（所在对象：{btn.gameObject.name}）");
        }
        else
        {
            Debug.LogWarning($"[BackupDialogEvents] {name} 上及其子级均未找到 Button 组件！");
        }
    }

    // ==================== 投递成功 ====================

    /// <summary>显示投递成功弹窗（由 PlaneGameNarrativeDirector 在语音播完后调用）。</summary>
    public void ShowSuccessDialog()
    {
        UnlockCursor();
        HideAllDialogs();
        if (successDialogImage != null) successDialogImage.SetActive(true);
        if (successDialogButton != null) successDialogButton.SetActive(true);
    }

    void OnSuccessButtonClicked()
    {
        if (successDialogButton != null) successDialogButton.SetActive(false);
        PlaySuccessVideo();
    }

    void PlaySuccessVideo()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("[BackupDialogEvents] videoPlayer 未赋值！立即跳转。");
            SceneTransitionHelper.TransitionTo("menu 2");
            return;
        }
        if (successVideoClip == null)
        {
            Debug.LogError("[BackupDialogEvents] successVideoClip 未赋值！立即跳转。");
            SceneTransitionHelper.TransitionTo("menu 2");
            return;
        }

        if (videoRenderTarget != null) videoRenderTarget.gameObject.SetActive(true);
        videoPlayer.clip = successVideoClip;
        videoPlayer.isLooping = false;
        videoPlayer.Play();

        // 隐藏小地图，避免遮挡视频
        Object.FindFirstObjectByType<MinimapUiController>()?.HideMap();

        Debug.Log($"[BackupDialogEvents] 视频已开始播放: {successVideoClip.name}，等待播放完毕...");

        // 视频等待协程放在 SceneTransitionHelper 上，场景切换后继续执行
        SceneTransitionHelper.TransitionAfterVideo(videoPlayer, 1f, "menu 2");
    }

    // ==================== 包裹损坏 ====================

    /// <summary>显示包裹损坏弹窗（由 PlaneGameNarrativeDirector 在检测到包裹掉落时调用）。</summary>
    public void ShowBrokenDialog()
    {
        UnlockCursor();
        HideAllDialogs();
        if (brokenDialogImage != null) brokenDialogImage.SetActive(true);
        if (brokenDialogButton != null) brokenDialogButton.SetActive(true);
    }

    void OnBrokenButtonClicked()
    {
        SceneTransitionHelper.TransitionTo("menu 1");
    }

    // ==================== 电量耗尽 ====================

    /// <summary>显示电量耗尽弹窗（由 GameUi 在电量耗尽音效播放后 1 秒调用）。</summary>
    public void ShowPowerDepletedDialog()
    {
        UnlockCursor();
        HideAllDialogs();
        if (powerDepletedDialogImage != null) powerDepletedDialogImage.SetActive(true);
        if (powerDepletedDialogButton != null) powerDepletedDialogButton.SetActive(true);
    }

    void OnPowerDepletedButtonClicked()
    {
        SceneTransitionHelper.TransitionTo("menu 1");
    }

    // ==================== 工具 ====================

    void HideAllDialogs()
    {
        if (successDialogImage != null) successDialogImage.SetActive(false);
        if (successDialogButton != null) successDialogButton.SetActive(false);
        if (brokenDialogImage != null) brokenDialogImage.SetActive(false);
        if (brokenDialogButton != null) brokenDialogButton.SetActive(false);
        if (powerDepletedDialogImage != null) powerDepletedDialogImage.SetActive(false);
        if (powerDepletedDialogButton != null) powerDepletedDialogButton.SetActive(false);
        if (videoRenderTarget != null) videoRenderTarget.gameObject.SetActive(false);
    }

    /// <summary>解锁鼠标，使弹窗按钮可点击。</summary>
    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
