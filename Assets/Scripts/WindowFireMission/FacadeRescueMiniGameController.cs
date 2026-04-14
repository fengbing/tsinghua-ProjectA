using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 全屏立面救援小游戏：电梯逐窗停靠；每窗可多人（<see cref="peoplePerWindow"/>），每人 intro→details→Choices 顺序，该窗全部完成后再开下一窗。
/// 同窗 intro 共用 <c>w{i}_intro</c>；每人一套 details：<c>w{i}_p{j}_details</c>（<c>j≥1</c>），第一人可用 <c>w{i}_details</c>。
/// 由 <see cref="WindowFireMission"/> 在条件满足后打开；Scene 编辑预览见 <see cref="sceneLayoutEditPreview"/>。
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-100)]
public class FacadeRescueMiniGameController : MonoBehaviour, IFacadeRescueMinigameEntry
{
    public static bool IsFacadeRescueOpen => FacadeRescueSessionState.IsOpen;

    [System.Serializable]
    public class FacadeRescueWindowSlot
    {
        public Button windowButton;
        [Tooltip("可选：用于诊断等。")]
        public Image windowStageImage;
        public Sprite introSprite;
        public Sprite detailsSprite;
        [Tooltip("单人时可选；多人时每份 intro 下同名子物体上的 Button 会优先于本字段。")]
        public Button probeButton;
        [Tooltip("单人时可选；多人时每份 details 下同名子物体上的 Button 会优先于本字段。")]
        public Button revealChoicesButton;
        [Tooltip("每人 intro 根下「打开 details」按钮的物体名（菜单生成体为 ActionButton）。")]
        public string introProbeButtonObjectName = "ActionButton";
        [Tooltip("每人 details 根下「揭示选项」按钮的物体名（菜单生成体为 ActionButton）。")]
        public string detailsRevealButtonObjectName = "ActionButton";
        public GameObject choicesPanel;
        public Button slideLeftButton;
        public Button elevatorButton;
        public Button slideRightButton;
    }

    enum EscapeRouteKind
    {
        SlideLeft,
        Elevator,
        SlideRight
    }

    [Header("Root")]
    [Tooltip("在其子层级（或本 Canvas 根下）按名查找 UI；intro 同窗共用 w{i}_intro；details 多人时为 w{i}_p{j}_details，第一人可仍用 w{i}_details。")]
    [SerializeField] GameObject fullscreenRoot;
    [Tooltip("立面全屏背景；留空则在 fullscreenRoot 下查找名为 FacadeBackdrop 的子物体")]
    [SerializeField] RectTransform facadeBackdropRect;
    [SerializeField] PlaneController planeController;
    [SerializeField] DroneAutocruiseController autocruiseController;

    [Header("Elevator")]
    [SerializeField] RectTransform elevatorRect;
    [Tooltip("进入小游戏时电梯起始 anchoredPosition.y（相对立面根）")]
    [SerializeField] float elevatorYStart = 420f;
    [Tooltip("三扇窗对应的停靠高度，下标 0..2")]
    [SerializeField] float[] elevatorStopAnchoredY = { 160f, 40f, -80f };
    [Tooltip("救完人后电梯移出画面时的 anchoredPosition.y")]
    [SerializeField] float elevatorYExit = -620f;
    [SerializeField, Min(0.05f)] float elevatorMoveSeconds = 0.85f;
    [Tooltip("按窗顺序扁平编号（窗0所有人→窗1…）对应电梯旁头像位；各槽 Image 的 Sprite 在场景中配置，代码只开关 enabled，不写 sprite。")]
    [SerializeField] Image[] portraitSlots;

    [Tooltip("三扇窗各有多少人（顺序救援）；intro 共用 w{i}_intro，每人一套 details（w{i}_p{j}_details 或第一人 w{i}_details）。")]
    [SerializeField] int[] peoplePerWindow = { 2, 3, 2 };

    [Header("Windows (exactly 3)")]
    [SerializeField] List<FacadeRescueWindowSlot> windows = new List<FacadeRescueWindowSlot>();

    [Header("Audio")]
    [SerializeField] AudioClip slideChoiceClip;
    [SerializeField] AudioSource sfxSource;
    [Tooltip("电梯移动过程循环播放；留空则无声")]
    [SerializeField] AudioClip elevatorMoveClip;
    [Tooltip("立面小游戏内各功能按钮点击短音；留空则无声")]
    [SerializeField] AudioClip facadeUiClickClip;
    [Tooltip("选对逃生方式后的提示音，在滑道 slideChoiceClip 与电梯移动音效之前播放；留空则无声。每次电梯换层/离场前会再等待「本段时长 + 1 秒」再开始移动。")]
    [SerializeField] AudioClip escapeChoiceCorrectHintClip;
    [Tooltip("触发 wrong1 / wrong2 时的提示音；留空则无声")]
    [SerializeField] AudioClip escapeChoiceWrongHintClip;
    [Tooltip("成功弹窗第一段语音；留空则跳过。原 completionSystemClip 字段会迁移到此。")]
    [FormerlySerializedAs("completionSystemClip")]
    [SerializeField] AudioClip successDialogVoiceClip1;
    [Tooltip("成功弹窗第二段语音（接在第一段之后）；留空则跳过")]
    [SerializeField] AudioClip successDialogVoiceClip2;
    [Tooltip("点击 success 弹窗内按钮后播放的语音；留空则跳过")]
    [SerializeField] AudioClip successAfterButtonVoiceClip;
    [Tooltip("成功相关语音的播放源；留空则用 sfxSource")]
    [SerializeField] AudioSource completionVoiceSource;

    [Header("成功 success")]
    [Tooltip("电梯移出立面后显示；弹出后依次播 successDialogVoiceClip1/2，点按钮后再播 successAfterButtonVoiceClip")]
    [SerializeField] GameObject successPanel;
    [Tooltip("留空则从 successPanel 子物体上取第一个 Button（含未激活子物体）")]
    [SerializeField] Button successContinueButton;

    [Header("开场系统提示（进入小游戏后）")]
    [Tooltip("进入立面救援后先播两段黑底打字提示；留空则场景里查找 SystemDialogController")]
    [SerializeField] SystemDialogController preSessionSystemDialog;
    [TextArea(2, 6)]
    [SerializeField] string preSessionPromptText1;
    [SerializeField] AudioClip preSessionPromptVoice1;
    [TextArea(2, 6)]
    [SerializeField] string preSessionPromptText2;
    [SerializeField] AudioClip preSessionPromptVoice2;
    [Tooltip("<=0 时使用 SystemDialogController 默认打字速度")]
    [SerializeField] float preSessionPromptCharInterval;

    [Header("Cursor")]
    [Tooltip("覆盖规划小游戏的手型；留空则从 plannerCursorSource 或场景中 RoutePlanningMiniGameController 读取")]
    [SerializeField] Texture2D facadePlannerCursorOverride;
    [SerializeField] Vector2 facadePlannerCursorHotspotOverride;
    [Tooltip("留空则运行时 FindFirstObjectByType<RoutePlanningMiniGameController>")]
    [SerializeField] RoutePlanningMiniGameController plannerCursorSource;

    [Header("全屏 Overlay（从 World Space 切入时）")]
    [Tooltip("切 Overlay 后把 CanvasScaler 设为「随屏幕缩放」参考 1920×1080，避免沿用 World 预设导致整 UI 比例异常。")]
    [SerializeField] bool resetCanvasScalerForScreenOverlay = true;
    [Tooltip("立面 Canvas 作为该 Rect 的子物体并铺满（默认同 sceneLayoutScaleReference，一般为 RoutePlannerRoot），与路线规划共用同一 HUD 局部矩形与缩放。")]
    [SerializeField] RectTransform overlayHudLayoutReference;

    [Header("Scene 可视化编辑（编辑器）")]
    [Tooltip("勾选后：非播放状态下将本 Canvas 挂到 sceneLayoutAnchor 下并切为 World Space，便于在 Scene 里对齐建筑立面；进入运行或 Open 时会自动还原。")]
    [SerializeField] bool sceneLayoutEditPreview;
    [Tooltip("场景里放的空物体：UI 作为其子物体 local 归零对齐（可旋转该物体让 UI 朝向楼体）。")]
    [SerializeField] Transform sceneLayoutAnchor;
    [Tooltip("Scene 预览：在「缩放参考」存在时作为乘数；不存在时作为根 Canvas 的 uniform localScale。")]
    [SerializeField] float sceneLayoutUnityScale = 1f;
    [Tooltip("可选：拖入 RoutePlannerRoot（或与主界面同大的全屏 UI 根 Rect）。预览时立面 Canvas 的 lossyScale 会与它对齐，Scene 里与路线规划等大；留空则仅用上方系数。")]
    [SerializeField] RectTransform sceneLayoutScaleReference;

    [Header("Diagnostics")]
    [Tooltip("进入小游戏并完成布局后打印一次布局快照（与 Scene 下右键 Dump 对比）。Intro/Details 共用 windowStageImage/windowButton 的 Rect，仅换图。")]
    [SerializeField] bool debugDumpFacadeLayoutWhenMinigameOpens;

    [Header("错误反馈 wrong1")]
    [Tooltip("窗0第2人、窗1第1/2人点左/右滑杆时显示（应选电梯）；窗1第3人点左或电梯时显示（应选右滑杆）。点确认后关闭并恢复 detail+选项。")]
    [SerializeField] GameObject wrong1Panel;
    [Tooltip("留空则从 wrong1Panel 子物体上取第一个 Button（含未激活子物体）")]
    [SerializeField] Button wrong1DismissButton;

    [Header("错误反馈 wrong2")]
    [Tooltip("窗2（第三扇窗）各人在选项中若点电梯或右滑杆则显示；应正确选左滑杆。点确认后关闭并恢复 detail+选项。")]
    [SerializeField] GameObject wrong2Panel;
    [Tooltip("留空则从 wrong2Panel 子物体上取第一个 Button（含未激活子物体）")]
    [SerializeField] Button wrong2DismissButton;

