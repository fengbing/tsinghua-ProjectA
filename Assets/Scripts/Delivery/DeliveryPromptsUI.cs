using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 快递配送流程专用提示 UI。
/// 覆盖 TutorialHud 显示配送各阶段的文字提示。
/// </summary>
public class DeliveryPromptsUI : MonoBehaviour
{
    [Header("提示文字")]
    [SerializeField] TextMeshProUGUI promptText;
    [SerializeField] GameObject promptBackground;

    [Header("进度条（验证时使用）")]
    [SerializeField] Image progressFillImage;
    [SerializeField] GameObject progressBarContainer;
    [SerializeField] TextMeshProUGUI progressLabelText;

    [Header("弹窗（接收阳台提示）")]
    [SerializeField] GameObject modalDialog;
    [SerializeField] TextMeshProUGUI dialogTitleText;
    [SerializeField] TextMeshProUGUI dialogDescText;
    [SerializeField] Button dialogConfirmButton;

    [Header("显示/隐藏动画")]
    [SerializeField] float fadeDuration = 0.3f;

    [Header("音效 - 弹窗弹出")]
    [Tooltip("弹窗出现时播放的音效")]
    [SerializeField] AudioClip dialogOpenClip;
    [Tooltip("弹窗弹出音效的音量（0~1）")]
    [Range(0f, 1f)][SerializeField] float dialogOpenVolume = 1f;
    [Tooltip("弹窗弹出音效从第几秒开始播放")]
    [SerializeField] float dialogOpenStartTime = 0f;

    [Header("音效 - 弹窗关闭")]
    [Tooltip("点击确认按钮后弹窗隐藏时播放的音效")]
    [SerializeField] AudioClip dialogCloseClip;
    [Tooltip("弹窗关闭音效的音量（0~1）")]
    [Range(0f, 1f)][SerializeField] float dialogCloseVolume = 1f;
    [Tooltip("弹窗关闭音效从第几秒开始播放")]
    [SerializeField] float dialogCloseStartTime = 0f;

    CanvasGroup _canvasGroup;
    Coroutine _fadeRoutine;
    System.Action _dialogConfirmedCallback;
    AudioSource _audioSource;

    // 弹窗模式鼠标视角控制
    FollowCamera _followCamera;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        _followCamera = FindObjectOfType<FollowCamera>();

        if (promptBackground != null)
            promptBackground.SetActive(false);
        if (progressBarContainer != null)
            progressBarContainer.SetActive(false);

        if (modalDialog != null)
        {
            modalDialog.SetActive(false);
            if (dialogConfirmButton != null)
                dialogConfirmButton.onClick.AddListener(OnDialogConfirmClicked);
        }
    }

    void OnDestroy()
    {
        if (dialogConfirmButton != null)
            dialogConfirmButton.onClick.RemoveListener(OnDialogConfirmClicked);
    }

    void Start()
    {
        gameObject.SetActive(false);
    }

    void OnDialogConfirmClicked()
    {
        PlayDialogCloseSound();
        HideModalDialog();
        _dialogConfirmedCallback?.Invoke();
        _dialogConfirmedCallback = null;
    }

    /// <summary>显示弹窗，传入描述文字和确认后的回调。确认按钮按下后弹窗消失并调用回调。</summary>
    public void ShowModalDialog(string title, string description, System.Action onConfirmed)
    {
        _dialogConfirmedCallback = onConfirmed;

        if (dialogTitleText != null)
            dialogTitleText.text = title;
        if (dialogDescText != null)
            dialogDescText.text = description;

        if (promptBackground != null)
            promptBackground.SetActive(false);
        if (progressBarContainer != null)
            progressBarContainer.SetActive(false);

        gameObject.SetActive(true);
        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        if (modalDialog != null)
            modalDialog.SetActive(true);

        if (_followCamera != null)
            _followCamera.SetMouseLookAllowed(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PlayDialogOpenSound();
    }

    /// <summary>隐藏弹窗（不触发回调）</summary>
    public void HideModalDialog()
    {
        if (modalDialog != null)
            modalDialog.SetActive(false);

        if (_followCamera != null)
            _followCamera.SetMouseLookAllowed(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PlayDialogCloseSound();
    }

    /// <summary>显示一行提示文字。传入空字符串则隐藏。</summary>
    public void ShowPrompt(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            HideAll();
            return;
        }

        gameObject.SetActive(true);

        if (promptText != null)
            promptText.text = text;
        if (promptBackground != null)
            promptBackground.SetActive(true);

        if (progressBarContainer != null)
            progressBarContainer.SetActive(false);

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        // 非弹窗模式下锁定鼠标（弹窗模式由 ShowModalDialog 接管）
        if (!IsModalActive())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        AnimateFade(1f);
    }

    /// <summary>判断弹窗是否正在显示。</summary>
    public bool IsModalActive()
    {
        return modalDialog != null && modalDialog.activeSelf;
    }

    /// <summary>显示带进度条的提示（用于验证阶段）。</summary>
    public void ShowWithProgress(string text, float progress)
    {
        gameObject.SetActive(true);

        if (promptText != null)
            promptText.text = text;
        if (promptBackground != null)
            promptBackground.SetActive(true);
        if (progressBarContainer != null)
            progressBarContainer.SetActive(true);

        if (progressFillImage != null)
            progressFillImage.fillAmount = Mathf.Clamp01(progress);

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        if (!IsModalActive())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        AnimateFade(1f);
    }

    /// <summary>更新验证进度条。</summary>
    public void UpdateProgress(float progress)
    {
        if (progressFillImage != null)
            progressFillImage.fillAmount = Mathf.Clamp01(progress);
    }

    /// <summary>隐藏所有提示。</summary>
    public void HideAll()
    {
        AnimateFade(0f, () =>
        {
            if (gameObject != null)
                gameObject.SetActive(false);
        });

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void AnimateFade(float targetAlpha, System.Action onComplete = null)
    {
        if (_canvasGroup == null)
        {
            onComplete?.Invoke();
            return;
        }
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeAlpha(_canvasGroup.alpha, targetAlpha, fadeDuration, onComplete));
    }

    void PlayDialogOpenSound()
    {
        if (dialogOpenClip == null || _audioSource == null) return;
        float clampedTime = Mathf.Clamp(dialogOpenStartTime, 0f, dialogOpenClip.length);
        _audioSource.clip = dialogOpenClip;
        _audioSource.time = clampedTime;
        _audioSource.volume = dialogOpenVolume;
        _audioSource.loop = false;
        _audioSource.Play();
    }

    void PlayDialogCloseSound()
    {
        if (dialogCloseClip == null || _audioSource == null) return;
        float clampedTime = Mathf.Clamp(dialogCloseStartTime, 0f, dialogCloseClip.length);
        _audioSource.clip = dialogCloseClip;
        _audioSource.time = clampedTime;
        _audioSource.volume = dialogCloseVolume;
        _audioSource.loop = false;
        _audioSource.Play();
    }

    IEnumerator FadeAlpha(float from, float to, float duration, System.Action onComplete)
    {
        if (duration <= 0f)
        {
            _canvasGroup.alpha = to;
            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = to;
        onComplete?.Invoke();
    }
}
