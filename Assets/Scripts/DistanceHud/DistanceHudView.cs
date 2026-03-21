using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom strip HUD: composite mask (bottom solid, top transparent) × horizontal center falloff;
/// breathing on overall alpha plus horizontal scale pulse from screen center (center → sides).
/// </summary>
/// <remarks>
/// URP / UI: no GrabPass blur; <see cref="useFrostedLayerFallback"/> stacks soft layers.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class DistanceHudView : MonoBehaviour
{
    [Header("Distance source")]
    [Tooltip("If set, must implement IDistanceHudSource. Otherwise the first enabled IDistanceHudSource on this GameObject is used.")]
    [SerializeField] MonoBehaviour distanceSourceOverride;

    [Header("Thresholds (meters)")]
    [SerializeField] float whiteAboveMeters = 300f;
    [SerializeField] float yellowBelowMeters = 150f;
    [SerializeField] float redBelowMeters = 50f;

    [Header("Colors")]
    [SerializeField] Color whiteTone = new Color(0.95f, 0.97f, 1f, 1f);
    [SerializeField] Color yellowTone = new Color(1f, 0.92f, 0.35f, 1f);
    [SerializeField] Color redTone = new Color(1f, 0.25f, 0.2f, 1f);

    [Header("Layout")]
    [SerializeField, Range(0.04f, 0.35f)] float stripHeightFraction = 0.12f;

    [Header("Horizontal shape")]
    [Tooltip("Higher = brighter core stays closer to screen center before fading to sides.")]
    [SerializeField, Range(1f, 5f)] float horizontalCenterFocus = 2.4f;

    [Header("Breathing")]
    [Tooltip("When on, breath period goes from slow (far) to fast (near) using distance thresholds below.")]
    [SerializeField] bool useDistanceBasedBreathingPeriod = true;
    [Tooltip("Used when distance-based period is off.")]
    [SerializeField] float breathingPeriodSeconds = 3f;
    [Tooltip("Breath period when distance ≥ white threshold (calm).")]
    [SerializeField] float breathingPeriodFarSeconds = 4f;
    [Tooltip("Breath period when distance ≤ red threshold (urgent).")]
    [SerializeField] float breathingPeriodNearSeconds = 0.65f;
    [Tooltip("Smooths the runtime breathing period when distance changes (e.g. while flying). Reduces flicker from period jumping every frame. 0 = follow target instantly.")]
    [SerializeField] float breathingPeriodSmoothTime = 0.2f;
    [Tooltip("Modulates strip CanvasGroup alpha (overall brightness pulse).")]
    [SerializeField, Range(0f, 0.35f)] float breathingAmplitude = 0.06f;
    [Tooltip("Horizontal scale pulse from center (pivot X=0.5): simulates light spreading to sides.")]
    [SerializeField, Range(0f, 0.25f)] float horizontalBreathingAmplitude = 0.12f;
    [SerializeField, Range(0.1f, 1f)] float baseStripAlpha = 0.55f;

    [Header("Blur / frosted fallback")]
    [Tooltip("True: extra soft layers behind the main strip (no true GPU blur; URP-safe). False: single main layer.")]
    [SerializeField] bool useFrostedLayerFallback = true;

    [Header("Optional refs (auto-built if null)")]
    [SerializeField] CanvasGroup stripCanvasGroup;
    [SerializeField] Image mainGlowImage;
    [Tooltip("Legacy: old prefabs had a separate vertical mask; composite strip makes this unused.")]
    [SerializeField] Image verticalFadeImage;
    [SerializeField] Image frostedA;
    [SerializeField] Image frostedB;

    const float FrostedScaleAX = 1.04f;
    const float FrostedScaleBX = 1.08f;

    IDistanceHudSource _source;
    Sprite _stripSprite;
    bool _ownsRuntimeSprites;
    float _cachedHorizontalFocus = -999f;
    float _breathingPhase;
    float _smoothedBreathingPeriodSeconds = -1f;
    float _breathingPeriodSmoothVelocity;

    void Awake()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, stripHeightFraction);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        EnsureBuilt();
        if (verticalFadeImage != null)
            verticalFadeImage.gameObject.SetActive(false);
        _cachedHorizontalFocus = horizontalCenterFocus;
        _smoothedBreathingPeriodSeconds = Mathf.Max(0.08f,
            useDistanceBasedBreathingPeriod ? breathingPeriodFarSeconds : breathingPeriodSeconds);
        ResolveSource();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying || !_ownsRuntimeSprites)
            return;
        if (Mathf.Approximately(horizontalCenterFocus, _cachedHorizontalFocus))
            return;
        RebuildStripSprite();
        _cachedHorizontalFocus = horizontalCenterFocus;
    }
