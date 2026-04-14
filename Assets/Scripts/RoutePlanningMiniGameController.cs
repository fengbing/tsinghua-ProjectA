using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(-100)]
public class RoutePlanningMiniGameController : MonoBehaviour
{
    /// <summary>规划全屏打开时为 true；供 <see cref="GameUi"/> 等忽略 Esc 打开其它面板。</summary>
    public static bool IsPlanningUiOpen { get; private set; }
    [System.Serializable]
    public class NodeBinding
    {
        public string nodeId;
        public Button button;
        public RectTransform clickArea;
        public RectTransform uiPoint;
        public Image stateImage;
        public Transform sceneWaypoint;
    }

    [System.Serializable]
    public class SegmentRouteConfig
    {
        [Tooltip("起点 ID（start 或某 nodeId）。")]
        public string fromNodeId;
        [Tooltip("终点 ID（end 或某 nodeId）。")]
        public string toNodeId;
        [FormerlySerializedAs("speedScale")]
        [Tooltip("该段风速：正数顺风、负数逆风。换算：speedScale = 1 + 0.05 * windSpeed。")]
        public float windSpeed = 0f;
    }

    [System.Serializable]
    public class RouteGradeVoice
    {
        [Tooltip("档位名称（例如 D/C/B/A/S）。")]
        public string gradeName = "Grade";
        [Tooltip("该档位确认后播报语音。")]
        public AudioClip voiceClip;
        [TextArea(2, 4)]
        [Tooltip("该档位确认后播报文本（留空则仅语音）。")]
        public string dialogText;
    }

    [Header("Mode")]
    [SerializeField] KeyCode toggleKey = KeyCode.H;
    [SerializeField] GameObject fullscreenRoot;

    /// <summary>路线规划全屏 UI 根 Rect（一般为 RoutePlannerRoot），供立面救援等与主 HUD 对齐布局。</summary>
    public RectTransform RoutePlannerHudRootRect =>
        fullscreenRoot != null ? fullscreenRoot.transform as RectTransform : null;

    [SerializeField] PlaneController planeController;
    [SerializeField] DroneAutocruiseController autocruiseController;

    [Header("Path Endpoints (可与子物体同名 start / end 自动匹配)")]
    [SerializeField] RectTransform startUiPoint;
    [SerializeField] RectTransform endUiPoint;
    [SerializeField] Transform startSceneWaypoint;
    [SerializeField] Transform endSceneWaypoint;
    [SerializeField] Button endButton;
    [SerializeField] RectTransform endClickArea;

    [Header("途径点（运行时用 Waypoint 叶子名自动填充，无需手填）")]
    [HideInInspector]
    [SerializeField] List<NodeBinding> nodes = new List<NodeBinding>();
    [Header("Click Area Automation")]
    [SerializeField] bool autoCreateClickAreas = true;
    [SerializeField] Vector2 defaultClickAreaSize = new Vector2(120f, 120f);
    [Tooltip("按圆点 Rect 自动算热区：至少为「可见边长 + 双侧内边距」，且不小于下面最小边长。缩小圆点后仍易点。")]
    [SerializeField] bool smartClickAreaSize = true;
    [SerializeField] float clickAreaPadding = 24f;
    [SerializeField] float clickAreaMinSide = 96f;
    [Tooltip("0 表示不限制。防止热区过大时可选上限。")]
    [SerializeField] float clickAreaMaxSide;
    [Tooltip("在编辑模式下为自动热区显示半透明预览（Scene / Game 视图不播放时）。")]
    [FormerlySerializedAs("showClickAreas")]
    [SerializeField] bool showClickAreasInEditor = true;
    [Tooltip("运行时也显示热区半透明预览（调试用；正式发布建议关闭）。")]
    [SerializeField] bool showClickAreasInPlayMode;
    [SerializeField] bool autoPopulateNodesFromWaypointNames = true;
    [SerializeField] Transform waypointRoot;

    [Header("Line Rendering")]
    [SerializeField] RectTransform segmentContainer;
    [SerializeField] Image segmentPrefab;
    [SerializeField] float segmentThickness = 6f;

    [Header("连线规则")]
    [Tooltip("勾选后：上一位置与目标在 Segment Container 下的图标中心距离须 ≤ 下方阈值，否则不能连线")]
    [SerializeField] bool enforceMaxWaypointIconConnectDistance = true;
    [Tooltip("两途径点图标中心距离超过此值（与 Segment Container 同坐标单位，一般为像素量级）则不可连线")]
    [SerializeField] float maxWaypointIconConnectDistance = 200f;
    [Tooltip("勾选后：线段附近不能有其它途径点图标（防跨点直连）；距离阈值见下")]
    [SerializeField] bool requireNoOtherWaypointOnSegment = true;
    [Tooltip("其它途径点圆心到当前线段的距离小于此值则视为「挡在两点之间」")]
    [SerializeField] float waypointSegmentBlockRadius = 36f;
    [Tooltip("勾选后：连线不能穿过 zuli 节点下的任何图片区域。")]
    [SerializeField] bool blockSegmentThroughZuliImages = true;
    [Tooltip("留空则自动使用 fullscreenRoot 下名为 zuli 的 RectTransform。")]
    [SerializeField] RectTransform zuliObstacleRoot;
    [Tooltip("无效点击（未绑定、跨点等）时红点提示时长（秒，不受 timeScale 影响）")]
    [SerializeField] float invalidNodeFlashDuration = 0.35f;

    [Header("分段速度与时间评分（由你配置）")]
    [Tooltip("每条可连线段的风速配置；未配置时使用下方默认值。")]
    [SerializeField] List<SegmentRouteConfig> segmentRouteConfigs = new List<SegmentRouteConfig>();
    [Tooltip("无人机基础巡航速度（米/秒）回退值：读不到 PlaneController 时使用。每段实际速度 = 该值 * speedScale。")]
    [SerializeField] float baseCruiseSpeedMetersPerSecond = 16f;
    [FormerlySerializedAs("defaultSegmentSpeedScale")]
    [SerializeField] float defaultSegmentWindSpeed = 0f;
    [Tooltip("评分时优先读取 PlaneController 的真实峰值巡航速度参数（maxSpeed * boostSpeedMultiplier）。")]
    [SerializeField] bool usePlaneControllerCruiseSpeedForRating = true;
    [Tooltip("用于按“路线总数平均划分”分档：填入所有候选路线总时间（秒），系统按数量平均分成 5 档。")]
    [SerializeField] List<float> routeTotalTimeSamples = new List<float>();
#if UNITY_EDITOR
    [Tooltip("编辑器开关：勾选后按当前节点自动生成分段模板（start->node、node->node、node->end），随后自动复位。")]
    [SerializeField] bool regenerateSegmentTemplateNow;
#endif

