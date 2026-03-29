#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>生成 Additive 小游戏占位场景并写入 Build Settings。</summary>
public static class BuiltinMiniGameSceneSetup
{
    const string ScenePath = "Assets/Scenes/BuiltinMiniGame.unity";
    const string SceneName = "BuiltinMiniGame";

    [MenuItem("Window Fire Mission/Create Builtin Mini-Game Scene (Additive)")]
    public static void CreateScene()
    {
        if (System.IO.File.Exists(ScenePath))
        {
            if (!EditorUtility.DisplayDialog(
                    "BuiltinMiniGame",
                    $"已存在 {ScenePath}，是否覆盖？",
                    "覆盖",
                    "取消"))
                return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        var canvasGo = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintGo.transform.SetParent(canvasGo.transform, false);
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0.62f);
        hintRt.anchorMax = new Vector2(0.5f, 0.62f);
        hintRt.sizeDelta = new Vector2(900f, 120f);
        var hintTmp = hintGo.GetComponent<TextMeshProUGUI>();
        hintTmp.text = "内置小游戏占位（Additive）\n完成后点击下方按钮返回主场景机位";
        hintTmp.fontSize = 32f;
        hintTmp.alignment = TextAlignmentOptions.Center;
        hintTmp.color = Color.white;
        hintTmp.raycastTarget = false;

        var btnGo = new GameObject("ReturnButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(canvasGo.transform, false);
        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.38f);
        btnRt.anchorMax = new Vector2(0.5f, 0.38f);
        btnRt.sizeDelta = new Vector2(280f, 72f);
        var btnImg = btnGo.GetComponent<Image>();
        btnImg.color = new Color(0.2f, 0.55f, 0.35f, 1f);
        btnGo.AddComponent<MiniGameReturnController>();

        var btnLabelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnLabelGo.transform.SetParent(btnGo.transform, false);
        var lblRt = btnLabelGo.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero;
        lblRt.offsetMax = Vector2.zero;
        var lbl = btnLabelGo.GetComponent<TextMeshProUGUI>();
        lbl.text = "返回主场景";
        lbl.fontSize = 28f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettingsIfMissing(ScenePath);
        EditorUtility.DisplayDialog(
            "BuiltinMiniGame",
            $"已保存：{ScenePath}\n已确保加入 Build Settings。\n主场景用 MiniGameLauncher 或代码 Begin(\"{SceneName}\") 加载。",
            "确定");
    }