#endif

    void OnDestroy()
    {
        if (!_ownsRuntimeSprites)
            return;
        if (_stripSprite != null && _stripSprite.texture != null)
            Destroy(_stripSprite.texture);
        if (_stripSprite != null)
            Destroy(_stripSprite);
    }

    void Update()
    {
        if (_source == null)
            ResolveSource();
        float d = _source != null ? _source.GetDistanceMeters() : 9999f;
        Color tint = DistanceHudColorMapper.Evaluate(
            d, whiteAboveMeters, yellowBelowMeters, redBelowMeters,
            whiteTone, yellowTone, redTone);

        float periodTarget = Mathf.Max(0.08f, GetBreathingPeriodSeconds(d));
        if (_smoothedBreathingPeriodSeconds < 0f)
            _smoothedBreathingPeriodSeconds = periodTarget;
        if (breathingPeriodSmoothTime <= 0.0001f)
        {
            _smoothedBreathingPeriodSeconds = periodTarget;
            _breathingPeriodSmoothVelocity = 0f;
        }
        else
        {
            _smoothedBreathingPeriodSeconds = Mathf.SmoothDamp(
                _smoothedBreathingPeriodSeconds,
                periodTarget,
                ref _breathingPeriodSmoothVelocity,
                breathingPeriodSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
        }

        float dt = Time.unscaledDeltaTime;
        _breathingPhase += dt * (2f * Mathf.PI / _smoothedBreathingPeriodSeconds);

        float breathAlpha = 1f + breathingAmplitude * Mathf.Sin(_breathingPhase);
        float alpha = Mathf.Clamp01(baseStripAlpha * breathAlpha);

        float spread = 1f + horizontalBreathingAmplitude * Mathf.Sin(_breathingPhase);

        if (stripCanvasGroup != null)
            stripCanvasGroup.alpha = alpha;

        if (mainGlowImage != null)
        {
            var c = tint;
            c.a = 1f;
            mainGlowImage.color = c;
            var mrt = mainGlowImage.rectTransform;
            mrt.localScale = new Vector3(spread, 1f, 1f);
        }

        if (frostedA != null)
            frostedA.rectTransform.localScale = new Vector3(FrostedScaleAX * spread, 1f, 1f);
        if (frostedB != null)
            frostedB.rectTransform.localScale = new Vector3(FrostedScaleBX * spread, 1f, 1f);
    }

    /// <summary>
    /// Far → longer period (slow pulse); near → shorter period (fast pulse). Uses same meters thresholds as tint.
    /// </summary>
    float GetBreathingPeriodSeconds(float distanceMeters)
    {
        if (!useDistanceBasedBreathingPeriod)
            return Mathf.Max(0.05f, breathingPeriodSeconds);
        float u = Mathf.InverseLerp(whiteAboveMeters, redBelowMeters, distanceMeters);
        u = Mathf.Clamp01(u);
        return Mathf.Lerp(
            Mathf.Max(0.05f, breathingPeriodFarSeconds),
            Mathf.Max(0.05f, breathingPeriodNearSeconds),
            u);
    }

    void RebuildStripSprite()
    {
        if (_stripSprite != null && _stripSprite.texture != null)
            Destroy(_stripSprite.texture);
        if (_stripSprite != null)
            Destroy(_stripSprite);
        _stripSprite = DistanceHudTextures.CreateCompositeStripSprite(256, 96, horizontalCenterFocus);
        if (mainGlowImage != null)
            mainGlowImage.sprite = _stripSprite;
        if (frostedA != null)
            frostedA.sprite = _stripSprite;
        if (frostedB != null)
            frostedB.sprite = _stripSprite;
    }

    void ResolveSource()
    {
        if (distanceSourceOverride != null && distanceSourceOverride is IDistanceHudSource s)
        {
            _source = s;
            return;
        }
        var toTarget = GetComponent<DistanceToTargetSource>();
        if (toTarget != null && toTarget.enabled)
        {
            _source = toTarget;
            return;
        }
        var forward = GetComponent<ForwardRaycastDistanceSource>();
        if (forward != null && forward.enabled)
        {
            _source = forward;
            return;
        }
        var dev = GetComponent<DevelopmentDistanceHudSource>();
        if (dev != null && dev.enabled)
        {
            _source = dev;
            return;
        }
        var constant = GetComponent<ConstantDistanceHudSource>();
        if (constant != null && constant.enabled)
        {
            _source = constant;
            return;
        }
        _source = null;
        foreach (var mb in GetComponents<MonoBehaviour>())
        {
            if (!mb.enabled || mb == this)
                continue;
            if (mb is IDistanceHudSource src)
            {
                _source = src;
                return;
            }
        }
    }

    void EnsureBuilt()
    {
        bool needStrip = mainGlowImage == null
            || (useFrostedLayerFallback && frostedA == null);
        if (needStrip && _stripSprite == null)
        {
            _stripSprite = DistanceHudTextures.CreateCompositeStripSprite(256, 96, horizontalCenterFocus);
            _ownsRuntimeSprites = true;
        }

        if (stripCanvasGroup == null)
            stripCanvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (stripCanvasGroup == null)
            stripCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        stripCanvasGroup.blocksRaycasts = false;
        stripCanvasGroup.interactable = false;

        if (useFrostedLayerFallback && frostedA == null && _stripSprite != null)
        {
            frostedA = CreateFrostedLayer("FrostedA", FrostedScaleAX, new Color(1f, 1f, 1f, 0.12f));
            frostedB = CreateFrostedLayer("FrostedB", FrostedScaleBX, new Color(1f, 1f, 1f, 0.08f));
        }

        if (mainGlowImage == null)
        {
            var go = new GameObject("MainGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            mainGlowImage = go.GetComponent<Image>();
            mainGlowImage.sprite = _stripSprite;
            mainGlowImage.type = Image.Type.Simple;
            mainGlowImage.preserveAspect = false;
            mainGlowImage.raycastTarget = false;
        }
        if (mainGlowImage != null && _stripSprite != null)
            mainGlowImage.sprite = _stripSprite;

        if (frostedA != null && _stripSprite != null)
            frostedA.sprite = _stripSprite;
        if (frostedB != null && _stripSprite != null)
            frostedB.sprite = _stripSprite;

        SortChildrenForDrawOrder();
    }

    void SortChildrenForDrawOrder()
    {
        if (frostedA != null) frostedA.transform.SetAsFirstSibling();
        if (frostedB != null) frostedB.transform.SetSiblingIndex(frostedA != null ? 1 : 0);
        if (mainGlowImage != null) mainGlowImage.transform.SetSiblingIndex(frostedB != null ? 2 : frostedA != null ? 1 : 0);
        if (verticalFadeImage != null) verticalFadeImage.transform.SetAsLastSibling();
    }

    Image CreateFrostedLayer(string name, float scaleX, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        StretchFull(rt);
        rt.localScale = new Vector3(scaleX, 1f, 1f);
        var img = go.GetComponent<Image>();
        img.sprite = _stripSprite;
        img.type = Image.Type.Simple;
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.localScale = Vector3.one;
    }
}
