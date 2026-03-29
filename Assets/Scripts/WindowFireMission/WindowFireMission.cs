using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 窗口区第一段旁白；烟区第二段旁白与交互；Zone2 仅控制烟环境音；UI 在对应旁白播完后出现；第二次 F 播结束视频（加载/结束转场图），ESC 关闭；
/// 若配置 Additive 小游戏：结束视频后可选先播一段音频再全屏转场图，最后进入小游戏场景；
/// 结束视频流程正常播完后可启用预先禁用的场景模型。
/// </summary>
public class WindowFireMission : MonoBehaviour
{
    enum Phase
    {
        Idle,
        AwaitSprinklerF,
        SprinklerSequenceRunning,
        AwaitThermalF,
        Done
    }

    [Header("UI")]
    [SerializeField] WindowFireDualPromptHud hud;

    [Header("VFX (window)")]
    [Tooltip("第一次按 F 后在此时间内用 emission 倍率渐灭，最后再彻底 Stop")]
    [SerializeField] float fireFadeDuration = 3f;
    [Tooltip("火焰完全熄灭后再等待此时长，关闭无人机水流与水声")]
    [SerializeField] float waterStopDelayAfterFireOut = 0.5f;
    [SerializeField] List<ParticleSystem> fireEffects = new List<ParticleSystem>();
    [Tooltip("烟粒子；流程不会自动关闭；可配循环环境音")]
    [SerializeField] List<ParticleSystem> smokeEffects = new List<ParticleSystem>();

    [Header("VFX (drone)")]
    [Tooltip("挂在无人机上；开局强制不喷，仅第一次按 F 后播放，结束后关闭")]
    [FormerlySerializedAs("waterSpray")]
    [SerializeField] ParticleSystem droneWaterSpray;

    [Header("Audio — 旁白（分段）")]
    [SerializeField] AudioSource voiceSource;
    [Tooltip("靠近窗口区域时播放，全流程仅一次")]
    [FormerlySerializedAs("narrationOnApproach")]
    [SerializeField] AudioClip narrationNearWindow;
    [Tooltip("靠近烟交互区（SmokeApproach）时播放，全流程仅一次；对应旁白结束后才出第一段交互 UI")]
    [SerializeField] AudioClip narrationNearSmoke;
    [Tooltip("灭火关水后播放，全流程仅一次；对应旁白结束后才出第二段交互 UI")]
    [FormerlySerializedAs("narrationAfterExtinguish")]
    [SerializeField] AudioClip narrationAfterFireOut;

    [Header("Audio — 烟 (循环环境音，3D)")]
    [SerializeField] AudioClip smokeAmbienceLoop;
    [Tooltip("仅当无人机在 Zone2（Kind = Zone2SmokeAmbience）内时播放，离开 Zone2 即停；与烟交互区范围可不同")]
    [SerializeField] Transform smokeSoundOrigin;
    [SerializeField] float smokeAudioMinDistance = 2f;
    [SerializeField] float smokeAudioMaxDistance = 40f;
    [SerializeField] AudioRolloffMode smokeAudioRolloff = AudioRolloffMode.Linear;
    [SerializeField, Range(0f, 180f)] float smokeAmbienceSpread = 90f;
    [SerializeField] AudioSource smokeAudioSource;

    [Header("Audio — 喷水 (循环，仅交互后)")]
    [SerializeField] AudioClip waterSprayLoop;
    [SerializeField] AudioSource waterAudioSource;

    [Header("结束：视频 + 转场图（第二次按 F）")]
    [Tooltip("第二次按 F 后播放；视频期间冻结场景；第二段转场淡出后回到游戏。按 ESC 可随时中断并恢复")]
    [SerializeField] VideoClip endSequenceVideo;
    [Tooltip("视频 Prepare/加载阶段全屏显示（淡入淡出）")]
    [SerializeField] Sprite videoPrepareTransitionSprite;
    [Tooltip("视频结束后全屏显示再淡出，随后关闭 UI 回到原场景")]
    [SerializeField] Sprite videoFinishedTransitionSprite;
    [Tooltip("转场图淡入时长（秒，不受 timeScale 影响）")]
    [SerializeField] float transitionFadeInSeconds = 0.6f;
    [Tooltip("转场图 / 视频层淡出时长（秒）")]
    [SerializeField] float transitionFadeOutSeconds = 0.6f;
    [Tooltip("加载转场图在 Prepare 完成后最少再显示时长（秒）")]
    [SerializeField] float minPrepareTransitionSeconds = 0.35f;
    [Tooltip("Prepare 超时（秒），避免卡死")]
    [SerializeField] float videoPrepareTimeoutSeconds = 15f;
    [SerializeField] int endScreenCanvasSortOrder = 400;

