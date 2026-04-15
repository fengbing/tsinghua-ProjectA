using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 烟区旁白；Zone2 控制烟环境音；火点触发区负责「按 F 开水」提示；
/// 开水后对每处火累积灭火进度（按到火点距离衰减，可选喷嘴朝向）；火焰粒子强度随之变弱；无效喷射时进度缓慢回退；
/// 全部熄灭后关水与音效，播收尾旁白，再播系统对话框（黑底 + 逐字 + 第二段音频）。
/// </summary>
public class WindowFireMission : MonoBehaviour
{
    /// <summary>
    /// Triggered when all fires are extinguished and the post-fire narration clip (if configured) has finished.
    /// </summary>
    public event Action OnFireExtinguishAndNarrationFinished;
    /// <summary>
    /// Triggered immediately when player presses F and sprinkler/fire-extinguish sequence starts.
    /// </summary>
    public event Action OnFireExtinguishStarted;

    enum Phase
    {
        Idle = 0,
        AwaitSprinklerF = 1,
        SprinklerSequenceRunning = 2,
        Done = 3,
        /// <summary>灭火与两段前置语音已结束，等待无人机进入立面小游戏触发区。</summary>
        AwaitFacadeMinigameTrigger = 4,
    }

    [Header("UI")]
    [SerializeField] WindowFireDualPromptHud hud;
    [Tooltip("留空则在场景里查找；灭火收尾旁白结束后用于系统提示（黑底 + 逐字 + 音频）。")]
    [SerializeField] SystemDialogController2 systemDialog;

    [Header("VFX (fires)")]
    [Tooltip("与 WindowFireExtinguishZone 的 fireIndex 一一对应。")]
    [SerializeField] List<ParticleSystem> fireEffects = new List<ParticleSystem>();

    [Tooltip("在「浇水效果=1」的理想情况下，单点火从全旺到完全熄灭所需时间（秒）。")]
    [FormerlySerializedAs("extinguishDwellSeconds")]
    [SerializeField] float extinguishSecondsAtFullEffect = 3.5f;

    [Tooltip("浇水效果不足时，灭火进度每秒回退量（0~1）；模拟离开有效喷射后火势回升。")]
    [SerializeField] float extinguishProgressDecayPerSecond = 0.18f;

    [Tooltip("超过该距离认为水打不到该火点，该火浇水强度为 0。")]
    [SerializeField] float maxSprayToFireDistance = 12f;

    [Tooltip("小于等于该距离时距离因子为 1；更远则线性减弱至最大距离处为 0。")]
    [SerializeField] float optimalSprayToFireDistance = 2.5f;

    [Tooltip("开启时：水粒子 forward 与指向火点方向越一致，该火浇水效果越好；关闭时朝向因子恒为 1。")]
    [SerializeField] bool factorNozzleAlignmentToFire = false;

    [Tooltip("喷射方向与指向火点方向点积低于该值时朝向因子为 0。")]
    [SerializeField] float minAlignmentDotForCooling = 0.2f;

    [Tooltip("烟粒子；流程不会自动关闭；可配循环环境音")]
    [SerializeField] List<ParticleSystem> smokeEffects = new List<ParticleSystem>();

    [Header("VFX (drone)")]
    [Tooltip("挂在无人机上；开局强制不喷，按 F 后播放，全部火灭后关闭")]
    [FormerlySerializedAs("waterSpray")]
    [SerializeField] ParticleSystem droneWaterSpray;

    [Header("Audio — 旁白（分段）")]
    [SerializeField] AudioSource voiceSource;
    [Tooltip("靠近烟交互区（SmokeApproach）时播放，全流程仅一次")]
    [SerializeField] AudioClip narrationNearSmoke;
    [Tooltip("全部火点熄灭且水关闭后播放一次；不填则无")]
    [SerializeField] AudioClip narrationAfterFireOut;

