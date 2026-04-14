using UnityEngine;

/// <summary>
/// Drone EV-style audio tuning profile.
/// Assign ys.wav (or derived loop) to BaseLoopClip.
/// </summary>
[CreateAssetMenu(fileName = "DroneAudioProfile", menuName = "Audio/Drone Audio Profile")]
public class DroneAudioProfile : ScriptableObject
{
    /// <summary>Inspector / OnValidate clamp for hover & cruise & max speed zone volume multipliers.</summary>
    public const float SpeedZoneVolumeScaleMax = 8f;

    [Header("Source")]
    public AudioClip BaseLoopClip;
    [Tooltip("Enable loop on the assigned AudioSource at runtime.")]
    public bool ForceLoop = true;

    [Header("Speed -> Base Tone")]
    public float MinPitch = 0.76f;
    [Tooltip("Keep max pitch modest; high values + thrust boost sound thin and synthetic.")]
    public float MaxPitch = 1.16f;
    [Tooltip("Volume envelope along speed; stack with VolumeVsSpeed curve and Speed zone volume (hover / cruise / max).")]
    public float MinVolume = 0.5f;
    public float MaxVolume = 0.95f;

    [Header("Thrust Boost")]
    [Tooltip("Extra pitch when pushing any thrust input.")]
    public float ThrustPitchBoost = 0.035f;
    [Tooltip("How quickly thrust boost engages.")]
    public float ThrustBoostAttackTime = 0.06f;
    [Tooltip("How slowly thrust boost releases.")]
    public float ThrustBoostReleaseTime = 0.14f;

    [Header("General Smoothing")]
    public float PitchSmoothTime = 0.14f;
    public float VolumeSmoothTime = 0.14f;
    public float PanSmoothTime = 0.1f;

    [Header("Vertical Character")]
    [Tooltip("Lift (Space/up intent) volume add.")]
    public float LiftVolumeBonus = 0.1f;
    [Tooltip("Lift (Space/up intent) extra pitch add.")]
    public float LiftPitchBonus = 0.025f;
    [Tooltip("Drop (Ctrl/down intent) volume multiplier.")]
    public float DropVolumeMultiplier = 0.75f;

    [Header("Stereo Pan")]
    [Tooltip("A/D turn pan depth. +right, -left.")]
    [Range(0f, 1f)]
    public float PanDepth = 0.14f;

    [Header("Dual layer (reduces loop phasing)")]
    [Tooltip("Play two looped sources: second pitch = first × multiplier.")]
    public bool DualLayerBlend = true;
    [Tooltip("Second layer pitch multiplier; smaller detune = less metallic beating.")]
    public float DualLayerPitchMultiplier = 1.005f;
    [Tooltip("Volume per layer so summed loudness stays similar (0.5 + 0.5 ≈ 1).")]
    [Range(0.2f, 1f)]
    public float DualLayerVolumeScalePerSource = 0.5f;

    [Header("Pitch micro-variation")]
    [Tooltip("Perlin-based pitch wobble amplitude.")]
    public float PerlinPitchAmplitude = 0.0035f;
    [Tooltip("Coordinate scale for Perlin (higher = faster variation).")]
    public float PerlinPitchFrequencyHz = 2.4f;

    [Header("Volume vs pitch")]
    [Tooltip("X: normalized pitch (0 = MinPitch, 1 = MaxPitch). Y: volume multiplier applied after speed-based volume.")]
    public AnimationCurve VolumeVsPitch;

    [Header("Volume vs speed (quiet hover & top speed)")]
    [Tooltip("X: speed 0 (hover) .. 1 (max). Y: multiplier on Lerp(MinVolume, MaxVolume, X). Use very low Y at 0/1 (barely audible); Y>1 mid-range boosts cruise vs extremes.")]
    public AnimationCurve VolumeVsSpeed;
    [Tooltip("Seconds to ease the speed volume multiplier (reduces abrupt jumps in/out of hover / max speed).")]
    public float VolumeVsSpeedSmoothTime = 0.22f;

    [Header("Speed zone volume (hover / cruise / max)")]
    [Tooltip("Extra multiplier near hover (low forward speed). Stacks with VolumeVsSpeed curve.")]
    [Range(0f, SpeedZoneVolumeScaleMax)]
    public float HoverVolumeScale = 0.45f;
    [Tooltip("Extra multiplier in mid-speed range (cruise / normal flight).")]
    [Range(0f, SpeedZoneVolumeScaleMax)]
    public float CruiseVolumeScale = 1.1f;
    [Tooltip("Extra multiplier near max forward speed.")]
    [Range(0f, SpeedZoneVolumeScaleMax)]
    public float MaxSpeedVolumeScale = 0.45f;