    [Header("结束后 — 场景内模型")]
    [Tooltip("结束视频与两段转场全部播完、回到场景后 SetActive(true)。可先禁用；数量按需要填写（常见为两个）。")]
    [SerializeField] List<GameObject> activateAfterEndVideo = new List<GameObject>();

    [Header("结束后（可选）— Additive 小游戏")]
    [Tooltip("非空时：结束视频与转场全部完成后，Additive 加载该场景；主场景不卸载，返回后机位不变。需加入 Build Settings；场景内用 MiniGameReturnController 返回。")]
    [SerializeField] string postEndSequenceAdditiveMiniGameScene;
    [Tooltip("勾选后主世界与小游戏 timeScale 都会为 0，小游戏内 SmoothDamp/部分 UI 刷新依赖 unscaledDeltaTime 虽仍可用，但易与 HUD/光标表现冲突。建议不勾选：只关主 UI/相机并 Kinematic 冻无人机。")]
    [SerializeField] bool pauseWorldDuringAdditiveMiniGame;
    [Tooltip("进入小游戏前播放（在转场图之前）；不填则跳过")]
    [SerializeField] AudioClip preAdditiveMiniGameClip;
    [Tooltip("播放入门音频用；不填则用 voiceSource")]
    [SerializeField] AudioSource preAdditiveMiniGameAudioSource;
    [Tooltip("音频之后全屏显示该图（淡入→停留→淡出）；不填则跳过")]
    [SerializeField] Sprite preAdditiveMiniGameTransitionSprite;
    [Tooltip("转场图完全显示后的停留时长（秒，不受 timeScale 影响）")]
    [SerializeField] float preAdditiveMiniGameTransitionHoldSeconds = 0.35f;

    Phase _phase = Phase.Idle;
    int _windowZoneCount;
    int _smokeZoneCount;
    int _zone2AmbienceCount;
    bool _loadStarted;
    bool _narrationNearWindowPlayed;
    bool _narrationNearSmokePlayed;
    bool _narrationAfterFirePlayed;
    AudioClip _lastVoiceClip;
    Coroutine _sprinklerRoutine;
    Coroutine _hudSprinklerDelayCo;
    Coroutine _hudThermalDelayCo;
    bool _endFullscreenActive;
    GameObject _endFullscreenRoot;
    Image _endTransitionImage;
    CanvasGroup _transitionCanvasGroup;
    RawImage _endVideoRaw;
    CanvasGroup _videoCanvasGroup;
    VideoPlayer _endVideoPlayer;
    RenderTexture _endVideoRenderTexture;
    AudioSource _endVideoAudioSource;
    Coroutine _endVideoRoutine;
    bool _endVideoLoopReached;
    float _savedTimeScale = 1f;
    bool _gameplayFrozenForEndVideo;
    GameObject _preMiniGameBridgeRoot;
    Image _preMiniGameBridgeImage;
    CanvasGroup _preMiniGameBridgeGroup;

    public static bool IsDroneCollider(Collider other)
    {
        return other.GetComponentInParent<PlaneController>() != null;
    }

