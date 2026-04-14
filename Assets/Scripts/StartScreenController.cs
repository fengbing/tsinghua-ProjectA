using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// 进入游戏的开始画面：点击后播放旁白再跳转到指定场景。
/// 黑屏期间静音所有音频，旁白通过 SystemDialogController 显示底部字幕，
/// 两段旁白顺序播放，播放完毕后等待1秒跳转目标场景。
/// </summary>
public class StartScreenController : MonoBehaviour
{
    [Tooltip("开始画面所在 Panel（挂本脚本的物体或父物体）")]
    [SerializeField] GameObject startScreenPanel;
    [Tooltip("点击后进入游戏的全屏隐形按钮")]
    [SerializeField] Button startButton;
    [Tooltip("加载进度条")]
    [SerializeField] Slider loadingSlider;
    [Tooltip("留空则只隐藏开始画面；填场景名则切换场景，如 storage、PlaneGame")]
    [SerializeField] string gameSceneName;

    [Header("旁白设置")]
    [Tooltip("SystemDialogController 组件引用（Canvas 上）")]
    [SerializeField] SystemDialogController systemDialog;
    [Tooltip("全屏黑幕 Image（播放旁白时显示）")]
    [SerializeField] Image blackScreen;
    [Tooltip("BGM AudioSource 引用")]
    [SerializeField] AudioSource bgmAudioSource;
    [Tooltip("视频 VideoPlayer 引用")]
    [SerializeField] VideoPlayer startVideoPlayer;
    [Tooltip("跳转目标场景名（旁白播放完毕后跳转至此）")]
    [SerializeField] string targetSceneName = "meau";

    [Header("视频 & 音频停止控制")]
    [Tooltip("与视频同步停止的 AudioSource（如开场语音/特效音）")]
    [SerializeField] AudioSource syncStopAudioSource;

    [Header("旁白1")]
    [TextArea(2, 5)]
    [SerializeField] string narrationText1 = "旁白1文字";
    [SerializeField] AudioClip narrationAudio1;
    [Range(0f, 1f)]
    [SerializeField] float narrationVolume1 = 1f;

    [Header("旁白2")]
    [TextArea(2, 5)]
    [SerializeField] string narrationText2 = "旁白2文字";
    [SerializeField] AudioClip narrationAudio2;
    [Range(0f, 1f)]
    [SerializeField] float narrationVolume2 = 1f;

    bool _loading;
    bool _narrationDone;

    void Awake()
    {
        if (startScreenPanel == null)
            startScreenPanel = gameObject;
        if (startButton == null)
            startButton = GetComponentInChildren<Button>();
    }

    void Start()
    {
        if (startScreenPanel != null)
            startScreenPanel.SetActive(true);

        if (loadingSlider != null)
            loadingSlider.gameObject.SetActive(false);

        if (startButton != null)
            startButton.onClick.AddListener(OnStartGame);
    }

    void Update()
    {
        if (_loading || _narrationDone) return;
        if (startScreenPanel != null && !startScreenPanel.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            OnStartGame();
    }

    public void OnStartGame()
    {
        if (_loading || _narrationDone) return;

        Cursor.lockState = CursorLockMode.Locked;

        // targetSceneName 非空时，优先走旁白流程
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            _narrationDone = true;
            StartCoroutine(PlayNarrationThenTransition());
        }
        else if (!string.IsNullOrEmpty(gameSceneName))
        {
            _loading = true;
            Cursor.lockState = CursorLockMode.Locked;

            if (loadingSlider != null)
                loadingSlider.gameObject.SetActive(true);

            StartCoroutine(LoadGameSceneAsync());
        }
        else if (startScreenPanel != null)
        {
            startScreenPanel.SetActive(false);
        }
    }

