using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoutePlanningMiniGameController : MonoBehaviour
{
    [System.Serializable]
    public class NodeBinding
    {
        public string nodeId;
        public Button button;
        public RectTransform uiPoint;
        public Image stateImage;
        public Transform sceneWaypoint;
    }

    [Header("Mode")]
    [SerializeField] KeyCode toggleKey = KeyCode.H;
    [SerializeField] GameObject fullscreenRoot;
    [SerializeField] PlaneController planeController;
    [SerializeField] DroneAutocruiseController autocruiseController;

    [Header("Path Endpoints")]
    [SerializeField] RectTransform startUiPoint;
    [SerializeField] RectTransform endUiPoint;
    [SerializeField] Transform startSceneWaypoint;
    [SerializeField] Transform endSceneWaypoint;

    [Header("Nodes")]
    [SerializeField] List<NodeBinding> nodes = new List<NodeBinding>();

    [Header("Line Rendering")]
    [SerializeField] RectTransform segmentContainer;
    [SerializeField] Image segmentPrefab;
    [SerializeField] float segmentThickness = 6f;

    [Header("Visual States")]
    [SerializeField] Color idleColor = Color.white;
    [SerializeField] Color selectedColor = new Color(0.2f, 1f, 0.3f, 1f);
    [SerializeField] Color invalidColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Actions")]
    [SerializeField] Button confirmButton;
    [SerializeField] Button cancelButton;
    [SerializeField] Text feedbackText;
    [SerializeField] bool autoCloseOnConfirm = true;
    [SerializeField] bool startCruiseOnConfirm;

    readonly List<int> _selectedNodeIndices = new List<int>();
    readonly List<GameObject> _spawnedSegments = new List<GameObject>();
    bool _isOpen;
    bool _routeFinalized;
    bool _planeInputBeforeOpen;

    void Awake()
    {
        if (planeController == null)
            planeController = FindFirstObjectByType<PlaneController>();
        if (autocruiseController == null)
            autocruiseController = FindFirstObjectByType<DroneAutocruiseController>();

        for (int i = 0; i < nodes.Count; i++)
        {
            int index = i;
            if (nodes[i].button != null)
                nodes[i].button.onClick.AddListener(() => OnNodeClicked(index));
            SetNodeVisual(i, idleColor);
        }

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelAndReset);

        if (fullscreenRoot != null)
            fullscreenRoot.SetActive(false);
        SetFeedback(string.Empty);
    }

    void Update()
    {
        if (!Input.GetKeyDown(toggleKey))
            return;

        if (_isOpen)
            ClosePlanningMode();
        else
            OpenPlanningMode();
    }

    void OpenPlanningMode()
    {
        _isOpen = true;
        _planeInputBeforeOpen = planeController != null && planeController.IsInputEnabled;
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
    }

    void OnNodeClicked(int index)
    {
        if (!_isOpen || _routeFinalized)
            return;
        if (index < 0 || index >= nodes.Count)
            return;
        if (_selectedNodeIndices.Contains(index))
            return;

        if (nodes[index].sceneWaypoint == null)
        {
            SetNodeVisual(index, invalidColor);
            SetFeedback("该节点未绑定场景路径点，无法加入路线。");
            return;
        }

        _selectedNodeIndices.Add(index);
        SetNodeVisual(index, selectedColor);
        SetFeedback(string.Empty);
        RebuildSegments(includeEnd: false);
    }

    void OnConfirmClicked()
    {
        if (!_isOpen)
            return;

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
        ResetRouteState();
    }

    void ResetRouteState()
    {
        _routeFinalized = false;
        _selectedNodeIndices.Clear();
        for (int i = 0; i < nodes.Count; i++)
            SetNodeVisual(i, idleColor);
        RebuildSegments(includeEnd: false);
        SetFeedback(string.Empty);
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
                Destroy(_spawnedSegments[i]);
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
            Destroy(seg.gameObject);
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

    void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }
}