    public void NotifyDroneEnteredZone(WindowFireProximityZone.ZoneKind kind)
    {
        if (_loadStarted)
            return;

        switch (kind)
        {
            case WindowFireProximityZone.ZoneKind.WindowApproach:
                _windowZoneCount++;
                if (_windowZoneCount == 1 && !_narrationNearWindowPlayed && narrationNearWindow != null)
                {
                    PlayNarrationClip(narrationNearWindow, allowRepeatSameClip: false);
                    _narrationNearWindowPlayed = true;
                }

                break;

            case WindowFireProximityZone.ZoneKind.SmokeApproach:
                _smokeZoneCount++;
                if (_smokeZoneCount == 1)
                {
                    // 离开烟区再进入时 count 也会从 0→1，只有「任务上第一次进烟」才是 Idle
                    if (_phase == Phase.Idle)
                        OnSmokeZoneFirstOccupantEntered();
                    else
                        OnSmokeZoneReentered();
                }
                else
                    OnSmokeZoneReentered();
                break;

            case WindowFireProximityZone.ZoneKind.Zone2SmokeAmbience:
                _zone2AmbienceCount++;
                if (_zone2AmbienceCount == 1)
                    TryStartSmokeAmbienceLoop();
                break;
        }
    }

    public void NotifyDroneExitedZone(WindowFireProximityZone.ZoneKind kind)
    {
        if (_loadStarted)
            return;

        switch (kind)
        {
            case WindowFireProximityZone.ZoneKind.WindowApproach:
                _windowZoneCount = Mathf.Max(0, _windowZoneCount - 1);
                break;

            case WindowFireProximityZone.ZoneKind.SmokeApproach:
                _smokeZoneCount = Mathf.Max(0, _smokeZoneCount - 1);
                StopHudDelayCoroutines();
                if (_smokeZoneCount == 0 && hud != null)
                    hud.Hide();
                break;

            case WindowFireProximityZone.ZoneKind.Zone2SmokeAmbience:
                _zone2AmbienceCount = Mathf.Max(0, _zone2AmbienceCount - 1);
                if (_zone2AmbienceCount == 0)
                    StopSmokeAmbienceLoop();
                break;
        }
    }

    void OnSmokeZoneFirstOccupantEntered()
    {
        if (!_narrationNearSmokePlayed && narrationNearSmoke != null)
        {
            PlayNarrationClip(narrationNearSmoke, allowRepeatSameClip: false);
            _narrationNearSmokePlayed = true;
        }

        if (_phase == Phase.Idle)
            _phase = Phase.AwaitSprinklerF;

        StopHudDelayCoroutines();
        _hudSprinklerDelayCo = StartCoroutine(CoSprinklerHudAfterNarration());
    }

    void OnSmokeZoneReentered()
    {
        if (hud == null || _smokeZoneCount <= 0)
            return;

        StopHudDelayCoroutines();
        switch (_phase)
        {
            case Phase.AwaitSprinklerF:
                if (CanShowSprinklerPromptNow())
                    hud.ShowSprinklerPrompt();
                else
                    _hudSprinklerDelayCo = StartCoroutine(CoSprinklerHudAfterNarration());
                break;
            case Phase.AwaitThermalF:
                if (CanShowThermalPromptNow())
                    hud.ShowThermalPrompt();
                else
                    _hudThermalDelayCo = StartCoroutine(CoThermalHudAfterNarration());
                break;
            default:
                hud.Hide();
                break;
        }
    }

    bool CanShowSprinklerPromptNow()
    {
        if (narrationNearSmoke == null)
            return true;
        if (voiceSource == null)
            return true;
        return !voiceSource.isPlaying || voiceSource.clip != narrationNearSmoke;
    }

    bool CanShowThermalPromptNow()
    {
        if (narrationAfterFireOut == null)
            return true;
        if (voiceSource == null)
            return true;
        return !voiceSource.isPlaying || voiceSource.clip != narrationAfterFireOut;
    }

    IEnumerator CoSprinklerHudAfterNarration()
    {
        if (narrationNearSmoke != null && voiceSource != null)
            yield return new WaitWhile(() => voiceSource.isPlaying && voiceSource.clip == narrationNearSmoke);

        if (_smokeZoneCount <= 0 || _phase != Phase.AwaitSprinklerF || hud == null)
        {
            _hudSprinklerDelayCo = null;
            yield break;
        }

        hud.ShowSprinklerPrompt();
        _hudSprinklerDelayCo = null;
    }

    IEnumerator CoThermalHudAfterNarration()
    {
        if (narrationAfterFireOut != null && voiceSource != null)
            yield return new WaitWhile(() => voiceSource.isPlaying && voiceSource.clip == narrationAfterFireOut);

        if (_smokeZoneCount <= 0 || _phase != Phase.AwaitThermalF || hud == null)
        {
            _hudThermalDelayCo = null;
            yield break;
        }

        hud.ShowThermalPrompt();
        _hudThermalDelayCo = null;
    }

