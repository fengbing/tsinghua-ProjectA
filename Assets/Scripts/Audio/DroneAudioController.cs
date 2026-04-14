using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Unified EV-style drone audio controller.
/// Maps physics speed + movement intent to pitch/volume/pan/mixer parameters.
/// Optional dual-layer loop, Perlin pitch wobble, volume-vs-pitch curve, and mixer low-pass.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class DroneAudioController : MonoBehaviour
{
    const float FallbackMinPitch = 0.76f;
    const float FallbackMaxPitch = 1.16f;
    const float FallbackMinVolume = 0.5f;
    const float FallbackMaxVolume = 0.95f;
    const float FallbackThrustPitchBoost = 0.035f;
    const float FallbackThrustAttack = 0.06f;
    const float FallbackThrustRelease = 0.14f;
    const float FallbackPitchSmooth = 0.14f;
    const float FallbackVolumeSmooth = 0.14f;
    const float FallbackPanSmooth = 0.1f;
    const float FallbackLiftVolume = 0.1f;
    const float FallbackLiftPitch = 0.025f;
    const float FallbackDropMul = 0.75f;
    const float FallbackPanDepth = 0.14f;
    const float FallbackDualPitchMul = 1.005f;
    const float FallbackDualVolPerSource = 0.5f;
    const float FallbackPerlinAmp = 0.0035f;
    const float FallbackPerlinHz = 2.4f;
    const bool FallbackBuiltinLowPass = false;
    const float FallbackBuiltinLowPassHz = 5400f;
    const float FallbackBuiltinLowPassQ = 1f;
    const float FallbackLpfAtMin = 5800f;
    const float FallbackLpfAtMax = 2200f;
    const float FallbackLpfMaxCap = 6200f;
    const float FallbackLpfSmooth = 0.2f;
    const float FallbackTurnSpeedThreshold = 0.52f;
    const float FallbackTurnPanExtra = 0.58f;
    const float FallbackYawRateFull = 2f;
    const float FallbackTurnPitchExtra = 0.032f;
    const float FallbackVolumeVsSpeedSmooth = 0.22f;
    const float FallbackHoverVolScale = 0.45f;
    const float FallbackCruiseVolScale = 1.1f;
    const float FallbackMaxSpeedVolScale = 0.45f;

    [Header("References")]
    [SerializeField] AudioSource audioSource;
    [Tooltip("Second layer; leave empty to auto-create child when dual-layer is enabled.")]
    [SerializeField] AudioSource audioSourceLayerB;
    [SerializeField] Rigidbody targetRigidbody;
    [SerializeField] PlaneController planeController;
    [SerializeField] AudioMixer audioMixer;
    [Header("Mixer routing (EV bus)")]
    [Tooltip("Route body loop(s) through DroneEV (or similar) for softer highs. Assign the same AudioMixer below for pitch-driven low-pass.")]
    [SerializeField] AudioMixerGroup droneMixerGroup;
    [SerializeField] DroneAudioProfile profile;

    /// <summary>Same profile reference for optional tooling (e.g. thrust layer zone scales).</summary>
    public DroneAudioProfile ActiveProfile => profile;
    [Tooltip("Optional runtime fallback clip when profile is not assigned.")]
    [SerializeField] AudioClip fallbackLoopClip;

    [Header("Fallbacks")]
    [Tooltip("Used when PlaneController max speed is not exposed.")]
    [SerializeField] float maxSpeed = 16f;
    [SerializeField] bool autoPlayOnStart = true;

    [Header("Optional Mixer Params (lift / drop)")]
    [Tooltip("Lift presence (e.g. mid/high EQ gain in dB).")]
    [SerializeField] string liftPresenceParam = string.Empty;
    [SerializeField] float liftPresenceIdle = 0f;
    [SerializeField] float liftPresenceActive = 3f;
    [SerializeField] float liftPresenceSmoothTime = 0.1f;

    [Tooltip("Drop darkening (e.g. high-pass cutoff frequency).")]
    [SerializeField] string dropHighPassCutoffParam = string.Empty;
    [SerializeField] float dropHighPassNormal = 8000f;
    [SerializeField] float dropHighPassActive = 2400f;
    [SerializeField] float dropHighPassSmoothTime = 0.1f;

    [Header("Mixer low-pass override")]
    [Tooltip("If set, overrides profile LowPassCutoffParam.")]
    [SerializeField] string lowPassCutoffParamOverride = string.Empty;

    float _pitchVelocity;
    float _volumeVelocity;
    float _panVelocity;
    float _boostVelocity;
    float _presenceVelocity;
    float _highPassVelocity;
    float _lpfVelocity;

    float _currentBoost;
    float _currentPresence;
    float _currentHighPass;
    float _currentLpf;
    bool _lowPassInitialized;

    float _smoothedLogicalPitch = 1f;
    float _smoothedOutputVolume = 0f;
    float _smoothedSpeedVolMul = 1f;
    float _speedVolMulVelocity;
    float _perlinSeed;

    static AnimationCurve _defaultVolumeVsPitch;
    static AnimationCurve _defaultVolumeVsSpeed;

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody>();
        if (planeController == null)
            planeController = GetComponent<PlaneController>();
        _perlinSeed = Random.Range(0f, 1000f);

        if (GetDualLayerBlend())
            EnsureLayerB();
    }

    void EnsureLayerB()
    {
        if (audioSourceLayerB != null)
            return;

        var go = new GameObject("DroneAudioLayerB");
        go.transform.SetParent(transform, false);
        audioSourceLayerB = go.AddComponent<AudioSource>();
        audioSourceLayerB.playOnAwake = false;
        audioSourceLayerB.loop = true;
        if (audioSource != null)
            audioSourceLayerB.spatialBlend = audioSource.spatialBlend;
    }

    void Start()
    {
        ApplyProfileDefaults();
        ApplyDroneMixerRouting();
        RandomizePlaybackStartTimes();
        ApplyBuiltinLowPassToSources();
        if (autoPlayOnStart)
            TryStartPlayback();
    }

    void ApplyDroneMixerRouting()
    {
        if (droneMixerGroup == null)
            return;
        if (audioSource != null)
            audioSource.outputAudioMixerGroup = droneMixerGroup;
        if (audioSourceLayerB != null)
            audioSourceLayerB.outputAudioMixerGroup = droneMixerGroup;
    }

    void TryStartPlayback()
    {
        if (audioSource == null)
            return;
        if (audioSource.clip == null)
        {
            Debug.LogWarning("[DroneAudioController] No clip assigned. Assign profile.BaseLoopClip or fallbackLoopClip.", this);
            return;
        }

        if (!audioSource.isPlaying)
            audioSource.Play();
        if (GetDualLayerBlend() && audioSourceLayerB != null && audioSourceLayerB.clip != null && !audioSourceLayerB.isPlaying)
            audioSourceLayerB.Play();
    }

    void RandomizePlaybackStartTimes()
    {
        AudioClip clip = audioSource != null ? audioSource.clip : null;
        if (clip == null || clip.length <= 0.01f)
            return;

        float len = clip.length;
        if (audioSource != null)
            audioSource.time = Random.Range(0f, len);
        if (GetDualLayerBlend() && audioSourceLayerB != null)
            audioSourceLayerB.time = Random.Range(0f, len);
    }

    void OnDisable()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
        if (audioSourceLayerB != null && audioSourceLayerB.isPlaying)
            audioSourceLayerB.Stop();
    }

    void ApplyBuiltinLowPassToSources()
    {
        bool en = profile != null ? profile.EnableBuiltinLowPass : FallbackBuiltinLowPass;
        float hz = profile != null ? profile.BuiltinLowPassCutoffHz : FallbackBuiltinLowPassHz;
        float q = profile != null ? profile.BuiltinLowPassQ : FallbackBuiltinLowPassQ;
        ConfigureBuiltinLowPass(audioSource, en, hz, q);
        ConfigureBuiltinLowPass(audioSourceLayerB, en, hz, q);
    }

    static void ConfigureBuiltinLowPass(AudioSource src, bool enabled, float cutoffHz, float resonanceQ)
    {
        if (src == null)
            return;
        var lp = src.GetComponent<AudioLowPassFilter>();
        if (!enabled)
        {
            if (lp != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(lp);
                else
                    Object.DestroyImmediate(lp);
            }
            return;
        }
        if (lp == null)
            lp = src.gameObject.AddComponent<AudioLowPassFilter>();
        lp.enabled = true;
        lp.cutoffFrequency = Mathf.Clamp(cutoffHz, 10f, 22000f);
        lp.lowpassResonanceQ = Mathf.Clamp(resonanceQ, 1f, 10f);
    }

    void ApplyProfileDefaults()
    {
        if (audioSource == null)
            return;

        AudioClip clip = null;
        bool loop = true;

        if (profile != null)
        {
            if (profile.BaseLoopClip != null)
                clip = profile.BaseLoopClip;
            loop = profile.ForceLoop;
        }
        else if (fallbackLoopClip != null)
            clip = fallbackLoopClip;

        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f;

        if (GetDualLayerBlend() && audioSourceLayerB != null)
        {
            audioSourceLayerB.clip = clip;
            audioSourceLayerB.loop = loop;
            audioSourceLayerB.playOnAwake = false;
            audioSourceLayerB.volume = 0f;
        }
    }

    void Update()
    {
        if (audioSource == null || targetRigidbody == null)
            return;

        float maxSpeedSafe = Mathf.Max(0.01f, maxSpeed);
        float speed01 = Mathf.Clamp01(targetRigidbody.velocity.magnitude / maxSpeedSafe);

        float minP = GetMinPitch();
        float maxP = GetMaxPitch();
        float basePitch = Mathf.Lerp(minP, maxP, speed01);
        float baseVolLinear = Mathf.Lerp(GetMinVolume(), GetMaxVolume(), speed01);
        float speedVolMulRaw = GetVolumeVsSpeedCurve().Evaluate(speed01) * GetSpeedZoneVolumeScale(speed01);
        _smoothedSpeedVolMul = Mathf.SmoothDamp(
            _smoothedSpeedVolMul,
            speedVolMulRaw,
            ref _speedVolMulVelocity,
            Mathf.Max(0.01f, GetVolumeVsSpeedSmoothTime()));
        float baseVolume = baseVolLinear * _smoothedSpeedVolMul;

        MovementIntent intent = ResolveIntent();
        bool isPushing = intent.IsPushing;

        float boostTarget = isPushing ? GetThrustPitchBoost() : 0f;
        float boostSmooth = isPushing ? GetThrustAttackTime() : GetThrustReleaseTime();
        _currentBoost = Mathf.SmoothDamp(_currentBoost, boostTarget, ref _boostVelocity, Mathf.Max(0.001f, boostSmooth));

        float liftWeight = Mathf.Clamp01(intent.VerticalUp01);
        float dropWeight = Mathf.Clamp01(intent.VerticalDown01);

        float turnSpeedThresh = GetTurnEffectSpeedThreshold();
        float turnGate = Mathf.Clamp01((speed01 - turnSpeedThresh) / Mathf.Max(1e-3f, 1f - turnSpeedThresh));
        float yawRate = Vector3.Dot(targetRigidbody.angularVelocity, transform.up);
        float yawNorm = Mathf.Clamp01(Mathf.Abs(yawRate) / Mathf.Max(0.01f, GetYawRateForFullTurnEffect()));
        float stick = intent.Horizontal;
        float yawSigned = Mathf.Clamp(yawRate / Mathf.Max(0.01f, GetYawRateForFullTurnEffect()), -1f, 1f);
        float mixedTurn = Mathf.Clamp(stick * 0.55f - yawSigned * 0.72f, -1f, 1f);
        float turnActivity = Mathf.Clamp01(Mathf.Max(Mathf.Abs(stick) * 0.92f, yawNorm));
        float panExtra = turnGate * GetHighSpeedTurnPanExtra() * mixedTurn * Mathf.Lerp(0.35f, 1f, turnActivity);
        float panTarget = Mathf.Clamp(stick * GetPanDepth() + panExtra, -1f, 1f);
        float turnPitch = turnGate * turnActivity * GetHighSpeedTurnPitchExtra();

        float pitchTarget = basePitch + _currentBoost + GetLiftPitchBonus() * liftWeight + turnPitch;
        float volumeBeforeCurve = baseVolume + GetLiftVolumeBonus() * liftWeight;
        volumeBeforeCurve *= Mathf.Lerp(1f, GetDropVolumeMultiplier(), dropWeight);

        _smoothedLogicalPitch = Mathf.SmoothDamp(
            _smoothedLogicalPitch,
            pitchTarget,
            ref _pitchVelocity,
            Mathf.Max(0.001f, GetPitchSmoothTime()));

        float perlinCoord = Time.time * Mathf.Max(0.01f, GetPerlinFrequencyHz());
        float perlin01 = Mathf.PerlinNoise(perlinCoord, _perlinSeed);
        float perlinJitter = (perlin01 - 0.5f) * 2f * GetPerlinAmplitude();

        float pitchLayerA = _smoothedLogicalPitch + perlinJitter;
        float pitchLayerB = pitchLayerA * GetDualPitchMultiplier();

        float pitchNorm = Mathf.InverseLerp(minP, maxP, Mathf.Clamp(_smoothedLogicalPitch, minP, maxP));
        float curveMul = GetVolumeVsPitchCurve().Evaluate(pitchNorm);
        float targetTotalVolume = volumeBeforeCurve * curveMul;

        float volScale = GetDualLayerBlend() ? GetDualVolumeScalePerSource() : 1f;
        float targetPerSourceVolume = targetTotalVolume * volScale;

        _smoothedOutputVolume = Mathf.SmoothDamp(
            _smoothedOutputVolume,
            targetPerSourceVolume,
            ref _volumeVelocity,
            Mathf.Max(0.001f, GetVolumeSmoothTime()));

        audioSource.pitch = pitchLayerA;
        audioSource.volume = _smoothedOutputVolume;
        audioSource.panStereo = Mathf.SmoothDamp(audioSource.panStereo, panTarget, ref _panVelocity, Mathf.Max(0.001f, GetPanSmoothTime()));

        if (GetDualLayerBlend() && audioSourceLayerB != null)
        {
            audioSourceLayerB.pitch = pitchLayerB;
            audioSourceLayerB.volume = _smoothedOutputVolume;
            audioSourceLayerB.panStereo = audioSource.panStereo;
        }
        else if (audioSourceLayerB != null)
        {
            audioSourceLayerB.volume = 0f;
        }

        DriveOptionalMixer(liftWeight, dropWeight);
        DriveLowPassFromPitch();
    }

    void DriveLowPassFromPitch()
    {
        string param = !string.IsNullOrWhiteSpace(lowPassCutoffParamOverride)
            ? lowPassCutoffParamOverride
            : (profile != null ? profile.LowPassCutoffParam : string.Empty);

        if (audioMixer == null || string.IsNullOrWhiteSpace(param))
            return;

        float minP = GetMinPitch();
        float maxP = GetMaxPitch();
        float pitchNorm = Mathf.InverseLerp(minP, maxP, Mathf.Clamp(_smoothedLogicalPitch, minP, maxP));

        float atMin = profile != null ? profile.LowPassCutoffAtMinPitch : FallbackLpfAtMin;
        float atMax = profile != null ? profile.LowPassCutoffAtMaxPitch : FallbackLpfAtMax;
        float cap = profile != null ? profile.MaxLowPassCutoffHz : FallbackLpfMaxCap;
        float smoothT = profile != null ? profile.LowPassSmoothTime : FallbackLpfSmooth;

        float target = Mathf.Lerp(atMin, atMax, pitchNorm);
        target = Mathf.Min(target, cap);

        if (!_lowPassInitialized)
        {
            _currentLpf = target;
            _lowPassInitialized = true;
        }
        else
            _currentLpf = Mathf.SmoothDamp(_currentLpf, target, ref _lpfVelocity, Mathf.Max(0.001f, smoothT));

        audioMixer.SetFloat(param, _currentLpf);
    }

    void DriveOptionalMixer(float liftWeight, float dropWeight)
    {
        if (audioMixer == null)
            return;

        if (!string.IsNullOrWhiteSpace(liftPresenceParam))
        {
            float presenceTarget = Mathf.Lerp(liftPresenceIdle, liftPresenceActive, liftWeight);
            _currentPresence = Mathf.SmoothDamp(_currentPresence, presenceTarget, ref _presenceVelocity, Mathf.Max(0.001f, liftPresenceSmoothTime));
            audioMixer.SetFloat(liftPresenceParam, _currentPresence);
        }

        if (!string.IsNullOrWhiteSpace(dropHighPassCutoffParam))
        {
            float highPassTarget = Mathf.Lerp(dropHighPassNormal, dropHighPassActive, dropWeight);
            _currentHighPass = Mathf.SmoothDamp(_currentHighPass, highPassTarget, ref _highPassVelocity, Mathf.Max(0.001f, dropHighPassSmoothTime));
            audioMixer.SetFloat(dropHighPassCutoffParam, _currentHighPass);
        }
    }

    MovementIntent ResolveIntent()
    {
        float horizontal = 0f;
        float verticalPlanar = 0f;
        float verticalUp01 = 0f;
        float verticalDown01 = 0f;
        bool pushing = false;

        if (planeController != null)
        {
            horizontal = Mathf.Clamp(planeController.HorizontalInputRaw, -1f, 1f);
            float throttle = Mathf.Clamp01(planeController.Throttle01);
            float verticalSpeed = planeController.VerticalSpeed;

            pushing = throttle > 0.02f;
            verticalUp01 = Mathf.Clamp01(verticalSpeed / Mathf.Max(0.1f, maxSpeed * 0.5f));
            verticalDown01 = Mathf.Clamp01(-verticalSpeed / Mathf.Max(0.1f, maxSpeed * 0.5f));
            verticalPlanar = throttle;
        }
        else
        {
            horizontal = Input.GetAxisRaw("Horizontal");
            verticalPlanar = Mathf.Abs(Input.GetAxisRaw("Vertical"));
            if (Input.GetKey(KeyCode.Space))
                verticalUp01 = 1f;
            if (Input.GetKey(KeyCode.LeftControl))
                verticalDown01 = 1f;
            pushing = verticalPlanar > 0.01f || Mathf.Abs(horizontal) > 0.01f || verticalUp01 > 0f || verticalDown01 > 0f;
        }

        bool hardPush =
            Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f ||
            Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f ||
            Input.GetKey(KeyCode.Space) ||
            Input.GetKey(KeyCode.LeftControl);
        pushing = pushing || hardPush;

        if (Input.GetKey(KeyCode.Space))
            verticalUp01 = Mathf.Max(verticalUp01, 1f);
        if (Input.GetKey(KeyCode.LeftControl))
            verticalDown01 = Mathf.Max(verticalDown01, 1f);

        return new MovementIntent
        {
            Horizontal = horizontal,
            VerticalPlanar = verticalPlanar,
            VerticalUp01 = verticalUp01,
            VerticalDown01 = verticalDown01,
            IsPushing = pushing
        };
    }

    struct MovementIntent
    {
        public float Horizontal;
        public float VerticalPlanar;
        public float VerticalUp01;
        public float VerticalDown01;
        public bool IsPushing;
    }

    AnimationCurve GetVolumeVsPitchCurve()
    {
        if (profile != null && profile.VolumeVsPitch != null && profile.VolumeVsPitch.length > 0)
            return profile.VolumeVsPitch;

        if (_defaultVolumeVsPitch == null || _defaultVolumeVsPitch.length == 0)
        {
            _defaultVolumeVsPitch = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0.52f));
            _defaultVolumeVsPitch.preWrapMode = WrapMode.Clamp;
            _defaultVolumeVsPitch.postWrapMode = WrapMode.Clamp;
        }

        return _defaultVolumeVsPitch;
    }

    AnimationCurve GetVolumeVsSpeedCurve()
    {
        if (profile != null && profile.VolumeVsSpeed != null && profile.VolumeVsSpeed.length > 0)
            return profile.VolumeVsSpeed;

        if (_defaultVolumeVsSpeed == null || _defaultVolumeVsSpeed.length == 0)
        {
            _defaultVolumeVsSpeed = new AnimationCurve(
                new Keyframe(0f, 0.1f),
                new Keyframe(0.1f, 0.38f),
                new Keyframe(0.5f, 1.12f),
                new Keyframe(0.9f, 0.36f),
                new Keyframe(1f, 0.1f));
            _defaultVolumeVsSpeed.preWrapMode = WrapMode.Clamp;
            _defaultVolumeVsSpeed.postWrapMode = WrapMode.Clamp;
            for (int i = 0; i < _defaultVolumeVsSpeed.length; i++)
                _defaultVolumeVsSpeed.SmoothTangents(i, 0.3f);
        }

        return _defaultVolumeVsSpeed;
    }

    bool GetDualLayerBlend() => profile != null ? profile.DualLayerBlend : true;

    float GetDualPitchMultiplier() => profile != null ? profile.DualLayerPitchMultiplier : FallbackDualPitchMul;

    float GetDualVolumeScalePerSource() => profile != null ? profile.DualLayerVolumeScalePerSource : FallbackDualVolPerSource;

    float GetPerlinAmplitude() => profile != null ? profile.PerlinPitchAmplitude : FallbackPerlinAmp;

    float GetPerlinFrequencyHz() => profile != null ? profile.PerlinPitchFrequencyHz : FallbackPerlinHz;

    float GetMinPitch() => profile != null ? profile.MinPitch : FallbackMinPitch;
    float GetMaxPitch() => profile != null ? profile.MaxPitch : FallbackMaxPitch;
    float GetMinVolume() => profile != null ? profile.MinVolume : FallbackMinVolume;
    float GetMaxVolume() => profile != null ? profile.MaxVolume : FallbackMaxVolume;
    float GetThrustPitchBoost() => profile != null ? profile.ThrustPitchBoost : FallbackThrustPitchBoost;
    float GetThrustAttackTime() => profile != null ? profile.ThrustBoostAttackTime : FallbackThrustAttack;
    float GetThrustReleaseTime() => profile != null ? profile.ThrustBoostReleaseTime : FallbackThrustRelease;
    float GetPitchSmoothTime() => profile != null ? profile.PitchSmoothTime : FallbackPitchSmooth;
    float GetVolumeSmoothTime() => profile != null ? profile.VolumeSmoothTime : FallbackVolumeSmooth;
    float GetPanSmoothTime() => profile != null ? profile.PanSmoothTime : FallbackPanSmooth;
    float GetLiftVolumeBonus() => profile != null ? profile.LiftVolumeBonus : FallbackLiftVolume;
    float GetLiftPitchBonus() => profile != null ? profile.LiftPitchBonus : FallbackLiftPitch;
    float GetDropVolumeMultiplier() => profile != null ? profile.DropVolumeMultiplier : FallbackDropMul;
    float GetPanDepth() => profile != null ? profile.PanDepth : FallbackPanDepth;

    float GetTurnEffectSpeedThreshold() => profile != null ? profile.TurnEffectSpeedThreshold : FallbackTurnSpeedThreshold;

    float GetHighSpeedTurnPanExtra() => profile != null ? profile.HighSpeedTurnPanExtra : FallbackTurnPanExtra;

    float GetYawRateForFullTurnEffect() => profile != null ? profile.YawRateForFullTurnEffect : FallbackYawRateFull;

    float GetHighSpeedTurnPitchExtra() => profile != null ? profile.HighSpeedTurnPitchExtra : FallbackTurnPitchExtra;

    float GetVolumeVsSpeedSmoothTime() => profile != null ? profile.VolumeVsSpeedSmoothTime : FallbackVolumeVsSpeedSmooth;

    float GetSpeedZoneVolumeScale(float speed01)
    {
        if (profile != null)
            return profile.EvaluateSpeedZoneVolumeScale(speed01);
        return DroneAudioProfile.EvaluateSpeedZoneVolumeScale(
            speed01, FallbackHoverVolScale, FallbackCruiseVolScale, FallbackMaxSpeedVolScale);
    }
}
