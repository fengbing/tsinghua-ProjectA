using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// Additive 加载小游戏场景；主场景不卸载，无人机机位与状态保留。
/// 会关闭主场景内 Canvas/相机等；在 _worldScene 中按名称关闭 environment 根物体（日本城市场景等），退出小游戏后恢复。
/// 小游戏加载后再关一遍「小游戏场景以外」的 Canvas（含 DontDestroyOnLoad），避免主游戏 UI 叠在小游戏上。
/// 进小游戏时会解锁鼠标（主场景 FollowCamera 默认锁定光标）、并暂时禁用主关卡上的 FollowCamera，避免抢鼠标；退出后恢复。
/// 可选整局 timeScale=0、并临时将无人机刚体设为 Kinematic 防漂移。
/// 小游戏内调用 <see cref="EndAndRestoreWorld"/> 卸载叠加场景并恢复主世界。
/// </summary>
public static class MiniGameAdditiveFlow
{
    static bool _active;
    static string _miniSceneName;
    static Scene _worldScene;
    static float _savedTimeScale = 1f;
    static bool _pausedTimeScale;

    static readonly List<CameraState> _cameras = new();
    static readonly List<CanvasState> _canvases = new();
    static readonly List<BehaviourState> _behaviours = new();

    static Rigidbody _frozenDroneRb;
    static bool _droneRbWasKinematic;

    static CursorLockMode _savedCursorLockMode;
    static bool _savedCursorVisible;
    static bool _haveSavedCursorState;

    struct FollowCamPause
    {
        public FollowCamera Fc;
        public bool WasEnabled;
    }

    static readonly List<FollowCamPause> _followCamPauses = new();

    /// <summary>主关卡里要暂时隐藏的根物体名称（与 Hierarchy 中根节点一致，不区分大小写）。</summary>
    public const string HiddenWorldEnvironmentRootName = "environment";

    static readonly List<HiddenGoState> _hiddenWorldRoots = new();

    struct HiddenGoState
    {
        public GameObject Go;
        public bool WasActive;
    }

    struct CameraState
    {
        public Camera Cam;
        public bool WasEnabled;
    }

    struct CanvasState
    {
        public Canvas Canvas;
        public bool WasEnabled;
    }

    struct BehaviourState
    {
        public Behaviour B;
        public bool WasEnabled;
    }

    public static bool IsActive => _active;

    /// <param name="sceneName">Build Settings 中的场景名（不含 .unity）</param>
    /// <param name="pauseWorldWithTimeScale">
    /// 为 true 时 <see cref="Time.timeScale"/> = 0，主世界与小游戏内依赖缩放时间的物理/动画都会停。
    /// 一般请保持 false，改用本类对无人机 Kinematic 冻结；仅当你需要整世界完全静止时再开。
    /// </param>
    public static bool Begin(string sceneName, bool pauseWorldWithTimeScale = false)
    {
        if (_active)
        {
            Debug.LogWarning($"{nameof(MiniGameAdditiveFlow)}: 已有叠加小游戏，忽略重复 Begin。", null);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        _worldScene = SceneManager.GetActiveScene();
        if (!_worldScene.IsValid())
            return false;

        _miniSceneName = sceneName.Trim();
        SaveAndUnlockCursorForMiniGame();
        CaptureWorldVisualAndInput();
        PauseFollowCamerasInWorldScene();

        AsyncOperation op = SceneManager.LoadSceneAsync(_miniSceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError(
                $"{nameof(MiniGameAdditiveFlow)}: 无法加载场景「{_miniSceneName}」，是否已加入 Build Settings？",
                null);
            ReleaseWorldVisualAndInput();
            return false;
        }

        _active = true;
        bool pause = pauseWorldWithTimeScale;
        op.completed += _ => OnMiniGameSceneLoadComplete(pause);
        return true;
    }

    static void OnMiniGameSceneLoadComplete(bool pauseWorldWithTimeScale)
    {
        Scene mini = SceneManager.GetSceneByName(_miniSceneName);
        if (mini.IsValid() && mini.isLoaded)
            SceneManager.SetActiveScene(mini);

        // 主场景里已关过 Canvas，但 DDOL / 其它已加载场景里的 Canvas 仍会叠在上面
        DisableAllCanvasesOutsideMiniScene(mini);
        SilenceEventSystemsAndAudioListenersOutsideMiniScene(mini);

        if (pauseWorldWithTimeScale)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _pausedTimeScale = true;
        }
    }

