#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public static class DroneAudioSetupEditor
{
    const string DefaultProfilePath = "Assets/audio/Profiles/DroneAudioProfile_Default.asset";
    const string YsAudioPath = "Assets/ys.wav";
    const string Ys3AudioPath = "Assets/ys3.wav";

    [MenuItem("Tools/Drone Audio/Create Default Profile (ys.wav)")]
    public static void CreateDefaultProfile()
    {
        EnsureFolder("Assets/audio");
        EnsureFolder("Assets/audio/Profiles");

        DroneAudioProfile profile = AssetDatabase.LoadAssetAtPath<DroneAudioProfile>(DefaultProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<DroneAudioProfile>();
            AssetDatabase.CreateAsset(profile, DefaultProfilePath);
        }

        profile.BaseLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(YsAudioPath);
        profile.ForceLoop = true;
        profile.MinPitch = 0.76f;
        profile.MaxPitch = 1.16f;
        profile.MinVolume = 0.5f;
        profile.MaxVolume = 0.95f;
        profile.ThrustPitchBoost = 0.035f;
        profile.ThrustBoostAttackTime = 0.06f;
        profile.ThrustBoostReleaseTime = 0.14f;
        profile.PitchSmoothTime = 0.14f;
        profile.VolumeSmoothTime = 0.14f;
        profile.PanSmoothTime = 0.1f;
        profile.LiftVolumeBonus = 0.1f;
        profile.LiftPitchBonus = 0.025f;
        profile.DropVolumeMultiplier = 0.75f;
        profile.PanDepth = 0.14f;

        profile.DualLayerBlend = true;
        profile.DualLayerPitchMultiplier = 1.005f;
        profile.DualLayerVolumeScalePerSource = 0.5f;
        profile.PerlinPitchAmplitude = 0.0035f;
        profile.PerlinPitchFrequencyHz = 2.4f;
        profile.EnableBuiltinLowPass = false;
        profile.LowPassCutoffParam = DroneEVMixerFactory.ExposedLowPassParamName;
        profile.LowPassCutoffAtMinPitch = 5800f;
        profile.LowPassCutoffAtMaxPitch = 2200f;
        profile.MaxLowPassCutoffHz = 6200f;
        profile.LowPassSmoothTime = 0.2f;
        profile.TurnEffectSpeedThreshold = 0.52f;
        profile.HighSpeedTurnPanExtra = 0.58f;
        profile.YawRateForFullTurnEffect = 2f;
        profile.HighSpeedTurnPitchExtra = 0.032f;
        profile.VolumeVsSpeedSmoothTime = 0.22f;
        profile.HoverVolumeScale = 0.45f;
        profile.CruiseVolumeScale = 1.1f;
        profile.MaxSpeedVolumeScale = 0.45f;
        if (profile.VolumeVsPitch == null || profile.VolumeVsPitch.length == 0)
        {
            profile.VolumeVsPitch = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0.52f));
            profile.VolumeVsPitch.preWrapMode = WrapMode.Clamp;
            profile.VolumeVsPitch.postWrapMode = WrapMode.Clamp;
        }

        if (profile.VolumeVsSpeed == null || profile.VolumeVsSpeed.length == 0)
        {
            profile.VolumeVsSpeed = new AnimationCurve(
                new Keyframe(0f, 0.1f),
                new Keyframe(0.1f, 0.38f),
                new Keyframe(0.5f, 1.12f),
                new Keyframe(0.9f, 0.36f),
                new Keyframe(1f, 0.1f));
            profile.VolumeVsSpeed.preWrapMode = WrapMode.Clamp;
            profile.VolumeVsSpeed.postWrapMode = WrapMode.Clamp;
            for (int i = 0; i < profile.VolumeVsSpeed.length; i++)
                profile.VolumeVsSpeed.SmoothTangents(i, 0.3f);
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = profile;
        Debug.Log($"[DroneAudioSetup] Default profile ready: {DefaultProfilePath}");
    }

    [MenuItem("Tools/Drone Audio/Setup Selected Drone Audio Controller")]
    public static void SetupSelectedDroneAudioController()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Drone Audio Setup", "Please select a drone GameObject in Hierarchy first.", "OK");
            return;
        }

        EnsureEvMixerExistsForSetup();

        var controller = go.GetComponent<DroneAudioController>();
        if (controller == null)
            controller = Undo.AddComponent<DroneAudioController>(go);

        var source = go.GetComponent<AudioSource>();
        if (source == null)
            source = Undo.AddComponent<AudioSource>(go);

        var rb = go.GetComponent<Rigidbody>();
        var plane = go.GetComponent<PlaneController>();

        DroneAudioProfile profile = AssetDatabase.LoadAssetAtPath<DroneAudioProfile>(DefaultProfilePath);
        if (profile == null)
        {
            CreateDefaultProfile();
            profile = AssetDatabase.LoadAssetAtPath<DroneAudioProfile>(DefaultProfilePath);
        }

        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(DroneEVMixerFactory.MixerAssetPath);
        AudioMixerGroup bus = DroneEVMixerFactory.TryFindDroneBus();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("audioSource").objectReferenceValue = source;
        so.FindProperty("targetRigidbody").objectReferenceValue = rb;
        so.FindProperty("planeController").objectReferenceValue = plane;
        so.FindProperty("audioMixer").objectReferenceValue = mixer;
        so.FindProperty("droneMixerGroup").objectReferenceValue = bus;
        so.FindProperty("profile").objectReferenceValue = profile;
        so.FindProperty("maxSpeed").floatValue = 16f;
        so.FindProperty("autoPlayOnStart").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.clip = profile != null ? profile.BaseLoopClip : null;
        source.outputAudioMixerGroup = bus;

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(go);

        Debug.Log($"[DroneAudioSetup] Setup complete on {go.name}. Body loop → DroneEV bus; mixer LPF param '{DroneEVMixerFactory.ExposedLowPassParamName}'.");
    }

    [MenuItem("Tools/Drone Audio/Add Thrust Motor Audio (ys3)")]
    public static void AddThrustMotorYs3()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Drone Audio Setup", "Please select the drone GameObject first.", "OK");
            return;
        }

        EnsureEvMixerExistsForSetup();

        var thrust = go.GetComponent<DroneThrustDualTrackAudio>();
        if (thrust == null)
            thrust = Undo.AddComponent<DroneThrustDualTrackAudio>(go);

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Ys3AudioPath);
        AudioMixerGroup bus = DroneEVMixerFactory.TryFindDroneBus();

        SerializedObject so = new SerializedObject(thrust);
        if (go.GetComponent<PlaneController>() != null)
            so.FindProperty("planeController").objectReferenceValue = go.GetComponent<PlaneController>();
        so.FindProperty("motorClip").objectReferenceValue = clip;
        so.FindProperty("outputMixerGroup").objectReferenceValue = bus;
        so.FindProperty("speedReferenceBody").objectReferenceValue = go.GetComponent<Rigidbody>();
        so.ApplyModifiedPropertiesWithoutUndo();

        thrust.EditorApplyClipsAndConfigure();

        EditorUtility.SetDirty(thrust);
        Debug.Log("[DroneAudioSetup] Thrust motor (ys3) added; routed to DroneEV bus when mixer exists.");
    }

    static void EnsureEvMixerExistsForSetup()
    {
        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(DroneEVMixerFactory.MixerAssetPath) != null)
            return;
        if (EditorUtility.DisplayDialog(
                "Drone EV Mixer",
                "DroneEVMixer.mixer not found. Create it now? (High-pass + low-pass EV-style bus)",
                "Create",
                "Skip"))
        {
            DroneEVMixerFactory.CreateEvMixer();
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int idx = path.LastIndexOf('/');
        string parent = idx > 0 ? path.Substring(0, idx) : "Assets";
        string leaf = idx > 0 ? path.Substring(idx + 1) : path;
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
