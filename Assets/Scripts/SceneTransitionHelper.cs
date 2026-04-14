using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using System.Collections;

/// <summary>
/// 场景过渡工具：实现与 StudyNarrativeController 完全一致的过渡效果。
/// - 流程：等待 delay → 黑幕淡入（unscaledDeltaTime）→ 异步预加载目标场景 → 激活。
/// - 支持视频等待模式：等待视频播放完毕 → 再等待 delay → 黑幕淡入 → 跳转。
/// 挂载在 DontDestroyOnLoad 对象上，场景切换后协程不中断。
/// </summary>
public class SceneTransitionHelper : MonoBehaviour
{
    [Header("黑幕 Image（留空则自动在场景 Canvas 上创建）")]
    [SerializeField] private Image blackScreenImage;

    [Header("时序参数")]
    [Tooltip("等待跳转的秒数")]
    [SerializeField] private float transitionDelaySeconds = 1f;
    [Tooltip("黑幕淡入持续时间（秒）")]
    [SerializeField] private float blackScreenFadeDuration = 0.3f;

    private Canvas _canvas;

    void Awake()
    {
        // 场景切换时不销毁，确保协程持续运行
        DontDestroyOnLoad(gameObject);
    }

    public static void TransitionTo(string targetScene,
        float delaySeconds = 1f,
        float fadeDuration = 0.3f)
    {
        var helper = GetOrCreateHelper();
        helper.StartTransition(targetScene, delaySeconds, fadeDuration);
    }

    /// <summary>
    /// 等待视频播放完毕 → 再等待 delay → 黑幕淡入 → 跳转。
    /// 整个过程不依赖 MonoBehaviour 生命周期，场景切换后继续执行。
    /// </summary>
    public static void TransitionAfterVideo(VideoPlayer videoPlayer, float postVideoDelay,
        string targetScene, float fadeDuration = 0.3f)
    {
        var helper = GetOrCreateHelper();
        helper.StartCoroutine(helper.WaitVideoThenTransition(videoPlayer, postVideoDelay, targetScene, fadeDuration));
    }

    private static SceneTransitionHelper GetOrCreateHelper()
    {
        var helper = FindObjectOfType<SceneTransitionHelper>();
        if (helper != null) return helper;
        var go = new GameObject("SceneTransitionHelper");
        return go.AddComponent<SceneTransitionHelper>();
    }

    public void StartTransition(string targetScene, float delay, float fade)
    {
        StartCoroutine(TransitionCoroutine(targetScene, delay, fade));
    }

    private IEnumerator WaitVideoThenTransition(VideoPlayer videoPlayer, float postVideoDelay,
        string targetScene, float fadeDuration)
    {
        if (videoPlayer == null)
        {
            Debug.LogWarning("[SceneTransitionHelper] videoPlayer 为空，直接跳转。");
            StartTransition(targetScene, postVideoDelay, fadeDuration);
            yield break;
        }

        // 等待视频真正开始：帧数 > 0 或 isPlaying == true
        while ((videoPlayer.frame == 0 || !videoPlayer.isPlaying) && videoPlayer.frame < 30)
            yield return null;

        if (videoPlayer == null)
        {
            StartTransition(targetScene, postVideoDelay, fadeDuration);
            yield break;
        }

        if (videoPlayer.isPlaying || videoPlayer.frame > 0)
        {
            Debug.Log($"[SceneTransitionHelper] 等待视频 {videoPlayer.clip?.name} 播放完毕（总帧数: {videoPlayer.frameCount}）...");
            while (videoPlayer != null && (videoPlayer.isPlaying || videoPlayer.frame < (long)videoPlayer.frameCount - 1))
                yield return null;
            Debug.Log("[SceneTransitionHelper] 视频播放完毕，开始跳转流程。");
        }
        else
        {
            Debug.LogWarning("[SceneTransitionHelper] 视频未能开始播放，直接跳转。");
        }

        StartTransition(targetScene, postVideoDelay, fadeDuration);
    }

    private IEnumerator TransitionCoroutine(string targetScene, float delay, float fade)
    {
        // 等待指定秒数（使用 unscaledDeltaTime，不受 Time.timeScale 影响）
        yield return new WaitForSecondsRealtime(delay);

        // 确保黑幕就位
        Image blackImg = EnsureBlackScreen();
        if (blackImg != null)
        {
            blackImg.gameObject.SetActive(true);
            yield return StartCoroutine(FadeInImage(blackImg, fade));
        }

        // 异步预加载并跳转
        AsyncOperation preloadOp = SceneManager.LoadSceneAsync(targetScene);
        if (preloadOp != null)
        {
            preloadOp.allowSceneActivation = false;
            yield return new WaitUntil(() => preloadOp.progress >= 0.9f);
            preloadOp.allowSceneActivation = true;
        }
        else
        {
            SceneManager.LoadScene(targetScene);
        }
    }

    private Image EnsureBlackScreen()
    {
        if (blackScreenImage != null)
            return blackScreenImage;

        if (_canvas == null)
            _canvas = FindObjectOfType<Canvas>();

        if (_canvas == null)
        {
            Debug.LogWarning("[SceneTransitionHelper] 场景中未找到 Canvas，无法创建黑幕。");
            return null;
        }

        var go = new GameObject("TransitionBlackScreen", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        blackScreenImage = go.GetComponent<Image>();
        blackScreenImage.color = new Color(0f, 0f, 0f, 0f);
        blackScreenImage.raycastTarget = false;
        return blackScreenImage;
    }

    private IEnumerator FadeInImage(Image image, float duration)
    {
        float elapsed = 0f;
        var color = image.color;
        color.a = 0f;
        image.color = color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = t;
            image.color = color;
            yield return null;
        }
        color.a = 1f;
        image.color = color;
    }
}