    static void CaptureWorldVisualAndInput()
    {
        _cameras.Clear();
        _canvases.Clear();
        _behaviours.Clear();
        _frozenDroneRb = null;

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.gameObject.scene != _worldScene)
                continue;
            _canvases.Add(new CanvasState { Canvas = canvas, WasEnabled = canvas.enabled });
            canvas.enabled = false;
        }

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam == null || cam.gameObject.scene != _worldScene)
                continue;
            _cameras.Add(new CameraState { Cam = cam, WasEnabled = cam.enabled });
            cam.enabled = false;
        }

        foreach (var al in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (al == null || al.gameObject.scene != _worldScene)
                continue;
            _behaviours.Add(new BehaviourState { B = al, WasEnabled = al.enabled });
            al.enabled = false;
        }

        foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (es == null || es.gameObject.scene != _worldScene)
                continue;
            _behaviours.Add(new BehaviourState { B = es, WasEnabled = es.enabled });
            es.enabled = false;
        }

        var plane = Object.FindFirstObjectByType<PlaneController>();
        if (plane != null)
        {
            plane.SetInputEnabled(false);
            var rb = plane.GetComponent<Rigidbody>();
            if (rb != null)
            {
                _frozenDroneRb = rb;
                _droneRbWasKinematic = rb.isKinematic;
                if (!rb.isKinematic)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                rb.isKinematic = true;
            }
        }

        HideWorldEnvironmentRoots();
    }

    static void HideWorldEnvironmentRoots()
    {
        _hiddenWorldRoots.Clear();
        if (!_worldScene.IsValid() || !_worldScene.isLoaded)
            return;

        var roots = _worldScene.GetRootGameObjects();
        var seen = new HashSet<GameObject>();
        foreach (var go in roots)
        {
            if (go == null)
                continue;
            TryHideEnvironmentObject(go, seen);
            for (int i = 0; i < go.transform.childCount; i++)
                TryHideEnvironmentObject(go.transform.GetChild(i).gameObject, seen);
        }
    }

    static void TryHideEnvironmentObject(GameObject go, HashSet<GameObject> seen)
    {
        if (go == null || !seen.Add(go))
            return;
        if (!string.Equals(go.name, HiddenWorldEnvironmentRootName, System.StringComparison.OrdinalIgnoreCase))
            return;
        _hiddenWorldRoots.Add(new HiddenGoState { Go = go, WasActive = go.activeSelf });
        go.SetActive(false);
    }

    static void RestoreHiddenWorldRoots()
    {
        foreach (var h in _hiddenWorldRoots)
        {
            if (h.Go != null)
                h.Go.SetActive(h.WasActive);
        }

        _hiddenWorldRoots.Clear();
    }

    static void SaveAndUnlockCursorForMiniGame()
    {
        _savedCursorLockMode = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;
        _haveSavedCursorState = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static void RestoreCursorAfterMiniGame()
    {
        if (!_haveSavedCursorState)
            return;
        Cursor.lockState = _savedCursorLockMode;
        Cursor.visible = _savedCursorVisible;
        _haveSavedCursorState = false;
    }

    static void PauseFollowCamerasInWorldScene()
    {
        _followCamPauses.Clear();
        if (!_worldScene.IsValid())
            return;
        foreach (var fc in Object.FindObjectsByType<FollowCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (fc == null || fc.gameObject.scene != _worldScene)
                continue;
            _followCamPauses.Add(new FollowCamPause { Fc = fc, WasEnabled = fc.enabled });
            fc.enabled = false;
        }
    }

    static void ResumeFollowCamerasInWorldScene()
    {
        foreach (var p in _followCamPauses)
        {
            if (p.Fc != null)
                p.Fc.enabled = p.WasEnabled;
        }

        _followCamPauses.Clear();
    }

    static bool BehaviourAlreadyTracked(Behaviour b)
    {
        foreach (var e in _behaviours)
        {
            if (e.B == b)
                return true;
        }

        return false;
    }

    /// <summary>关掉小游戏场景以外的 EventSystem / AudioListener（含 DDOL），避免 EventSystem.current 仍指向主菜单、导致 IsPointerOverGameObject / 点击异常。</summary>
    static void SilenceEventSystemsAndAudioListenersOutsideMiniScene(Scene miniScene)
    {
        if (!miniScene.IsValid())
            return;

        foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (es == null || es.gameObject.scene == miniScene || !es.enabled)
                continue;
            if (BehaviourAlreadyTracked(es))
                continue;
            _behaviours.Add(new BehaviourState { B = es, WasEnabled = es.enabled });
            es.enabled = false;
        }

        foreach (var al in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (al == null || al.gameObject.scene == miniScene || !al.enabled)
                continue;
            if (BehaviourAlreadyTracked(al))
                continue;
            _behaviours.Add(new BehaviourState { B = al, WasEnabled = al.enabled });
            al.enabled = false;
        }
    }

    static bool CanvasListContains(Canvas c)
    {
        foreach (var e in _canvases)
        {
            if (e.Canvas == c)
                return true;
        }

        return false;
    }

    /// <summary>关闭所有「不属于小游戏场景」的 Canvas（含 DontDestroyOnLoad），并记入列表以便退出时恢复。</summary>
    static void DisableAllCanvasesOutsideMiniScene(Scene miniScene)
    {
        if (!miniScene.IsValid())
            return;

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null)
                continue;
            var sc = canvas.gameObject.scene;
            if (!sc.IsValid() || sc == miniScene)
                continue;

            if (!CanvasListContains(canvas))
                _canvases.Add(new CanvasState { Canvas = canvas, WasEnabled = canvas.enabled });

            canvas.enabled = false;
        }
    }

    /// <summary>小游戏结束时调用：卸载叠加场景并恢复主世界。</summary>
    public static void EndAndRestoreWorld()
    {
        if (!_active || string.IsNullOrEmpty(_miniSceneName))
            return;

        AsyncOperation op = SceneManager.UnloadSceneAsync(_miniSceneName);
        if (op == null)
        {
            Debug.LogError($"{nameof(MiniGameAdditiveFlow)}: UnloadSceneAsync 失败：{_miniSceneName}", null);
            RestoreAfterUnload();
            return;
        }

        op.completed += _ => RestoreAfterUnload();
    }

    static void RestoreAfterUnload()
    {
        if (_pausedTimeScale)
        {
            Time.timeScale = _savedTimeScale;
            _pausedTimeScale = false;
        }

        ReleaseWorldVisualAndInput();

        if (_worldScene.IsValid() && _worldScene.isLoaded)
            SceneManager.SetActiveScene(_worldScene);

        _active = false;
        _miniSceneName = null;
    }

    static void ReleaseWorldVisualAndInput()
    {
        RestoreHiddenWorldRoots();
        ResumeFollowCamerasInWorldScene();
        RestoreCursorAfterMiniGame();

        foreach (var c in _cameras)
        {
            if (c.Cam != null)
                c.Cam.enabled = c.WasEnabled;
        }

        _cameras.Clear();

        foreach (var cv in _canvases)
        {
            if (cv.Canvas != null)
                cv.Canvas.enabled = cv.WasEnabled;
        }

        _canvases.Clear();

        foreach (var b in _behaviours)
        {
            if (b.B != null)
                b.B.enabled = b.WasEnabled;
        }

        _behaviours.Clear();

        if (_frozenDroneRb != null)
        {
            _frozenDroneRb.isKinematic = _droneRbWasKinematic;
            _frozenDroneRb = null;
        }

        var plane = Object.FindFirstObjectByType<PlaneController>();
        if (plane != null)
            plane.SetInputEnabled(true);
    }
}