    void StopHudDelayCoroutines()
    {
        if (_hudSprinklerDelayCo != null)
        {
            StopCoroutine(_hudSprinklerDelayCo);
            _hudSprinklerDelayCo = null;
        }

        if (_hudThermalDelayCo != null)
        {
            StopCoroutine(_hudThermalDelayCo);
            _hudThermalDelayCo = null;
        }
    }

    void Awake()
    {
        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();
        if (voiceSource == null)
            voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;

        if (waterSprayLoop != null && waterAudioSource == null)
            waterAudioSource = gameObject.AddComponent<AudioSource>();

        if (waterAudioSource != null)
        {
            waterAudioSource.playOnAwake = false;
            waterAudioSource.loop = true;
            waterAudioSource.spatialBlend = 0f;
        }
    }

    void Start()
    {
        if (hud == null)
            hud = FindObjectOfType<WindowFireDualPromptHud>();

        var zones = GetComponentsInChildren<WindowFireProximityZone>(true);
        if (zones.Length < 2)
        {
            Debug.LogWarning(
                $"{nameof(WindowFireMission)}: 至少需要窗口区 + 烟交互区两个 {nameof(WindowFireProximityZone)}。烟环境音需再挂一个 Kind = Zone2SmokeAmbience 的触发区。",
                this);
        }

        SetupSmoke3DAudio();
        StopDroneWaterImmediate();
    }

    void OnDisable()
    {
        StopHudDelayCoroutines();
        if (smokeAudioSource != null)
            smokeAudioSource.Stop();
        if (_sprinklerRoutine != null)
        {
            StopCoroutine(_sprinklerRoutine);
            _sprinklerRoutine = null;
        }

        TeardownEndSequence();
        TeardownPreMiniGameBridgeCanvas();
    }

    void Update()
    {
        if (_endFullscreenActive && Input.GetKeyDown(KeyCode.Escape))
        {
            TeardownEndSequence();
            return;
        }

        if (_smokeZoneCount <= 0 || _loadStarted)
            return;

        if (!Input.GetKeyDown(KeyCode.F))
            return;

        if (_phase == Phase.AwaitSprinklerF)
            OnSprinklerConfirmed();
        else if (_phase == Phase.AwaitThermalF)
            OnThermalConfirmed();
    }

    void SetupSmoke3DAudio()
    {
        if (smokeAmbienceLoop == null)
            return;

        Transform origin = smokeSoundOrigin;
        if (origin == null && smokeEffects != null)
        {
            foreach (var ps in smokeEffects)
            {
                if (ps != null)
                {
                    origin = ps.transform;
                    break;
                }
            }
        }

        AudioSource src = smokeAudioSource;
        if (origin != null)
        {
            if (src == null || src.gameObject != origin.gameObject)
            {
                src = origin.GetComponent<AudioSource>();
                if (src == null)
                    src = origin.gameObject.AddComponent<AudioSource>();
            }

            src.spatialBlend = 1f;
            src.minDistance = Mathf.Max(0.01f, smokeAudioMinDistance);
            src.maxDistance = Mathf.Max(src.minDistance + 0.01f, smokeAudioMaxDistance);
            src.rolloffMode = smokeAudioRolloff;
            src.dopplerLevel = 0f;
            src.spread = smokeAmbienceSpread;
        }
        else
        {
            if (src == null)
                src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            Debug.LogWarning(
                $"{nameof(WindowFireMission)}: 未设置 {nameof(smokeSoundOrigin)} 且 {nameof(smokeEffects)} 为空，烟环境音退化为 2D。",
                this);
        }

        smokeAudioSource = src;
        src.loop = true;
        src.playOnAwake = false;
        src.clip = smokeAmbienceLoop;
    }

    void TryStartSmokeAmbienceLoop()
    {
        if (smokeAmbienceLoop == null || smokeAudioSource == null)
            return;
        smokeAudioSource.clip = smokeAmbienceLoop;
        smokeAudioSource.loop = true;
        if (!smokeAudioSource.isPlaying)
            smokeAudioSource.Play();
    }

