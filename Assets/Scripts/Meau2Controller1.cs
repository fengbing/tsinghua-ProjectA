using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Meau2Controller : MonoBehaviour
{
    [Header("SystemDialogController")]
    [SerializeField] private SystemDialogController systemDialog;

    [Header("=== 按钮 ===")]
    [SerializeField] private Button studyButton;
    [SerializeField] private Button storageButton;
    [SerializeField] private Button level2Button;

    [Header("=== 跳转设置 ===")]
    [SerializeField] private Image blackScreenImage;
    [SerializeField] private float blackScreenFadeDuration = 0.4f;
    [SerializeField] private string targetStudyScene = "study";
    [SerializeField] private string targetStorageScene = "storage";
    [SerializeField] private string targetLevel2Scene = "Level 2";

    [Header("=== Storage 按钮跳转前的语音 ===")]
    [SerializeField] private AudioClip storageAudio1;
    [Range(0f, 1f)]
    [SerializeField] private float storageVolume1 = 1f;
    [SerializeField] private AudioClip storageAudio2;
    [Range(0f, 1f)]
    [SerializeField] private float storageVolume2 = 1f;

    [Header("=== Level 2 按钮跳转前的语音 ===")]
    [SerializeField] private AudioClip level2Audio1;
    [Range(0f, 1f)]
    [SerializeField] private float level2Volume1 = 1f;
    [SerializeField] private AudioClip level2Audio2;
    [Range(0f, 1f)]
    [SerializeField] private float level2Volume2 = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource storageAudioSource;

    private bool _transitionStarted;

    private void Start()
    {
        EnsureBlackScreenImage();
        EnsureStorageAudioSource();

        if (studyButton != null)
            studyButton.onClick.AddListener(BeginTransitionToStudy);

        if (storageButton != null)
            storageButton.onClick.AddListener(BeginTransitionToStorage);

        if (level2Button != null)
            level2Button.onClick.AddListener(BeginTransitionToLevel2);
    }

    private void OnDestroy()
    {
        if (studyButton != null)
            studyButton.onClick.RemoveListener(BeginTransitionToStudy);
        if (storageButton != null)
            storageButton.onClick.RemoveListener(BeginTransitionToStorage);
        if (level2Button != null)
            level2Button.onClick.RemoveListener(BeginTransitionToLevel2);
    }

    private void OnDisable()
    {
        if (studyButton != null)
            studyButton.onClick.RemoveListener(BeginTransitionToStudy);
        if (storageButton != null)
            storageButton.onClick.RemoveListener(BeginTransitionToStorage);
        if (level2Button != null)
            level2Button.onClick.RemoveListener(BeginTransitionToLevel2);
    }

    // ========================
    // Button 1: 跳转 study
    // ========================
    public void BeginTransitionToStudy()
    {
        if (_transitionStarted) return;
        _transitionStarted = true;
        Debug.Log("[Meau2] BeginTransitionToStudy");
        StartCoroutine(CoTransitionToStudy());
    }

    private IEnumerator CoTransitionToStudy()
    {
        if (systemDialog != null)
            systemDialog.HideSubtitle(forceFadeOut: false);

        EnsureBlackScreenImage();

        if (blackScreenImage == null)
        {
            SceneManager.LoadScene(targetStudyScene);
            yield break;
        }

        if (string.IsNullOrEmpty(targetStudyScene))
            yield break;

        blackScreenImage.gameObject.SetActive(true);
        var col = blackScreenImage.color;
        col.a = 0f;
        blackScreenImage.color = col;

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetStudyScene);
        if (loadOp == null)
        {
            SceneManager.LoadScene(targetStudyScene);
            yield break;
        }
        loadOp.allowSceneActivation = false;

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

        yield return new WaitUntil(() => loadOp.progress >= 0.9f);
        loadOp.allowSceneActivation = true;
    }

    // ========================
    // Button 2: 跳转 storage（先播两段语音）
    // ========================
    public void BeginTransitionToStorage()
    {
        if (_transitionStarted) return;
        _transitionStarted = true;
        Debug.Log("[Meau2] BeginTransitionToStorage");
        StartCoroutine(CoTransitionToStorage());
    }

    private IEnumerator CoTransitionToStorage()
    {
        if (systemDialog != null)
            systemDialog.HideSubtitle(forceFadeOut: false);

        EnsureBlackScreenImage();

        if (blackScreenImage == null)
        {
            SceneManager.LoadScene(targetStorageScene);
            yield break;
        }

        if (string.IsNullOrEmpty(targetStorageScene))
            yield break;

        blackScreenImage.gameObject.SetActive(true);
        var col = blackScreenImage.color;
        col.a = 0f;
        blackScreenImage.color = col;

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetStorageScene);
        if (loadOp == null)
        {
            SceneManager.LoadScene(targetStorageScene);
            yield break;
        }
        loadOp.allowSceneActivation = false;

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

        yield return new WaitUntil(() => loadOp.progress >= 0.9f);
        Debug.Log("[Meau2] storage 场景预加载完成，等待语音播完");

        yield return StartCoroutine(PlayAudio(storageAudio1, storageVolume1));
        yield return StartCoroutine(PlayAudio(storageAudio2, storageVolume2));

        Debug.Log("[Meau2] 语音播完，激活 storage 场景");
        loadOp.allowSceneActivation = true;
    }

    // ========================
    // Button 3: 跳转 Level 2（先播两段语音）
    // ========================
    public void BeginTransitionToLevel2()
    {
        if (_transitionStarted) return;
        _transitionStarted = true;
        Debug.Log("[Meau2] BeginTransitionToLevel2");
        StartCoroutine(CoTransitionToLevel2());
    }

    private IEnumerator CoTransitionToLevel2()
    {
        if (systemDialog != null)
            systemDialog.HideSubtitle(forceFadeOut: false);

        EnsureBlackScreenImage();

        if (blackScreenImage == null)
        {
            SceneManager.LoadScene(targetLevel2Scene);
            yield break;
        }

        if (string.IsNullOrEmpty(targetLevel2Scene))
            yield break;

        blackScreenImage.gameObject.SetActive(true);
        var col = blackScreenImage.color;
        col.a = 0f;
        blackScreenImage.color = col;

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetLevel2Scene);
        if (loadOp == null)
        {
            SceneManager.LoadScene(targetLevel2Scene);
            yield break;
        }
        loadOp.allowSceneActivation = false;

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

        yield return new WaitUntil(() => loadOp.progress >= 0.9f);
        Debug.Log("[Meau2] Level 2 场景预加载完成，等待语音播完");

        yield return StartCoroutine(PlayAudio(level2Audio1, level2Volume1));
        yield return StartCoroutine(PlayAudio(level2Audio2, level2Volume2));

        Debug.Log("[Meau2] 语音播完，激活 Level 2 场景");
        loadOp.allowSceneActivation = true;
    }

    private IEnumerator PlayAudio(AudioClip clip, float volume)
    {
        Debug.Log($"[Meau2] PlayAudio — clip: {(clip != null ? clip.name : "NULL")}, volume: {volume}");

        if (clip == null)
        {
            Debug.LogWarning("[Meau2] clip is null, skipping");
            yield return new WaitForSecondsRealtime(1.5f);
            yield break;
        }

        AudioSource source = null;

        if (systemDialog != null && systemDialog.VoiceSource != null)
            source = systemDialog.VoiceSource;
        else if (storageAudioSource != null)
            source = storageAudioSource;
        else
        {
            Debug.LogWarning("[Meau2] No audio source available");
            yield return new WaitForSecondsRealtime(clip.length);
            yield break;
        }

        source.volume = volume;
        source.PlayOneShot(clip);
        Debug.Log($"[Meau2] Playing audio, length: {clip.length}s");
        yield return new WaitForSecondsRealtime(clip.length);
        Debug.Log("[Meau2] PlayAudio finished");
    }

    private void EnsureBlackScreenImage()
    {
        if (blackScreenImage != null)
        {
            blackScreenImage.gameObject.SetActive(false);
            var c = blackScreenImage.color;
            c.a = 0f;
            blackScreenImage.color = c;
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Meau2] 场景中未找到 Canvas！");
            return;
        }

        var go = new GameObject("Meau2BlackScreen", typeof(RectTransform), typeof(Image));
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
    }

    private void EnsureStorageAudioSource()
    {
        if (storageAudioSource != null)
            return;
        storageAudioSource = GetComponent<AudioSource>();
        if (storageAudioSource == null)
            storageAudioSource = gameObject.AddComponent<AudioSource>();
        storageAudioSource.playOnAwake = false;
        storageAudioSource.loop = false;
    }
}
