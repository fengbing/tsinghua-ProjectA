using System.Collections;
using UnityEngine;

/// <summary>
/// Storage 场景剧情：全屏图片阶段按顺序自动推进，每页对应语音播完后进入下一页（无需按键）。
/// 第二加载页两段语音顺序播放完毕后自动进入「场景内」阶段：隐藏 UI，此时为相机视角可鼠标环视，
/// <c>PlaneController</c> 仍为关闭（无移动）；<c>clipGameplayA</c>、<c>clipGameplayB</c> 顺序播完后才恢复无人机移动输入。
/// 取件码成功时播放音效并 <see cref="MinimapUiController.PerformToggle"/>。
/// </summary>
public class StorageNarrativeController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject panelLoading1;
    [SerializeField] GameObject panelStory;
    [SerializeField] GameObject panelLoading2;

    [Header("音频")]
    [SerializeField] AudioSource narrativeAudio;
    [Tooltip("第一加载页")]
    [SerializeField] AudioClip clipLoading1;
    [Tooltip("背景故事页")]
    [SerializeField] AudioClip clipStory;
    [Tooltip("第二加载页，顺序播放")]
    [SerializeField] AudioClip clipLoading2A;
    [SerializeField] AudioClip clipLoading2B;
    [Tooltip("面板隐藏后：进入相机视角时第一段（此段期间仅能转动视角，不能移动）")]
    [SerializeField] AudioClip clipGameplayA;
    [SerializeField] AudioClip clipGameplayB;
    [Header("无人机")]
    [SerializeField] PlaneController planeController;
    [Tooltip("剧情全过程禁用移动直至 clipGameplayB 播完")]
    [SerializeField] bool disablePlaneUntilNarrativeDone = true;

    [Header("SystemDialogController")]
    [SerializeField] SystemDialogController systemDialog;

    [Header("前置旁白（场景开始2秒后顺序播放）")]
    [TextArea(2, 5)]
    [SerializeField] string narrationText1 = "";
    [SerializeField] AudioClip narrationAudio1;
    [Range(0f, 1f)]
    [SerializeField] float narrationVolume1 = 1f;

    [TextArea(2, 5)]
    [SerializeField] string narrationText2 = "";
    [SerializeField] AudioClip narrationAudio2;
    [Range(0f, 1f)]
    [SerializeField] float narrationVolume2 = 1f;

    [TextArea(2, 5)]
    [SerializeField] string narrationText3 = "";
    [SerializeField] AudioClip narrationAudio3;
    [Range(0f, 1f)]
    [SerializeField] float narrationVolume3 = 1f;

    [Header("密码成功后旁白")]
    [TextArea(2, 5)]
    [SerializeField] string pickupNarrationText = "";
    [SerializeField] AudioClip pickupNarrationAudio;
    [Range(0f, 1f)]
    [SerializeField] float pickupNarrationVolume = 1f;

    bool _flowFinished;
    bool _pickupNarrationPlayed;
    DecryptPuzzleUI _puzzleUi;
    MinimapUiController _minimapController;

    void Awake()
    {
        if (narrativeAudio == null)
            narrativeAudio = GetComponent<AudioSource>();
        if (narrativeAudio == null)
            narrativeAudio = gameObject.AddComponent<AudioSource>();
        narrativeAudio.playOnAwake = false;
        narrativeAudio.loop = false;

        if (planeController == null)
            planeController = FindObjectOfType<PlaneController>();
        _minimapController = FindObjectOfType<MinimapUiController>();
    }

    void Start()
    {
        if (systemDialog == null)
            systemDialog = FindObjectOfType<SystemDialogController>();

        _puzzleUi = DecryptPuzzleUI.Instance != null ? DecryptPuzzleUI.Instance : FindObjectOfType<DecryptPuzzleUI>();
        if (_puzzleUi != null)
            _puzzleUi.OnDecryptPuzzleSolved += OnPickupSuccess;

        if (disablePlaneUntilNarrativeDone && planeController != null)
            planeController.SetInputEnabled(false);

        StartCoroutine(StartNarrativeSequence());
    }

    IEnumerator StartNarrativeSequence()
    {
        yield return new WaitForSecondsRealtime(2f);

        yield return PlayNarration(narrationText1, narrationAudio1, narrationVolume1);
        yield return PlayNarration(narrationText2, narrationAudio2, narrationVolume2);
        yield return PlayNarration(narrationText3, narrationAudio3, narrationVolume3);

        yield return StartCoroutine(RunNarrativeFlow());
    }

    void OnDestroy()
    {
        if (_puzzleUi != null)
            _puzzleUi.OnDecryptPuzzleSolved -= OnPickupSuccess;
    }

    IEnumerator RunNarrativeFlow()
    {
        ShowOnlyPanel(panelLoading1);
        yield return PlayClipBlocking(clipLoading1);

        ShowOnlyPanel(panelStory);
        yield return PlayClipBlocking(clipStory);

        ShowOnlyPanel(panelLoading2);
        yield return PlayClipBlocking(clipLoading2A);
        yield return PlayClipBlocking(clipLoading2B);

        // 相机视角：隐藏全屏 UI，FollowCamera 仍可鼠标转动；Plane 仍处于禁用，无平移/升降。
        HideAllPanels();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yield return PlayClipBlocking(clipGameplayA);
        yield return PlayClipBlocking(clipGameplayB);

        _flowFinished = true;
        if (disablePlaneUntilNarrativeDone && planeController != null)
            planeController.SetInputEnabled(true);
    }

    void HideAllPanels()
    {
        if (panelLoading1 != null) panelLoading1.SetActive(false);
        if (panelStory != null) panelStory.SetActive(false);
        if (panelLoading2 != null) panelLoading2.SetActive(false);
    }

    void ShowOnlyPanel(GameObject active)
    {
        if (panelLoading1 != null) panelLoading1.SetActive(panelLoading1 == active);
        if (panelStory != null) panelStory.SetActive(panelStory == active);
        if (panelLoading2 != null) panelLoading2.SetActive(panelLoading2 == active);
    }

    IEnumerator PlayClipBlocking(AudioClip clip)
    {
        if (clip == null || narrativeAudio == null) yield break;
        narrativeAudio.PlayOneShot(clip);
        yield return new WaitForSeconds(clip.length);
    }

    void OnPickupSuccess()
    {
        if (!_flowFinished) return;
        if (_pickupNarrationPlayed) return;
        _pickupNarrationPlayed = true;

        StartCoroutine(PlayPickupNarration());
    }

    IEnumerator PlayPickupNarration()
    {
        yield return PlayNarration(pickupNarrationText, pickupNarrationAudio, pickupNarrationVolume);

        var map = _minimapController != null ? _minimapController : FindObjectOfType<MinimapUiController>();
        if (map != null)
            map.PerformToggle();
    }

    IEnumerator PlayNarration(string text, AudioClip clip, float volume)
    {
        if (systemDialog == null)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            yield break;
        }

        var voiceSrc = systemDialog.VoiceSource;
        float savedVolume = 1f;
        if (voiceSrc != null)
        {
            savedVolume = voiceSrc.volume;
            voiceSrc.volume = volume;
        }

        if (clip != null && !string.IsNullOrEmpty(text))
        {
            systemDialog.ShowSubtitle(text, clip.length, clip);
            yield return new WaitForSecondsRealtime(clip.length);
        }
        else if (clip != null)
        {
            if (voiceSrc != null)
                voiceSrc.PlayOneShot(clip);
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

        if (voiceSrc != null)
            voiceSrc.volume = savedVolume;
        systemDialog.HideSubtitle(forceFadeOut: false);
    }
}
