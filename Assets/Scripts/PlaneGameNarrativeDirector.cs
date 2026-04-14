using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PlaneGame 叙事音频：进场 BGM；HUD 同源距离 ≤ 阈值时停 BGM 并播放一次性近距音效；
/// 成功后顺序播放三段语音并加载 Level 2。
/// </summary>
public class PlaneGameNarrativeDirector : MonoBehaviour
{
    // ==================== 静态暂停系统 ====================
    public static bool IsPaused { get; private set; }

    public static event System.Action OnGamePaused;
    public static event System.Action OnGameResumed;

    public static void PauseGame()
    {
        if (IsPaused) return;
        IsPaused = true;

        // 从当前场景任意 PlaneController 入手，禁用其输入
        var plane = FindObjectOfType<PlaneController>();
        if (plane != null)
            plane.SetInputEnabled(false);

        // 禁用所有 DroneGripper 的 F 键（向上查找父级 PlaneController）
        foreach (var gripper in FindObjectsOfType<DroneGripper>())
        {
            var pc = gripper.GetComponentInParent<PlaneController>();
            if (pc != null)
                pc.SetInputEnabled(false);
        }

        var followCam = FindObjectOfType<FollowCamera>();
        if (followCam != null) followCam.Pause();

        OnGamePaused?.Invoke();
    }

    public static void ResumeGame()
    {
        if (!IsPaused) return;
        IsPaused = false;

        var plane = FindObjectOfType<PlaneController>();
        if (plane != null)
            plane.SetInputEnabled(true);

        foreach (var gripper in FindObjectsOfType<DroneGripper>())
        {
            var pc = gripper.GetComponentInParent<PlaneController>();
            if (pc != null)
                pc.SetInputEnabled(true);
        }

        var followCam = FindObjectOfType<FollowCamera>();
        if (followCam != null) followCam.Resume();

        OnGameResumed?.Invoke();
    }

    // ==================== 字段 ====================
    [Header("距离（与 DistanceHudStrip 上 DistanceToTargetSource 一致）")]
    [SerializeField] DistanceToTargetSource distanceSource;
    [SerializeField] float proximityThresholdMeters = 50f;
    [Tooltip("若为 true：离开阈值后再进入会再次播放近距音效（默认 false = 每局仅一次）。")]
    [SerializeField] bool resetProximityCueWhenLeavingThreshold;

    [Header("音频")]
    [Tooltip("留空则在同物体上添加并用于循环 BGM")]
    [SerializeField] AudioSource bgmSource;
    [Tooltip("留空则自动创建子物体用于近距音效与语音")]
    [SerializeField] AudioSource voiceSource;
    [SerializeField] AudioClip bgmLoop;
    [Tooltip("BGM 音量 (0-1)")]
    [SerializeField] [Range(0f, 1f)] float bgmVolume = 0.6f;
    [SerializeField] AudioClip proximityStinger;
    [SerializeField] AudioClip voiceLine1;
    [SerializeField] AudioClip voiceLine2;
    [SerializeField] AudioClip voiceLine3;
    [SerializeField] AudioClip voiceLine4;
    [SerializeField] AudioClip voiceLine5;
    [SerializeField] AudioClip voiceLine6;

    [Header("字幕（对应每个音效）")]
    [Tooltip("近距音效 proximityStinger 对应的字幕文字")]
    [TextArea(1, 3)][SerializeField] string subtitleProximityStinger;
    [Tooltip("语音 voiceLine1 对应的字幕文字")]
    [TextArea(1, 3)][SerializeField] string subtitleVoiceLine1;
    [Tooltip("语音 voiceLine2 对应的字幕文字")]
    [TextArea(1, 3)][SerializeField] string subtitleVoiceLine2;
    [Tooltip("语音 voiceLine3 第1段字幕文字")]
    [TextArea(1, 2)][SerializeField] string subtitleVoiceLine3_Part1;
    [Tooltip("语音 voiceLine3 第2段字幕文字")]
    [TextArea(1, 2)][SerializeField] string subtitleVoiceLine3_Part2;
    [Tooltip("语音 voiceLine3 第3段字幕文字")]
    [TextArea(1, 2)][SerializeField] string subtitleVoiceLine3_Part3;
    [Tooltip("语音 voiceLine4 对应的字幕文字")]
    [TextArea(1, 3)][SerializeField] string subtitleVoiceLine4;
    [Tooltip("语音 voiceLine5 对应的字幕文字")]
    [TextArea(1, 3)][SerializeField] string subtitleVoiceLine5;
    [Tooltip("语音 voiceLine6 对应的字幕文字")]
    [TextArea(1, 3)][SerializeField] string subtitleVoiceLine6;
    [Tooltip("字幕显示时长（秒），0 = 自动使用音频长度")]
    [SerializeField] float subtitleDurationOverride = 0f;

    [Header("过场")]
    [SerializeField] string nextSceneName = "Level 2";

    [Header("配送流程")]
    [Tooltip("留空则不启动配送流程（仅用于测试）；填入则在此脚本触发近距音效时同步启动配送")]
    [SerializeField] DeliveryPhaseManager deliveryPhaseManager;

