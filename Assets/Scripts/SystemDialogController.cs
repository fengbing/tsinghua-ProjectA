using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class SystemDialogLine
{
    [TextArea(2, 5)] public string text;
    public AudioClip voiceClip;
    [Min(0.005f)] public float characterInterval = 0.04f;
}

/// <summary>
/// 底部系统对话框控制器：按需显示、逐字渲染、句级语音播放。
/// </summary>
public class SystemDialogController : MonoBehaviour
{
    const string DedicatedCanvasName = "SystemDialogCanvas";

    [Header("UI References (optional, auto-created when empty)")]
    [SerializeField] Canvas targetCanvas;
    [SerializeField] Image dialogPanel;
    [SerializeField] TextMeshProUGUI dialogText;
    [SerializeField] bool forceDedicatedOverlayCanvas = true;
    [SerializeField] int dedicatedCanvasSortingOrder = 5000;

    [Header("Style")]
    [SerializeField] float panelHeight = 180f;
    [SerializeField] float panelAnchorY = 0f;
    [SerializeField] float panelOffsetY = 10f;
    [SerializeField] Color panelColor = new(0f, 0f, 0f, 0.85f);
    [SerializeField] Color textColor = Color.white;
    [SerializeField] int fontSize = 36;
    [SerializeField] TMP_FontAsset fontAsset;
    [Tooltip("主字体缺字时的后备字体列表（建议放中文字体）")]
    [SerializeField] List<TMP_FontAsset> fallbackFontAssets = new();
    [SerializeField] bool useUnscaledTime = true;
    [Header("Complete Behavior")]
    [SerializeField] bool fadeOutAfterComplete = true;
    [SerializeField] float completeHoldSeconds = 1f;
    [SerializeField] float fadeOutDuration = 0.8f;

    [Header("Panel Gradient")]
    [SerializeField] bool useCenterToEdgeFade = true;
    [SerializeField] int gradientTextureWidth = 256;
    [SerializeField] float gradientEdgeFadePower = 1f;

    [Header("Audio")]
    [SerializeField] AudioSource voiceSource;
    /// <summary>旁白配音 AudioSource，供外部读取。</summary>
    public AudioSource VoiceSource => voiceSource;

    [Header("Events")]
    public UnityEvent<int> onLineCompleted;
    public UnityEvent onDialogCompleted;

    [Header("Subtitle Mode（独立于 PlayDialog 使用）")]
    [SerializeField] bool subtitleAutoFadeOut = true;
    [SerializeField] float subtitleFadeOutDuration = 0.5f;
    [SerializeField] float subtitleCharacterInterval = 0.04f;
    [SerializeField] bool subtitleUseUnscaledTime = true;

    Coroutine _playRoutine;
    Coroutine _subtitleRoutine;
    Coroutine _queuedSubtitleRoutine;
    bool _skipCurrentLine;
    bool _isPlaying;
    bool _isShowingSubtitle;
    CanvasGroup _panelCanvasGroup;
    Texture2D _gradientTexture;
    Sprite _gradientSprite;

    (string text, float duration, AudioClip voiceClip, bool forceFadeOut) _queuedSubtitle;

    public bool IsPlaying => _isPlaying;
    public bool IsShowingSubtitle => _isShowingSubtitle;
    public float SubtitleCharacterInterval => subtitleCharacterInterval > 0f ? subtitleCharacterInterval : 0.04f;

    /// <summary>
    /// 播放单句对话（带配音和打字速度控制）。
    /// 等效于 PlayDialog(new List&lt;SystemDialogLine&gt; { new() { text, voiceClip, characterInterval } })，
    /// 但写法更简洁。
    /// </summary>
    /// <param name="text">对话文本</param>
    /// <param name="voice">配音 AudioClip，可为 null</param>
    /// <param name="characterInterval">每字间隔秒数；&lt;= 0 时默认 0.04s</param>
    public void PlaySingleLine(string text, AudioClip voice, float characterInterval = 0f)
    {
        float ch = characterInterval > 0f ? characterInterval : 0.04f;
        PlayDialog(new SystemDialogLine { text = text ?? string.Empty, voiceClip = voice, characterInterval = ch }.ToSingletonList());
    }

