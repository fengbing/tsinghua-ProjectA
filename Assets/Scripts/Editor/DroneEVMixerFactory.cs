#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Builds a drone "EV-style" mixer bus: gentle high-pass (removes mud) + low-pass (less buzz),
/// exposes low-pass cutoff for <see cref="DroneAudioController"/> pitch mapping.
/// Uses UnityEditor.Audio internals via reflection (same approach as Unity's own mixer creation).
/// </summary>
public static class DroneEVMixerFactory
{
    public const string MixerAssetPath = "Assets/audio/Mixers/DroneEVMixer.mixer";
    public const string DroneBusName = "DroneEV";
    public const string ExposedLowPassParamName = "DroneEV_LP_Cutoff";

    [MenuItem("Tools/Drone Audio/Create EV Mixer (soft motor bus)")]
    public static void CreateEvMixer()
    {
        EnsureFolder("Assets/audio");
        EnsureFolder("Assets/audio/Mixers");

        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath) != null)
        {
            if (!EditorUtility.DisplayDialog("Drone EV Mixer", "Overwrite existing DroneEVMixer.mixer?", "Overwrite", "Cancel"))
                return;
            AssetDatabase.DeleteAsset(MixerAssetPath);
            AssetDatabase.Refresh();
        }

        Assembly editorAsm = typeof(Editor).Assembly;
        Type controllerType = editorAsm.GetType("UnityEditor.Audio.AudioMixerController");
        Type groupType = editorAsm.GetType("UnityEditor.Audio.AudioMixerGroupController");
        Type effectType = editorAsm.GetType("UnityEditor.Audio.AudioMixerEffectController");
        Type snapshotType = editorAsm.GetType("UnityEditor.Audio.AudioMixerSnapshotController");
        Type pathType = editorAsm.GetType("UnityEditor.Audio.AudioEffectParameterPath");

        if (controllerType == null || groupType == null || effectType == null || snapshotType == null || pathType == null)
        {
            Debug.LogError("[DroneEVMixerFactory] UnityEditor.Audio types missing — Unity version mismatch?");
            return;
        }

        MethodInfo createMixer = controllerType.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.Static);
        object controller = createMixer?.Invoke(null, new object[] { MixerAssetPath });
        var mixer = controller as AudioMixer;
        if (mixer == null)
        {
            Debug.LogError("[DroneEVMixerFactory] Failed to create mixer asset.");
            return;
        }

        object master = controllerType.GetProperty("masterGroup")?.GetValue(controller);
        MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", new[] { typeof(string), typeof(bool) });
        object droneGroup = createGroup.Invoke(controller, new object[] { DroneBusName, true });
        controllerType.GetMethod("AddChildToParent")?.Invoke(controller, new object[] { droneGroup, master });

        object highpass = Activator.CreateInstance(effectType, "Highpass Simple");
        object lowpass = Activator.CreateInstance(effectType, "Lowpass Simple");
        effectType.GetMethod("PreallocateGUIDs")?.Invoke(highpass, null);
        effectType.GetMethod("PreallocateGUIDs")?.Invoke(lowpass, null);
        AssetDatabase.AddObjectToAsset((UnityEngine.Object)highpass, mixer);
        AssetDatabase.AddObjectToAsset((UnityEngine.Object)lowpass, mixer);

        MethodInfo insert = groupType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m =>
            {
                if (m.Name != "InsertEffect")
                    return false;
                ParameterInfo[] p = m.GetParameters();
                return p.Length == 2 && p[1].ParameterType == typeof(int);
            });
        if (insert == null)
        {
            Debug.LogError("[DroneEVMixerFactory] InsertEffect not found.");
            return;
        }

        insert.Invoke(droneGroup, new[] { highpass, 1 });
        insert.Invoke(droneGroup, new[] { lowpass, 2 });

        object snapshot = controllerType.GetProperty("startSnapshot")?.GetValue(controller);
        MethodInfo getGuidForParam = effectType.GetMethod("GetGUIDForParameter", new[] { typeof(string) });
        MethodInfo setSnapshotValue = FindSnapshotSetValue(snapshotType);

        if (getGuidForParam == null || setSnapshotValue == null || snapshot == null)
        {
            Debug.LogError("[DroneEVMixerFactory] Could not resolve snapshot / GUID APIs.");
            return;
        }

        object hpGuid = getGuidForParam.Invoke(highpass, new object[] { "Cutoff freq" });
        object lpGuid = getGuidForParam.Invoke(lowpass, new object[] { "Cutoff freq" });
        setSnapshotValue.Invoke(snapshot, new[] { hpGuid, 95f });
        setSnapshotValue.Invoke(snapshot, new[] { lpGuid, 4800f });

        object lpCutoffGuid = lpGuid;
        object pathObj = Activator.CreateInstance(pathType, droneGroup, lowpass, lpCutoffGuid);
        controllerType.GetMethod("AddExposedParameter", BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(controller, new[] { pathObj });

        var so = new SerializedObject(mixer);
        SerializedProperty exp = so.FindProperty("exposedParameters");
        if (exp != null && exp.isArray && exp.arraySize > 0)
        {
            exp.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue = ExposedLowPassParamName;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = mixer;
        Debug.Log($"[DroneEVMixer] OK: {MixerAssetPath}. Bus '{DroneBusName}', exposed '{ExposedLowPassParamName}'. Run Setup on drone to wire AudioSources.");
    }

    static MethodInfo FindSnapshotSetValue(Type snapshotType)
    {
        foreach (MethodInfo m in snapshotType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.Name != "SetValue" || m.GetParameters().Length != 2)
                continue;
            ParameterInfo[] p = m.GetParameters();
            if (p[1].ParameterType == typeof(float))
                return m;
        }
        return null;
    }

    public static AudioMixerGroup TryFindDroneBus()
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
        if (mixer == null)
            return null;
        AudioMixerGroup[] g = mixer.FindMatchingGroups(DroneBusName);
        return g != null && g.Length > 0 ? g[0] : null;
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
