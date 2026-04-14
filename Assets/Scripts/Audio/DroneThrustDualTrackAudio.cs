using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

/// <summary>
/// Throttle-driven motor loop (e.g. ys3): pitch + light Perlin jitter + volume by drive.
/// Optional <see cref="AudioLowPassFilter"/> on the source GameObject, or route <see cref="outputMixerGroup"/> to an EV-style AudioMixer bus.
/// </summary>
public class DroneThrustDualTrackAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] PlaneController planeController;
    [Tooltip("Used for U-shaped volume vs forward speed; defaults to Rigidbody on this object.")]
    [SerializeField] Rigidbody speedReferenceBody;
    [FormerlySerializedAs("clipHigh")]
    [Tooltip("Motor loop (e.g. ys3).")]
    [SerializeField] AudioClip motorClip;
    [FormerlySerializedAs("sourceHigh")]
    [Tooltip("Leave empty to auto-create child with AudioSource.")]
    [SerializeField] AudioSource motorSource;
    [Tooltip("Same EV bus as DroneAudioController (e.g. DroneEV group on DroneEVMixer).")]
    [SerializeField] AudioMixerGroup outputMixerGroup;
    [Tooltip("Optional: use same hover/cruise/max zone scales as body audio. If empty, uses DroneAudioController.ActiveProfile on this object.")]
    [SerializeField] DroneAudioProfile volumeZoneProfile;

    [Header("Throttle")]
    [SerializeField] float throttleSmoothTime = 0.1f;

    [Header("Non-linear drive")]
    [Tooltip("X: raw throttle 0..1, Y: drive 0..1 for pitch/volume mapping. Keep mid-throttle in the lower pitch range to avoid harsh highs.")]
    [SerializeField] AnimationCurve throttleToDrive;

    [Header("Pitch (by drive 0..1)")]
    [FormerlySerializedAs("highPitchMin")]
    [SerializeField] float pitchMin = 0.84f;
    [FormerlySerializedAs("highPitchMax")]
    [Tooltip("Keep near 1.0–1.2 for natural motor; values >1.35 get thin and loud.")]
    [SerializeField] float pitchMax = 1.12f;

    [Header("Pitch jitter (anti-digital)")]
    [SerializeField] float jitterAmplitude = 0.0035f;
    [Tooltip("Perlin coordinate scale; lower = slower, less 'fluttery'.")]
    [FormerlySerializedAs("jitterSpeedHigh")]
    [SerializeField] float jitterSpeed = 7f;

    [Header("Tone (no mixer)")]
    [Tooltip("Off by default: AudioLowPassFilter + looped AudioSource often clicks at the seam. Prefer softer pitch/volume or an offline EQ'd clip.")]
    [SerializeField] bool enableLowPass = false;
    [Tooltip("Roll off highs; ~4.5k–7kHz reads as 'airframe / distance', less buzzy.")]
    [SerializeField] float lowPassCutoffHz = 5600f;
    [SerializeField] float lowPassResonanceQ = 1f;

    [Header("Volume by drive")]
    [FormerlySerializedAs("masterVolume")]
    [SerializeField] float masterVolume = 0.62f;
    [SerializeField] float volumeAtLowDrive = 0.42f;
    [SerializeField] float volumeAtHighDrive = 0.68f;
    [SerializeField] float volumeSmoothTime = 0.09f;

    [Header("Volume vs speed (quiet hover & top speed)")]
    [Tooltip("X: |velocity|/maxSpeed 0..1. Y: multiplier on throttle motor volume. Keep ~1 mid-range; dip only near 0 and 1.")]
    [SerializeField] AnimationCurve volumeVsSpeed;
    [SerializeField] float maxSpeedForVolume = 16f;
    [Tooltip("Ease speed multiplier to avoid sudden level jumps.")]
    [SerializeField] float volumeVsSpeedSmoothTime = 0.22f;

    [Header("Playback")]
    [SerializeField] bool playOnAwake = true;
    [SerializeField] bool randomizeStartTime = true;

    float _smoothedThrottle;
    float _throttleVel;
    float _volVel;
    float _smoothedSpeedVolMul = 1f;
    float _speedVolMulVelocity;
    float _perlinSeed;
    DroneAudioProfile _resolvedZoneProfile;

    void Reset()
    {
        EnsureDefaultThrottleCurve();
        EnsureDefaultVolumeVsSpeedCurve();
    }

    void Awake()
    {
        DestroyLegacyDpChild();
        if (planeController == null)
            planeController = GetComponent<PlaneController>();
        if (speedReferenceBody == null)
            speedReferenceBody = GetComponent<Rigidbody>();
        _resolvedZoneProfile = volumeZoneProfile;
        if (_resolvedZoneProfile == null)
        {
            var dac = GetComponent<DroneAudioController>();
            if (dac != null)
                _resolvedZoneProfile = dac.ActiveProfile;
        }
        _perlinSeed = Random.Range(0f, 1000f);
        EnsureDefaultThrottleCurve();
        EnsureDefaultVolumeVsSpeedCurve();
        EnsureSource();
        ConfigureSource();
        ConfigureLowPass();
    }

    void DestroyLegacyDpChild()
    {
        Transform legacy = transform.Find("ThrustMotorLow_dp");
        if (legacy != null)
            Destroy(legacy.gameObject);
    }

    void OnEnable()
    {
        if (!playOnAwake || motorSource == null || motorClip == null)
            return;
        // Must set start time before Play — playing then changing time causes a loud click.
        if (randomizeStartTime)
            RandomizeTime();
        TryPlay();
    }

    void Update()
    {
        if (planeController == null || motorSource == null)
            return;

        float targetT = Mathf.Clamp01(planeController.Throttle01);
        _smoothedThrottle = Mathf.SmoothDamp(_smoothedThrottle, targetT, ref _throttleVel, Mathf.Max(0.001f, throttleSmoothTime));

        float drive = SampleDrive(_smoothedThrottle);
        float jitter = SampleJitter(Time.time * jitterSpeed, _perlinSeed, jitterAmplitude);
        motorSource.pitch = Mathf.Lerp(pitchMin, pitchMax, drive) + jitter;

        float volTarget = masterVolume * Mathf.Lerp(volumeAtLowDrive, volumeAtHighDrive, drive);
        if (speedReferenceBody != null)
        {
            float spd = Mathf.Clamp01(speedReferenceBody.velocity.magnitude / Mathf.Max(0.01f, maxSpeedForVolume));
            float zone = _resolvedZoneProfile != null
                ? _resolvedZoneProfile.EvaluateSpeedZoneVolumeScale(spd)
                : DroneAudioProfile.EvaluateSpeedZoneVolumeScale(spd, 0.45f, 1.1f, 0.45f);
            float spdMulRaw = SampleVolumeVsSpeed(spd) * zone;
            _smoothedSpeedVolMul = Mathf.SmoothDamp(
                _smoothedSpeedVolMul,
                spdMulRaw,
                ref _speedVolMulVelocity,
                Mathf.Max(0.01f, volumeVsSpeedSmoothTime));
            volTarget *= _smoothedSpeedVolMul;
        }

        motorSource.volume = Mathf.SmoothDamp(motorSource.volume, volTarget, ref _volVel, Mathf.Max(0.001f, volumeSmoothTime));
    }

    static float SampleJitter(float coord, float seedY, float amplitude)
    {
        float n = Mathf.PerlinNoise(coord, seedY);
        return (n - 0.5f) * 2f * amplitude;
    }

    float SampleDrive(float throttle01)
    {
        if (throttleToDrive != null && throttleToDrive.length > 0)
            return Mathf.Clamp01(throttleToDrive.Evaluate(throttle01));
        return throttle01;
    }

    float SampleVolumeVsSpeed(float speed01)
    {
        if (volumeVsSpeed != null && volumeVsSpeed.length > 0)
            return Mathf.Clamp(volumeVsSpeed.Evaluate(speed01), 0f, 1.35f);
        return 1f;
    }

    void EnsureDefaultVolumeVsSpeedCurve()
    {
        if (volumeVsSpeed != null && volumeVsSpeed.length > 0)
            return;
        volumeVsSpeed = new AnimationCurve(
            new Keyframe(0f, 0.1f),
            new Keyframe(0.1f, 0.38f),
            new Keyframe(0.5f, 1.12f),
            new Keyframe(0.9f, 0.36f),
            new Keyframe(1f, 0.1f));
        volumeVsSpeed.preWrapMode = WrapMode.Clamp;
        volumeVsSpeed.postWrapMode = WrapMode.Clamp;
        for (int i = 0; i < volumeVsSpeed.length; i++)
            volumeVsSpeed.SmoothTangents(i, 0.3f);
    }

    void EnsureDefaultThrottleCurve()
    {
        if (throttleToDrive != null && throttleToDrive.length > 0)
            return;
        throttleToDrive = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0.32f),
            new Keyframe(0.58f, 0.14f, 0.45f, 0.75f),
            new Keyframe(1f, 1f, 1.25f, 0f));
        throttleToDrive.preWrapMode = WrapMode.Clamp;
        throttleToDrive.postWrapMode = WrapMode.Clamp;
        for (int i = 0; i < throttleToDrive.length; i++)
            throttleToDrive.SmoothTangents(i, 0.35f);
    }

    void ConfigureLowPass()
    {
        if (motorSource == null)
            return;
        var lp = motorSource.GetComponent<AudioLowPassFilter>();
        if (!enableLowPass)
        {
            DestroyLowPassFilter(lp);
            return;
        }
        if (lp == null)
            lp = motorSource.gameObject.AddComponent<AudioLowPassFilter>();
        lp.enabled = true;
        lp.cutoffFrequency = Mathf.Clamp(lowPassCutoffHz, 10f, 22000f);
        lp.lowpassResonanceQ = Mathf.Clamp(lowPassResonanceQ, 1f, 10f);
    }

    static void DestroyLowPassFilter(AudioLowPassFilter lp)
    {
        if (lp == null)
            return;
        if (Application.isPlaying)
            Destroy(lp);
        else
            DestroyImmediate(lp);
    }

    void EnsureSource()
    {
        if (motorSource != null)
            return;
        var go = new GameObject("ThrustMotorHigh_ys3");
        go.transform.SetParent(transform, false);
        motorSource = go.AddComponent<AudioSource>();
    }

    void ConfigureSource()
    {
        if (motorSource == null)
            return;
        motorSource.clip = motorClip;
        motorSource.loop = true;
        motorSource.playOnAwake = false;
        motorSource.spatialBlend = 0f;
        motorSource.outputAudioMixerGroup = outputMixerGroup;
    }

    void RandomizeTime()
    {
        if (motorSource == null || motorSource.clip == null || motorSource.clip.length < 0.02f)
            return;
        motorSource.time = Random.Range(0f, motorSource.clip.length);
    }

    void TryPlay()
    {
        if (motorSource != null && motorClip != null && !motorSource.isPlaying)
            motorSource.Play();
    }

    void OnDisable()
    {
        if (motorSource != null && motorSource.isPlaying)
            motorSource.Stop();
    }

#if UNITY_EDITOR
    public void EditorApplyClipsAndConfigure()
    {
        EnsureDefaultThrottleCurve();
        EnsureDefaultVolumeVsSpeedCurve();
        EnsureSource();
        ConfigureSource();
        ConfigureLowPass();
        RandomizeTime();
    }
#endif
}
