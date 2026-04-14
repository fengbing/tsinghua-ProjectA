using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class RoutePlanningMiniGameController : MonoBehaviour
{
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

    [Header("Mode")]
    [SerializeField] KeyCode toggleKey = KeyCode.H;
    [SerializeField] GameObject fullscreenRoot;
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
    [Header("Cursor")]
    [SerializeField] Texture2D plannerCursorTexture;
    [SerializeField] Vector2 plannerCursorHotspot = Vector2.zero;

    [Header("Audio")]
    [Tooltip("途径点 / 终点 / 确认 / 取消 等规划 UI 按钮按下时播放；留空则不发声")]
    [SerializeField] AudioClip planningButtonClickClip;
    [Tooltip("留空则使用本物体上的 AudioSource，没有再添加一个")]
    [SerializeField] AudioSource planningButtonAudio;
    [Range(0f, 1f)]
    [SerializeField] float planningClickVolume = 1f;

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

    void Awake()
    {
        if (Application.isPlaying)
        {
            if (planeController == null)
                planeController = FindFirstObjectByType<PlaneController>();
            if (autocruiseController == null)
                autocruiseController = FindFirstObjectByType<DroneAutocruiseController>();
            EnsurePlanningClickAudio();
        }

        AutoPopulateNodesFromScene();
        BindEndpointUiFromNamedChildren();
        EnsureClickAreas();

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
        if (endButton != null)
            endButton.onClick.AddListener(OnEndPointClicked);
        else if (endUiPoint != null)
        {
            endButton = endUiPoint.GetComponent<Button>();
            endButton?.onClick.AddListener(OnEndPointClicked);
        }

        ApplyEndpointVisuals();
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

    void OpenPlanningMode()
    {
        _isOpen = true;
        _planeInputBeforeOpen = planeController != null && planeController.IsInputEnabled;
        _cursorLockBeforePlanner = Cursor.lockState;
        _cursorVisibleBeforePlanner = Cursor.visible;
        ApplyPlannerCursor();
        planeController?.SetInputEnabled(false);
        autocruiseController?.SetPlannerInputBlocked(true);
        ResetRouteState();
        if (fullscreenRoot != null)
            fullscreenRoot.SetActive(true);
    }

    void ClosePlanningMode()
    {
        _isOpen = false;
        if (fullscreenRoot != null)
            fullscreenRoot.SetActive(false);
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
            _invalidNodeIndices.Add(index);
            SetNodeVisual(index, invalidColor);
            SetFeedback("该节点未绑定场景路径点，无法加入路线。");
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
        if (!_isOpen)
            return;
        PlayPlanningClickSound();
        if (!_endPointPressed)
        {
            SetFeedback("请先点击结束点，再点击确认。");
            return;
        }

        if (!TryBuildRouteWaypoints(out var routeWaypoints))
            return;

        bool ok = autocruiseController != null &&
                  autocruiseController.TryApplyPlannedRoute(routeWaypoints, startCruiseOnConfirm);
        if (!ok)
        {
            SetFeedback("自动巡航控制器未就绪，路线提交失败。");
            return;
        }

        _routeFinalized = true;
        RebuildSegments(includeEnd: true);
        SetFeedback("路线已提交到自动巡航。");
        if (autoCloseOnConfirm)
            ClosePlanningMode();
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
        _routeFinalized = false;
        _endPointPressed = false;
        _selectedNodeIndices.Clear();
        _invalidNodeIndices.Clear();
        for (int i = 0; i < nodes.Count; i++)
            SetNodeVisual(i, idleColor);
        ApplyEndpointVisuals();
        RebuildSegments(includeEnd: false);
        SetFeedback(string.Empty);
    }

    void OnEndPointClicked()
    {
        if (!_isOpen || _routeFinalized)
            return;
        if (_endPointPressed)
            return;
        PlayPlanningClickSound();
        _endPointPressed = true;
        ApplyEndpointVisuals();
        RebuildSegments(includeEnd: true);
        SetFeedback("已选择结束点，可点击确认提交路线。");
    }

    bool TryBuildRouteWaypoints(out List<Transform> routeWaypoints)
    {
        routeWaypoints = null;
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
                _invalidNodeIndices.Add(_selectedNodeIndices[i]);
                SetNodeVisual(_selectedNodeIndices[i], invalidColor);
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
        seg.color = lineColor;
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
    }

    void SetNodeVisual(int index, Color color)
    {
        if (index < 0 || index >= nodes.Count)
            return;
        if (nodes[index].stateImage != null)
            nodes[index].stateImage.color = color;
    }

    void ApplyEndpointVisuals()
    {
        if (_startPointImage != null)
        {
            bool startLinked = _selectedNodeIndices.Count > 0;
            _startPointImage.color = startLinked ? selectedColor : endpointIdleColor;
        }

        if (_endPointImage != null)
            _endPointImage.color = _endPointPressed ? selectedColor : endpointIdleColor;
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

        if (!Application.isPlaying)
            RefreshClickAreasInEditor();
    }
}