    /// <summary>
    /// 在协程中等待当前对话（含淡出）完全结束后再继续。
    /// 调用方式：yield return systemDialog.WaitUntilDialogIdle();
    /// </summary>
    public IEnumerator WaitUntilDialogIdle()
    {
        yield return new WaitUntil(() => !_isPlaying);
    }

    /// <summary>直接设置 _isPlaying 状态，不停止任何协程（用于阶段切换时重置状态）</summary>
    public void SetIsPlaying(bool value) => _isPlaying = value;

    /// <summary>
    /// 兼容旧调用：播放单行系统提示（文字+可选语音）。
    /// </summary>
    public void PlaySingleLine(string text, AudioClip voiceClip = null, float characterInterval = 0.04f)
    {
        var line = new SystemDialogLine
        {
            text = text ?? string.Empty,
            voiceClip = voiceClip,
            characterInterval = characterInterval > 0f ? characterInterval : 0.04f
        };
        PlayDialog(new List<SystemDialogLine> { line });
    }

    /// <summary>
    /// 兼容旧调用：等待对话与字幕都空闲。
    /// </summary>
    public CustomYieldInstruction WaitUntilDialogIdle()
    {
        return new WaitUntil(() =>
            !_isPlaying &&
            !_isShowingSubtitle &&
            (voiceSource == null || !voiceSource.isPlaying));
    }

    /// <summary>清除排队的旁白字幕（用于阶段切换时避免排队旁白被意外执行）</summary>
    public void ClearQueuedSubtitle()
    {
        if (_queuedSubtitleRoutine != null)
        {
            StopCoroutine(_queuedSubtitleRoutine);
            _queuedSubtitleRoutine = null;
        }
        _queuedSubtitle = (null, 0f, null, true);
    }

    void Awake()
    {
        EnsureUiSetup();
        EnsureAudioSource();
        HideDialog();
    }

    void OnDisable()
    {
        StopPlaybackAndCleanup();
        HideDialog();
    }

    void OnDestroy()
    {
        StopPlaybackAndCleanup();
        ReleaseGradientAssets();
    }

    public void PlayDialog(IList<SystemDialogLine> lines)
    {
        EnsureUiSetup();
        EnsureAudioSource();

        if (lines == null || lines.Count == 0)
        {
            StopPlaybackAndCleanup();
            _isPlaying = false;
            HideDialog();
            return;
        }

        var nonEmptyLines = new List<SystemDialogLine>();
        foreach (var line in lines)
        {
            if (line != null && (!string.IsNullOrEmpty(line.text) || line.voiceClip != null))
                nonEmptyLines.Add(line);
        }

        if (nonEmptyLines.Count == 0)
        {
            StopPlaybackAndCleanup();
            _isPlaying = false;
            HideDialog();
            return;
        }

        StopPlaybackAndCleanup();
        _isPlaying = true;
        _playRoutine = StartCoroutine(PlayDialogRoutine(nonEmptyLines));
    }

    public void SkipCurrentLine()
    {
        _skipCurrentLine = true;
    }

    public void HideDialog()
    {
        if (_isShowingSubtitle)
            return; // 旁白字幕正在显示，不隐藏面板
        if (dialogText != null)
            dialogText.text = string.Empty;
        if (dialogPanel != null)
            dialogPanel.gameObject.SetActive(false);
    }

    public void CancelAndHide()
    {
        StopPlaybackAndCleanup();
        HideDialog();
    }

    /// <summary>
    /// 显示独立字幕（与 PlayDialog 互不干扰）。
    /// 若当前有对话正在播放，旁白会排队等待当前对话结束后再执行。
    /// 面板出现后逐字渲染，渲染完毕后按 duration 停留，然后淡出。
    /// 若 duration 小于打字所需时间，则等打字完成后立即淡出。
    /// </summary>
    /// <param name="text">字幕文字</param>
    /// <param name="duration">显示总时长（秒），0 = 只显示不自动淡出</param>
    /// <param name="voiceClip">配音（可选，null 时仅显示字幕）</param>
    /// <param name="forceFadeOut">调用 HideSubtitle 时是否强制执行淡出动画（true = 淡出后消失，false = 立即消失）</param>
    public void ShowSubtitle(string text, float duration, AudioClip voiceClip = null, bool forceFadeOut = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            HideSubtitle(forceFadeOut);
            return;
        }