    [Header("High-speed turn (motion cue)")]
    [Tooltip("Apply extra pan / pitch from yaw when forward speed fraction is above this.")]
    [Range(0.05f, 0.95f)]
    public float TurnEffectSpeedThreshold = 0.52f;
    [Tooltip("Extra stereo pan from yaw at high speed (adds to stick pan).")]
    public float HighSpeedTurnPanExtra = 0.58f;
    [Tooltip("Abs yaw rate (rad/s) mapped to full turn pan contribution.")]
    public float YawRateForFullTurnEffect = 2f;
    [Tooltip("Pitch add when yawing hard at high speed (subtle load / asymmetry).")]
    public float HighSpeedTurnPitchExtra = 0.032f;

    [Header("Built-in low-pass (no mixer)")]
    [Tooltip("Uses AudioLowPassFilter on each loop AudioSource. Often causes clicks at loop seams — leave off unless you accept that risk or use a crossfaded clip.")]
    public bool EnableBuiltinLowPass = false;
    [Tooltip("Lower = darker / less fatiguing (typical 4500–6500).")]
    public float BuiltinLowPassCutoffHz = 5400f;
    [Tooltip("Keep at 1 unless you want a resonant peak (can sound nasal).")]
    public float BuiltinLowPassQ = 1f;

    [Header("Low-pass (mixer) — optional")]
    [Tooltip("Exposed name on the mixer (Tools/Drone Audio/Create EV Mixer → DroneEV_LP_Cutoff).")]
    public string LowPassCutoffParam = "DroneEV_LP_Cutoff";
    [Tooltip("Cutoff when logical pitch is at MinPitch (relatively open, capped by MaxLowPassCutoffHz).")]
    public float LowPassCutoffAtMinPitch = 5800f;
    [Tooltip("Cutoff when logical pitch is at MaxPitch (darker EV tone, less buzz).")]
    public float LowPassCutoffAtMaxPitch = 2200f;
    [Tooltip("Never exceed this Hz (prevents runaway brightness / harsh highs).")]
    public float MaxLowPassCutoffHz = 6200f;
    public float LowPassSmoothTime = 0.2f;

    void OnValidate()
    {
        if (VolumeVsPitch == null || VolumeVsPitch.length == 0)
        {
            VolumeVsPitch = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0.52f));
            VolumeVsPitch.preWrapMode = WrapMode.Clamp;
            VolumeVsPitch.postWrapMode = WrapMode.Clamp;
        }

        if (VolumeVsSpeed == null || VolumeVsSpeed.length == 0)
        {
            VolumeVsSpeed = new AnimationCurve(
                new Keyframe(0f, 0.1f),
                new Keyframe(0.1f, 0.38f),
                new Keyframe(0.5f, 1.12f),
                new Keyframe(0.9f, 0.36f),
                new Keyframe(1f, 0.1f));
            VolumeVsSpeed.preWrapMode = WrapMode.Clamp;
            VolumeVsSpeed.postWrapMode = WrapMode.Clamp;
            for (int i = 0; i < VolumeVsSpeed.length; i++)
                VolumeVsSpeed.SmoothTangents(i, 0.3f);
        }

        VolumeVsSpeedSmoothTime = Mathf.Max(0.02f, VolumeVsSpeedSmoothTime);
        HoverVolumeScale = Mathf.Clamp(HoverVolumeScale, 0f, SpeedZoneVolumeScaleMax);
        CruiseVolumeScale = Mathf.Clamp(CruiseVolumeScale, 0f, SpeedZoneVolumeScaleMax);
        MaxSpeedVolumeScale = Mathf.Clamp(MaxSpeedVolumeScale, 0f, SpeedZoneVolumeScaleMax);
        DualLayerPitchMultiplier = Mathf.Max(1.0001f, DualLayerPitchMultiplier);
        BuiltinLowPassCutoffHz = Mathf.Clamp(BuiltinLowPassCutoffHz, 10f, 22000f);
        BuiltinLowPassQ = Mathf.Clamp(BuiltinLowPassQ, 1f, 10f);
        MaxLowPassCutoffHz = Mathf.Clamp(MaxLowPassCutoffHz, 1000f, 22000f);
    }

    /// <summary>
    /// Smooth blend: hover scale at speed≈0, cruise at mid, max at speed≈1.
    /// </summary>
    public float EvaluateSpeedZoneVolumeScale(float speed01)
    {
        return EvaluateSpeedZoneVolumeScale(speed01, HoverVolumeScale, CruiseVolumeScale, MaxSpeedVolumeScale);
    }

    public static float EvaluateSpeedZoneVolumeScale(float speed01, float hover, float cruise, float maxSpd)
    {
        float t = Mathf.Clamp01(speed01);
        float Smooth01(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        if (t <= 0.5f)
            return Mathf.Lerp(hover, cruise, Smooth01(t / 0.5f));
        return Mathf.Lerp(cruise, maxSpd, Smooth01((t - 0.5f) / 0.5f));
    }
}