    [Header("失败 fail1")]
    [Tooltip("窗0第一人若选左/右滑杆：全员选完后电梯不下降并显示本面板；点按钮仅重新开始立面救援流程（不重载场景）。")]
    [SerializeField] GameObject fail1Panel;
    [Tooltip("留空则从 fail1Panel 子物体上取第一个 Button（含未激活子物体）")]
    [SerializeField] Button fail1RestartButton;

    [Header("失败 fail2（超时）")]
    [Tooltip("立面救援总时长（秒）倒计时，归零后弹出 fail2；点按钮与 fail1 相同仅重开本回合。")]
    [SerializeField, Min(1f)] float facadeRescueTimeLimitSeconds = 200f;
    [Tooltip("可选：显示剩余秒数（向上取整）")]
    [SerializeField] TextMeshProUGUI facadeRescueCountdownText;
    [SerializeField] GameObject fail2Panel;
    [Tooltip("留空则从 fail2Panel 子物体上取第一个 Button（含未激活子物体）")]
    [SerializeField] Button fail2RestartButton;
    [Tooltip("弹出 fail1 或 fail2 面板时先播放；留空则跳过首段")]
    [SerializeField] AudioClip fail1OrFail2PanelShowClip;
    [Tooltip("首段 fail1/fail2 弹出音效播完后紧接播放；留空则跳过")]
    [SerializeField] AudioClip fail1OrFail2PanelShowClipFollowUp;

    bool _sceneEditCaptured;
    bool _sceneEditScalerWasEnabled = true;
    bool _scenePreviewHasLayoutSnapshot;
    RenderMode _sceneEditSavedRenderMode;
    Transform _sceneEditSavedParent;
    int _sceneEditSavedSibling;
    Vector3 _sceneEditSavedLocalPos;
    Quaternion _sceneEditSavedLocalRot;
    Vector3 _sceneEditSavedLocalScale;
    Vector2 _sceneEditSavedSizeDelta;
    Camera _sceneEditSavedWorldCamera;

    /// <summary>wrong1 显示中对应的窗下标；-1 表示未显示。</summary>
    int _wrong1BlockingWindow = -1;

    /// <summary>wrong2 显示中对应的窗下标；-1 表示未显示。</summary>
    int _wrong2BlockingWindow = -1;

    /// <summary>与 <see cref="portraitSlots"/> 对齐：仅当中间「电梯」逃生成功时才允许显示该槽头像。</summary>
    bool[] _portraitAllowedForSlotByElevatorEscape;

    WindowFireMission _mission;
    bool _planeInputBefore;
    CursorLockMode _cursorLockBefore;
    bool _cursorVisibleBefore;
    Coroutine _sessionCo;
    bool _isOpen;
    /// <summary>编辑器按 0 调试：协程用非缩放时间，避免 Time.timeScale=0 卡住。</summary>
    bool _sessionUsesUnscaledTime;
    bool _editorDebugWePausedWorld;
    bool _assignedWorldCameraForWorldSpaceCanvas;
    bool _runtimeOverlayPresentationActive;
    RenderMode _runtimeSavedRenderMode;
    Camera _runtimeSavedWorldCamera;
    Transform _runtimeSavedParent;
    int _runtimeSavedSiblingIndex;
    int _runtimeSavedSortingOrder;
    bool _runtimeSavedOverrideSorting;
    bool _runtimeSavedScalerEnabled;
    RectTransformSnap _runtimeSavedCanvasSnap;
    RectTransformSnap _runtimeSavedFullscreenRootSnap;
    bool _runtimeCanvasScalerPropsCaptured;
    CanvasScaler.ScaleMode _runtimeSavedScalerUiScaleMode;
    Vector2 _runtimeSavedScalerReferenceResolution;
    CanvasScaler.ScreenMatchMode _runtimeSavedScalerScreenMatchMode;
    float _runtimeSavedScalerMatchWidthOrHeight;
    float _runtimeSavedScalerScaleFactor;

    const float DesignCanvasWidth = 1920f;
    const float DesignCanvasHeight = 1080f;

    struct RectTransformSnap
    {
        public bool valid;
        public Vector2 anchorMin, anchorMax, pivot, sizeDelta, anchoredPosition, offsetMin, offsetMax;
        public Vector3 localScale;
        public bool hasImage;
        public bool imagePreserveAspect;
        public int imageType;

        public static RectTransformSnap Capture(RectTransform rt)
        {
            if (rt == null)
                return new RectTransformSnap { valid = false };
            var s = new RectTransformSnap
            {
                valid = true,
                anchorMin = rt.anchorMin,
                anchorMax = rt.anchorMax,
                pivot = rt.pivot,
                sizeDelta = rt.sizeDelta,
                anchoredPosition = rt.anchoredPosition,
                offsetMin = rt.offsetMin,
                offsetMax = rt.offsetMax,
                localScale = rt.localScale
            };
            var img = rt.GetComponent<Image>();
            if (img != null)
            {
                s.hasImage = true;
                s.imagePreserveAspect = img.preserveAspect;
                s.imageType = (int)img.type;
            }

            return s;
        }

        public void Apply(RectTransform rt)
        {
            if (!valid || rt == null)
                return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPosition;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            rt.localScale = localScale;
            if (!hasImage)
                return;
            var img = rt.GetComponent<Image>();
            if (img == null)
                return;
            img.preserveAspect = imagePreserveAspect;
            img.type = (Image.Type)imageType;
        }
    }

    RectTransformSnap _savedFullscreenRootSnap;
    RectTransformSnap _savedBackdropSnap;

    /// <summary>每窗流程：0 等点窗 →1 显示 intro →2 显示 details →3 选项 →4 当前人结束。</summary>
    readonly int[] _windowFlowPhase = new int[3];
    readonly int[] _activePersonInWindow = new int[3];
    readonly bool[] _resumeDetailsOnWindowClick = new bool[3];

    bool _elevatorMoveSoundPlaying;
    AudioClip _elevatorRestoreClip;
    bool _elevatorRestoreLoop;

    /// <summary>窗0第一人选了左或右滑杆时置位；全员完成后走 fail1 流程。</summary>
    bool _fail1PendingRestart;
    bool _fail1RestartClicked;

    float _facadeRescueSecondsLeft;
    bool _fail2TimeExpired;
    bool _fail2RestartClicked;

    bool _successFlowActive;
    bool _successContinueClicked;

    void Awake()
    {
        RestoreSceneEditPreview();

        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();
        if (sfxSource != null)
            sfxSource.playOnAwake = false;
        if (completionVoiceSource == null)
            completionVoiceSource = sfxSource;

        if (sfxSource != null)
            sfxSource.loop = false;

        if (Application.isPlaying)
        {
            if (fullscreenRoot != null)
                fullscreenRoot.SetActive(false);
            WireWindowButtons();
            WireWrong1DismissButton();
            WireWrong2DismissButton();
            WireFail1RestartButton();
            WireFail2RestartButton();
            WireSuccessContinueButton();
            HideAllWindowUi();
        }
#if UNITY_EDITOR
        else
        {
            ApplySceneEditPreview();
            if (!(sceneLayoutEditPreview && sceneLayoutAnchor != null) && fullscreenRoot != null)
                fullscreenRoot.SetActive(false);
        }
#endif
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            ApplySceneEditPreview();
#endif
        if (!Application.isPlaying || !_isOpen)
            return;
        TickFacadeRescueCountdown();
    }

    void TickFacadeRescueCountdown()
    {
        if (_fail2TimeExpired)
            return;
        float dt = _sessionUsesUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _facadeRescueSecondsLeft = Mathf.Max(0f, _facadeRescueSecondsLeft - dt);
        RefreshFacadeRescueCountdownLabel();
        if (_facadeRescueSecondsLeft > 0f)
            return;
        _fail2TimeExpired = true;
        if (fail2Panel != null)
        {
            fail2Panel.SetActive(true);
            PlayFail1OrFail2PanelOpenSfx();
        }
    }

    void RefreshFacadeRescueCountdownLabel()
    {
        if (facadeRescueCountdownText == null)
            return;
        facadeRescueCountdownText.text = Mathf.CeilToInt(_facadeRescueSecondsLeft).ToString();
    }

    /// <summary>由 <see cref="FacadeRescueDebugHotkeyRunnerBehaviour"/> 调用；本组件被禁用时也能从全局 Runner 打开。</summary>
    public void TryDebugOpenFacadeMinigame()
    {
        if (_isOpen)
            return;
        var missions = FindObjectsByType<WindowFireMission>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var mission = missions.Length > 0 ? missions[0] : null;
        OpenInternal(mission, editorDebugIsolateWorld: true);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
            return;
        ApplySceneEditPreview();
    }
#endif

    void OnDestroy()
    {
        if (Application.isPlaying)
            SetMinimapSuppressedForFacade(false);
        EndRuntimeFullscreenOverlayPresentation(restoreHierarchy: false);
        // 销毁阶段禁止 SetParent / 改层级，否则会报 Cannot set the parent while being destroyed。
        RestoreSceneEditPreview(allowHierarchyRestore: false);
    }

