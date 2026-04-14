using UnityEngine;

/// <summary>
/// 关卡分阶段背景音乐：进场景至开始自动巡航前一段；巡航中一段；飞抵终点后再一段。
/// 依赖 <see cref="DroneAutocruiseController"/> 的 <see cref="DroneAutocruiseController.OnCruiseStarted"/> /
/// <see cref="DroneAutocruiseController.OnAutocruiseRouteCompleted"/> / <see cref="DroneAutocruiseController.OnCruiseStopped"/>。
/// </summary>
[DefaultExecutionOrder(-200)]
public class ScenePhaseBgmController : MonoBehaviour
{
    enum Phase
    {
        PreCruise,
        Cruise,
        Arrived
    }

    [Header("Refs")]
    [Tooltip("留空则运行时 FindFirstObjectByType")]
    [SerializeField] DroneAutocruiseController autocruise;

    [Tooltip("留空则在本物体上取或添加 AudioSource；建议 2D、loop 由本脚本控制")]
    [SerializeField] AudioSource musicSource;

    [Header("BGM（均可留空：该阶段保持静音或上一段）")]
    [Tooltip("进场景起 → 进入自动巡航前（含自由飞行与路线规划 UI）")]
    [SerializeField] AudioClip preCruiseBgm;
    [Tooltip("自动巡航进行中")]
    [SerializeField] AudioClip cruiseBgm;
    [Tooltip("非循环路线抵达最后一个航点后")]
    [SerializeField] AudioClip afterArrivalBgm;

    [Range(0f, 1f)]
    [SerializeField] float musicVolume = 0.7f;

    [Header("可选")]
    [Tooltip("巡航中按 Y 等提前结束时，是否切回 preCruiseBgm")]
    [SerializeField] bool revertToPreCruiseIfCruiseCancelled = true;

    Phase _phase = Phase.PreCruise;
    bool _arrivalEventConsumedForStop;

    void Awake()
    {
        if (autocruise == null)
            autocruise = FindFirstObjectByType<DroneAutocruiseController>();
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
            if (musicSource == null)
                musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;
    }

    void OnEnable()
    {
        if (autocruise == null)
            return;
        autocruise.OnCruiseStarted += OnCruiseStarted;
        autocruise.OnAutocruiseRouteCompleted += OnRouteCompleted;
        autocruise.OnCruiseStopped += OnCruiseStopped;
    }

    void OnDisable()
    {
        if (autocruise == null)
            return;
        autocruise.OnCruiseStarted -= OnCruiseStarted;
        autocruise.OnAutocruiseRouteCompleted -= OnRouteCompleted;
        autocruise.OnCruiseStopped -= OnCruiseStopped;
    }

    void Start()
    {
        if (!Application.isPlaying)
            return;
        _phase = Phase.PreCruise;
        _arrivalEventConsumedForStop = false;
        PlayBgm(preCruiseBgm);
    }

    void OnCruiseStarted()
    {
        if (!Application.isPlaying)
            return;
        _phase = Phase.Cruise;
        _arrivalEventConsumedForStop = false;
        PlayBgm(cruiseBgm);
    }

    void OnRouteCompleted()
    {
        if (!Application.isPlaying)
            return;
        _phase = Phase.Arrived;
        _arrivalEventConsumedForStop = true;
        PlayBgm(afterArrivalBgm);
    }

    void OnCruiseStopped()
    {
        if (!Application.isPlaying)
            return;
        if (_arrivalEventConsumedForStop)
        {
            _arrivalEventConsumedForStop = false;
            return;
        }

        if (!revertToPreCruiseIfCruiseCancelled)
            return;
        if (_phase != Phase.Cruise)
            return;
        _phase = Phase.PreCruise;
        PlayBgm(preCruiseBgm);
    }

    void PlayBgm(AudioClip clip)
    {
        if (musicSource == null)
            return;
        musicSource.volume = musicVolume;
        if (clip == null)
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        if (musicSource.isPlaying && musicSource.clip == clip)
            return;
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }
}
