using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Storage 场景的 Loading 屏幕控制器：
///   1. 特效2触发 → Storage 黑屏淡入 + 全局静音 + 开始加载 backup 场景
///   2. 加载中全程保持 Storage 黑屏
///   3. 加载完成后保持黑屏静音
///   4. 切换到 backup 场景后黑屏自然消失（无需淡出），恢复音频
/// 总黑屏时长 = 加载时长 + waitAfterLoad
/// </summary>
public class StorageLoadingScreen : MonoBehaviour
{
    [Header("黑屏遮罩 — Storage 场景")]
    [Tooltip("Storage 场景的全屏黑色 CanvasGroup，全程保持遮盖")]
    [SerializeField] private CanvasGroup blackScreen;

    [Tooltip("黑屏完全不透明时的 Alpha")]
    [SerializeField] private float opaqueAlpha = 1f;

    [Tooltip("黑屏完全透明时的 Alpha")]
    [SerializeField] private float hiddenAlpha = 0f;

    [Tooltip("黑屏淡入时长（秒）")]
    [SerializeField] private float fadeInDuration = 0.4f;

    [Header("Loading 提示")]
    [Tooltip("右下角的 Loading 文字（TMP）")]
    [SerializeField] private TMP_Text loadingText;

    [Tooltip("Loading 文字内容，{0} 显示百分比")]
    [SerializeField] private string loadingFormat = "Loading... {0}%";

    [Tooltip("是否显示百分比进度")]
    [SerializeField] private bool showProgressPercent = true;

    [Header("音频静音")]
    [Tooltip("Storage 场景的 AudioListener，Loading 期间禁用实现全场景静音")]
    [SerializeField] private AudioListener audioListener;

    // Runtime
    private AsyncOperation _pendingLoadOp;
    private bool _loading;
    private bool _loadingComplete;

    /// <summary>
    /// 静态标志：标记本次会话中是否触发过 Storage → backup 的加载流程。
    /// 一旦设为 true 永不重置，用于跨场景通知 backup 音频需要等待恢复。
    /// </summary>
    private static bool _hasTriggeredLoadingSequence;

    /// <summary>
    /// Storage → backup 加载流程是否已触发（仅本次会话有效）。
    /// BackupInitialBlackScreen 通过此标志判断是否需要等待音频恢复。
    /// </summary>
    public static bool HasTriggeredLoadingSequence => _hasTriggeredLoadingSequence;

    /// <summary>
    /// 标识 StorageLoadingScreen 当前是否正在控制跨场景加载流程。
    /// true 表示黑屏静音由本脚本主导，Backup 场景应跳过自身的黑屏静音逻辑。
    /// </summary>
    public bool IsLoadingInProgress => _loading;

    /// <summary>
    /// 标识 StorageLoadingScreen 的 Storage → backup 加载流程是否已完成（阶段4结束）。
    /// BackupInitialBlackScreen 通过此标志判断何时可以恢复音频。
    /// </summary>
    public bool IsLoadingComplete => _loadingComplete;

    static StorageLoadingScreen _instance;
    public static StorageLoadingScreen Instance => _instance;