    [Header("收尾 — 系统提示（在上一段旁白播完后）")]
    [Tooltip("系统对话框逐字显示的正文；可与下一条语音同步")]
    [TextArea(2, 6)]
    [SerializeField] string postMissionSystemPromptText;
    [Tooltip("系统提示句的语音（由 SystemDialogController2 播放）")]
    [SerializeField] AudioClip postMissionSystemPromptClip;
    [Tooltip("≤0 时用对话框默认间隔（约 0.04s/字）")]
    [SerializeField] float postMissionSystemPromptCharInterval;

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
    [SerializeField, Range(0f, 1f)] float waterSprayVolume = 0.75f;

    [Header("收尾")]
    [Tooltip("停水后到播放 narrationAfterFireOut 之间的间隔（秒）")]
    [FormerlySerializedAs("waterStopDelayAfterFireOut")]
    [SerializeField] float missionEndLineDelayAfterWaterStop = 0.5f;

    [Header("立面救援小游戏（可选）")]
    [Tooltip("拖挂 FacadeRescueMiniGameController。非空时：灭火 → narrationAfterFireOut（可空）→ 系统提示（与无小游戏时相同）→ 无人机进触发区后打开小游戏；小游戏结束不再播系统提示。")]
    [SerializeField] Component facadeRescueMinigame;
    [Tooltip("立面小游戏触发器根物体（含 Collider、WindowFireFacadeMinigameTrigger 与子物体特效）。有立面小游戏时开局隐藏，灭火且「灭火后旁白 + 系统提示」都播完后再显示。留空则运行时按场景中物体名 Teleport_7 查找（含未激活）。")]
    [SerializeField] GameObject facadeMinigameTriggerRoot;

    /// <summary>当 Inspector 未指定 facadeMinigameTriggerRoot 时，按名 Teleport_7 解析一次并缓存。</summary>
    GameObject _resolvedFacadeMinigameTriggerByName;

    Phase _phase = Phase.Idle;
    int _smokeZoneCount;
    int _zone2AmbienceCount;
    bool _loadStarted;
    bool _facadeRescueOpened;
    Coroutine _afterFacadeCo;
    bool _narrationNearSmokePlayed;
    bool _narrationAfterFirePlayed;
    AudioClip _lastVoiceClip;
    Coroutine _sprinklerRoutine;
    Coroutine _hudSprinklerDelayCo;

    int _fireCount;
    int[] _fireZoneDepth;
    /// <summary>0 = 未浇灭，1 = 已完全浇灭阈值。</summary>
    float[] _fireExtinguishProgress;
    bool[] _fireAlive;
    float[] _fireBaseEmissionMultiplier;

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
                break;

