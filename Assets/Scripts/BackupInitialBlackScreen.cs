using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Backup 场景初始化脚本：
/// 在场景启动后的前 N 秒内保持全屏黑遮罩 + 全局静音，
/// 之后淡出遮罩并恢复音频。
/// </summary>
public class BackupInitialBlackScreen : MonoBehaviour
{
    [Header("黑屏遮罩")]
    [Tooltip("全屏黑色 CanvasGroup")]
    [SerializeField] private CanvasGroup blackScreen;

    [Tooltip("黑屏完全不透明时的 Alpha")]
    [SerializeField] private float opaqueAlpha = 1f;

    [Tooltip("黑屏完全透明时的 Alpha")]
    [SerializeField] private float hiddenAlpha = 0f;

    [Tooltip("场景启动后保持黑屏静音的秒数")]
    [SerializeField] private float blackDuration = 3f;

    [Tooltip("黑屏淡出时长（秒）")]
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("音频静音")]
    [Tooltip("Backup 场景的 AudioListener，初始期间禁用实现全场景静音")]
    [SerializeField] private AudioListener audioListener;

    private readonly List<AudioSource> _pausedAudioSources = new();

    [Header("事件")]
    [Tooltip("黑屏淡出完成后触发，可用于延迟显示地图等")]
    public UnityEvent onFadeOutComplete;

    void Start()
    {
        // 如果本次会话中触发过 Storage → backup 加载，
        // 跳过本脚本的静音逻辑（Storage 已禁用 AudioListener），
        // 但黑屏仍需正常显示，由本脚本控制淡出。
        if (StorageLoadingScreen.HasTriggeredLoadingSequence)
        {
            Debug.Log("[BackupBlack] 检测到 StorageLoadingScreen 已禁用音频，正常显示黑屏并等待淡出");
            if (audioListener != null)
                audioListener.enabled = false;
            StartCoroutine(CoFadeOutWithoutRestore());
            return;
        }

        Debug.Log("[BackupBlack] Start — 启用黑屏静音");

        if (audioListener == null)
            audioListener = Object.FindFirstObjectByType<AudioListener>();

        // 立即静音：禁用 AudioListener + 暂停所有 AudioSource
        if (audioListener != null)
            audioListener.enabled = false;
        foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (source.isPlaying)
            {
                source.Pause();
                _pausedAudioSources.Add(source);
            }
        }

        // 立即显示黑屏（完全不透明）
        if (blackScreen != null)
            blackScreen.alpha = opaqueAlpha;

        // 等待 blackDuration 秒后淡出
        StartCoroutine(CoFadeOutAndRestore());
    }

    /// <summary>
    /// 仅黑屏淡出，不恢复音频（音频已由 Storage 禁用，等待自然恢复）。
    /// 黑屏至少保持 blackDuration 秒后再淡出。
    /// </summary>
    IEnumerator CoFadeOutWithoutRestore()
    {
        // 立即显示黑屏（完全不透明）
        if (blackScreen != null)
            blackScreen.alpha = opaqueAlpha;

        // 等待至少 blackDuration 秒（保持黑屏遮盖）
        Debug.Log($"[BackupBlack] 等待 {blackDuration} 秒黑屏保持");
        yield return new WaitForSeconds(blackDuration);

        Debug.Log("[BackupBlack] 黑屏保持结束，启用 AudioListener");

        // 启用 AudioListener，恢复音频
        if (audioListener != null)
            audioListener.enabled = true;

        Debug.Log("[BackupBlack] 开始淡出黑屏");

        // 淡出黑屏
        if (blackScreen != null)
            yield return StartCoroutine(CoFadeCanvasGroup(
                blackScreen, opaqueAlpha, hiddenAlpha, fadeOutDuration));

        Debug.Log("[BackupBlack] 完成");
        onFadeOutComplete?.Invoke();
    }

    IEnumerator CoFadeOutAndRestore()
    {
        Debug.Log($"[BackupBlack] 等待 {blackDuration} 秒");
        yield return new WaitForSeconds(blackDuration);

        Debug.Log("[BackupBlack] 开始淡出黑屏 + 恢复音频");

        // 恢复音频：先恢复所有 AudioSource，再恢复 AudioListener
        foreach (var source in _pausedAudioSources)
        {
            if (source != null)
                source.UnPause();
        }
        _pausedAudioSources.Clear();
        if (audioListener != null)
            audioListener.enabled = true;

        // 淡出黑屏
        if (blackScreen != null)
            yield return StartCoroutine(CoFadeCanvasGroup(
                blackScreen, opaqueAlpha, hiddenAlpha, fadeOutDuration));

        Debug.Log("[BackupBlack] 完成");
        onFadeOutComplete?.Invoke();
    }

    /// <summary>
    /// 通用 CanvasGroup 渐变协程（使用 unscaledDeltaTime，不受 Time.timeScale 影响）。
    /// </summary>
    IEnumerator CoFadeCanvasGroup(CanvasGroup cg, float fromAlpha, float toAlpha, float duration)
    {
        if (cg == null)
        {
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