        // 有对话正在播放时，排队等待
        if (_isPlaying)
        {
            _queuedSubtitle = (text, duration, voiceClip, forceFadeOut);
            if (_queuedSubtitleRoutine != null)
                StopCoroutine(_queuedSubtitleRoutine);
            _queuedSubtitleRoutine = StartCoroutine(WaitForDialogThenShowSubtitle());
            return;
        }

        if (_subtitleRoutine != null)
            StopCoroutine(_subtitleRoutine);
        _subtitleRoutine = StartCoroutine(ShowSubtitleRoutine(text, duration, voiceClip, forceFadeOut));
    }

    /// <summary>
    /// 显示独立字幕（无配音，字幕持续时长完全由 duration 控制）。
    /// 若当前有对话正在播放，旁白会排队等待当前对话结束后再执行。
    /// 面板出现后逐字渲染，渲染完毕后按 duration 停留，然后淡出。
    /// 若 duration 小于打字所需时间，则等打字完成后立即淡出。
    /// </summary>
    /// <param name="text">字幕文字</param>
    /// <param name="duration">显示总时长（秒），必须 > 0</param>
    /// <param name="forceFadeOut">调用 HideSubtitle 时是否强制执行淡出动画（true = 淡出后消失，false = 立即消失）</param>
    public void ShowSubtitleByDuration(string text, float duration, bool forceFadeOut = true)
    {
        if (string.IsNullOrEmpty(text) || duration <= 0f)
        {
            HideSubtitle(forceFadeOut);
            return;
        }

        if (_isPlaying)
        {
            _queuedSubtitle = (text, duration, null, forceFadeOut);
            if (_queuedSubtitleRoutine != null)
                StopCoroutine(_queuedSubtitleRoutine);
            _queuedSubtitleRoutine = StartCoroutine(WaitForDialogThenShowSubtitle());
            return;
        }

        if (_subtitleRoutine != null)
            StopCoroutine(_subtitleRoutine);
        _subtitleRoutine = StartCoroutine(ShowSubtitleByDurationRoutine(text, duration, forceFadeOut));
    }

    IEnumerator WaitForDialogThenShowSubtitle()
    {
        yield return new WaitUntil(() => !_isPlaying);
        var (text, duration, voiceClip, forceFadeOut) = _queuedSubtitle;
        _queuedSubtitleRoutine = null;
        if (_subtitleRoutine != null)
            StopCoroutine(_subtitleRoutine);
        _subtitleRoutine = StartCoroutine(ShowSubtitleRoutine(text, duration, voiceClip, forceFadeOut));
    }

    /// <summary>隐藏字幕</summary>
    /// <param name="forceFadeOut">是否执行淡出动画后再消失，false = 立即消失</param>
    public void HideSubtitle(bool forceFadeOut = true)
    {
        if (_subtitleRoutine != null)
        {
            StopCoroutine(_subtitleRoutine);
            _subtitleRoutine = null;
        }
        if (_queuedSubtitleRoutine != null)
        {
            StopCoroutine(_queuedSubtitleRoutine);
            _queuedSubtitleRoutine = null;
        }
        _isShowingSubtitle = false;
        _isPlaying = false;
        if (forceFadeOut)
            StartCoroutine(HideSubtitleWithFade());
        else
            HideDialog();
    }

    public void ApplyTextStyle(TMP_FontAsset overrideFont, int overrideFontSize = 0)
    {
        if (dialogText == null)
            return;
        if (overrideFont != null)
            dialogText.font = overrideFont;
        if (overrideFontSize > 0)
            dialogText.fontSize = overrideFontSize;
        ApplyFallbackFontsToCurrentFont();
    }

    IEnumerator PlayDialogRoutine(IList<SystemDialogLine> lines)
    {
        _isPlaying = true;
        ResetPanelLayout();
        if (dialogPanel != null)
        {
            dialogPanel.transform.SetAsLastSibling();
            dialogPanel.gameObject.SetActive(true);
        }
        SetPanelAlpha(1f);

        for (int i = 0; i < lines.Count; i++)
        {
            SystemDialogLine line = lines[i];
            _skipCurrentLine = false;
            yield return PlayLineSynchronized(line);
            yield return WaitForSecondsByMode(Mathf.Max(0f, completeHoldSeconds));
            onLineCompleted?.Invoke(i);
        }

        StopVoice();

        if (fadeOutAfterComplete)
            yield return FadeOutPanel(Mathf.Max(0.01f, fadeOutDuration));

        HideDialog();
        _isPlaying = false;
        onDialogCompleted?.Invoke();
    }

    IEnumerator PlayLineSynchronized(SystemDialogLine line)
    {
        if (dialogText == null)
            EnsureUiSetup();

        string content = line?.text ?? string.Empty;
        if (dialogText != null)
            dialogText.text = string.Empty;
        if (string.IsNullOrEmpty(content))
        {
            PlayVoiceForLine(line);
            if (voiceSource != null && voiceSource.isPlaying)
                yield return new WaitWhile(() => voiceSource != null && voiceSource.isPlaying);
            yield break;
        }

        PlayVoiceForLine(line);
        bool hasVoice = line != null && line.voiceClip != null && voiceSource != null;
        float baseInterval = line != null && line.characterInterval > 0f ? line.characterInterval : 0.04f;
        float typingDuration = hasVoice
            ? Mathf.Max(0.01f, line.voiceClip.length)
            : Mathf.Max(0.01f, content.Length * baseInterval);

        float elapsed = 0f;
        int shownChars = 0;
        int charCount = content.Length;
        while (elapsed < typingDuration)
        {
            if (_skipCurrentLine)
            {
                if (dialogText != null)
                    dialogText.text = content;
                StopVoice();
                yield break;
            }

            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / typingDuration);
            int wantChars = Mathf.Clamp(Mathf.FloorToInt(t * charCount), 0, charCount);
            if (wantChars != shownChars)
            {
                shownChars = wantChars;
                if (dialogText != null)
                    dialogText.text = content.Substring(0, shownChars);
            }
            yield return null;
        }

        if (dialogText != null)
            dialogText.text = content;
        if (hasVoice && voiceSource != null && voiceSource.isPlaying)
            StopVoice();
    }

    void PlayVoiceForLine(SystemDialogLine line)
    {
        StopVoice();
        if (voiceSource == null || line == null || line.voiceClip == null)
            return;

        voiceSource.clip = line.voiceClip;
        voiceSource.loop = false;
        voiceSource.Play();
    }

    void StopVoice()
    {
        if (voiceSource == null)
            return;
        voiceSource.Stop();
        voiceSource.clip = null;
    }

    IEnumerator ShowSubtitleRoutine(string text, float duration, AudioClip voiceClip, bool forceFadeOut)
    {
        _isShowingSubtitle = true;
        ResetPanelLayout();
        if (dialogPanel != null)
        {
            dialogPanel.transform.SetAsLastSibling();
            dialogPanel.gameObject.SetActive(true);
        }
        SetPanelAlpha(1f);

        if (dialogText != null)
            dialogText.text = string.Empty;

        float charInterval = subtitleCharacterInterval > 0f ? subtitleCharacterInterval : 0.04f;
        int charCount = text.Length;

        // 配音和打字并行进行
        if (voiceClip != null && voiceSource != null)
        {
            voiceSource.clip = voiceClip;
            voiceSource.loop = false;
            voiceSource.Play();
        }

        int nextCharIndex = 0;
        bool voiceFinished = voiceClip == null || voiceSource == null; // 无配音时直接标记完成
        bool typingFinished = nextCharIndex >= charCount;

        while (!voiceFinished || !typingFinished)
        {
            yield return new WaitForSecondsRealtime(charInterval);

            // 打字（每隔 charInterval 打一个字）
            if (!typingFinished)
            {
                nextCharIndex++;
                if (dialogText != null)
                    dialogText.text = text.Substring(0, nextCharIndex);
                if (nextCharIndex >= charCount)
                    typingFinished = true;
            }

            // 检查配音是否结束
            if (!voiceFinished && voiceClip != null && voiceSource != null && !voiceSource.isPlaying)
                voiceFinished = true;
        }

        // 先取消保护，允许 HideDialog 真正隐藏面板
        _isShowingSubtitle = false;
        HideDialog();
    }

    IEnumerator ShowSubtitleByDurationRoutine(string text, float duration, bool forceFadeOut)
    {
        _isShowingSubtitle = true;
        ResetPanelLayout();
        if (dialogPanel != null)
        {
            dialogPanel.transform.SetAsLastSibling();
            dialogPanel.gameObject.SetActive(true);
        }
        SetPanelAlpha(1f);

        if (dialogText != null)
            dialogText.text = string.Empty;

        float charInterval = subtitleCharacterInterval > 0f ? subtitleCharacterInterval : 0.04f;
        int charCount = text.Length;
        float typingDuration = charCount * charInterval;
        float showDuration = Mathf.Max(typingDuration, duration);

        // 逐字打字
        for (int i = 1; i <= charCount; i++)
        {
            if (dialogText != null)
                dialogText.text = text.Substring(0, i);
            yield return new WaitForSecondsRealtime(charInterval);
        }

        // 等待剩余时长（确保总显示时长 = duration）
        float used = typingDuration;
        float remaining = duration - used;
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        _isShowingSubtitle = false;
        HideDialog();
    }

    IEnumerator HideSubtitleWithFade()
    {
        if (_panelCanvasGroup == null)
        {
            _isShowingSubtitle = false;
            HideDialog();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < subtitleFadeOutDuration)
        {
            elapsed += subtitleUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / subtitleFadeOutDuration);
            _panelCanvasGroup.alpha = 1f - t;
            yield return null;
        }
        _panelCanvasGroup.alpha = 0f;
        _isShowingSubtitle = false;
        HideDialog();
    }

    void StopPlaybackAndCleanup()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
        if (_subtitleRoutine != null)
        {
            StopCoroutine(_subtitleRoutine);
            _subtitleRoutine = null;
        }
        if (_queuedSubtitleRoutine != null)
        {
            StopCoroutine(_queuedSubtitleRoutine);
            _queuedSubtitleRoutine = null;
        }
        _skipCurrentLine = false;
        _isPlaying = false;
        _isShowingSubtitle = false;
        StopVoice();
    }

    void EnsureAudioSource()
    {
        if (voiceSource != null)
            return;
        voiceSource = gameObject.GetComponent<AudioSource>();
        if (voiceSource == null)
            voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
    }

    void EnsureUiSetup()
    {
        if (forceDedicatedOverlayCanvas)
        {
            EnsureDedicatedOverlayCanvas();
        }

        if (targetCanvas == null)
        {
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            Transform deliveryPromptsRoot = FindDeliveryPromptsUIRoot();
            Canvas fallbackCanvas = null;
            foreach (var c in allCanvases)
            {
                // 跳过 DeliveryPromptsUI 及其子级下的 Canvas
                if (deliveryPromptsRoot != null && c.transform.IsChildOf(deliveryPromptsRoot))
                    continue;
                if (c == null || !c.gameObject.activeInHierarchy)
                    continue;

                // 优先选择屏幕空间 Overlay，避免挂到 WorldSpace Canvas 导致“有声音无字幕”。
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    targetCanvas = c;
                    break;
                }

                // 次优先：屏幕空间 Camera 且有有效 camera。
                if (targetCanvas == null && c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera != null)
                    targetCanvas = c;

                // 最后兜底：记录任意激活 canvas。
                fallbackCanvas ??= c;
            }
            if (targetCanvas == null)
                targetCanvas = fallbackCanvas;
        }

        // 若找到的是 WorldSpace Canvas，改用专用 Overlay Canvas，确保字幕一定可见。
        if (targetCanvas == null || targetCanvas.renderMode == RenderMode.WorldSpace || !targetCanvas.gameObject.activeInHierarchy)
        {
            GameObject canvasGo = new GameObject("SystemDialogCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasGo.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }
        if (targetCanvas == null)
            return;

        if (dialogPanel == null)
        {
            GameObject panelObject = new("SystemDialogPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(targetCanvas.transform, false);
            dialogPanel = panelObject.GetComponent<Image>();

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, panelAnchorY);
            panelRect.anchorMax = new Vector2(1f, panelAnchorY);
            panelRect.pivot = new Vector2(0.5f, panelAnchorY);
            panelRect.anchoredPosition = new Vector2(0f, panelOffsetY);
            panelRect.sizeDelta = new Vector2(0f, panelHeight);
        }
        else
        {
            RectTransform panelRect = dialogPanel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, panelAnchorY);
            panelRect.anchorMax = new Vector2(1f, panelAnchorY);
            panelRect.pivot = new Vector2(0.5f, panelAnchorY);
            panelRect.anchoredPosition = new Vector2(0f, panelOffsetY);
            panelRect.sizeDelta = new Vector2(0f, panelHeight);
        }

        dialogPanel.color = panelColor;
        EnsurePanelCanvasGroup();
        ApplyPanelGradient();

        if (dialogText == null)
        {
            GameObject textObject = new("SystemDialogText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(dialogPanel.transform, false);
            dialogText = textObject.GetComponent<TextMeshProUGUI>();
        }

        // 无论 dialogText 是否新创建，都强制设置 RectTransform 保证占满面板
        RectTransform textRect = dialogText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        dialogText.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null)
            dialogText.font = fontAsset;
        ApplyFallbackFontsToCurrentFont();
        dialogText.color = textColor;
        dialogText.fontSize = fontSize;
        dialogText.enableWordWrapping = true;
        dialogText.overflowMode = TextOverflowModes.Ellipsis;
    }

    void EnsureDedicatedOverlayCanvas()
    {
        if (targetCanvas != null
            && targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            && targetCanvas.gameObject.name == DedicatedCanvasName)
        {
            targetCanvas.overrideSorting = true;
            targetCanvas.sortingOrder = Mathf.Max(targetCanvas.sortingOrder, dedicatedCanvasSortingOrder);
            return;
        }

        Canvas existing = null;
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c != null && c.gameObject.name == DedicatedCanvasName)
            {
                existing = c;
                break;
            }
        }

        if (existing == null)
        {
            GameObject canvasGo = new GameObject(DedicatedCanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            existing = canvasGo.GetComponent<Canvas>();
            existing.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        existing.overrideSorting = true;
        existing.sortingOrder = Mathf.Max(existing.sortingOrder, dedicatedCanvasSortingOrder);
        targetCanvas = existing;
    }

    /// <summary>
    /// 查找 DeliveryPromptsUI 根物体，若不存在则返回 null（不报错）。
    /// 用于排除其子级 Canvas，避免字幕面板被挂到 DeliveryPromptsUI 下。
    /// </summary>
    Transform FindDeliveryPromptsUIRoot()
    {
        var go = GameObject.Find("DeliveryPromptsUI");
        return go != null ? go.transform : null;
    }

    void ApplyFallbackFontsToCurrentFont()
    {
        if (dialogText == null || dialogText.font == null || fallbackFontAssets == null || fallbackFontAssets.Count == 0)
        {
            MergeTmpGlobalFallbacks();
            return;
        }

        if (dialogText.font.fallbackFontAssetTable == null)
            dialogText.font.fallbackFontAssetTable = new List<TMP_FontAsset>();

        for (int i = 0; i < fallbackFontAssets.Count; i++)
        {
            TMP_FontAsset fallback = fallbackFontAssets[i];
            if (fallback == null || fallback == dialogText.font)
                continue;
            if (!dialogText.font.fallbackFontAssetTable.Contains(fallback))
                dialogText.font.fallbackFontAssetTable.Add(fallback);
        }

        MergeTmpGlobalFallbacks();
    }

    void MergeTmpGlobalFallbacks()
    {
        if (dialogText == null || dialogText.font == null)
            return;

        var globalFallbacks = TMP_Settings.fallbackFontAssets;
        if (globalFallbacks == null || globalFallbacks.Count == 0)
            return;

        if (dialogText.font.fallbackFontAssetTable == null)
            dialogText.font.fallbackFontAssetTable = new List<TMP_FontAsset>();

        for (int i = 0; i < globalFallbacks.Count; i++)
        {
            TMP_FontAsset fallback = globalFallbacks[i];
            if (fallback == null || fallback == dialogText.font)
                continue;
            if (!dialogText.font.fallbackFontAssetTable.Contains(fallback))
                dialogText.font.fallbackFontAssetTable.Add(fallback);
        }
    }

    IEnumerator FadeOutPanel(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetPanelAlpha(1f - t);
            yield return null;
        }
        SetPanelAlpha(0f);
    }

    object WaitForSecondsByMode(float seconds)
    {
        return useUnscaledTime ? new WaitForSecondsRealtime(seconds) : new WaitForSeconds(seconds);
    }

    void EnsurePanelCanvasGroup()
    {
        if (dialogPanel == null)
            return;
        _panelCanvasGroup = dialogPanel.GetComponent<CanvasGroup>();
        if (_panelCanvasGroup == null)
            _panelCanvasGroup = dialogPanel.gameObject.AddComponent<CanvasGroup>();
    }

    void SetPanelAlpha(float alpha)
    {
        if (_panelCanvasGroup != null)
            _panelCanvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    /// <summary>
    /// 强制重置面板锚点和 pivot，确保面板水平撑满、底部对齐。
    /// 无论面板在 Unity Inspector 中如何预设，播放时都保证正确布局。
    /// </summary>
    void ResetPanelLayout()
    {
        if (dialogPanel == null) return;
        RectTransform rt = dialogPanel.rectTransform;
        rt.anchorMin = new Vector2(0f, panelAnchorY);
        rt.anchorMax = new Vector2(1f, panelAnchorY);

        // pivot 的 Y 与 anchorY 对齐，使面板紧贴锚点边缘
        rt.pivot = new Vector2(0.5f, panelAnchorY);
        rt.anchoredPosition = new Vector2(0f, panelOffsetY);
        rt.sizeDelta = new Vector2(0f, panelHeight);

        // 重置文字框布局：撑满面板内区域
        if (dialogText != null)
        {
            RectTransform textRt = dialogText.rectTransform;
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            textRt.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    void ApplyPanelGradient()
    {
        if (dialogPanel == null)
            return;
        if (!useCenterToEdgeFade)
        {
            dialogPanel.sprite = null;
            return;
        }

        int width = Mathf.Clamp(gradientTextureWidth, 16, 2048);
        float power = Mathf.Max(0.1f, gradientEdgeFadePower);

        ReleaseGradientAssets();
        _gradientTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        _gradientTexture.name = "SystemDialogCenterFade";
        _gradientTexture.wrapMode = TextureWrapMode.Clamp;
        _gradientTexture.filterMode = FilterMode.Bilinear;

        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            float distFromCenter = Mathf.Abs(t - 0.5f) * 2f;
            float alpha = Mathf.Pow(1f - distFromCenter, power);
            _gradientTexture.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
        }
        _gradientTexture.Apply(false, false);

        _gradientSprite = Sprite.Create(
            _gradientTexture,
            new Rect(0f, 0f, width, 1f),
            new Vector2(0.5f, 0.5f),
            100f);
        dialogPanel.sprite = _gradientSprite;
        dialogPanel.type = Image.Type.Simple;
        dialogPanel.preserveAspect = false;
    }

    void ReleaseGradientAssets()
    {
        if (_gradientSprite != null)
        {
            Destroy(_gradientSprite);
            _gradientSprite = null;
        }
        if (_gradientTexture != null)
        {
            Destroy(_gradientTexture);
            _gradientTexture = null;
        }
    }
}