            case WindowFireProximityZone.ZoneKind.SmokeApproach:
                _smokeZoneCount++;
                if (_smokeZoneCount == 1)
                {
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
                break;

            case WindowFireProximityZone.ZoneKind.SmokeApproach:
                _smokeZoneCount = Mathf.Max(0, _smokeZoneCount - 1);
                StopHudDelayCoroutines();
                if (!PromptZonesOccupied() && hud != null)
                    hud.Hide();
                break;

            case WindowFireProximityZone.ZoneKind.Zone2SmokeAmbience:
                _zone2AmbienceCount = Mathf.Max(0, _zone2AmbienceCount - 1);
                if (_zone2AmbienceCount == 0)
                    StopSmokeAmbienceLoop();
                break;
        }
    }

    /// <summary>火点触发区进入（与 fireEffects 下标对应）。</summary>
    public void NotifyFireExtinguishZoneEntered(int index)
    {
        if (_loadStarted || !IsValidFireIndex(index))
            return;

        _fireZoneDepth[index]++;
        if (_fireZoneDepth[index] != 1)
            return;

        switch (_phase)
        {
            case Phase.Idle:
                _phase = Phase.AwaitSprinklerF;
                StopHudDelayCoroutines();
                _hudSprinklerDelayCo = StartCoroutine(CoSprinklerHudAfterNarration());
                break;
            case Phase.AwaitSprinklerF:
                StopHudDelayCoroutines();
                if (hud != null)
                {
                    if (CanShowSprinklerPromptNow())
                        hud.ShowSprinklerPrompt();
                    else
                        _hudSprinklerDelayCo = StartCoroutine(CoSprinklerHudAfterNarration());
                }

                break;
        }
    }

    /// <summary>火点触发区离开。</summary>
    public void NotifyFireExtinguishZoneExited(int index)
    {
        if (_loadStarted || !IsValidFireIndex(index))
            return;
        if (_fireZoneDepth[index] <= 0)
            return;

        _fireZoneDepth[index]--;

        if (!AnyFireZoneOccupied())
        {
            StopHudDelayCoroutines();
            if (_smokeZoneCount <= 0 && hud != null)
                hud.Hide();
        }
    }

    void OnSmokeZoneFirstOccupantEntered()
    {
        if (!_narrationNearSmokePlayed && narrationNearSmoke != null)
        {
            PlayNarrationClip(narrationNearSmoke, allowRepeatSameClip: false);
            _narrationNearSmokePlayed = true;
        }
    }

    void OnSmokeZoneReentered()
    {
        if (hud == null || _smokeZoneCount <= 0)
            return;

        StopHudDelayCoroutines();
        if (_phase == Phase.AwaitSprinklerF)
        {
            if (CanShowSprinklerPromptNow())
                hud.ShowSprinklerPrompt();
            else
                _hudSprinklerDelayCo = StartCoroutine(CoSprinklerHudAfterNarration());
        }
        else
            hud.Hide();
    }

    bool PromptZonesOccupied()
    {
        return _smokeZoneCount > 0 || AnyFireZoneOccupied();
    }

    bool AnyFireZoneOccupied()
    {
        if (_fireZoneDepth == null)
            return false;
        for (int i = 0; i < _fireZoneDepth.Length; i++)
        {
            if (_fireZoneDepth[i] > 0)
                return true;
        }

        return false;
    }

    bool IsValidFireIndex(int index)
    {
        return index >= 0 && index < _fireCount;
    }

    bool CanShowSprinklerPromptNow()
    {
        if (narrationNearSmoke == null)
            return true;
        if (voiceSource == null)
            return true;
        return !voiceSource.isPlaying || voiceSource.clip != narrationNearSmoke;
    }

    IEnumerator CoSprinklerHudAfterNarration()
    {
        if (narrationNearSmoke != null && voiceSource != null)
            yield return new WaitWhile(() => voiceSource.isPlaying && voiceSource.clip == narrationNearSmoke);

        if (!PromptZonesOccupied() || _phase != Phase.AwaitSprinklerF || hud == null)
        {
            _hudSprinklerDelayCo = null;
            yield break;
        }

        hud.ShowSprinklerPrompt();
        _hudSprinklerDelayCo = null;
    }

    void StopHudDelayCoroutines()
    {
        if (_hudSprinklerDelayCo != null)
        {
            StopCoroutine(_hudSprinklerDelayCo);
            _hudSprinklerDelayCo = null;
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
            waterAudioSource.volume = Mathf.Clamp01(waterSprayVolume);
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
                $"{nameof(WindowFireMission)}: 至少需要烟交互区与一个 Kind = Zone2SmokeAmbience 的触发区（各一个 {nameof(WindowFireProximityZone)}）。窗口区触发器可选。",
                this);
        }

        InitFireRuntimeState();
        SetupSmoke3DAudio();
        StopDroneWaterImmediate();
        ApplyFacadeMinigameTriggerInitialVisibility();
    }

    GameObject GetFacadeMinigameTriggerRootOrResolve()
    {
        if (facadeMinigameTriggerRoot != null)
            return facadeMinigameTriggerRoot;
        if (_resolvedFacadeMinigameTriggerByName != null)
            return _resolvedFacadeMinigameTriggerByName;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || t.gameObject.name != "Teleport_7")
                continue;
            _resolvedFacadeMinigameTriggerByName = t.gameObject;
            break;
        }

        return _resolvedFacadeMinigameTriggerByName;
    }

    void ApplyFacadeMinigameTriggerInitialVisibility()
    {
        if (ResolveFacadeRescueEntry() == null)
            return;
        var root = GetFacadeMinigameTriggerRootOrResolve();
        if (root == null)
            return;
        root.SetActive(false);
    }

    void InitFireRuntimeState()
    {
        _fireCount = fireEffects != null ? fireEffects.Count : 0;
        _fireZoneDepth = new int[_fireCount];
        _fireExtinguishProgress = new float[_fireCount];
        _fireAlive = new bool[_fireCount];
        _fireBaseEmissionMultiplier = new float[_fireCount];
        for (int i = 0; i < _fireCount; i++)
        {
            _fireAlive[i] = fireEffects[i] != null;
            if (fireEffects[i] != null)
                _fireBaseEmissionMultiplier[i] = fireEffects[i].emission.rateOverTimeMultiplier;
            else
                _fireBaseEmissionMultiplier[i] = 1f;
        }
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

        if (_afterFacadeCo != null)
        {
            StopCoroutine(_afterFacadeCo);
            _afterFacadeCo = null;
        }
    }

    /// <summary>由立面小游戏触发器调用；条件满足时打开小游戏且只触发一次。</summary>
    public void TryOpenFacadeRescueFromTrigger()
    {
        var entry = ResolveFacadeRescueEntry();
        if (entry == null)
            return;
        if (_loadStarted)
            return;
        if (_phase != Phase.AwaitFacadeMinigameTrigger)
            return;
        if (_facadeRescueOpened)
            return;
        _facadeRescueOpened = true;
        entry.Open(this);
    }

    IFacadeRescueMinigameEntry ResolveFacadeRescueEntry()
    {
        if (facadeRescueMinigame != null)
        {
            if (facadeRescueMinigame is IFacadeRescueMinigameEntry direct)
                return direct;
            var onSame = facadeRescueMinigame.GetComponent<IFacadeRescueMinigameEntry>();
            if (onSame != null)
                return onSame;
            var child = facadeRescueMinigame.GetComponentInChildren<IFacadeRescueMinigameEntry>(true);
            if (child != null)
                return child;
        }

        var any = FindObjectsByType<FacadeRescueMiniGameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < any.Length; i++)
        {
            if (any[i] != null)
                return any[i];
        }

        return null;
    }

    /// <summary>由立面救援小游戏在流程结束后调用（系统提示已在进小游戏前播过，此处仅收尾关门）。</summary>
    public void NotifyFacadeRescueMinigameComplete()
    {
        if (_afterFacadeCo != null)
            StopCoroutine(_afterFacadeCo);
        _afterFacadeCo = StartCoroutine(CoAfterFacadeMinigame());
    }

    /// <summary>配置错误等导致小游戏未正常结束时，允许再次尝试进入触发区。</summary>
    public void NotifyFacadeRescueMinigameAborted()
    {
        _facadeRescueOpened = false;
    }

    IEnumerator CoAfterFacadeMinigame()
    {
        _loadStarted = true;
        _phase = Phase.Done;
        _afterFacadeCo = null;
        yield break;
    }

    void Update()
    {
        if (_loadStarted)
            return;
        if (!PromptZonesOccupied())
            return;
        if (!Input.GetKeyDown(KeyCode.F))
            return;

        if (_phase == Phase.AwaitSprinklerF)
            OnSprinklerConfirmed();
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
        if (_fireCount == 0 || CountAliveFires() == 0)
        {
            Debug.LogWarning(
                $"{nameof(WindowFireMission)}: 未配置有效 {nameof(fireEffects)}，无法开始灭火流程。",
                this);
            return;
        }

        _sprinklerRoutine = StartCoroutine(CoWaterActiveExtinguishLoop());
        InvokeEventSafely(OnFireExtinguishStarted, nameof(OnFireExtinguishStarted));
    }

    IEnumerator CoWaterActiveExtinguishLoop()
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
            waterAudioSource.volume = Mathf.Clamp01(waterSprayVolume);
            waterAudioSource.Play();
        }

        for (int i = 0; i < _fireCount; i++)
        {
            _fireExtinguishProgress[i] = 0f;
            if (_fireAlive[i] && fireEffects[i] != null)
                ApplyFireRemainingVisual(fireEffects[i], 1f, _fireBaseEmissionMultiplier[i]);
        }

        float fullEffectSeconds = Mathf.Max(0.2f, extinguishSecondsAtFullEffect);
        const float extinguishDoneThreshold = 0.998f;
        while (CountAliveFires() > 0)
        {
            float dt = GlobalGamePause.IsPaused ? Time.unscaledDeltaTime : Time.deltaTime;
            for (int i = 0; i < _fireCount; i++)
            {
                if (!_fireAlive[i])
                    continue;
                var ps = fireEffects[i];
                if (ps == null)
                {
                    _fireAlive[i] = false;
                    continue;
                }

                float w = ComputeCoolingEffectiveness(ps.transform.position);
                float p = _fireExtinguishProgress[i];
                if (w > 0.001f)
                    p += (dt / fullEffectSeconds) * w;
                else
                    p -= extinguishProgressDecayPerSecond * dt;
                p = Mathf.Clamp01(p);
                _fireExtinguishProgress[i] = p;

                float remaining = 1f - p;
                ApplyFireRemainingVisual(ps, remaining, _fireBaseEmissionMultiplier[i]);

                if (p >= extinguishDoneThreshold)
                    ExtinguishFireAtIndex(i);
            }

            yield return null;
        }

        if (waterAudioSource != null)
            waterAudioSource.Stop();

        if (droneWaterSpray != null)
        {
            droneWaterSpray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            droneWaterSpray.gameObject.SetActive(false);
        }

        float tail = Mathf.Max(0f, missionEndLineDelayAfterWaterStop);
        if (tail > 0f)
        {
            if (GlobalGamePause.IsPaused)
                yield return new WaitForSecondsRealtime(tail);
            else
                yield return new WaitForSeconds(tail);
        }

        if (!_narrationAfterFirePlayed && narrationAfterFireOut != null)
        {
            PlayNarrationClip(narrationAfterFireOut, allowRepeatSameClip: false);
            _narrationAfterFirePlayed = true;
            if (voiceSource != null)
                yield return new WaitWhile(() => voiceSource.isPlaying && voiceSource.clip == narrationAfterFireOut);
        }

        InvokeEventSafely(OnFireExtinguishAndNarrationFinished, nameof(OnFireExtinguishAndNarrationFinished));

        if (ResolveFacadeRescueEntry() != null)
        {
            yield return CoPostMissionSystemPrompt();
            var triggerRoot = GetFacadeMinigameTriggerRootOrResolve();
            if (triggerRoot != null)
                triggerRoot.SetActive(true);
            else
            {
                Debug.LogWarning(
                    $"{nameof(WindowFireMission)}: 立面小游戏已启用但未配置 facadeMinigameTriggerRoot，且场景中未找到名为 Teleport_7 的物体。",
                    this);
            }

            _phase = Phase.AwaitFacadeMinigameTrigger;
            _sprinklerRoutine = null;
            yield break;
        }

        yield return CoPostMissionSystemPrompt();

        _loadStarted = true;
        _phase = Phase.Done;
        _sprinklerRoutine = null;
    }

    IEnumerator CoPostMissionSystemPrompt()
    {
        bool hasText = !string.IsNullOrEmpty(postMissionSystemPromptText);
        bool hasVoice = postMissionSystemPromptClip != null;
        if (!hasText && !hasVoice)
            yield break;

        var dlg = systemDialog != null ? systemDialog : FindObjectOfType<SystemDialogController2>();
        if (dlg == null)
        {
            Debug.LogWarning(
                $"{nameof(WindowFireMission)}: 已配置系统提示文案或音频，但场景中无 {nameof(SystemDialogController2)}。",
                this);
            yield break;
        }

        float interval = postMissionSystemPromptCharInterval > 0f ? postMissionSystemPromptCharInterval : 0f;
        dlg.PlaySingleLine(hasText ? postMissionSystemPromptText : string.Empty, postMissionSystemPromptClip, interval);
        yield return dlg.WaitUntilDialogIdle();
    }

    int CountAliveFires()
    {
        int n = 0;
        if (_fireAlive == null)
            return 0;
        for (int i = 0; i < _fireAlive.Length; i++)
        {
            if (_fireAlive[i])
                n++;
        }

        return n;
    }

    void ExtinguishFireAtIndex(int index)
    {
        if (!IsValidFireIndex(index) || !_fireAlive[index])
            return;
        _fireAlive[index] = false;
        _fireExtinguishProgress[index] = 0f;
        var ps = fireEffects[index];
        if (ps != null)
            SuppressSingleFire(ps);
    }

    /// <summary>浇水对某火的相对强度 0~1：距离衰减 × 喷嘴朝向（朝向可关）。</summary>
    float ComputeCoolingEffectiveness(Vector3 fireWorldPos)
    {
        if (droneWaterSpray == null)
            return 0f;

        Transform nozzle = droneWaterSpray.transform;
        Vector3 nozzlePos = nozzle.position;
        float distFactor = ComputeSprayDistanceFactor(nozzlePos, fireWorldPos);
        if (distFactor <= 0.001f)
            return 0f;

        float alignFactor = 1f;
        if (factorNozzleAlignmentToFire)
        {
            Vector3 toFire = fireWorldPos - nozzlePos;
            if (toFire.sqrMagnitude < 1e-6f)
                alignFactor = 1f;
            else
            {
                float dot = Vector3.Dot(nozzle.forward, toFire.normalized);
                alignFactor = Mathf.InverseLerp(minAlignmentDotForCooling, 1f, dot);
                alignFactor = Mathf.Clamp01(alignFactor);
            }
        }

        return Mathf.Clamp01(distFactor * alignFactor);
    }

    float ComputeSprayDistanceFactor(Vector3 nozzlePos, Vector3 fireWorldPos)
    {
        float dist = Vector3.Distance(nozzlePos, fireWorldPos);
        if (dist > maxSprayToFireDistance)
            return 0f;
        float dMax = Mathf.Max(maxSprayToFireDistance, optimalSprayToFireDistance + 0.01f);
        float dOpt = Mathf.Min(optimalSprayToFireDistance, dMax - 0.001f);
        if (dist <= dOpt)
            return 1f;
        return 1f - Mathf.Clamp01((dist - dOpt) / (dMax - dOpt));
    }

    static void ApplyFireRemainingVisual(ParticleSystem ps, float remainingIntensity01, float baseEmissionMultiplier)
    {
        float k = Mathf.Clamp01(remainingIntensity01);
        var em = ps.emission;
        em.rateOverTimeMultiplier = Mathf.Max(0.04f, k) * baseEmissionMultiplier;
    }

    static void SuppressSingleFire(ParticleSystem ps)
    {
        var em = ps.emission;
        em.enabled = false;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

    void InvokeEventSafely(Action evt, string eventName)
    {
        if (evt == null)
            return;
        Delegate[] handlers = evt.GetInvocationList();
        for (int i = 0; i < handlers.Length; i++)
        {
            try
            {
                ((Action)handlers[i])?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"{nameof(WindowFireMission)}: subscriber error in {eventName} ({handlers[i].Method.DeclaringType?.Name}.{handlers[i].Method.Name}): {ex}",
                    this);
            }
        }
    }
}