    [Header("字幕")]
    [Tooltip("留空则自动查找场景中的 SystemDialogController")]
    [SerializeField] SystemDialogController subtitleController;

    [Header("包裹损坏检测")]
    [Tooltip("场景开始后延迟 N 秒再开启包裹损坏检测")]
    [SerializeField] float brokenDetectionDelay = 5f;
    [Tooltip("包裹下落超过此值（米）判定为损坏")]
    [SerializeField] float brokenFallThreshold = 2f;

    bool _proximityCuePlayed;
    bool _wasInsideProximity;
    bool _outroStarted;
    bool _loggedMissingDistance;
    bool _brokenDetected;

    public bool IsOutroStarted => _outroStarted;

    void Awake()
    {
        if (bgmSource == null)
        {
            bgmSource = GetComponent<AudioSource>();
            if (bgmSource == null)
                bgmSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.playOnAwake = false;
        bgmSource.loop = false;

        if (voiceSource == null)
        {
            var voiceGo = new GameObject("NarrativeVoiceAudio");
            voiceGo.transform.SetParent(transform, false);
            voiceSource = voiceGo.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
        }

        if (distanceSource == null)
            distanceSource = FindObjectOfType<DistanceToTargetSource>();

        if (subtitleController == null)
            subtitleController = FindObjectOfType<SystemDialogController>();
    }

    void Start()
    {
        if (bgmLoop == null || bgmSource == null)
            return;
        bgmSource.volume = bgmVolume;
        bgmSource.clip = bgmLoop;
        bgmSource.loop = true;
        bgmSource.Play();

        // 立即开启包裹损坏检测
        var gripper = FindObjectOfType<DroneGripper>();
        if (gripper != null)
        {
            gripper.OnPackageReleased += OnPackageReleased;
            Debug.Log($"[PlaneGameNarrativeDirector] 包裹损坏检测已开启，立即生效，阈值={brokenFallThreshold}m");
        }
    }

    void Update()
    {
        if (distanceSource == null)
        {
            if (!_loggedMissingDistance)
            {
                _loggedMissingDistance = true;
                Debug.LogWarning("[PlaneGameNarrativeDirector] 未指定 DistanceToTargetSource，近距音效已禁用。");
            }
            return;
        }

        float d = distanceSource.GetDistanceMeters();
        bool inside = d <= proximityThresholdMeters;

        if (resetProximityCueWhenLeavingThreshold)
        {
            if (_wasInsideProximity && !inside)
                _proximityCuePlayed = false;
        }
        _wasInsideProximity = inside;

        if (!inside || _proximityCuePlayed)
            return;

        _proximityCuePlayed = true;
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
        if (proximityStinger != null && voiceSource != null)
            voiceSource.PlayOneShot(proximityStinger);
        if (deliveryPhaseManager != null)
            deliveryPhaseManager.StartDeliveryFlow();

        TryShowSubtitle(subtitleProximityStinger, proximityStinger);
    }

    /// <summary>由放置检测（触发区等）在投递成功时调用。</summary>
    public void NotifyDeliveryComplete()
    {
        if (_outroStarted)
            return;
        _outroStarted = true;
        StartCoroutine(PlayOutroAndLoad());
    }

    IEnumerator PlayOutroAndLoad()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
        if (voiceSource != null)
        {
            yield return PlayClipBlocking(voiceLine1);
            yield return PlayClipBlocking(voiceLine2);
            yield return PlayClipBlockingWithSubtitle(voiceLine4, subtitleVoiceLine4);
            yield return PlayClipBlockingWithSubtitle(voiceLine5, subtitleVoiceLine5);
            yield return PlayClipBlockingWithSubtitle(voiceLine6, subtitleVoiceLine6);
        }

        // 语音播完后：暂停游戏并显示成功弹窗
        PauseGame();
        if (BackupDialogEvents.Instance != null)
            BackupDialogEvents.Instance.ShowSuccessDialog();
    }

    IEnumerator PlayClipBlocking(AudioClip clip)
    {
        if (clip == null || voiceSource == null)
            yield break;
        voiceSource.PlayOneShot(clip);
        yield return new WaitForSeconds(clip.length);
    }

    IEnumerator PlayClipBlockingWithSubtitle(AudioClip clip, string subtitleText)
    {
        if (clip == null || voiceSource == null)
            yield break;
        voiceSource.PlayOneShot(clip);
        if (subtitleController != null && !string.IsNullOrEmpty(subtitleText))
            subtitleController.ShowSubtitleByDuration(subtitleText, clip.length, true);
        yield return new WaitForSeconds(clip.length);
    }