    void StopSmokeAmbienceLoop()
    {
        if (smokeAudioSource != null)
            smokeAudioSource.Stop();
    }

    void OnSprinklerConfirmed()
    {
        if (_sprinklerRoutine != null)
            return;
        _sprinklerRoutine = StartCoroutine(CoSprinklerSequence());
    }

    IEnumerator CoSprinklerSequence()
    {
        _phase = Phase.SprinklerSequenceRunning;
        StopHudDelayCoroutines();
        if (hud != null)
            hud.Hide();

        if (droneWaterSpray != null)
        {
            var main = droneWaterSpray.main;
            main.playOnAwake = false;
            droneWaterSpray.gameObject.SetActive(true);
            droneWaterSpray.Play(true);
        }

        if (waterAudioSource != null && waterSprayLoop != null)
        {
            waterAudioSource.clip = waterSprayLoop;
            waterAudioSource.loop = true;
            waterAudioSource.Play();
        }

        float dur = Mathf.Max(0.05f, fireFadeDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / dur);
            ApplyFireEmissionMultiplier(k);
            yield return null;
        }

        SuppressEffects(fireEffects);
        ResetFireEmissionMultipliers();

        yield return new WaitForSeconds(Mathf.Max(0f, waterStopDelayAfterFireOut));

        if (waterAudioSource != null)
            waterAudioSource.Stop();

        if (droneWaterSpray != null)
        {
            droneWaterSpray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            droneWaterSpray.gameObject.SetActive(false);
        }

        if (!_narrationAfterFirePlayed && narrationAfterFireOut != null)
        {
            PlayNarrationClip(narrationAfterFireOut, allowRepeatSameClip: false);
            _narrationAfterFirePlayed = true;
        }

