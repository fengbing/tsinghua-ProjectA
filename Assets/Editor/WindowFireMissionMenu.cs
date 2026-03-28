#if UNITY_EDITOR
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
}
#endif