    IEnumerator LoadGameSceneAsync()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName);
        if (op == null) yield break;
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            if (loadingSlider != null)
                loadingSlider.value = op.progress;
            yield return null;
        }

        if (loadingSlider != null)
            loadingSlider.value = 1f;

        yield return new WaitForSeconds(0.1f);
        op.allowSceneActivation = true;
    }

    /// <summary>
    /// 旁白流程：黑屏静默 → 依次播放两段旁白 → 等待1秒 → 跳转 targetSceneName。
    /// 字幕和配音均由 SystemDialogController 统一管理，逻辑与 SceneEntryDialogTrigger 一致。
    /// 目标场景在旁白开始时即异步预加载，避免黑幕消失后卡顿。
    /// </summary>
    IEnumerator PlayNarrationThenTransition()
    {
        // 1. 查找 SystemDialogController
        if (systemDialog == null)
            systemDialog = FindObjectOfType<SystemDialogController>();

        // 2. 禁用其他 AudioSource
        SetAllAudioDisabled(true);

        // 3. 立即开始异步预加载目标场景
        AsyncOperation preloadOp = null;
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            preloadOp = SceneManager.LoadSceneAsync(targetSceneName);
            if (preloadOp != null)
                preloadOp.allowSceneActivation = false;
        }

        // 4. 显示黑幕并等待淡入
        if (blackScreen != null)
        {
            var color = blackScreen.color;
            color.a = 0f;
            blackScreen.color = color;
            blackScreen.gameObject.SetActive(true);
            yield return StartCoroutine(FadeInImage(blackScreen, 0.3f));
        }

        // 5. 隐藏开始画面面板（仅当非本脚本所在物体时才 SetActive）
        if (startScreenPanel != null && startScreenPanel != gameObject)
            startScreenPanel.SetActive(false);

        // 6. 停止视频和同步音频
        if (startVideoPlayer != null)
            startVideoPlayer.Stop();
        if (syncStopAudioSource != null && syncStopAudioSource.isPlaying)
            syncStopAudioSource.Stop();

        // 7. 播放旁白1
        yield return StartCoroutine(PlayNarrationLine(narrationText1, narrationAudio1, narrationVolume1));

        // 8. 播放旁白2
        yield return StartCoroutine(PlayNarrationLine(narrationText2, narrationAudio2, narrationVolume2));

        // 9. 等待1秒
        yield return new WaitForSeconds(1f);

        // 10. 黑幕淡出
        if (blackScreen != null)
            StartCoroutine(FadeOutAndHideImage(blackScreen, 0.3f));

        // 11. 激活已预加载的场景
        if (preloadOp != null)
            preloadOp.allowSceneActivation = true;
    }

    /// <summary>
    /// 将 Image 从完全透明淡入到完全不透明。
    /// </summary>
    IEnumerator FadeInImage(Image image, float duration)
    {
        float elapsed = 0f;
        var color = image.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = t;
            image.color = color;
            yield return null;
        }
        color.a = 1f;
        image.color = color;
    }

    /// <summary>
    /// 将 Image 从当前透明度淡出到完全透明，然后隐藏 GameObject。
    /// </summary>
    IEnumerator FadeOutAndHideImage(Image image, float duration)
    {
        float elapsed = 0f;
        var color = image.color;
        float startAlpha = color.a;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(startAlpha, 0f, t);
            image.color = color;
            yield return null;
        }
        color.a = 0f;
        image.color = color;
        image.gameObject.SetActive(false);
    }

    /// <summary>
    /// 播放单段旁白，完全委托 SystemDialogController 处理字幕和配音。
    /// 逻辑与 SceneEntryDialogTrigger 一致：无文字时等待配音时长或1.5秒。
    /// </summary>
    IEnumerator PlayNarrationLine(string text, AudioClip clip, float volume)
    {
        if (string.IsNullOrEmpty(text) && clip == null)
        {
            yield return new WaitForSeconds(1.5f);
            yield break;
        }

        if (systemDialog != null)
        {
            // 根据配音时长动态计算打字速度，使字幕总时长与配音对齐
            float charInterval;
            if (clip != null && !string.IsNullOrEmpty(text))
                charInterval = clip.length / text.Length;
            else
                charInterval = 0.04f;

            var line = new SystemDialogLine
            {
                text = string.IsNullOrEmpty(text) ? "\u200B" : text,
                voiceClip = clip,
                characterInterval = charInterval
            };

            var linesToPlay = new List<SystemDialogLine> { line };
            systemDialog.PlayDialog(linesToPlay);

            while (systemDialog.IsPlaying)
                yield return null;
        }
        else if (clip != null)
        {
            // systemDialog 不可用但有配音时，直接播放音频
            AudioSource audio = bgmAudioSource ?? GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.clip = clip;
                audio.volume = volume;
                audio.Play();
                yield return new WaitForSeconds(clip.length);
            }
            else
            {
                yield return new WaitForSeconds(clip.length);
            }
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }
    }

    /// <summary>
    /// 禁用/启用开始画面 BGM AudioSource 和视频播放器音频轨道。
    /// 自动查找场景中除 systemDialog.VoiceSource 之外的首个 AudioSource。
    /// </summary>
    void SetAllAudioDisabled(bool disabled)
    {
        if (bgmAudioSource == null)
        {
            AudioSource narrationSource = null;
            if (systemDialog != null)
                narrationSource = systemDialog.VoiceSource;

            foreach (var src in FindObjectsOfType<AudioSource>())
            {
                if (src == narrationSource) continue;
                bgmAudioSource = src;
                break;
            }
        }

        if (bgmAudioSource != null)
            bgmAudioSource.enabled = !disabled;

        if (startVideoPlayer != null)
            startVideoPlayer.SetDirectAudioMute(0, disabled);
    }
}