    void Awake()
    {
        Debug.Log("[StorageLoading] Awake");
        if (_instance != null && _instance != this)
        {
            Debug.Log("[StorageLoading] 多余实例已销毁");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        Debug.Log("[StorageLoading] Start");
        if (blackScreen != null)
            blackScreen.alpha = hiddenAlpha;
        if (loadingText != null)
            loadingText.gameObject.SetActive(false);

        if (audioListener == null)
            audioListener = Object.FindFirstObjectByType<AudioListener>();
        Debug.Log($"[StorageLoading] AudioListener: {(audioListener != null ? "找到" : "未找到")}");
    }

    /// <summary>
    /// 由 EffectTransition 在 ShowEffect2 完成后调用。
    /// </summary>
    public void BeginLoadingSequence(string targetScene)
    {
        if (_loading)
        {
            Debug.LogWarning("[StorageLoading] 已在 Loading 中，忽略重复调用");
            return;
        }
        Debug.Log($"[StorageLoading] BeginLoadingSequence: {targetScene}");
        StartCoroutine(CoLoadingSequence(targetScene));
    }

    IEnumerator CoLoadingSequence(string targetScene)
    {
        _loading = true;
        _hasTriggeredLoadingSequence = true;

        // ========== 阶段 1：静音 + Storage 黑屏淡入 ==========
        Debug.Log("[StorageLoading] 阶段1：静音 + 黑屏淡入");
        MuteAudio();

        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.text = showProgressPercent ? string.Format(loadingFormat, 0) : loadingFormat;
        }

        yield return StartCoroutine(CoFadeCanvasGroup(
            blackScreen, hiddenAlpha, opaqueAlpha, fadeInDuration));
        Debug.Log("[StorageLoading] 阶段1完成");

    // ========== 阶段 2：特效2已触发，立即异步加载 backup 场景 ==========
        // 检测是否有无人机正在抓取包裹，如有则在新场景中自动重新抓取
        bool anyGripperHolding = false;
        string holdingPackageName = null;
        foreach (var gripper in Object.FindObjectsByType<DroneGripper>(FindObjectsSortMode.None))
        {
            if (gripper.IsHolding)
            {
                anyGripperHolding = true;
                holdingPackageName = gripper.HoldingPackageName;
                break;
            }
        }
        if (anyGripperHolding)
        {
            Debug.Log($"[StorageLoading] 检测到无人机正在抓取: {holdingPackageName}，设置跨场景自动抓取标志");
            DroneGripper.SetPendingSimulatedGrabKeyAfterLoad(holdingPackageName);
        }

        Debug.Log($"[StorageLoading] 阶段2：开始加载 {targetScene}");
        _pendingLoadOp = SceneManager.LoadSceneAsync(targetScene);
        _pendingLoadOp.allowSceneActivation = false;
        Debug.Log("[StorageLoading] 阶段2：加载请求已发出，等待完成");

        // ========== 阶段 3：等待加载完成（全程黑屏静音）==========
        Debug.Log("[StorageLoading] 阶段3：等待加载完成");
        while (_pendingLoadOp.progress < 0.9f)
            yield return null;
        Debug.Log("[StorageLoading] 阶段3：资源加载完成，等待场景激活");

        _pendingLoadOp.allowSceneActivation = true;
        _pendingLoadOp = null;

        // 等待场景真正切换到 backup 后再恢复音频。
        // Unity 异步场景激活需要多帧，仅 yield return null 不够，
        // 需要等待当前活跃场景不再是 Storage。
        string currentScene = gameObject.scene.name;
        while (gameObject.scene.isLoaded &&
               UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != currentScene)
            yield return null;
        Debug.Log($"[StorageLoading] 阶段3：场景已切换到 {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

        // ========== 阶段 4（黑屏随场景切换自然消失）==========
        Debug.Log("[StorageLoading] 阶段4：加载流程结束");
        if (loadingText != null)
            loadingText.gameObject.SetActive(false);
        _loading = false;
        _loadingComplete = true;
    }

    void MuteAudio()
    {
        // 禁用 AudioListener，阻止所有音频输出
        if (audioListener != null)
            audioListener.enabled = false;
        Debug.Log("[StorageLoading] MuteAudio — AudioListener 已禁用");
    }

    /// <summary>
    /// 通用 CanvasGroup 渐变协程（使用 unscaledDeltaTime，不受 Time.timeScale 影响）。
    /// </summary>
    IEnumerator CoFadeCanvasGroup(CanvasGroup cg, float fromAlpha, float toAlpha, float duration)
    {
        if (cg == null)
        {
            Debug.LogWarning("[StorageLoading] CoFadeCanvasGroup: CanvasGroup 为空，跳过");
            yield break;
        }
        if (Mathf.Approximately(duration, 0f))
        {
            cg.alpha = toAlpha;
            yield break;
        }

        cg.alpha = fromAlpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
            yield return null;
        }
        cg.alpha = toAlpha;
    }
}
