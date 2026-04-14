using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MeauIntroController : MonoBehaviour
{
    [Header("SystemDialogController")]
    [SerializeField] private SystemDialogController systemDialog;

    [Header("=== 跳转设置（Button 触发）===")]
    [SerializeField] private Button transitionButton;
    [SerializeField] private Image blackScreenImage;
    [SerializeField] private float blackScreenFadeDuration = 0.4f;
    [SerializeField] private string targetSceneName = "study";

    [Header("旁白 1")]
    [TextArea(2, 5)]
    [SerializeField] private string narrationText1 = "旁白1文字";
    [SerializeField] private AudioClip narrationAudio1;
    [Range(0f, 1f)]
    [SerializeField] private float narrationVolume1 = 1f;

    [Header("旁白 2")]
    [TextArea(2, 5)]
    [SerializeField] private string narrationText2 = "旁白2文字";
    [SerializeField] private AudioClip narrationAudio2;
    [Range(0f, 1f)]
    [SerializeField] private float narrationVolume2 = 1f;

    [Header("旁白 3")]
    [TextArea(2, 5)]
    [SerializeField] private string narrationText3 = "旁白3文字";
    [SerializeField] private AudioClip narrationAudio3;
    [Range(0f, 1f)]
    [SerializeField] private float narrationVolume3 = 1f;

    [Header("旁白 4")]
    [TextArea(2, 5)]
    [SerializeField] private string narrationText4 = "旁白4文字";
    [SerializeField] private AudioClip narrationAudio4;
    [Range(0f, 1f)]
    [SerializeField] private float narrationVolume4 = 1f;

    private static bool _hasPlayedThisRun;
    private bool _transitionStarted;

    private void Start()
    {
        if (_hasPlayedThisRun)
        {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
            Destroy(gameObject);
            return;
        }

        EnsureBlackScreenImage();

        StartCoroutine(PlayIntroSequence());

        if (transitionButton != null)
            transitionButton.onClick.AddListener(BeginTransitionToStudy);
    }

    private void OnDestroy()
    {
        if (transitionButton != null)
            transitionButton.onClick.RemoveListener(BeginTransitionToStudy);
    }

    private void OnDisable()
    {
        if (transitionButton != null)
            transitionButton.onClick.RemoveListener(BeginTransitionToStudy);
    }

    private IEnumerator PlayIntroSequence()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        yield return new WaitForSecondsRealtime(1f);

        if (systemDialog == null)
            systemDialog = FindObjectOfType<SystemDialogController>();

        float originalVolume = 1f;
        if (systemDialog != null && systemDialog.VoiceSource != null)
            originalVolume = systemDialog.VoiceSource.volume;

        yield return StartCoroutine(PlayNarration(narrationText1, narrationAudio1, narrationVolume1));
        yield return StartCoroutine(PlayNarration(narrationText2, narrationAudio2, narrationVolume2));
        yield return StartCoroutine(PlayNarration(narrationText3, narrationAudio3, narrationVolume3));
        yield return StartCoroutine(PlayNarration(narrationText4, narrationAudio4, narrationVolume4));

        if (systemDialog != null && systemDialog.VoiceSource != null)
            systemDialog.VoiceSource.volume = originalVolume;

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
        _hasPlayedThisRun = true;
    }

    private IEnumerator PlayNarration(string text, AudioClip clip, float volume)
    {
        if (systemDialog == null)
        {
            yield break;
        }

        if (systemDialog.VoiceSource != null)
            systemDialog.VoiceSource.volume = volume;

        if (clip != null && !string.IsNullOrEmpty(text))
        {
            systemDialog.ShowSubtitle(text, clip.length, clip);
            yield return new WaitForSecondsRealtime(clip.length);
        }
        else if (clip != null)
        {
            if (systemDialog.VoiceSource != null)
                systemDialog.VoiceSource.PlayOneShot(clip);
            yield return new WaitForSecondsRealtime(clip.length);
        }
        else if (!string.IsNullOrEmpty(text))
        {
            float estimatedDuration = text.Length * 0.04f + 0.5f;
            systemDialog.ShowSubtitle(text, estimatedDuration, null);
            yield return new WaitForSecondsRealtime(estimatedDuration);
        }
        else
        {
            yield return new WaitForSecondsRealtime(1.5f);
        }

        if (systemDialog != null)
            systemDialog.HideSubtitle(forceFadeOut: false);
    }

    public void BeginTransitionToStudy()
    {
        if (_transitionStarted)
        {
            Debug.LogWarning("[MeauIntro] 已被调用，忽略重复");
            return;
        }
        _transitionStarted = true;
        Debug.Log($"[MeauIntro] BeginTransitionToStudy → target='{targetSceneName}'");
        StartCoroutine(CoTransitionToStudy());
    }

    private IEnumerator CoTransitionToStudy()
    {
        Debug.Log($"[MeauIntro] ★★★ CoTransitionToStudy 协程开始执行 ★★★");

        if (systemDialog != null)
            systemDialog.HideSubtitle(forceFadeOut: false);

        EnsureBlackScreenImage();

        if (blackScreenImage == null)
        {
            Debug.LogError("[MeauIntro] blackScreenImage 为空，直接跳转！");
            SceneManager.LoadScene(targetSceneName);
            yield break;
        }

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[MeauIntro] targetSceneName 为空！");
            yield break;
        }

        // Step 1: 激活黑幕，alpha 归零
        blackScreenImage.gameObject.SetActive(true);
        var col = blackScreenImage.color;
        col.a = 0f;
        blackScreenImage.color = col;
        Debug.Log($"[MeauIntro] 黑幕激活，alpha=0");

        // Step 2: 立刻开始异步加载场景（不阻塞）
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetSceneName);
        if (loadOp == null)
        {
            Debug.LogError($"[MeauIntro] LoadSceneAsync 返回 null，场景='{targetSceneName}'，检查 Build Settings");
            SceneManager.LoadScene(targetSceneName);
            yield break;
        }
        loadOp.allowSceneActivation = false;
        Debug.Log($"[MeauIntro] 异步加载开始，场景='{targetSceneName}'");

        // Step 3: 黑幕从 0 淡入到 1（与加载并行）
        float elapsed = 0f;
        float duration = blackScreenFadeDuration > 0f ? blackScreenFadeDuration : 0.4f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (blackScreenImage == null) yield break;
            var c = blackScreenImage.color;
            c.a = Mathf.Clamp01(elapsed / duration);
            blackScreenImage.color = c;
            yield return null;
        }
        if (blackScreenImage != null)
        {
            var c = blackScreenImage.color;
            c.a = 1f;
            blackScreenImage.color = c;
        }
        Debug.Log($"[MeauIntro] 黑幕淡入完成，alpha=1");

        // Step 4: 等待加载进度达到 0.9
        yield return new WaitUntil(() => loadOp.progress >= 0.9f);
        Debug.Log($"[MeauIntro] 场景加载完成，progress={loadOp.progress:F3}");

        // Step 5: 激活场景切换，黑幕持续到场景切换完成
        loadOp.allowSceneActivation = true;
        Debug.Log($"[MeauIntro] allowSceneActivation=true，黑幕保持，场景切换中...");
    }

    private void EnsureBlackScreenImage()
    {
        if (blackScreenImage != null)
        {
            // 强制重置为初始隐藏状态（无论 Inspector 里拖入的 Image 是什么状态）
            blackScreenImage.gameObject.SetActive(false);
            var col = blackScreenImage.color;
            col.a = 0f;
            blackScreenImage.color = col;
            Debug.Log($"[MeauIntro] 黑幕 Image 已强制重置，activeSelf={blackScreenImage.gameObject.activeSelf}，alpha={blackScreenImage.color.a}");
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[MeauIntro] 场景中未找到任何 Canvas！请确认 Canvas 已正确设置。");
            return;
        }

        var go = new GameObject("MeauBlackScreen", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        blackScreenImage = go.GetComponent<Image>();
        blackScreenImage.color = new Color(0f, 0f, 0f, 0f);
        blackScreenImage.raycastTarget = false;
        go.SetActive(false);

        Debug.Log($"[MeauIntro] 黑幕 Image 自动创建完成，activeSelf={go.activeSelf}，color={blackScreenImage.color}");
    }
}