        _phase = Phase.AwaitThermalF;
        StopHudDelayCoroutines();
        _hudThermalDelayCo = StartCoroutine(CoThermalHudAfterNarration());
        _sprinklerRoutine = null;
    }

    void ApplyFireEmissionMultiplier(float multiplier)
    {
        if (fireEffects == null)
            return;
        foreach (var ps in fireEffects)
        {
            if (ps == null)
                continue;
            var em = ps.emission;
            em.rateOverTimeMultiplier = multiplier;
        }
    }

    void ResetFireEmissionMultipliers()
    {
        if (fireEffects == null)
            return;
        foreach (var ps in fireEffects)
        {
            if (ps == null)
                continue;
            var em = ps.emission;
            em.rateOverTimeMultiplier = 1f;
        }
    }

    void StopDroneWaterImmediate()
    {
        if (droneWaterSpray == null)
            return;
        var main = droneWaterSpray.main;
        main.playOnAwake = false;
        droneWaterSpray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        droneWaterSpray.gameObject.SetActive(false);
    }

    void OnThermalConfirmed()
    {
        if (endSequenceVideo == null)
        {
            Debug.LogWarning($"{nameof(WindowFireMission)}: 未设置 {nameof(endSequenceVideo)}，无法播放结束视频。", this);
            return;
        }

        if (videoPrepareTransitionSprite == null || videoFinishedTransitionSprite == null)
        {
            Debug.LogWarning(
                $"{nameof(WindowFireMission)}: 请设置 {nameof(videoPrepareTransitionSprite)} 与 {nameof(videoFinishedTransitionSprite)}。",
                this);
            return;
        }

        _loadStarted = true;
        _phase = Phase.Done;
        StopHudDelayCoroutines();
        if (hud != null)
            hud.Hide();

        if (_endVideoRoutine != null)
            StopCoroutine(_endVideoRoutine);
        _endVideoRoutine = StartCoroutine(CoEndVideoWithTransitions());
    }

    IEnumerator CoEndVideoWithTransitions()
    {
        TeardownEndSequence();
        BuildEndSequenceCanvas();
        _endFullscreenActive = true;
        RestoreGameplayTimeScale();

        _endTransitionImage.sprite = videoPrepareTransitionSprite;
        _transitionCanvasGroup.alpha = 0f;
        _videoCanvasGroup.alpha = 0f;
        _endVideoRaw.gameObject.SetActive(true);

        _endVideoPlayer.clip = endSequenceVideo;
        _endVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.GameTime;
        _endVideoPlayer.Prepare();

        yield return FadeCanvasGroup(_transitionCanvasGroup, 0f, 1f, transitionFadeInSeconds);

        float waited = 0f;
        while (!_endVideoPlayer.isPrepared && waited < videoPrepareTimeoutSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_endVideoPlayer.isPrepared)
        {
            Debug.LogError($"{nameof(WindowFireMission)}: 视频 Prepare 失败或超时。", this);
            yield return FadeCanvasGroup(_transitionCanvasGroup, _transitionCanvasGroup.alpha, 0f, transitionFadeOutSeconds);
            TeardownEndSequence();
            _endVideoRoutine = null;
            yield break;
        }

        if (minPrepareTransitionSeconds > 0f)
            yield return new WaitForSecondsRealtime(minPrepareTransitionSeconds);

        yield return FadeCanvasGroup(_transitionCanvasGroup, _transitionCanvasGroup.alpha, 0f, transitionFadeOutSeconds);

        FreezeGameplayForEndVideo();
        yield return FadeCanvasGroup(_videoCanvasGroup, 0f, 1f, transitionFadeInSeconds);
        _endVideoLoopReached = false;
        _endVideoPlayer.loopPointReached += OnEndVideoLoopPointReached;
        _endVideoPlayer.Play();

        yield return new WaitUntil(() => _endVideoLoopReached);

        _endVideoPlayer.loopPointReached -= OnEndVideoLoopPointReached;
        _endVideoPlayer.Stop();

        // 全程保持冻结，直到第二段转场淡出完毕并在 Teardown 里恢复 timeScale
        yield return FadeCanvasGroup(_videoCanvasGroup, _videoCanvasGroup.alpha, 0f, transitionFadeOutSeconds);

        _endTransitionImage.sprite = videoFinishedTransitionSprite;
        yield return FadeCanvasGroup(_transitionCanvasGroup, 0f, 1f, transitionFadeInSeconds);

        yield return FadeCanvasGroup(_transitionCanvasGroup, _transitionCanvasGroup.alpha, 0f, transitionFadeOutSeconds);

        // 必须先清引用：TeardownEndSequence 会 StopCoroutine(_endVideoRoutine)，否则会停掉当前协程，后续音频/转场/小游戏不会执行
        _endVideoRoutine = null;
        TeardownEndSequence();
        ActivateModelsAfterEndVideo();

        if (!string.IsNullOrWhiteSpace(postEndSequenceAdditiveMiniGameScene))
            yield return CoPreMiniGameBridgeThenAdditive();
    }

    IEnumerator CoPreMiniGameBridgeThenAdditive()
    {
        AudioSource src = preAdditiveMiniGameAudioSource != null ? preAdditiveMiniGameAudioSource : voiceSource;
        if (preAdditiveMiniGameClip != null)
        {
            if (src == null)
            {
                Debug.LogWarning(
                    $"{nameof(WindowFireMission)}: 已配置 {nameof(preAdditiveMiniGameClip)} 但无可用 AudioSource（可指定 {nameof(preAdditiveMiniGameAudioSource)} 或 {nameof(voiceSource)}）。",
                    this);
            }
            else
            {
                src.Stop();
                src.clip = preAdditiveMiniGameClip;
                src.Play();
                yield return new WaitWhile(() => src.isPlaying);
            }
        }

        if (preAdditiveMiniGameTransitionSprite != null)
        {
            BuildPreMiniGameBridgeCanvas();
            _preMiniGameBridgeImage.sprite = preAdditiveMiniGameTransitionSprite;
            _preMiniGameBridgeGroup.alpha = 0f;
            yield return FadeCanvasGroup(_preMiniGameBridgeGroup, 0f, 1f, transitionFadeInSeconds);
            if (preAdditiveMiniGameTransitionHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(preAdditiveMiniGameTransitionHoldSeconds);
            yield return FadeCanvasGroup(_preMiniGameBridgeGroup, _preMiniGameBridgeGroup.alpha, 0f, transitionFadeOutSeconds);
            TeardownPreMiniGameBridgeCanvas();
        }

        MiniGameAdditiveFlow.Begin(
            postEndSequenceAdditiveMiniGameScene.Trim(),
            pauseWorldDuringAdditiveMiniGame);
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float fromAlpha, float toAlpha, float duration)
    {
        if (cg == null)
            yield break;

        cg.alpha = fromAlpha;
        bool visible = toAlpha > 0.01f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;

        if (duration <= 0f)
        {
            cg.alpha = toAlpha;
            visible = toAlpha > 0.01f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(t / duration));
            yield return null;
        }

        cg.alpha = toAlpha;
        visible = toAlpha > 0.01f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }

    void FreezeGameplayForEndVideo()
    {
        if (_gameplayFrozenForEndVideo)
            return;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        _gameplayFrozenForEndVideo = true;
        if (_endVideoPlayer != null)
            _endVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
    }

    void RestoreGameplayTimeScale()
    {
        if (!_gameplayFrozenForEndVideo)
            return;
        Time.timeScale = _savedTimeScale;
        _gameplayFrozenForEndVideo = false;
        if (_endVideoPlayer != null)
            _endVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.GameTime;
    }

    void OnEndVideoLoopPointReached(VideoPlayer source)
    {
        _endVideoLoopReached = true;
    }

    static void StretchChildFullScreen(Transform parent, GameObject go)
    {
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    void BuildEndSequenceCanvas()
    {
        if (_endFullscreenRoot != null)
            return;

        var canvasGo = new GameObject("MissionEndVideoCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = endScreenCanvasSortOrder;
        canvasGo.AddComponent<GraphicRaycaster>();
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 转场与视频可同时为 alpha=0；底层黑场避免透视到游戏场景闪一帧
        var backdropGo = new GameObject("EndSequenceBackdrop", typeof(RectTransform), typeof(Image));
        StretchChildFullScreen(canvasGo.transform, backdropGo);
        var backdropImage = backdropGo.GetComponent<Image>();
        var white = Texture2D.whiteTexture;
        backdropImage.sprite = Sprite.Create(
            white,
            new Rect(0f, 0f, white.width, white.height),
            new Vector2(0.5f, 0.5f),
            100f);
        backdropImage.color = Color.black;
        backdropImage.raycastTarget = false;

        var transitionGo = new GameObject("TransitionImage", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        StretchChildFullScreen(canvasGo.transform, transitionGo);
        _endTransitionImage = transitionGo.GetComponent<Image>();
        _endTransitionImage.preserveAspect = true;
        _endTransitionImage.raycastTarget = true;
        _transitionCanvasGroup = transitionGo.GetComponent<CanvasGroup>();
        _transitionCanvasGroup.alpha = 0f;
        _transitionCanvasGroup.blocksRaycasts = false;
        _transitionCanvasGroup.interactable = false;

        var rawGo = new GameObject("VideoRawImage", typeof(RectTransform), typeof(RawImage), typeof(CanvasGroup));
        StretchChildFullScreen(canvasGo.transform, rawGo);
        _endVideoRaw = rawGo.GetComponent<RawImage>();
        _endVideoRaw.raycastTarget = true;
        _endVideoRaw.texture = null;
        _videoCanvasGroup = rawGo.GetComponent<CanvasGroup>();
        _videoCanvasGroup.alpha = 0f;
        _videoCanvasGroup.blocksRaycasts = false;
        _videoCanvasGroup.interactable = false;

        var audioHost = new GameObject("VideoAudio");
        audioHost.transform.SetParent(canvasGo.transform, false);
        _endVideoAudioSource = audioHost.AddComponent<AudioSource>();
        _endVideoAudioSource.playOnAwake = false;
        _endVideoAudioSource.spatialBlend = 0f;

        var vpHost = new GameObject("VideoPlayer");
        vpHost.transform.SetParent(canvasGo.transform, false);
        _endVideoPlayer = vpHost.AddComponent<VideoPlayer>();
        _endVideoPlayer.playOnAwake = false;
        _endVideoPlayer.isLooping = false;
        _endVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _endVideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        _endVideoPlayer.EnableAudioTrack(0, true);
        _endVideoPlayer.SetTargetAudioSource(0, _endVideoAudioSource);

        int w = (int)endSequenceVideo.width;
        int h = (int)endSequenceVideo.height;
        if (w <= 16 || h <= 16)
        {
            w = 1920;
            h = 1080;
        }

        _endVideoRenderTexture = new RenderTexture(w, h, 0);
        _endVideoPlayer.targetTexture = _endVideoRenderTexture;
        _endVideoRaw.texture = _endVideoRenderTexture;

        _endFullscreenRoot = canvasGo;
    }

    void BuildPreMiniGameBridgeCanvas()
    {
        if (_preMiniGameBridgeRoot != null)
            return;

        var canvasGo = new GameObject("PreMiniGameBridgeCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = endScreenCanvasSortOrder + 50;
        canvasGo.AddComponent<GraphicRaycaster>();
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var backdropGo = new GameObject("BridgeBackdrop", typeof(RectTransform), typeof(Image));
        StretchChildFullScreen(canvasGo.transform, backdropGo);
        var backdropImage = backdropGo.GetComponent<Image>();
        var white = Texture2D.whiteTexture;
        backdropImage.sprite = Sprite.Create(
            white,
            new Rect(0f, 0f, white.width, white.height),
            new Vector2(0.5f, 0.5f),
            100f);
        backdropImage.color = Color.black;
        backdropImage.raycastTarget = false;

        var imgGo = new GameObject("BridgeTransition", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        StretchChildFullScreen(canvasGo.transform, imgGo);
        _preMiniGameBridgeImage = imgGo.GetComponent<Image>();
        _preMiniGameBridgeImage.preserveAspect = true;
        _preMiniGameBridgeImage.raycastTarget = false;
        _preMiniGameBridgeGroup = imgGo.GetComponent<CanvasGroup>();
        _preMiniGameBridgeGroup.alpha = 0f;
        _preMiniGameBridgeGroup.blocksRaycasts = false;
        _preMiniGameBridgeGroup.interactable = false;

        _preMiniGameBridgeRoot = canvasGo;
    }

    void TeardownPreMiniGameBridgeCanvas()
    {
        if (_preMiniGameBridgeRoot != null)
            Destroy(_preMiniGameBridgeRoot);

        _preMiniGameBridgeRoot = null;
        _preMiniGameBridgeImage = null;
        _preMiniGameBridgeGroup = null;
    }

    void TeardownEndSequence()
    {
        RestoreGameplayTimeScale();

        if (_endVideoRoutine != null)
        {
            StopCoroutine(_endVideoRoutine);
            _endVideoRoutine = null;
        }

        if (_endVideoPlayer != null)
        {
            _endVideoPlayer.loopPointReached -= OnEndVideoLoopPointReached;
            _endVideoPlayer.Stop();
            _endVideoPlayer.targetTexture = null;
            _endVideoPlayer.clip = null;
        }

        if (_endVideoRenderTexture != null)
        {
            _endVideoRenderTexture.Release();
            Destroy(_endVideoRenderTexture);
            _endVideoRenderTexture = null;
        }

        if (_endFullscreenRoot != null)
            Destroy(_endFullscreenRoot);

        _endFullscreenRoot = null;
        _endTransitionImage = null;
        _transitionCanvasGroup = null;
        _endVideoRaw = null;
        _videoCanvasGroup = null;
        _endVideoPlayer = null;
        _endVideoAudioSource = null;
        _endFullscreenActive = false;
        _endVideoLoopReached = false;
    }

    static void SuppressEffects(List<ParticleSystem> systems)
    {
        if (systems == null)
            return;
        foreach (var ps in systems)
        {
            if (ps == null)
                continue;
            var em = ps.emission;
            em.enabled = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void ActivateModelsAfterEndVideo()
    {
        if (activateAfterEndVideo == null)
            return;
        foreach (var go in activateAfterEndVideo)
        {
            if (go != null)
                go.SetActive(true);
        }
    }

    void PlayNarrationClip(AudioClip clip, bool allowRepeatSameClip)
    {
        if (clip == null || voiceSource == null)
            return;
        if (!allowRepeatSameClip && voiceSource.isPlaying && _lastVoiceClip == clip)
            return;
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();
        _lastVoiceClip = clip;
    }
}