    static void SetMinimapSuppressedForFacade(bool suppress)
    {
        var maps = FindObjectsByType<MinimapUiController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var m in maps)
        {
            if (m != null)
                m.SetMapSuppressedForExternalReason(suppress);
        }
    }

    /// <summary>从本节点向上打开 inactive 祖先，使本物体可 StartCoroutine（Overlay 挂到未激活的 HUD 子树下会再次变「逻辑上不可见」）。</summary>
    bool TryActivateHierarchyForThisObject()
    {
        Transform walk = transform;
        while (walk != null && !gameObject.activeInHierarchy)
        {
            if (!walk.gameObject.activeSelf)
                walk.gameObject.SetActive(true);
            walk = walk.parent;
        }

        return gameObject.activeInHierarchy;
    }

    RectTransform ResolveFacadeBackdropRect()
    {
        if (facadeBackdropRect != null)
            return facadeBackdropRect;
        if (fullscreenRoot == null)
            return null;
        var t = fullscreenRoot.transform.Find("FacadeBackdrop");
        return t as RectTransform;
    }

    Transform NamedWindowSearchRootTransform =>
        fullscreenRoot != null ? fullscreenRoot.transform : transform;

    static Transform FindDescendantByNameRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;
        if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindDescendantByNameRecursive(root.GetChild(i), targetName);
            if (hit != null)
                return hit;
        }

        return null;
    }

    /// <summary>这些窗/人在「逃生选项」里只能选中间电梯；点左/右滑杆走 wrong1 分支。</summary>
    static bool MustUseElevatorOnlyForWrong1Slide(int windowIndex, int personIndex)
    {
        if (windowIndex == 0 && personIndex == 1)
            return true;
        if (windowIndex == 1 && personIndex >= 0 && personIndex <= 1)
            return true;
        return false;
    }

    /// <summary>窗1第3人：逃生选项里只能选右滑杆；点电梯或左滑杆走 wrong1。</summary>
    static bool MustUseRightSlideOnlyForWrong1(int windowIndex, int personIndex)
    {
        return windowIndex == 1 && personIndex == 2;
    }

    /// <summary>窗2（第三扇窗）所有人：逃生选项里只能选左滑杆；点中间电梯或右滑杆走 wrong2。</summary>
    bool MustUseLeftSlideOnlyForWrong2(int windowIndex, int personIndex)
    {
        if (windowIndex != 2)
            return false;
        int n = GetPeopleCountAtWindow(2);
        return personIndex >= 0 && personIndex < n;
    }

    bool IsWrongFeedbackBlocking() => _wrong1BlockingWindow >= 0 || _wrong2BlockingWindow >= 0;

    bool IsRescueInteractionBlocked() =>
        IsWrongFeedbackBlocking() || _fail2TimeExpired || _successFlowActive;

    int GetPeopleCountAtWindow(int windowIndex)
    {
        if (peoplePerWindow == null || windowIndex < 0 || windowIndex >= peoplePerWindow.Length)
            return 1;
        return Mathf.Max(1, peoplePerWindow[windowIndex]);
    }

    GameObject GetWindowIntroObject(int windowIndex)
    {
        // 同窗所有人共用同一套 intro（不按人分物体）。
        Transform t = FindDescendantByNameRecursive(
            NamedWindowSearchRootTransform,
            $"w{windowIndex}_intro");
        return t != null ? t.gameObject : null;
    }

    GameObject GetWindowDetailsObject(int windowIndex, int personIndex)
    {
        Transform t = FindDescendantByNameRecursive(
            NamedWindowSearchRootTransform,
            $"w{windowIndex}_p{personIndex}_details");
        if (t == null && personIndex == 0)
            t = FindDescendantByNameRecursive(NamedWindowSearchRootTransform, $"w{windowIndex}_details");
        return t != null ? t.gameObject : null;
    }

    void HideIntroDetailsForPerson(int windowIndex, int personIndex)
    {
        GameObject intro = GetWindowIntroObject(windowIndex);
        if (intro != null)
            intro.SetActive(false);
        GameObject details = GetWindowDetailsObject(windowIndex, personIndex);
        if (details != null)
            details.SetActive(false);
    }

    void HideAllIntroDetailsForWindow(int windowIndex)
    {
        int n = GetPeopleCountAtWindow(windowIndex);
        for (int p = 0; p < n; p++)
            HideIntroDetailsForPerson(windowIndex, p);
    }

    Button ResolveProbeButtonForWindow(int windowIndex)
    {
        if (windowIndex < 0 || windowIndex >= windows.Count)
            return null;
        var w = windows[windowIndex];
        GameObject intro = GetWindowIntroObject(windowIndex);
        if (intro == null)
            return w.probeButton;
        string nm = string.IsNullOrEmpty(w.introProbeButtonObjectName) ? "ActionButton" : w.introProbeButtonObjectName;
        Transform tr = FindDescendantByNameRecursive(intro.transform, nm);
        if (tr != null && tr.TryGetComponent(out Button btn))
            return btn;
        return w.probeButton;
    }

    Button ResolveRevealButtonForPerson(int windowIndex, int personIndex)
    {
        if (windowIndex < 0 || windowIndex >= windows.Count)
            return null;
        var w = windows[windowIndex];
        GameObject det = GetWindowDetailsObject(windowIndex, personIndex);
        if (det == null)
            return w.revealChoicesButton;
        string nm = string.IsNullOrEmpty(w.detailsRevealButtonObjectName) ? "ActionButton" : w.detailsRevealButtonObjectName;
        Transform tr = FindDescendantByNameRecursive(det.transform, nm);
        if (tr != null && tr.TryGetComponent(out Button btn))
            return btn;
        return w.revealChoicesButton;
    }

    Button ResolveDetailsBackButtonForPerson(int windowIndex, int personIndex)
    {
        GameObject det = GetWindowDetailsObject(windowIndex, personIndex);
        if (det == null)
            return null;
        Transform tr = FindDescendantByNameRecursive(det.transform, "Button2");
        if (tr != null && tr.TryGetComponent(out Button btn))
            return btn;
        return null;
    }

    [ContextMenu("Diagnostics/Dump facade layout vs backdrop (当前状态)")]
    void ContextMenu_DumpFacadeLayoutVsBackdrop()
    {
        DebugDumpFacadeLayoutSnapshot(Application.isPlaying ? "MANUAL_PlayMode" : "MANUAL_SceneOrEditMode");
    }

    /// <summary>
    /// 以 <see cref="ResolveFacadeBackdropRect"/> 为参考系，输出 fullscreenRoot 与各窗 stage（intro/details 同 Rect）的轴对齐包围盒，
    /// 便于对比 Scene 预览与进入游戏后的几何关系（非激活按钮会短暂打开再还原，以便算世界包围盒）。
    /// </summary>
    public void DebugDumpFacadeLayoutSnapshot(string label)
    {
        var bd = ResolveFacadeBackdropRect();
        var canvas = GetComponent<Canvas>();
        var scaler = GetComponent<CanvasScaler>();
        var sb = new StringBuilder();
        sb.AppendLine($"=== FacadeLayoutDiagnostics [{label}] ===");
        sb.AppendLine(
            $"activeInHierarchy={gameObject.activeInHierarchy} renderMode={canvas?.renderMode} canvas.scaleFactor={(canvas != null ? canvas.scaleFactor.ToString("F4") : "n/a")} scaler.enabled={(scaler != null && scaler.enabled)} scaler.scaleFactor={(scaler != null ? scaler.scaleFactor.ToString("F4") : "n/a")}");
        sb.AppendLine($"canvasRoot lossyScale={transform.lossyScale}");

        if (bd == null)
        {
            Debug.LogWarning($"[{label}] FacadeBackdrop 未解析（检查 facadeBackdropRect 或 fullscreenRoot/FacadeBackdrop）", this);
            return;
        }

        var savedWindowActives = new bool[windows.Count];
        for (int i = 0; i < windows.Count; i++)
        {
            var btn = windows[i].windowButton;
            savedWindowActives[i] = btn != null && btn.gameObject.activeSelf;
            if (btn != null)
                btn.gameObject.SetActive(true);
        }

        try
        {
            var rootRt = transform as RectTransform;
            if (rootRt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
            Canvas.ForceUpdateCanvases();

            float rw = Mathf.Max(1e-6f, bd.rect.width);
            float rh = Mathf.Max(1e-6f, bd.rect.height);
            float bx0 = bd.rect.xMin;
            float by0 = bd.rect.yMin;
            sb.AppendLine(
                $"Backdrop \"{bd.name}\" rect.size=({bd.rect.width:F2},{bd.rect.height:F2}) rect.xyMin=({bx0:F2},{by0:F2}) lossyScale={bd.lossyScale}");

            var fs = fullscreenRoot != null ? fullscreenRoot.transform as RectTransform : null;
            if (fs != null)
            {
                Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(bd, fs);
                sb.AppendLine(
                    $"fullscreenRoot relToBackdrop center=({b.center.x:F2},{b.center.y:F2}) size=({b.size.x:F2},{b.size.y:F2}) size/backdrop=({b.size.x / rw:F4},{b.size.y / rh:F4})");
            }

            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                var stageImg = GetWindowStageImage(w);
                var stageRt = stageImg != null ? stageImg.rectTransform : null;
                if (stageRt == null)
                {
                    sb.AppendLine($"W{i} stage: <null>");
                    continue;
                }

                Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(bd, stageRt);
                float nminX = (b.min.x - bx0) / rw;
                float nminY = (b.min.y - by0) / rh;
                float nmaxX = (b.max.x - bx0) / rw;
                float nmaxY = (b.max.y - by0) / rh;
                sb.AppendLine(
                    $"W{i} stage(intro/details同Rect) rel center=({b.center.x:F2},{b.center.y:F2}) rel size=({b.size.x:F2},{b.size.y:F2}) size/backdrop=({b.size.x / rw:F4},{b.size.y / rh:F4}) normInBackdrop=[{nminX:F4},{nminY:F4}]-[{nmaxX:F4},{nmaxY:F4}]");
            }
        }
        finally
        {
            for (int i = 0; i < windows.Count; i++)
            {
                var btn = windows[i].windowButton;
                if (btn != null)
                    btn.gameObject.SetActive(savedWindowActives[i]);
            }
        }

        Debug.Log(sb.ToString(), this);
    }

    /// <summary>运行时保证背景在 1920×1080 设计坐标下铺满 fullscreenRoot（由 CanvasScaler 再适配屏幕）。</summary>
    void EnsureBackdropStretchFillsDesignCanvasRuntime()
    {
        var bd = ResolveFacadeBackdropRect();
        if (bd == null)
            return;
        bd.anchorMin = Vector2.zero;
        bd.anchorMax = Vector2.one;
        bd.offsetMin = Vector2.zero;
        bd.offsetMax = Vector2.zero;
        bd.localScale = Vector3.one;
        var img = bd.GetComponent<Image>();
        if (img != null)
        {
            img.preserveAspect = false;
            img.type = Image.Type.Simple;
        }
    }