    static void AddToBuildSettingsIfMissing(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path))
            return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    const string GameplayRootName = "MiniGame_GameplayRoot";

    /// <summary>在已存在的 BuiltinMiniGame 场景中生成 2D 玩法节点（不覆盖整场景）。可重复运行：已存在 GameplayRoot 则跳过。</summary>
    [MenuItem("Window Fire Mission/Scaffold Builtin Mini-Game 2D Content")]
    public static void Scaffold2DMinigame()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog(
                "BuiltinMiniGame",
                $"未找到 {ScenePath}，请先运行「Create Builtin Mini-Game Scene」。",
                "确定");
            return;
        }

        if (SceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        bool alreadyHas = GameObject.Find(GameplayRootName) != null
                          || Object.FindFirstObjectByType<MiniGameSession>() != null;
        if (alreadyHas
            && !EditorUtility.DisplayDialog(
                "BuiltinMiniGame",
                "场景中似乎已有 MiniGame_GameplayRoot / MiniGameSession，是否仍要再创建一份？",
                "再创建",
                "取消"))
            return;

        var bg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/转场.png");
        var cursorSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/guangbiao.png");
        var recvSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/交互1.png");
        var padSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/交互2.png");
        var droneSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Gemini_Generated_Image_uu8uqkuu8uqkuu8u (1).png");
        var clickWav = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/xiangji.wav");

        var camGo = GameObject.FindWithTag("MainCamera");
        if (camGo == null)
            camGo = GameObject.Find("Main Camera");
        Camera cam = camGo != null ? camGo.GetComponent<Camera>() : null;
        if (camGo != null && camGo.GetComponent<MiniGameViewController>() == null)
            Undo.AddComponent<MiniGameViewController>(camGo);

        var root = new GameObject(GameplayRootName);
        Undo.RegisterCreatedObjectUndo(root, "MiniGame GameplayRoot");

        var session = root.AddComponent<MiniGameSession>();
        var router = root.AddComponent<MiniGameClickRouter>();
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        var droneGo = new GameObject("Drone");
        Undo.RegisterCreatedObjectUndo(droneGo, "Drone");
        droneGo.transform.SetParent(root.transform);
        droneGo.transform.position = new Vector3(0f, 0f, 0f);
        var droneSr = droneGo.AddComponent<SpriteRenderer>();
        if (droneSpr != null)
            droneSr.sprite = droneSpr;
        droneSr.sortingOrder = 5;
        var droneComp = droneGo.AddComponent<MiniGameDrone>();

        // Background
        var bgGo = new GameObject("Background");
        Undo.RegisterCreatedObjectUndo(bgGo, "Background");
        bgGo.transform.SetParent(root.transform);
        bgGo.transform.localPosition = Vector3.zero;
        var bgSr = bgGo.AddComponent<SpriteRenderer>();
        if (bg != null)
        {
            bgSr.sprite = bg;
            float w = bg.bounds.size.x;
            float h = bg.bounds.size.y;
            if (w > 0.01f && h > 0.01f)
                bgGo.transform.localScale = new Vector3(40f / w, 40f / h, 1f);
        }

        bgSr.sortingOrder = 0;

        // Full playfield collider (behind entities)
        var hitGo = new GameObject("PlayfieldHit");
        Undo.RegisterCreatedObjectUndo(hitGo, "PlayfieldHit");
        hitGo.transform.SetParent(root.transform);
        hitGo.transform.localPosition = Vector3.zero;
        var playBox = hitGo.AddComponent<BoxCollider2D>();
        playBox.size = new Vector2(80f, 80f);
        hitGo.AddComponent<MiniGamePlayfield>();

        // Cursor overlay under Canvas
        var canvasGo = GameObject.Find("Canvas");
        RectTransform cursorRt = null;
        Image cursorImg = null;
        if (canvasGo != null)
        {
            var cursorGo = new GameObject("MiniGameCursorGraphic");
            Undo.RegisterCreatedObjectUndo(cursorGo, "MiniGameCursor");
            cursorGo.transform.SetParent(canvasGo.transform, false);
            cursorRt = cursorGo.AddComponent<RectTransform>();
            cursorRt.anchorMin = new Vector2(0.5f, 0.5f);
            cursorRt.anchorMax = new Vector2(0.5f, 0.5f);
            cursorRt.pivot = new Vector2(0.5f, 0.5f);
            cursorRt.sizeDelta = cursorSpr != null
                ? new Vector2(cursorSpr.texture.width / 4f, cursorSpr.texture.height / 4f)
                : new Vector2(48f, 48f);
            var img = cursorGo.AddComponent<Image>();
            img.sprite = cursorSpr;
            img.raycastTarget = false;
            cursorGo.SetActive(false);
        }

        var cursorCtl = root.AddComponent<MiniGameCursorController>();
        var texGuang = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/guangbiao.png");

        using (var so = new SerializedObject(cursorCtl))
        {
            so.FindProperty("cursorTexture").objectReferenceValue = texGuang;
            so.FindProperty("hotspotPixels").vector2Value = Vector2.zero;
            so.FindProperty("forceSoftwareCursor").boolValue = true;
            so.FindProperty("softwareCursorSprite").objectReferenceValue = cursorSpr;
            if (canvasGo != null)
            {
                so.FindProperty("overlayCanvas").objectReferenceValue = canvasGo.GetComponent<Canvas>();
                so.FindProperty("cursorGraphic").objectReferenceValue = cursorRt;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        using (var so = new SerializedObject(session))
        {
            so.FindProperty("drone").objectReferenceValue = droneComp;
            so.FindProperty("clickAudio").objectReferenceValue = audio;
            so.FindProperty("clickClip").objectReferenceValue = clickWav;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        using (var so = new SerializedObject(router))
        {
            so.FindProperty("worldCamera").objectReferenceValue = cam;
            so.FindProperty("gameplayPlaneZ").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Demo receiver + pad pair
        var recvGo = new GameObject("Receiver_A");
        Undo.RegisterCreatedObjectUndo(recvGo, "Receiver");
        recvGo.transform.SetParent(root.transform);
        recvGo.transform.localPosition = new Vector3(-3f, 1f, 0f);
        var recvSr = recvGo.AddComponent<SpriteRenderer>();
        recvSr.sprite = recvSpr;
        recvSr.sortingOrder = 10;
        var recvBox = recvGo.AddComponent<BoxCollider2D>();
        if (recvSpr != null)
            recvBox.size = recvSpr.bounds.size;
        else
            recvBox.size = new Vector2(1f, 1f);

        var padGo = new GameObject("BufferPad_A");
        Undo.RegisterCreatedObjectUndo(padGo, "BufferPad");
        padGo.transform.SetParent(recvGo.transform);
        padGo.transform.localPosition = new Vector3(2f, 0f, 0f);
        var padSr = padGo.AddComponent<SpriteRenderer>();
        padSr.sprite = padSpr;
        padSr.sortingOrder = 11;
        var padBox = padGo.AddComponent<BoxCollider2D>();
        if (padSpr != null)
        {
            var sz = padSpr.bounds.size;
            const float maxW = 2.4f;
            const float maxH = 2f;
            padBox.size = new Vector2(Mathf.Min(sz.x, maxW), Mathf.Min(sz.y, maxH));
        }
        else
            padBox.size = new Vector2(1f, 1f);
        padGo.AddComponent<MiniGameBufferPad>();
        padGo.SetActive(false);

        var recvComp = recvGo.AddComponent<MiniGameReceiver>();
        using (var so = new SerializedObject(recvComp))
        {
            so.FindProperty("pad").objectReferenceValue = padGo.GetComponent<MiniGameBufferPad>();
            so.FindProperty("padPlacement").enumValueIndex = (int)MiniGamePadPlacement.LocalAsChild;
            so.FindProperty("padPlacementValue").vector3Value = new Vector3(2f, 0f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog(
            "BuiltinMiniGame",
            "已生成 MiniGame_GameplayRoot（背景、命中面、无人机、示例接收器/缓冲垫、光标脚本）。\n默认已勾选软件光标（Additive 时更稳）。",
            "确定");
    }
}
#endif
