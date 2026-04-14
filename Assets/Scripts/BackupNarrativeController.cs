using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Backup 场景四段旁白控制器：
/// 场景加载2秒后播放旁白1、找到目标住户（到达楼栋外）播放旁白2、
/// 楼顶验证成功后播放旁白3、找到目标阳台（准星命中）播放旁白4。
/// 每段旁白的文字、音频、音量均可通过 Inspector 面板配置。
/// </summary>
public class BackupNarrativeController : MonoBehaviour
{
    [Header("SystemDialogController")]
    [SerializeField] private SystemDialogController systemDialog;

    [Header("旁白1 — 场景加载2秒后")]
    [TextArea(2, 5)]
    [SerializeField] private string narration1Text = "";
    [SerializeField] private AudioClip narration1Audio;
    [Range(0f, 1f)]
    [SerializeField] private float narration1Volume = 1f;

    [Header("旁白2 — 找到目标住户（到达楼栋外）")]
    [TextArea(2, 5)]
    [SerializeField] private string narration2Text = "";
    [SerializeField] private AudioClip narration2Audio;
    [Range(0f, 1f)]
    [SerializeField] private float narration2Volume = 1f;

    [Header("旁白3 — 楼顶验证成功")]
    [TextArea(2, 5)]
    [SerializeField] private string narration3Text = "";
    [SerializeField] private AudioClip narration3Audio;
    [Range(0f, 1f)]
    [SerializeField] private float narration3Volume = 1f;

    [Header("旁白4 — 找到目标阳台（准星命中）")]
    [TextArea(2, 5)]
    [SerializeField] private string narration4Text = "";
    [SerializeField] private AudioClip narration4Audio;
    [Range(0f, 1f)]
    [SerializeField] private float narration4Volume = 1f;

    private bool _narration1Played;
    private bool _narration2Played;
    private bool _narration3Played;
    private bool _narration4Played;

    void Awake()
    {
        if (systemDialog == null)
            systemDialog = FindObjectOfType<SystemDialogController>();
    }

    void Start()
    {
        SubscribeToEvents();
        TryStartFallbackTimer();
    }

    void TryStartFallbackTimer()
    {
        StartCoroutine(CoPlayNarration1AfterDelay());
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    void SubscribeToEvents()
    {
        // 旁白2：DeliveryPhaseManager 状态变为 DroneAtBuildingExterior
        if (DeliveryPhaseManager.Instance != null)
            DeliveryPhaseManager.Instance.OnStateChanged += OnDeliveryStateChanged;
        else
            Debug.LogWarning("[BackupNarrativeController] DeliveryPhaseManager 未找到，旁白2将不会触发（请确保场景中已添加该组件）");

        // 旁白3：RooftopVerifier 验证成功
        var verifier = FindObjectOfType<RooftopVerifier>();
        if (verifier != null)
            verifier.OnVerifyComplete += OnRooftopVerifyComplete;
        else
            Debug.LogWarning("[BackupNarrativeController] RooftopVerifier 未找到，旁白3将不会触发（请确保场景中已添加该组件）");

        // 旁白4：CrosshairTargetDetector 命中目标阳台
        var detector = FindObjectOfType<CrosshairTargetDetector>();
        if (detector != null)
            detector.OnTargetHit += OnTargetBalconyHit;
        else
            Debug.LogWarning("[BackupNarrativeController] CrosshairTargetDetector 未找到，旁白4将不会触发（请确保场景中已添加该组件）");
    }

    void UnsubscribeFromEvents()
    {
        if (DeliveryPhaseManager.Instance != null)
            DeliveryPhaseManager.Instance.OnStateChanged -= OnDeliveryStateChanged;

        var verifier = FindObjectOfType<RooftopVerifier>();
        if (verifier != null)
            verifier.OnVerifyComplete -= OnRooftopVerifyComplete;

        var detector = FindObjectOfType<CrosshairTargetDetector>();
        if (detector != null)
            detector.OnTargetHit -= OnTargetBalconyHit;
    }

    // ========== 事件回调 ==========

    void OnDeliveryStateChanged(DeliveryPhaseManager.DeliveryState newState)
    {
        if (newState == DeliveryPhaseManager.DeliveryState.DroneAtBuildingExterior)
            TryPlayNarration2();
    }

    void OnRooftopVerifyComplete()
    {
        TryPlayNarration3();
    }

    void OnTargetBalconyHit()
    {
        TryPlayNarration4();
    }

    // ========== 延迟播放协程 ==========

    IEnumerator CoPlayNarration1AfterDelay()
    {
        yield return new WaitForSecondsRealtime(5f);
        TryPlayNarration1();
    }

    // ========== 触发尝试（带防重复） ==========

    void TryPlayNarration1()
    {
        if (_narration1Played) return;
        _narration1Played = true;
        StartCoroutine(PlayNarration(narration1Text, narration1Audio, narration1Volume));
    }

    void TryPlayNarration2()
    {
        if (_narration2Played) return;
        _narration2Played = true;
        StartCoroutine(PlayNarration(narration2Text, narration2Audio, narration2Volume));
    }

    void TryPlayNarration3()
    {
        if (_narration3Played) return;
        _narration3Played = true;
        StartCoroutine(PlayNarration(narration3Text, narration3Audio, narration3Volume));
    }

    void TryPlayNarration4()
    {
        if (_narration4Played) return;
        _narration4Played = true;
        StartCoroutine(PlayNarration(narration4Text, narration4Audio, narration4Volume));

        var manager = DeliveryPhaseManager.Instance;
        if (manager != null)
            manager.useBackupScenePositionLerp = true;
    }

    // ========== 核心播放方法 ==========

    /// <summary>
    /// 通过 SystemDialogController 播放黑底字幕 + 配音，并控制音量。
    /// </summary>
    IEnumerator PlayNarration(string text, AudioClip clip, float volume)
    {
        if (systemDialog == null)
        {
            Debug.LogWarning("[BackupNarrativeController] SystemDialogController 为空，无法播放旁白");
            yield break;
        }

        var voiceSrc = systemDialog.VoiceSource;
        float savedVolume = voiceSrc != null ? voiceSrc.volume : 1f;
        if (voiceSrc != null)
            voiceSrc.volume = volume;

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

    // ========== 公开重置接口（用于测试或重新开始任务）==========
    public void ResetAll()
    {
        _narration1Played = false;
        _narration2Played = false;
        _narration3Played = false;
        _narration4Played = false;
    }
}
