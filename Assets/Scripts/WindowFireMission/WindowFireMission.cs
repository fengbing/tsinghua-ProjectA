using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Proximity-gated two-step F interaction: sprinkler/extinguish then load next scene.
/// Drone is detected via <see cref="PlaneController"/> on the entering collider's parent chain.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WindowFireMission : MonoBehaviour
{
    enum Phase
    {
        Idle,
        AwaitSprinklerF,
        AwaitThermalF,
        Done
    }

    [Header("UI")]
    [SerializeField] WindowFireDualPromptHud hud;

    [Header("VFX (window)")]
    [Tooltip("第一次按 F 后仅关闭这些粒子；烟留在 Smoke Effects 里且不会被关")]
    [SerializeField] List<ParticleSystem> fireEffects = new List<ParticleSystem>();
    [Tooltip("烟粒子；当前流程不会在按 F 后关闭，仅作列表占位或后续扩展")]
    [SerializeField] List<ParticleSystem> smokeEffects = new List<ParticleSystem>();

    [Header("VFX (drone)")]
    [Tooltip("挂在无人机上的喷水 ParticleSystem；在 WindowFireMission 上从 Hierarchy 拖无人机子物体上的粒子")]
    [FormerlySerializedAs("waterSpray")]
    [SerializeField] ParticleSystem droneWaterSpray;

    [Header("Audio")]
    [SerializeField] AudioSource voiceSource;
    [SerializeField] AudioClip narrationOnApproach;
    [SerializeField] AudioClip narrationAfterExtinguish;

    [Header("Scene")]
    [Tooltip("热成像等下一阶段场景名（与 Build Settings 里场景名一致，例如 fire）")]
    [SerializeField] string nextSceneName = "fire";
    [SerializeField] LoadSceneMode loadMode = LoadSceneMode.Single;

    Phase _phase = Phase.Idle;
    int _triggerCount;
    bool _loadStarted;
    AudioClip _lastVoiceClip;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;
    }

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();
        if (voiceSource == null)
            voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
    }

    void Start()
    {
        if (hud == null)
            hud = FindObjectOfType<WindowFireDualPromptHud>();
    }

    void Update()
    {
        if (_triggerCount <= 0 || _loadStarted)
            return;

        if (!Input.GetKeyDown(KeyCode.F))
            return;

        if (_phase == Phase.AwaitSprinklerF)
            OnSprinklerConfirmed();
        else if (_phase == Phase.AwaitThermalF)
            OnThermalConfirmed();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsDrone(other))
            return;

        _triggerCount++;
        if (_phase == Phase.Idle)
            BeginSprinklerPhase();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsDrone(other))
            return;

        _triggerCount = Mathf.Max(0, _triggerCount - 1);
        if (_triggerCount == 0 && _phase != Phase.Done && !_loadStarted)
        {
            if (_phase == Phase.AwaitSprinklerF)
            {
                _phase = Phase.Idle;
                if (hud != null)
                    hud.Hide();
                StopVoiceIfPlaying();
            }
        }
    }

    static bool IsDrone(Collider other)
    {
        return other.GetComponentInParent<PlaneController>() != null;
    }

    void BeginSprinklerPhase()
    {
        if (hud != null)
            hud.ShowSprinklerPrompt();
        PlayVoiceOnce(narrationOnApproach);
        _phase = Phase.AwaitSprinklerF;
    }

    void OnSprinklerConfirmed()
    {
        if (droneWaterSpray != null)
        {
            droneWaterSpray.gameObject.SetActive(true);
            droneWaterSpray.Play(true);
        }

        SuppressEffects(fireEffects);

        PlayVoiceOnce(narrationAfterExtinguish);
        if (hud != null)
            hud.ShowThermalPrompt();
        _phase = Phase.AwaitThermalF;
    }

    void OnThermalConfirmed()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning($"{nameof(WindowFireMission)}: next scene name is empty.", this);
            return;
        }

        _loadStarted = true;
        _phase = Phase.Done;
        if (hud != null)
            hud.Hide();
        SceneManager.LoadScene(nextSceneName, loadMode);
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

    void PlayVoiceOnce(AudioClip clip)
    {
        if (clip == null || voiceSource == null)
            return;
        if (voiceSource.isPlaying && _lastVoiceClip == clip)
            return;
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();
        _lastVoiceClip = clip;
    }

    void StopVoiceIfPlaying()
    {
        if (voiceSource != null && voiceSource.isPlaying)
            voiceSource.Stop();
        _lastVoiceClip = null;
    }
}