    /// <summary>分三段显示 voiceLine3 的字幕，连续打字，每段时长与音频总长挂钩（均分）。</summary>
    IEnumerator PlayClipBlockingWithTripleSubtitle(
        AudioClip clip,
        string part1, string part2, string part3)
    {
        if (clip == null || voiceSource == null)
            yield break;
        voiceSource.PlayOneShot(clip);

        float totalDuration = clip.length;
        float charInterval = subtitleController.SubtitleCharacterInterval;

        // 按字符数均分音频时长，最小每字符 0.03s
        int part1Len = part1?.Length ?? 0;
        int part2Len = part2?.Length ?? 0;
        int part3Len = part3?.Length ?? 0;
        int totalLen = part1Len + part2Len + part3Len;

        float part1Dur, part2Dur, part3Dur;
        if (totalLen == 0)
        {
            // 没有字幕时直接等音频播完
            yield return new WaitForSeconds(totalDuration);
            subtitleController.HideSubtitle(true);
            yield break;
        }

        // 每字符时长 = 音频总长 / 总字符数，但最小 0.03s
        float perCharDur = Mathf.Max(0.03f, totalDuration / totalLen);
        part1Dur = part1Len * perCharDur;
        part2Dur = part2Len * perCharDur;
        part3Dur = part3Len * perCharDur;

        // 第1段：打字，打完清空，不自动淡出
        if (!string.IsNullOrEmpty(part1))
        {
            subtitleController.ShowSubtitle(part1, 0f, null, false);
            yield return new WaitForSeconds(part1Dur);
            subtitleController.HideSubtitle(false);
        }

        // 第2段：打第2段，打完清空，不自动淡出
        if (!string.IsNullOrEmpty(part2))
        {
            subtitleController.ShowSubtitle(part2, 0f, null, false);
            yield return new WaitForSeconds(part2Dur);
            subtitleController.HideSubtitle(false);
        }

        // 第3段：打完字后持续显示到音频播完，不自动淡出
        if (!string.IsNullOrEmpty(part3))
        {
            subtitleController.ShowSubtitle(part3, 0f, null, false);
            yield return new WaitForSeconds(part3Dur);
        }

        // 等待音频剩余时长（确保字幕与音频同步）
        float used = part1Dur + part2Dur + part3Dur;
        float remaining = totalDuration - used;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // 音频播完后统一淡出
        subtitleController.HideSubtitle(true);
    }

    void TryShowSubtitle(string text, AudioClip clip)
    {
        if (subtitleController == null || string.IsNullOrEmpty(text))
            return;
        float duration = subtitleDurationOverride > 0f ? subtitleDurationOverride : (clip != null ? clip.length : 3f);
        subtitleController.ShowSubtitle(text, duration, clip);
    }

    void TryShowSubtitle(string text, float duration)
    {
        if (subtitleController == null || string.IsNullOrEmpty(text))
            return;
        float effectiveDuration = subtitleDurationOverride > 0f ? subtitleDurationOverride : duration;
        subtitleController.ShowSubtitle(text, effectiveDuration, null);
    }


    void OnPackageReleased(Rigidbody packageRb)
    {
        if (packageRb == null) return;
        float releaseY = packageRb.position.y;
        Debug.Log($"[PlaneGameNarrativeDirector] OnPackageReleased: 释放Y={releaseY:F2}m，阈值={brokenFallThreshold}m，_brokenDetected={_brokenDetected}，_outroStarted={_outroStarted}");
        if (_brokenDetected || _outroStarted) return;
        StartCoroutine(TrackPackageLanding(packageRb, releaseY, brokenFallThreshold));
    }

    IEnumerator TrackPackageLanding(Rigidbody packageRb, float releaseY, float fallThreshold)
    {
        bool wasFalling = false;
        float lastLogTime = Time.time;
        Debug.Log($"[PlaneGameNarrativeDirector] TrackPackageLanding 启动！释放Y={releaseY:F2}m，阈值={fallThreshold}m，当前Y={packageRb.position.y:F2}m");
        while (packageRb != null && !_brokenDetected && !_outroStarted)
        {
            yield return new WaitForFixedUpdate();

            if (Time.time - lastLogTime >= 0.2f)
            {
                lastLogTime = Time.time;
                float curY = packageRb.position.y;
                float yDrop = releaseY - curY;
                Debug.Log($"[PlaneGameNarrativeDirector] 监测中：当前Y={curY:F2}m，速度Y={packageRb.velocity.y:F2}m/s，落差={yDrop:F2}m");
            }

            float vy = packageRb.velocity.y;
            if (vy < -0.5f)
                wasFalling = true;
            else if (wasFalling && Mathf.Abs(vy) < 0.3f)
            {
                float curY = packageRb.position.y;
                float yDrop = releaseY - curY;
                Debug.Log($"[PlaneGameNarrativeDirector] 落地判定！释放Y={releaseY:F2}m，落地Y={curY:F2}m，落差={yDrop:F2}m，阈值={fallThreshold}m");
                if (yDrop >= fallThreshold)
                {
                    _brokenDetected = true;
                    PauseGame();
                    if (BackupDialogEvents.Instance != null)
                        BackupDialogEvents.Instance.ShowBrokenDialog();
                }
                yield break;
            }
        }
        Debug.Log($"[PlaneGameNarrativeDirector] TrackPackageLanding 退出！packageRb={packageRb}，_brokenDetected={_brokenDetected}，_outroStarted={_outroStarted}");
    }
}
