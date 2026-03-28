using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 窗口区第一段旁白；烟区第二段旁白与交互；Zone2 仅控制烟环境音；UI 在对应旁白播完后出现；第二次 F 全屏图，ESC 关闭。
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

    [Header("结束：全屏图（第二次按 F，不再切场景）")]
    [Tooltip("第二次按 F 后全屏显示；按 ESC 关闭")]
    [SerializeField] Sprite endFullscreenSprite;
    [SerializeField] int endScreenCanvasSortOrder = 400;

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

        HideEndFullscreen();
    }

    void Update()
    {
        if (_endFullscreenActive && Input.GetKeyDown(KeyCode.Escape))
        {
            HideEndFullscreen();
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
        if (endFullscreenSprite == null)
        {
            Debug.LogWarning($"{nameof(WindowFireMission)}: 未设置 {nameof(endFullscreenSprite)}，无法显示结束全屏图。", this);
            return;
        }

        _loadStarted = true;
        _phase = Phase.Done;
        StopHudDelayCoroutines();
        if (hud != null)
            hud.Hide();
        ShowEndFullscreen();
    }

    void ShowEndFullscreen()
    {
        if (_endFullscreenRoot != null || endFullscreenSprite == null)
            return;

        var canvasGo = new GameObject("MissionEndFullscreen");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = endScreenCanvasSortOrder;
        canvasGo.AddComponent<GraphicRaycaster>();
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var imgGo = new GameObject("FullScreenImage", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(canvasGo.transform, false);
        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = imgGo.GetComponent<Image>();
        img.sprite = endFullscreenSprite;
        img.preserveAspect = true;
        img.raycastTarget = true;

        _endFullscreenRoot = canvasGo;
        _endFullscreenActive = true;
    }

    void HideEndFullscreen()
    {
        if (_endFullscreenRoot != null)
            Destroy(_endFullscreenRoot);
        _endFullscreenRoot = null;
        _endFullscreenActive = false;
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
