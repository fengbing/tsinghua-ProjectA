using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MinimapUiController : MonoBehaviour
{
    [Header("Data Sources")]
    [Tooltip("地图边界、贴图、校准（旋转/翻转）都在这里配置。可：Create > Map System > Minimap Config 创建资源并拖进来；或菜单 Tools/Map System/Auto Setup 自动创建并绑定。留空则用运行时默认（无保存资源、无地图贴图）。")]
    [SerializeField] private MinimapConfig config;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private MissionObjectiveProvider objectiveProvider;

    [Header("Prefabs")]
    [SerializeField] private RectTransform markerPrefab;
    [SerializeField] private RectTransform playerIconPrefab;

    [Header("Display")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;

    [Header("Minimap layout (screen space)")]
    [Tooltip("Pixel offset of minimap center from the top-left anchor of the canvas.")]
    [SerializeField] private Vector2 minimapScreenOffset = new Vector2(12f, -12f);
    [SerializeField] private Vector2 minimapSize = new Vector2(220f, 220f);

    [Header("Fullscreen map layout")]
    [Tooltip("Uniform inset from screen edges when map is fullscreen.")]
    [SerializeField] private float fullscreenEdgePadding = 0f;

    [Header("Map zoom (display)")]
    [Tooltip("1 = fit full bounds. Larger = zoom in (see less world, centered). Smaller = zoom out.")]
    [SerializeField] private float minimapMapZoom = 1f;
    [Tooltip("Fullscreen map zoom. Use a value > minimap zoom if the big map should feel more \"magnified\".")]
    [SerializeField] private float fullscreenMapZoom = 1f;

    [Header("Icon sizes (per view)")]
    [SerializeField] private Vector2 playerIconSizeMinimap = new Vector2(14f, 14f);
    [SerializeField] private Vector2 playerIconSizeFullscreen = new Vector2(28f, 28f);
    [SerializeField] private Vector2 objectiveMarkerSizeMinimap = new Vector2(10f, 10f);
    [SerializeField] private Vector2 objectiveMarkerSizeFullscreen = new Vector2(20f, 20f);

    [Header("Heading")]
    [Tooltip("UI icon heading offset in degrees. Use this if your icon's 'up' is not forward.")]
    [SerializeField] private float playerIconHeadingOffset = 0f;

    [Header("Player icon rendering")]
    [Tooltip("Keep player icon sprite aspect ratio to avoid stretching.")]
    [SerializeField] private bool preservePlayerIconAspect = true;

    [SerializeField] private Sprite playerIconSprite;
    [Tooltip("UI Image tint color applied to the player icon sprite. Use white to keep original sprite colors.")]
    [SerializeField] private Color playerIconTint = Color.white;
    [SerializeField] private Color activeMarkerColor = Color.red;
    [SerializeField] private Color completedMarkerColor = Color.green;

    private enum MapViewMode { Minimap, Fullscreen }
    private MapViewMode mode = MapViewMode.Minimap;

    private RectTransform miniRoot;
    private RectTransform fullRoot;
    /// <summary>Minimap mask viewport; markers use this rect for layout.</summary>
    private RectTransform miniViewport;
    private RectTransform miniMapLayer;
    private RectTransform fullViewport;
    private RectTransform fullMapLayer;
    private Image miniMapLayerImage;
    private Image fullMapLayerImage;
    private RectTransform miniPlayerIcon;
    private RectTransform fullPlayerIcon;
    private readonly Dictionary<string, RectTransform> miniMarkers = new Dictionary<string, RectTransform>();
    private readonly Dictionary<string, RectTransform> fullMarkers = new Dictionary<string, RectTransform>();

    private bool _uiBuilt;
    private bool _runtimeConfigInstance;
    private bool _mapVisible;

    private void Awake()
    {
        EnsureConfigAssigned();
    }

    private void EnsureConfigAssigned()
    {
        if (config != null) return;
        config = ScriptableObject.CreateInstance<MinimapConfig>();
        _runtimeConfigInstance = true;
        Debug.LogWarning(
            "[MinimapUiController] 未指定 MinimapConfig，已使用运行时临时默认（边界见脚本默认值，无地图贴图）。请创建资源：Create > Map System > Minimap Config，或使用 Tools > Map System > Auto Setup Minimap 一键绑定。",
            this
        );
    }

    private void Start()
    {
        BuildUi();
        if (objectiveProvider != null) objectiveProvider.ObjectivesChanged += RefreshMarkers;
        RefreshMarkers();
        ApplyMode();
        _mapVisible = false;
        ApplyVisibility(false);
        BackupInitialBlackScreen blacksScreen = Object.FindFirstObjectByType<BackupInitialBlackScreen>();
        if (blacksScreen != null) blacksScreen.onFadeOutComplete.AddListener(OnBlackScreenFadeOutComplete);
    }

    private void OnBlackScreenFadeOutComplete()
    {
        _mapVisible = true;
        ApplyVisibility(true);
        BackupInitialBlackScreen blacksScreen = Object.FindFirstObjectByType<BackupInitialBlackScreen>();
        if (blacksScreen != null) blacksScreen.onFadeOutComplete.RemoveListener(OnBlackScreenFadeOutComplete);
    }

    private void ApplyVisibility(bool visible)
    {
        if (miniRoot != null) miniRoot.gameObject.SetActive(visible && mode == MapViewMode.Minimap);
        if (fullRoot != null) fullRoot.gameObject.SetActive(visible && mode == MapViewMode.Fullscreen);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying || !_uiBuilt) return;
        ApplyLayoutAndIconSizes();
    }

    private void OnDestroy()
    {
        if (objectiveProvider != null) objectiveProvider.ObjectivesChanged -= RefreshMarkers;
        BackupInitialBlackScreen blacksScreen = Object.FindFirstObjectByType<BackupInitialBlackScreen>();
        if (blacksScreen != null) blacksScreen.onFadeOutComplete.RemoveListener(OnBlackScreenFadeOutComplete);
        if (_runtimeConfigInstance && config != null)
        {
            Destroy(config);
            config = null;
            _runtimeConfigInstance = false;
        }
    }

    private void Update()
    {
        if (config == null || playerTransform == null) return;
        if (Input.GetKeyDown(toggleKey)) PerformToggle();
        UpdateMapLayers();
        RefreshMarkers();
        UpdatePlayerIcons();
        RefreshMarkerPositions();
    }

    private void LateUpdate()
    {
        if (!_uiBuilt) return;
        ApplyViewportMapLayerZoom(miniViewport, miniMapLayer, miniMapLayerImage, minimapMapZoom);
        ApplyViewportMapLayerZoom(fullViewport, fullMapLayer, fullMapLayerImage, fullscreenMapZoom);
    }

    private void BuildUi()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) canvas = new GameObject("MinimapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        miniRoot = CreatePanel("MinimapRoot", canvas.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), minimapScreenOffset, minimapSize);
        fullRoot = CreatePanel("FullscreenMapRoot", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        (miniViewport, miniMapLayer, miniMapLayerImage) = CreateMapViewport("MiniMapImage", miniRoot, true);
        (fullViewport, fullMapLayer, fullMapLayerImage) = CreateMapViewport("FullMapImage", fullRoot, false);
        miniPlayerIcon = CreateIcon("MiniPlayerIcon", miniViewport, playerIconTint, playerIconPrefab, playerIconSprite, playerIconSizeMinimap);
        fullPlayerIcon = CreateIcon("FullPlayerIcon", fullViewport, playerIconTint, playerIconPrefab, playerIconSprite, playerIconSizeFullscreen);
        _uiBuilt = true;
        ApplyLayoutAndIconSizes();
    }

    private void ApplyLayoutAndIconSizes()
    {
        if (miniRoot != null)
        {
            miniRoot.anchoredPosition = minimapScreenOffset;
            miniRoot.sizeDelta = minimapSize;
        }

        if (fullRoot != null)
        {
            float p = Mathf.Max(0f, fullscreenEdgePadding);
            fullRoot.offsetMin = new Vector2(p, p);
            fullRoot.offsetMax = new Vector2(-p, -p);
        }

        if (miniPlayerIcon != null)
        {
            miniPlayerIcon.sizeDelta = playerIconSizeMinimap;
            ApplySpriteIfNeeded(miniPlayerIcon);
        }
        if (fullPlayerIcon != null)
        {
            fullPlayerIcon.sizeDelta = playerIconSizeFullscreen;
            ApplySpriteIfNeeded(fullPlayerIcon);
        }

        foreach (RectTransform rt in miniMarkers.Values)
            if (rt != null) rt.sizeDelta = objectiveMarkerSizeMinimap;
        foreach (RectTransform rt in fullMarkers.Values)
            if (rt != null) rt.sizeDelta = objectiveMarkerSizeFullscreen;

        ApplyViewportMapLayerZoom(miniViewport, miniMapLayer, miniMapLayerImage, minimapMapZoom);
        ApplyViewportMapLayerZoom(fullViewport, fullMapLayer, fullMapLayerImage, fullscreenMapZoom);
    }

    private void ApplySpriteIfNeeded(RectTransform icon)
    {
        if (icon == null) return;
        Image image = icon.GetComponent<Image>();
        if (image == null) return;
        image.preserveAspect = preservePlayerIconAspect;
        if (playerIconSprite != null) image.sprite = playerIconSprite;
    }

    private (RectTransform viewport, RectTransform mapLayer, Image mapLayerImage) CreateMapViewport(string name, Transform parent, bool circular)
    {
        RectTransform viewport = CreatePanel(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image bg = viewport.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);

        RectTransform mapLayer = new GameObject(name + "_MapLayer", typeof(RectTransform)).GetComponent<RectTransform>();
        mapLayer.SetParent(viewport, false);
        mapLayer.anchorMin = mapLayer.anchorMax = new Vector2(0.5f, 0.5f);
        mapLayer.pivot = new Vector2(0.5f, 0.5f);
        mapLayer.anchoredPosition = Vector2.zero;
        mapLayer.sizeDelta = Vector2.zero;

        Image mapLayerImage = mapLayer.gameObject.AddComponent<Image>();
        mapLayerImage.sprite = config != null ? config.MapSprite : null;
        mapLayerImage.color = mapLayerImage.sprite == null ? new Color(0.1f, 0.1f, 0.1f, 0.8f) : Color.white;
        mapLayerImage.preserveAspect = true;

        if (circular)
        {
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            bg.sprite = config != null && config.CircularMaskSprite != null
                ? config.CircularMaskSprite
                : Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            bg.type = Image.Type.Sliced;
        }

        return (viewport, mapLayer, mapLayerImage);
    }

    private static Vector2 ComputeLayerSize(RectTransform viewport, Image mapLayerImage, float mapZoom)
    {
        Vector2 viewSize = viewport.rect.size;
        float z = Mathf.Max(0.01f, mapZoom);
        if (mapLayerImage == null || mapLayerImage.sprite == null) return new Vector2(viewSize.x * z, viewSize.y * z);

        Rect sr = mapLayerImage.sprite.rect;
        float spriteAspect = Mathf.Max(0.0001f, sr.width / sr.height);
        float viewAspect = Mathf.Max(0.0001f, viewSize.x / Mathf.Max(0.0001f, viewSize.y));

        float baseW;
        float baseH;
        if (spriteAspect >= viewAspect)
        {
            baseH = viewSize.y;
            baseW = baseH * spriteAspect;
        }
        else
        {
            baseW = viewSize.x;
            baseH = baseW / spriteAspect;
        }

        return new Vector2(baseW * z, baseH * z);
    }

    private static void ApplyViewportMapLayerZoom(RectTransform viewport, RectTransform mapLayer, Image mapLayerImage, float mapZoom)
    {
        if (viewport == null || mapLayer == null) return;
        Vector2 viewSize = viewport.rect.size;
        if (viewSize.x < 0.5f || viewSize.y < 0.5f) return;
        Vector2 target = ComputeLayerSize(viewport, mapLayerImage, mapZoom);
        if ((mapLayer.sizeDelta - target).sqrMagnitude > 0.01f) mapLayer.sizeDelta = target;
    }

    private RectTransform CreatePanel(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        return rect;
    }

    private RectTransform CreateIcon(string name, RectTransform parent, Color color, RectTransform prefab, Sprite spriteOverride, Vector2 size)
    {
        RectTransform icon = prefab != null ? Instantiate(prefab, parent) : new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        icon.SetParent(parent, false);
        Image image = icon.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
            if (spriteOverride != null) image.sprite = spriteOverride;
        }
        icon.sizeDelta = size;
        return icon;
    }

    private void UpdatePlayerIcons()
    {
        Vector2 normalized = config.WorldToNormalized(playerTransform.position);
        if (miniPlayerIcon != null) miniPlayerIcon.anchoredPosition = Vector2.zero;

        Vector2 fullLayerSize = fullMapLayer != null ? fullMapLayer.sizeDelta : fullViewport.rect.size;
        if (fullPlayerIcon != null) fullPlayerIcon.anchoredPosition = ToAnchored(fullLayerSize, normalized);

        float heading = -playerTransform.eulerAngles.y + playerIconHeadingOffset;
        if (miniPlayerIcon != null) miniPlayerIcon.localEulerAngles = new Vector3(0f, 0f, heading);
        if (fullPlayerIcon != null) fullPlayerIcon.localEulerAngles = new Vector3(0f, 0f, heading);
    }

    private void UpdateMapLayers()
    {
        if (miniMapLayer != null)
        {
            Vector2 playerNormalized = config.WorldToNormalized(playerTransform.position);
            Vector2 miniLayerSize = miniMapLayer.sizeDelta.sqrMagnitude > 0.01f ? miniMapLayer.sizeDelta : miniViewport.rect.size;
            miniMapLayer.anchoredPosition = -ToAnchored(miniLayerSize, playerNormalized);
        }

        if (fullMapLayer != null)
        {
            fullMapLayer.anchoredPosition = Vector2.zero;
        }
    }

    private void RefreshMarkers()
    {
        if (objectiveProvider == null) return;
        SyncMarkerSet(miniMarkers, miniViewport, objectiveMarkerSizeMinimap);
        SyncMarkerSet(fullMarkers, fullViewport, objectiveMarkerSizeFullscreen);
        RefreshMarkerPositions();
    }

    private void SyncMarkerSet(Dictionary<string, RectTransform> set, RectTransform parent, Vector2 markerSize)
    {
        foreach (MissionObjectiveEntry entry in objectiveProvider.Objectives)
        {
            if (string.IsNullOrEmpty(entry.id) || entry.target == null) continue;
            if (!set.TryGetValue(entry.id, out RectTransform marker))
            {
                marker = CreateIcon("Marker_" + entry.id, parent, activeMarkerColor, markerPrefab, null, markerSize);
                set[entry.id] = marker;
            }
            else marker.sizeDelta = markerSize;
            Image image = marker.GetComponent<Image>();
            marker.gameObject.SetActive(entry.state != MissionObjectiveState.Hidden);
            if (image != null) image.color = entry.state == MissionObjectiveState.Completed ? completedMarkerColor : activeMarkerColor;
        }
    }

    private void RefreshMarkerPositions()
    {
        if (objectiveProvider == null || config == null) return;
        foreach (MissionObjectiveEntry entry in objectiveProvider.Objectives)
        {
            if (entry.target == null || !miniMarkers.ContainsKey(entry.id) || !fullMarkers.ContainsKey(entry.id)) continue;
            Vector2 playerNormalized = config.WorldToNormalized(playerTransform.position);
            Vector2 objectiveNormalized = config.WorldToNormalized(entry.target.position);

            Vector2 miniLayerSize = miniMapLayer != null ? miniMapLayer.sizeDelta : miniViewport.rect.size;
            Vector2 fullLayerSize = fullMapLayer != null ? fullMapLayer.sizeDelta : fullViewport.rect.size;

            Vector2 playerPosInMiniLayer = ToAnchored(miniLayerSize, playerNormalized);
            Vector2 objectivePosInMiniLayer = ToAnchored(miniLayerSize, objectiveNormalized);
            miniMarkers[entry.id].anchoredPosition = objectivePosInMiniLayer - playerPosInMiniLayer;

            fullMarkers[entry.id].anchoredPosition = ToAnchored(fullLayerSize, objectiveNormalized);
        }
    }

    /// <summary>
    /// Same view change as one press of <see cref="toggleKey"/> (default M). Safe to call from gameplay / UI events.
    /// </summary>
    public void PerformToggle()
    {
        mode = mode == MapViewMode.Minimap ? MapViewMode.Fullscreen : MapViewMode.Minimap;
        ApplyMode();
    }

    public void ShowMap()
    {
        if (!_mapVisible)
        {
            _mapVisible = true;
            ApplyVisibility(true);
        }
    }

    public void HideMap()
    {
        if (_mapVisible)
        {
            _mapVisible = false;
            ApplyVisibility(false);
        }
    }

    private void ApplyMode()
    {
        bool visible = _mapVisible;
        if (miniRoot != null) miniRoot.gameObject.SetActive(visible && mode == MapViewMode.Minimap);
        if (fullRoot != null) fullRoot.gameObject.SetActive(visible && mode == MapViewMode.Fullscreen);
    }

    /// <summary>
    /// Map layer and markers share the same zoom: &gt;1 enlarges content around center (zoom in); &lt;1 shrinks (zoom out).
    /// </summary>
    private static Vector2 ToAnchored(Vector2 layerSize, Vector2 normalized)
    {
        return new Vector2((normalized.x - 0.5f) * layerSize.x, (normalized.y - 0.5f) * layerSize.y);
    }
}
