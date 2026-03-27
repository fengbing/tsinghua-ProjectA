using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MinimapSceneAutoSetupWindow : EditorWindow
{
    private const string DefaultConfigDir = "Assets/MapSystem";
    private const string DefaultConfigPath = DefaultConfigDir + "/MinimapConfig.asset";

    private Sprite mapSprite;
    private Sprite circularMaskSprite;
    private Sprite playerIconSprite;
    private Transform playerTransform;
    private Transform[] objectiveTargets = new Transform[3];

    [MenuItem("Tools/Map System/Auto Setup Minimap")]
    public static void OpenWindow()
    {
        GetWindow<MinimapSceneAutoSetupWindow>("Minimap Auto Setup");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Scene Auto Wiring", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "一键创建 MinimapConfig 资源（Assets/MapSystem/MinimapConfig.asset）并挂到 MinimapUiController；未运行本工具时，Controller 上需手动拖入 Config，否则会使用运行时临时默认且无地图贴图。",
            MessageType.Info);

        mapSprite = (Sprite)EditorGUILayout.ObjectField("Map Sprite", mapSprite, typeof(Sprite), false);
        circularMaskSprite = (Sprite)EditorGUILayout.ObjectField("Circular Mask Sprite", circularMaskSprite, typeof(Sprite), false);
        playerIconSprite = (Sprite)EditorGUILayout.ObjectField("Player Icon Sprite", playerIconSprite, typeof(Sprite), false);
        playerTransform = (Transform)EditorGUILayout.ObjectField("Player Transform (Optional)", playerTransform, typeof(Transform), true);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Objective Targets (Optional)");
        for (int i = 0; i < objectiveTargets.Length; i++)
        {
            objectiveTargets[i] = (Transform)EditorGUILayout.ObjectField($"Target {i + 1}", objectiveTargets[i], typeof(Transform), true);
        }

        EditorGUILayout.Space(10f);
        if (GUILayout.Button("Auto Setup In Active Scene", GUILayout.Height(32f)))
        {
            SetupInActiveScene();
        }
    }

    private void SetupInActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[MinimapAutoSetup] Active scene is invalid or not loaded.");
            return;
        }

        EnsureDirectory(DefaultConfigDir);
        MinimapConfig config = LoadOrCreateConfig();
        UpdateConfig(config);

        GameObject root = FindOrCreate("MapSystem");
        MissionObjectiveProvider provider = root.GetComponent<MissionObjectiveProvider>();
        if (provider == null) provider = Undo.AddComponent<MissionObjectiveProvider>(root);
        MinimapUiController controller = root.GetComponent<MinimapUiController>();
        if (controller == null) controller = Undo.AddComponent<MinimapUiController>(root);
        MapBoundsGizmo gizmo = root.GetComponent<MapBoundsGizmo>();
        if (gizmo == null) gizmo = Undo.AddComponent<MapBoundsGizmo>(root);

        Transform resolvedPlayer = playerTransform != null ? playerTransform : AutoFindPlayer();
        ApplyProviderObjectives(provider);
        ApplyControllerBindings(controller, config, resolvedPlayer, provider, playerIconSprite);
        ApplyGizmoBinding(gizmo, config);

        EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(provider);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(gizmo);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[MinimapAutoSetup] Setup complete. Verify positions/bounds in Play Mode.");
    }

    private static void EnsureDirectory(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static MinimapConfig LoadOrCreateConfig()
    {
        MinimapConfig config = AssetDatabase.LoadAssetAtPath<MinimapConfig>(DefaultConfigPath);
        if (config != null) return config;
        config = CreateInstance<MinimapConfig>();
        AssetDatabase.CreateAsset(config, DefaultConfigPath);
        AssetDatabase.ImportAsset(DefaultConfigPath);
        return config;
    }

    private void UpdateConfig(MinimapConfig config)
    {
        SerializedObject so = new SerializedObject(config);
        if (mapSprite != null) so.FindProperty("mapSprite").objectReferenceValue = mapSprite;
        if (circularMaskSprite != null) so.FindProperty("circularMaskSprite").objectReferenceValue = circularMaskSprite;

        Bounds bounds = EstimateWorldBounds();
        so.FindProperty("worldMin").vector2Value = new Vector2(bounds.min.x, bounds.min.z);
        so.FindProperty("worldMax").vector2Value = new Vector2(bounds.max.x, bounds.max.z);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Bounds EstimateWorldBounds()
    {
        Renderer[] renderers = FindObjectsOfType<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, new Vector3(500f, 1f, 500f));
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static Transform AutoFindPlayer()
    {
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null) return tagged.transform;
        PlaneController plane = FindObjectOfType<PlaneController>();
        if (plane != null) return plane.transform;
        return null;
    }

    private void ApplyProviderObjectives(MissionObjectiveProvider provider)
    {
        SerializedObject so = new SerializedObject(provider);
        SerializedProperty objectives = so.FindProperty("objectives");
        objectives.arraySize = 0;
        int index = 0;
        for (int i = 0; i < objectiveTargets.Length; i++)
        {
            if (objectiveTargets[i] == null) continue;
            objectives.InsertArrayElementAtIndex(index);
            SerializedProperty element = objectives.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("id").stringValue = $"objective-{index + 1}";
            element.FindPropertyRelative("target").objectReferenceValue = objectiveTargets[i];
            element.FindPropertyRelative("state").enumValueIndex = (int)MissionObjectiveState.Active;
            index++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        provider.NotifyObjectivesChanged();
    }

    private static void ApplyControllerBindings(MinimapUiController controller, MinimapConfig config, Transform player, MissionObjectiveProvider provider, Sprite playerSpr)
    {
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("config").objectReferenceValue = config;
        so.FindProperty("playerTransform").objectReferenceValue = player;
        so.FindProperty("objectiveProvider").objectReferenceValue = provider;
        if (playerSpr != null) so.FindProperty("playerIconSprite").objectReferenceValue = playerSpr;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyGizmoBinding(MapBoundsGizmo gizmo, MinimapConfig config)
    {
        SerializedObject so = new SerializedObject(gizmo);
        so.FindProperty("config").objectReferenceValue = config;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindOrCreate(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) return go;
        go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }
}