    [Header("Visual States")]
    [SerializeField] Color idleColor = new Color(0.65f, 0.9f, 1f, 1f);
    [Tooltip("起点 / 终点未被“激活”时的颜色（深蓝，与途径点区分）。")]
    [SerializeField] Color endpointIdleColor = new Color(0.08f, 0.25f, 0.55f, 1f);
    [SerializeField] Color selectedColor = new Color(1f, 0.55f, 0.8f, 1f);
    [SerializeField] Color invalidColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] Color lineColor = new Color(1f, 0.55f, 0.8f, 1f);
    [SerializeField] Sprite pointCircleSprite;
    [Header("Waypoint Idle — 呼吸（仅途径点，不含 start/end）")]
    [SerializeField] float breathCycleSeconds = 2.8f;
    [Range(0f, 1f)]
    [SerializeField] float breathAlphaMin = 0.28f;
    [Range(0f, 1f)]
    [SerializeField] float breathAlphaMax = 1f;

    [Header("Actions")]
    [SerializeField] Button confirmButton;
    [SerializeField] Button cancelButton;
    [SerializeField] Text feedbackText;
    [SerializeField] bool autoCloseOnConfirm = true;
    [SerializeField] bool startCruiseOnConfirm;
    [Header("评级弹窗分流")]
    [Tooltip("A/S/SS 档位的 keep 按钮（可不填，运行时自动按名称查找）。")]
    [SerializeField] Button gradeKeepButtonA;
    [SerializeField] Button gradeKeepButtonS;
    [SerializeField] Button gradeKeepButtonSS;
    [Tooltip("B/C 档位的 restart 按钮（可不填，运行时自动按名称查找）。")]
    [SerializeField] Button gradeRestartButtonA;
    [SerializeField] Button gradeRestartButtonS;
    [SerializeField] Button gradeRestartButtonB;
    [SerializeField] Button gradeRestartButtonC;
    [SerializeField] GameObject gradePanelA;
    [SerializeField] GameObject gradePanelS;
    [SerializeField] GameObject gradePanelSS;
    [SerializeField] GameObject gradePanelB;
    [SerializeField] GameObject gradePanelC;
    [Header("风场背景切换")]
    [Tooltip("风场切换按钮（留空则自动在 fullscreenRoot 下查找名为 fengchang 的 Button）。")]
    [SerializeField] Button fengchangButton;
    [Tooltip("默认背景（风场关闭时显示）。")]
    [SerializeField] GameObject defaultBackgroundRoot;
    [Tooltip("风场背景（风场开启时显示）。")]
    [SerializeField] GameObject fengchangBackgroundRoot;
    [SerializeField] Color fengchangPointAndLineColor = new Color(1f, 0.25f, 0.25f, 1f);
    [Header("Cursor")]
    [SerializeField] Texture2D plannerCursorTexture;
    [SerializeField] Vector2 plannerCursorHotspot = Vector2.zero;

    /// <summary>供立面救援等小游戏复用与规划界面相同的手型贴图。</summary>
    public Texture2D PlannerCursorTexture => plannerCursorTexture;

    /// <summary>与 <see cref="PlannerCursorTexture"/> 配套的热点。</summary>
    public Vector2 PlannerCursorHotspot => plannerCursorHotspot;

    [Header("Audio")]
    [Tooltip("途径点 / 终点 / 确认 / 取消 等规划 UI 按钮按下时播放；留空则不发声")]
    [SerializeField] AudioClip planningButtonClickClip;
    [Tooltip("留空则使用本物体上的 AudioSource，没有再添加一个")]
    [SerializeField] AudioSource planningButtonAudio;
    [Range(0f, 1f)]
    [SerializeField] float planningClickVolume = 1f;

    [Header("路线语音与确认后巡航")]
    [Tooltip("勾选后：确认时只写入路线不立即起飞；依次播放确认语音→（可选）关闭面板→过渡音频→再启动自动巡航；抵达终点再播抵达语音。此时会忽略 startCruiseOnConfirm。")]
    [SerializeField] bool usePostConfirmNarrationSequence;
    [Tooltip("进入规划界面时播放；留空则跳过")]
    [SerializeField] AudioClip enterPlanningVoiceClip;
    [Tooltip("进入规划界面第二段语音（与第一段分开播报）。")]
    [SerializeField] AudioClip enterPlanningVoiceClip2;
    [Tooltip("规划阶段每生成一段连线时播放；留空则跳过。确认后重建连线不会播放。")]
    [SerializeField] AudioClip segmentLineConnectClip;
    [Range(0f, 1f)]
    [SerializeField] float segmentConnectVolume = 1f;
    [Tooltip("5 档评级对应语音/文本（最后一档视为最优解）。")]
    [SerializeField] List<RouteGradeVoice> routeGradeVoices = new List<RouteGradeVoice>();
    [Tooltip("弹出 A/S/SS 评级面板时播放的音效。")]
    [SerializeField] AudioClip highGradePopupSfx;
    [Tooltip("弹出 B/C 评级面板时播放的音效。")]
    [SerializeField] AudioClip lowGradePopupSfx;
    [Range(0f, 1f)]
    [SerializeField] float gradePopupSfxVolume = 1f;
    [Tooltip("关闭规划 UI 后、启动巡航前播放")]
    [SerializeField] AudioClip afterPlannerCloseClip;
    [Tooltip("非循环路线飞抵最后一个航点后播放")]
    [SerializeField] AudioClip arrivalVoiceClip;
    [Range(0f, 1f)]
    [SerializeField] float routeNarrationVolume = 1f;
    [Tooltip("语音与较长音效；留空则在需要时自动添加第二个 AudioSource，避免与按钮点击互抢")]
    [SerializeField] AudioSource routeNarrationAudio;

    [Header("系统对话框（与 Scene Entry 同款黑底打字）")]
    [Tooltip("可拖 SystemDialogController 或 SystemDialogController2；留空则运行时按场景查找（优先 2 再 1）")]
    [SerializeField] Component systemDialog;
    [Tooltip("勾选后按语音长度自动摊字间隔（与 SceneEntryDialogTrigger 一致）")]
    [SerializeField] bool routeDialogAutoFitToVoiceEnd;
    [SerializeField] float routeDialogExtraSecondsAfterVoice = 1f;
    [Tooltip("未勾选自动适配时：逐字间隔；≤0 用 0.04s")]
    [SerializeField] float routeDialogCharacterInterval;
    [TextArea(2, 4)]
    [Tooltip("非空则本句用系统对话框播语音；留空则仅用上方 AudioClip")]
    [SerializeField] string enterPlanningDialogText;
    [TextArea(2, 4)]
    [Tooltip("进入规划时第二句（与第一句分开播报）。")]
    [SerializeField] string enterPlanningDialogText2;
    [TextArea(2, 4)]
    [SerializeField] string afterPlannerCloseDialogText;
    [TextArea(2, 4)]
    [SerializeField] string arrivalDialogText;

    [Header("规划中的地图与暂停")]
    [Tooltip("留空则运行时 FindFirstObjectByType")]
    [SerializeField] MinimapUiController minimapUi;
    [SerializeField] bool hideMinimapWhilePlanning = true;

    readonly List<int> _selectedNodeIndices = new List<int>();
    readonly HashSet<int> _invalidNodeIndices = new HashSet<int>();
    readonly List<GameObject> _spawnedSegments = new List<GameObject>();
    bool _isOpen;
    bool _routeFinalized;
    bool _planeInputBeforeOpen;
    bool _endPointPressed;
    Image _startPointImage;
    Image _endPointImage;
    bool _listenersBound;
    CursorLockMode _cursorLockBeforePlanner;
    bool _cursorVisibleBeforePlanner;
    bool _confirmSequenceRunning;
    bool _fengchangActive;
    bool _awaitingGradeDecision;
    bool _gradeNarrationAlreadyPlayed;
    Coroutine _gradePopupNarrationRoutine;
    RouteEvaluationResult _pendingRouteEval;
    List<Transform> _pendingRouteWaypoints;
    List<float> _pendingSegmentSpeedScales;
    Coroutine _openFromSceneEntryRoutine;
    readonly Dictionary<int, Coroutine> _invalidFlashByNode = new Dictionary<int, Coroutine>();
    readonly Dictionary<string, SegmentRouteConfig> _segmentConfigByDirectedKey = new Dictionary<string, SegmentRouteConfig>();

    struct RouteEvaluationResult
    {
        public float totalTime;
        public int gradeIndex;
        public string gradeName;
    }

    ISystemDialogPresentation ResolveSystemDialogPresentation()
    {
        if (systemDialog is ISystemDialogPresentation p)
            return p;
        return SystemDialogLocator.FindPresentation();
    }

    void Awake()
    {
        if (Application.isPlaying)
        {
            if (planeController == null)
                planeController = FindFirstObjectByType<PlaneController>();
            if (autocruiseController == null)
                autocruiseController = FindFirstObjectByType<DroneAutocruiseController>();
            if (systemDialog == null)
                systemDialog = SystemDialogLocator.FindComponent();
            if (minimapUi == null)
                minimapUi = FindFirstObjectByType<MinimapUiController>();
            EnsurePlanningClickAudio();
            EnsureRouteNarrationAudio();
        }

        AutoPopulateNodesFromScene();
        BindEndpointUiFromNamedChildren();
        EnsureClickAreas();
        RebuildSegmentRouteConfigCache();

        if (Application.isPlaying)
        {
            BindRuntimeUiListeners();
            if (fullscreenRoot != null)
                fullscreenRoot.SetActive(false);
            SetFeedback(string.Empty);
        }
    }

    void OnDestroy()
    {
        StopInvalidFlashCoroutines();
        if (!Application.isPlaying || !_listenersBound)
            return;

        for (int i = 0; i < nodes.Count; i++)
        {
            Button b = ResolveNodeButton(i);
            if (b != null)
                b.onClick.RemoveAllListeners();
        }

        confirmButton?.onClick.RemoveListener(OnConfirmClicked);
        cancelButton?.onClick.RemoveListener(CancelAndReset);
        endButton?.onClick.RemoveListener(OnEndPointClicked);
        fengchangButton?.onClick.RemoveListener(ToggleFengchangVisualMode);
        gradeKeepButtonA?.onClick.RemoveListener(OnGradeKeepClicked);
        gradeKeepButtonS?.onClick.RemoveListener(OnGradeKeepClicked);
        gradeKeepButtonSS?.onClick.RemoveListener(OnGradeKeepClicked);
        gradeRestartButtonA?.onClick.RemoveListener(OnGradeRestartClicked);
        gradeRestartButtonS?.onClick.RemoveListener(OnGradeRestartClicked);
        gradeRestartButtonB?.onClick.RemoveListener(OnGradeRestartClicked);
        gradeRestartButtonC?.onClick.RemoveListener(OnGradeRestartClicked);
        if (autocruiseController != null)
            autocruiseController.OnAutocruiseRouteCompleted -= OnAutocruiseRouteCompletedPlayArrivalClip;
        _listenersBound = false;
    }

    void BindRuntimeUiListeners()
    {
        if (_listenersBound)
            return;
        _listenersBound = true;

        for (int i = 0; i < nodes.Count; i++)
        {
            int index = i;
            Button clickButton = ResolveNodeButton(i);
            if (clickButton != null)
                clickButton.onClick.AddListener(() => OnNodeClicked(index));
            ApplyCircleSprite(nodes[i].stateImage);
            SetNodeVisual(i, idleColor);
        }

        _startPointImage = startUiPoint != null ? startUiPoint.GetComponent<Image>() : null;
        _endPointImage = endUiPoint != null ? endUiPoint.GetComponent<Image>() : null;
        ApplyCircleSprite(_startPointImage);
        ApplyCircleSprite(_endPointImage);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelAndReset);
        TryAutoBindFengchangButtonAndBackgrounds();
        TryAutoBindGradePanelsAndButtons();
        if (fengchangButton != null)
            fengchangButton.onClick.AddListener(ToggleFengchangVisualMode);
        if (gradeKeepButtonA != null)
            gradeKeepButtonA.onClick.AddListener(OnGradeKeepClicked);
        if (gradeKeepButtonS != null)
            gradeKeepButtonS.onClick.AddListener(OnGradeKeepClicked);
        if (gradeKeepButtonSS != null)
            gradeKeepButtonSS.onClick.AddListener(OnGradeKeepClicked);
        if (gradeRestartButtonA != null)
            gradeRestartButtonA.onClick.AddListener(OnGradeRestartClicked);
        if (gradeRestartButtonS != null)
            gradeRestartButtonS.onClick.AddListener(OnGradeRestartClicked);
        if (gradeRestartButtonB != null)
            gradeRestartButtonB.onClick.AddListener(OnGradeRestartClicked);
        if (gradeRestartButtonC != null)
            gradeRestartButtonC.onClick.AddListener(OnGradeRestartClicked);
        if (endButton != null)
            endButton.onClick.AddListener(OnEndPointClicked);
        else if (endUiPoint != null)
        {
            endButton = endUiPoint.GetComponent<Button>();
            endButton?.onClick.AddListener(OnEndPointClicked);
        }

        ApplyEndpointVisuals();
        ApplyFengchangVisualState();
        HideAllGradePanels();
    }

    void TryAutoBindFengchangButtonAndBackgrounds()
    {
        if (fullscreenRoot == null)
            return;
        if (fengchangButton == null)
        {
            Transform t = fullscreenRoot.transform.Find("fengchang");
            if (t != null)
                fengchangButton = t.GetComponent<Button>();
        }

        if (defaultBackgroundRoot == null)
        {
            Transform t = fullscreenRoot.transform.Find("beijing");
            if (t != null)
                defaultBackgroundRoot = t.gameObject;
        }

        if (fengchangBackgroundRoot == null)
        {
            Transform t = fullscreenRoot.transform.Find("fengchangbeijing");
            if (t != null)
                fengchangBackgroundRoot = t.gameObject;
        }
    }

    void TryAutoBindGradePanelsAndButtons()
    {
        if (fullscreenRoot == null)
            return;
        Transform root = fullscreenRoot.transform;
        if (gradePanelA == null)
            gradePanelA = root.Find("A")?.gameObject;
        if (gradePanelS == null)
            gradePanelS = root.Find("S")?.gameObject;
        if (gradePanelSS == null)
            gradePanelSS = root.Find("SS")?.gameObject;
        if (gradePanelB == null)
            gradePanelB = root.Find("B")?.gameObject;
        if (gradePanelC == null)
            gradePanelC = root.Find("C")?.gameObject;

        gradeKeepButtonA ??= gradePanelA != null ? gradePanelA.transform.Find("keep")?.GetComponent<Button>() : null;
        gradeKeepButtonS ??= gradePanelS != null ? gradePanelS.transform.Find("keep")?.GetComponent<Button>() : null;
        gradeRestartButtonA ??= gradePanelA != null ? gradePanelA.transform.Find("restart")?.GetComponent<Button>() : null;
        gradeRestartButtonS ??= gradePanelS != null ? gradePanelS.transform.Find("restart")?.GetComponent<Button>() : null;
        if (gradeKeepButtonSS == null && gradePanelSS != null)
        {
            gradeKeepButtonSS = gradePanelSS.transform.Find("keep")?.GetComponent<Button>();
            if (gradeKeepButtonSS == null)
                gradeKeepButtonSS = gradePanelSS.transform.Find("keep ")?.GetComponent<Button>();
        }

        gradeRestartButtonB ??= gradePanelB != null ? gradePanelB.transform.Find("restart")?.GetComponent<Button>() : null;
        gradeRestartButtonC ??= gradePanelC != null ? gradePanelC.transform.Find("restart")?.GetComponent<Button>() : null;
    }

    void HideAllGradePanels()
    {
        if (gradePanelA != null) gradePanelA.SetActive(false);
        if (gradePanelS != null) gradePanelS.SetActive(false);
        if (gradePanelSS != null) gradePanelSS.SetActive(false);
        if (gradePanelB != null) gradePanelB.SetActive(false);
        if (gradePanelC != null) gradePanelC.SetActive(false);
    }

    void ShowGradePanelForEval(RouteEvaluationResult eval)
    {
        HideAllGradePanels();
        switch (eval.gradeIndex)
        {
            case 4:
                if (gradePanelSS != null) gradePanelSS.SetActive(true);
                break;
            case 3:
                if (gradePanelS != null) gradePanelS.SetActive(true);
                break;
            case 2:
                if (gradePanelA != null) gradePanelA.SetActive(true);
                break;
            case 1:
                if (gradePanelB != null) gradePanelB.SetActive(true);
                break;
            default:
                if (gradePanelC != null) gradePanelC.SetActive(true);
                break;
        }
    }

    AudioClip ResolveGradePopupSfx(int gradeIndex)
    {
        return gradeIndex >= 2 ? highGradePopupSfx : lowGradePopupSfx;
    }

    IEnumerator PlayGradePopupThenNarration(RouteEvaluationResult eval)
    {
        EnsureRouteNarrationAudio();
        AudioClip popupSfx = ResolveGradePopupSfx(eval.gradeIndex);
        if (popupSfx != null && routeNarrationAudio != null)
        {
            routeNarrationAudio.PlayOneShot(popupSfx, gradePopupSfxVolume);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, popupSfx.length));
        }

        RouteGradeVoice gradeVoice = ResolveGradeVoice(eval.gradeIndex);
        bool hasGradeNarration = gradeVoice != null
            && (gradeVoice.voiceClip != null || !string.IsNullOrWhiteSpace(gradeVoice.dialogText));
        if (hasGradeNarration)
        {
            _gradeNarrationAlreadyPlayed = true;
            yield return PlayNarrationOrDialogCoroutine(gradeVoice.voiceClip, gradeVoice.dialogText);
        }
    }

    void ToggleFengchangVisualMode()
    {
        PlayPlanningClickSound();
        _fengchangActive = !_fengchangActive;
        ApplyFengchangVisualState();
    }

    void ApplyFengchangVisualState()
    {
        if (defaultBackgroundRoot != null)
            defaultBackgroundRoot.SetActive(!_fengchangActive);
        if (fengchangBackgroundRoot != null)
            fengchangBackgroundRoot.SetActive(_fengchangActive);

        ApplySegmentVisualColors();

        for (int i = 0; i < nodes.Count; i++)
        {
            if (_selectedNodeIndices.Contains(i))
                SetNodeVisual(i, selectedColor);
            else if (_invalidNodeIndices.Contains(i))
                SetNodeVisual(i, invalidColor);
            else
                SetNodeVisual(i, idleColor);
        }

        ApplyEndpointVisuals();
    }

    void ApplySegmentVisualColors()
    {
        if (segmentContainer == null)
            return;
        Image[] segImages = segmentContainer.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < segImages.Length; i++)
        {
            Image img = segImages[i];
            if (img == null || img.gameObject == segmentContainer.gameObject)
                continue;
            img.color = _fengchangActive ? ResolveFengchangActiveColor() : lineColor;
        }
    }

    static Color ResolveFengchangActiveColor()
    {
        return new Color(1f, 0f, 0f, 1f);
    }

    static Color ResolveFengchangBaseColor()
    {
        return Color.white;
    }

    bool ShouldShowClickAreaOverlay()
    {
        return Application.isPlaying ? showClickAreasInPlayMode : showClickAreasInEditor;
    }

    void RefreshClickAreasInEditor()
    {
        if (Application.isPlaying || fullscreenRoot == null)
            return;

        AutoPopulateNodesFromScene();
        BindEndpointUiFromNamedChildren();
        EnsureClickAreas();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    void Update()
    {
        if (!Application.isPlaying)
            return;

        if (!Input.GetKeyDown(toggleKey))
            return;

        if (_isOpen)
            ClosePlanningMode();
        else
            OpenPlanningMode();
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || !_isOpen || _routeFinalized)
            return;

        ApplyEndpointVisuals();
        ApplyWaypointBreathing();
    }

    /// <summary>
    /// 开场流程用：等当前系统对话框播完后再打开规划，避免全屏 UI 压住字幕。
    /// </summary>
    public void OpenFromSceneEntry()
    {
        if (!Application.isPlaying || _isOpen)
            return;
        if (_openFromSceneEntryRoutine != null)
            StopCoroutine(_openFromSceneEntryRoutine);
        _openFromSceneEntryRoutine = StartCoroutine(OpenFromSceneEntryRoutine());
    }

    IEnumerator OpenFromSceneEntryRoutine()
    {
        try
        {
            if (systemDialog == null)
                systemDialog = SystemDialogLocator.FindComponent();
            var dlg = ResolveSystemDialogPresentation();
            if (dlg != null)
                yield return dlg.WaitUntilDialogIdle();
            if (_isOpen)
                yield break;
            OpenPlanningMode();
        }
        finally
        {
            _openFromSceneEntryRoutine = null;
        }
    }

    void OpenPlanningMode()
    {
        if (_isOpen)
            return;
        _isOpen = true;
        IsPlanningUiOpen = true;
        _planeInputBeforeOpen = planeController != null && planeController.IsInputEnabled;
        _cursorLockBeforePlanner = Cursor.lockState;
        _cursorVisibleBeforePlanner = Cursor.visible;
        ApplyPlannerCursor();
        planeController?.SetInputEnabled(false);
        autocruiseController?.SetPlannerInputBlocked(true);
        ResetRouteState();
        if (hideMinimapWhilePlanning && minimapUi != null)
            minimapUi.SetMapSuppressedForExternalReason(true);
        if (fullscreenRoot != null)
            fullscreenRoot.SetActive(true);

        RefreshSceneWaypointTransformsFromWaypointRoot();
        EnsureClickAreas();

        EnsureRouteNarrationAudio();
        if (enterPlanningVoiceClip != null
            || !string.IsNullOrWhiteSpace(enterPlanningDialogText)
            || !string.IsNullOrWhiteSpace(enterPlanningDialogText2))
            StartCoroutine(PlayEnterPlanningNarrationSequence());
    }

    IEnumerator PlayEnterPlanningNarrationSequence()
    {
        yield return PlayNarrationOrDialogCoroutine(enterPlanningVoiceClip, enterPlanningDialogText);
        if (enterPlanningVoiceClip2 != null || !string.IsNullOrWhiteSpace(enterPlanningDialogText2))
            yield return PlayNarrationOrDialogCoroutine(enterPlanningVoiceClip2, enterPlanningDialogText2);
    }

    void ClosePlanningMode()
    {
        if (_openFromSceneEntryRoutine != null)
        {
            StopCoroutine(_openFromSceneEntryRoutine);
            _openFromSceneEntryRoutine = null;
        }
        _isOpen = false;
        IsPlanningUiOpen = false;
        GlobalGamePause.ForceResumeIfPaused();
        if (hideMinimapWhilePlanning && minimapUi != null)
            minimapUi.SetMapSuppressedForExternalReason(false);
        if (fullscreenRoot != null)
            fullscreenRoot.SetActive(false);
        HideAllGradePanels();
        _awaitingGradeDecision = false;
        autocruiseController?.SetPlannerInputBlocked(false);
        if (planeController != null)
            planeController.SetInputEnabled(_planeInputBeforeOpen);
        RestoreSceneCursor();
    }

    void ApplyPlannerCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Cursor.SetCursor(plannerCursorTexture, plannerCursorHotspot, CursorMode.Auto);
    }

    void RestoreSceneCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.lockState = _cursorLockBeforePlanner;
        Cursor.visible = _cursorVisibleBeforePlanner;
    }

    void EnsurePlanningClickAudio()
    {
        if (planningButtonClickClip == null)
            return;
        if (planningButtonAudio == null)
        {
            planningButtonAudio = GetComponent<AudioSource>();
            if (planningButtonAudio == null)
                planningButtonAudio = gameObject.AddComponent<AudioSource>();
            planningButtonAudio.playOnAwake = false;
            planningButtonAudio.spatialBlend = 0f;
        }
    }

    void PlayPlanningClickSound()
    {
        if (planningButtonClickClip == null || planningButtonAudio == null)
            return;
        planningButtonAudio.PlayOneShot(planningButtonClickClip, planningClickVolume);
    }

    void EnsureRouteNarrationAudio()
    {
        bool needSource = enterPlanningVoiceClip != null || enterPlanningVoiceClip2 != null || segmentLineConnectClip != null
            || afterPlannerCloseClip != null || arrivalVoiceClip != null || HasAnyGradeVoiceClip();
        if (!needSource)
            return;

        if (routeNarrationAudio != null)
            return;

        var sources = GetComponents<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != planningButtonAudio)
            {
                routeNarrationAudio = sources[i];
                break;
            }
        }

        if (routeNarrationAudio == null)
        {
            routeNarrationAudio = gameObject.AddComponent<AudioSource>();
            routeNarrationAudio.playOnAwake = false;
            routeNarrationAudio.spatialBlend = 0f;
        }
    }

    void PlaySegmentConnectSound()
    {
        if (segmentLineConnectClip == null)
            return;
        EnsureRouteNarrationAudio();
        AudioSource src = routeNarrationAudio != null ? routeNarrationAudio : planningButtonAudio;
        if (src == null)
        {
            EnsurePlanningClickAudio();
            src = planningButtonAudio;
        }
        if (src == null)
            return;
        src.PlayOneShot(segmentLineConnectClip, segmentConnectVolume);
    }

    IEnumerator PlayNarrationClipCoroutine(AudioClip clip)
    {
        if (clip == null || routeNarrationAudio == null)
            yield break;
        routeNarrationAudio.Stop();
        routeNarrationAudio.clip = clip;
        routeNarrationAudio.volume = routeNarrationVolume;
        routeNarrationAudio.loop = false;
        routeNarrationAudio.Play();
        yield return new WaitWhile(() => routeNarrationAudio.isPlaying);
    }

    /// <summary>有字幕且存在系统对话框时走黑底打字+对话语音；否则走 <see cref="routeNarrationAudio"/>。</summary>
    IEnumerator PlayNarrationOrDialogCoroutine(AudioClip clip, string dialogText)
    {
        var dlg = ResolveSystemDialogPresentation();
        if (dlg != null && !string.IsNullOrWhiteSpace(dialogText))
        {
            float ch = routeDialogCharacterInterval > 0f ? routeDialogCharacterInterval : 0.04f;
            if (routeDialogAutoFitToVoiceEnd && clip != null)
                ch = SystemDialogCue.CharacterIntervalToMatchVoice(dialogText.Trim(), clip, routeDialogExtraSecondsAfterVoice);
            dlg.PlaySingleLine(dialogText.Trim(), clip, ch);
            yield return dlg.WaitUntilDialogIdle();
            yield break;
        }

        yield return PlayNarrationClipCoroutine(clip);
    }

    bool HasAnyGradeVoiceClip()
    {
        for (int i = 0; i < routeGradeVoices.Count; i++)
        {
            if (routeGradeVoices[i] != null && routeGradeVoices[i].voiceClip != null)
                return true;
        }

        return false;
    }

    RouteGradeVoice ResolveGradeVoice(int gradeIndex)
    {
        if (routeGradeVoices == null || routeGradeVoices.Count == 0)
            return null;
        int idx = Mathf.Clamp(gradeIndex, 0, routeGradeVoices.Count - 1);
        return routeGradeVoices[idx];
    }

    IEnumerator PostConfirmNarrationAndCruiseSequence(RouteEvaluationResult eval)
    {
        _confirmSequenceRunning = true;
        if (confirmButton != null)
            confirmButton.interactable = false;
        try
        {
            RouteGradeVoice gradeVoice = ResolveGradeVoice(eval.gradeIndex);
            bool hasGradeNarration = gradeVoice != null
                && (gradeVoice.voiceClip != null || !string.IsNullOrWhiteSpace(gradeVoice.dialogText));
            if (hasGradeNarration && !_gradeNarrationAlreadyPlayed)
                yield return PlayNarrationOrDialogCoroutine(gradeVoice.voiceClip, gradeVoice.dialogText);
            if (autoCloseOnConfirm)
                ClosePlanningMode();
            yield return PlayNarrationOrDialogCoroutine(afterPlannerCloseClip, afterPlannerCloseDialogText);

            if (autocruiseController == null)
                yield break;

            if (!autocruiseController.TryStartCruiseFromExternal())
            {
                SetFeedback("无法启动自动巡航（例如爪持物或路线无效）。");
                yield break;
            }

            autocruiseController.OnAutocruiseRouteCompleted -= OnAutocruiseRouteCompletedPlayArrivalClip;
            autocruiseController.OnAutocruiseRouteCompleted += OnAutocruiseRouteCompletedPlayArrivalClip;
        }
        finally
        {
            _confirmSequenceRunning = false;
            if (confirmButton != null)
                confirmButton.interactable = true;
        }
    }

    string BuildRouteSubmittedFeedback(RouteEvaluationResult eval)
    {
        RouteGradeVoice gradeVoice = ResolveGradeVoice(eval.gradeIndex);
        string grade = string.IsNullOrWhiteSpace(eval.gradeName) ? $"第{eval.gradeIndex + 1}档" : eval.gradeName.Trim();
        if (gradeVoice != null && !string.IsNullOrWhiteSpace(gradeVoice.dialogText))
            return $"路线已提交（{grade}，预计总巡航时间 {eval.totalTime:0.0}s）。{gradeVoice.dialogText.Trim()}";
        return $"路线已提交（{grade}，预计总巡航时间 {eval.totalTime:0.0}s）。";
    }

    void OnAutocruiseRouteCompletedPlayArrivalClip()
    {
        if (autocruiseController != null)
            autocruiseController.OnAutocruiseRouteCompleted -= OnAutocruiseRouteCompletedPlayArrivalClip;

        if (ResolveSystemDialogPresentation() != null && !string.IsNullOrWhiteSpace(arrivalDialogText))
            StartCoroutine(PlayNarrationOrDialogCoroutine(arrivalVoiceClip, arrivalDialogText));
        else if (arrivalVoiceClip != null && routeNarrationAudio != null)
            routeNarrationAudio.PlayOneShot(arrivalVoiceClip, routeNarrationVolume);
    }

    void OnNodeClicked(int index)
    {
        if (!_isOpen || _routeFinalized)
            return;
        if (index < 0 || index >= nodes.Count)
            return;
        if (_selectedNodeIndices.Contains(index))
            return;

        PlayPlanningClickSound();

        if (nodes[index].sceneWaypoint == null)
        {
            TriggerInvalidNodeFlash(index);
            SetFeedback("该节点未绑定场景路径点，无法加入路线。");
            return;
        }

        RectTransform fromRt = _selectedNodeIndices.Count == 0 ? startUiPoint : nodes[_selectedNodeIndices[^1]].uiPoint;
        int fromIdx = _selectedNodeIndices.Count == 0 ? -1 : _selectedNodeIndices[^1];
        if (!TryValidateWaypointSegment(fromRt, nodes[index].uiPoint, fromIdx, index, out string segMsg))
        {
            TriggerInvalidNodeFlash(index);
            SetFeedback(segMsg);
            return;
        }

        _selectedNodeIndices.Add(index);
        SetNodeVisual(index, selectedColor);
        SetFeedback(string.Empty);
        RebuildSegments(includeEnd: _endPointPressed);
        ApplyEndpointVisuals();
    }

    void OnConfirmClicked()
    {
        if (!_isOpen || _confirmSequenceRunning)
            return;
        PlayPlanningClickSound();
        if (!_endPointPressed)
        {
            SetFeedback("请先点击结束点，再点击确认。");
            return;
        }

        if (!TryBuildRouteWaypoints(out var routeWaypoints))
            return;

        List<string> routeIds = BuildSelectedRouteNodeIds();
        RouteEvaluationResult eval = EvaluateRouteByConfiguredSegments(routeIds, out List<float> segmentSpeedScales);

        if (autocruiseController == null)
        {
            SetFeedback("未绑定 Autocruise Controller：请在规划器上指定飞机上的 Drone Autocruise Controller。");
            return;
        }

        if (!autocruiseController.HasAutocruiseRouteAssigned())
        {
            SetFeedback("请在飞机的 Drone Autocruise Controller 上把「Route」拖成场景中的 Drone Autocruise Route 组件。");
            return;
        }

        _pendingRouteEval = eval;
        _pendingRouteWaypoints = routeWaypoints;
        _pendingSegmentSpeedScales = segmentSpeedScales;
        _gradeNarrationAlreadyPlayed = false;
        _awaitingGradeDecision = true;
        _routeFinalized = true;
        RebuildSegments(includeEnd: true);
        SetFeedback(BuildRouteSubmittedFeedback(eval));
        ShowGradePanelForEval(eval);
        if (_gradePopupNarrationRoutine != null)
            StopCoroutine(_gradePopupNarrationRoutine);
        _gradePopupNarrationRoutine = StartCoroutine(PlayGradePopupThenNarration(eval));
    }

    void OnGradeKeepClicked()
    {
        if (!_awaitingGradeDecision)
            return;
        PlayPlanningClickSound();
        _awaitingGradeDecision = false;
        HideAllGradePanels();
        if (_gradePopupNarrationRoutine != null)
        {
            StopCoroutine(_gradePopupNarrationRoutine);
            _gradePopupNarrationRoutine = null;
        }

        bool ok = autocruiseController.TryApplyPlannedRoute(_pendingRouteWaypoints, _pendingSegmentSpeedScales, !usePostConfirmNarrationSequence && startCruiseOnConfirm);
        if (!ok)
        {
            SetFeedback("路线数据无效（例如含空路径点），提交失败。");
            ResetRouteState();
            return;
        }

        if (usePostConfirmNarrationSequence)
        {
            EnsureRouteNarrationAudio();
            StartCoroutine(PostConfirmNarrationAndCruiseSequence(_pendingRouteEval));
            return;
        }

        if (autoCloseOnConfirm)
            ClosePlanningMode();
    }

    void OnGradeRestartClicked()
    {
        if (!_awaitingGradeDecision)
            return;
        PlayPlanningClickSound();
        _awaitingGradeDecision = false;
        HideAllGradePanels();
        if (_gradePopupNarrationRoutine != null)
        {
            StopCoroutine(_gradePopupNarrationRoutine);
            _gradePopupNarrationRoutine = null;
        }
        ResetRouteState();
        SetFeedback("请重新选择路线。");
    }

    void CancelAndReset()
    {
        if (!_isOpen)
            return;
        PlayPlanningClickSound();
        ResetRouteState();
    }

    void ResetRouteState()
    {
        StopInvalidFlashCoroutines();
        _awaitingGradeDecision = false;
        _gradeNarrationAlreadyPlayed = false;
        _gradePopupNarrationRoutine = null;
        _pendingRouteWaypoints = null;
        _pendingSegmentSpeedScales = null;
        _routeFinalized = false;
        _endPointPressed = false;
        _selectedNodeIndices.Clear();
        _invalidNodeIndices.Clear();
        for (int i = 0; i < nodes.Count; i++)
            SetNodeVisual(i, idleColor);
        ApplyEndpointVisuals();
        RebuildSegments(includeEnd: false);
        HideAllGradePanels();
        SetFeedback(string.Empty);
    }

    void OnEndPointClicked()
    {
        if (!_isOpen || _routeFinalized)
            return;
        if (_endPointPressed)
            return;
        PlayPlanningClickSound();

        RectTransform fromRt = _selectedNodeIndices.Count == 0 ? startUiPoint : nodes[_selectedNodeIndices[^1]].uiPoint;
        int fromIdx = _selectedNodeIndices.Count == 0 ? -1 : _selectedNodeIndices[^1];
        if (!TryValidateWaypointSegment(fromRt, endUiPoint, fromIdx, -1, out string endSegMsg))
        {
            SetFeedback(endSegMsg);
            return;
        }

        _endPointPressed = true;
        ApplyEndpointVisuals();
        RebuildSegments(includeEnd: true);
        SetFeedback("已选择结束点，可点击确认提交路线。");
    }

    Vector2 PointInSegmentSpace(RectTransform rt)
    {
        if (segmentContainer == null || rt == null)
            return Vector2.zero;
        return segmentContainer.InverseTransformPoint(rt.position);
    }

    static float PointSegmentDistanceSq(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float den = ab.sqrMagnitude;
        if (den < 1e-8f)
            return (p - a).sqrMagnitude;
        float t = Vector2.Dot(p - a, ab) / den;
        t = Mathf.Clamp01(t);
        Vector2 proj = a + t * ab;
        return (p - proj).sqrMagnitude;
    }

    bool IsOtherWaypointTooCloseToSegment(Vector2 a, Vector2 b, int ignoreFromNodeIndex, int ignoreToNodeIndex)
    {
        float r = Mathf.Max(4f, waypointSegmentBlockRadius);
        float r2 = r * r;
        for (int j = 0; j < nodes.Count; j++)
        {
            if (j == ignoreFromNodeIndex)
                continue;
            if (ignoreToNodeIndex >= 0 && j == ignoreToNodeIndex)
                continue;
            RectTransform ui = nodes[j].uiPoint;
            if (ui == null)
                continue;
            Vector2 c = PointInSegmentSpace(ui);
            if (PointSegmentDistanceSq(c, a, b) <= r2)
                return true;
        }

        return false;
    }

    RectTransform ResolveZuliObstacleRoot()
    {
        if (zuliObstacleRoot != null)
            return zuliObstacleRoot;
        if (fullscreenRoot == null)
            return null;
        return fullscreenRoot.transform.Find("zuli") as RectTransform;
    }

    bool IsSegmentBlockedByZuliImages(Vector2 a, Vector2 b)
    {
        if (!blockSegmentThroughZuliImages || segmentContainer == null)
            return false;

        RectTransform root = ResolveZuliObstacleRoot();
        if (root == null)
            return false;

        Image[] imgs = root.GetComponentsInChildren<Image>(true);
        Vector3[] worldCorners = new Vector3[4];
        Vector2[] quad = new Vector2[4];
        for (int i = 0; i < imgs.Length; i++)
        {
            Image img = imgs[i];
            if (img == null || !img.enabled)
                continue;
            RectTransform rt = img.rectTransform;
            if (rt == null || rt.rect.width <= 0.01f || rt.rect.height <= 0.01f)
                continue;

            rt.GetWorldCorners(worldCorners);
            for (int k = 0; k < 4; k++)
                quad[k] = segmentContainer.InverseTransformPoint(worldCorners[k]);

            if (IsPointInConvexQuad(a, quad) || IsPointInConvexQuad(b, quad))
                return true;

            for (int e = 0; e < 4; e++)
            {
                Vector2 p = quad[e];
                Vector2 q = quad[(e + 1) % 4];
                if (DoSegmentsIntersect(a, b, p, q))
                    return true;
            }
        }

        return false;
    }

    static bool IsPointInConvexQuad(Vector2 p, Vector2[] q)
    {
        bool? sign = null;
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = q[i];
            Vector2 b = q[(i + 1) % 4];
            float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            if (Mathf.Abs(cross) <= 1e-5f)
                continue;
            bool current = cross > 0f;
            if (!sign.HasValue)
                sign = current;
            else if (sign.Value != current)
                return false;
        }

        return true;
    }

    static bool DoSegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float o1 = Orientation(p1, p2, q1);
        float o2 = Orientation(p1, p2, q2);
        float o3 = Orientation(q1, q2, p1);
        float o4 = Orientation(q1, q2, p2);

        if (Mathf.Abs(o1) <= 1e-5f && OnSegment(p1, q1, p2)) return true;
        if (Mathf.Abs(o2) <= 1e-5f && OnSegment(p1, q2, p2)) return true;
        if (Mathf.Abs(o3) <= 1e-5f && OnSegment(q1, p1, q2)) return true;
        if (Mathf.Abs(o4) <= 1e-5f && OnSegment(q1, p2, q2)) return true;

        return (o1 > 0f) != (o2 > 0f) && (o3 > 0f) != (o4 > 0f);
    }

    static float Orientation(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    static bool OnSegment(Vector2 a, Vector2 p, Vector2 b)
    {
        return p.x <= Mathf.Max(a.x, b.x) + 1e-5f && p.x >= Mathf.Min(a.x, b.x) - 1e-5f
            && p.y <= Mathf.Max(a.y, b.y) + 1e-5f && p.y >= Mathf.Min(a.y, b.y) - 1e-5f;
    }

    bool TryValidateWaypointSegment(RectTransform fromRt, RectTransform toRt, int fromNodeIndex, int toNodeIndex, out string message)
    {
        message = null;
        if (!enforceMaxWaypointIconConnectDistance && !requireNoOtherWaypointOnSegment && !blockSegmentThroughZuliImages)
            return true;
        if (segmentContainer == null)
            return true;
        if (fromRt == null || toRt == null)
            return true;

        Vector2 a = PointInSegmentSpace(fromRt);
        Vector2 b = PointInSegmentSpace(toRt);

        if (enforceMaxWaypointIconConnectDistance)
        {
            float maxD = Mathf.Max(1f, maxWaypointIconConnectDistance);
            if (Vector2.Distance(a, b) > maxD)
            {
                message = $"两途径点图标距离过远（须 ≤ {maxD:0}）。";
                return false;
            }
        }

        if (requireNoOtherWaypointOnSegment && (a - b).sqrMagnitude > 1e-6f)
        {
            if (IsOtherWaypointTooCloseToSegment(a, b, fromNodeIndex, toNodeIndex))
            {
                message = "两点连线之间不能有其它途径点。";
                return false;
            }
        }

        if (IsSegmentBlockedByZuliImages(a, b))
        {
            message = "连线不能穿过阻力区图片。";
            return false;
        }

        return true;
    }

    static string MakeDirectedSegKey(string fromId, string toId)
    {
        return $"{fromId}->{toId}";
    }

    void RebuildSegmentRouteConfigCache()
    {
        _segmentConfigByDirectedKey.Clear();
        if (segmentRouteConfigs == null)
            return;

        for (int i = 0; i < segmentRouteConfigs.Count; i++)
        {
            SegmentRouteConfig c = segmentRouteConfigs[i];
            if (c == null)
                continue;
            if (string.IsNullOrWhiteSpace(c.fromNodeId) || string.IsNullOrWhiteSpace(c.toNodeId))
                continue;
            _segmentConfigByDirectedKey[MakeDirectedSegKey(c.fromNodeId.Trim(), c.toNodeId.Trim())] = c;
        }
    }

    void GenerateFullDirectedSegmentTemplate()
    {
        AutoPopulateNodesFromScene();
        RefreshSceneWaypointTransformsFromWaypointRoot();

        Transform wpRoot = waypointRoot;
        if (wpRoot == null)
        {
            GameObject go = GameObject.Find("Waypoint");
            wpRoot = go != null ? go.transform : null;
        }
        if (wpRoot == null || fullscreenRoot == null)
            return;

        var waypointByName = new Dictionary<string, Transform>();
        CollectLeafWaypoints(wpRoot, waypointByName);

        var routeNodeIds = new List<string>();
        var routeNodeUi = new List<RectTransform>();
        foreach (Transform child in fullscreenRoot.transform)
        {
            if (child == null)
                continue;
            RectTransform rt = child as RectTransform;
            if (rt == null)
                continue;
            if (child.GetComponent<Image>() == null || child.GetComponent<Button>() == null)
                continue;
            if (!waypointByName.ContainsKey(child.name))
                continue;

            routeNodeIds.Add(child.name.Trim());
            routeNodeUi.Add(rt);
        }

        var generated = new List<SegmentRouteConfig>();

        for (int i = 0; i < routeNodeIds.Count; i++)
        {
            RectTransform toRt = routeNodeUi[i];
            if (!TryValidateWaypointSegment(startUiPoint, toRt, -1, i, out _))
                continue;
            generated.Add(new SegmentRouteConfig
            {
                fromNodeId = "start",
                toNodeId = routeNodeIds[i],
                windSpeed = defaultSegmentWindSpeed
            });
        }

        for (int i = 0; i < routeNodeIds.Count; i++)
        {
            for (int j = 0; j < routeNodeIds.Count; j++)
            {
                if (i == j)
                    continue;
                RectTransform fromRt = routeNodeUi[i];
                RectTransform toRt = routeNodeUi[j];
                if (!TryValidateWaypointSegment(fromRt, toRt, i, j, out _))
                    continue;
                generated.Add(new SegmentRouteConfig
                {
                    fromNodeId = routeNodeIds[i],
                    toNodeId = routeNodeIds[j],
                    windSpeed = defaultSegmentWindSpeed
                });
            }
        }

        for (int i = 0; i < routeNodeIds.Count; i++)
        {
            RectTransform fromRt = routeNodeUi[i];
            if (!TryValidateWaypointSegment(fromRt, endUiPoint, i, -1, out _))
                continue;
            generated.Add(new SegmentRouteConfig
            {
                fromNodeId = routeNodeIds[i],
                toNodeId = "end",
                windSpeed = defaultSegmentWindSpeed
            });
        }

        segmentRouteConfigs = generated;
        RebuildSegmentRouteConfigCache();
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Segment Route Template")]
    public void GenerateSegmentRouteTemplateInEditor()
    {
        GenerateFullDirectedSegmentTemplate();
        regenerateSegmentTemplateNow = false;
        EditorUtility.SetDirty(this);
    }
#endif

    SegmentRouteConfig ResolveSegmentConfig(string fromId, string toId)
    {
        if (_segmentConfigByDirectedKey.Count == 0)
            RebuildSegmentRouteConfigCache();
        _segmentConfigByDirectedKey.TryGetValue(MakeDirectedSegKey(fromId, toId), out SegmentRouteConfig cfg);
        return cfg;
    }

    static float WindSpeedToSpeedScale(float windSpeed)
    {
        // 需求：speedScale = 1 + 0.10 * windSpeed（负值为逆风）。
        return 1f + 0.10f * windSpeed;
    }

    List<string> BuildSelectedRouteNodeIds()
    {
        var ids = new List<string>(_selectedNodeIndices.Count + 2) { "start" };
        for (int i = 0; i < _selectedNodeIndices.Count; i++)
            ids.Add(nodes[_selectedNodeIndices[i]].nodeId);
        ids.Add("end");
        return ids;
    }

    RouteEvaluationResult EvaluateRouteByConfiguredSegments(List<string> routeIds, out List<float> segmentSpeedScales)
    {
        segmentSpeedScales = new List<float>(Mathf.Max(0, routeIds.Count - 1));
        float total = 0f;
        float baseSpeed = ResolveRatingBaseCruiseSpeedMetersPerSecond();
        for (int i = 0; i < routeIds.Count - 1; i++)
        {
            string fromId = routeIds[i];
            string toId = routeIds[i + 1];
            SegmentRouteConfig cfg = ResolveSegmentConfig(fromId, toId);
            float wind = cfg != null ? cfg.windSpeed : defaultSegmentWindSpeed;
            float segScale = Mathf.Clamp(WindSpeedToSpeedScale(wind), 0.2f, 2.5f);
            segmentSpeedScales.Add(segScale);

            if (!TryResolveRouteNodeWorldPosition(fromId, out Vector3 fromPos) || !TryResolveRouteNodeWorldPosition(toId, out Vector3 toPos))
                continue;

            float segDistance = Vector3.Distance(fromPos, toPos);
            float segSpeed = Mathf.Max(0.1f, baseSpeed * segScale);
            total += segDistance / segSpeed;
        }

        int grade = ResolveGradeIndexByRouteTime(total);
        string gradeName = ResolveGradeName(grade);
        return new RouteEvaluationResult
        {
            totalTime = total,
            gradeIndex = grade,
            gradeName = gradeName
        };
    }

    float ResolveRatingBaseCruiseSpeedMetersPerSecond()
    {
        if (usePlaneControllerCruiseSpeedForRating && planeController != null)
            return Mathf.Max(0.1f, planeController.PeakCruiseSpeed);
        return Mathf.Max(0.1f, baseCruiseSpeedMetersPerSecond);
    }

    int ResolveGradeIndexByRouteTime(float totalRouteTime)
    {
        const int gradeCount = 5; // 0:C, 1:B, 2:A, 3:S, 4:SS
        List<float> effectiveSamples = BuildEffectiveRouteTimeSamples();
        if (effectiveSamples.Count == 0)
            return gradeCount - 1;

        var sorted = new List<float>(effectiveSamples.Count + 1);
        for (int i = 0; i < effectiveSamples.Count; i++)
            if (effectiveSamples[i] > 0f)
                sorted.Add(effectiveSamples[i]);
        sorted.Add(Mathf.Max(0.01f, totalRouteTime));
        sorted.Sort();

        // 越小越快：rank 采用 1-based，便于按“前 X%”表达阈值。
        int rankFast = sorted.IndexOf(Mathf.Max(0.01f, totalRouteTime)) + 1;
        int n = Mathf.Max(1, sorted.Count);
        float ratio = rankFast / (float)n;

        // 分级标准（从快到慢）：
        // 第 1 名 => SS
        // 前 1%   => S
        // 前 1%-10% => A
        // 前 10%-50% => B
        // 后 50% => C
        if (rankFast == 1)
            return 4; // SS
        if (ratio <= 0.01f)
            return 3; // S
        if (ratio <= 0.10f)
            return 2; // A
        if (ratio <= 0.50f)
            return 1; // B
        return 0; // C
    }

    List<float> BuildEffectiveRouteTimeSamples()
    {
        if (routeTotalTimeSamples != null && routeTotalTimeSamples.Count > 0)
            return routeTotalTimeSamples;
        return EnumerateAllRouteTimesFromCurrentConfig();
    }

    List<float> EnumerateAllRouteTimesFromCurrentConfig()
    {
        const int maxRouteCount = 4000;
        var samples = new List<float>();
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "start", "end" };
        if (segmentRouteConfigs == null || segmentRouteConfigs.Count == 0)
            return samples;

        var outgoing = new Dictionary<string, List<SegmentRouteConfig>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < segmentRouteConfigs.Count; i++)
        {
            SegmentRouteConfig cfg = segmentRouteConfigs[i];
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.fromNodeId) || string.IsNullOrWhiteSpace(cfg.toNodeId))
                continue;
            string from = cfg.fromNodeId.Trim();
            string to = cfg.toNodeId.Trim();
            nodeIds.Add(from);
            nodeIds.Add(to);
            if (!outgoing.TryGetValue(from, out List<SegmentRouteConfig> list))
            {
                list = new List<SegmentRouteConfig>();
                outgoing[from] = list;
            }

            list.Add(cfg);
        }

        if (!outgoing.ContainsKey("start"))
            return samples;

        float baseSpeed = ResolveRatingBaseCruiseSpeedMetersPerSecond();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Dfs(string nodeId, float accTime)
        {
            if (samples.Count >= maxRouteCount)
                return;
            if (string.Equals(nodeId, "end", StringComparison.OrdinalIgnoreCase))
            {
                if (accTime > 0f)
                    samples.Add(accTime);
                return;
            }

            if (!outgoing.TryGetValue(nodeId, out List<SegmentRouteConfig> next))
                return;
            if (!visiting.Add(nodeId))
                return;

            for (int i = 0; i < next.Count; i++)
            {
                SegmentRouteConfig edge = next[i];
                string to = edge.toNodeId.Trim();
                if (visiting.Contains(to))
                    continue;
                if (!TryResolveRouteNodeWorldPosition(nodeId, out Vector3 fromPos) || !TryResolveRouteNodeWorldPosition(to, out Vector3 toPos))
                    continue;

                float segScale = Mathf.Clamp(WindSpeedToSpeedScale(edge.windSpeed), 0.2f, 2.5f);
                float segSpeed = Mathf.Max(0.1f, baseSpeed * segScale);
                float segTime = Vector3.Distance(fromPos, toPos) / segSpeed;
                Dfs(to, accTime + segTime);
            }

            visiting.Remove(nodeId);
        }

        Dfs("start", 0f);
        return samples;
    }

    string ResolveGradeName(int gradeIndex)
    {
        RouteGradeVoice gv = ResolveGradeVoice(gradeIndex);
        if (gv != null && !string.IsNullOrWhiteSpace(gv.gradeName))
            return gv.gradeName.Trim();
        return $"第{gradeIndex + 1}档";
    }

    bool TryResolveRouteNodeWorldPosition(string nodeId, out Vector3 worldPos)
    {
        worldPos = default;
        if (string.Equals(nodeId, "start", StringComparison.OrdinalIgnoreCase))
        {
            if (startSceneWaypoint == null)
                return false;
            worldPos = startSceneWaypoint.position;
            return true;
        }

        if (string.Equals(nodeId, "end", StringComparison.OrdinalIgnoreCase))
        {
            if (endSceneWaypoint == null)
                return false;
            worldPos = endSceneWaypoint.position;
            return true;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            if (!string.Equals(nodes[i].nodeId, nodeId, StringComparison.Ordinal))
                continue;
            if (nodes[i].sceneWaypoint == null)
                return false;
            worldPos = nodes[i].sceneWaypoint.position;
            return true;
        }

        return false;
    }

    void StopInvalidFlashCoroutines()
    {
        if (_invalidFlashByNode.Count == 0)
            return;
        var keys = new List<int>(_invalidFlashByNode.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            if (_invalidFlashByNode.TryGetValue(keys[i], out Coroutine c) && c != null)
                StopCoroutine(c);
        }

        _invalidFlashByNode.Clear();
    }

    void TriggerInvalidNodeFlash(int index)
    {
        if (_invalidFlashByNode.TryGetValue(index, out Coroutine old) && old != null)
            StopCoroutine(old);
        _invalidNodeIndices.Add(index);
        SetNodeVisual(index, invalidColor);
        if (!Application.isPlaying)
            return;
        Coroutine routine = StartCoroutine(InvalidNodeFlashRoutine(index));
        _invalidFlashByNode[index] = routine;
    }

    IEnumerator InvalidNodeFlashRoutine(int index)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, invalidNodeFlashDuration));
        _invalidFlashByNode.Remove(index);
        if (!_invalidNodeIndices.Contains(index))
            yield break;
        if (_selectedNodeIndices.Contains(index))
            yield break;
        _invalidNodeIndices.Remove(index);
        SetNodeVisual(index, idleColor);
    }

    bool TryBuildRouteWaypoints(out List<Transform> routeWaypoints)
    {
        routeWaypoints = null;
        RefreshSceneWaypointTransformsFromWaypointRoot();

        if (startSceneWaypoint == null || endSceneWaypoint == null)
        {
            SetFeedback("起点或终点未绑定场景路径点。");
            return false;
        }

        if (_selectedNodeIndices.Count == 0)
        {
            SetFeedback("请至少选择一个中间路径点。");
            return false;
        }

        routeWaypoints = new List<Transform>(_selectedNodeIndices.Count + 2)
        {
            startSceneWaypoint
        };

        for (int i = 0; i < _selectedNodeIndices.Count; i++)
        {
            NodeBinding node = nodes[_selectedNodeIndices[i]];
            if (node.sceneWaypoint == null)
            {
                TriggerInvalidNodeFlash(_selectedNodeIndices[i]);
                SetFeedback("存在无效节点，请修正后再提交。");
                return false;
            }

            routeWaypoints.Add(node.sceneWaypoint);
        }

        routeWaypoints.Add(endSceneWaypoint);
        return true;
    }

    void RebuildSegments(bool includeEnd)
    {
        for (int i = 0; i < _spawnedSegments.Count; i++)
        {
            if (_spawnedSegments[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(_spawnedSegments[i]);
                else
                    DestroyImmediate(_spawnedSegments[i]);
            }
        }
        _spawnedSegments.Clear();

        if (segmentContainer == null || segmentPrefab == null || startUiPoint == null)
            return;
        if (_selectedNodeIndices.Count == 0)
            return;

        RectTransform prev = startUiPoint;
        for (int i = 0; i < _selectedNodeIndices.Count; i++)
        {
            RectTransform next = nodes[_selectedNodeIndices[i]].uiPoint;
            if (next == null)
                continue;
            SpawnSegment(prev, next);
            prev = next;
        }

        if (includeEnd && endUiPoint != null)
            SpawnSegment(prev, endUiPoint);
    }

    void SpawnSegment(RectTransform a, RectTransform b)
    {
        if (a == null || b == null || segmentContainer == null || segmentPrefab == null)
            return;

        Image seg = Instantiate(segmentPrefab, segmentContainer);
#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RegisterCreatedObjectUndo(seg.gameObject, "Route Planner Preview Segment");
#endif
        seg.color = _fengchangActive ? ResolveFengchangActiveColor() : lineColor;
        RectTransform rt = seg.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 localA = segmentContainer.InverseTransformPoint(a.position);
        Vector2 localB = segmentContainer.InverseTransformPoint(b.position);
        Vector2 delta = localB - localA;
        float length = delta.magnitude;
        if (length <= 0.01f)
        {
            if (Application.isPlaying)
                Destroy(seg.gameObject);
            else
                DestroyImmediate(seg.gameObject);
            return;
        }

        rt.anchoredPosition = (localA + localB) * 0.5f;
        rt.sizeDelta = new Vector2(length, Mathf.Max(1f, segmentThickness));
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        _spawnedSegments.Add(seg.gameObject);
        if (Application.isPlaying && _isOpen && !_routeFinalized)
            PlaySegmentConnectSound();
    }

    void SetNodeVisual(int index, Color color)
    {
        if (index < 0 || index >= nodes.Count)
            return;
        if (nodes[index].stateImage != null)
        {
            if (_fengchangActive)
            {
                // 风场模式：默认白色；仅选中/无效节点变红，同时保留原透明度变化。
                bool isHighlighted = _selectedNodeIndices.Contains(index) || _invalidNodeIndices.Contains(index);
                Color c = isHighlighted ? ResolveFengchangActiveColor() : ResolveFengchangBaseColor();
                c.a = color.a;
                nodes[index].stateImage.color = c;
            }
            else
            {
                nodes[index].stateImage.color = color;
            }
        }
    }

    void ApplyEndpointVisuals()
    {
        Color endpointRed = ResolveFengchangActiveColor();
        Color endpointWhite = ResolveFengchangBaseColor();
        if (_startPointImage != null)
        {
            bool startLinked = _selectedNodeIndices.Count > 0;
            _startPointImage.color = _fengchangActive
                ? endpointRed
                : (startLinked ? selectedColor : endpointIdleColor);
        }

        if (_endPointImage != null)
            _endPointImage.color = _fengchangActive
                ? endpointRed
                : (_endPointPressed ? selectedColor : endpointIdleColor);
    }

    void ApplyWaypointBreathing()
    {
        if (breathCycleSeconds <= 0.01f)
            return;

        float phase = Mathf.Sin(Time.time * (Mathf.PI * 2f / breathCycleSeconds)) * 0.5f + 0.5f;
        float a = Mathf.Lerp(breathAlphaMin, breathAlphaMax, phase);
        Color c = idleColor;
        c.a = a;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (_selectedNodeIndices.Contains(i) || _invalidNodeIndices.Contains(i))
                continue;
            SetNodeVisual(i, c);
        }
    }

    void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    void ApplyCircleSprite(Image image)
    {
        if (image == null || pointCircleSprite == null)
            return;
        image.sprite = pointCircleSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }

    Button ResolveNodeButton(int index)
    {
        if (index < 0 || index >= nodes.Count)
            return null;

        NodeBinding node = nodes[index];
        if (node.button != null)
            return node.button;

        if (node.clickArea == null)
            return null;

        // Allow using a separate, adjustable RectTransform as hit area.
        return node.clickArea.GetComponent<Button>();
    }

    void EnsureClickAreas()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (autoCreateClickAreas && nodes[i].uiPoint != null)
            {
                nodes[i].clickArea = GetOrCreateClickArea(nodes[i].uiPoint, $"Node_{i}_ClickArea_Auto");
                nodes[i].button = nodes[i].clickArea != null ? nodes[i].clickArea.GetComponent<Button>() : null;
            }
            else if (nodes[i].clickArea == null)
            {
                nodes[i].clickArea = nodes[i].uiPoint;
            }

            if (nodes[i].button == null)
            {
                Button b = ResolveNodeButton(i);
                if (b != null)
                    nodes[i].button = b;
            }
        }

        if (endUiPoint != null)
        {
            if (autoCreateClickAreas)
            {
                endClickArea = GetOrCreateClickArea(endUiPoint, "EndPoint_ClickArea_Auto");
                endButton = endClickArea != null ? endClickArea.GetComponent<Button>() : null;
            }
            else if (endClickArea == null)
                endClickArea = endUiPoint;

            if (endButton == null && endClickArea != null)
                endButton = endClickArea.GetComponent<Button>();
            if (endButton == null)
                endButton = endUiPoint.GetComponent<Button>();
        }
    }

    void BindEndpointUiFromNamedChildren()
    {
        if (fullscreenRoot == null)
            return;

        Transform root = fullscreenRoot.transform;
        var tStart = root.Find("start") as RectTransform;
        if (tStart != null)
            startUiPoint = tStart;

        var tEnd = root.Find("end") as RectTransform;
        if (tEnd != null)
        {
            endUiPoint = tEnd;
            endClickArea = tEnd;
            if (endButton == null)
                endButton = tEnd.GetComponent<Button>();
        }
    }

    void AutoPopulateNodesFromScene()
    {
        if (!autoPopulateNodesFromWaypointNames || fullscreenRoot == null)
            return;

        if (waypointRoot == null)
        {
            GameObject go = GameObject.Find("Waypoint");
            waypointRoot = go != null ? go.transform : null;
        }
        if (waypointRoot == null)
            return;

        var waypointByName = new Dictionary<string, Transform>();
        CollectLeafWaypoints(waypointRoot, waypointByName);

        var populated = new List<NodeBinding>();
        foreach (Transform child in fullscreenRoot.transform)
        {
            if (child == null)
                continue;
            if (!waypointByName.TryGetValue(child.name, out Transform sceneWp))
                continue;

            Image img = child.GetComponent<Image>();
            Button btn = child.GetComponent<Button>();
            RectTransform rt = child as RectTransform;
            if (img == null || btn == null || rt == null)
                continue;

            populated.Add(new NodeBinding
            {
                nodeId = child.name,
                button = btn,
                clickArea = rt,
                uiPoint = rt,
                stateImage = img,
                sceneWaypoint = sceneWp
            });
        }

        if (populated.Count > 0)
            nodes = populated;
    }

    /// <summary>
    /// 在确认路线或打开规划前，按当前场景 Waypoint 层级用名字重新绑定 <see cref="NodeBinding.sceneWaypoint"/> 与起终点，
    /// 避免序列化引用仍指向已替换的旧物体（仅移动坐标时同一引用不变，本调用也为幂等）。
    /// </summary>
    void RefreshSceneWaypointTransformsFromWaypointRoot()
    {
        Transform root = waypointRoot;
        if (root == null)
        {
            GameObject go = GameObject.Find("Waypoint");
            root = go != null ? go.transform : null;
        }

        if (root == null)
            return;

        for (int i = 0; i < nodes.Count; i++)
        {
            string id = nodes[i].nodeId;
            if (string.IsNullOrEmpty(id) && nodes[i].sceneWaypoint != null)
                id = nodes[i].sceneWaypoint.name;
            if (string.IsNullOrEmpty(id))
                continue;
            Transform resolved = DroneAutocruiseRoute.ResolveWaypointTransformByName(root, id);
            if (resolved != null)
                nodes[i].sceneWaypoint = resolved;
        }

        if (startSceneWaypoint != null)
        {
            Transform t = DroneAutocruiseRoute.ResolveWaypointTransformByName(root, startSceneWaypoint.name);
            if (t != null)
                startSceneWaypoint = t;
        }

        if (endSceneWaypoint != null)
        {
            Transform t = DroneAutocruiseRoute.ResolveWaypointTransformByName(root, endSceneWaypoint.name);
            if (t != null)
                endSceneWaypoint = t;
        }
    }

    void CollectLeafWaypoints(Transform root, Dictionary<string, Transform> outMap)
    {
        if (root == null)
            return;

        bool isLeaf = root.childCount == 0;
        if (isLeaf && root.name != "start" && root.name != "end")
        {
            if (!outMap.ContainsKey(root.name))
                outMap.Add(root.name, root);
            return;
        }

        for (int i = 0; i < root.childCount; i++)
            CollectLeafWaypoints(root.GetChild(i), outMap);
    }

    /// <summary>
    /// 热区为正方形边长（同一坐标系 as 目标 uiPoint）。缩小圆点时仍保证不小于最小可点区域。
    /// </summary>
    Vector2 ResolveClickAreaSize(RectTransform target)
    {
        if (target == null)
            return defaultClickAreaSize;

        if (!smartClickAreaSize)
            return defaultClickAreaSize;

        Vector2 v = target.rect.size;
        if (v.x < 0.01f && v.y < 0.01f)
            v = target.sizeDelta;
        float baseDim = Mathf.Max(v.x, v.y);
        if (baseDim < 0.01f)
            baseDim = Mathf.Max(defaultClickAreaSize.x, defaultClickAreaSize.y);

        float side = baseDim + Mathf.Max(0f, clickAreaPadding) * 2f;
        side = Mathf.Max(side, Mathf.Max(0f, clickAreaMinSide));
        side = Mathf.Max(side, defaultClickAreaSize.x);
        side = Mathf.Max(side, defaultClickAreaSize.y);
        if (clickAreaMaxSide > 0.01f)
            side = Mathf.Min(side, clickAreaMaxSide);
        return new Vector2(side, side);
    }

    RectTransform GetOrCreateClickArea(RectTransform target, string defaultName)
    {
        if (target == null)
            return null;

        Transform existing = target.Find(defaultName);
        RectTransform rt;
        if (existing != null)
        {
            rt = existing as RectTransform;
        }
        else
        {
            GameObject go = new GameObject(defaultName, typeof(RectTransform), typeof(Image), typeof(Button));
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(go, "Route Planner Click Area");
#endif
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(target, false);
        }

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = ResolveClickAreaSize(target);

        Image img = rt.GetComponent<Image>();
        if (img != null)
        {
            Color c = ShouldShowClickAreaOverlay() ? new Color(0.2f, 0.9f, 1f, 0.18f) : new Color(1f, 1f, 1f, 0f);
            img.color = c;
            img.raycastTarget = true;
        }

        return rt;
    }

    void OnValidate()
    {
        if (fullscreenRoot != null)
            BindEndpointUiFromNamedChildren();
        TryAutoBindFengchangButtonAndBackgrounds();
        TryAutoBindGradePanelsAndButtons();

        if (endButton == null && endClickArea != null)
            endButton = endClickArea.GetComponent<Button>();
        if (endButton == null && endUiPoint != null)
            endButton = endUiPoint.GetComponent<Button>();
        if (endClickArea == null && endUiPoint != null)
            endClickArea = endUiPoint;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].button == null && nodes[i].clickArea != null)
                nodes[i].button = nodes[i].clickArea.GetComponent<Button>();
        }

#if UNITY_EDITOR
        if (regenerateSegmentTemplateNow)
        {
            regenerateSegmentTemplateNow = false;
            GenerateFullDirectedSegmentTemplate();
            EditorUtility.SetDirty(this);
        }
        else if (ShouldAutoGenerateTemplateFromCurrentNodes())
        {
            GenerateFullDirectedSegmentTemplate();
            EditorUtility.SetDirty(this);
        }
#endif

        RebuildSegmentRouteConfigCache();

        if (!Application.isPlaying)
            RefreshClickAreasInEditor();
    }

#if UNITY_EDITOR
    bool ShouldAutoGenerateTemplateFromCurrentNodes()
    {
        if (segmentRouteConfigs == null || segmentRouteConfigs.Count == 0)
            return true;
        // 兼容旧序列化残留（空条目）：自动替换为完整模板，避免手工重建。
        for (int i = 0; i < segmentRouteConfigs.Count; i++)
        {
            SegmentRouteConfig c = segmentRouteConfigs[i];
            if (c == null || string.IsNullOrWhiteSpace(c.fromNodeId) || string.IsNullOrWhiteSpace(c.toNodeId))
                return true;
        }

        return false;
    }
#endif
}
