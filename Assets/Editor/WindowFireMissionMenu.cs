#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor helpers to scaffold window-fire mission HUD and anchor (see OpenSpec change high-rise-fire-window-drone-flow).
/// </summary>
public static class WindowFireMissionMenu
{
    const string MenuRoot = "Window Fire Mission/";

    const string FacadeRescueControllerScriptPath =
        "Assets/Scripts/WindowFireMission/FacadeRescueMiniGameController.cs";
    const string WindowFireFacadeTriggerScriptPath =
        "Assets/Scripts/WindowFireMission/WindowFireFacadeMinigameTrigger.cs";

    /// <summary>在 Assets 下按「定义的主类名」查找 MonoScript（多策略；不依赖固定文件夹路径）。</summary>
    static MonoScript FindMonoScriptByClassName(string className)
    {
        if (string.IsNullOrEmpty(className))
            return null;

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var filter in new[] { $"{className} t:MonoScript", className })
        {
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                if (!seen.Add(guid))
                    continue;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms == null)
                    continue;
                var cls = ms.GetClass();
                if (cls != null && string.Equals(cls.Name, className, StringComparison.Ordinal))
                    return ms;
            }
        }

        foreach (var ms in Resources.FindObjectsOfTypeAll<MonoScript>())
        {
            if (ms == null)
                continue;
            var path = AssetDatabase.GetAssetPath(ms);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                continue;
            var cls = ms.GetClass();
            if (cls != null && string.Equals(cls.Name, className, StringComparison.Ordinal))
                return ms;
        }

        return null;
    }

    /// <summary>按文件名「ClassName.cs」查找脚本（用于 GetClass 为空时的错误提示）。</summary>
    static MonoScript FindMonoScriptLooseByFileName(string className)
    {
        if (string.IsNullOrEmpty(className))
            return null;

        foreach (var guid in AssetDatabase.FindAssets(className))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(className + ".cs", StringComparison.OrdinalIgnoreCase))
                continue;
            return AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        }

        return null;
    }

    /// <summary>解析运行时的 Mono 类型；Editor 不直接引用该类名。优先 MonoScript，再回退到程序集扫描。</summary>
    static Type ResolveRuntimeMonoType(string shortName, string monoScriptAssetPath)
    {
        if (!string.IsNullOrEmpty(monoScriptAssetPath))
        {
            var atPath = AssetDatabase.LoadAssetAtPath<MonoScript>(monoScriptAssetPath);
            if (atPath != null)
            {
                var c0 = atPath.GetClass();
                if (c0 != null)
                    return c0;
            }
        }

        var byName = FindMonoScriptByClassName(shortName);
        if (byName != null)
        {
            var c1 = byName.GetClass();
            if (c1 != null)
                return c1;
        }

        var fromMissionAsm = typeof(WindowFireMission).Assembly.GetType(shortName);
        if (fromMissionAsm != null)
            return fromMissionAsm;

        var dotted = Type.GetType(shortName + ", Assembly-CSharp");
        if (dotted != null)
            return dotted;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name;
            if (!string.Equals(name, "Assembly-CSharp", StringComparison.Ordinal) &&
                !(name != null && name.StartsWith("Assembly-CSharp.", StringComparison.Ordinal)))
                continue;

            try
            {
                var t = asm.GetType(shortName);
                if (t != null)
                    return t;
            }
            catch
            {
                // ignored
            }
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types = null;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            catch
            {
                continue;
            }

            if (types == null)
                continue;
            foreach (var t in types)
            {
                if (t != null && t.Name == shortName)
                    return t;
            }
        }

        return null;
    }

    static Type FacadeRescueControllerRuntimeType =>
        ResolveRuntimeMonoType("FacadeRescueMiniGameController", FacadeRescueControllerScriptPath);

    static Type WindowFireFacadeTriggerRuntimeType =>
        ResolveRuntimeMonoType("WindowFireFacadeMinigameTrigger", WindowFireFacadeTriggerScriptPath);

    [MenuItem(MenuRoot + "Diagnose Facade / WindowFire scripts (log to Console)")]
    static void DiagnoseFacadeScriptsToConsole()
    {
        var names = new[]
        {
            "FacadeRescueMiniGameController",
            "WindowFireFacadeMinigameTrigger",
            "IFacadeRescueMinigameEntry",
            "FacadeRescueSessionState",
            "WindowFireMission",
        };
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[WindowFireMissionMenu] Script scan (Assets/ only):");
        foreach (var n in names)
        {
            sb.AppendLine($"--- {n} ---");
            var hits = 0;
            foreach (var guid in AssetDatabase.FindAssets($"{n} t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                var cls = ms != null ? ms.GetClass() : null;
                sb.AppendLine($"  path={path}");
                sb.AppendLine($"  GetClass={(cls != null ? cls.FullName : "null (编译错误或未编译)")}");
                hits++;
            }

            if (hits == 0)
                sb.AppendLine("  (FindAssets 无 t:MonoScript 命中；可能文件不在 Assets 或尚未导入)");
        }

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Diagnose", "已输出到 Console（Window 标签）。若 FacadeRescueMiniGameController 无路径，说明工程里确实没有该脚本。", "OK");
    }

    static string BuildMissingRuntimeTypeMessage(string shortName, string scriptPath)
    {
        var msAtPath = !string.IsNullOrEmpty(scriptPath)
            ? AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath)
            : null;
        var ms = msAtPath != null ? msAtPath : FindMonoScriptByClassName(shortName);
        if (ms == null)
            ms = FindMonoScriptLooseByFileName(shortName);

        if (ms == null)
        {
            return $"未找到定义类「{shortName}」的 .cs 文件。\n" +
                   $"默认期望路径：{scriptPath}\n\n" +
                   "请先执行菜单：Window Fire Mission → Diagnose Facade / WindowFire scripts (log to Console)，\n" +
                   "查看 Unity 工程里是否真的存在该脚本路径。\n\n" +
                   "若 Console 里也没有路径：请从版本库/本机 Cursor 工程把脚本拷入当前 Unity 打开的 **同一项目** 的 Assets 下，再等待编译完成。";
        }

        var resolvedPath = AssetDatabase.GetAssetPath(ms);
        if (ms.GetClass() == null)
        {
            return $"脚本已存在但类型未生成（通常表示该脚本或依赖有编译错误）：\n{resolvedPath}\n\n" +
                   "请打开 Console，修复第一条红色编译错误；并确认能通过编译：\n" +
                   "• IFacadeRescueMinigameEntry.cs\n" +
                   "• FacadeRescueSessionState.cs\n" +
                   "• WindowFireMission.cs\n" +
                   "然后等待 Unity 重新编译完成再执行菜单。";
        }

        return $"未能解析类型 {shortName}（脚本：{resolvedPath}）。请尝试：Assets → Reimport All，或重启 Unity。";
    }

    struct FacadeWindowUiRefs
    {
        public Button windowButton;
        public Button probeButton;
        public Button revealChoicesButton;
        public GameObject choicesPanel;
        public Button slideLeftButton;
        public Button elevatorButton;
        public Button slideRightButton;
    }

    [MenuItem(MenuRoot + "Create Dual-Prompt HUD")]
    static void CreateDualPromptHud()
    {
        var canvasGo = new GameObject(
            "WindowFirePromptCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var panel = new GameObject("PromptPanel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.22f);
        panelRt.anchorMax = new Vector2(0.5f, 0.22f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(700, 96);
        var hlg = panel.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 14f;
        hlg.padding = new RectOffset(12, 12, 8, 8);
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        var keyBlock = new GameObject("KeyBlock", typeof(RectTransform), typeof(LayoutElement));
        keyBlock.transform.SetParent(panel.transform, false);
        var keyLe = keyBlock.GetComponent<LayoutElement>();
        keyLe.preferredWidth = 76f;
        keyLe.preferredHeight = 84f;
        keyLe.minWidth = 64f;
        var keyBg = AddFullStretchImage(keyBlock.transform, "KeyBackground", new Color(0f, 0f, 0f, 0.78f));
        var keyTmp = AddFullStretchTmp(
            keyBlock.transform,
            "KeyHintText",
            "F",
            40f,
            TextAlignmentOptions.Center);

        var instrBlock = new GameObject("InstructionBlock", typeof(RectTransform), typeof(LayoutElement));
        instrBlock.transform.SetParent(panel.transform, false);
        var instrLe = instrBlock.GetComponent<LayoutElement>();
        instrLe.flexibleWidth = 1f;
        instrLe.minHeight = 84f;
        var instrBg = AddFullStretchImage(instrBlock.transform, "InstructionBackground", new Color(0f, 0f, 0f, 0.78f));
        var instrTmp = AddFullStretchTmp(
            instrBlock.transform,
            "InstructionText",
            "快打开喷淋系统灭火！",
            28f,
            TextAlignmentOptions.MidlineLeft);

        var hud = canvasGo.AddComponent<WindowFireDualPromptHud>();
        var so = new SerializedObject(hud);
        so.FindProperty("panelRoot").objectReferenceValue = panel;
        so.FindProperty("layoutRootRect").objectReferenceValue = panelRt;
        so.FindProperty("keyBlockRect").objectReferenceValue = keyBlock.GetComponent<RectTransform>();
        so.FindProperty("instructionBlockRect").objectReferenceValue = instrBlock.GetComponent<RectTransform>();
        so.FindProperty("rowLayoutGroup").objectReferenceValue = hlg;
        so.FindProperty("rowSpacing").floatValue = 14f;
        so.FindProperty("keyHintBackground").objectReferenceValue = keyBg;
        so.FindProperty("keyHintText").objectReferenceValue = keyTmp;
        so.FindProperty("instructionBackground").objectReferenceValue = instrBg;
        so.FindProperty("instructionText").objectReferenceValue = instrTmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        Selection.activeGameObject = canvasGo;
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Dual-Prompt HUD");
    }

    static Image AddFullStretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI AddFullStretchTmp(
        Transform parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 6f);
        rt.offsetMax = new Vector2(-10f, -6f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    [MenuItem(MenuRoot + "Create Window Anchor (trigger + mission)")]
    static void CreateWindowAnchor()
    {
        var go = new GameObject("WindowFireMissionAnchor");
        var mission = go.AddComponent<WindowFireMission>();

        var windowZoneGo = new GameObject("WindowApproachZone");
        windowZoneGo.transform.SetParent(go.transform, false);
        windowZoneGo.transform.localPosition = Vector3.zero;
        var windowBox = windowZoneGo.AddComponent<BoxCollider>();
        windowBox.isTrigger = true;
        windowBox.size = new Vector3(12f, 7f, 12f);
        var windowProximity = windowZoneGo.AddComponent<WindowFireProximityZone>();
        var soWindowZ = new SerializedObject(windowProximity);
        soWindowZ.FindProperty("kind").enumValueIndex = (int)WindowFireProximityZone.ZoneKind.WindowApproach;
        soWindowZ.FindProperty("mission").objectReferenceValue = mission;
        soWindowZ.ApplyModifiedPropertiesWithoutUndo();

        var smokeZoneGo = new GameObject("SmokeApproachZone");
        smokeZoneGo.transform.SetParent(go.transform, false);
        smokeZoneGo.transform.localPosition = Vector3.zero;
        var smokeBox = smokeZoneGo.AddComponent<BoxCollider>();
        smokeBox.isTrigger = true;
        smokeBox.size = new Vector3(5f, 4f, 5f);
        var smokeProximity = smokeZoneGo.AddComponent<WindowFireProximityZone>();
        var soSmokeZ = new SerializedObject(smokeProximity);
        soSmokeZ.FindProperty("kind").enumValueIndex = (int)WindowFireProximityZone.ZoneKind.SmokeApproach;
        soSmokeZ.FindProperty("mission").objectReferenceValue = mission;
        soSmokeZ.ApplyModifiedPropertiesWithoutUndo();

        var zone2Go = new GameObject("Zone2SmokeAmbienceZone");
        zone2Go.transform.SetParent(go.transform, false);
        zone2Go.transform.localPosition = Vector3.zero;
        var zone2Box = zone2Go.AddComponent<BoxCollider>();
        zone2Box.isTrigger = true;
        zone2Box.size = new Vector3(6f, 5f, 6f);
        var zone2Prox = zone2Go.AddComponent<WindowFireProximityZone>();
        var soZ2 = new SerializedObject(zone2Prox);
        soZ2.FindProperty("kind").enumValueIndex = (int)WindowFireProximityZone.ZoneKind.Zone2SmokeAmbience;
        soZ2.FindProperty("mission").objectReferenceValue = mission;
        soZ2.ApplyModifiedPropertiesWithoutUndo();

        var fireChild = new GameObject("FireVFX");
        fireChild.transform.SetParent(go.transform, false);
        fireChild.transform.localPosition = Vector3.zero;
        var firePs = fireChild.AddComponent<ParticleSystem>();
        var fireMain = firePs.main;
        fireMain.startLifetime = 1.2f;
        fireMain.startSpeed = 2f;
        fireMain.startSize = 0.35f;
        fireMain.maxParticles = 80;
        fireMain.simulationSpace = ParticleSystemSimulationSpace.World;
        var fireEm = firePs.emission;
        fireEm.rateOverTime = 28f;
        var fireShape = firePs.shape;
        fireShape.shapeType = ParticleSystemShapeType.Sphere;
        fireShape.radius = 0.15f;
        var fireCol = firePs.colorOverLifetime;
        fireCol.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.yellow, 0f), new GradientColorKey(Color.red, 0.5f), new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fireCol.color = new ParticleSystem.MinMaxGradient(g);

        var smokeChild = new GameObject("SmokeVFX");
        smokeChild.transform.SetParent(go.transform, false);
        smokeChild.transform.localPosition = Vector3.zero;
        var smokePs = smokeChild.AddComponent<ParticleSystem>();
        var smMain = smokePs.main;
        smMain.startLifetime = 3f;
        smMain.startSpeed = 0.8f;
        smMain.startSize = 1.2f;
        smMain.maxParticles = 60;
        smMain.simulationSpace = ParticleSystemSimulationSpace.World;
        smMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.35f, 0.35f, 0.5f));
        var smEm = smokePs.emission;
        smEm.rateOverTime = 12f;
        var smShape = smokePs.shape;
        smShape.shapeType = ParticleSystemShapeType.Sphere;
        smShape.radius = 0.25f;

        var so = new SerializedObject(mission);
        var fireProp = so.FindProperty("fireEffects");
        fireProp.arraySize = 1;
        fireProp.GetArrayElementAtIndex(0).objectReferenceValue = firePs;
        var smokeProp = so.FindProperty("smokeEffects");
        smokeProp.arraySize = 1;
        smokeProp.GetArrayElementAtIndex(0).objectReferenceValue = smokePs;
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = go;
        Undo.RegisterCreatedObjectUndo(go, "Create Window Anchor");
    }

    [MenuItem(MenuRoot + "Add Facade Minigame Trigger (child of selection)")]
    static void AddFacadeMinigameTrigger()
    {
        var parent = Selection.activeTransform;
        if (parent == null)
        {
            EditorUtility.DisplayDialog("Facade Minigame Trigger", "先在 Hierarchy 中选中挂有 WindowFireMission 的物体（或其子级父物体）。", "OK");
            return;
        }

        var mission = parent.GetComponentInParent<WindowFireMission>();
        if (mission == null)
        {
            EditorUtility.DisplayDialog("Facade Minigame Trigger", "父级链上未找到 WindowFireMission。", "OK");
            return;
        }

        var go = new GameObject("FacadeMinigameTrigger");
        Undo.RegisterCreatedObjectUndo(go, "Add Facade Minigame Trigger");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(10f, 6f, 10f);
        var triggerType = WindowFireFacadeTriggerRuntimeType;
        if (triggerType == null)
        {
            EditorUtility.DisplayDialog(
                "Facade Minigame Trigger",
                BuildMissingRuntimeTypeMessage("WindowFireFacadeMinigameTrigger", WindowFireFacadeTriggerScriptPath),
                "OK");
            Undo.DestroyObjectImmediate(go);
            return;
        }

        var trig = go.AddComponent(triggerType);
        var so = new SerializedObject(trig);
        so.FindProperty("mission").objectReferenceValue = mission;
        so.ApplyModifiedPropertiesWithoutUndo();
        Selection.activeGameObject = go;
    }

    [MenuItem(MenuRoot + "Create Facade Rescue Minigame Canvas (scaffold)")]
    static void CreateFacadeRescueMinigameCanvas()
    {
        var facadeType = FacadeRescueControllerRuntimeType;
        if (facadeType == null)
        {
            EditorUtility.DisplayDialog(
                "Facade Rescue",
                BuildMissingRuntimeTypeMessage("FacadeRescueMiniGameController", FacadeRescueControllerScriptPath),
                "OK");
            return;
        }

        var canvasGo = new GameObject(
            "FacadeRescueCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(AudioSource));
        canvasGo.AddComponent(facadeType);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var audio = canvasGo.GetComponent<AudioSource>();
        audio.playOnAwake = false;

        var fullscreenRoot = new GameObject("FacadeRescueFullscreenRoot", typeof(RectTransform));
        fullscreenRoot.transform.SetParent(canvasGo.transform, false);
        var fullRt = fullscreenRoot.GetComponent<RectTransform>();
        StretchFull(fullRt);

        var backdrop = new GameObject("FacadeBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backdrop.transform.SetParent(fullscreenRoot.transform, false);
        StretchFull(backdrop.GetComponent<RectTransform>());
        backdrop.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.22f, 1f);

        var elevatorGo = new GameObject("ElevatorCar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        elevatorGo.transform.SetParent(fullscreenRoot.transform, false);
        var elevRt = elevatorGo.GetComponent<RectTransform>();
        elevRt.anchorMin = elevRt.anchorMax = new Vector2(0.5f, 0.5f);
        elevRt.pivot = new Vector2(0.5f, 0.5f);
        elevRt.sizeDelta = new Vector2(140f, 220f);
        elevRt.anchoredPosition = new Vector2(420f, 0f);
        elevatorGo.GetComponent<Image>().color = new Color(0.55f, 0.75f, 0.9f, 1f);

        var portraitRow = new GameObject("PortraitRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        portraitRow.transform.SetParent(elevatorGo.transform, false);
        var prRt = portraitRow.GetComponent<RectTransform>();
        StretchTopBand(prRt, 0.72f);
        var hlg = portraitRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.padding = new RectOffset(6, 6, 4, 4);

        var p0 = AddLayoutImage(portraitRow.transform, "PortraitSlot0", new Color(0.2f, 0.2f, 0.25f, 1f));
        var p1 = AddLayoutImage(portraitRow.transform, "PortraitSlot1", new Color(0.2f, 0.2f, 0.25f, 1f));
        var p2 = AddLayoutImage(portraitRow.transform, "PortraitSlot2", new Color(0.2f, 0.2f, 0.25f, 1f));
        var le0 = p0.gameObject.AddComponent<LayoutElement>();
        le0.preferredWidth = le0.preferredHeight = 44f;
        var le1 = p1.gameObject.AddComponent<LayoutElement>();
        le1.preferredWidth = le1.preferredHeight = 44f;
        var le2 = p2.gameObject.AddComponent<LayoutElement>();
        le2.preferredWidth = le2.preferredHeight = 44f;

        float[] anchorsX = { 0.28f, 0.5f, 0.72f };
        float anchorY = 0.42f;
        var windowSlots = new FacadeWindowUiRefs[3];
        for (int i = 0; i < 3; i++)
        {
            windowSlots[i] = BuildWindowFlow(fullscreenRoot.transform, i, anchorsX[i], anchorY);
        }

        fullscreenRoot.SetActive(false);

        var ctrl = canvasGo.GetComponent(facadeType);
        var soCtrl = new SerializedObject(ctrl);
        soCtrl.FindProperty("fullscreenRoot").objectReferenceValue = fullscreenRoot;
        soCtrl.FindProperty("elevatorRect").objectReferenceValue = elevRt;
        var stops = soCtrl.FindProperty("elevatorStopAnchoredY");
        stops.arraySize = 3;
        stops.GetArrayElementAtIndex(0).floatValue = 160f;
        stops.GetArrayElementAtIndex(1).floatValue = 40f;
        stops.GetArrayElementAtIndex(2).floatValue = -80f;
        soCtrl.FindProperty("elevatorYStart").floatValue = 420f;
        soCtrl.FindProperty("elevatorYExit").floatValue = -620f;
        soCtrl.FindProperty("elevatorMoveSeconds").floatValue = 0.85f;

        var portraits = soCtrl.FindProperty("portraitSlots");
        portraits.arraySize = 3;
        portraits.GetArrayElementAtIndex(0).objectReferenceValue = p0;
        portraits.GetArrayElementAtIndex(1).objectReferenceValue = p1;
        portraits.GetArrayElementAtIndex(2).objectReferenceValue = p2;

        var wins = soCtrl.FindProperty("windows");
        wins.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var el = wins.GetArrayElementAtIndex(i);
            var s = windowSlots[i];
            el.FindPropertyRelative("windowButton").objectReferenceValue = s.windowButton;
            el.FindPropertyRelative("probeButton").objectReferenceValue = s.probeButton;
            el.FindPropertyRelative("revealChoicesButton").objectReferenceValue = s.revealChoicesButton;
            el.FindPropertyRelative("choicesPanel").objectReferenceValue = s.choicesPanel;
            el.FindPropertyRelative("slideLeftButton").objectReferenceValue = s.slideLeftButton;
            el.FindPropertyRelative("elevatorButton").objectReferenceValue = s.elevatorButton;
            el.FindPropertyRelative("slideRightButton").objectReferenceValue = s.slideRightButton;
        }

        soCtrl.FindProperty("completionVoiceSource").objectReferenceValue = audio;
        soCtrl.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = canvasGo;
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Facade Rescue Minigame Canvas");
    }

    static FacadeWindowUiRefs BuildWindowFlow(Transform root, int index, float ax, float ay)
    {
        var wb = new GameObject($"W{index}_WindowHotspot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        wb.transform.SetParent(root, false);
        var wbrt = wb.GetComponent<RectTransform>();
        wbrt.anchorMin = wbrt.anchorMax = new Vector2(ax, ay);
        wbrt.pivot = new Vector2(0.5f, 0.5f);
        wbrt.sizeDelta = new Vector2(160f, 120f);
        wbrt.anchoredPosition = Vector2.zero;
        var wimg = wb.GetComponent<Image>();
        wimg.color = new Color(1f, 1f, 1f, 0.12f);
        var wbtn = wb.GetComponent<Button>();

        var intro = BuildModal(root, $"w{index}_intro", "窗口信息", "查看详情", out var probeBtn);
        var details = BuildModal(root, $"w{index}_details", "被困人员信息", "选择逃生方式", out var revealBtn);
        var choices = BuildChoiceBar(root, $"w{index}_choices", out var bL, out var bM, out var bR);

        intro.SetActive(false);
        details.SetActive(false);
        choices.SetActive(false);
        wb.SetActive(false);

        return new FacadeWindowUiRefs
        {
            windowButton = wbtn,
            probeButton = probeBtn,
            revealChoicesButton = revealBtn,
            choicesPanel = choices,
            slideLeftButton = bL,
            elevatorButton = bM,
            slideRightButton = bR,
        };
    }

    static GameObject BuildModal(Transform root, string name, string title, string actionLabel, out Button actionBtn)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(root, false);
        StretchFull(go.GetComponent<RectTransform>());
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(go.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(520f, 280f);
        prt.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 1f);

        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(panel.transform, false);
        var tRt = titleGo.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0.62f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = new Vector2(16f, 0f);
        tRt.offsetMax = new Vector2(-16f, -12f);
        var tmp = titleGo.GetComponent<TextMeshProUGUI>();
        tmp.text = title;
        tmp.fontSize = 26f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var infoGo = new GameObject("InfoStub", typeof(RectTransform), typeof(TextMeshProUGUI));
        infoGo.transform.SetParent(panel.transform, false);
        var iRt = infoGo.GetComponent<RectTransform>();
        iRt.anchorMin = new Vector2(0f, 0.28f);
        iRt.anchorMax = new Vector2(1f, 0.62f);
        iRt.offsetMin = new Vector2(16f, 8f);
        iRt.offsetMax = new Vector2(-16f, -8f);
        var infoTmp = infoGo.GetComponent<TextMeshProUGUI>();
        infoTmp.text = "（替换为信息图 / 文案）";
        infoTmp.fontSize = 20f;
        infoTmp.alignment = TextAlignmentOptions.MidlineLeft;
        infoTmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        var btnGo = new GameObject("ActionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(panel.transform, false);
        var bRt = btnGo.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.35f, 0.06f);
        bRt.anchorMax = new Vector2(0.65f, 0.22f);
        bRt.offsetMin = bRt.offsetMax = Vector2.zero;
        btnGo.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.85f, 1f);
        actionBtn = btnGo.GetComponent<Button>();
        var btnTmpGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTmpGo.transform.SetParent(btnGo.transform, false);
        StretchFull(btnTmpGo.GetComponent<RectTransform>());
        var btnTmp = btnTmpGo.GetComponent<TextMeshProUGUI>();
        btnTmp.text = actionLabel;
        btnTmp.fontSize = 22f;
        btnTmp.alignment = TextAlignmentOptions.Center;
        btnTmp.color = Color.white;

        return go;
    }

    static GameObject BuildChoiceBar(Transform root, string name, out Button left, out Button mid, out Button right)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(root, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0.06f);
        rt.anchorMax = new Vector2(0.92f, 0.18f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(24, 24, 10, 10);
        h.spacing = 24f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        left = CreateChoiceButton(go.transform, "SlideLeft", "滑梯");
        mid = CreateChoiceButton(go.transform, "Elevator", "电梯");
        right = CreateChoiceButton(go.transform, "SlideRight", "滑梯");
        return go;
    }

    static Button CreateChoiceButton(Transform parent, string name, string label)
    {
        var bgo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        bgo.transform.SetParent(parent, false);
        bgo.GetComponent<Image>().color = new Color(0.3f, 0.55f, 0.35f, 1f);
        var le = bgo.GetComponent<LayoutElement>();
        le.preferredHeight = 64f;
        le.flexibleWidth = 1f;
        var btn = bgo.GetComponent<Button>();
        var lg = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        lg.transform.SetParent(bgo.transform, false);
        StretchFull(lg.GetComponent<RectTransform>());
        var tmp = lg.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return btn;
    }

    static Image AddLayoutImage(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void StretchTopBand(RectTransform rt, float anchorYMin)
    {
        rt.anchorMin = new Vector2(0f, anchorYMin);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(8f, 6f);
        rt.offsetMax = new Vector2(-8f, -8f);
    }
}
#endif
