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
    [Header("UI References (optional, auto-created when empty)")]
    [SerializeField] Canvas targetCanvas;
    [SerializeField] Image dialogPanel;
    [SerializeField] TextMeshProUGUI dialogText;

    [Header("Style")]
    [SerializeField] float panelHeight = 180f;
    [SerializeField] float panelAnchorY = 0f;
    [SerializeField] float panelOffsetY = 0f;
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

    [Header("Events")]
    public UnityEvent<int> onLineCompleted;
    public UnityEvent onDialogCompleted;

    Coroutine _playRoutine;
    bool _skipCurrentLine;
    bool _isPlaying;
    CanvasGroup _panelCanvasGroup;
    Texture2D _gradientTexture;
    Sprite _gradientSprite;

    public bool IsPlaying => _isPlaying;

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
        if (lines == null || lines.Count == 0)
        {
            HideDialog();
            return;
        }

        StopPlaybackAndCleanup();
        _playRoutine = StartCoroutine(PlayDialogRoutine(lines));
    }

    public void SkipCurrentLine()
    {
        _skipCurrentLine = true;
    }

    public void HideDialog()
    {
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
        if (dialogPanel != null)
            dialogPanel.gameObject.SetActive(true);
        SetPanelAlpha(1f);

        for (int i = 0; i < lines.Count; i++)
        {
            SystemDialogLine line = lines[i];
            _skipCurrentLine = false;
            PlayVoiceForLine(line);
            yield return TypeLine(line);
            onLineCompleted?.Invoke(i);
        }

        StopVoice();

        if (fadeOutAfterComplete)
        {
            yield return WaitForSecondsByMode(Mathf.Max(0f, completeHoldSeconds));
            yield return FadeOutPanel(Mathf.Max(0.01f, fadeOutDuration));
        }

        HideDialog();
        _isPlaying = false;
        onDialogCompleted?.Invoke();
    }

    IEnumerator TypeLine(SystemDialogLine line)
    {
        if (dialogText == null)
            yield break;

        string content = line?.text ?? string.Empty;
        dialogText.text = string.Empty;
        if (string.IsNullOrEmpty(content))
            yield break;

        float interval = line != null && line.characterInterval > 0f ? line.characterInterval : 0.04f;
        for (int i = 1; i <= content.Length; i++)
        {
            if (_skipCurrentLine)
            {
                dialogText.text = content;
                yield break;
            }

            dialogText.text = content.Substring(0, i);
            if (useUnscaledTime) yield return new WaitForSecondsRealtime(interval);
            else yield return new WaitForSeconds(interval);
        }
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

    void StopPlaybackAndCleanup()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
        _skipCurrentLine = false;
        _isPlaying = false;
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
        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();
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
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, panelOffsetY);
            panelRect.sizeDelta = new Vector2(0f, panelHeight);
        }
        else
        {
            RectTransform panelRect = dialogPanel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, panelAnchorY);
            panelRect.anchorMax = new Vector2(1f, panelAnchorY);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
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

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(48f, 24f);
            textRect.offsetMax = new Vector2(-48f, -24f);
        }

        dialogText.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null)
            dialogText.font = fontAsset;
        ApplyFallbackFontsToCurrentFont();
        dialogText.color = textColor;
        dialogText.fontSize = fontSize;
        dialogText.enableWordWrapping = true;
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