#if UNITY_EDITOR
    void ApplyDesign1920ForScenePreview()
    {
        var root = transform as RectTransform;
        if (root != null)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, DesignCanvasWidth);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, DesignCanvasHeight);
        }

        if (fullscreenRoot != null)
        {
            var fsRt = fullscreenRoot.transform as RectTransform;
            if (fsRt != null)
            {
                fsRt.anchorMin = Vector2.zero;
                fsRt.anchorMax = Vector2.one;
                fsRt.offsetMin = Vector2.zero;
                fsRt.offsetMax = Vector2.zero;
                fsRt.anchoredPosition = Vector2.zero;
                fsRt.localScale = Vector3.one;
            }
        }

        var bd = ResolveFacadeBackdropRect();
        if (bd != null)
        {
            bd.SetAsFirstSibling();
            bd.anchorMin = Vector2.zero;
            bd.anchorMax = Vector2.one;
            bd.offsetMin = Vector2.zero;
            bd.offsetMax = Vector2.zero;
            bd.localScale = Vector3.one;
            var img = bd.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = false;
                img.type = Image.Type.Simple;
            }
        }

        if (root != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    /// <summary>
    /// 路线规划在主 Canvas（Overlay）下；立面预览挂在世界锚点下。用参考 Rect 的 lossyScale 对齐，避免 Scene 里一大一小。
    /// </summary>
    float ResolveScenePreviewUniformScale()
    {
        float user = Mathf.Max(1e-6f, sceneLayoutUnityScale);
        if (sceneLayoutScaleReference == null || sceneLayoutAnchor == null)
            return user;

        Vector3 rl = sceneLayoutScaleReference.lossyScale;
        float refUniform = Mathf.Max(Mathf.Max(rl.x, rl.y), rl.z);
        Vector3 al = sceneLayoutAnchor.lossyScale;
        float anchorUniform = Mathf.Max(Mathf.Max(al.x, al.y), al.z);
        if (refUniform < 1e-6f || anchorUniform < 1e-6f)
            return user;

        return (refUniform / anchorUniform) * user;
    }
#endif

    void ApplySceneEditPreview()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
        if (!sceneLayoutEditPreview || sceneLayoutAnchor == null)
        {
            RestoreSceneEditPreview();
            return;
        }

        var canvas = GetComponent<Canvas>();
        if (canvas == null)
            return;

        if (!_sceneEditCaptured)
        {
            if (!_scenePreviewHasLayoutSnapshot)
            {
                var fsRt0 = fullscreenRoot != null ? fullscreenRoot.transform as RectTransform : null;
                _savedFullscreenRootSnap = RectTransformSnap.Capture(fsRt0);
                _savedBackdropSnap = RectTransformSnap.Capture(ResolveFacadeBackdropRect());
                _scenePreviewHasLayoutSnapshot = true;
            }

            _sceneEditSavedRenderMode = canvas.renderMode;
            _sceneEditSavedWorldCamera = canvas.worldCamera;
            _sceneEditSavedParent = transform.parent;
            _sceneEditSavedSibling = transform.GetSiblingIndex();
            _sceneEditSavedLocalPos = transform.localPosition;
            _sceneEditSavedLocalRot = transform.localRotation;
            _sceneEditSavedLocalScale = transform.localScale;
            var rtSnap = transform as RectTransform;
            if (rtSnap != null)
                _sceneEditSavedSizeDelta = rtSnap.sizeDelta;

            var scalerOnce = GetComponent<CanvasScaler>();
            if (scalerOnce != null)
                _sceneEditScalerWasEnabled = scalerOnce.enabled;

            _sceneEditCaptured = true;
        }

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null;
        transform.SetParent(sceneLayoutAnchor, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.enabled = false;

        ApplyDesign1920ForScenePreview();
        transform.localScale = Vector3.one * ResolveScenePreviewUniformScale();

        if (fullscreenRoot != null)
            fullscreenRoot.SetActive(true);
#endif
    }

    void RestoreSceneEditPreview(bool allowHierarchyRestore = true)
    {
        if (!_sceneEditCaptured)
            return;
        _sceneEditCaptured = false;

        if (!allowHierarchyRestore)
            return;

        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = _sceneEditSavedRenderMode;
            canvas.worldCamera = _sceneEditSavedWorldCamera;
        }

        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.enabled = _sceneEditScalerWasEnabled;

        if (_sceneEditSavedParent != null)
            transform.SetParent(_sceneEditSavedParent, false);
        else
            transform.SetParent(null, true);

        var n = transform.parent != null ? transform.parent.childCount : 0;
        if (n > 0)
            transform.SetSiblingIndex(Mathf.Clamp(_sceneEditSavedSibling, 0, n - 1));

        transform.localPosition = _sceneEditSavedLocalPos;
        transform.localRotation = _sceneEditSavedLocalRot;
        transform.localScale = _sceneEditSavedLocalScale;

        var rt = transform as RectTransform;
        if (rt != null)
            rt.sizeDelta = _sceneEditSavedSizeDelta;

        if (_scenePreviewHasLayoutSnapshot)
        {
            if (fullscreenRoot != null)
                _savedFullscreenRootSnap.Apply(fullscreenRoot.transform as RectTransform);
            _savedBackdropSnap.Apply(ResolveFacadeBackdropRect());
            _scenePreviewHasLayoutSnapshot = false;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && fullscreenRoot != null && (!sceneLayoutEditPreview || sceneLayoutAnchor == null))
            fullscreenRoot.SetActive(false);
#endif
    }

    Image GetWindowStageImage(FacadeRescueWindowSlot w)
    {
        if (w == null)
            return null;
        if (w.windowStageImage != null)
            return w.windowStageImage;
        return w.windowButton != null ? w.windowButton.GetComponent<Image>() : null;
    }

    void SetChoicesVisible(FacadeRescueWindowSlot w, bool on)
    {
        if (w == null)
            return;
        if (w.choicesPanel != null)
        {
            w.choicesPanel.SetActive(on);
            return;
        }

        if (w.slideLeftButton != null)
            w.slideLeftButton.gameObject.SetActive(on);
        if (w.elevatorButton != null)
            w.elevatorButton.gameObject.SetActive(on);
        if (w.slideRightButton != null)
            w.slideRightButton.gameObject.SetActive(on);
    }

    RectTransform ResolveOverlayHudLayoutReference()
    {
        if (overlayHudLayoutReference != null)
            return overlayHudLayoutReference;
        if (sceneLayoutScaleReference != null)
            return sceneLayoutScaleReference;
        var p = plannerCursorSource != null
            ? plannerCursorSource
            : FindFirstObjectByType<RoutePlanningMiniGameController>();
        return p != null ? p.RoutePlannerHudRootRect : null;
    }

    static void StretchRectTransformToFillParent(RectTransform rt)
    {
        if (rt == null)
            return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 立面 Canvas 在场景里多为 World Space，第三人称难看见；打开时切 Overlay。
    /// 若可解析到 HUD 参考 Rect（如 RoutePlannerRoot），则作为其子物体铺满，与路线规划同一局部布局空间；嵌套时关闭本 Canvas 的 CanvasScaler，避免与根 Canvas 双重缩放。
    /// 否则回退为根节点 + 打开前快照，并按 resetCanvasScalerForScreenOverlay 调整 Scaler。
    /// </summary>
    void BeginRuntimeFullscreenOverlayPresentation()
    {
        if (_runtimeOverlayPresentationActive)
            return;
        var canvas = GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            return;

        _runtimeOverlayPresentationActive = true;
        _runtimeSavedRenderMode = canvas.renderMode;
        _runtimeSavedWorldCamera = canvas.worldCamera;
        _runtimeSavedParent = transform.parent;
        _runtimeSavedSiblingIndex = transform.GetSiblingIndex();
        _runtimeSavedSortingOrder = canvas.sortingOrder;
        _runtimeSavedOverrideSorting = canvas.overrideSorting;
        var canvasRt = transform as RectTransform;
        _runtimeSavedCanvasSnap = RectTransformSnap.Capture(canvasRt);
        _runtimeSavedFullscreenRootSnap = fullscreenRoot != null
            ? RectTransformSnap.Capture(fullscreenRoot.transform as RectTransform)
            : new RectTransformSnap { valid = false };

        var scaler = GetComponent<CanvasScaler>();
        _runtimeCanvasScalerPropsCaptured = scaler != null;
        if (scaler != null)
        {
            _runtimeSavedScalerEnabled = scaler.enabled;
            _runtimeSavedScalerUiScaleMode = scaler.uiScaleMode;
            _runtimeSavedScalerReferenceResolution = scaler.referenceResolution;
            _runtimeSavedScalerScreenMatchMode = scaler.screenMatchMode;
            _runtimeSavedScalerMatchWidthOrHeight = scaler.matchWidthOrHeight;
            _runtimeSavedScalerScaleFactor = scaler.scaleFactor;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 320);

        var layoutRef = ResolveOverlayHudLayoutReference();
        if (layoutRef != null && layoutRef != canvasRt && !layoutRef.IsChildOf(canvasRt) && canvasRt != null)
        {
            transform.SetParent(layoutRef, false);
            StretchRectTransformToFillParent(canvasRt);
            canvasRt.SetAsLastSibling();
            if (scaler != null)
                scaler.enabled = false;
        }
        else
        {
            transform.SetParent(null, false);
            if (canvasRt != null)
            {
                _runtimeSavedCanvasSnap.Apply(canvasRt);
                canvasRt.localScale = Vector3.one;
                canvasRt.localRotation = Quaternion.identity;
            }

            if (scaler != null)
            {
                scaler.enabled = true;
                if (resetCanvasScalerForScreenOverlay)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(DesignCanvasWidth, DesignCanvasHeight);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0.5f;
                }
            }
        }

        if (canvasRt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRt);
            Canvas.ForceUpdateCanvases();
        }
    }

    /// <param name="restoreHierarchy">false 时跳过 Rect 快照与 SetParent（供 OnDestroy 使用，避免销毁中改父节点报错）。</param>
    void EndRuntimeFullscreenOverlayPresentation(bool restoreHierarchy = true)
    {
        if (!_runtimeOverlayPresentationActive)
            return;
        _runtimeOverlayPresentationActive = false;

        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = _runtimeSavedRenderMode;
            canvas.worldCamera = _runtimeSavedWorldCamera;
            canvas.sortingOrder = _runtimeSavedSortingOrder;
            canvas.overrideSorting = _runtimeSavedOverrideSorting;
        }

        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            if (_runtimeCanvasScalerPropsCaptured)
            {
                scaler.uiScaleMode = _runtimeSavedScalerUiScaleMode;
                scaler.referenceResolution = _runtimeSavedScalerReferenceResolution;
                scaler.screenMatchMode = _runtimeSavedScalerScreenMatchMode;
                scaler.matchWidthOrHeight = _runtimeSavedScalerMatchWidthOrHeight;
                scaler.scaleFactor = _runtimeSavedScalerScaleFactor;
            }

            scaler.enabled = _runtimeSavedScalerEnabled;
        }

        _runtimeCanvasScalerPropsCaptured = false;

        if (!restoreHierarchy)
            return;

        _runtimeSavedCanvasSnap.Apply(transform as RectTransform);
        if (_runtimeSavedFullscreenRootSnap.valid && fullscreenRoot != null)
            _runtimeSavedFullscreenRootSnap.Apply(fullscreenRoot.transform as RectTransform);

        if (_runtimeSavedParent != null)
            transform.SetParent(_runtimeSavedParent, false);
        else
            transform.SetParent(null, true);

        int n = transform.parent != null ? transform.parent.childCount : 0;
        if (n > 0)
            transform.SetSiblingIndex(Mathf.Clamp(_runtimeSavedSiblingIndex, 0, n - 1));
    }

    void ApplyFacadePlannerCursor()
    {
        Texture2D tex = facadePlannerCursorOverride;
        Vector2 hot = facadePlannerCursorHotspotOverride;
        if (tex == null)
        {
            var src = plannerCursorSource != null
                ? plannerCursorSource
                : FindFirstObjectByType<RoutePlanningMiniGameController>();
            if (src != null)
            {
                tex = src.PlannerCursorTexture;
                hot = src.PlannerCursorHotspot;
            }
        }

        Cursor.SetCursor(tex, hot, CursorMode.Auto);
    }

    void WireWindowButtons()
    {
        var wiredProbe = new HashSet<Button>();
        var wiredReveal = new HashSet<Button>();
        var wiredDetailsBack = new HashSet<Button>();
        for (int i = 0; i < windows.Count; i++)
        {
            int idx = i;
            var w = windows[i];
            if (w.windowButton != null)
            {
                w.windowButton.onClick.RemoveAllListeners();
                w.windowButton.onClick.AddListener(() => OnWindowClicked(idx));
            }

            Button sharedProbe = ResolveProbeButtonForWindow(i);
            if (sharedProbe != null && wiredProbe.Add(sharedProbe))
            {
                sharedProbe.onClick.RemoveAllListeners();
                sharedProbe.onClick.AddListener(() => OnProbeClicked(idx));
            }

            int n = GetPeopleCountAtWindow(i);
            for (int p = 0; p < n; p++)
            {
                Button reveal = ResolveRevealButtonForPerson(i, p);
                if (reveal != null && wiredReveal.Add(reveal))
                {
                    reveal.onClick.RemoveAllListeners();
                    reveal.onClick.AddListener(() => OnRevealChoicesClicked(idx));
                }

                int personIdx = p;
                Button detailsBack = ResolveDetailsBackButtonForPerson(i, p);
                if (detailsBack != null && wiredDetailsBack.Add(detailsBack))
                {
                    detailsBack.onClick.RemoveAllListeners();
                    detailsBack.onClick.AddListener(() => OnDetailsBackToWindowClicked(idx, personIdx));
                }
            }

            if (w.slideLeftButton != null)
            {
                w.slideLeftButton.onClick.RemoveAllListeners();
                w.slideLeftButton.onClick.AddListener(() => OnEscapeChoice(idx, EscapeRouteKind.SlideLeft));
            }

            if (w.slideRightButton != null)
            {
                w.slideRightButton.onClick.RemoveAllListeners();
                w.slideRightButton.onClick.AddListener(() => OnEscapeChoice(idx, EscapeRouteKind.SlideRight));
            }

            if (w.elevatorButton != null)
            {
                w.elevatorButton.onClick.RemoveAllListeners();
                w.elevatorButton.onClick.AddListener(() => OnEscapeChoice(idx, EscapeRouteKind.Elevator));
            }
        }
    }

    void WireWrong1DismissButton()
    {
        if (!Application.isPlaying)
            return;
        var btn = wrong1DismissButton;
        if (btn == null && wrong1Panel != null)
            btn = wrong1Panel.GetComponentInChildren<Button>(true);
        if (btn == null)
            return;
        btn.onClick.RemoveListener(OnWrong1Dismissed);
        btn.onClick.AddListener(OnWrong1Dismissed);
    }

    void OnWrong1Dismissed()
    {
        if (!_isOpen || _wrong1BlockingWindow < 0 || _fail2TimeExpired)
            return;
        int idx = _wrong1BlockingWindow;
        _wrong1BlockingWindow = -1;
        if (wrong1Panel != null)
            wrong1Panel.SetActive(false);

        int p = idx >= 0 && idx < _activePersonInWindow.Length ? _activePersonInWindow[idx] : 0;
        if (idx < 0 || idx >= windows.Count)
            return;
        var w = windows[idx];
        GameObject det = GetWindowDetailsObject(idx, p);
        if (det != null)
            det.SetActive(true);
        SetChoicesVisible(w, true);
    }

    void WireWrong2DismissButton()
    {
        if (!Application.isPlaying)
            return;
        var btn = wrong2DismissButton;
        if (btn == null && wrong2Panel != null)
            btn = wrong2Panel.GetComponentInChildren<Button>(true);
        if (btn == null)
            return;
        btn.onClick.RemoveListener(OnWrong2Dismissed);
        btn.onClick.AddListener(OnWrong2Dismissed);
    }

    void OnWrong2Dismissed()
    {
        if (!_isOpen || _wrong2BlockingWindow < 0 || _fail2TimeExpired)
            return;
        int idx = _wrong2BlockingWindow;
        _wrong2BlockingWindow = -1;
        if (wrong2Panel != null)
            wrong2Panel.SetActive(false);

        int p = idx >= 0 && idx < _activePersonInWindow.Length ? _activePersonInWindow[idx] : 0;
        if (idx < 0 || idx >= windows.Count)
            return;
        var w = windows[idx];
        GameObject det = GetWindowDetailsObject(idx, p);
        if (det != null)
            det.SetActive(true);
        SetChoicesVisible(w, true);
    }

    void WireFail1RestartButton()
    {
        if (!Application.isPlaying)
            return;
        var btn = fail1RestartButton;
        if (btn == null && fail1Panel != null)
            btn = fail1Panel.GetComponentInChildren<Button>(true);
        if (btn == null)
            return;
        btn.onClick.RemoveListener(OnFail1RestartClicked);
        btn.onClick.AddListener(OnFail1RestartClicked);
    }

    void OnFail1RestartClicked()
    {
        if (!_isOpen || !_fail1PendingRestart)
            return;
        _fail1RestartClicked = true;
    }

    void WireFail2RestartButton()
    {
        if (!Application.isPlaying)
            return;
        var btn = fail2RestartButton;
        if (btn == null && fail2Panel != null)
            btn = fail2Panel.GetComponentInChildren<Button>(true);
        if (btn == null)
            return;
        btn.onClick.RemoveListener(OnFail2RestartClicked);
        btn.onClick.AddListener(OnFail2RestartClicked);
    }

    void OnFail2RestartClicked()
    {
        if (!_isOpen)
            return;
        if (!_fail2TimeExpired && (fail2Panel == null || !fail2Panel.activeSelf))
            return;
        _fail2RestartClicked = true;
    }

    void WireSuccessContinueButton()
    {
        if (!Application.isPlaying)
            return;
        var btn = successContinueButton;
        if (btn == null && successPanel != null)
            btn = successPanel.GetComponentInChildren<Button>(true);
        if (btn == null)
            return;
        btn.onClick.RemoveListener(OnSuccessContinueClicked);
        btn.onClick.AddListener(OnSuccessContinueClicked);
    }

    void OnSuccessContinueClicked()
    {
        if (!_isOpen || !_successFlowActive)
            return;
        _successContinueClicked = true;
    }

    IEnumerator CoSuccessPanelAndCompletionVoices()
    {
        _successFlowActive = true;
        _successContinueClicked = false;
        if (successPanel != null)
            successPanel.SetActive(true);

        var src = completionVoiceSource != null ? completionVoiceSource : sfxSource;

        if (successDialogVoiceClip1 != null && src != null)
        {
            src.PlayOneShot(successDialogVoiceClip1);
            if (_sessionUsesUnscaledTime)
                yield return new WaitForSecondsRealtime(successDialogVoiceClip1.length);
            else
                yield return new WaitForSeconds(successDialogVoiceClip1.length);
        }

        if (successDialogVoiceClip2 != null && src != null)
        {
            src.PlayOneShot(successDialogVoiceClip2);
            if (_sessionUsesUnscaledTime)
                yield return new WaitForSecondsRealtime(successDialogVoiceClip2.length);
            else
                yield return new WaitForSeconds(successDialogVoiceClip2.length);
        }

        Button btn = successContinueButton;
        if (btn == null && successPanel != null)
            btn = successPanel.GetComponentInChildren<Button>(true);

        if (btn != null)
        {
            _successContinueClicked = false;
            yield return new WaitUntil(() => _successContinueClicked);
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(FacadeRescueMiniGameController)}: successPanel 未找到 Button，将不等待点击直接播放第三段语音。",
                this);
        }

        if (successAfterButtonVoiceClip != null && src != null)
        {
            src.PlayOneShot(successAfterButtonVoiceClip);
            if (_sessionUsesUnscaledTime)
                yield return new WaitForSecondsRealtime(successAfterButtonVoiceClip.length);
            else
                yield return new WaitForSeconds(successAfterButtonVoiceClip.length);
        }

        if (successPanel != null)
            successPanel.SetActive(false);
        _successFlowActive = false;
    }

    /// <summary>由任务在无人机进入触发区且前置音频完成后调用。</summary>
    public void Open(WindowFireMission mission)
    {
        OpenInternal(mission, editorDebugIsolateWorld: false);
    }

    void OpenInternal(WindowFireMission mission, bool editorDebugIsolateWorld)
    {
        if (!Application.isPlaying)
            return;
        if (!editorDebugIsolateWorld && mission == null)
            return;
        if (_isOpen)
            return;

        if (!TryActivateHierarchyForThisObject())
            return;

        RestoreSceneEditPreview();
        EnsureBackdropStretchFillsDesignCanvasRuntime();

        _assignedWorldCameraForWorldSpaceCanvas = false;
        BeginRuntimeFullscreenOverlayPresentation();
        if (!_runtimeOverlayPresentationActive)
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    canvas.worldCamera = mainCam;
                    _assignedWorldCameraForWorldSpaceCanvas = true;
                }
            }
        }

        // 挂到 RoutePlannerRoot 等之下时，若整条 HUD 曾处于未激活，需再次沿父链打开。
        if (!TryActivateHierarchyForThisObject())
        {
            EndRuntimeFullscreenOverlayPresentation();
            return;
        }

        _mission = mission;
        _isOpen = true;
        _fail2TimeExpired = false;
        _fail2RestartClicked = false;
        _facadeRescueSecondsLeft = Mathf.Max(1f, facadeRescueTimeLimitSeconds);
        RefreshFacadeRescueCountdownLabel();
        _sessionUsesUnscaledTime = editorDebugIsolateWorld;
        _editorDebugWePausedWorld = editorDebugIsolateWorld;
        FacadeRescueSessionState.SetOpen(true);
        SetMinimapSuppressedForFacade(true);
        _planeInputBefore = planeController != null && planeController.IsInputEnabled;
        autocruiseController?.SetPlannerInputBlocked(true);

        _cursorLockBefore = Cursor.lockState;
        _cursorVisibleBefore = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ApplyFacadePlannerCursor();

        planeController?.SetInputEnabled(false);
        if (editorDebugIsolateWorld)
        {
            GlobalGamePause.Pause();
            AudioListener.pause = false;
        }
        else
        {
            GlobalGamePause.ForceResumeIfPaused();
        }

        if (fullscreenRoot != null)
            fullscreenRoot.SetActive(true);

        HideAllWindowUi();
        ClearPortraits();

        if (debugDumpFacadeLayoutWhenMinigameOpens)
            DebugDumpFacadeLayoutSnapshot("Play_Open_AfterLayout");

        if (_sessionCo != null)
            StopCoroutine(_sessionCo);
        enabled = true;
        if (!TryActivateHierarchyForThisObject())
        {
            Close(notifyMission: false);
            return;
        }

        _sessionCo = StartCoroutine(CoOpenPreludeAndSession());
    }

    /// <param name="stopSessionCoroutine">为 false 时仅解除对协程的引用、不 StopCoroutine（历史用途；fail1 已改为协程内直接重开回合）。</param>
    void Close(bool notifyMission, bool stopSessionCoroutine = true)
    {
        if (!_isOpen)
            return;

        SetMinimapSuppressedForFacade(false);

        EndRuntimeFullscreenOverlayPresentation();

        if (_sessionCo != null)
        {
            if (stopSessionCoroutine)
                StopCoroutine(_sessionCo);
            _sessionCo = null;
        }

        EndElevatorMoveSound();

        _isOpen = false;
        FacadeRescueSessionState.SetOpen(false);
        _sessionUsesUnscaledTime = false;
        if (_assignedWorldCameraForWorldSpaceCanvas)
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
                canvas.worldCamera = null;
            _assignedWorldCameraForWorldSpaceCanvas = false;
        }

        if (_editorDebugWePausedWorld)
        {
            _editorDebugWePausedWorld = false;
            GlobalGamePause.Resume();
        }

        HideAllWindowUi();

        if (fullscreenRoot != null)
            fullscreenRoot.SetActive(false);

        if (autocruiseController != null)
            autocruiseController.SetPlannerInputBlocked(false);

        if (planeController != null)
            planeController.SetInputEnabled(_planeInputBefore);

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.lockState = _cursorLockBefore;
        Cursor.visible = _cursorVisibleBefore;

        var m = _mission;
        _mission = null;
        if (notifyMission)
            m?.NotifyFacadeRescueMinigameComplete();
        else
            m?.NotifyFacadeRescueMinigameAborted();
    }

    IEnumerator CoSession()
    {
        if (elevatorRect == null || windows.Count != 3 || elevatorStopAnchoredY == null || elevatorStopAnchoredY.Length != 3)
        {
            Debug.LogError($"{nameof(FacadeRescueMiniGameController)}: 需配置 elevatorRect 与恰好 3 个窗口及 3 个停靠高度。", this);
            Close(false);
            yield break;
        }

        while (true)
        {
            int totalPeople = 0;
            for (int i = 0; i < 3; i++)
                totalPeople += GetPeopleCountAtWindow(i);
            if (portraitSlots != null && totalPeople > portraitSlots.Length)
                Debug.LogWarning(
                    $"{nameof(FacadeRescueMiniGameController)}: 总人数 {totalPeople} 大于 portraitSlots 长度 {portraitSlots.Length}，超出部分不会显示头像。",
                    this);

            if (portraitSlots != null && portraitSlots.Length > 0)
                _portraitAllowedForSlotByElevatorEscape = new bool[portraitSlots.Length];
            else
                _portraitAllowedForSlotByElevatorEscape = null;

            _fail1PendingRestart = false;
            _fail1RestartClicked = false;
            _fail2TimeExpired = false;
            _fail2RestartClicked = false;
            _facadeRescueSecondsLeft = Mathf.Max(1f, facadeRescueTimeLimitSeconds);
            RefreshFacadeRescueCountdownLabel();

            HideAllWindowUi();
            ClearPortraits();
            WireWindowButtons();

            var pos = elevatorRect.anchoredPosition;
            pos.y = elevatorYStart;
            elevatorRect.anchoredPosition = pos;

            for (int w = 0; w < 3; w++)
            {
                if (_fail2TimeExpired)
                    break;
                if (w > 0)
                    yield return CoWaitAfterCorrectHintBeforeElevator();
                if (_fail2TimeExpired)
                    break;
                yield return CoTweenElevatorY(elevatorStopAnchoredY[w]);
                if (_fail2TimeExpired)
                    break;
                int nPeople = GetPeopleCountAtWindow(w);
                for (int p = 0; p < nPeople; p++)
                {
                    if (_fail2TimeExpired)
                        break;
                    yield return CoOnePersonInWindow(w, p);
                    if (_fail2TimeExpired)
                        break;
                    int slot = FlatPortraitSlotIndex(w, p);
                    if (_portraitAllowedForSlotByElevatorEscape != null &&
                        slot >= 0 &&
                        slot < _portraitAllowedForSlotByElevatorEscape.Length &&
                        _portraitAllowedForSlotByElevatorEscape[slot])
                        ApplyPortraitForWindowPerson(w, p);
                }

                if (_fail2TimeExpired)
                    break;
            }

            if (_fail2TimeExpired)
            {
                yield return CoAwaitFail2RestartClick();
                continue;
            }

            if (_fail1PendingRestart)
            {
                Button failBtn = fail1RestartButton;
                if (failBtn == null && fail1Panel != null)
                    failBtn = fail1Panel.GetComponentInChildren<Button>(true);

                if (fail1Panel != null && failBtn != null)
                {
                    fail1Panel.SetActive(true);
                    PlayFail1OrFail2PanelOpenSfx();
                    _fail1RestartClicked = false;
                    _fail2RestartClicked = false;
                    yield return new WaitUntil(() => _fail1RestartClicked || _fail2RestartClicked);
                    if (_fail2RestartClicked)
                    {
                        if (fail1Panel != null)
                            fail1Panel.SetActive(false);
                        _fail1PendingRestart = false;
                        _fail1RestartClicked = false;
                        yield return CoAwaitFail2RestartClick();
                        continue;
                    }

                    if (fail1Panel != null)
                        fail1Panel.SetActive(false);
                    continue;
                }
                else if (fail1Panel != null)
                {
                    Debug.LogWarning(
                        $"{nameof(FacadeRescueMiniGameController)}: fail1Panel 已配置但找不到 Button，将直接重新开始救援回合。",
                        this);
                }
                else
                {
                    Debug.LogWarning(
                        $"{nameof(FacadeRescueMiniGameController)}: 窗0第一人选了左/右滑杆但未配置 fail1Panel，将直接重新开始救援回合。",
                        this);
                }

                continue;
            }

            yield return CoWaitAfterCorrectHintBeforeElevator();
            if (!_fail2TimeExpired)
                yield return CoTweenElevatorY(elevatorYExit);

            if (_fail2TimeExpired)
            {
                yield return CoAwaitFail2RestartClick();
                continue;
            }

            if (successPanel != null)
                yield return CoSuccessPanelAndCompletionVoices();
            else if (successDialogVoiceClip1 != null || successDialogVoiceClip2 != null || successAfterButtonVoiceClip != null)
            {
                Debug.LogWarning(
                    $"{nameof(FacadeRescueMiniGameController)}: 已配置成功相关语音但未指定 successPanel，将跳过成功弹窗与语音。",
                    this);
            }

            Close(true);
            yield break;
        }
    }

    IEnumerator CoOpenPreludeAndSession()
    {
        yield return CoPlayPreSessionSystemPrompts();
        if (!_isOpen)
            yield break;
        yield return CoSession();
    }

    IEnumerator CoPlayPreSessionSystemPrompts()
    {
        bool hasLine1 = !string.IsNullOrEmpty(preSessionPromptText1) || preSessionPromptVoice1 != null;
        bool hasLine2 = !string.IsNullOrEmpty(preSessionPromptText2) || preSessionPromptVoice2 != null;
        if (!hasLine1 && !hasLine2)
            yield break;

        var dlg = preSessionSystemDialog != null
            ? preSessionSystemDialog
            : FindFirstObjectByType<SystemDialogController>();
        if (dlg == null)
        {
            Debug.LogWarning(
                $"{nameof(FacadeRescueMiniGameController)}: 已配置开场系统提示但未找到 {nameof(SystemDialogController)}，将直接开始救援流程。",
                this);
            yield break;
        }

        float interval = preSessionPromptCharInterval > 0f ? preSessionPromptCharInterval : 0f;
        if (hasLine1)
        {
            dlg.PlaySingleLine(preSessionPromptText1 ?? string.Empty, preSessionPromptVoice1, interval);
            yield return dlg.WaitUntilDialogIdle();
            if (!_isOpen)
                yield break;
        }

        if (hasLine2)
        {
            dlg.PlaySingleLine(preSessionPromptText2 ?? string.Empty, preSessionPromptVoice2, interval);
            yield return dlg.WaitUntilDialogIdle();
        }
    }

    IEnumerator CoOnePersonInWindow(int windowIndex, int personIndex)
    {
        var w = windows[windowIndex];
        _activePersonInWindow[windowIndex] = personIndex;
        _windowFlowPhase[windowIndex] = 0;

        if (personIndex == 0)
        {
            SetOnlyWindowHotspotActive(windowIndex);
            yield return new WaitUntil(() => _windowFlowPhase[windowIndex] >= 1 || _fail2TimeExpired);
            if (_fail2TimeExpired)
                yield break;
        }
        else
        {
            // 同窗下一人：不再要求再点 intro 上的「查看详情」；直接打开当前人的 details（等同 OnProbeClicked）。
            HideIntroDetailsForPerson(windowIndex, personIndex - 1);
            SetChoicesVisible(w, false);
            var prevReveal = ResolveRevealButtonForPerson(windowIndex, personIndex - 1);
            if (prevReveal != null)
                prevReveal.gameObject.SetActive(false);
            var prevProbe = ResolveProbeButtonForWindow(windowIndex);
            if (prevProbe != null)
                prevProbe.gameObject.SetActive(false);

            GameObject intro = GetWindowIntroObject(windowIndex);
            if (intro != null)
                intro.SetActive(true);
            GameObject details = GetWindowDetailsObject(windowIndex, personIndex);
            if (details != null)
                details.SetActive(true);
            var revealCur = ResolveRevealButtonForPerson(windowIndex, personIndex);
            if (revealCur != null)
                revealCur.gameObject.SetActive(true);
            _windowFlowPhase[windowIndex] = 2;
        }

        yield return new WaitUntil(() => _windowFlowPhase[windowIndex] >= 2 || _fail2TimeExpired);
        if (_fail2TimeExpired)
            yield break;
        yield return new WaitUntil(() => _windowFlowPhase[windowIndex] >= 3 || _fail2TimeExpired);
        if (_fail2TimeExpired)
            yield break;
        yield return new WaitUntil(() => _windowFlowPhase[windowIndex] >= 4 || _fail2TimeExpired);
    }

    void OnWindowClicked(int index)
    {
        if (!_isOpen)
            return;
        if (index < 0 || index >= windows.Count)
            return;
        if (IsRescueInteractionBlocked())
            return;
        var w = windows[index];
        if (!IsWindowHotspotActive(index))
            return;

        PlayFacadeUiClick();

        int p = index >= 0 && index < _activePersonInWindow.Length ? _activePersonInWindow[index] : 0;
        if (_resumeDetailsOnWindowClick[index])
        {
            _resumeDetailsOnWindowClick[index] = false;
            GameObject detailsResume = GetWindowDetailsObject(index, p);
            if (detailsResume != null)
                detailsResume.SetActive(true);
            var revealResume = ResolveRevealButtonForPerson(index, p);
            if (revealResume != null)
                revealResume.gameObject.SetActive(true);
            if (w.windowButton != null)
                w.windowButton.interactable = false;
            _windowFlowPhase[index] = 2;
            return;
        }

        GameObject intro = GetWindowIntroObject(index);
        if (intro != null)
            intro.SetActive(true);
        var probe = ResolveProbeButtonForWindow(index);
        if (probe != null)
            probe.gameObject.SetActive(true);
        if (w.windowButton != null)
            w.windowButton.interactable = false;
        _windowFlowPhase[index] = 1;
    }

    void OnDetailsBackToWindowClicked(int windowIndex, int personIndex)
    {
        if (!_isOpen || windowIndex < 0 || windowIndex >= windows.Count)
            return;
        if (IsRescueInteractionBlocked())
            return;
        if (windowIndex < 0 || windowIndex >= _activePersonInWindow.Length)
            return;

        int activePerson = _activePersonInWindow[windowIndex];
        if (activePerson != personIndex)
            return;

        var w = windows[windowIndex];
        PlayFacadeUiClick();
        SetChoicesVisible(w, false);
        GameObject details = GetWindowDetailsObject(windowIndex, personIndex);
        if (details != null)
            details.SetActive(false);
        var reveal = ResolveRevealButtonForPerson(windowIndex, personIndex);
        if (reveal != null)
            reveal.gameObject.SetActive(false);
        var probe = ResolveProbeButtonForWindow(windowIndex);
        if (probe != null)
            probe.gameObject.SetActive(false);

        _resumeDetailsOnWindowClick[windowIndex] = true;
        _windowFlowPhase[windowIndex] = 0;
        SetOnlyWindowHotspotActive(windowIndex);
    }

    void OnProbeClicked(int index)
    {
        if (!_isOpen || index < 0 || index >= windows.Count)
            return;
        if (IsRescueInteractionBlocked())
            return;
        var w = windows[index];
        // 仅「已点窗户、尚未打开 details」阶段响应；同窗下一人由协程直接进 phase 2，避免误点 probe 重复执行。
        if (_windowFlowPhase[index] != 1)
            return;

        PlayFacadeUiClick();

        int p = index >= 0 && index < _activePersonInWindow.Length ? _activePersonInWindow[index] : 0;
        GameObject details = GetWindowDetailsObject(index, p);
        if (details != null)
            details.SetActive(true);
        var reveal = ResolveRevealButtonForPerson(index, p);
        if (reveal != null)
            reveal.gameObject.SetActive(true);
        _windowFlowPhase[index] = 2;
    }

    void OnRevealChoicesClicked(int index)
    {
        if (!_isOpen || index < 0 || index >= windows.Count)
            return;
        if (IsRescueInteractionBlocked())
            return;
        var w = windows[index];
        if (_windowFlowPhase[index] < 2)
            return;

        PlayFacadeUiClick();

        SetChoicesVisible(w, true);
        _windowFlowPhase[index] = 3;
    }

    void OnEscapeChoice(int index, EscapeRouteKind route)
    {
        if (!_isOpen || index < 0 || index >= windows.Count)
            return;
        var w = windows[index];
        if (_windowFlowPhase[index] < 3)
            return;
        if (IsRescueInteractionBlocked())
            return;

        int p = index >= 0 && index < _activePersonInWindow.Length ? _activePersonInWindow[index] : 0;

        if (MustUseElevatorOnlyForWrong1Slide(index, p) &&
            (route == EscapeRouteKind.SlideLeft || route == EscapeRouteKind.SlideRight))
        {
            PlayFacadeUiClick();
            PlayEscapeChoiceWrongHint();
            ApplyWrongSelectionTimePenalty(10f);
            if (wrong1Panel == null)
            {
                Debug.LogWarning(
                    $"{nameof(FacadeRescueMiniGameController)}: 当前窗/人需选电梯逃生但未指定 wrong1Panel。",
                    this);
                return;
            }

            _wrong1BlockingWindow = index;
            wrong1Panel.SetActive(true);
            SetChoicesVisible(w, false);
            GameObject det = GetWindowDetailsObject(index, p);
            if (det != null)
                det.SetActive(false);
            return;
        }

        if (MustUseRightSlideOnlyForWrong1(index, p) &&
            (route == EscapeRouteKind.SlideLeft || route == EscapeRouteKind.Elevator))
        {
            PlayFacadeUiClick();
            PlayEscapeChoiceWrongHint();
            ApplyWrongSelectionTimePenalty(10f);
            if (wrong1Panel == null)
            {
                Debug.LogWarning(
                    $"{nameof(FacadeRescueMiniGameController)}: 当前窗/人需选右滑杆逃生但未指定 wrong1Panel。",
                    this);
                return;
            }

            _wrong1BlockingWindow = index;
            wrong1Panel.SetActive(true);
            SetChoicesVisible(w, false);
            GameObject detRight = GetWindowDetailsObject(index, p);
            if (detRight != null)
                detRight.SetActive(false);
            return;
        }

        if (MustUseLeftSlideOnlyForWrong2(index, p) && route != EscapeRouteKind.SlideLeft)
        {
            PlayFacadeUiClick();
            PlayEscapeChoiceWrongHint();
            ApplyWrongSelectionTimePenalty(10f);
            if (wrong2Panel == null)
            {
                Debug.LogWarning(
                    $"{nameof(FacadeRescueMiniGameController)}: 当前窗/人需选左滑杆逃生但未指定 wrong2Panel。",
                    this);
                return;
            }

            _wrong2BlockingWindow = index;
            wrong2Panel.SetActive(true);
            SetChoicesVisible(w, false);
            GameObject det2 = GetWindowDetailsObject(index, p);
            if (det2 != null)
                det2.SetActive(false);
            return;
        }

        PlayFacadeUiClick();
        PlayEscapeChoiceCorrectHint();

        if ((route == EscapeRouteKind.SlideLeft || route == EscapeRouteKind.SlideRight) &&
            slideChoiceClip != null &&
            sfxSource != null)
            sfxSource.PlayOneShot(slideChoiceClip);

        SetChoicesVisible(w, false);
        var reveal = ResolveRevealButtonForPerson(index, p);
        if (reveal != null)
            reveal.gameObject.SetActive(false);
        var probe = ResolveProbeButtonForWindow(index);
        if (probe != null)
            probe.gameObject.SetActive(false);
        HideIntroDetailsForPerson(index, p);
        if (w.windowButton != null)
            w.windowButton.interactable = false;
        if (portraitSlots != null && _portraitAllowedForSlotByElevatorEscape != null)
        {
            int slot = FlatPortraitSlotIndex(index, p);
            if (slot >= 0 && slot < _portraitAllowedForSlotByElevatorEscape.Length)
                _portraitAllowedForSlotByElevatorEscape[slot] = route == EscapeRouteKind.Elevator;
        }

        if (index == 0 && p == 0 &&
            (route == EscapeRouteKind.SlideLeft || route == EscapeRouteKind.SlideRight))
            _fail1PendingRestart = true;

        _windowFlowPhase[index] = 4;
    }

    bool IsWindowHotspotActive(int index)
    {
        for (int i = 0; i < windows.Count; i++)
        {
            var b = windows[i].windowButton;
            if (b == null)
                continue;
            if (i == index)
                return b.interactable && b.gameObject.activeSelf;
        }

        return false;
    }

    void SetOnlyWindowHotspotActive(int activeIndex)
    {
        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (w.windowButton == null)
                continue;
            bool on = i == activeIndex;
            w.windowButton.gameObject.SetActive(on);
            w.windowButton.interactable = on;
        }
    }

    void HideAllWindowUi()
    {
        _wrong1BlockingWindow = -1;
        if (wrong1Panel != null)
            wrong1Panel.SetActive(false);
        _wrong2BlockingWindow = -1;
        if (wrong2Panel != null)
            wrong2Panel.SetActive(false);
        if (fail1Panel != null)
            fail1Panel.SetActive(false);
        if (fail2Panel != null)
            fail2Panel.SetActive(false);
        if (successPanel != null)
            successPanel.SetActive(false);
        _successFlowActive = false;

        for (int i = 0; i < _activePersonInWindow.Length; i++)
            _activePersonInWindow[i] = 0;
        for (int i = 0; i < _resumeDetailsOnWindowClick.Length; i++)
            _resumeDetailsOnWindowClick[i] = false;
        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            SetChoicesVisible(w, false);
            HideAllIntroDetailsForWindow(i);
            _windowFlowPhase[i] = 0;
            if (w.windowButton != null)
            {
                w.windowButton.gameObject.SetActive(false);
                w.windowButton.interactable = false;
            }
        }
    }

    void ClearPortraits()
    {
        if (portraitSlots == null)
            return;
        foreach (var img in portraitSlots)
        {
            if (img == null)
                continue;
            img.enabled = false;
        }
    }

    int FlatPortraitSlotIndex(int windowIndex, int personIndex)
    {
        int sum = 0;
        for (int i = 0; i < windowIndex; i++)
            sum += GetPeopleCountAtWindow(i);
        return sum + personIndex;
    }

    void ApplyPortraitForWindowPerson(int windowIndex, int personIndex)
    {
        if (portraitSlots == null || windowIndex < 0 || windowIndex >= windows.Count)
            return;
        int slot = FlatPortraitSlotIndex(windowIndex, personIndex);
        if (slot < 0 || slot >= portraitSlots.Length)
            return;
        var img = portraitSlots[slot];
        if (img == null)
            return;
        img.enabled = true;
    }

    /// <summary>正确提示音时长 + 1 秒后再动电梯（首轮抵达窗 0 前不调用）。</summary>
    IEnumerator CoWaitAfterCorrectHintBeforeElevator()
    {
        float hintLen = escapeChoiceCorrectHintClip != null ? escapeChoiceCorrectHintClip.length : 0f;
        float total = hintLen + 1f;
        if (total <= 0f)
            yield break;
        float waited = 0f;
        while (waited < total && !_fail2TimeExpired)
        {
            float dt = _sessionUsesUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            waited += dt;
            yield return null;
        }
    }

    IEnumerator CoAwaitFail2RestartClick()
    {
        Button failBtn = fail2RestartButton;
        if (failBtn == null && fail2Panel != null)
            failBtn = fail2Panel.GetComponentInChildren<Button>(true);

        if (fail2Panel != null && failBtn != null)
        {
            if (!fail2Panel.activeSelf)
            {
                fail2Panel.SetActive(true);
                PlayFail1OrFail2PanelOpenSfx();
            }

            _fail2RestartClicked = false;
            yield return new WaitUntil(() => _fail2RestartClicked);
        }
        else if (fail2Panel != null)
        {
            Debug.LogWarning(
                $"{nameof(FacadeRescueMiniGameController)}: fail2Panel 已配置但找不到 Button，将直接重新开始救援回合。",
                this);
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(FacadeRescueMiniGameController)}: 超时失败但未配置 fail2Panel，将直接重新开始救援回合。",
                this);
        }

        if (fail2Panel != null)
            fail2Panel.SetActive(false);
        _fail2RestartClicked = false;
        _fail2TimeExpired = false;
    }

    IEnumerator CoTweenElevatorY(float targetY)
    {
        BeginElevatorMoveSound();
        try
        {
            float t = 0f;
            Vector2 start = elevatorRect.anchoredPosition;
            Vector2 end = new Vector2(start.x, targetY);
            float dur = Mathf.Max(0.05f, elevatorMoveSeconds);
            while (t < dur)
            {
                if (_fail2TimeExpired)
                    yield break;
                t += _sessionUsesUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                elevatorRect.anchoredPosition = Vector2.Lerp(start, end, u);
                yield return null;
            }

            elevatorRect.anchoredPosition = end;
        }
        finally
        {
            EndElevatorMoveSound();
        }
    }

    void BeginElevatorMoveSound()
    {
        EndElevatorMoveSound();
        var src = sfxSource;
        if (elevatorMoveClip == null || src == null)
            return;
        _elevatorRestoreClip = src.clip;
        _elevatorRestoreLoop = src.loop;
        src.loop = true;
        src.clip = elevatorMoveClip;
        src.Play();
        _elevatorMoveSoundPlaying = true;
    }

    void EndElevatorMoveSound()
    {
        if (!_elevatorMoveSoundPlaying || sfxSource == null)
            return;
        sfxSource.Stop();
        sfxSource.loop = _elevatorRestoreLoop;
        sfxSource.clip = _elevatorRestoreClip;
        _elevatorMoveSoundPlaying = false;
    }

    void PlayFacadeUiClick()
    {
        if (facadeUiClickClip != null && sfxSource != null)
            sfxSource.PlayOneShot(facadeUiClickClip);
    }

    void PlayFail1OrFail2PanelOpenSfx()
    {
        if (sfxSource == null)
            return;
        if (fail1OrFail2PanelShowClip == null && fail1OrFail2PanelShowClipFollowUp == null)
            return;
        StartCoroutine(CoPlayFail1OrFail2PanelOpenSfxChain());
    }

    void ApplyWrongSelectionTimePenalty(float seconds)
    {
        if (!_isOpen || _fail2TimeExpired || seconds <= 0f)
            return;
        _facadeRescueSecondsLeft = Mathf.Max(0f, _facadeRescueSecondsLeft - seconds);
        RefreshFacadeRescueCountdownLabel();
        if (_facadeRescueSecondsLeft > 0f)
            return;

        _fail2TimeExpired = true;
        _wrong1BlockingWindow = -1;
        if (wrong1Panel != null)
            wrong1Panel.SetActive(false);
        _wrong2BlockingWindow = -1;
        if (wrong2Panel != null)
            wrong2Panel.SetActive(false);
        if (fail2Panel != null)
        {
            fail2Panel.SetActive(true);
            PlayFail1OrFail2PanelOpenSfx();
        }
    }

    IEnumerator CoPlayFail1OrFail2PanelOpenSfxChain()
    {
        if (fail1OrFail2PanelShowClip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(fail1OrFail2PanelShowClip);
            float w = fail1OrFail2PanelShowClip.length;
            if (w > 0f)
            {
                if (_sessionUsesUnscaledTime)
                    yield return new WaitForSecondsRealtime(w);
                else
                    yield return new WaitForSeconds(w);
            }
        }

        if (fail1OrFail2PanelShowClipFollowUp != null && sfxSource != null)
            sfxSource.PlayOneShot(fail1OrFail2PanelShowClipFollowUp);
    }

    void PlayEscapeChoiceCorrectHint()
    {
        if (escapeChoiceCorrectHintClip != null && sfxSource != null)
            sfxSource.PlayOneShot(escapeChoiceCorrectHintClip);
    }

    void PlayEscapeChoiceWrongHint()
    {
        if (escapeChoiceWrongHintClip != null && sfxSource != null)
            sfxSource.PlayOneShot(escapeChoiceWrongHintClip);
    }
}
